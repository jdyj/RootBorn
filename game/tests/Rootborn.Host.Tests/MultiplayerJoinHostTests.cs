using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using Rootborn.Core.Save;
using Rootborn.Core.World;
using Rootborn.Data.Save;
using Rootborn.Host.Net;
using Rootborn.Host.World;
using Rootborn.Net.Room;
using Rootborn.Net.Session;
using Rootborn.Net.Transport;
using Rootborn.Screens;
using Rootborn.Screens.Start;
using Rootborn.Ui;
using GameSession = Rootborn.Game.GameSession;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 같은 테스트 프로세스 안에 방(RoomServer, 실제 UDP 소켓)을 띄운다. 방은 실제 서버처럼 자기 스레드에서 실제 시간으로 돌고,
    /// 두 번째 참가자(SessionClient를 직접)도 그 스레드에서 돈다. 방의 객체는 그 스레드만 만지고, 테스트는 잠금 안에서만 읽고 쓴다.
    /// 주소는 루프백(127.0.0.1)이고 포트는 0으로 받아 운영체제가 준 빈 포트다 — 같은 기계 안에서만 오가는 시험용 값이라 안전하다
    /// (방화벽 창도 뜨지 않는다).
    /// </summary>
    internal sealed class LoopbackRoom : IDisposable
    {
        private readonly object _gate = new object();
        private readonly RoomServer _server;
        private readonly RoomLoop _loop;
        private readonly StopwatchRoomClock _clock = new StopwatchRoomClock();
        private readonly List<SessionClient> _friends = new List<SessionClient>();
        private readonly Thread _thread;
        private volatile bool _stop;

        public LoopbackRoom(string saveRoot, RoomVersions versions, int maxPlayers = 4)
        {
            var transport = UdpTransport.Listen(IPAddress.Loopback, 0, RoomProtocol.ConnectionKey);
            Port = transport.LocalPort;
            Versions = versions;
            var settings = new RoomServerSettings { MaxPlayers = maxPlayers };
            _server = new RoomServer(transport, settings, versions, new FileSaveStore(saveRoot), "room", line => TestContext.Progress.WriteLine("[방] " + line));
            _loop = new RoomLoop(_server, _clock, settings.TickSeconds);
            _thread = new Thread(Run) { IsBackground = true, Name = "loopback-room" };
            _thread.Start();
        }

        public int Port { get; }
        public RoomVersions Versions { get; }
        public string Address => IPAddress.Loopback + ":" + Port;

        private void Run()
        {
            while (!_stop)
            {
                lock (_gate)
                {
                    _loop.RunDue();
                    foreach (SessionClient friend in _friends) friend.Update(_clock.Now);
                }
                Thread.Sleep(2);
            }
        }

        public T Read<T>(Func<RoomServer, T> read) { lock (_gate) return read(_server); }
        public void Do(Action act) { lock (_gate) act(); }
        public double Now => _clock.Now;

        /// <summary>두 번째 참가자를 같은 방에 넣는다(같은 버전, 같은 루프백 주소).</summary>
        public SessionClient AddFriend(string playerId, string look)
        {
            var client = RoomClient.Create(UdpTransport.Connect(IPAddress.Loopback, IPAddress.Loopback.ToString(), Port, RoomProtocol.ConnectionKey),
                Versions, playerId, look, new SessionClientSettings());
            lock (_gate) _friends.Add(client);
            return client;
        }

        /// <summary>서버를 끈다(저장하고 모두에게 "서버가 닫힘"을 알린다).</summary>
        public void Shutdown() { lock (_gate) _server.Shutdown(); }

        public void Dispose()
        {
            _stop = true;
            _thread.Join(2000);
            lock (_gate)
            {
                foreach (SessionClient friend in _friends) friend.Dispose();
                _server.Shutdown();
            }
        }
    }

    /// <summary>
    /// 서버 주소로 방에 들어가 다른 사람을 본다(설계 6절 "서버 주소로 들어가기", 7절 6단계 중 위치·겉모습·접속).
    /// 근거: 장치 이벤트 주입(OS 입력 계층 미확인), 같은 기계 루프백 UDP — 두 기계·두 프로세스는 미확인. 세이브 폴더는 임시 폴더.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class MultiplayerJoinHostTests
    {
        private string _saves, _roomSaves, _repoRoot;

        [SetUp]
        public void SetUp()
        {
            _saves = Path.Combine(Path.GetTempPath(), "rootborn-mp-host-" + Guid.NewGuid().ToString("N"));
            _roomSaves = Path.Combine(Path.GetTempPath(), "rootborn-mp-room-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
            Directory.CreateDirectory(_roomSaves);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (string dir in new[] { _saves, _roomSaves })
                try { Directory.Delete(dir, true); } catch (IOException) { }
        }

        private RoomVersions Versions()
        {
            _repoRoot ??= HostOptions.Resolve(new[] { "--sample" }).RepoRoot;
            return RoomVersions.ForProgram(typeof(GameSession).Assembly, Rootborn.Data.DataFingerprint.Compute(Path.Combine(_repoRoot, "data")));
        }

        private PlayerDriver Launch(params string[] extra)
        {
            HostOptions options = HostOptions.Resolve(new[] { "--save-root", _saves, "--size", "1280x720" }.Concat(extra).ToArray());
            options.TestMode = true;
            options.EvidenceDir = Path.Combine(options.RepoRoot, "output", "host-multiplayer-evidence");
            var player = new PlayerDriver(options);
            player.Frames(3);
            return player;
        }

        /// <summary>조건이 될 때까지 프레임을 돌린다(방은 자기 스레드에서 실제 시간으로 돈다). 실제 시간 seconds를 넘기면 false.</summary>
        private static bool Until(PlayerDriver player, Func<bool> condition, double seconds = 10)
        {
            var watch = Stopwatch.StartNew();
            while (watch.Elapsed.TotalSeconds < seconds)
            {
                player.Frames(1);
                if (condition()) return true;
                Thread.Sleep(1);
            }
            return false;
        }

        private static bool UntilReal(Func<bool> condition, double seconds)
        {
            var watch = Stopwatch.StartNew();
            while (watch.Elapsed.TotalSeconds < seconds)
            {
                if (condition()) return true;
                Thread.Sleep(5);
            }
            return false;
        }

        private void WriteStory(string slot, string name)
        {
            var model = new CharacterCreatorModel(CreatorLooks.Load(Path.Combine(_repoRoot ?? HostOptions.Resolve(new[] { "--sample" }).RepoRoot, "data")));
            SaveSlotMetadata metadata = SaveSlots.CreateMetadata(slot, name, model.Snapshot(), DateTime.UtcNow.Ticks, 4242);
            new FileSaveStore(_saves).Write(slot, SaveFileNames.Metadata, metadata);
        }

        [Test]
        public void MP_JOIN_001_JoinBySlot_SeesAFriendWalk_OnlyOnTheSameMap_AndWalksThroughThem()
        {
            using var room = new LoopbackRoom(_roomSaves, Versions());
            using PlayerDriver player = Launch("--join", room.Address, "--slot", "slot-0");
            Assert.That(player.Screens.IsOpen(ScreenCatalog.JoinServer), Is.True, "--join은 들어가기 창을 그 주소로 연다");
            Assert.That(Until(player, () => player.Game.World != null), Is.True, "방이 받아들이면 그 슬롯으로 월드가 뜬다");
            Assert.That(player.Game.Room.InRoom, Is.True);
            Assert.That(player.Game.Session, Is.Not.Null, "혼자 하기와 같은 게임 한 판");
            Assert.That(player.Screens.IsOpen(ScreenCatalog.HudClock), Is.True, "혼자 하기와 같은 화면");
            Assert.That(UntilReal(() => room.Read(s => s.Players.Count) == 1, 3), Is.True);
            string hostId = player.Game.Room.PlayerId;
            Assert.That(room.Read(s => s.Players.Select(p => p.PlayerId).ToArray()), Is.EqualTo(new[] { hostId }));

            Assert.That(player.WalkThroughDoor("town"), Is.EqualTo("Town"), "방에 있어도 문으로 맵을 옮긴다");
            Walker me = player.World.Walker;
            float cell = player.World.Map.Grid.CellSize;
            string look = player.Game.World.PlayerLook.Code;
            SessionClient friend = room.AddFriend("friend", look);
            Assert.That(UntilReal(() => room.Read(_ => friend.Status) == ClientStatus.Joined, 5), Is.True, "두 번째 참가자도 들어온다");

            // 친구가 내 옆(오른쪽 1.5칸)에 선다 → 내 화면의 그리기 목록에 나타난다.
            float fx = me.X + cell * 1.5f, fy = me.Y;
            room.Do(() => friend.SetPose("Town", fx, fy, Facing.Left, false));
            Assert.That(Until(player, () => player.Game.Remotes.Visible.Count == 1 && player.Game.World.RemotesDrawn == 1), Is.True, "같은 맵의 친구가 그려진다");
            RemoteAvatar seen = player.Game.Remotes.Visible[0];
            Assert.That(seen.PlayerId, Is.EqualTo("friend"));
            Assert.That(seen.Look, Is.Not.Null, "겉모습 코드를 풀었다");
            Assert.That(Until(player, () => Math.Abs(player.Game.Remotes.Visible[0].X - fx) < 0.01f), Is.True);
            Assert.That(seen.Facing, Is.EqualTo(Facing.Left));
            string shot = player.Screenshot("multiplayer-01-town-friend");
            TestContext.WriteLine("스크린샷: " + shot);

            // 친구도 나를 본다(내 위치와 겉모습을 보냈다).
            Assert.That(UntilReal(() => room.Read(_ => friend.PlayersOn("Town", room.Now).Any(p => p.PlayerId == hostId && p.Look == look)), 3), Is.True,
                "친구 쪽에 내 몸과 겉모습이 보인다");

            // 친구가 걷는다 → 내 화면에서 움직이고 걷는 중이다.
            for (int i = 1; i <= 20; i++)
            {
                float x = fx + cell * 0.1f * i;
                room.Do(() => friend.SetPose("Town", x, fy, Facing.Right, true));
                player.Frames(2);
                Thread.Sleep(10);
            }
            Assert.That(Until(player, () => player.Game.Remotes.Visible.Count == 1 && player.Game.Remotes.Visible[0].X > fx + cell), Is.True, "움직임이 보인다");
            Assert.That(player.Game.Remotes.Visible[0].Walking, Is.True);
            Assert.That(player.Game.Remotes.Visible[0].Facing, Is.EqualTo(Facing.Right));
            player.Screenshot("multiplayer-02-town-friend-walked");

            // 몸은 막지 않는다: 친구를 내 자리에 세운 뒤 걸어 나갔다가 다시 그 칸으로 걸어 들어간다.
            CellPos start = player.PlayerCell;
            float sx = me.X, sy = me.Y;
            room.Do(() => friend.SetPose("Town", sx, sy, Facing.Down, false));
            Assert.That(Until(player, () => player.Game.Remotes.Visible.Count == 1 && Math.Abs(player.Game.Remotes.Visible[0].X - sx) < 0.01f), Is.True);
            player.WalkAway(3);
            player.WalkTo(start);
            Assert.That(player.PlayerCell, Is.EqualTo(start), "친구가 선 칸으로 걸어 들어간다(충돌 없음)");

            // 다른 맵이면 안 보인다.
            room.Do(() => friend.SetPose("House", 10f, 10f, Facing.Down, false));
            Assert.That(Until(player, () => player.Game.Remotes.Visible.Count == 0), Is.True, "다른 맵의 친구는 안 보인다");
            player.Frames(2);
            Assert.That(player.Game.World.RemotesDrawn, Is.EqualTo(0));
        }

        [Test]
        public void MP_JOIN_002_StartMenuButton_TypeTheAddress_JoinWithAStory()
        {
            using var room = new LoopbackRoom(_roomSaves, Versions());
            WriteStory("slot-1", "별");
            using PlayerDriver player = Launch();
            Assert.That(player.Screens.IsOpen(ScreenCatalog.SaveSlotSelect), Is.True);
            player.PressButton(SaveSlotScreen.JoinId);
            player.WaitForScreen(ScreenCatalog.JoinServer);
            var screen = (JoinServerScreen)player.Screens.Find(ScreenCatalog.JoinServer);
            Assert.That(screen.SlotIds, Is.EqualTo(new[] { "slot-1" }));
            player.PressButton(JoinServerScreen.AddressId);
            Assert.That(player.Game.TextInputActive, Is.True);
            player.TypeText(room.Address);
            Assert.That(((UiTextField)player.Node(JoinServerScreen.AddressId)).Text, Is.EqualTo(room.Address));
            player.Screenshot("multiplayer-03-join-modal");
            player.PressButton(JoinServerScreen.ConnectId);
            Assert.That(Until(player, () => player.Game.World != null), Is.True, "주소를 쳐서 들어간다");
            Assert.That(player.Game.World.SlotStart.NewGame, Is.False, "고른 이야기로 이어서 시작한다");
            string expected = GuestProgress.RoomIdentity("slot-1", new FileSaveStore(_saves).TryRead("slot-1", SaveFileNames.Metadata, out SaveSlotMetadata m) ? m : null);
            Assert.That(UntilReal(() => room.Read(s => s.Players.Any(p => p.PlayerId == expected)), 3), Is.True, "신원은 그 슬롯의 인물에 고정된 id");
        }

        [Test]
        public void MP_JOIN_003_DifferentGameVersion_IsRefusedWithTheReason()
        {
            RoomVersions mine = Versions();
            using var room = new LoopbackRoom(_roomSaves, new RoomVersions(mine.Protocol, mine.Game + ".other-build", mine.Data));
            using PlayerDriver player = Launch("--join", room.Address, "--slot", "slot-0");
            var screen = (JoinServerScreen)player.Screens.Find(ScreenCatalog.JoinServer);
            Assert.That(Until(player, () => player.Game.Room.Phase == RoomJoinPhase.Failed), Is.True);
            player.Frames(2);
            Assert.That(player.Game.Room.FailureCode, Is.EqualTo(RoomRefusal.GameVersionMismatch));
            Assert.That(screen.StatusKey, Is.EqualTo("ui.join.reason.game-version-mismatch"));
            Assert.That(((UiTextBlock)player.Node(JoinServerScreen.StatusId)).Text, Is.EqualTo(player.Game.Text.Get("ui.join.reason.game-version-mismatch")));
            Assert.That(player.Game.World, Is.Null, "거절되면 월드를 띄우지 않는다");
            Assert.That(Directory.Exists(Path.Combine(_saves, "slot-0")), Is.False, "거절되면 슬롯을 만들지 않는다");
            player.ScreenshotScreen(ScreenCatalog.JoinServer, "multiplayer-04-refused-version");
        }

        [Test]
        public void MP_JOIN_004_ServerCloses_BackToTheStartMenuWithANotice_ProgressStaysOnThisPc()
        {
            var room = new LoopbackRoom(_roomSaves, Versions());
            try
            {
                using PlayerDriver player = Launch("--join", room.Address, "--slot", "slot-0");
                Assert.That(Until(player, () => player.Game.World != null), Is.True);
                Assert.That(player.WalkThroughDoor("town"), Is.EqualTo("Town"));
                room.Shutdown();
                Assert.That(Until(player, () => player.Screens.IsOpen(ScreenCatalog.RoomNotice)), Is.True, "끊기면 알림");
                Assert.That(player.Game.World, Is.Null, "시작 화면으로 돌아왔다");
                Assert.That(player.Screens.IsOpen(ScreenCatalog.SaveSlotSelect), Is.True);
                var notice = (RoomNoticeScreen)player.Screens.Find(ScreenCatalog.RoomNotice);
                Assert.That(notice.ReasonKey, Is.EqualTo("ui.join.reason.server-shutdown"));
                player.ScreenshotScreen(ScreenCatalog.RoomNotice, "multiplayer-05-server-closed");

                // 임시: 진행은 이 PC의 슬롯에 남아 있다 — 이야기와 마지막 도착(마을).
                var store = new FileSaveStore(_saves);
                Assert.That(store.TryRead("slot-0", SaveFileNames.Metadata, out SaveSlotMetadata _), Is.True);
                Assert.That(store.TryRead("slot-0", SaveFileNames.WorldProgress, out WorldProgressSaveData progress), Is.True);
                Assert.That(progress.CurrentScene, Is.EqualTo("Town"));

                player.PressButton(RoomNoticeScreen.OkId);
                Assert.That(player.Screens.IsOpen(ScreenCatalog.RoomNotice), Is.False);
                Assert.That(player.Screens.IsOpen(ScreenCatalog.SaveSlotSelect), Is.True);
                Assert.That(((UiButton)player.Node(SaveSlotScreen.JoinId)).Visible, Is.True, "다시 들어가기를 누를 수 있다");
            }
            finally { room.Dispose(); }
        }

        [Test]
        public void MP_JOIN_005_QuittingTheGame_TellsTheServerAtOnce()
        {
            using var room = new LoopbackRoom(_roomSaves, Versions());
            PlayerDriver player = Launch("--join", room.Address, "--slot", "slot-0");
            try
            {
                Assert.That(Until(player, () => player.Game.World != null), Is.True);
                Assert.That(UntilReal(() => room.Read(s => s.Players.Count) == 1, 3), Is.True);
            }
            finally { player.Dispose(); }
            // 시간 초과(10초)를 기다리지 않고 나간 것으로 안다.
            Assert.That(UntilReal(() => room.Read(s => s.Players.Count) == 0, 3), Is.True, "끄면 서버에 나간다고 알린다");
        }

        [Test]
        public void MP_JOIN_006_NothingListening_SaysTheServerIsUnreachable()
        {
            // 열었다 바로 닫은 루프백 포트: 듣는 쪽이 없다(시험용 값).
            int port;
            using (var probe = UdpTransport.Listen(IPAddress.Loopback, 0, RoomProtocol.ConnectionKey)) port = probe.LocalPort;
            using PlayerDriver player = Launch("--join", IPAddress.Loopback + ":" + port, "--slot", "slot-0");
            Assert.That(Until(player, () => player.Game.Room.Phase == RoomJoinPhase.Failed, 15), Is.True);
            Assert.That(player.Game.Room.FailureCode, Is.EqualTo(RoomJoinText.Unreachable));
            Assert.That(player.Game.World, Is.Null);
        }

        [Test]
        public void SoloPlay_HasNoRoomWork()
        {
            using PlayerDriver player = Launch("--slot", "slot-0");
            Assert.That(player.Game.World, Is.Not.Null);
            Assert.That(player.Game.Room, Is.Null, "혼자 하기(--slot만)에는 방 접속이 없다 — 프레임마다 도는 Net 코드 없음");
            Assert.That(player.Game.World.Remotes, Is.Null);
        }

        [Test]
        public void JoinOption_NeedsHostAndPort_AndASaveFolder()
        {
            Assert.Throws<InvalidOperationException>(() => HostOptions.Resolve(new[] { "--save-root", _saves, "--join", "no-port" }));
            Assert.Throws<InvalidOperationException>(() => HostOptions.Resolve(new[] { "--save-root", _saves, "--join", "host:0" }));
            Assert.Throws<InvalidOperationException>(() => HostOptions.Resolve(new[] { "--join", "host:7777" }, _ => null), "주소 기본값도 세이브 폴더 기본값도 없다");
            Assert.Throws<InvalidOperationException>(() => HostOptions.Resolve(new[] { "--save-root", _saves, "--join", "host:7777", "--world", "Town" }));
            HostOptions viaEnv = HostOptions.Resolve(new[] { "--save-root", _saves }, name => name == HostOptions.JoinVariable ? "10.1.2.3:7777" : null);
            Assert.That(viaEnv.JoinAddress, Is.EqualTo("10.1.2.3:7777"));
            Assert.That(viaEnv.StartMenu, Is.True);
            HostOptions withSlot = HostOptions.Resolve(new[] { "--save-root", _saves, "--join", "host.example:7777", "--slot", "slot-0" });
            Assert.That(withSlot.WorldActive, Is.False, "먼저 방에 들어간 뒤 월드를 띄운다");
            Assert.That(withSlot.JoinOnStart, Is.True);
            Assert.That(HostOptions.Resolve(new[] { "--save-root", _saves }, _ => null).JoinAddress, Is.Null, "주소 기본값이 없다");
        }

        [Test]
        public void RoomAddress_Parsing()
        {
            Assert.That(RoomAddress.TryParse("192.168.0.10:7777", out string h, out int p), Is.True);
            Assert.That((h, p), Is.EqualTo(("192.168.0.10", 7777)));
            Assert.That(RoomAddress.TryParse(" host.example:1 ", out h, out p), Is.True);
            Assert.That(h, Is.EqualTo("host.example"));
            foreach (string bad in new[] { "", "host", ":7777", "host:", "host:70000", "host:-1", "a:b:7777", "host:12x" })
                Assert.That(RoomAddress.TryParse(bad, out _, out _), Is.False, bad);
            Assert.That(RoomAddress.LocalBindFor("127.0.0.1"), Is.EqualTo(IPAddress.Loopback));
            Assert.That(RoomAddress.LocalBindFor("localhost"), Is.EqualTo(IPAddress.Loopback));
            Assert.That(RoomAddress.LocalBindFor("10.0.0.5"), Is.EqualTo(IPAddress.Any));
        }

        /// <summary>서버가 보낼 수 있는 이유 코드(Net의 상수)와 세션의 끊김 이유마다 모든 언어에 문구가 있다.</summary>
        [Test]
        public void EveryRefusalReason_HasTextInEveryLanguage()
        {
            _repoRoot ??= HostOptions.Resolve(new[] { "--sample" }).RepoRoot;
            GameText text = GameText.LoadMerged(GameText.DefaultBaseLanguage, Path.Combine(_repoRoot, "data", "localization"), Path.Combine(_repoRoot, "game", "localization"));
            var codes = typeof(RoomRefusal).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()).ToList();
            codes.Add(SessionClient.TimeoutReason);
            codes.Add(SessionClient.ClosedReason);
            Assert.That(codes, Has.Count.GreaterThanOrEqualTo(10));
            foreach (string code in codes.Distinct())
                foreach (string language in text.Languages)
                    Assert.That(text.Has(RoomJoinText.ReasonKey(code), language), Is.True, language + ": " + code + " → " + RoomJoinText.ReasonKey(code));
        }

        [Test]
        public void SameCharacter_HasTheSameRoomIdentity_DifferentCharactersDiffer()
        {
            var a = new SaveSlotMetadata { CreatedAtUtcTicks = 638000000000000000, WorldSeed = 11, TileSeed = 22 };
            var b = new SaveSlotMetadata { CreatedAtUtcTicks = 638000000000000001, WorldSeed = 11, TileSeed = 22 };
            Assert.That(GuestProgress.RoomIdentity("slot-0", a), Is.EqualTo(GuestProgress.RoomIdentity("slot-0", a)));
            Assert.That(GuestProgress.RoomIdentity("slot-0", a), Is.Not.EqualTo(GuestProgress.RoomIdentity("slot-0", b)));
            Assert.That(GuestProgress.RoomIdentity("slot-0", a), Is.Not.EqualTo(SaveFileNames.DefaultPlayerId), "모든 슬롯이 같은 local-player면 두 PC가 서로를 밀어낸다");
        }
    }
}
