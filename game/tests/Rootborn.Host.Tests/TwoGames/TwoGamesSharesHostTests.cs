using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Placement;
using Rootborn.Core.Room;
using Rootborn.Screens;
using Rootborn.Screens.Day;
using Rootborn.Screens.Hud;
using Rootborn.Screens.Storage;
using Rootborn.Ui;

namespace Rootborn.Host.Tests.TwoGames
{
    /// <summary>같이 쓰는 돈·상자·집을 두 게임의 입력으로. 근거와 준비는 <see cref="TwoGamesHostTests"/> 머리말.</summary>
    public sealed partial class TwoGamesHostTests
    {
        private const string ShopWork = "work.shift.corner-store"; // 가게 알바: 순서 외우기, 보상 나무 3 + 용돈 5(data/studentlife/PartTimeWorkDefinition.json)
        private const string Shop = "location.corner-store";

        private string HostHud => ((UiLabel)_host.Node(ClockHudScreen.LabelId)).Text;
        private string RoomBalance(int amount) => _host.Game.Text.Format("ui.hud.room-balance", amount);

        /// <summary>
        /// 손님이 마을 가게 알바(미니게임을 포인터로)로 번 돈이 방장 HUD의 방 지갑 잔액에 보인다. 방장도 알바를 한 뒤 가게에서 가구를 사면(포인터로 줄)
        /// 손님 HUD의 잔액도 준다. 준비: 이야기가 있는 슬롯 파일(양쪽).
        /// </summary>
        [Test]
        public void TWO_GAMES_002_GuestsShiftPayShowsOnHostHud_HostBuysAtTheShop_GuestBalanceDrops()
        {
            WriteStory(_hostSaves, "slot-1", "방장", 101);
            WriteStory(_guestSaves, "slot-1", "손님", 202);
            _host = LaunchHost();
            HostOpensFromStartMenu(null, RoomWallet.PieceName, RoomHouse.PieceName);
            _guest = StartGuest();
            GuestJoinsByAddress(_host.Game.Hosting.ListenAddress, null);
            Assert.That(_guest.Until(() => HostHud.EndsWith(RoomBalance(0))), Is.True, "방 지갑 0: " + HostHud);

            _guest.GoTo("Town");
            Assert.That(_guest.Call("work-shift", ShopWork), Is.EqualTo("worked"), "손님이 알바 미니게임을 끝까지 했다");
            Assert.That(_guest.Until(() => HostHud.EndsWith(RoomBalance(5)) && _guest.Query("wallet") == "5"), Is.True,
                "손님이 번 돈이 방장 HUD에: " + HostHud);
            Assert.That(_guest.Query("text", ClockHudScreen.LabelId), Does.EndWith(RoomBalance(5)), "손님 HUD도 방 지갑");
            TestContext.WriteLine("스크린샷: " + _host.Screenshot("two-02-host-hud-guest-pay"));
            TestContext.WriteLine("스크린샷(손님): " + _guest.Screenshot("two-02-guest-after-shift"));

            _host.GoTo("Town");
            Assert.That(_host.WorkShift(ShopWork), Is.True, "방장도 알바");
            Assert.That(_guest.Until(() => _host.Game.Session.Wallet.Balance == 10 && _guest.Query("wallet") == "10"), Is.True, "방 지갑 10");

            string bought = _host.BuyFirstOffer(Shop);
            var store = (LocationScreen)_host.Screens.Find(ScreenCatalog.Location);
            LocationRow row = store.Model.Rows.First(r => r.Id == bought);
            int left = 10 - row.Price;
            Assert.That(_guest.Until(() => !_host.Game.Session.WaitingForRoom && store.Model.Rows.First(r => r.Id == bought).Owned), Is.True, "방이 허락하고 산다");
            Assert.That(_guest.Until(() => _host.Game.Session.Wallet.Balance == left && _guest.Query("wallet") == left.ToString()), Is.True,
                "방장이 쓴 돈이 손님 잔액에서도 빠진다: 방장 " + _host.Game.Session.Wallet.Balance + ", 손님 " + _guest.Query("wallet"));
            Assert.That(_guest.Until(() => _guest.Query("text", ClockHudScreen.LabelId).EndsWith(RoomBalance(left))), Is.True, "손님 HUD 잔액이 준다");
            _host.MovePointer(-50, -50);
            _host.Frames(2);
            TestContext.WriteLine("스크린샷: " + _host.ScreenshotScreen(ScreenCatalog.Location, "two-02-host-bought"));
            TestContext.WriteLine("스크린샷(손님): " + _guest.Screenshot("two-02-guest-hud-after-host-bought"));
        }

        private string CrateId => _host.Game.Session.Definitions.All<FurnitureStorageDefinition>().First().Furniture.StableId;

