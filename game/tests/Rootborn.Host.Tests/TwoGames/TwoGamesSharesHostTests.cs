using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Building;
using Rootborn.Core.Placement;
using Rootborn.Core.Room;
using Rootborn.Core.World;
using Rootborn.Screens;
using Rootborn.Screens.Day;
using Rootborn.Screens.Furniture;
using Rootborn.Screens.Hud;
using Rootborn.Screens.Settings;
using Rootborn.Screens.Start;
using Rootborn.Screens.Storage;
using Rootborn.Ui;
using Rootborn.World;

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

        // ---------- 같이 쓰는 집의 꾸밈(집 꾸미기 1단계 (다), HOUSE-004) ----------

        private const string Carpet = "finish.floor.carpet", Checker = "finish.floor.checker-soft", Sprig = "finish.wall.wallpaper-sprig";

        /// <summary>
        /// 집의 왼쪽 위 방(행 순서로 첫 바닥 칸의 방, 문간 포함 — Core 방 채우기)과 가리킬 칸 순서(가운데에 가까운 문간 아닌 칸부터). 칸 좌표를 적지 않는다.
        /// <see cref="HouseFinishHostTests"/>와 같은 방이다.
        /// </summary>
        private static (IReadOnlyList<CellPos> Room, List<CellPos> Aim, CellPos Middle) TopLeftRoom(LoadedMap map)
        {
            RoomGrid grid = map.Tiles.Grid;
            RoomFill fill = RoomFill.Build(grid);
            for (int y = 0; y < grid.Rows; y++)
                for (int x = 0; x < grid.Columns; x++)
                    if (grid.At(x, y) == RoomCells.Floor)
                    {
                        IReadOnlyList<CellPos> room = fill.RoomOf(new CellPos(x, y));
                        double cx = room.Average(c => c.X), cy = room.Average(c => c.Y);
                        List<CellPos> aim = room.Where(c => !fill.IsDoorway(c)).OrderBy(c => (c.X - cx) * (c.X - cx) + (c.Y - cy) * (c.Y - cy)).ToList();
                        return (room, aim, aim[0]);
                    }
            throw new InvalidOperationException("바닥이 없다");
        }

        private RoomFinishDefinition FinishDef(string id) => _host.Game.Session.Definitions.Get<RoomFinishDefinition>(id);

        /// <summary>그 꾸밈으로 칸 n개를 바꾸는 값(데이터의 칸당 값 × n, Core HouseFinishes.CostFor). 무료면 0.</summary>
        private int CostOf(string id, int cells)
        {
            var cost = HouseFinishes.CostFor(FinishDef(id), cells);
            return cost.Count == 0 ? 0 : cost[0].Count;
        }

        private static string Cell(CellPos c, int i) => (i == 0 ? c.X : c.Y).ToString(CultureInfo.InvariantCulture);
        private string GuestFloor(CellPos c) => _guest.Query("house-floor-at", Cell(c, 0), Cell(c, 1));
        private string GuestWall(CellPos c) => _guest.Query("house-wall-at", Cell(c, 0), Cell(c, 1));
        private string Text(string key) => _host.Game.Text.Get(key);
        private string HostReason => ((UiTextBlock)_host.Node(FurniturePlacementScreen.FinishReasonId)).Text;

        /// <summary>방(방장 프로세스 안)의 요청 장부에 지금 있는 답 id. 뒤에 <see cref="FinishAnswersSince"/>로 그 뒤에 생긴 답만 본다.</summary>
        private HashSet<string> LedgerIds()
            => new HashSet<string>(_host.Game.Hosting.Room.Server.Pieces.Ledger.ToSaveData().Records.Select(r => r.RequestId), StringComparer.Ordinal);

        /// <summary>
        /// <paramref name="before"/> 뒤에 방이 답한 꾸밈 답을 답한 순서대로: (누구 "방장"/"손님", 결과). 한 사람의 꾸밈 한 번은 잡기 답 + 쓰기 답 둘이다
        /// (쓰기는 돈을 낸 뒤 방이 꾸밈을 칠한 답). 장부는 답의 종류를 남기지 않으므로 사람마다 첫 답이 잡기, 둘째가 쓰기다.
        /// </summary>
        private List<(string Who, string Outcome)> FinishAnswersSince(HashSet<string> before)
        {
            string hostPlayer = _host.Game.Room.PlayerId, guestPlayer = GuestId;
            return _host.Game.Hosting.Room.Server.Pieces.Ledger.ToSaveData().Records
                .Where(r => !before.Contains(r.RequestId) && r.Piece == RoomHouseBuild.PieceName)
                .Select(r => (r.PlayerId == hostPlayer ? "방장" : r.PlayerId == guestPlayer ? "손님" : r.PlayerId, r.Outcome)).ToList();
        }

        /// <summary>
        /// 같은 방을 둘이 차례로 칠한 끝(TWO_GAMES_011의 "차례로 적용" 갈래, TWO_GAMES_012): 거절 없음, 방 전부·두 화면이 나중 쪽 꾸밈 하나, 각자 자기 비용을 정확히 한 번씩,
        /// 이유 줄에 거절 문구 없음, 그리고 끝 상태만으로는 "먼저 낸 쪽이 돈만 내고 칠해지지 않음"과 같아 보이므로 방이 두 번 칠했음을 장부의 답 순서로 본다 —
        /// 먼저 쪽의 잡기·쓰기 적용이 둘 다 끝난 뒤에 나중 쪽의 잡기·쓰기 적용이 있고, 끝 꾸밈은 나중 쪽의 것이다.
        /// </summary>
        private void AssertAppliedInTurn(List<(string Who, string Outcome)> answers, IReadOnlyList<CellPos> room, string won, string hostFinish, string guestFinish,
            int hostPaid, int hostCost, int guestPaid, int guestCost, string hostRefusal, string guestRefusal, string hostReason, string guestReason)
        {
            Assert.That((hostRefusal, guestRefusal), Is.EqualTo((string.Empty, string.Empty)), "둘 다 거절 없음");
            Assert.That(won, Is.EqualTo(hostFinish).Or.EqualTo(guestFinish), "그 방은 나중에 적용된 쪽의 꾸밈 하나");
            Assert.That(room.All(c => _host.Game.Session.HouseFloorAt(c) == won && GuestFloor(c) == won), Is.True, "그 방 전부가 한 꾸밈이고 두 화면이 같다");
            Assert.That(hostPaid, Is.EqualTo(hostCost), "방장 돈: 자기 적용 한 번만큼 정확히");
            Assert.That(guestPaid, Is.EqualTo(guestCost), "손님 돈: 자기 적용 한 번만큼 정확히");
            Assert.That(hostReason, Is.Not.EqualTo(Text(HouseFinishReasons.RoomRefused)), "방장 이유 줄에 거절 문구 없음");
            Assert.That(guestReason, Is.Not.EqualTo(Text(HouseFinishReasons.RoomRefused)), "손님 이유 줄에 거절 문구 없음");
            string later = won == hostFinish ? "방장" : "손님", earlier = won == hostFinish ? "손님" : "방장";
            Assert.That(answers, Is.EqualTo(new[] { (earlier, RoomOutcomes.Applied), (earlier, RoomOutcomes.Applied), (later, RoomOutcomes.Applied), (later, RoomOutcomes.Applied) }),
                "방이 먼저 쪽의 꾸밈을 칠한(쓰기 적용) 뒤에 나중 쪽을 잡고 칠했다 — 먼저 낸 쪽도 칠해진 뒤 덮였다");
        }

        /// <summary>손님: P(가구 배치 카메라의 집 전체 보기 — 들어간 직후 알림도 닫힌다)로 찍고 닫는다. 스크린샷 경로.</summary>
        private string GuestWholeHouse(string name)
        {
            _guest.Call("key", "P");
            _guest.WaitForScreen(ScreenCatalog.FurniturePlacement);
            _guest.Call("move-pointer", "-50", "-50");
            _guest.Frames(2);
            string path = _guest.Screenshot(name);
            _guest.Call("close-placement");
            return path;
        }

        /// <summary>방장·손님 둘 다 같이 쓰는 집(House)에 있는 방을 연다(집만 공유). 스크린샷은 output/house-building/stage1c/.</summary>
        private void OpenSharedHouseRoom()
        {
            _evidence = Path.Combine(_repo, "output", "house-building", "stage1c");
            WriteStory(_hostSaves, "slot-1", "방장", 101);
            WriteStory(_guestSaves, "slot-1", "손님", 202);
            _host = LaunchHost();
            HostOpensFromStartMenu(null, RoomHouse.PieceName);
            _guest = StartGuest();
            GuestJoinsByAddress(_host.Game.Hosting.ListenAddress, null);
            Assert.That(_host.World.Map.Id, Is.EqualTo("House"));
            Assert.That(_guest.Query("map"), Is.EqualTo("House"));
            Assert.That(_host.Game.Session.SharesHouseBuild, Is.True, "방장 게임: 꾸밈도 같이 쓰는 집");
            Assert.That(_guest.QueryBool("shares-house"), Is.True, "손님 게임도 같이 쓰는 집");
            Assert.That(_host.Game.Session.SharesWallet, Is.False, "돈은 각자");
        }

        /// <summary>
        /// HOUSE-004 두 게임 창: 손님이 P → 꾸미기 → 바닥 → 방 채우기 → 카펫 카드 → 왼쪽 위 방 칸을 누르면(손님 게임의 입력) 방의 허락을 기다린 뒤
        /// 손님 화면과 방장 화면 모두 그 방이 카펫으로 다시 조합되고, 손님 돈만 바뀐 칸 수 × 칸당 값만큼 한 번 준다. 그다음 방장이(내 PC에서 방을 연 사람 —
        /// 방이 아는 신원이 게임 한 판의 id와 다른 길) 띠 색(무료)과 벽지(유료)를 바꾸면 손님 화면에 보이고 방장 돈만 벽지 값만큼 한 번 준다.
        /// 준비(실제 플레이 아님): 이야기가 있는 슬롯 파일(양쪽), 돈 주기(가방 명령 — 손님은 give-item 동사, 방장은 Inventory.Add).
        /// 그림 색은 단언하지 않는다: 보이는 꾸밈 조회(HouseFloorAt·HouseWallAt·HouseBandColors)와 조합 횟수(WorldScreen.ComposeCount)를 본다.
        /// 스크린샷: output/house-building/stage1c/two-games-0N-(host|guest)-*.png(직접 연다).
        /// </summary>
        [Test]
        public void TWO_GAMES_010_GuestDecoratesTheSharedHouse_HostSeesIt_GuestPaysOnce()
        {
            OpenSharedHouseRoom();
            var session = _host.Game.Session;
            var world = _host.Game.World;
            var (room, aim, middle) = TopLeftRoom(world.Current);
            string aimText = PeerVerbs.CellsText(aim);
            string money = FinishDef(Carpet).Cost[0].Item;
            TestContext.WriteLine("[방] 왼쪽 위 방 " + room.Count + "칸, 가운데 " + middle + ", 돈 아이템 " + money);
            Assert.That(session.HouseFloorAt(middle), Is.Not.EqualTo(Carpet), "전제: 카펫이 아니다");

            // 준비(실제 플레이 아님): 손님에게 카펫 값을 준다.
            _guest.Call("give-item", money, CostOf(Carpet, room.Count).ToString(CultureInfo.InvariantCulture));
            int guestBefore = _guest.QueryInt("wallet"), hostBefore = session.Wallet.Balance;
            int hostComposes = world.ComposeCount, guestComposes = _guest.QueryInt("compose-count");

            // 손님이 화면에서 꾸민다(손님 게임의 포인터·단추).
            string[] d = _guest.Call("decorate", RoomFinishSlots.Floor, Carpet, HouseFinishTools.Room, aimText).Split('|');
            int changed = int.Parse(d[0], CultureInfo.InvariantCulture);
            TestContext.WriteLine("[손님 꾸밈] 바뀔 칸 " + changed + ", 누름 " + d[1] + ", 누른 직후 이유 줄 '" + d[2] + "'");
            Assert.That(changed, Is.EqualTo(room.Count), "방 채우기: 방 + 맞닿은 문간");
            Assert.That(d[1], Is.EqualTo("waiting"), "같이 쓰는 집: 방의 허락을 기다린다");
            Assert.That(d[2], Is.EqualTo(Text(RoomMoneyText.WaitingKey)), "기다리는 동안 이유 줄에 기다림 문구");
            Assert.That(_guest.UntilQuery("False", 900, "waiting-for-room"), Is.True, "손님 쪽 기다림이 끝난다");
            Assert.That(_guest.Until(() => room.All(c => session.HouseFloorAt(c) == Carpet) && world.ComposeCount > hostComposes, 900), Is.True,
                "방장 화면도 그 방이 카펫으로 다시 조합된다: 방장 가운데 " + session.HouseFloorAt(middle) + ", 조합 " + world.ComposeCount + "(전 " + hostComposes + ")");
            Assert.That(_guest.Until(() => GuestFloor(middle) == Carpet && _guest.QueryInt("compose-count") > guestComposes, 300), Is.True, "손님 화면도 다시 조합");
            Assert.That(room.All(c => GuestFloor(c) == Carpet), Is.True, "손님의 보이는 꾸밈도 그 방 전부 카펫");
            Assert.That(_guest.Query("text", FurniturePlacementScreen.FinishReasonId), Is.Not.EqualTo(Text(RoomMoneyText.WaitingKey)), "적용되면 기다림 문구가 사라진다");
            Assert.That(_guest.QueryInt("wallet"), Is.EqualTo(guestBefore - CostOf(Carpet, changed)), "손님 돈이 바뀐 칸 수 × 칸당 값만큼");
            Assert.That(session.Wallet.Balance, Is.EqualTo(hostBefore), "방장 돈은 그대로");
            int hostComposed = world.ComposeCount;
            _guest.Until(() => false, 30);
            Assert.That(_guest.QueryInt("wallet"), Is.EqualTo(guestBefore - CostOf(Carpet, changed)), "한 번만(더 빠지지 않는다)");
            Assert.That(world.ComposeCount, Is.EqualTo(hostComposed), "방장은 가만히 있으면 다시 조합하지 않는다");
            Assert.That(_host.Game.Renderer.MissingImages, Is.Empty);

            _guest.Call("move-pointer", "-50", "-50");
            TestContext.WriteLine("스크린샷(손님): " + _guest.ScreenshotScreen(ScreenCatalog.FurniturePlacement, "two-games-01-guest-decorated-floor"));
            int size = _host.World.Map.Grid.CellSize;
            var center = CellPos.Center(middle, size);
            _host.WalkNear(center.X, center.Y, size * 1.6f);
            _host.Frames(2);
            TestContext.WriteLine("스크린샷: " + _host.Screenshot("two-games-01-host-sees-guest-floor"));
            _host.OpenScreenWithKey(TestKey.P, ScreenCatalog.FurniturePlacement); // 가구 배치 카메라의 집 전체 보기
            _host.MovePointer(-50, -50);
            _host.Frames(2);
            TestContext.WriteLine("스크린샷: " + _host.Screenshot("two-games-01-host-whole-house"));

            // 방장이 화면에서 띠 색(무료)과 벽지(유료)를 바꾼다. 준비(실제 플레이 아님): 방장에게 벽지 값을 준다.
            session.Inventory.Add(session.Item(money), CostOf(Sprig, room.Count));
            hostBefore = session.Wallet.Balance;
            guestBefore = _guest.QueryInt("wallet");
            RoomFinishDefinition band = session.HouseFinishOptions(RoomFinishSlots.Band).First(b => b.Art != session.HouseBandColors());
            string bandAction = _host.DecorateBand(band.Id);
            TestContext.WriteLine("[방장 띠 색] " + band.Id + " 누름 " + bandAction);
            Assert.That(bandAction, Is.EqualTo("waiting"), "같이 쓰는 집: 방의 허락을 기다린다");
            Assert.That(Until(_host, () => !session.WaitingForRoom && session.HouseBandColors() == band.Art, 900), Is.True, "방장 화면의 띠 색: " + session.HouseBandColors());
            Assert.That(_guest.Until(() => _guest.Query("house-band") == band.Art, 300), Is.True, "손님 화면에도 그 띠 색: " + _guest.Query("house-band"));
            Assert.That(session.Wallet.Balance, Is.EqualTo(hostBefore), "띠 색은 무료");

            int hostComposesBeforeWall = world.ComposeCount, guestComposesBeforeWall = _guest.QueryInt("compose-count");
            var wall = _host.Decorate(RoomFinishSlots.Wall, Sprig, HouseFinishTools.Room, aim);
            TestContext.WriteLine("[방장 벽지] 바뀔 칸 " + wall.Cells + ", 누름 " + wall.Action + ", 이유 줄 '" + wall.Reason + "'");
            Assert.That(wall.Action, Is.EqualTo("waiting"));
            Assert.That(wall.Reason, Is.EqualTo(Text(RoomMoneyText.WaitingKey)));
            // 벽지는 그 방의 벽 정면을 비추는 바닥 칸(미리 보기의 바뀔 칸)에 적힌다 — 방 가운데 칸이 아니다.
            Assert.That(wall.Changed.Count, Is.EqualTo(wall.Cells).And.GreaterThan(0));
            Assert.That(wall.Changed.All(room.Contains), Is.True, "바뀔 칸은 그 방 안");
            CellPos wallCell = wall.Changed[0];
            Assert.That(Until(_host, () => !session.WaitingForRoom && wall.Changed.All(c => session.HouseWallAt(c) == Sprig), 900), Is.True,
                "방장 화면의 벽지: " + session.HouseWallAt(wallCell));
            Assert.That(_guest.Until(() => GuestWall(wallCell) == Sprig, 300), Is.True, "손님 화면에도 그 벽지: " + GuestWall(wallCell));
            Assert.That(wall.Changed.All(c => GuestWall(c) == Sprig), Is.True, "손님의 보이는 꾸밈도 바뀔 칸 전부 그 벽지");
            Assert.That(_guest.Until(() => world.ComposeCount > hostComposesBeforeWall && _guest.QueryInt("compose-count") > guestComposesBeforeWall, 300), Is.True,
                "두 화면 모두 다시 조합");
            Assert.That(HostReason, Is.Not.EqualTo(Text(RoomMoneyText.WaitingKey)), "적용되면 기다림 문구가 사라진다");
            Assert.That(session.Wallet.Balance, Is.EqualTo(hostBefore - CostOf(Sprig, wall.Cells)), "방장 돈이 벽지 값만큼");
            Assert.That(_guest.QueryInt("wallet"), Is.EqualTo(guestBefore), "손님 돈은 그대로");
            Until(_host, () => false, 30);
            Assert.That(session.Wallet.Balance, Is.EqualTo(hostBefore - CostOf(Sprig, wall.Cells)), "한 번만");
            Assert.That(GuestFloor(middle), Is.EqualTo(Carpet), "바닥은 그대로");

            _host.MovePointer(-50, -50);
            _host.Frames(2);
            TestContext.WriteLine("스크린샷: " + _host.ScreenshotScreen(ScreenCatalog.FurniturePlacement, "two-games-02-host-wallpaper-band"));
            _guest.Call("close-placement");
            _guest.Call("key", "P");
            _guest.WaitForScreen(ScreenCatalog.FurniturePlacement);
            _guest.Call("move-pointer", "-50", "-50");
            _guest.Frames(2);
            TestContext.WriteLine("스크린샷(손님): " + _guest.Screenshot("two-games-02-guest-sees-wallpaper-band"));
            Assert.That(_guest.Query("missing-images"), Is.Empty);
            Assert.That(_host.CollisionViolations, Is.EqualTo(0));
            Assert.That(_guest.QueryInt("collision-violations"), Is.EqualTo(0));
        }

        /// <summary>
        /// HOUSE-004 두 게임 창, 부딪힘과 이어짐: 두 사람이 같은 방(왼쪽 위)을 다른 바닥으로 같은 프레임에 누른다(둘 다 포인터를 올려 두고, 방장 누름은 바꿔만 두고
        /// 손님의 pointer-down이 두 게임을 같이 돌린다 — TWO_GAMES_003과 같은 넣기). 정당한 결과는 둘이고 거절 여부로 가른다: 한쪽만 적용되고 다른 쪽은 이유 줄에
        /// 방의 거절 문구(돈은 적용된 쪽만 한 번), 또는 둘이 차례로 적용돼 각자 자기 값을 한 번씩 내고 나중 것이 덮는다(거절 없음). 둘 다 거절이면 실패.
        /// 손님이 끄고 다시 들어와도, 방장이 방을 닫고 같은 슬롯으로 다시 열어도(방 기록 저장·복원) 그 꾸밈이 두 화면에 그대로다.
        /// 준비(실제 플레이 아님): 이야기가 있는 슬롯 파일(양쪽), 돈 주기(가방 명령). 스크린샷: output/house-building/stage1c/two-games-0N-*.png.
        /// </summary>
        [Test]
        public void TWO_GAMES_011_SameRoomSameFrame_OneAppliesOrBothInTurn_EachPaysOnce_RejoinAndReopenKeepTheFinish()
        {
            OpenSharedHouseRoom();
            var session = _host.Game.Session;
            var (room, aim, middle) = TopLeftRoom(_host.Game.World.Current);
            string money = FinishDef(Carpet).Cost[0].Item;
            Assert.That(session.HouseFloorAt(middle), Is.Not.EqualTo(Carpet).And.Not.EqualTo(Checker), "전제: 둘 다 바뀌는 칸이 있다");

            // 준비(실제 플레이 아님): 둘 다 자기 바닥 값을 받는다.
            _guest.Call("give-item", money, CostOf(Checker, room.Count).ToString(CultureInfo.InvariantCulture));
            session.Inventory.Add(session.Item(money), CostOf(Carpet, room.Count));
            int guestBefore = _guest.QueryInt("wallet"), hostBefore = session.Wallet.Balance;

            int guestCells = int.Parse(_guest.Call("aim-finish", RoomFinishSlots.Floor, Checker, HouseFinishTools.Room, PeerVerbs.CellsText(aim)), CultureInfo.InvariantCulture);
            int hostCells = _host.AimFinish(RoomFinishSlots.Floor, Carpet, HouseFinishTools.Room, aim);
            Assert.That((guestCells, hostCells), Is.EqualTo((room.Count, room.Count)), "둘 다 그 방 전부를 바꾸려 한다");
            // 방(이 프로세스의 방장 게임 안)의 요청 장부: 누르기 전에 있던 답을 기억해 두고, 뒤에 이 누름이 낳은 꾸밈 답(잡기·쓰기)만 본다.
            var ledgerBefore = LedgerIds();
            _host.SetPointerDown(true);
            _guest.Call("pointer-down"); // 손님의 누름 두 프레임이 방장 게임도 한 프레임씩 돌린다 — 둘 다 같은 프레임에 누른 것을 읽는다
            _host.SetPointerDown(false);
            _guest.Call("pointer-up");
            var screen = (FurniturePlacementScreen)_host.Screens.Find(ScreenCatalog.FurniturePlacement);
            TestContext.WriteLine("[같은 프레임] 누름: 방장 " + screen.FinishLastAction + ", 손님 " + _guest.Query("finish-last-action"));
            Assert.That((screen.FinishLastAction, _guest.Query("finish-last-action")), Is.EqualTo(("waiting", "waiting")), "둘 다 방에 요청했다");

            Assert.That(_guest.Until(() => !session.WaitingForRoom && _guest.Query("waiting-for-room") == "False"
                && (session.HouseFloorAt(middle) == Carpet || session.HouseFloorAt(middle) == Checker) && GuestFloor(middle) == session.HouseFloorAt(middle), 1200), Is.True,
                "둘의 요청이 끝나고 두 화면의 꾸밈이 같다: 방장 " + session.HouseFloorAt(middle) + ", 손님 " + GuestFloor(middle));
            _guest.Until(() => false, 30); // 늦게 오는 상태·돈이 없는지
            string won = session.HouseFloorAt(middle);
            int hostAfter = session.Wallet.Balance, guestAfter = _guest.QueryInt("wallet");
            string hostRefusal = session.LastHouseFinishRefusal ?? string.Empty, guestRefusal = _guest.Query("finish-refusal") ?? string.Empty;
            string hostReasonText = HostReason, guestReasonText = _guest.Query("text", FurniturePlacementScreen.FinishReasonId);
            Assert.That(hostRefusal, Is.Empty.Or.EqualTo(HouseFinishReasons.RoomRefused), "방장의 거절은 없거나 방의 거절");
            Assert.That(guestRefusal, Is.Empty.Or.EqualTo(HouseFinishReasons.RoomRefused), "손님의 거절은 없거나 방의 거절");
            bool hostRefused = hostRefusal.Length > 0, guestRefused = guestRefusal.Length > 0;
            // 갈래는 끝 바닥이 아니라 거절 여부로 가른다. 손님 요청이 방장 잡기의 짧은 창 안에 닿으면 한쪽만 적용·다른 쪽 거절, 창 뒤에 닿으면 둘이 차례로 적용된다
            // (각자 자기 것 한 번씩 내고 나중 것이 덮는다 — 2026-11-14 원격 Linux에서 본 갈래). 어느 쪽이 될지는 방의 벽시계 틱과 UDP 도착에 따른다.
            string branch = hostRefused && guestRefused ? "둘 다 거절" : hostRefused ? "손님만 적용(방장 거절)" : guestRefused ? "방장만 적용(손님 거절)" : "차례로 둘 다 적용";
            // 이 누름이 낳은 꾸밈 답을 방이 답한 순서대로: (누구, 결과). 한 사람의 꾸밈 한 번은 잡기 답 + 쓰기 답 둘이다(쓰기는 돈을 낸 뒤 방이 꾸밈을 칠한 답).
            var answers = FinishAnswersSince(ledgerBefore);
            TestContext.WriteLine("[같은 프레임] 갈래: " + branch + ", 끝 꾸밈 " + won + ", 돈 방장 " + hostBefore + "→" + hostAfter + ", 손님 " + guestBefore + "→" + guestAfter
                + ", 거절: 방장 '" + hostRefusal + "' 손님 '" + guestRefusal + "', 이유 줄: 방장 '" + hostReasonText + "' 손님 '" + guestReasonText + "'"
                + ", 방의 꾸밈 답(순서): " + string.Join(" ", answers.Select(a => a.Who + ":" + a.Outcome)));
            Assert.That(hostRefused && guestRefused, Is.False, "둘 다 거절되면 안 된다 — 먼저 닿은 요청은 적용된다");
            Assert.That(room.All(c => session.HouseFloorAt(c) == won && GuestFloor(c) == won), Is.True, "그 방 전부가 한 꾸밈(섞이지 않는다, 두 화면이 같다)");
            if (hostRefused || guestRefused)
            {
                bool hostWon = guestRefused;
                Assert.That(won, Is.EqualTo(hostWon ? Carpet : Checker), "그 방은 이긴(거절되지 않은) 쪽의 꾸밈");
                Assert.That(hostAfter, Is.EqualTo(hostWon ? hostBefore - CostOf(Carpet, room.Count) : hostBefore), "방장 돈: 적용됐을 때만 정확히 한 번");
                Assert.That(guestAfter, Is.EqualTo(hostWon ? guestBefore : guestBefore - CostOf(Checker, room.Count)), "손님 돈: 적용됐을 때만 정확히 한 번");
                Assert.That(hostWon ? guestReasonText : hostReasonText, Is.EqualTo(Text(HouseFinishReasons.RoomRefused)), "거절된 쪽 이유 줄에 방의 거절 문구");
                Assert.That(hostWon ? hostReasonText : guestReasonText, Is.Not.EqualTo(Text(HouseFinishReasons.RoomRefused)), "이긴 쪽 이유 줄에는 거절 문구가 없다");
                string winner = hostWon ? "방장" : "손님", loser = hostWon ? "손님" : "방장";
                Assert.That(answers.Where(a => a.Who == winner).Select(a => a.Outcome), Is.EqualTo(new[] { RoomOutcomes.Applied, RoomOutcomes.Applied }),
                    "이긴 쪽은 방이 잡기와 쓰기(칠함)를 한 번씩 적용했다");
                Assert.That(answers.Where(a => a.Who == loser).Select(a => a.Outcome), Is.EqualTo(new[] { RoomOutcomes.Refused }), "진 쪽은 잡기 거절 하나뿐(쓰기 없음)");
            }
            else
            {
                // 차례로 적용: 앞 것이 방 전부를 칠한 뒤 나중 것이 판정됐다. 나중 것의 비용은 그때 방에서 바뀌는 칸 수 × 칸당 값인데, 앞 꾸밈과 다른 꾸밈이라
                // 방 전부가 바뀌므로 요청이 보낸 수(room.Count)와 같다. 그래서 둘 다 자기 꾸밈 값을 정확히 한 번씩 냈다.
                AssertAppliedInTurn(answers, room, won, Carpet, Checker, hostBefore - hostAfter, CostOf(Carpet, room.Count), guestBefore - guestAfter, CostOf(Checker, room.Count),
                    hostRefusal, guestRefusal, hostReasonText, guestReasonText);
            }

            _host.MovePointer(-50, -50);
            _guest.Call("move-pointer", "-50", "-50");
            _host.Frames(2);
            TestContext.WriteLine("스크린샷: " + _host.ScreenshotScreen(ScreenCatalog.FurniturePlacement, "two-games-03-host-same-frame"));
            TestContext.WriteLine("스크린샷(손님): " + _guest.ScreenshotScreen(ScreenCatalog.FurniturePlacement, "two-games-03-guest-same-frame"));
            _host.ClosePlacement();
            _guest.Call("close-placement");

            // 손님이 끄고 다시 들어온다 → 꾸밈이 그대로 보인다.
            string guestId = GuestId;
            _guest.Call("close");
            Assert.That(Until(_host, () => _host.Game.Hosting.PlayerCount == 1), Is.True, "끄면 방에서 나간다");
            _guest.Launch(GuestArgs);
            GuestJoinsByAddress(_host.Game.Hosting.ListenAddress, null);
            Assert.That(GuestId, Is.EqualTo(guestId), "같은 인물");
            Assert.That(_guest.Query("map"), Is.EqualTo("House"));
            Assert.That(_guest.Until(() => GuestFloor(middle) == won && _guest.QueryInt("compose-count") >= 1, 900), Is.True, "다시 들어온 손님 화면에도 그 꾸밈: " + GuestFloor(middle));
            Assert.That(room.All(c => GuestFloor(c) == won), Is.True);
            Assert.That(_guest.QueryInt("wallet"), Is.EqualTo(guestAfter), "다시 들어와도 돈이 그대로(다시 빠지거나 돌아오지 않는다)");
            _guest.Frames(2);
            TestContext.WriteLine("스크린샷(손님): " + _guest.Screenshot("two-games-04-guest-rejoined"));
            TestContext.WriteLine("스크린샷(손님): " + GuestWholeHouse("two-games-04-guest-rejoined-whole-house"));

            // 방장이 방을 닫고(설정 → 방 닫기) 같은 슬롯으로 다시 연다 → 방 기록에서 꾸밈이 돌아온다. 손님도 다시 들어온다.
            _host.OpenScreenWithKey(TestKey.Escape, ScreenCatalog.Settings);
            _host.PressButton(SettingsScreen.RoomCloseId);
            Assert.That(_guest.Until(() => _host.Game.World == null && _host.Screens.IsOpen(ScreenCatalog.SaveSlotSelect)), Is.True, "방장은 시작 화면");
            Assert.That(_guest.UntilQuery("ui.join.reason.server-shutdown", 900, "room-notice-reason"), Is.True, "손님 화면에 방이 닫혔다는 알림");
            _guest.PressButton(RoomNoticeScreen.OkId);
            HostReopensFromStartMenu();
            Assert.That(_host.Game.Hosting.Room.Server.IsNewRoom, Is.False, "같은 슬롯으로 다시 열면 그 방을 잇는다");
            session = _host.Game.Session;
            Assert.That(Until(_host, () => session.HouseFloorAt(middle) == won && _host.Game.World.ComposeCount >= 1, 900), Is.True,
                "다시 연 방에서 방장 화면에 그 꾸밈: " + session.HouseFloorAt(middle));
            Assert.That(room.All(c => session.HouseFloorAt(c) == won), Is.True);
            Assert.That(session.Wallet.Balance, Is.EqualTo(hostAfter), "다시 열어도 방장 돈 그대로");
            GuestJoinsByAddress(_host.Game.Hosting.ListenAddress, null);
            Assert.That(_guest.Until(() => GuestFloor(middle) == won, 900), Is.True, "다시 연 방에 다시 들어온 손님 화면에도: " + GuestFloor(middle));
            Assert.That(_guest.QueryInt("wallet"), Is.EqualTo(guestAfter));
            _host.Frames(2);
            TestContext.WriteLine("스크린샷: " + _host.Screenshot("two-games-05-host-reopened"));
            TestContext.WriteLine("스크린샷(손님): " + _guest.Screenshot("two-games-05-guest-after-reopen"));
            TestContext.WriteLine("스크린샷(손님): " + GuestWholeHouse("two-games-05-guest-after-reopen-whole-house"));
            _host.OpenScreenWithKey(TestKey.P, ScreenCatalog.FurniturePlacement); // 가구 배치 카메라의 집 전체 보기
            _host.MovePointer(-50, -50);
            _host.Frames(2);
            TestContext.WriteLine("스크린샷: " + _host.Screenshot("two-games-05-host-reopened-whole-house"));
            _host.ClosePlacement();
            Assert.That(_host.Game.Renderer.MissingImages, Is.Empty);
            Assert.That(_guest.Query("missing-images"), Is.Empty);
        }

        /// <summary>
        /// HOUSE-004 두 게임 창, 차례로 칠하기(순서를 정한 TWO_GAMES_011의 "차례로 적용" 갈래): 방장이 화면에서 왼쪽 위 방 바닥을 카펫으로 바꾸고, 적용이 끝나
        /// 두 화면에 카펫이 보인 뒤 손님이 화면에서 같은 방을 체크 바닥으로 바꾼다. 거절 없이 둘 다 적용되고 방은 체크 하나(두 화면 같음), 방장은 카펫 값·손님은 체크 값을
        /// 정확히 한 번씩(손님 값은 그때 방에서 바뀌는 칸 수 × 칸당 값), 방의 장부에는 방장 적용 둘 뒤 손님 적용 둘. 단언은 011과 같은 도우미(<see cref="AssertAppliedInTurn"/>).
        /// 준비(실제 플레이 아님): 이야기가 있는 슬롯 파일(양쪽), 돈 주기(가방 명령). 꾸미는 조작은 두 게임의 화면 입력. 스크린샷: two-games-06-(host|guest)-overwritten.png.
        /// </summary>
        [Test]
        public void TWO_GAMES_012_SameRoomOneAfterAnother_SecondOverwrites_EachPaysOnce()
        {
            OpenSharedHouseRoom();
            var session = _host.Game.Session;
            var (room, aim, middle) = TopLeftRoom(_host.Game.World.Current);
            string money = FinishDef(Carpet).Cost[0].Item;
            Assert.That(session.HouseFloorAt(middle), Is.Not.EqualTo(Carpet).And.Not.EqualTo(Checker), "전제: 둘 다 바뀌는 칸이 있다");

            // 준비(실제 플레이 아님): 둘 다 자기 바닥 값을 받는다.
            session.Inventory.Add(session.Item(money), CostOf(Carpet, room.Count));
            _guest.Call("give-item", money, CostOf(Checker, room.Count).ToString(CultureInfo.InvariantCulture));
            int hostBefore = session.Wallet.Balance, guestBefore = _guest.QueryInt("wallet");
            var ledgerBefore = LedgerIds();

            // 1) 방장이 화면에서 카펫으로 칠하고, 두 화면에 카펫이 보일 때까지 기다린다.
            var first = _host.Decorate(RoomFinishSlots.Floor, Carpet, HouseFinishTools.Room, aim);
            TestContext.WriteLine("[방장 꾸밈] 바뀔 칸 " + first.Cells + ", 누름 " + first.Action);
            Assert.That((first.Cells, first.Action), Is.EqualTo((room.Count, "waiting")), "방장: 방 전부, 방의 허락을 기다린다");
            Assert.That(Until(_host, () => !session.WaitingForRoom && room.All(c => session.HouseFloorAt(c) == Carpet), 900), Is.True, "방장 화면에 카펫: " + session.HouseFloorAt(middle));
            Assert.That(_guest.Until(() => room.All(c => GuestFloor(c) == Carpet), 300), Is.True, "손님 화면에도 카펫: " + GuestFloor(middle));
            Assert.That(HostReason, Is.Not.EqualTo(Text(RoomMoneyText.WaitingKey)), "방장 기다림 문구가 사라졌다");
            Assert.That(session.Wallet.Balance, Is.EqualTo(hostBefore - CostOf(Carpet, room.Count)), "방장이 카펫 값을 냈다");

            // 2) 그다음 손님이 화면에서 같은 방을 체크로 칠한다.
            string[] d = _guest.Call("decorate", RoomFinishSlots.Floor, Checker, HouseFinishTools.Room, PeerVerbs.CellsText(aim)).Split('|');
            int guestChanged = int.Parse(d[0], CultureInfo.InvariantCulture);
            TestContext.WriteLine("[손님 꾸밈] 바뀔 칸 " + guestChanged + ", 누름 " + d[1]);
            Assert.That(d[1], Is.EqualTo("waiting"), "손님: 방의 허락을 기다린다");
            Assert.That(_guest.Until(() => _guest.Query("waiting-for-room") == "False" && room.All(c => session.HouseFloorAt(c) == Checker && GuestFloor(c) == Checker), 900), Is.True,
                "두 화면에 체크: 방장 " + session.HouseFloorAt(middle) + ", 손님 " + GuestFloor(middle));
            _guest.Until(() => false, 30); // 늦게 오는 상태·돈이 없는지

            string won = session.HouseFloorAt(middle);
            int hostAfter = session.Wallet.Balance, guestAfter = _guest.QueryInt("wallet");
            string hostRefusal = session.LastHouseFinishRefusal ?? string.Empty, guestRefusal = _guest.Query("finish-refusal") ?? string.Empty;
            string hostReasonText = HostReason, guestReasonText = _guest.Query("text", FurniturePlacementScreen.FinishReasonId);
            var answers = FinishAnswersSince(ledgerBefore);
            TestContext.WriteLine("[차례로] 끝 꾸밈 " + won + ", 돈 방장 " + hostBefore + "→" + hostAfter + ", 손님 " + guestBefore + "→" + guestAfter
                + ", 손님 바뀔 칸 " + guestChanged + ", 방의 꾸밈 답(순서): " + string.Join(" ", answers.Select(a => a.Who + ":" + a.Outcome)));
            Assert.That(won, Is.EqualTo(Checker), "나중에 칠한 손님의 꾸밈이 덮었다");
            AssertAppliedInTurn(answers, room, won, Carpet, Checker, hostBefore - hostAfter, CostOf(Carpet, first.Cells), guestBefore - guestAfter, CostOf(Checker, guestChanged),
                hostRefusal, guestRefusal, hostReasonText, guestReasonText);
            Assert.That(guestChanged, Is.EqualTo(room.Count), "손님 값의 칸 수: 카펫인 방 전부가 체크로 바뀐다");

            _host.MovePointer(-50, -50);
            _guest.Call("move-pointer", "-50", "-50");
            _host.Frames(2);
            TestContext.WriteLine("스크린샷: " + _host.ScreenshotScreen(ScreenCatalog.FurniturePlacement, "two-games-06-host-overwritten"));
            TestContext.WriteLine("스크린샷(손님): " + _guest.ScreenshotScreen(ScreenCatalog.FurniturePlacement, "two-games-06-guest-overwritten"));
            Assert.That(_host.Game.Renderer.MissingImages, Is.Empty);
            Assert.That(_guest.Query("missing-images"), Is.Empty);
        }
    }
}
