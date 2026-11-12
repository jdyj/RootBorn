using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using NUnit.Framework;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;
using Rootborn.Core.Room;
using Rootborn.Core.Save;
using Rootborn.Data;
using Rootborn.Data.Builders;
using Rootborn.Data.Save;
using Rootborn.Game;
using Rootborn.Net.Room;
using Rootborn.Net.Session;
using Rootborn.Net.Transport;
using Rootborn.World;

namespace Rootborn.Server.Tests
{
    /// <summary>
    /// 배포한 데디케이티드 서버(Tools/publish 의 server-&lt;플랫폼&gt; 폴더, 저장소 밖 복사본)를 그대로 띄워 돈·집·마을을 같이 쓰는 방으로 열고,
    /// 이 테스트가 손님 하나로 실제 UDP(같은 기계의 루프백)로 들어가 본다: 가구 놓기가 방의 허락을 받는지(서버가 배포 폴더의 맵 데이터로 집을 판정하는지),
    /// 번 돈이 방 지갑에 들어가는지, 손님 진행이 서버의 guests/ 에 저장되고 서버를 껐다 켜서 다시 들어오면 이어지는지.
    /// 손님 쪽 게임 한 판은 배포 폴더의 data 로 만든다(같은 데이터 지문). 명령은 GameSession 직접 호출이다 — 화면·입력 근거가 아니다.
    /// 배포 폴더가 있어야 하므로 [Explicit]: <c>Tools/publish/verify.sh --server</c> 가 복사본 경로를 환경 변수 <see cref="FolderVariable"/> 로 주고 돌린다.
    /// </summary>
    [Explicit("배포 폴더가 있어야 한다 — Tools/publish/verify.sh --server 가 돌린다"), NonParallelizable]
    public sealed class DeployedServerSharingTests
    {
        public const string FolderVariable = "ROOTBORN_DEPLOYED_SERVER";
        private const string Slot = "share-room";
        private const string Player = "deploy-guest";
        private const string MoneyOnlyWork = "work.clinic-helper"; // 보상: 용돈 5 (SharedPiecesRoomTests 와 같다)
        private static readonly string[] Shared = { RoomWallet.PieceName, RoomHouse.PieceName, RoomWorldFlags.PieceName };

        private string _folder;
        private string _saves;
        private GameDefinitions _definitions;
        private GameStartSettings _settings;
        private RoomVersions _versions;
        private PlacementGrid _houseGrid;
        private string _surface;

        [SetUp]
        public void SetUp()
        {
            _folder = Environment.GetEnvironmentVariable(FolderVariable);
            if (string.IsNullOrEmpty(_folder) || !Directory.Exists(_folder))
                Assert.Fail(FolderVariable + " 에 배포한 서버 폴더(복사본)를 준다. 지금 값: " + (_folder ?? "(없음)"));
            _saves = Path.Combine(Path.GetTempPath(), "rootborn-deployed-server", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);

            string data = Path.Combine(_folder, "data");
            var load = GameDataLoader.Load(data, RootbornBuilders.Register);
            load.ThrowIfFailed();
            _definitions = load.Definitions;
            _settings = GameStartSettings.FromDefinitions(_definitions);
            _versions = Program.CurrentVersions(data);

            // 가구를 놓는 맵: 배포 폴더의 상호작용 데이터에서 찾는다(맵 이름을 적지 않는다).
            var interactions = InteractionData.Load(Path.Combine(_folder, InteractionData.DefaultRelativePath));
            string houseMap = interactions.MapIds.First(m => !string.IsNullOrEmpty(interactions.Map(m).PlacementSurface));
            _surface = interactions.Map(houseMap).PlacementSurface;
            _houseGrid = HouseGrids.FromMap(WorldMapLoader.FromRepo(_folder).LoadMap(houseMap).World);
        }

        [TearDown]
        public void TearDown()
        {
            if (_saves != null && Directory.Exists(_saves)) Directory.Delete(_saves, true);
        }

