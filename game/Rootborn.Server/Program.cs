using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Rootborn.Core.Save;
using Rootborn.Core.Time;
using Rootborn.Data;
using Rootborn.Data.Builders;
using Rootborn.Data.Save;
using Rootborn.Game;
using Rootborn.Net.Room;
using Rootborn.Net.Transport;

namespace Rootborn.Server
{
    /// <summary>
    /// 데디케이티드 서버의 입구: 인자 읽기 → 로그 열기 → 데이터 읽기(오류면 목록을 찍고 끝) → 슬롯 잠그기 → UDP로 듣기 → 슬롯 열기 →
    /// 고정 간격 루프(콘솔 명령, 주기 저장 — <see cref="ServerRunner"/>) → 끝낼 때 저장하고 닫기.
    /// 방의 논리는 전부 Rootborn.Net/Room(<see cref="RoomServer"/>)에 있다. 로그는 운영자가 읽는 것이라 현지화하지 않는다.
    /// 사람이 고쳐야 하는 오류는 끝 코드 하나와 무엇을 고치면 되는지 한 줄로 알린다.
    /// </summary>
    public static class Program
    {
        public const int ExitOk = 0;
        public const int ExitBadArguments = 2;
        public const int ExitBadData = 3;
        public const int ExitBadSave = 4;
        public const int ExitCannotListen = 5;

        /// <summary>그 슬롯을 다른 서버나 내 PC의 방이 이미 열어 두었다(슬롯 잠금 — <see cref="RoomSlotLock"/>).</summary>
        public const int ExitSlotInUse = 6;

        /// <summary>세이브 폴더(또는 로그 파일)에 쓸 수 없다.</summary>
        public const int ExitCannotWrite = 7;

        /// <summary>도는 중에 예상하지 못한 오류(저장은 해 보고 끝낸다).</summary>
        public const int ExitCrashed = 8;

        /// <summary>로그 줄 맨 앞에 찍히는 듣는 곳 알림. 포트 0으로 띄운 쪽(테스트)이 이 줄에서 실제 포트를 읽는다.</summary>
        public const string ListeningPrefix = "듣는 중: ";

        /// <summary>콘솔에 이 줄을 넣으면 저장하고 끝낸다(명령 표 <see cref="ServerCommands"/>의 하나).</summary>
        public const string StopCommand = "stop";

        /// <summary>시작이 끝나 콘솔 명령을 받는다는 줄.</summary>
        public const string ReadyLine = "콘솔 명령을 받는다: help 로 목록을 본다";

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

            ServerLog log;
            try
            {
                log = new ServerLog(Console.Out, options.LogFile, options.LogMaxBytes, options.LogFiles);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Console.Error.WriteLine("로그 파일을 열 수 없다: " + options.LogFile + " — 그 폴더에 쓸 수 있는지 보거나 --log 에 다른 파일을 준다 (" + e.Message + ")");
                return ExitCannotWrite;
            }

            using (log)
            {
                using var stop = new CancellationTokenSource();
                using var sigint = PosixSignalRegistration.Create(PosixSignal.SIGINT, Stop);
                using var sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, Stop);
                using var sigquit = PosixSignalRegistration.Create(PosixSignal.SIGQUIT, Stop);
                var lines = new BlockingCollection<string>();
                var input = new Thread(() => WatchConsole(lines)) { IsBackground = true, Name = "console" };
                input.Start();
                return Run(options, log.Write, stop.Token, lines);

