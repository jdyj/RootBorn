using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using NUnit.Framework;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;
using Rootborn.Core.Room;
using Rootborn.Core.Save;
using Rootborn.Data;
using Rootborn.Core.World;
using Rootborn.Data.Save;
using Rootborn.Game;
using Rootborn.Net.Room;
using Rootborn.Net.Session;
using Rootborn.Net.Transport;
using Rootborn.Screens;
using Rootborn.Screens.Day;
using Rootborn.Screens.Hud;
using Rootborn.Screens.Settings;
using Rootborn.Screens.Start;
using Rootborn.Ui;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 내 PC에서 연 방의 같이 쓸 것(멀티플레이 설계 2절·4절): 방 열기 창에서 고르고, 방 설정 파일에 남고, 돈을 같이 쓰면 친구가 번 돈이 내 HUD에 보인다.
    /// 친구는 같은 테스트 프로세스의 게임 한 판(GameSession, 실제 data/, 임시 폴더 세이브)이 UDP 루프백으로 들어온 것이다(두 번째 Host 창이 아니다).
    /// 친구의 명령은 GameSession을 직접 부른다(준비 단계). 내 쪽은 장치 이벤트 주입(키·포인터), 같은 기계 루프백 — 두 기계는 미확인.
    /// 주소는 127.0.0.1, 포트 0(운영체제가 준 빈 포트) — 같은 기계 안에서만 오가는 시험용 값이다.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class MultiplayerRoomSharesHostTests
    {
        private const string Loopback = "127.0.0.1:0";
        private const string MoneyOnlyWork = "work.clinic-helper"; // 보상: 용돈 5
        private const float StoreX = 1264f, StoreY = 304f; // game/world/interactions.json의 가게 장소 지점(임시 지점)
        private string _saves, _friendSaves;
        private readonly List<Friend> _friends = new List<Friend>();
        private readonly Stopwatch _clock = Stopwatch.StartNew();

        private sealed class Friend
        {
            public SessionClient Client;
            public RoomGuestLink Link;
            public GameSession Game;
        }

        [SetUp]
        public void SetUp()
        {
            string id = Guid.NewGuid().ToString("N");
            _saves = Path.Combine(Path.GetTempPath(), "rootborn-mp-shares-" + id);
            _friendSaves = Path.Combine(Path.GetTempPath(), "rootborn-mp-shares-friend-" + id);
            Directory.CreateDirectory(_saves);
            Directory.CreateDirectory(_friendSaves);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Friend friend in _friends) friend.Client.Dispose();
            _friends.Clear();
            foreach (string dir in new[] { _saves, _friendSaves })
                try { Directory.Delete(dir, true); } catch (IOException) { }
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

        private bool Until(PlayerDriver player, Func<bool> condition, double seconds = 10)
        {
            var watch = Stopwatch.StartNew();
            while (watch.Elapsed.TotalSeconds < seconds)
            {
                player.Frames(1);
                foreach (Friend friend in _friends)
                {
                    friend.Link.FlushRequests();
                    friend.Client.Update(Now);
                }
                if (condition()) return true;
                Thread.Sleep(1);
            }
            return false;
        }

        private void WriteStory(string slot, string name)
        {
            string repo = HostOptions.Resolve(new[] { "--sample" }).RepoRoot;
            var model = new CharacterCreatorModel(CreatorLooks.Load(Path.Combine(repo, "data")));
            SaveSlotMetadata metadata = SaveSlots.CreateMetadata(slot, name, model.Snapshot(), DateTime.UtcNow.Ticks, 4242);
            new FileSaveStore(_saves).Write(slot, SaveFileNames.Metadata, metadata);
        }

        /// <summary>
        /// 친구가 UDP로 들어오고 방 설정과 자기 세이브를 받으면 그 조각으로 게임 한 판을 시작한다. 내 PC에서 연 방은 손님 진행을 내 슬롯 안 guests/에 받으므로
        /// 친구의 게임 한 판은 방이 내려 준 세이브(메모리)로 돌고 저장할 때마다 방으로 보낸다(Host GuestProgress.RoomStoreFor와 같은 일).
        /// </summary>
        private Friend Join(PlayerDriver player, string id)
        {
            var client = RoomClient.Create(UdpTransport.Connect(IPAddress.Loopback, IPAddress.Loopback.ToString(), player.Game.Hosting.Room.Port, RoomProtocol.ConnectionKey),
                player.Game.Room.Versions, id, player.Game.World.PlayerLook.Code, new SessionClientSettings());
            var friend = new Friend { Client = client, Link = new RoomGuestLink(client, new SessionClientSettings().MaxMessageBytes) };
            _friends.Add(friend);
            Assert.That(Until(player, () => client.Status == ClientStatus.Joined && friend.Link.Ready), Is.True, "친구가 들어와 방 설정을 받는다");
            Assert.That(friend.Link.TakesGuestSaves, Is.True, "내 PC에서 연 방도 손님 진행을 방에 둔다");
            GameDefinitions definitions = player.Game.Session.Definitions;
            var store = new MemorySaveSlot(RoomGuestLink.Slot, friend.Link.Files.Writes);
            store.Committed += c => friend.Link.Upload(SaveFileSet.From(c.Writes, c.Deletes));
            friend.Game = GameSession.Start(definitions, GameStartSettings.FromDefinitions(definitions), store, RoomGuestLink.Slot, id, shares: friend.Link.Shares);
            return friend;
        }

        /// <summary>친구가 알바 한 번(돈만 주는 일): 예약하고 성공으로 정산한다(친구 쪽 GameSession 명령 — 준비 단계).</summary>
        private static void Shift(Friend friend, string request)
        {
            Assert.That(friend.Game.BeginPartTimeWork(MoneyOnlyWork, request, 1).Applied, Is.True, request);
            Assert.That(friend.Game.SettleMinigame(request, true).Applied, Is.True, request);
        }

        private static string Hud(PlayerDriver player) => ((UiLabel)player.Node(ClockHudScreen.LabelId)).Text;

        private RoomWallet RoomWalletOf(PlayerDriver player)
            => player.Game.Hosting.Room.Server.Pieces.TryGet(RoomWallet.PieceName, out IRoomPiece piece) ? (RoomWallet)piece : null;

        [Test]
        public void MP_SHARE_001_PickMoneyInTheHostModal_FriendsPayShowsOnMyHud_ThenBuyingUsesTheRoomWallet()
        {
            WriteStory("slot-1", "별");
            using PlayerDriver player = Launch();
            player.PressButton(SaveSlotScreen.HostId);
            player.WaitForScreen(ScreenCatalog.HostRoom);
            var modal = (HostRoomScreen)player.Screens.Find(ScreenCatalog.HostRoom);
            Assert.That(((UiToggle)player.Node(HostRoomScreen.ShareToggleId(RoomWallet.PieceName))).IsOn, Is.False, "처음에는 각자");
            player.PressButton(HostRoomScreen.ShareToggleId(RoomWallet.PieceName));
            Assert.That(modal.ChosenSharedPieces, Is.EqualTo(new[] { RoomWallet.PieceName }), "포인터로 돈 공유를 켰다");
            player.PressButton(HostRoomScreen.AddressId);
            player.TypeText(Loopback);
            player.MovePointer(-50, -50);
            player.Frames(2);
            TestContext.WriteLine("스크린샷: " + player.ScreenshotScreen(ScreenCatalog.HostRoom, "shares-01-host-modal-money-on"));
            player.PressButton(HostRoomScreen.OpenId);
            Assert.That(Until(player, () => player.Game.World != null && player.Game.Hosting.IsOpen), Is.True, "방을 열고 들어간다");

            Assert.That(new FileSaveStore(_saves).TryRead("slot-1", SaveFileNames.RoomSettings, out RoomSettingsSaveData saved), Is.True, "방 설정 파일");
            Assert.That(saved.SharedPieces, Is.EqualTo(new[] { RoomWallet.PieceName }));
            Assert.That(saved.MaxPlayers, Is.EqualTo(4), "정원도 방 설정에");
            Assert.That(player.Game.Session.SharesWallet, Is.True, "내 게임 한 판도 방 지갑을 쓴다");
            Assert.That(Until(player, () => Hud(player).EndsWith(player.Game.Text.Format("ui.hud.room-balance", 0))), Is.True, Hud(player));

            Friend friend = Join(player, "friend");
            Shift(friend, "friend-shift-1");
            Assert.That(Until(player, () => RoomWalletOf(player).Balance == 5), Is.True, "친구가 번 돈이 방 지갑에");
            Assert.That(Until(player, () => Hud(player).EndsWith(player.Game.Text.Format("ui.hud.room-balance", 5))), Is.True,
                "친구가 알바로 번 돈이 내 HUD에 보인다: " + Hud(player));
            player.Screenshot("shares-02-hud-room-wallet");

            // 가게 지점까지 걸어가 E, 가구 한 줄을 포인터로 누른다. 방 지갑의 돈이 모자라지 않게 친구가 더 넣는다(친구 쪽 요청 — 준비 단계).
            // 손님의 요청은 저장과 함께 가므로 알바 한 번 더(저장)로 같이 보낸다.
            friend.Link.Shares.Outbox.Add(new RoomRequestSaveData { Piece = RoomWallet.PieceName, RequestId = "friend-gift", Kind = RoomWallet.Deposit, Amount = 495 });
            Shift(friend, "friend-shift-2");
            Assert.That(Until(player, () => RoomWalletOf(player).Balance == 505 && player.Game.Session.Wallet.Balance == 505), Is.True);
            Assert.That(player.WalkThroughDoor("town"), Is.EqualTo("Town"));
            player.WalkNear(StoreX, StoreY, 40f);
            Assert.That(player.Game.World.Interactions.Target?.Kind, Is.EqualTo(LocationScreen.InteractionKind));
            player.PressKey(TestKey.E);
            player.WaitForScreen(ScreenCatalog.Location);
            var store = (LocationScreen)player.Screens.Find(ScreenCatalog.Location);
            int row = store.Model.Rows.ToList().FindIndex(r => r.Kind != LocationRowKind.Activity && r.Enabled);
            Assert.That(row, Is.GreaterThanOrEqualTo(0), "방 지갑의 돈으로 살 수 있는 가구");
            LocationRow offer = store.Model.Rows[row];
            UiRect rect = store.RowRect(row);
            Assert.That(player.Game.Ui.HitTest(rect.CenterX, rect.CenterY)?.Id, Is.EqualTo(LocationScreen.RowsId));
            player.ClickAt(rect.CenterX, rect.CenterY);
            Assert.That(Until(player, () => !player.Game.Session.WaitingForRoom && store.Model.Rows[row].Owned), Is.True, "방이 허락하면 산다");
            int after = 505 - offer.Price;
            Assert.That(Until(player, () => RoomWalletOf(player).Balance == after && friend.Game.Wallet.Balance == after), Is.True,
                "산 값이 방 지갑에서 나가고 친구 쪽 잔액도 같다");
            string result = ((UiTextBlock)player.Node(LocationScreen.ResultId)).Text;
            Assert.That(result, Is.EqualTo(string.Format(player.Game.Text.Get("dream.shop.bought"), player.Game.Text.Get(offer.NameKey))), result);
            Assert.That(player.Game.Session.Inventory.CountOf(player.Game.Session.Inventory.Currency), Is.EqualTo(after), "내 화면의 용돈 = 방 지갑");
            player.MovePointer(-50, -50);
            player.Frames(2);
            TestContext.WriteLine("스크린샷: " + player.ScreenshotScreen(ScreenCatalog.Location, "shares-03-bought-with-room-wallet"));
            player.PressButton(LocationScreen.CloseId);
            player.WaitForScreenClosed(ScreenCatalog.Location);

            player.OpenScreenWithKey(TestKey.Escape, ScreenCatalog.Settings);
            Assert.That(((UiLabel)player.Node(SettingsScreen.RoomSharesId)).Text,
                Is.EqualTo(RoomShareText.Describe(player.Game.Text, new[] { RoomWallet.PieceName })), "설정 화면의 방 줄에 같이 쓰는 것");
            TestContext.WriteLine("스크린샷: " + player.ScreenshotScreen(ScreenCatalog.Settings, "shares-04-settings-room-shares"));
        }

        /// <summary>
        /// 집과 가구 공유: --share house로 연 방에서, 들어온 친구가 같이 쓰는 집에 가구를 놓으면(친구 쪽 GameSession 명령 — 준비) 방이 허락하고
        /// 방을 연 내 화면(집 맵)에 그 가구가 그려진다. 막는 가구면 내 캐릭터의 충돌 칸에도 더해진다. 방을 연 창: 실제 창과 게임 루프(장치 입력은 없음)와
        /// 직접 본 스크린샷. 친구: 자동 테스트(명령).
        /// </summary>
        [Test]
        public void MP_HOUSE_001_AFriendPlacesFurnitureInTheSharedHouse_ItShowsOnMyScreen()
        {
            using PlayerDriver player = Launch("--host", Loopback, "--slot", "slot-0", "--share", "house");
            Assert.That(Until(player, () => player.Game.World != null && player.Game.Hosting.IsOpen), Is.True);
            Assert.That(player.Game.Session.SharesHouse, Is.True, "내 게임 한 판도 같이 쓰는 집");
            Assert.That(player.World.Map.Id, Is.EqualTo("House"), "새 이야기는 집에서 시작한다(이 시험의 전제)");
            Assert.That(player.Game.Session.House, Is.Not.Null);
            int before = player.Game.World.PlacedFurnitureSpriteCount;
            player.Frames(3);
            TestContext.WriteLine("스크린샷: " + player.Screenshot("house-00-before-friend-places"));

            Friend friend = Join(player, "friend");
            var grid = player.Game.World.Rooms.GridForSurface("house");
            friend.Game.EnterHouse(grid, "house"); // 친구의 Host가 집 맵에 들어가면 하는 일
            var furniture = friend.Game.AvailableFurniture().First(f => f.BlocksMovement && f.FootprintCells.Count == 1);
            // 내 캐릭터에서 가까운 빈 칸(화면 안에 보이게, 바로 옆 칸은 피한다).
            var room = player.Game.World.Room;
            GridPoint me = room.ToPlacement(player.PlayerCell);
            GridPoint cell = default;
            int best = int.MaxValue;
            foreach (GridPoint c in room.FloorCells)
            {
                int d = Math.Abs(c.X - me.X) + Math.Abs(c.Y - me.Y);
                if (d >= 3 && d < best && friend.Game.House.CanPlace(furniture, c, FurniturePlacementDirection.North)) { best = d; cell = c; }
            }
            Assert.That(best, Is.LessThan(int.MaxValue), "친구가 놓을 칸");
            friend.Game.PlaceFurniture(furniture.StableId, cell, FurniturePlacementDirection.North);
            Assert.That(friend.Game.WaitingForRoom, Is.True, "친구 쪽은 방의 허락을 기다린다");

            Assert.That(Until(player, () => player.Game.Session.House.Placed.Count == 1 && !friend.Game.WaitingForRoom), Is.True, "방이 허락하고 내 집에도 놓인다");
            Assert.That(friend.Game.LastRoomCommandApplied, Is.True);
            Assert.That(player.Game.Session.House.Placed[0].Anchor, Is.EqualTo(cell));
            Assert.That(player.Game.World.PlacedFurnitureSpriteCount, Is.GreaterThan(before), "내 화면의 월드에 가구 그림이 더해졌다");
            CellPos mapCell = player.Game.World.Room.ToMap(cell);
            Assert.That(player.Game.World.Room.WalkMap.Collision.IsBlocked(mapCell.X, mapCell.Y), Is.True, "막는 가구는 내 캐릭터의 충돌에도");
            var placedBy = ((RoomHouse)player.Game.Hosting.Room.Server.Pieces.Pieces.First(p => p.Name == RoomHouse.PieceName)).Placed.Single().PlacedBy;
            Assert.That(placedBy, Is.EqualTo("friend"));
            player.Frames(3);
            TestContext.WriteLine("스크린샷: " + player.Screenshot("house-01-friend-placed-on-my-screen"));
        }

        /// <summary>
        /// 내 PC에서 연 방의 손님 진행은 내 슬롯 안 guests/(친구 id)/에 남는다. 처음 온 친구는 빈 세이브(새 캐릭터), 알바 한 번 뒤 방을 닫고(게임을 끔)
        /// 같은 슬롯으로 다시 열면 친구가 자기 진행으로 돌아온다. 친구 PC의 폴더에는 아무것도 남지 않는다.
        /// 방을 연 창: 인자(--host --slot)로 열고 끈다(장치 입력 없음). 친구: 자동 테스트(명령).
        /// </summary>
        [Test]
        public void MP_GUEST_003_AGuestsProgress_StaysInMySlot_AndContinuesWhenIOpenTheRoomAgain()
        {
            string guestFolder = Path.Combine(GuestSaveFolder.FolderFor(_saves, "slot-0"), "friend");
            using (PlayerDriver first = Launch("--host", Loopback, "--slot", "slot-0"))
            {
                Assert.That(Until(first, () => first.Game.World != null && first.Game.Hosting.IsOpen), Is.True);
                Friend friend = Join(first, "friend");
                Assert.That(friend.Link.Files.Writes, Is.Empty, "처음 온 친구: 새 캐릭터");
                Shift(friend, "friend-shift-1");
                Assert.That(Until(first, () => File.Exists(Path.Combine(guestFolder, SaveFileNames.AdultCareerSnapshot("friend")))), Is.True,
                    "친구 진행이 내 슬롯 안 guests/friend/에");
                friend.Client.Dispose();
                _friends.Remove(friend);
            }

            Assert.That(Directory.GetFiles(_friendSaves, "*", SearchOption.AllDirectories), Is.Empty, "친구 PC에는 진행이 남지 않는다");

            using PlayerDriver again = Launch("--host", Loopback, "--slot", "slot-0");
            Assert.That(Until(again, () => again.Game.World != null && again.Game.Hosting.IsOpen), Is.True, "같은 슬롯으로 다시 연다(내 진행도 읽힌다)");
            Friend back = Join(again, "friend");
            Assert.That(back.Link.Files.Writes, Is.Not.Empty, "방이 친구의 세이브를 내려 준다");
            Assert.That(back.Game.Wallet.Balance, Is.EqualTo(5), "친구가 자기 진행으로 돌아왔다");
        }

        [Test]
        public void MP_SHARE_002_MoneyNotShared_FriendsPayStaysTheirs()
        {
            using PlayerDriver player = Launch("--host", Loopback, "--slot", "slot-0", "--share", "none");
            Assert.That(Until(player, () => player.Game.World != null && player.Game.Hosting.IsOpen), Is.True);
            Assert.That(player.Game.Session.SharesWallet, Is.False);
            int mine = player.Game.Session.Wallet.Balance;
            Friend friend = Join(player, "friend");
            Shift(friend, "friend-shift-1");
            Assert.That(friend.Game.Wallet.Balance, Is.GreaterThanOrEqualTo(5), "친구의 돈은 친구 지갑에");
            Until(player, () => false, 1.0); // 1초 돌려도
            Assert.That(player.Game.Session.Wallet.Balance, Is.EqualTo(mine), "내 지갑은 그대로");
            Assert.That(Hud(player), Does.EndWith(player.Game.Text.Format("dream.shop.balance", mine)), "HUD는 내 용돈");
            Assert.That(player.Game.Hosting.Room.Server.Pieces.Pieces, Is.Empty, "방에 조각이 없다");
        }

        [Test]
        public void MP_SHARE_003_ReopeningTheSlot_ShowsTheSavedSettingsLockedInTheModal()
        {
            WriteStory("slot-1", "별");
            using (PlayerDriver first = Launch("--host", Loopback, "--slot", "slot-1", "--share", "wallet,town", "--max-players", "3"))
            {
                Assert.That(Until(first, () => first.Game.World != null && first.Game.Hosting.IsOpen), Is.True);
                Assert.That(first.Game.Hosting.SharedPieces, Is.EqualTo(new[] { "town", "wallet" }));
            }

            using PlayerDriver player = Launch();
            player.PressButton(SaveSlotScreen.HostId);
            player.WaitForScreen(ScreenCatalog.HostRoom);
            var modal = (HostRoomScreen)player.Screens.Find(ScreenCatalog.HostRoom);
            Assert.That(modal.ExistingRoom, Is.True);
            Assert.That(modal.ChosenSharedPieces, Is.EqualTo(new[] { "wallet", "town" }), "저장된 설정");
            Assert.That(player.Node(HostRoomScreen.ShareToggleId("wallet")).IsActive, Is.False, "바꿀 수 없다");
            Assert.That(modal.MaxPlayers, Is.EqualTo(3));
            Assert.That(modal.StatusKey, Is.EqualTo("ui.host.saved-room"));
            player.MovePointer(-50, -50);
            player.Frames(2);
            TestContext.WriteLine("스크린샷: " + player.ScreenshotScreen(ScreenCatalog.HostRoom, "shares-05-host-modal-saved-room"));
        }
    }
}
