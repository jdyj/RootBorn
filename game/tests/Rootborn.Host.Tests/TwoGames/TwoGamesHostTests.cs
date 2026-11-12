using System;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Room;
using Rootborn.Core.Save;
using Rootborn.Data.Save;
using Rootborn.Screens;
using Rootborn.Screens.Start;
using Rootborn.Ui;

namespace Rootborn.Host.Tests.TwoGames
{
    /// <summary>
    /// 같이 하기를 실제 게임 둘로: 이 테스트 프로세스의 게임(방장) + 자식 프로세스의 게임(손님, <see cref="PeerGame"/>). 둘 다 실제 창과 게임 루프이고
    /// 조작은 둘 다 PlayerDriver의 동사다(키·글자는 SDL 이벤트 큐, 포인터는 게임의 포인터 장치 계층). 두 게임은 번갈아 한 프레임씩 돈다.
    /// 근거: 두 프로세스, 같은 기계 루프백 UDP, 장치 이벤트 주입(OS 입력 계층 미확인) — 두 기계와 사람 플레이는 미확인.
    /// 주소는 루프백(127.0.0.1)이고 포트는 0으로 받아 운영체제가 준 빈 포트다 — 같은 기계 안에서만 오가는 시험용 값이라 안전하다.
    /// 준비(입력이 아닌 것)는 각 테스트에 적는다: 이야기가 있는 슬롯 파일 쓰기(들어가기·방 열기 창은 이야기가 있는 슬롯만 고른다) 등.
    /// 스크린샷: output/two-games-evidence/.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed partial class TwoGamesHostTests
    {
        private const string Loopback = "127.0.0.1:0"; // 시험용: 이 기계 안에서만, 빈 포트
        private string _hostSaves, _guestSaves, _evidence, _repo;
        private PlayerDriver _host;
        private PeerGame _guest;

        [SetUp]
        public void SetUp()
        {
            string id = Guid.NewGuid().ToString("N");
            _hostSaves = Path.Combine(Path.GetTempPath(), "rootborn-two-games-host-" + id);
            _guestSaves = Path.Combine(Path.GetTempPath(), "rootborn-two-games-guest-" + id);
            Directory.CreateDirectory(_hostSaves);
            Directory.CreateDirectory(_guestSaves);
            _repo = HostOptions.Resolve(new[] { "--sample" }).RepoRoot;
            _evidence = Path.Combine(_repo, "output", "two-games-evidence");
        }

        [TearDown]
        public void TearDown()
        {
            try { _guest?.Dispose(); }
            finally
            {
                _guest = null;
                try { _host?.Dispose(); }
                finally
                {
                    _host = null;
                    foreach (string dir in new[] { _hostSaves, _guestSaves })
                        try { Directory.Delete(dir, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                }
            }
        }

        // ---------- 띄우기 ----------

        /// <summary>준비: 이야기가 있는 슬롯(슬롯 정보 파일)을 쓴다. 신원은 이 파일에서 나온다(두 PC가 같은 신원이 되지 않게 시각·씨앗이 다르다).</summary>
        private void WriteStory(string saveRoot, string slot, string name, int seed)
        {
            var model = new CharacterCreatorModel(CreatorLooks.Load(Path.Combine(_repo, "data")));
            SaveSlotMetadata metadata = SaveSlots.CreateMetadata(slot, name, model.Snapshot(), DateTime.UtcNow.Ticks + seed, seed);
            new FileSaveStore(saveRoot).Write(slot, SaveFileNames.Metadata, metadata);
        }

        private PlayerDriver LaunchHost(params string[] extra)
        {
            HostOptions options = HostOptions.Resolve(new[] { "--save-root", _hostSaves, "--size", "1280x720", "--evidence", _evidence }.Concat(extra).ToArray());
            options.TestMode = true;
            var player = new PlayerDriver(options);
            if (_guest != null) _guest.Partner = player;
            player.Frames(3);
            return player;
        }

        private PeerGame StartGuest(params string[] extra)
        {
            var guest = PeerGame.Start("손님");
            guest.Partner = _host;
            guest.Launch(new[] { "--save-root", _guestSaves, "--size", "1280x720", "--evidence", _evidence }.Concat(extra).ToArray());
            return guest;
        }

        /// <summary>방장 게임만 조건까지 돌린다(손님이 있으면 같이 돈다). 프레임 수로 센다.</summary>
        private static bool Until(PlayerDriver player, Func<bool> condition, int maxFrames = 600)
        {
            for (int i = 0; i <= maxFrames; i++)
            {
                if (condition()) return true;
                player.Frames(1);
            }
            return false;
        }

        /// <summary>방장: 시작 화면의 "방 열기" → 같이 쓸 것을 포인터로 켬 → 주소를 쳐서 연다. 들어갈 때까지 돌린다.</summary>
        private void HostOpensFromStartMenu(string shot, params string[] shares)
        {
            Assert.That(_host.Screens.IsOpen(ScreenCatalog.SaveSlotSelect), Is.True, "실행하면 슬롯 선택부터");
            _host.PressButton(SaveSlotScreen.HostId);
            _host.WaitForScreen(ScreenCatalog.HostRoom);
            var modal = (HostRoomScreen)_host.Screens.Find(ScreenCatalog.HostRoom);
            foreach (string piece in shares) _host.PressButton(HostRoomScreen.ShareToggleId(piece));
            Assert.That(modal.ChosenSharedPieces, Is.EquivalentTo(shares), "포인터로 켠 것");
            _host.PressButton(HostRoomScreen.AddressId);
            _host.TypeText(Loopback);
            _host.MovePointer(-50, -50);
            _host.Frames(2);
            if (shot != null) TestContext.WriteLine("스크린샷: " + _host.ScreenshotScreen(ScreenCatalog.HostRoom, shot));
            _host.PressButton(HostRoomScreen.OpenId);
            Assert.That(Until(_host, () => _host.Game.World != null && _host.Game.Hosting.IsOpen), Is.True, "방을 열고 들어간다");
        }

        /// <summary>손님: 시작 화면의 "서버 주소로 들어가기" → 주소를 쳐서 들어간다. 월드가 뜰 때까지 두 게임을 같이 돌린다.</summary>
        private void GuestJoinsByAddress(string address, string shot)
        {
            Assert.That(_guest.QueryBool("screen-open", ScreenCatalog.SaveSlotSelect), Is.True, "손님도 슬롯 선택부터");
            _guest.PressButton(SaveSlotScreen.JoinId);
            _guest.WaitForScreen(ScreenCatalog.JoinServer);
            _guest.PressButton(JoinServerScreen.AddressId);
            _guest.TypeText(address);
            Assert.That(_guest.Query("text", JoinServerScreen.AddressId), Is.EqualTo(address));
            _guest.Call("move-pointer", "-50", "-50");
            if (shot != null) TestContext.WriteLine("스크린샷(손님): " + _guest.ScreenshotScreen(ScreenCatalog.JoinServer, shot));
            _guest.PressButton(JoinServerScreen.ConnectId);
            Assert.That(_guest.UntilQuery("True", 900, "in-world"), Is.True, "손님이 주소로 들어가 월드가 뜬다\n" + _guest.Output);
            Assert.That(_guest.QueryBool("in-room"), Is.True);
        }

        private string GuestId => _guest.Query("room-player-id");

        // ---------- 시나리오 ----------

        /// <summary>
        /// 방장이 시작 화면의 방 열기로(돈·집 공유 켬) 방을 열고 손님이 서버 주소로 들어가기로 들어온다. 서로 화면에 보이고, 손님이 방향 키로 걸으면
        /// 방장 화면에서 움직인다(걷는 중). 손님이 문으로 다른 맵에 가면 서로 안 보인다.
        /// 준비: 두 PC(세이브 폴더)에 이야기가 있는 슬롯 파일.
        /// </summary>
        [Test]
        public void TWO_GAMES_001_HostOpens_GuestJoinsByAddress_TheySeeEachOtherWalk_NotOnOtherMaps()
        {
            WriteStory(_hostSaves, "slot-1", "방장", 101);
            WriteStory(_guestSaves, "slot-1", "손님", 202);
            _host = LaunchHost();
            HostOpensFromStartMenu("two-01-host-modal", RoomWallet.PieceName, RoomHouse.PieceName);
            _guest = StartGuest();
            GuestJoinsByAddress(_host.Game.Hosting.ListenAddress, "two-01-guest-join-modal");

            string hostId = _host.Game.Room.PlayerId, guestId = GuestId;
            Assert.That(guestId, Is.Not.Empty.And.Not.EqualTo(hostId), "다른 PC의 인물은 다른 신원");
            Assert.That(_guest.Query("map"), Is.EqualTo(_host.World.Map.Id), "둘 다 이야기의 첫 맵");
            Assert.That(_guest.Until(() => _host.Game.Remotes.Visible.Count == 1 && _guest.Query("remotes") == hostId), Is.True, "서로 보인다");
            Assert.That(_host.Game.Remotes.Visible[0].PlayerId, Is.EqualTo(guestId));
            Assert.That(_guest.Until(() => _host.Game.World.RemotesDrawn == 1 && _guest.QueryInt("remotes-drawn") == 1), Is.True, "서로의 화면에 그려진다");
            TestContext.WriteLine("스크린샷: " + _host.Screenshot("two-01-host-sees-guest"));
            TestContext.WriteLine("스크린샷(손님): " + _guest.Screenshot("two-01-guest-sees-host"));

            // 손님이 방향 키로 4칸 걸어 나간다(손님 게임 안의 길찾기 + 방향 키) → 그동안 방장 화면에서 손님이 걷는 중으로 보이고 자리가 바뀐다.
            float bx = _host.Game.Remotes.Visible[0].X, by = _host.Game.Remotes.Visible[0].Y;
            bool walking = false;
            _guest.WhileCalling = () => walking |= _host.Game.Remotes.Visible.Count == 1 && _host.Game.Remotes.Visible[0].Walking;
            _guest.Call("walk-away", "4");
            _guest.WhileCalling = null;
            Assert.That(walking, Is.True, "손님이 걷는 동안 방장 화면에 걷는 중으로 보인다");
            // 남의 몸은 받은 자세를 실제 시간으로 메워 보인다(조금 늦게 따라온다). 테스트는 프레임을 실제보다 빨리 돌리므로 따라올 때까지 돌린다.
            string[] guestPos = _guest.Query("pos").Split(',');
            float gx = float.Parse(guestPos[0], CultureInfo.InvariantCulture), gy = float.Parse(guestPos[1], CultureInfo.InvariantCulture);
            Assert.That(_guest.Until(() => _host.Game.Remotes.Visible.Count == 1 && !_host.Game.Remotes.Visible[0].Walking
                && Math.Abs(_host.Game.Remotes.Visible[0].X - gx) < 1f && Math.Abs(_host.Game.Remotes.Visible[0].Y - gy) < 1f, 3000), Is.True,
                "멈춘 손님이 방장 화면에서 손님 게임의 자리에 선다");
            TestContext.WriteLine("[걷기] 방장 화면의 손님 " + bx + "," + by + " → " + _host.Game.Remotes.Visible[0].X + "," + _host.Game.Remotes.Visible[0].Y + ", 손님 게임 " + _guest.Query("pos"));
            float moved = MathF.Sqrt(MathF.Pow(_host.Game.Remotes.Visible[0].X - bx, 2) + MathF.Pow(_host.Game.Remotes.Visible[0].Y - by, 2));
            Assert.That(moved, Is.GreaterThan(_host.World.Map.Grid.CellSize * 2f), "움직임이 보인다(4칸 걸음)");
            TestContext.WriteLine("스크린샷: " + _host.Screenshot("two-01-host-sees-guest-walked"));

            // 손님이 문으로 마을에 간다 → 서로 안 보인다.
            Assert.That(_guest.WalkThroughDoor("town"), Is.EqualTo("Town"));
            Assert.That(_guest.Until(() => _host.Game.Remotes.Visible.Count == 0 && _guest.Query("remotes") == string.Empty), Is.True, "다른 맵이면 안 보인다");
            _host.Frames(2);
            Assert.That(_host.Game.World.RemotesDrawn, Is.EqualTo(0));
            TestContext.WriteLine("스크린샷(손님): " + _guest.Screenshot("two-01-guest-in-town-alone"));
            Assert.That(_host.CollisionViolations, Is.EqualTo(0));
            Assert.That(_guest.QueryInt("collision-violations"), Is.EqualTo(0));
        }
    }
}
