using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;
using Rootborn.Data;
using Rootborn.Data.Builders;
using Rootborn.Data.Save;
using Rootborn.Game;
using Rootborn.Net.Room;
using Rootborn.Net.Transport;

namespace Rootborn.Server
{
    /// <summary>
    /// 데디케이티드 서버의 입구: 인자 읽기 → 데이터 읽기(오류면 목록을 찍고 끝) → 슬롯 열기 → UDP로 듣기 → 고정 간격 루프 → 끝낼 때 저장하고 닫기.
    /// 방의 논리는 전부 Rootborn.Net/Room(<see cref="RoomServer"/>)에 있다. 로그는 운영자가 읽는 것이라 현지화하지 않는다.
    /// </summary>
    public static class Program
    {
        public const int ExitOk = 0;
        public const int ExitBadArguments = 2;
        public const int ExitBadData = 3;
        public const int ExitBadSave = 4;
        public const int ExitCannotListen = 5;

        /// <summary>로그 줄 맨 앞에 찍히는 듣는 곳 알림. 포트 0으로 띄운 쪽(테스트)이 이 줄에서 실제 포트를 읽는다.</summary>
        public const string ListeningPrefix = "듣는 중: ";

        /// <summary>콘솔에 이 줄을 넣으면 저장하고 끝낸다.</summary>
        public const string StopCommand = "stop";

        public static int Main(string[] args)
        {
            // 로그를 파일·파이프로 받아도 한글이 깨지지 않게 운영체제와 무관하게 UTF-8로 쓴다.
            Console.OutputEncoding = new System.Text.UTF8Encoding(false);
            Console.InputEncoding = new System.Text.UTF8Encoding(false);
            ServerOptions options;
            try
            {
                options = ServerOptions.Resolve(args);
            }
            catch (ServerOptionsException e)
            {
                Console.Error.WriteLine(e.Message);
                Console.Error.WriteLine();
                Console.Error.WriteLine(ServerOptions.HelpText);
                return ExitBadArguments;
            }

            if (options.HelpRequested)
            {
                Console.Out.WriteLine(ServerOptions.HelpText);
                return ExitOk;
            }

            using var stop = new CancellationTokenSource();
            using var sigint = PosixSignalRegistration.Create(PosixSignal.SIGINT, Stop);
            using var sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, Stop);
            using var sigquit = PosixSignalRegistration.Create(PosixSignal.SIGQUIT, Stop);
            var input = new Thread(() => WatchConsole(stop)) { IsBackground = true, Name = "console" };
            input.Start();
            return Run(options, Console.Out, stop.Token);

            void Stop(PosixSignalContext context)
            {
                context.Cancel = true; // 바로 죽지 않고 루프가 저장하고 끝내게
                stop.Cancel();
            }
        }

        /// <summary>서버 한 번 돌리기. 취소되면 저장하고 닫은 뒤 돌아온다.</summary>
        public static int Run(ServerOptions options, TextWriter log, CancellationToken stop)
        {
            void Log(string line) => log.WriteLine(DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + " " + line);

            Log("데이터 읽는 중: " + options.DataDirectory);
            var load = GameDataLoader.Load(options.DataDirectory, RootbornBuilders.Register);
            if (!load.Ok)
            {
                if (load.UnclassifiedTypes.Count > 0) Log("분류 안 된 종류: " + string.Join(", ", load.UnclassifiedTypes));
                foreach (var error in load.Errors) Log("데이터 오류: " + error);
                Log($"게임 데이터 오류 {load.Errors.Count}건, 분류 안 된 종류 {load.UnclassifiedTypes.Count}개 — 시작하지 않는다");
                return ExitBadData;
            }

            var versions = CurrentVersions(options.DataDirectory);
            ISaveStore store = new FileSaveStore(options.SaveRoot);
            UdpTransport transport;
            try
            {
                transport = UdpTransport.Listen(options.BindAddress, options.Port, RoomProtocol.ConnectionKey);
            }
            catch (Exception e) when (e is InvalidOperationException || e is System.Net.Sockets.SocketException)
            {
                Log("들을 수 없다: " + e.Message);
                return ExitCannotListen;
            }

            var settings = new RoomServerSettings { MaxPlayers = options.MaxPlayers, SharedPieces = options.SharedPieces, DayCopiesToKeep = options.DayCopies };
            var guests = new GuestSaveFolder(GuestSaveFolder.FolderFor(options.SaveRoot, options.SaveSlot), load.Definitions,
                GameStartSettings.FromDefinitions(load.Definitions), Log);
            RoomServer room;
            try
            {
                room = new RoomServer(transport, settings, versions, store, options.SaveSlot, Log, RoomCalendar(load.Definitions), guests);
            }
            catch (Exception e) when (e is SaveFileCorruptException || e is SaveFileTooNewException || e is ArgumentException)
            {
                transport.Dispose();
                Log("세이브를 읽을 수 없다(덮어쓰지 않는다): " + e.Message);
                return ExitBadSave;
            }

            Log(ListeningPrefix + options.BindAddress + ":" + transport.LocalPort.ToString(CultureInfo.InvariantCulture));
            var loop = new RoomLoop(room, new StopwatchRoomClock(), settings.TickSeconds);
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    loop.RunDue();
                    double wait = loop.SecondsUntilNextTick;
                    if (wait > 0) stop.WaitHandle.WaitOne(TimeSpan.FromSeconds(wait));
                }
            }
            finally
            {
                Log("끝내는 중");
                room.Shutdown();
            }

            return ExitOk;
        }

        /// <summary>
        /// 방의 시계가 쓸 달력: 게임 한 판(GameSession)과 같은 정의(registry.json 기본값의 달력 id). 대표 시각은 아직 데이터에 없어 달력의 기본값이다.
        /// </summary>
        public static LifeCalendar RoomCalendar(GameDefinitions definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            return definitions.Get<LifeCalendar>(GameStartSettings.FromDefinitions(definitions).CalendarId);
        }

        /// <summary>
        /// 이 서버의 버전: 프로토콜, 게임 프로그램(Rootborn.Game 어셈블리의 정보 버전 — 같은 커밋에서 빌드하면 같다), 데이터 지문.
        /// </summary>
        public static RoomVersions CurrentVersions(string dataDirectory)
            => RoomVersions.ForProgram(typeof(GameSession).Assembly, Rootborn.Data.DataFingerprint.Compute(dataDirectory));

        private static void WatchConsole(CancellationTokenSource stop)
        {
            try
            {
                string line;
                // 입력이 닫히면(null) 서비스로 돈 것이라 보고 끝내지 않는다. stop 줄만 끝낸다.
                while ((line = Console.In.ReadLine()) != null)
                {
                    if (string.Equals(line.Trim(), StopCommand, StringComparison.OrdinalIgnoreCase))
                    {
                        stop.Cancel();
                        return;
                    }
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (IOException)
            {
            }
        }
    }
}