        [Test]
        public void DeployedServer_SharesMoneyHouseAndTown_AndKeepsTheGuestsProgressAcrossARestart()
        {
            string guestFolder = Path.Combine(GuestSaveFolder.FolderFor(_saves, Slot), Player);
            string chair;
            GridPoint cell;
            using (var server = DeployedServer.Start(_folder, _saves, Slot, "--share", string.Join(",", Shared)))
            {
                int port = server.WaitForPort(TimeSpan.FromSeconds(60));
                StringAssert.Contains("새 방을 만듦", server.Output);
                using (var guest = Guest.Join(this, port, server))
                {
                    Assert.That(guest.Link.Settings.SharedPieces, Is.EquivalentTo(Shared), "방이 같이 쓰는 것\n" + server.Output);
                    Assert.That(guest.Link.TakesGuestSaves, Is.True, "데디케이티드 서버는 손님 진행을 받는다");
                    Assert.That(guest.Game.Wallet, Is.InstanceOf<RoomWalletClient>(), "돈을 같이 쓰는 방");

                    // 가구 놓기: 방이 배포 폴더의 맵으로 판정해 허락한다(맵 데이터가 빠졌으면 거절된다).
                    chair = guest.Game.AvailableFurniture()[0].StableId;
                    var furniture = _definitions.Get<FurnitureDefinition>(chair);
                    cell = _houseGrid.FloorCellsInOrder().First(c => guest.Game.House.CanPlace(furniture, c, FurniturePlacementDirection.North));
                    guest.Game.PlaceFurniture(chair, cell, FurniturePlacementDirection.North);
                    Assert.That(guest.Pump(() => !guest.Game.WaitingForRoom), Is.True, "방의 답이 오지 않았다\n" + server.Output);
                    Assert.That(guest.Game.LastRoomCommandApplied, Is.True, "방이 가구 놓기를 허락한다\n" + server.Output);
                    Assert.That(guest.Pump(() => guest.Game.House.Placed.Count == 1), Is.True, "허락된 상태로 집이 다시 채워진다(놓인 가구 " + guest.Game.House.Placed.Count + ")\n" + server.Output);

                    // 방 지갑: 알바로 번 돈이 방의 잔액이 된다.
                    Assert.That(guest.Game.BeginPartTimeWork(MoneyOnlyWork, "deploy:w1", 1).Applied, Is.True);
                    Assert.That(guest.Game.SettleMinigame("deploy:w1", true).Applied, Is.True);
                    // 넣기 요청은 손님 저장과 함께 가고, 방이 반영하기 전까지 잔액은 내 쪽에서 미리 더해 보인다 — 방이 보낸 잔액이 5가 될 때까지 기다린다.
                    var roomWallet = (RoomWalletClient)guest.Game.Wallet;
                    Assert.That(guest.Pump(() => roomWallet.RoomBalance == 5 && guest.Game.Wallet.Balance == 5), Is.True,
                        "방 지갑 " + roomWallet.RoomBalance + " (보이는 잔액 " + guest.Game.Wallet.Balance + ")\n" + server.Output);

                    // 손님 진행: 서버의 guests/<플레이어>/ 에 저장된다.
                    Assert.That(guest.Pump(() => File.Exists(Path.Combine(guestFolder, SaveFileNames.AdultCareerSnapshot(Player)))), Is.True,
                        "손님 진행이 서버의 " + guestFolder + " 에 없다\n" + server.Output);
                }

                server.Stop();
                Assert.That(server.WaitForExit(TimeSpan.FromSeconds(30)), Is.True, "stop 으로 끝난다\n" + server.Output);
                Assert.That(server.ExitCode, Is.EqualTo(Program.ExitOk), server.Output);
                StringAssert.Contains("저장하고 닫음", server.Output);
                StringAssert.DoesNotContain("집 맵을 읽지 못했다", server.Output);
            }

            Assert.That(File.Exists(Path.Combine(_saves, Slot, SaveFileNames.RoomPiece(RoomHouse.PieceName))), Is.True, "같이 쓰는 집이 방의 세이브에");
            Assert.That(new FileSaveStore(_saves).TryRead(Slot, SaveFileNames.RoomPiece(RoomWallet.PieceName), out RoomWalletSaveData wallet), Is.True, "방 지갑이 방의 세이브에");
            Assert.That(wallet.Balance, Is.EqualTo(5), "방의 세이브에 남은 방 지갑");

            // 껐다 켜서 다시 들어오면 이어진다: 방의 집·지갑, 손님 자기 진행.
            using (var again = DeployedServer.Start(_folder, _saves, Slot, "--share", string.Join(",", Shared)))
            {
                int port = again.WaitForPort(TimeSpan.FromSeconds(60));
                StringAssert.Contains("방을 이어서 엶", again.Output);
                using (var guest = Guest.Join(this, port, again))
                {
                    Assert.That(guest.Link.Files.Writes, Is.Not.Empty, "서버가 손님 세이브를 내려 준다(새 게임이 아니다)");
                    Assert.That(guest.Pump(() => guest.Game.Wallet.Balance == 5), Is.True, "방 지갑이 이어진다: " + guest.Game.Wallet.Balance
                        + " (방이 보낸 잔액 " + ((RoomWalletClient)guest.Game.Wallet).RoomBalance + ")\n" + again.Output);
                    Assert.That(guest.Pump(() => guest.Game.House.Placed.Count == 1), Is.True, "같이 쓰는 집이 이어진다\n" + again.Output);
                    Assert.That(guest.Game.House.Placed[0].Anchor, Is.EqualTo(cell));
                    Assert.That(guest.Game.Student.TimePointsSpentToday, Is.GreaterThan(0), "손님 자기 진행(알바한 시간)이 이어진다");
                }

                again.Stop();
                Assert.That(again.WaitForExit(TimeSpan.FromSeconds(30)), Is.True, again.Output);
                Assert.That(again.ExitCode, Is.EqualTo(Program.ExitOk), again.Output);
            }
        }

        /// <summary>손님 하나: UDP 접속 + 방 연결(RoomGuestLink) + 방이 내려 준 세이브로 시작한 게임 한 판(Host가 하는 일을 테스트가 한다).</summary>
        private sealed class Guest : IDisposable
        {
            public SessionClient Client;
            public RoomGuestLink Link;
            public GameSession Game;
            private readonly Stopwatch _clock = Stopwatch.StartNew();