        /// <summary>
        /// 방장이 알바로 받은 나무를 집에 놓은 상자에 넣고(가구 배치 → 상자까지 걸어가 E → 가방 칸 누름), 손님이 걸어가 그 상자를 열어 꺼낸다(상자 칸 누름).
        /// 그 다음 둘이 같은 칸을 같은 프레임에 누르면 한 사람만 받는다. 준비: 이야기가 있는 슬롯 파일(양쪽). 나무는 둘 다 알바(입력)로 받는다.
        /// </summary>
        [Test]
        public void TWO_GAMES_003_HostPutsWoodInAChest_GuestWalksUpAndTakesIt_SameFrameTakeGoesToOnlyOne()
        {
            WriteStory(_hostSaves, "slot-1", "방장", 101);
            WriteStory(_guestSaves, "slot-1", "손님", 202);
            _host = LaunchHost();
            HostOpensFromStartMenu(null, RoomHouse.PieceName);
            _guest = StartGuest();
            GuestJoinsByAddress(_host.Game.Hosting.ListenAddress, null);
            var session = _host.Game.Session;

            _host.GoTo("Town");
            Assert.That(_host.WorkShift(ShopWork), Is.True);
            Assert.That(session.Inventory.CountOf(session.Item("Wood")), Is.EqualTo(3), "방장이 알바로 나무 3");
            _guest.GoTo("Town");
            Assert.That(_guest.Call("work-shift", ShopWork), Is.EqualTo("worked"));
            Assert.That(_guest.QueryInt("item", "Wood"), Is.EqualTo(3), "손님도 나무 3");

            _host.GoTo("House");
            _host.PlaceFurniture(CrateId);
            Assert.That(_guest.Until(() => !session.WaitingForRoom && session.House.Placed.Any(f => f.Definition.StableId == CrateId)), Is.True, "방이 허락하고 상자가 놓인다");
            string chest = session.House.Placed.Single(f => f.Definition.StableId == CrateId).InstanceId;
            _guest.GoTo("House");
            Assert.That(_guest.Until(() => _guest.Query("house-placed-ids").Split(',').Contains(chest)), Is.True, "손님의 집에도 그 상자");

            _host.OpenChest(chest);
            _host.MoveStack("Wood", put: true);
            Assert.That(_guest.Until(() => !session.WaitingForRoom && _guest.QueryInt("chest", chest, "Wood") == 3), Is.True, "방장이 넣은 나무가 같이 쓰는 상자에");
            Assert.That(session.Inventory.CountOf(session.Item("Wood")), Is.EqualTo(0));
            _host.CloseChest();

            _guest.Call("open-chest", chest);
            _guest.Call("move-stack", "Wood", "take");
            Assert.That(_guest.Until(() => _guest.QueryInt("item", "Wood") == 6 && _guest.QueryInt("chest", chest, "Wood") == 0
                && session.StorageAt(chest).Stacks.All(s => s.Count == 0 || s.Item == null)), Is.True, "손님이 꺼낸 나무는 손님 가방에, 상자에는 없다");
            TestContext.WriteLine("스크린샷(손님): " + _guest.ScreenshotScreen(ScreenCatalog.Storage, "two-03-guest-took-wood"));

            // 같은 칸을 같은 프레임에: 손님이 6개를 다시 넣고, 둘 다 상자 화면을 연 채 그 칸에 포인터를 올린 뒤 같은 프레임에 누르고 뗀다.
            _guest.Call("move-stack", "Wood", "put");
            Assert.That(_guest.Until(() => _guest.QueryInt("chest", chest, "Wood") == 6 && ChestWood(chest) == 6), Is.True, "다시 넣었다");
            _host.OpenChest(chest);
            UiRect cell = _host.StorageCell("Wood", put: false);
            _host.MovePointer(cell.CenterX, cell.CenterY);
            _guest.Call("aim-stack", "Wood", "take");
            _host.SetPointerDown(true);
            _guest.Call("pointer-down"); // 손님의 누름 두 프레임이 방장 게임도 한 프레임씩 돌린다 — 둘 다 같은 프레임에 누른 것을 읽는다
            _host.SetPointerDown(false);
            _guest.Call("pointer-up");
            Assert.That(_guest.Until(() => !session.WaitingForRoom && _guest.Query("waiting-for-room") == "False"
                && session.Inventory.CountOf(session.Item("Wood")) + _guest.QueryInt("item", "Wood") == 6), Is.True, "둘의 요청이 끝났다");
            int hostWood = session.Inventory.CountOf(session.Item("Wood")), guestWood = _guest.QueryInt("item", "Wood");
            TestContext.WriteLine("[같은 프레임] 방장 " + hostWood + ", 손님 " + guestWood + " — 방장 안내 '" + ((UiTextBlock)_host.Node(StorageScreen.NoticeId)).Text
                + "', 손님 안내 '" + _guest.Query("storage-notice") + "'");
            Assert.That(new[] { hostWood, guestWood }, Is.EquivalentTo(new[] { 0, 6 }), "한 사람만 받는다(나누거나 둘 다 받지 않는다)");
            Assert.That(ChestWood(chest), Is.EqualTo(0));
            Assert.That(_guest.QueryInt("chest", chest, "Wood"), Is.EqualTo(0));
            _host.MovePointer(-50, -50);
            _guest.Call("move-pointer", "-50", "-50");
            TestContext.WriteLine("스크린샷: " + _host.ScreenshotScreen(ScreenCatalog.Storage, "two-03-host-same-frame"));
            TestContext.WriteLine("스크린샷(손님): " + _guest.ScreenshotScreen(ScreenCatalog.Storage, "two-03-guest-same-frame"));
        }

