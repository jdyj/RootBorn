using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Screens;
using Rootborn.Screens.Inventory;
using Rootborn.Screens.Status;
using Rootborn.Ui;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 묶음 9(가방과 상태)를 실제 창과 게임 루프로: 새 슬롯 → I로 가방이 열림 → 가방이 떠 있는 채 걷기 → 탭을 포인터로 → 검색 칸에 한글 입력
    /// (입력 중에는 I·C 키 배정이 건너뛰어진다) → 칸을 눌러 상세 → C를 누르면 가방이 닫히고 상태가 열림 → Esc로 설정이 열리면 상태가 닫힘.
    /// 조작은 키·포인터·글자 이벤트뿐(장치 이벤트 주입, OS 입력 계층 미확인). 가방 안의 아이템은 준비 단계에서 가방에 직접 넣는다
    /// (새 게임 가방이 비어 있고, 아이템을 모으는 흐름은 이 시나리오의 대상이 아니다).
    /// 스크린샷: output/host-screens-evidence/bag-*.png, status-*.png.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class InventoryStatusHostTests
    {
        private string _saves;
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _saves = Path.Combine(Path.GetTempPath(), "rootborn-bag-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
            _root = HostOptions.Resolve(new[] { "--sample" }).RepoRoot;
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_saves, true); } catch (IOException) { }
        }

        private PlayerDriver Start()
        {
            HostOptions options = HostOptions.Resolve(new[]
            {
                "--save-root", _saves, "--slot", "slot-1", "--size", "1920x1080",
                "--evidence", Path.Combine(_root, "output", "host-screens-evidence"),
            });
            options.TestMode = true;
            var player = new PlayerDriver(options);
            player.Frames(3);
            return player;
        }

        [Test]
        public void BAG_STATUS_001_I_OpensBag_WalkWithItOpen_TypeHangulInSearch_C_SwapsToStatus_Esc_Closes_ThenOpensSettings()
        {
            using PlayerDriver player = Start();
            var inventory = player.Game.Session.Inventory;
            inventory.Clear();
            inventory.Add(player.Game.Session.Item("Wood"), 12);
            inventory.Add(player.Game.Session.Item("Stone"), 3);
            inventory.Add(player.Game.Session.Item("StoneAxe"), 1);
            inventory.Add(player.Game.Session.Item("item.allowance"), 5);

            // I → 가방.
            player.OpenScreenWithKey(TestKey.I, ScreenCatalog.Inventory);
            var bag = (InventoryScreen)player.Screens.Find(ScreenCatalog.Inventory);
            Assert.That(bag.Model.Rows.Count, Is.EqualTo(4));
            Assert.That(player.Screens.MovementBlocked, Is.False);
            player.MovePointer(-50, -50);
            player.Frames(2);
            player.ScreenshotScreen(ScreenCatalog.Inventory, "bag-01-open");

            // 가방이 떠 있는 채 걷는다(Unity 판은 가방이 이동을 막지 않았다).
            float x0 = player.World.Walker.X, y0 = player.World.Walker.Y;
            player.WalkAway(2);
            Assert.That(Math.Abs(player.World.Walker.X - x0) + Math.Abs(player.World.Walker.Y - y0), Is.GreaterThan(player.World.Map.Grid.CellSize));
            Assert.That(player.Screens.IsOpen(ScreenCatalog.Inventory), Is.True);

            // 도구 탭을 포인터로 누른다.
            var tabs = (UiTabBar)player.Node(InventoryScreen.TabsId);
            UiRect tab = tabs.TabRect(1);
            player.ClickAt(tab.CenterX, tab.CenterY);
            Assert.That(bag.Tab, Is.EqualTo(1));
            Assert.That(bag.Model.Rows.Select(r => r.Item.Id), Is.EqualTo(new[] { "StoneAxe" }));
            player.MovePointer(-50, -50);
            player.Frames(2);
            player.Screenshot("bag-02-tools-tab");
            tab = tabs.TabRect(0);
            player.ClickAt(tab.CenterX, tab.CenterY);
            Assert.That(bag.Model.Rows.Count, Is.EqualTo(4));

            // 검색 칸을 눌러 한글을 친다. 입력 중에는 I·C와 걷기가 건너뛰어진다.
            UiRect field = player.Node(InventoryScreen.SearchId).Rect;
            player.ClickAt(field.CenterX, field.CenterY);
            Assert.That(player.Screens.TextInputFocused, Is.True);
            Assert.That(player.Screens.MovementBlocked, Is.True);
            string woodName = player.Game.Text.Get(player.Game.Session.Item("Wood").DisplayKey);
            player.Compose(woodName.Substring(0, 1)); // 조합 중 글자
            player.TypeText(woodName);
            player.Frames(2);
            Assert.That(bag.Query, Is.EqualTo(woodName));
            Assert.That(bag.Model.Rows.Select(r => r.Item.Id), Is.EqualTo(new[] { "Wood" }));
            player.PressKey(TestKey.I);
            player.PressKey(TestKey.C);
            Assert.That(player.Screens.IsOpen(ScreenCatalog.Inventory), Is.True, "글자를 치는 동안 I는 가방을 닫지 않는다");
            Assert.That(player.Screens.IsOpen(ScreenCatalog.Status), Is.False, "글자를 치는 동안 C는 상태를 열지 않는다");
            player.MovePointer(-50, -50);
            player.Frames(2);
            player.Screenshot("bag-03-search-typed");

            // Esc로 글자 칸에서 나오고(설정은 열리지 않는다), 검색어를 지우면 전체가 다시 보인다.
            player.PressKey(TestKey.Escape);
            Assert.That(player.Screens.TextInputFocused, Is.False);
            Assert.That(player.Screens.IsOpen(ScreenCatalog.Settings), Is.False);
            player.ClickAt(field.CenterX, field.CenterY);
            player.PressKey(TestKey.Backspace, woodName.Length);
            player.Frames(2);
            Assert.That(bag.Model.Rows.Count, Is.EqualTo(4));
            player.PressKey(TestKey.Escape);

            // 칸을 눌러 상세 팝업.
            var grid = (UiCardGrid)player.Node(InventoryScreen.GridId);
            UiRect cell = grid.CellRect(0);
            player.ClickAt(cell.CenterX, cell.CenterY);
            Assert.That(bag.PopupOpen, Is.True);
            player.MovePointer(-50, -50);
            player.Frames(2);
            player.Screenshot("bag-04-detail");
            player.PressButton(InventoryScreen.PopupCloseId);
            Assert.That(bag.PopupOpen, Is.False);

            // C → 가방이 닫히고 상태가 열린다.
            player.PressKey(TestKey.C);
            player.WaitForScreen(ScreenCatalog.Status);
            Assert.That(player.Screens.IsOpen(ScreenCatalog.Inventory), Is.False);
            var status = (StatusScreen)player.Screens.Find(ScreenCatalog.Status);
            Assert.That(status.Model.Energy, Is.EqualTo(player.Game.Session.Student.Energy));
            Assert.That(player.Screens.MovementBlocked, Is.False);
            player.MovePointer(-50, -50);
            player.Frames(2);
            player.ScreenshotScreen(ScreenCatalog.Status, "status-01-open");

            // Esc → 상태가 닫히기만 한다(2026-11-13 Esc 규칙: 맨 위 화면을 닫고, 닫을 화면이 없을 때만 설정).
            player.CloseScreenWithKey(TestKey.Escape, ScreenCatalog.Status);
            Assert.That(player.Screens.IsOpen(ScreenCatalog.Settings), Is.False);
            // 한 번 더 Esc → 닫을 화면이 없으므로 설정이 열린다.
            player.OpenScreenWithKey(TestKey.Escape, ScreenCatalog.Settings);
            player.Screenshot("status-02-esc-settings");

            Assert.That(player.CollisionViolations, Is.EqualTo(0));
            Assert.That(player.Game.Renderer.MissingImages, Is.Empty, "아이콘 키와 아이템 그림이 모두 풀렸다");
            Rootborn.Screens.Tests.KnownMissingKeys.AssertOnlyKnownMissing(player.Game.Text, bag.TextKeys.Concat(status.TextKeys));
        }
    }
}
