using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using NUnit.Framework;
using Rootborn.Core.Room;
using Rootborn.Core.Save;
using Rootborn.Core.Time;
using Rootborn.Data;
using Rootborn.Data.Builders;
using Rootborn.Data.Save;
using Rootborn.Game;
using Rootborn.Net.Room;
using Rootborn.Net.Session;
using Rootborn.Net.Transport;

namespace Rootborn.Server.Tests
{
    /// <summary>
    /// 서버를 돌리는 사람이 겪는 일을 빌드한 Rootborn.Server로(별도 프로세스, 실제 UDP 루프백, 실제 data/, 임시 세이브 폴더) 본다:
    /// 콘솔 명령, 로그 파일, 같은 슬롯을 두 서버가 열기(잠금), 죽은 서버의 잠금 넘기기, 포트·주소·세이브 폴더 오류의 끝 코드와 안내,
    /// 저장 중 강제 종료 뒤 다시 켜기, 손님이 요청을 보낸 직후 서버가 죽었다 켜졌을 때 이어지기.
    /// 주소는 같은 기계의 루프백(127.0.0.1)만 쓴다(테스트용으로 안전한 값). 자동 테스트다(두 기계·화면 입력 아님).
    /// </summary>
    [NonParallelizable]
    public sealed class ServerOperationsProcessTests
    {
        private static readonly TimeSpan Startup = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan Quick = TimeSpan.FromSeconds(20);
        private string _saveRoot;
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = ServerProcess.RepoRoot();
            _saveRoot = Path.Combine(Path.GetTempPath(), "rootborn-server-ops", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_saveRoot)) Directory.Delete(_saveRoot, true);
        }

        private ServerProcess Start(string slot, params string[] extra)
        {
            var args = new System.Collections.Generic.List<string> { "--root", _root, "--save-root", _saveRoot, "--slot", slot, "--bind", IPAddress.Loopback.ToString(), "--port", "0" };
            args.AddRange(extra);
            return ServerProcess.Start(args.ToArray());
        }

        private static void StopCleanly(ServerProcess server)
        {
            server.Stop();
            Assert.That(server.WaitForExit(Quick), Is.True, "stop 으로 끝난다\n" + server.Output);
            Assert.That(server.ExitCode, Is.EqualTo(Program.ExitOk), server.Output);
        }

        [Test]
        public void ConsoleCommands_StatusPlayersSaveHelpUnknown_AndTheLogFile()
        {
            string logFile = Path.Combine(_saveRoot, "logs", "server.log");
            using var server = Start("ops", "--log", logFile, "--share", "wallet", "--autosave-seconds", "0");
            try
            {
                int port = server.WaitForPort(Startup);
                server.WaitForLine(Program.ReadyLine, Quick);
                var versions = Program.CurrentVersions(Path.Combine(_root, "data"));
                using var player = RoomClient.Create(UdpTransport.Connect(IPAddress.Loopback, IPAddress.Loopback.ToString(), port, RoomProtocol.ConnectionKey), versions,
                    "console-guest", "child|", new SessionClientSettings());
                var clock = Stopwatch.StartNew();
                while (player.Status != ClientStatus.Joined && clock.Elapsed < Quick)
                {
                    player.Update(clock.Elapsed.TotalSeconds);
                    Thread.Sleep(5);
                }

                Assert.That(player.Status, Is.EqualTo(ClientStatus.Joined), server.Output);
                server.WaitForLine("들어옴: console-guest", Quick);

                server.Send("status");
                server.WaitForLine("가동 시간: ", Quick);
                server.WaitForLine("마지막 저장: ", Quick);
                StringAssert.Contains("슬롯 ops, 1일째", server.Output);
                StringAssert.Contains("같이 쓰는 것 wallet", server.Output);
                StringAssert.Contains("들어와 있는 사람: 1명", server.Output);

                server.Send("players");
                server.WaitForLine("1명 / 정원 4", Quick);
                server.Send("save");
                server.WaitForLine("저장함(save 명령): 슬롯 ops", Quick);
                server.Send("help");
                server.WaitForLine("  players", Quick);
                server.Send("jump");
                server.WaitForLine("모르는 명령: jump — help", Quick);

                StopCleanly(server);
                StringAssert.Contains("끝내 달라고 함: stop 명령", server.Output);
                StringAssert.Contains("저장하고 닫음", server.Output);
            }
            finally
            {
                server.Kill();
            }

            // 로그 파일에도 같은 줄이 시각과 함께 남는다(접속 주소는 남지 않는다 — 접속은 번호로만 보인다).
            string text = File.ReadAllText(logFile);
            StringAssert.Contains("들어옴: console-guest", text);
            StringAssert.Contains("저장함(save 명령)", text);
            StringAssert.Contains("저장하고 닫음", text);
            Assert.That(System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllLines(logFile)[0], @"^\d{4}-\d\d-\d\d \d\d:\d\d:\d\d "), Is.True, "줄마다 시각");
            Assert.That(File.Exists(Path.Combine(_saveRoot, "ops", RoomSlotLock.OwnerFileName)), Is.False, "정상으로 끝나면 잠금 주인 파일을 지운다");
        }

        [Test]
        public void TwoServers_SameSlot_SecondRefusesToStart_AndADeadServersLockIsTakenOver()
        {
            using var first = Start("shared-slot");
            try
            {
                first.WaitForPort(Startup);
                using var second = Start("shared-slot");
                Assert.That(second.WaitForExit(Startup), Is.True, second.Output);
                Assert.That(second.ExitCode, Is.EqualTo(Program.ExitSlotInUse), second.Output);
                StringAssert.Contains("이미 다른 곳에서 열려 있다", second.Output);
                StringAssert.Contains("데디케이티드 서버, 프로세스", second.Output, "누가 열었는지");
                StringAssert.Contains("--slot 에 다른 슬롯", second.Output, "무엇을 고치면 되는지");
                StringAssert.DoesNotContain(Program.ListeningPrefix, second.Output, "듣지 않았다");

                using var otherSlot = Start("another-slot");
                otherSlot.WaitForPort(Startup);
                StopCleanly(otherSlot);

                first.Kill(); // 저장 없이 죽는다
            }
            finally
            {
                first.Kill();
            }

            using var third = Start("shared-slot");
            try
            {
                third.WaitForPort(Startup);
                StringAssert.Contains("지난번에 끝내지 못하고 꺼진 서버의 잠금을 넘어감", third.Output);
                StringAssert.Contains("방을 이어서 엶", third.Output);
                StopCleanly(third);
            }
            finally
            {
                third.Kill();
            }
        }

        [Test]
        public void HostedRoomAndServer_SameSlot_TheSecondToOpenIsRefused()
        {
            var settings = new RoomServerSettings();
            var versions = Program.CurrentVersions(Path.Combine(_root, "data"));
            using (var mine = HostedRoom.Open(IPAddress.Loopback, 0, settings, versions, new FileSaveStore(_saveRoot), "my-slot", new ManualRoomClock(), null))
            {
                using var server = Start("my-slot");
                Assert.That(server.WaitForExit(Startup), Is.True, server.Output);
                Assert.That(server.ExitCode, Is.EqualTo(Program.ExitSlotInUse), server.Output);
                StringAssert.Contains("내 PC의 방", server.Output);
            }

            using var running = Start("my-slot");
            try
            {
                running.WaitForPort(Startup);
                Assert.Throws<RoomSlotLockedException>(() =>
                    HostedRoom.Open(IPAddress.Loopback, 0, settings, versions, new FileSaveStore(_saveRoot), "my-slot", new ManualRoomClock(), null));
                StopCleanly(running);
            }
            finally
            {
                running.Kill();
            }
        }

        [Test]
        public void PortInUse_ExitsWithCannotListen_AndSaysWhatToChange()
        {
            using var taken = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            int port = ((IPEndPoint)taken.Client.LocalEndPoint).Port;
            using var server = ServerProcess.Start("--root", _root, "--save-root", _saveRoot, "--slot", "busy", "--bind", IPAddress.Loopback.ToString(), "--port",
                port.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Assert.That(server.WaitForExit(Startup), Is.True, server.Output);
            Assert.That(server.ExitCode, Is.EqualTo(Program.ExitCannotListen), server.Output);
            StringAssert.Contains("이미 쓰고 있다", server.Output);
            StringAssert.Contains("--port 에 다른 포트", server.Output);
            Assert.That(File.Exists(Path.Combine(_saveRoot, "busy", RoomSlotLock.OwnerFileName)), Is.False, "듣지 못하면 잠금도 푼다");
        }

        [Test]
        public void BindToAnAddressThisMachineDoesNotHave_ExitsWithCannotListen()
        {
            // 203.0.113.0/24 는 문서용 주소(RFC 5737)라 어느 기계의 주소도 아니다 — 테스트에서 안전한 값.
            using var server = ServerProcess.Start("--root", _root, "--save-root", _saveRoot, "--slot", "far", "--bind", "203.0.113.7", "--port", "0");
            Assert.That(server.WaitForExit(Startup), Is.True, server.Output);
            Assert.That(server.ExitCode, Is.EqualTo(Program.ExitCannotListen), server.Output);
            StringAssert.Contains("이 기계의 주소가 아니다", server.Output);
        }

        [Test]
        public void SaveFolderThatCannotBeWritten_ExitsWithCannotWrite()
        {
            // 세이브 폴더 자리에 파일이 있으면 그 아래에 폴더를 만들 수 없다(운영체제와 무관하게 쓸 수 없는 폴더를 만드는 방법).
            string blocker = Path.Combine(_saveRoot, "not-a-folder");
            File.WriteAllText(blocker, "");
            using var server = ServerProcess.Start("--root", _root, "--save-root", Path.Combine(blocker, "saves"), "--slot", "s", "--bind", IPAddress.Loopback.ToString(), "--port", "0");
            Assert.That(server.WaitForExit(Startup), Is.True, server.Output);
            Assert.That(server.ExitCode, Is.EqualTo(Program.ExitCannotWrite), server.Output);
            StringAssert.Contains("세이브 폴더에 쓸 수 없다", server.Output);
            StringAssert.Contains("--save-root 에 쓸 수 있는 폴더", server.Output);
        }

        [Test]
        public void LogFileThatCannotBeOpened_ExitsWithCannotWrite()
        {
            string blocker = Path.Combine(_saveRoot, "not-a-folder");
            File.WriteAllText(blocker, "");
            using var server = Start("s", "--log", Path.Combine(blocker, "server.log"));
            Assert.That(server.WaitForExit(Startup), Is.True, server.Output);
            Assert.That(server.ExitCode, Is.EqualTo(Program.ExitCannotWrite), server.Output);
            StringAssert.Contains("로그 파일을 열 수 없다", server.Output);
        }

        [Test]
        public void KilledWhileSavingOften_ReopensTheRoomEveryTime()
        {
            // 주기 저장을 거의 틱마다(0.05초) 하게 하고 여러 때에 죽인다. 죽은 때가 쓰는 도중이어도 다시 켜면 방이 읽혀야 한다
            // (파일마다 임시 파일에 쓰고 바꿔치기 — 반쯤 쓴 파일이 남지 않는다).
            int[] delaysMs = { 0, 37, 120, 333 };
            int day = 0;
            foreach (int delay in delaysMs)
            {
                using var server = Start("crashy", "--autosave-seconds", "0.05");
                try
                {
                    server.WaitForPort(Startup);
                    server.WaitForLine("저장함(주기 저장)", Quick);
                    Thread.Sleep(delay);
                    server.Kill();
                }
                finally
                {
                    server.Kill();
                }

                Assert.That(new FileSaveStore(_saveRoot).TryRead("crashy", SaveFileNames.RoomClock, out RoomClockSaveData saved), Is.True, "방의 시계 파일이 읽힌다");
                Assert.That(saved.Day, Is.GreaterThanOrEqualTo(Math.Max(1, day)));
                day = saved.Day;
            }

            using var again = Start("crashy");
            try
            {
                again.WaitForPort(Startup);
                StringAssert.Contains("방을 이어서 엶", again.Output);
                StringAssert.Contains("지난번에 끝내지 못하고 꺼진 서버의 잠금을 넘어감", again.Output);
                StringAssert.DoesNotContain("세이브를 읽을 수 없다", again.Output);
                StopCleanly(again);
            }
            finally
            {
                again.Kill();
            }
        }

        [Test]
        public void GuestSendsARequest_ServerDiesRightAfter_GuestRejoinsAndEverythingMatches()
        {
            const string slot = "wallet-room";
            const string playerId = "crash-guest";
            const string work = "work.clinic-helper"; // 보상: 용돈 5 (SharedPiecesRoomTests 와 같다)
            var load = GameDataLoader.Load(Path.Combine(_root, "data"), RootbornBuilders.Register);
            load.ThrowIfFailed();
            var definitions = load.Definitions;
            var start = GameStartSettings.FromDefinitions(definitions);
            var versions = Program.CurrentVersions(Path.Combine(_root, "data"));

            // 1) 들어가서 알바 한 번(돈 넣기 요청이 손님 저장과 함께 간다) → 답을 기다리지 않고 바로 서버를 죽인다.
            using (var server = Start(slot, "--share", RoomWallet.PieceName))
            {
                try
                {
                    int port = server.WaitForPort(Startup);
                    using var guest = Guest.Join(port, versions, playerId, definitions, start, server);
                    Assert.That(guest.Game.BeginPartTimeWork(work, "crash:w1", 1).Applied, Is.True);
                    Assert.That(guest.Game.SettleMinigame("crash:w1", true).Applied, Is.True);
                    guest.Pump(() => false, 0.05); // 보내기만 하고
                    server.Kill();
                }
                finally
                {
                    server.Kill();
                }
            }

            // 2) 다시 켜고 같은 사람이 들어온다: 방의 지갑과 그 사람의 세이브가 같은 쪽이어야 한다(둘 다 들어갔거나 둘 다 아니다).
            //    손님 세이브를 쓴 뒤 지갑을 쓰기 전에 죽었으면 다시 들어올 때 반영한다(RoomServer — 장부가 한 번만).
            int balanceAfterRestart;
            bool progressKept;
            using (var server = Start(slot, "--share", RoomWallet.PieceName))
            {
                try
                {
                    int port = server.WaitForPort(Startup);
                    StringAssert.Contains("방을 이어서 엶", server.Output);
                    using var guest = Guest.Join(port, versions, playerId, definitions, start, server);
                    var wallet = (RoomWalletClient)guest.Game.Wallet;
                    Assert.That(guest.Pump(() => wallet.RoomBalance >= 0 && guest.Link.Ready, 5), Is.True, server.Output);
                    guest.Pump(() => false, 0.5);
                    balanceAfterRestart = wallet.RoomBalance;
                    progressKept = guest.Game.Student.TimePointsSpentToday > 0;
                    Assert.That(balanceAfterRestart == 5, Is.EqualTo(progressKept),
                        $"방 지갑 {balanceAfterRestart}, 손님 진행이 남았나 {progressKept} — 한쪽만 남으면 안 된다\n" + server.Output);
                }
                finally
                {
                    if (!server.HasExited)
                    {
                        server.Stop();
                        server.WaitForExit(Quick);
                    }

                    server.Kill();
                }
            }

            Assert.That(new FileSaveStore(_saveRoot).TryRead(slot, SaveFileNames.RoomPiece(RoomWallet.PieceName), out RoomWalletSaveData saved), Is.True);
            Assert.That(saved.Balance, Is.EqualTo(balanceAfterRestart), "끄면 그 잔액이 방의 세이브에");
            TestContext.WriteLine("죽기 전 요청이 " + (progressKept ? "들어갔다" : "들어가지 않았다(손님 진행도 같이 없다)"));
        }

        /// <summary>손님 하나: UDP 접속 + 방 연결 + 방이 내려 준 세이브로 시작한 게임 한 판(Host가 하는 일을 테스트가 한다 — GameSession 명령 직접 호출).</summary>
        private sealed class Guest : IDisposable
        {
            public SessionClient Client;
            public RoomGuestLink Link;
            public GameSession Game;
            private readonly Stopwatch _clock = Stopwatch.StartNew();

            public static Guest Join(int port, RoomVersions versions, string playerId, GameDefinitions definitions, GameStartSettings start, ServerProcess server)
            {
                var guest = new Guest();
                var settings = new SessionClientSettings();
                guest.Client = RoomClient.Create(UdpTransport.Connect(IPAddress.Loopback, IPAddress.Loopback.ToString(), port, RoomProtocol.ConnectionKey),
                    versions, playerId, "child|", settings);
                guest.Link = new RoomGuestLink(guest.Client, settings.MaxMessageBytes);
                Assert.That(guest.Pump(() => guest.Link.Ready || guest.Client.Status == ClientStatus.Refused), Is.True, "방 설정과 세이브를 받지 못했다\n" + server.Output);
                Assert.That(guest.Client.Status, Is.EqualTo(ClientStatus.Joined), "거절: " + guest.Client.FarewellReason + "\n" + server.Output);
                var store = new MemorySaveSlot(RoomGuestLink.Slot, guest.Link.Files.Writes);
                store.Committed += c => guest.Link.Upload(SaveFileSet.From(c.Writes, c.Deletes));
                guest.Game = GameSession.Start(definitions, start, store, RoomGuestLink.Slot, playerId, shares: guest.Link.Shares);
                guest.Pump(() => false, 0.3);
                return guest;
            }

            public bool Pump(Func<bool> until, double seconds = 15.0)
            {
                double end = _clock.Elapsed.TotalSeconds + seconds;
                while (_clock.Elapsed.TotalSeconds < end)
                {
                    Client.Update(_clock.Elapsed.TotalSeconds);
                    Link?.FlushRequests();
                    if (until()) return true;
                    Thread.Sleep(5);
                }

                return until();
            }

            public void Dispose() => Client.Dispose();
        }
    }
}