        private int ChestWood(string chest)
        {
            var contents = _host.Game.Session.StorageAt(chest);
            return contents == null ? -1 : contents.Stacks.Where(s => s.Item != null && s.Item.Id == "Wood").Sum(s => s.Count);
        }

        /// <summary>손님이 같이 쓰는 집에 가구를 놓으면(가구 배치 HUD → 검색 → 칸 누름) 방장 화면의 월드에 그 가구가 그려진다. 준비: 이야기가 있는 슬롯 파일.</summary>
        [Test]
        public void TWO_GAMES_004_GuestPlacesFurniture_ItShowsOnTheHostScreen()
        {
            WriteStory(_hostSaves, "slot-1", "방장", 101);
            WriteStory(_guestSaves, "slot-1", "손님", 202);
            _host = LaunchHost();
            HostOpensFromStartMenu(null, RoomHouse.PieceName);
            _guest = StartGuest();
            GuestJoinsByAddress(_host.Game.Hosting.ListenAddress, null);
            Assert.That(_host.World.Map.Id, Is.EqualTo("House"));
            Assert.That(_guest.Query("map"), Is.EqualTo("House"));
            int before = _host.Game.Session.House.Placed.Count, drawn = _host.Game.World.FurnitureDrawCount;
            Assert.That(_guest.QueryBool("shares-house"), Is.True, "손님 게임도 같이 쓰는 집");
            TestContext.WriteLine("스크린샷: " + _host.Screenshot("two-04-host-before"));

            string furniture = _guest.Query("first-free-furniture");
            string cellText = _guest.Call("place-furniture", furniture);
            var placedCell = new Rootborn.Core.Minigames.GridPoint(int.Parse(cellText.Split(',')[0]), int.Parse(cellText.Split(',')[1]));
            TestContext.WriteLine("[가구] 손님이 " + furniture + "를 칸 " + cellText + "에 놓음");
            Assert.That(_guest.UntilQuery("False", 600, "waiting-for-room"), Is.True, "손님 쪽은 방의 허락을 받았다");
            bool shown = _guest.Until(() => _host.Game.Session.House.Placed.Count == before + 1 && _host.Game.World.FurnitureDrawCount > drawn);
            Assert.That(shown, Is.True, "방장 집에도 놓이고 그려진다: 방장 집 " + _host.Game.Session.House.Placed.Count + "개(전 " + before + "), 그린 가구 "
                + _host.Game.World.FurnitureDrawCount + "(전 " + drawn + "), 손님 집 " + _guest.Query("house-placed") + "개, 방의 집 "
                + ((RoomHouse)_host.Game.Hosting.Room.Server.Pieces.Pieces.First(p => p.Name == RoomHouse.PieceName)).Placed.Count + "개");
            Assert.That(_host.Game.Session.House.Placed.Last().Definition.StableId, Is.EqualTo(furniture));
            var piece = (RoomHouse)_host.Game.Hosting.Room.Server.Pieces.Pieces.First(p => p.Name == RoomHouse.PieceName);
            Assert.That(piece.Placed.Last().PlacedBy, Is.EqualTo(GuestId), "손님이 놓은 것");
            // 손님은 배치 화면(방 전체가 보이는 카메라)에서 칸을 골랐다. 방장은 그 가구 옆까지 걸어가 일반 카메라로 찍는다.
            var mapCell = _host.Game.World.Room.ToMap(placedCell);
            int size = _host.World.Map.Grid.CellSize;
            var center = Rootborn.Core.World.CellPos.Center(mapCell, size);
            _host.WalkNear(center.X, center.Y, size * 1.6f);
            _host.Frames(2);
            UiRect onHost = new Rootborn.Host.World.HostPlacementView(_host.Game.World, () => _host.Game.Fit.Scale).CellRect(placedCell);
            Assert.That(onHost.Y >= 0 && onHost.Bottom <= _host.Game.Fit.LogicalHeight && onHost.X >= 0 && onHost.Right <= _host.Game.Fit.LogicalWidth, Is.True,
                "걸어간 뒤 그 칸이 방장 화면 안: " + onHost.X + "," + onHost.Y);
            TestContext.WriteLine("스크린샷: " + _host.Screenshot("two-04-host-sees-guest-furniture") + " (가구 칸 " + onHost.X + "," + onHost.Y + ")");
            TestContext.WriteLine("스크린샷(손님): " + _guest.Screenshot("two-04-guest-placed"));
        }
    }
}