                void Stop(PosixSignalContext context)
                {
                    context.Cancel = true; // 바로 죽지 않고 루프가 저장하고 끝내게
                    log.Write("종료 신호를 받음: " + context.Signal);
                    stop.Cancel();
                }
            }
        }

        /// <summary>서버 한 번 돌리기(로그는 TextWriter로 — 시각을 붙인다). 취소되면 저장하고 닫은 뒤 돌아온다.</summary>
        public static int Run(ServerOptions options, TextWriter log, CancellationToken stop)
        {
            using var serverLog = new ServerLog(log);
            return Run(options, serverLog.Write, stop, null);
        }

        /// <summary>서버 한 번 돌리기. 취소되거나 stop 명령이 오면 저장하고 닫은 뒤 돌아온다.</summary>
        /// <param name="log">운영 로그 한 줄 쓰기(시각은 쓰는 쪽이 붙인다 — <see cref="ServerLog"/>).</param>
        /// <param name="console">콘솔 줄(명령). 없으면 명령을 받지 않는다.</param>
        public static int Run(ServerOptions options, Action<string> log, CancellationToken stop, BlockingCollection<string> console)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (log == null) throw new ArgumentNullException(nameof(log));
            log("데이터 읽는 중: " + options.DataDirectory);
            var load = GameDataLoader.Load(options.DataDirectory, RootbornBuilders.Register);
            if (!load.Ok)
            {
                if (load.UnclassifiedTypes.Count > 0) log("분류 안 된 종류: " + string.Join(", ", load.UnclassifiedTypes));
                foreach (var error in load.Errors) log("데이터 오류: " + error);
                log($"게임 데이터 오류 {load.Errors.Count}건, 분류 안 된 종류 {load.UnclassifiedTypes.Count}개 — 시작하지 않는다");
                return ExitBadData;
            }

            // 슬롯 잠금: 같은 세이브 폴더·슬롯을 다른 서버나 내 PC의 방이 열어 두었으면 아무것도 읽거나 쓰지 않고 끝낸다.
            RoomSlotLock slotLock;
            try
            {
                slotLock = RoomSlotLock.Acquire(options.SaveRoot, options.SaveSlot, "데디케이티드 서버");
            }
            catch (RoomSlotLockedException e)
            {
                log("시작하지 않는다: " + e.Message + " — 그 서버(또는 게임에서 연 방)를 먼저 끄거나 --slot 에 다른 슬롯을 준다");
                return ExitSlotInUse;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                log(CannotWrite(options, e));
                return ExitCannotWrite;
            }

            using (slotLock)
            {
                if (slotLock.PreviousOwnerCrashed) log("지난번에 끝내지 못하고 꺼진 서버의 잠금을 넘어감: " + slotLock.PreviousOwner);
                return RunLocked(options, log, stop, console, load);
            }
        }

        private static int RunLocked(ServerOptions options, Action<string> log, CancellationToken stop, BlockingCollection<string> console, LoadResult load)
        {
            var versions = CurrentVersions(options.DataDirectory);
            var store = new ObservedSaveStore(new FileSaveStore(options.SaveRoot));
            UdpTransport transport;
            try
            {
                transport = UdpTransport.Listen(options.BindAddress, options.Port, RoomProtocol.ConnectionKey);
            }
            catch (Exception e) when (e is InvalidOperationException || e is System.Net.Sockets.SocketException)
            {
                log("들을 수 없다: " + ListenProblem.Explain(options.BindAddress, options.Port));
                return ExitCannotListen;
            }

            var settings = new RoomServerSettings { MaxPlayers = options.MaxPlayers, SharedPieces = options.SharedPieces, DayCopiesToKeep = options.DayCopies };
            var guests = new GuestSaveFolder(GuestSaveFolder.FolderFor(options.SaveRoot, options.SaveSlot), GameSession.SaveCheck(load.Definitions,
                GameStartSettings.FromDefinitions(load.Definitions)), log);
            RoomServer room;
            try
            {
                // 같이 쓰는 집의 판정 칸은 데이터 폴더 옆의 game/world와 그 맵(LDtk)에서 처음 물을 때 읽는다(HouseGrids).
                var pieces = Rootborn.Core.Room.RoomPieceCatalog.CreateDefault(HouseGrids.Rules(load.Definitions, RepoRootOf(options.DataDirectory), log));
                room = new RoomServer(transport, settings, versions, store, options.SaveSlot, log, RoomTimeRules(load.Definitions), guests, pieces);
            }
            catch (Exception e) when (e is SaveFileCorruptException || e is SaveFileTooNewException || e is ArgumentException)
            {
                transport.Dispose();
                log("세이브를 읽을 수 없다(덮어쓰지 않는다): " + e.Message);
                return ExitBadSave;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                transport.Dispose();
                log(CannotWrite(options, e));
                return ExitCannotWrite;
            }

            log(ListeningPrefix + options.BindAddress + ":" + transport.LocalPort.ToString(CultureInfo.InvariantCulture));
            var runner = new ServerRunner(room, new StopwatchRoomClock(), settings.TickSeconds, options.SaveSlot, store, options.AutosaveSeconds, log);
            if (console != null) log(ReadyLine);
            int exit = ExitOk;
            try
            {
                while (!stop.IsCancellationRequested && !runner.StopRequested)
                {
                    if (console != null)
                        while (console.TryTake(out var line)) runner.Enqueue(line);
                    runner.RunDue();
                    double wait = runner.SecondsUntilNextTick;
                    if (wait > 0 && !runner.StopRequested) stop.WaitHandle.WaitOne(TimeSpan.FromSeconds(wait));
                }
            }
            catch (Exception e)
            {
                log("오류: 도는 중에 멈췄다 — 저장하고 끝낸다: " + e);
                exit = ExitCrashed;
            }
            finally
            {
                log("끝내는 중");
                try
                {
                    room.Shutdown();
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    log("오류: 끝낼 때 저장하지 못했다 — 마지막으로 저장한 상태가 남는다: " + e.Message);
                    if (exit == ExitOk) exit = ExitCannotWrite;
                }
            }

            return exit;
        }

        private static string CannotWrite(ServerOptions options, Exception e)
            => "세이브 폴더에 쓸 수 없다: " + Path.Combine(options.SaveRoot, options.SaveSlot)
               + " — 그 폴더의 권한과 남은 공간을 보거나 --save-root 에 쓸 수 있는 폴더를 준다 (" + e.Message + ")";

        /// <summary>
        /// 방의 시계가 쓸 시간 규칙: 게임 한 판(GameSession)과 같은 정의(game/time.json + data/time). 하루 길이가 없는 데이터면 null(시계가 흐르지 않는다).
        /// </summary>
        public static TimeRules RoomTimeRules(GameDefinitions definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            return definitions.TimeRules;
        }

        /// <summary>데이터 폴더의 부모(저장소 또는 배포 폴더의 맨 위 — game/world가 그 아래에 있다).</summary>
        public static string RepoRootOf(string dataDirectory)
            => Path.GetDirectoryName(Path.GetFullPath(dataDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? ".";

        /// <summary>
        /// 이 서버의 버전: 프로토콜, 게임 프로그램(Rootborn.Game 어셈블리의 정보 버전 — 같은 커밋에서 빌드하면 같다), 데이터 지문.
        /// </summary>
        public static RoomVersions CurrentVersions(string dataDirectory)
            => RoomVersions.ForProgram(typeof(GameSession).Assembly, Rootborn.Data.DataFingerprint.Compute(dataDirectory));

        // 콘솔 줄을 루프에 넘긴다(명령은 루프 스레드가 돌린다 — ServerRunner). 입력이 닫히면(null) 서비스로 돈 것이라 보고 끝내지 않는다.
        private static void WatchConsole(BlockingCollection<string> lines)
        {
            try
            {
                string line;
                while ((line = Console.In.ReadLine()) != null) lines.Add(line);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (IOException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }
    }
}