            public static Guest Join(DeployedServerSharingTests t, int port, DeployedServer server)
            {
                var guest = new Guest();
                var settings = new SessionClientSettings();
                guest.Client = RoomClient.Create(UdpTransport.Connect(IPAddress.Loopback, IPAddress.Loopback.ToString(), port, RoomProtocol.ConnectionKey),
                    t._versions, Player, "child|", settings);
                guest.Link = new RoomGuestLink(guest.Client, settings.MaxMessageBytes);
                Assert.That(guest.Pump(() => guest.Link.Ready || guest.Client.Status == ClientStatus.Refused), Is.True, "방 설정과 세이브를 받지 못했다\n" + server.Output);
                Assert.That(guest.Client.Status, Is.EqualTo(ClientStatus.Joined), "거절: " + guest.Client.FarewellReason + "\n" + server.Output);
                var store = new MemorySaveSlot(RoomGuestLink.Slot, guest.Link.Files.Writes);
                store.Committed += c => guest.Link.Upload(SaveFileSet.From(c.Writes, c.Deletes));
                guest.Game = GameSession.Start(t._definitions, t._settings, store, RoomGuestLink.Slot, Player, shares: guest.Link.Shares);
                guest.Game.EnterHouse(t._houseGrid, t._surface); // Host: 집 맵에 들어가면
                guest.Pump(() => false, 0.5);
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

            public void Dispose()
            {
                Client.Dispose();
                Thread.Sleep(200); // 나간다는 알림이 나가게
            }
        }

        /// <summary>배포 폴더의 실행 파일(Rootborn.Server[.exe])을 그대로 띄운다. --root 없이 실행 파일 위치에서 data 를 찾는다. 출력은 모아 둔다.</summary>
        private sealed class DeployedServer : IDisposable
        {
            private readonly Process _process;
            private readonly ConcurrentQueue<string> _lines = new ConcurrentQueue<string>();

            private DeployedServer(Process process)
            {
                _process = process;
                _process.OutputDataReceived += (s, e) => { if (e.Data != null) _lines.Enqueue(e.Data); };
                _process.ErrorDataReceived += (s, e) => { if (e.Data != null) _lines.Enqueue("[표준 오류] " + e.Data); };
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();
            }

            public string Output => string.Join(Environment.NewLine, _lines);
            public int ExitCode => _process.ExitCode;

            public static DeployedServer Start(string folder, string saves, string slot, params string[] more)
            {
                string exe = Path.Combine(folder, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Rootborn.Server.exe" : "Rootborn.Server");
                Assert.That(File.Exists(exe), Is.True, "배포 폴더에 서버 실행 파일이 없다: " + exe);
                var info = new ProcessStartInfo(exe)
                {
                    UseShellExecute = false,
                    WorkingDirectory = folder,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8,
                    StandardInputEncoding = new System.Text.UTF8Encoding(false),
                };
                // 같은 기계 안에서만 닿는 루프백, 빈 포트 — 시험용으로 안전한 값이다.
                foreach (var arg in new[] { "--save-root", saves, "--slot", slot, "--bind", IPAddress.Loopback.ToString(), "--port", "0" }) info.ArgumentList.Add(arg);
                foreach (var arg in more) info.ArgumentList.Add(arg);
                // 저장소·실행 환경의 서버 값이 섞이지 않게 비운다(배포 폴더만으로 돌아야 한다).
                foreach (var name in new[] { "ROOTBORN_REPO_ROOT", ServerOptions.SlotVariable, ServerOptions.BindVariable, ServerOptions.PortVariable, ServerOptions.MaxPlayersVariable,
                             ServerOptions.SaveRootVariable, ServerOptions.DataVariable, "ROOTBORN_ROOM_SHARE", "ROOTBORN_DAY_COPIES" })
                    info.Environment.Remove(name);
                return new DeployedServer(Process.Start(info));
            }

            public int WaitForPort(TimeSpan timeout)
            {
                var watch = Stopwatch.StartNew();
                while (watch.Elapsed < timeout)
                {
                    foreach (var line in _lines)
                    {
                        int at = line.IndexOf(Program.ListeningPrefix, StringComparison.Ordinal);
                        if (at < 0) continue;
                        string endpoint = line.Substring(at + Program.ListeningPrefix.Length).Trim();
                        return int.Parse(endpoint.Substring(endpoint.LastIndexOf(':') + 1));
                    }

                    if (_process.HasExited) break;
                    Thread.Sleep(20);
                }

                throw new AssertionException("서버가 듣기 시작하지 않았다\n" + Output);
            }

            public void Stop()
            {
                _process.StandardInput.WriteLine(Program.StopCommand);
                _process.StandardInput.Flush();
            }

            public bool WaitForExit(TimeSpan timeout)
            {
                if (!_process.WaitForExit((int)timeout.TotalMilliseconds)) return false;
                _process.WaitForExit();
                return true;
            }

            public void Dispose()
            {
                try
                {
                    if (!_process.HasExited) _process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                }

                _process.Dispose();
            }
        }
    }
}
