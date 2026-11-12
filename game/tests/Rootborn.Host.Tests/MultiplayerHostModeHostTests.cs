using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using NUnit.Framework;
using Rootborn.Core.Save;
using Rootborn.Core.Time;
using Rootborn.Core.World;
using Rootborn.Data.Save;
using Rootborn.Host.Net;
using Rootborn.Net.Room;
using Rootborn.Net.Session;
using Rootborn.Net.Transport;
using Rootborn.Screens;
using Rootborn.Screens.Settings;
using Rootborn.Screens.Start;
using Rootborn.Ui;
using Rootborn.World;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 내 PC에서 방을 연다(설계 6절 "내 PC에서 열기"): 같은 Host 프로세스 안에 방이 돌고, 나는 같은 프로세스 안 전송으로 들어간 플레이어,
    /// 친구는 UDP로 들어온다(SessionClient를 직접 — 두 번째 Host 창이 아니다). 방의 틱은 Host의 게임 루프에서 돌므로 친구도
    /// 테스트 스레드에서 프레임마다 돌린다. 주소는 루프백(127.0.0.1)이고 포트는 0으로 받아 운영체제가 준 빈 포트다 — 같은 기계 안에서만
    /// 오가는 시험용 값이라 안전하다(방화벽 창도 뜨지 않는다).
    /// 근거: 장치 이벤트 주입(OS 입력 계층 미확인), 같은 기계 루프백 UDP — 두 기계는 미확인. 세이브 폴더는 임시 폴더.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class MultiplayerHostModeHostTests
    {
        private const string Loopback = "127.0.0.1:0"; // 시험용: 이 기계 안에서만, 빈 포트
        private string _saves;
        private readonly List<SessionClient> _friends = new List<SessionClient>();
        private readonly Stopwatch _clock = Stopwatch.StartNew();

        [SetUp]
        public void SetUp()
        {
            _saves = Path.Combine(Path.GetTempPath(), "rootborn-mp-hostmode-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (SessionClient friend in _friends) friend.Dispose();
            _friends.Clear();
            try { Directory.Delete(_saves, true); } catch (IOException) { }
        }

        private double Now => _clock.Elapsed.TotalSeconds;

        private PlayerDriver Launch(params string[] extra)
        {
            HostOptions options = HostOptions.Resolve(new[] { "--save-root", _saves, "--size", "1280x720" }.Concat(extra).ToArray());
            options.TestMode = true;
            options.EvidenceDir = Path.Combine(options.RepoRoot, "output", "host-multiplayer-evidence");
            var player = new PlayerDriver(options);
            player.Frames(3);
            return player;
        }

        /// <summary>조건이 될 때까지 프레임을 돌리고(방은 그 안에서 돈다) 친구들도 돌린다. 실제 시간 seconds를 넘기면 false.</summary>
        private bool Until(PlayerDriver player, Func<bool> condition, double seconds = 10)
        {
            var watch = Stopwatch.StartNew();
            while (watch.Elapsed.TotalSeconds < seconds)
            {
                player.Frames(1);
                foreach (SessionClient friend in _friends) friend.Update(Now);
                if (condition()) return true;
                Thread.Sleep(1);
            }
            return false;
        }

        private SessionClient AddFriend(PlayerDriver player, string id, string look, RoomVersions? versions = null)
        {
            var client = RoomClient.Create(UdpTransport.Connect(IPAddress.Loopback, IPAddress.Loopback.ToString(), player.Game.Hosting.Room.Port, RoomProtocol.ConnectionKey),
                versions ?? player.Game.Room.Versions, id, look, new SessionClientSettings());
            _friends.Add(client);
            return client;
        }

        private void WriteStory(string slot, string name)
        {
            string repo = HostOptions.Resolve(new[] { "--sample" }).RepoRoot;
            var model = new CharacterCreatorModel(CreatorLooks.Load(Path.Combine(repo, "data")));
            SaveSlotMetadata metadata = SaveSlots.CreateMetadata(slot, name, model.Snapshot(), DateTime.UtcNow.Ticks, 4242);
            new FileSaveStore(_saves).Write(slot, SaveFileNames.Metadata, metadata);
        }

        private bool HostedAndIn(PlayerDriver player) => player.Game.World != null && player.Game.Hosting.IsOpen;

        [Test]
        public void MP_HOST_001_HostArgument_OpensTheRoomAndEnters_AFriendJoinsOverUdp_TheySeeEachOther_SettingsShowsTheRoom()
        {
            using PlayerDriver player = Launch("--host", Loopback, "--slot", "slot-0");
            Assert.That(Until(player, () => HostedAndIn(player)), Is.True, "방을 열고 그 슬롯으로 들어가 월드가 뜬다");
            RoomHosting hosting = player.Game.Hosting;
            Assert.That(hosting.Room.Port, Is.GreaterThan(0), "포트 0이면 운영체제가 준 빈 포트");
            Assert.That(hosting.ListenAddress, Is.EqualTo("127.0.0.1:" + hosting.Room.Port));
            Assert.That(hosting.Room.Server.IsNewRoom, Is.True);
            Assert.That(hosting.PlayerCount, Is.EqualTo(1), "나 혼자");
            Assert.That(player.Game.Session, Is.Not.Null, "혼자 하기와 같은 게임 한 판");
            Assert.That(player.Screens.IsOpen(ScreenCatalog.HudClock), Is.True, "혼자 하기와 같은 화면");
            Assert.That(File.Exists(Path.Combine(_saves, "slot-0", SaveFileNames.RoomClock)), Is.True, "방의 시계는 내 슬롯 폴더에");
            string me = player.Game.Room.PlayerId;
            Assert.That(hosting.Room.Server.Players.Select(p => p.PlayerId), Is.EqualTo(new[] { me }));

            Assert.That(player.WalkThroughDoor("town"), Is.EqualTo("Town"), "방을 연 채로 문으로 맵을 옮긴다");
            Walker walker = player.World.Walker;
            float cell = player.World.Map.Grid.CellSize;
            string look = player.Game.World.PlayerLook.Code;
            SessionClient friend = AddFriend(player, "friend", look);
            Assert.That(Until(player, () => friend.Status == ClientStatus.Joined && hosting.PlayerCount == 2), Is.True, "친구가 UDP로 들어온다");

            float fx = walker.X + cell * 1.5f, fy = walker.Y;
            friend.SetPose("Town", fx, fy, Facing.Left, false);
            Assert.That(Until(player, () => player.Game.Remotes.Visible.Count == 1 && player.Game.World.RemotesDrawn == 1
                && Math.Abs(player.Game.Remotes.Visible[0].X - fx) < 0.01f), Is.True, "같은 맵의 친구가 내 화면에 그려진다");
            Assert.That(player.Game.Remotes.Visible[0].PlayerId, Is.EqualTo("friend"));
            Assert.That(Until(player, () => friend.PlayersOn("Town", Now).Any(p => p.PlayerId == me && p.Look == look)), Is.True,
                "친구 쪽에 내 몸과 겉모습이 보인다");
            player.Screenshot("hostmode-01-town-friend");

            player.OpenScreenWithKey(TestKey.Escape, ScreenCatalog.Settings);
            Assert.That(player.Node(SettingsScreen.RoomLineId).IsActive, Is.True, "방을 연 사람의 설정 화면에 방 줄");
            string line = ((UiTextBlock)player.Node(SettingsScreen.RoomTextId)).Text;
            Assert.That(line, Is.EqualTo(player.Game.Text.Format("ui.settings.room-open", hosting.ListenAddress, 2, 4)), line);
            TestContext.WriteLine("스크린샷: " + player.ScreenshotScreen(ScreenCatalog.Settings, "hostmode-02-settings-room-line"));

            friend.Dispose();
            _friends.Clear();
            Assert.That(Until(player, () => hosting.PlayerCount == 1 && ((UiTextBlock)player.Node(SettingsScreen.RoomTextId)).Text
                == player.Game.Text.Format("ui.settings.room-open", hosting.ListenAddress, 1, 4)), Is.True, "친구가 나가면 사람 수가 준다");
        }

        [Test]
        public void MP_HOST_002_FullRoom_AndOtherVersion_AreRefused()
        {
            using PlayerDriver player = Launch("--host", Loopback, "--slot", "slot-0", "--max-players", "2");
            Assert.That(Until(player, () => HostedAndIn(player)), Is.True);
            Assert.That(player.Game.Hosting.MaxPlayers, Is.EqualTo(2));
            SessionClient friend = AddFriend(player, "friend", "child|");
            Assert.That(Until(player, () => friend.Status == ClientStatus.Joined), Is.True);
            SessionClient third = AddFriend(player, "third", "child|");
            Assert.That(Until(player, () => third.Status == ClientStatus.Refused || third.Status == ClientStatus.Disconnected), Is.True);
            Assert.That(third.FarewellReason, Is.EqualTo(RoomRefusal.Full), "정원 초과");

            RoomVersions mine = player.Game.Room.Versions;
            SessionClient other = AddFriend(player, "other", "child|", new RoomVersions(mine.Protocol, mine.Game + ".other-build", mine.Data));
            Assert.That(Until(player, () => other.Status == ClientStatus.Refused || other.Status == ClientStatus.Disconnected), Is.True);
            Assert.That(other.FarewellReason, Is.EqualTo(RoomRefusal.GameVersionMismatch), "버전이 다르면 거절");
            Assert.That(player.Game.Hosting.PlayerCount, Is.EqualTo(2));
        }

        [Test]
        public void MP_HOST_003_StartMenuButton_TypeTheAddress_PickTheSize_OpenWithAStory()
        {
            WriteStory("slot-1", "별");
            using PlayerDriver player = Launch();
            Assert.That(player.Screens.IsOpen(ScreenCatalog.SaveSlotSelect), Is.True);
            player.PressButton(SaveSlotScreen.HostId);
            player.WaitForScreen(ScreenCatalog.HostRoom);
            var screen = (HostRoomScreen)player.Screens.Find(ScreenCatalog.HostRoom);
            Assert.That(screen.SlotIds, Is.EqualTo(new[] { "slot-1" }));
            player.PressButton(HostRoomScreen.AddressId);
            Assert.That(player.Game.TextInputActive, Is.True);
            player.TypeText(Loopback);
            UiRect three = ((UiTabBar)player.Node(HostRoomScreen.CapacityId)).TabRect(2);
            player.ClickAt(three.CenterX, three.CenterY);
            Assert.That(screen.MaxPlayers, Is.EqualTo(3));
            player.Screenshot("hostmode-03-host-modal");
            TestContext.WriteLine("스크린샷: " + player.ScreenshotScreen(ScreenCatalog.HostRoom, "hostmode-03-host-modal"));
            player.PressButton(HostRoomScreen.OpenId);
            Assert.That(Until(player, () => HostedAndIn(player)), Is.True, "주소를 쳐서 방을 열고 들어간다");
            Assert.That(player.Game.World.SlotStart.NewGame, Is.False, "고른 이야기로 이어서 시작한다");
            Assert.That(player.Game.Hosting.MaxPlayers, Is.EqualTo(3));
            Assert.That(File.Exists(Path.Combine(_saves, "slot-1", SaveFileNames.RoomClock)), Is.True);
        }

        [Test]
        public void MP_HOST_004_BadOrBusyAddress_SaysWhy_AndOpensNothing()
        {
            WriteStory("slot-0", "별");
            using var busy = UdpTransport.Listen(IPAddress.Loopback, 0, RoomProtocol.ConnectionKey); // 이미 쓰는 루프백 포트(시험용 값)
            using PlayerDriver player = Launch("--host", "127.0.0.1:" + busy.LocalPort, "--slot", "slot-0");
            var screen = (HostRoomScreen)player.Screens.Find(ScreenCatalog.HostRoom);
            Assert.That(Until(player, () => player.Game.Hosting.Phase == RoomJoinPhase.Failed, 5), Is.True);
            Assert.That(player.Game.Hosting.FailureCode, Is.EqualTo(RoomHostText.CannotListen));
            Assert.That(screen.StatusKey, Is.EqualTo("ui.join.reason.cannot-listen"));
            Assert.That(player.Game.World, Is.Null);
            Assert.That(player.Game.Hosting.Room, Is.Null, "듣지 못하면 방을 남기지 않는다");
            player.ScreenshotScreen(ScreenCatalog.HostRoom, "hostmode-04-cannot-listen");

            player.PressButton(HostRoomScreen.AddressId);
            for (int i = 0; i < 24; i++) player.PressKey(TestKey.Backspace);
            player.TypeText("no-port");
            player.PressButton(HostRoomScreen.OpenId);
            Assert.That(player.Game.Hosting.FailureCode, Is.EqualTo(RoomJoinText.BadAddress));
            Assert.That(screen.StatusKey, Is.EqualTo("ui.join.reason.bad-address"));
        }

        [Test]
        public void MP_HOST_005_CloseFromSettings_TellsTheFriend_SavesTheRoom_BackToTheStartMenu_ThenReopenContinues_AndSoloStillReads()
        {
            using (PlayerDriver player = Launch("--host", Loopback, "--slot", "slot-0"))
            {
                Assert.That(Until(player, () => HostedAndIn(player)), Is.True);
                Assert.That(player.WalkThroughDoor("town"), Is.EqualTo("Town"));
                SessionClient friend = AddFriend(player, "friend", "child|");
                Assert.That(Until(player, () => friend.Status == ClientStatus.Joined && player.Game.Hosting.PlayerCount == 2), Is.True);
                int day = player.Game.Hosting.Room.Server.Clock.Day;
                File.Delete(Path.Combine(_saves, "slot-0", SaveFileNames.RoomClock)); // 닫을 때 다시 쓰는지 본다

                player.OpenScreenWithKey(TestKey.Escape, ScreenCatalog.Settings);
                player.PressButton(SettingsScreen.RoomCloseId);
                Assert.That(Until(player, () => player.Game.World == null && player.Screens.IsOpen(ScreenCatalog.SaveSlotSelect)), Is.True, "방을 닫으면 시작 화면");
                Assert.That(player.Screens.IsOpen(ScreenCatalog.RoomNotice), Is.False, "내가 닫았으니 끊김 알림은 없다");
                Assert.That(player.Game.Hosting.IsOpen, Is.False);
                Assert.That(Until(player, () => friend.Status != ClientStatus.Joined, 5), Is.True);
                Assert.That(friend.FarewellReason, Is.EqualTo(RoomRefusal.Shutdown), "들어와 있던 친구에게 닫혔다고 알린다");
                Assert.That(new FileSaveStore(_saves).TryRead("slot-0", SaveFileNames.RoomClock, out RoomClockSaveData saved), Is.True, "닫을 때 방을 내 슬롯에 저장한다");
                Assert.That(saved.Day, Is.EqualTo(day));
                Assert.That(new FileSaveStore(_saves).TryRead("slot-0", SaveFileNames.WorldProgress, out WorldProgressSaveData progress), Is.True);
                Assert.That(progress.CurrentScene, Is.EqualTo("Town"), "내 진행도 내 슬롯에");
                player.Screenshot("hostmode-05-closed-back-to-start");

                // 같은 슬롯으로 다시 연다(시작 화면의 방 열기) → 그 방을 잇는다.
                player.PressButton(SaveSlotScreen.HostId);
                player.WaitForScreen(ScreenCatalog.HostRoom);
                player.PressButton(HostRoomScreen.AddressId);
                player.TypeText(Loopback);
                player.PressButton(HostRoomScreen.OpenId);
                Assert.That(Until(player, () => HostedAndIn(player)), Is.True);
                Assert.That(player.Game.Hosting.Room.Server.IsNewRoom, Is.False, "같은 슬롯으로 다시 열면 이어진다");
                Assert.That(player.Game.Hosting.Room.Server.Clock.Day, Is.EqualTo(day));
                Assert.That(player.Game.World.Session.Map.Id, Is.EqualTo("Town"), "마지막 자리(마을)에서 잇는다");
            }

            // 방을 연 슬롯으로 혼자 이어 하기: 방의 파일이 혼자 하기를 깨지 않는다.
            using PlayerDriver solo = Launch("--slot", "slot-0");
            Assert.That(solo.Game.World, Is.Not.Null, "방을 연 슬롯도 혼자 이어 하기로 읽힌다");
            Assert.That(solo.Game.SlotDamage, Is.Null);
            Assert.That(solo.Game.Session, Is.Not.Null);
            Assert.That(solo.Game.World.SlotStart.NewGame, Is.False);
            Assert.That(solo.World.Map.Id, Is.EqualTo("Town"));
            Assert.That(solo.Game.Hosting, Is.Null, "혼자 하기에는 방이 없다");
            Assert.That(File.Exists(Path.Combine(_saves, "slot-0", SaveFileNames.RoomClock)), Is.True, "혼자 하기가 방의 파일을 지우지 않는다");
        }

        [Test]
        public void MP_HOST_006_QuittingTheGame_TellsTheFriendAtOnce_AndSaves()
        {
            PlayerDriver player = Launch("--host", Loopback, "--slot", "slot-0");
            SessionClient friend;
            try
            {
                Assert.That(Until(player, () => HostedAndIn(player)), Is.True);
                friend = AddFriend(player, "friend", "child|");
                Assert.That(Until(player, () => friend.Status == ClientStatus.Joined), Is.True);
                File.Delete(Path.Combine(_saves, "slot-0", SaveFileNames.RoomClock));
            }
            finally { player.Dispose(); }
            var watch = Stopwatch.StartNew();
            while (watch.Elapsed.TotalSeconds < 3 && friend.Status == ClientStatus.Joined)
            {
                friend.Update(Now);
                Thread.Sleep(5);
            }
            Assert.That(friend.FarewellReason, Is.EqualTo(RoomRefusal.Shutdown), "게임을 끄면 시간 초과를 기다리지 않고 닫혔다고 알린다");
            Assert.That(File.Exists(Path.Combine(_saves, "slot-0", SaveFileNames.RoomClock)), Is.True, "끌 때 방을 저장한다");
        }

        /// <summary>
        /// 두 번째 Host 창(다른 PC 흉내: 다른 세이브 폴더)이 내 방에 주소로 들어온다. 한 테스트 프로세스에서 Host 둘을 띄우고 번갈아 한 프레임씩 돌린다.
        /// 두 창이 SDL 이벤트 큐를 같이 쓸 수 있어 키는 넣지 않는다(들어가기는 --join --slot, 닫기는 방을 연 게임을 끈다).
        /// 스크린샷은 손님 창 하나만(아래 주석).
        /// </summary>
        [Test]
        public void MP_HOST_008_ASecondGameWindow_JoinsMyRoom_BothSeeEachOther_AndIsToldWhenIQuit()
        {
            string otherPc = Path.Combine(Path.GetTempPath(), "rootborn-mp-hostmode-guest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(otherPc);
            PlayerDriver host = Launch("--host", Loopback, "--slot", "slot-0");
            try
            {
                Assert.That(Until(host, () => HostedAndIn(host)), Is.True);
                // 다른 PC에는 이야기가 있는 슬롯(새 슬롯끼리는 신원이 slot-<id>로 같아 서로 밀어낸다 — 보고에 적은 알려진 문제).
                string repo = HostOptions.Resolve(new[] { "--sample" }).RepoRoot;
                new FileSaveStore(otherPc).Write("slot-0", SaveFileNames.Metadata,
                    SaveSlots.CreateMetadata("slot-0", "손님", new CharacterCreatorModel(CreatorLooks.Load(Path.Combine(repo, "data"))).Snapshot(), DateTime.UtcNow.Ticks, 777));
                HostOptions options = HostOptions.Resolve(new[] { "--save-root", otherPc, "--size", "1280x720", "--join", host.Game.Hosting.ListenAddress, "--slot", "slot-0" });
                options.TestMode = true;
                options.EvidenceDir = Path.Combine(options.RepoRoot, "output", "host-multiplayer-evidence");
                using var guest = new PlayerDriver(options);
                bool Both(Func<bool> condition)
                {
                    var watch = Stopwatch.StartNew();
                    while (watch.Elapsed.TotalSeconds < 15)
                    {
                        host.Frames(1);
                        guest.Frames(1);
                        if (condition()) return true;
                    }
                    return false;
                }

                Assert.That(Both(() => guest.Game.World != null && guest.Game.Room.InRoom && host.Game.Hosting.PlayerCount == 2), Is.True, "두 번째 창이 주소로 들어온다");
                Assert.That(guest.Game.Room.PlayerId, Is.Not.EqualTo(host.Game.Room.PlayerId), "다른 PC의 인물은 다른 신원");
                string map = host.World.Map.Id;
                Assert.That(guest.World.Map.Id, Is.EqualTo(map), "둘 다 새 이야기의 첫 맵(이야기만 쓴 슬롯도 새 게임 자리에서 시작)");
                Assert.That(Both(() => host.Game.Remotes.Visible.Count == 1 && guest.Game.Remotes.Visible.Count == 1), Is.True, "서로 보인다");
                Assert.That(host.Game.Remotes.Visible[0].PlayerId, Is.EqualTo(guest.Game.Room.PlayerId));
                Assert.That(guest.Game.Remotes.Visible[0].PlayerId, Is.EqualTo(host.Game.Room.PlayerId));
                Assert.That(guest.Game.World.RemotesDrawn, Is.EqualTo(1));
                // 한 프로세스에 창이 둘이면 그리기 맥락이 하나만 맞아 나중에 띄운 창(손님)의 스크린샷만 믿을 수 있다(방을 연 창의 그림은 깨져 나온다).
                guest.Screenshot("hostmode-07-two-windows-guest");

                host.Dispose();
                host = null;
                var watch2 = Stopwatch.StartNew();
                while (watch2.Elapsed.TotalSeconds < 5 && !guest.Screens.IsOpen(ScreenCatalog.RoomNotice)) guest.Frames(1);
                Assert.That(guest.Screens.IsOpen(ScreenCatalog.RoomNotice), Is.True, "방을 연 게임을 끄면 들어온 사람은 시작 화면에서 알림을 본다");
                Assert.That(((RoomNoticeScreen)guest.Screens.Find(ScreenCatalog.RoomNotice)).ReasonKey, Is.EqualTo("ui.join.reason.server-shutdown"));
                Assert.That(guest.Game.World, Is.Null);
            }
            finally
            {
                host?.Dispose();
                try { Directory.Delete(otherPc, true); } catch (IOException) { }
            }
        }

        /// <summary>
        /// 흐르는 시간 4단계: 방을 연 창(--host)과 UDP로 들어온 손님(창 없는 게임 한 판 — GameSession + RemoteRoomTime)이 같은 방의 시각을 본다.
        /// 방을 연 사람이 잠자리에서 E로 자면 "자는 중" 화면(아직 깨어 있는 사람)이 보이고 다음 날 단추가 꺼져 있다. 손님도 자면(명령) 곧바로 둘 다
        /// 다음 날 07:00 — 방을 연 사람이 단추를 눌러 2일째를 연다. 그 뒤 활동 하나는 시계를 건너뛰지 않고 HUD에 "하는 중 · 끝나는 시각"이 보인다
        /// (활동 시작은 명령 직접 호출 — 준비). 방을 연 창: 장치 이벤트 주입(OS 입력 계층 미확인). 손님: 자동 테스트(명령).
        /// 한 프로세스에 창을 둘 띄우면 입력과 그리기가 한 창에만 맞아 손님을 창 없는 판으로 둔다.
        /// </summary>
        [Test]
        public void MP_HOST_009_HostSleeps_SeesWhoIsAwake_TheGuestSleepsToo_BothWakeAtSeven_ThenTheHostIsBusy()
        {
            using PlayerDriver host = Launch("--host", Loopback, "--slot", "slot-0");
            Assert.That(Until(host, () => HostedAndIn(host)), Is.True);
            var definitions = host.Game.Session.Definitions;
            SessionClient friend = AddFriend(host, "friend", "child|");
            Assert.That(Until(host, () => friend.Status == ClientStatus.Joined && friend.RoomTime.Known), Is.True, "손님이 들어온다");
            string guestSaves = Path.Combine(_saves, "guest-pc");
            var time = new RemoteRoomTime(friend, "friend", definitions.TimeRules);
            var guest = Rootborn.Game.GameSession.Start(definitions, Rootborn.Game.GameStartSettings.FromDefinitions(definitions), new FileSaveStore(guestSaves), "slot-g", "friend", room: time);
            time.Changed += () => guest.SyncWithRoom();
            bool Run(Func<bool> condition, double seconds = 10) => Until(host, () => { guest.AdvanceTime(1f / 60f, false); return condition(); }, seconds);

            var hostGame = host.Game.Session;
            Assert.That(hostGame.IsSoloRoom, Is.False, "방을 연 사람의 게임도 방의 시계(손님 저장이 없는 방)");
            Assert.That(Run(() => hostGame.Room.Known && Math.Abs(guest.StudentTimeMinutes - hostGame.StudentTimeMinutes) <= RemoteRoomTime.ResyncMinutes), Is.True, "둘이 같은 시각");
            Assert.That(guest.Student.CurrentDay, Is.EqualTo(hostGame.Student.CurrentDay));

            // 방을 연 사람이 잠자리에서 잔다(E).
            InteractionThing bed = host.Game.World.HereData.Things.FirstOrDefault(t => t.Kind == Rootborn.Screens.Day.DayResultScreen.InteractionKind);
            Assert.That(bed, Is.Not.Null, host.World.Map.Id + "에 잠자리가 있다");
            host.WalkNear(bed.X, bed.Y, host.World.Map.Grid.CellSize * 1.25f);
            host.PressKey(TestKey.E);
            host.WaitForScreen(ScreenCatalog.DayResult);
            var result = (Rootborn.Screens.Day.DayResultScreen)host.Screens.Find(ScreenCatalog.DayResult);
            // "자는 중"은 내 게임이 잠든 순간 참이고 "손님은 깨어 있다"는 잠들기 전부터 참이다. 방이 내 잠 요청을 받아 세는 것은 그 뒤의 방 틱이라
            // 셈까지 조건에 넣어 기다린다(예전에는 바로 다음 줄에서 단정해 원격에서 한 번 "방이 내 잠을 셌다"로 실패했다).
            string me = host.Game.Room.PlayerId;
            Assert.That(Run(() => result.Model.Sleeping && result.Model.AwakeOthers.Count == 1 && host.Game.Hosting.Room.Server.Clock.IsAsleep(me)), Is.True,
                "자는 중 — 손님은 아직 깨어 있다, 방이 내 잠을 셌다");
            Assert.That(result.Model.AwakeOthers[0], Is.EqualTo("friend"));
            var next = (UiButton)host.Node(Rootborn.Screens.Day.DayResultScreen.NextDayId);
            Assert.That(next.Enabled, Is.False, "자면 07:00까지 깨어날 수 없다");
            Assert.That(next.Text, Is.EqualTo(host.Game.Text.Get("day-result.sleeping")));
            host.ScreenshotScreen(ScreenCatalog.DayResult, "hostmode-09-host-sleeping");

            // 손님도 잔다(명령) → 모두 잠들었으니 곧바로 다음 날 07:00.
            Assert.That(guest.Sleep().Ended, Is.True);
            Assert.That(Run(() => next.Enabled), Is.True, "모두 잠들면 다음 날 단추가 켜진다");
            Assert.That(next.Text, Is.EqualTo(host.Game.Text.Get("day-result.next-day")));
            Assert.That(host.Game.Hosting.Room.Server.Clock.Day, Is.EqualTo(2));
            Assert.That(TimeRules.ClockMinutes(host.Game.Hosting.Room.Server.Clock.Minutes) / 60, Is.EqualTo(7), "07:00");
            host.PressButton(Rootborn.Screens.Day.DayResultScreen.NextDayId);
            host.WaitForScreenClosed(ScreenCatalog.DayResult);
            Assert.That(hostGame.Student.CurrentDay, Is.EqualTo(2));
            Assert.That(((UiLabel)host.Node(Rootborn.Screens.Hud.ClockHudScreen.LabelId)).Text, Does.StartWith("2일  07:"));
            // 손님은 방의 다음 날을 UDP로 따로 받는다 — 방장 쪽 단추가 켜진 것으로 손님도 받았다고 보지 않고 기다린다
            // (원격 Linux에서 손님이 아직 받지 못해 StartNextDay가 false로 한 번 더 실패했다).
            Assert.That(Run(() => guest.CanStartNextDay), Is.True, "손님도 방의 다음 날을 받았다");
            Assert.That(guest.StartNextDay(), Is.True);

            // 활동 하나(준비: 명령 직접 호출) — 시계를 건너뛰지 않고 하는 중.
            int before = hostGame.StudentTimeMinutes;
            Assert.That(hostGame.PerformOutsideSchoolActivity("outside.help-neighbor", "host:busy").Kind, Is.EqualTo(Rootborn.Core.StudentLife.LifeActivityResultKind.Applied));
            host.Frames(5);
            Assert.That(hostGame.StudentTimeMinutes - before, Is.LessThan(10), "같이 할 때 활동은 시계를 건너뛰지 않는다");
            var clock = (Rootborn.Screens.Hud.ClockHudScreen)host.Screens.Find(ScreenCatalog.HudClock);
            Assert.That(clock.Model.Busy, Is.True);
            int ends = TimeRules.ClockMinutes(hostGame.ActivityEndsAtMinutes);
            Assert.That(((UiLabel)host.Node(Rootborn.Screens.Hud.ClockHudScreen.RoomLabelId)).Text,
                Does.Contain(string.Format("{0:00}:{1:00}", ends / 60, ends % 60)), "HUD에 끝나는 시각");
            Assert.That(guest.IsBusy, Is.False, "손님은 하는 중이 아니다");
            host.Screenshot("hostmode-10-host-busy");
        }

        /// <summary>
        /// 성능: 방을 열지 않은 혼자 하기에서는 방 코드가 없다(프레임마다 도는 서버 코드 없음). 방을 연 프레임의 할당을 잰다
        /// (방 하나 + 친구 하나가 걷는 동안). 잰 값은 출력에 남긴다. 상한은 넉넉히 — 프레임마다 쌓이는 큰 할당만 잡는다.
        /// </summary>
        [Test]
        public void MP_HOST_007_SoloHasNoRoomWork_AndAHostedFrameAllocatesLittle()
        {
            long solo;
            using (PlayerDriver alone = Launch("--slot", "slot-0"))
            {
                Assert.That(alone.Game.Hosting, Is.Null, "혼자 하기(--slot만)에는 방 여는 쪽이 없다");
                Assert.That(alone.Game.Room, Is.Null);
                solo = AllocatedPerFrame(alone, 240);
            }

            using PlayerDriver player = Launch("--host", Loopback, "--slot", "slot-1");
            Assert.That(Until(player, () => HostedAndIn(player)), Is.True);
            SessionClient friend = AddFriend(player, "friend", player.Game.World.PlayerLook.Code); // 그림으로 풀리는 겉모습이어야 그린다
            Assert.That(Until(player, () => friend.Status == ClientStatus.Joined), Is.True);
            Walker walker = player.World.Walker;
            string map = player.World.Map.Id;
            friend.SetPose(map, walker.X + 20f, walker.Y, Facing.Right, true);
            Assert.That(Until(player, () => player.Game.Remotes.Visible.Count == 1), Is.True);
            long hosted = AllocatedPerFrame(player, 240, () =>
            {
                friend.SetPose(map, walker.X + 20f, walker.Y, Facing.Right, true);
                friend.Update(Now);
            });
            TestContext.WriteLine($"[할당] 혼자 하기 {solo}바이트/프레임, 방을 열고 친구 하나 {hosted}바이트/프레임(차이 {hosted - solo})");
            Assert.That(hosted - solo, Is.LessThan(16 * 1024), "방을 연 프레임의 더 쓰는 할당이 프레임마다 16KB를 넘지 않는다");
        }

        private static long AllocatedPerFrame(PlayerDriver player, int frames, Action eachFrame = null)
        {
            player.Frames(30);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < frames; i++)
            {
                player.Frames(1);
                eachFrame?.Invoke();
            }
            return (GC.GetAllocatedBytesForCurrentThread() - before) / frames;
        }

        [Test]
        public void HostOption_NeedsAnIpAndPort_AndASaveFolder_NotWithJoinOrWorld()
        {
            Assert.Throws<InvalidOperationException>(() => HostOptions.Resolve(new[] { "--save-root", _saves, "--host", "no-port" }));
            Assert.Throws<InvalidOperationException>(() => HostOptions.Resolve(new[] { "--save-root", _saves, "--host", "host.example:7777" }), "이름이 아니라 이 PC의 IP 주소");
            Assert.Throws<InvalidOperationException>(() => HostOptions.Resolve(new[] { "--save-root", _saves, "--host", "127.0.0.1:70000" }));
            Assert.Throws<InvalidOperationException>(() => HostOptions.Resolve(new[] { "--host", "127.0.0.1:7777" }, _ => null), "주소 기본값도 세이브 폴더 기본값도 없다");
            Assert.Throws<InvalidOperationException>(() => HostOptions.Resolve(new[] { "--save-root", _saves, "--host", "127.0.0.1:7777", "--join", "10.0.0.1:7777" }));
            Assert.Throws<InvalidOperationException>(() => HostOptions.Resolve(new[] { "--save-root", _saves, "--host", "127.0.0.1:7777", "--world", "Town" }));
            Assert.Throws<InvalidOperationException>(() => HostOptions.Resolve(new[] { "--save-root", _saves, "--max-players", "3" }), "--max-players는 --host와");
            Assert.Throws<InvalidOperationException>(() => HostOptions.Resolve(new[] { "--save-root", _saves, "--host", "127.0.0.1:0", "--max-players", "9" }));
            Assert.Throws<InvalidOperationException>(() => HostOptions.Resolve(new[] { "--save-root", _saves, "--host", "127.0.0.1:0", "--max-players", "0" }));

            HostOptions withSlot = HostOptions.Resolve(new[] { "--save-root", _saves, "--host", "127.0.0.1:0", "--slot", "slot-0", "--max-players", "6" });
            Assert.That(withSlot.WorldActive, Is.False, "먼저 방을 열고 들어간 뒤 월드를 띄운다");
            Assert.That(withSlot.StartMenu, Is.True);
            Assert.That(withSlot.HostOnStart, Is.True);
            Assert.That(withSlot.MaxPlayers, Is.EqualTo(6));
            HostOptions viaEnv = HostOptions.Resolve(new[] { "--save-root", _saves },
                name => name == HostOptions.HostVariable ? "10.1.2.3:7777" : name == HostOptions.MaxPlayersVariable ? "5" : null);
            Assert.That(viaEnv.HostAddress, Is.EqualTo("10.1.2.3:7777"));
            Assert.That(viaEnv.MaxPlayers, Is.EqualTo(5));
            HostOptions none = HostOptions.Resolve(new[] { "--save-root", _saves }, name => name == HostOptions.MaxPlayersVariable ? "5" : null);
            Assert.That(none.HostAddress, Is.Null, "주소 기본값이 없다");
            Assert.That(none.StartMenu, Is.True, "서버와 같은 이름의 정원 환경 변수만 있으면 그냥 쓰지 않는다");
        }

        [Test]
        public void HostOption_Share_UsesTheServersNameAndRules_OnlyWithHost()
        {
            HostOptions given = HostOptions.Resolve(new[] { "--save-root", _saves, "--host", "127.0.0.1:0", "--share", "wallet, town" });
            Assert.That(given.SharedPieces, Is.EqualTo(new[] { "wallet", "town" }));
            Assert.That(HostOptions.Resolve(new[] { "--save-root", _saves, "--host", "127.0.0.1:0", "--share", "none" }).SharedPieces, Is.Empty, "시간만");
            Assert.That(HostOptions.Resolve(new[] { "--save-root", _saves, "--host", "127.0.0.1:0" }).SharedPieces, Is.Null, "주지 않으면 창에서 고른다");
            Assert.That(HostOptions.ShareVariable, Is.EqualTo(Rootborn.Server.ServerOptions.ShareVariable), "데디케이티드 서버와 같은 환경 변수 이름");
            HostOptions viaEnv = HostOptions.Resolve(new[] { "--save-root", _saves, "--host", "127.0.0.1:0" }, name => name == HostOptions.ShareVariable ? "wallet" : null);
            Assert.That(viaEnv.SharedPieces, Is.EqualTo(new[] { "wallet" }));
            Assert.Throws<InvalidOperationException>(() => HostOptions.Resolve(new[] { "--save-root", _saves, "--host", "127.0.0.1:0", "--share", "items" }), "가방은 조각이 아니다");
            Assert.Throws<InvalidOperationException>(() => HostOptions.Resolve(new[] { "--save-root", _saves, "--share", "wallet" }), "--share는 --host와");
            HostOptions envOnly = HostOptions.Resolve(new[] { "--save-root", _saves }, name => name == HostOptions.ShareVariable ? "wallet" : null);
            Assert.That(envOnly.SharedPieces, Is.Null, "서버와 같은 이름의 환경 변수만 있으면 그냥 쓰지 않는다");
        }

        [Test]
        public void ListenAddress_Parsing()
        {
            Assert.That(RoomAddress.TryParseListen("192.168.0.10:7777", out IPAddress ip, out int port), Is.True);
            Assert.That((ip, port), Is.EqualTo((IPAddress.Parse("192.168.0.10"), 7777)));
            Assert.That(RoomAddress.TryParseListen(" 0.0.0.0:0 ", out ip, out port), Is.True, "모든 주소, 빈 포트");
            Assert.That((ip, port), Is.EqualTo((IPAddress.Any, 0)));
            foreach (string bad in new[] { "", "host:7777", "1:7777", "1.2:7777", "::1:7777", "192.168.0.10", "192.168.0.10:", "192.168.0.10:-1", "192.168.0.10:65536", "300.1.1.1:7" })
                Assert.That(RoomAddress.TryParseListen(bad, out _, out _), Is.False, bad);
        }
    }
}
