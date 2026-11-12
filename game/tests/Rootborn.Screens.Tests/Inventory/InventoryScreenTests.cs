using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Inventory;
using Rootborn.Screens.Inventory;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests.Inventory
{
    public sealed class InventoryScreenTests
    {
        private static void Give(TestSession t, string itemId, int count) => t.Session.Inventory.Add(t.Session.Item(itemId), count);

        private static void ClickAt(TestSession t, float x, float y)
        {
            t.Stack.Pointer(x, y, false, 0);
            t.Stack.Pointer(x, y, true, 0);
            t.Stack.Pointer(x, y, false, 0);
        }

        private static void ClickCell(TestSession t, int index)
        {
            var grid = (UiCardGrid)t.Node(InventoryScreen.GridId);
            UiRect r = grid.CellRect(index);
            ClickAt(t, r.CenterX, r.CenterY);
        }

        private static void ClickTab(TestSession t, int index)
        {
            var tabs = (UiTabBar)t.Node(InventoryScreen.TabsId);
            UiRect r = tabs.TabRect(index);
            ClickAt(t, r.CenterX, r.CenterY);
        }

        private static InventoryScreen Open(TestSession t)
        {
            Assert.That(t.Stack.Key(ScreenKey.Inventory, false), Is.True);
            return t.Screen<InventoryScreen>(ScreenCatalog.Inventory);
        }

        [Test]
        public void I_OpensA1040x760PanelInTheMiddle_WithoutBlockingMovement_AndICloses()
        {
            using var t = new TestSession();
            Open(t);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Inventory), Is.True);
            Assert.That(t.Stack.MovementBlocked, Is.False);
            Assert.That(t.Node(InventoryScreen.RootId).Rect, Is.EqualTo(new UiRect(440, 160, 1040, 760)));
            Assert.That(((UiCardGrid)t.Node(InventoryScreen.GridId)).Columns, Is.EqualTo(5));
            Assert.That(t.Stack.Key(ScreenKey.Inventory, false), Is.True);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Inventory), Is.False);
        }

        [Test]
        public void TheFourTabsStickOutAboveThePanel_InUnityOrder()
        {
            using var t = new TestSession();
            Open(t);
            var tabs = (UiTabBar)t.Node(InventoryScreen.TabsId);
            Assert.That(tabs.Count, Is.EqualTo(4));
            UiRect panel = t.Node(InventoryScreen.RootId).Rect;
            Assert.That(tabs.Rect.Y, Is.EqualTo(panel.Y - 60f), "tab sticks out 60 above (height 96, overlap 36)");
            Assert.That(tabs.Rect.W, Is.EqualTo(4 * 128f + 3 * 8f));
            Assert.That(InventoryFilter.Tabs.Select(x => x.CategoryKey), Is.EqualTo(new[]
            {
                "ui.inventory.category.all", "ui.inventory.category.tools", "ui.inventory.category.resources", "ui.inventory.category.misc",
            }));
        }

        [Test]
        public void EmptyBag_ShowsOnlyTheEmptyHint()
        {
            using var t = new TestSession();
            t.Session.Inventory.Clear();
            var screen = Open(t);
            Assert.That(screen.Model.Rows, Is.Empty);
            Assert.That(t.Node(InventoryScreen.EmptyId).Visible, Is.True);
            Assert.That(t.Node(InventoryScreen.GridId).Visible, Is.False);
            Assert.That(t.Texts(t.Emit()), Does.Contain(t.Text.Get("ui.inventory.empty")));
        }

        [Test]
        public void Money_IsInTheWallet_ButStillShowsAsTheFirstCard_WithTheBalance()
        {
            // 돈은 가방 칸을 쓰지 않지만(지갑), 가방 화면에는 지금처럼 용돈 카드로 보인다(사용자 확인 필요: 맨 앞 자리).
            using var t = new TestSession();
            t.Session.Inventory.Clear();
            Give(t, "Wood", 5);
            Give(t, "item.allowance", 7);
            var screen = Open(t);
            Assert.That(t.Session.Inventory.Slots.Count, Is.EqualTo(1), "가방 칸은 나무 하나");
            Assert.That(t.Session.Wallet.Balance, Is.EqualTo(7));
            Assert.That(screen.Model.Rows.Select(r => (r.Item.Id, r.Count)), Is.EqualTo(new[] { ("item.allowance", 7), ("Wood", 5) }));
        }

        [Test]
        public void AFullBagAndMoney_EveryCardStillHasAFrame()
        {
            using var t = new TestSession();
            t.Session.Inventory.Clear();
            for (int i = 0; i < ItemInventory.MaxSlots; i++) Give(t, "Stone", t.Session.Item("Stone").MaxStack);
            Give(t, "item.allowance", 3);
            var screen = Open(t);
            Assert.That(screen.Model.Rows.Count, Is.EqualTo(ItemInventory.MaxSlots + 1));
            Assert.That(((UiCardGrid)t.Node(InventoryScreen.GridId)).ItemCount, Is.EqualTo(ItemInventory.MaxSlots + 1), "돈 카드 때문에 마지막 물건이 가려지지 않는다");
        }

        [Test]
        public void Items_ShowInSlotOrder_WithAllThirtyTwoSlotFrames()
        {
            using var t = new TestSession();
            t.Session.Inventory.Clear();
            Give(t, "Wood", 5);
            Give(t, "StoneAxe", 1);
            var screen = Open(t);
            Assert.That(screen.Model.Rows.Select(r => (r.Item.Id, r.Count)), Is.EqualTo(new[] { ("Wood", 5), ("StoneAxe", 1) }));
            var grid = (UiCardGrid)t.Node(InventoryScreen.GridId);
            Assert.That(grid.ItemCount, Is.EqualTo(ItemInventory.MaxSlots));
            Assert.That(t.Node(InventoryScreen.EmptyId).Visible, Is.False);
            DrawList list = t.Emit();
            Assert.That(list.Commands.Any(c => c.Kind == DrawKind.Image && c.Skin == "item:Wood"), Is.True, "only an icon key is said");
            Assert.That(t.Texts(list), Does.Contain("5"));
            Assert.That(t.Texts(list), Does.Not.Contain("1"), "count 1 is not written");
        }

        [Test]
        public void TabsFilterByCategory()
        {
            using var t = new TestSession();
            t.Session.Inventory.Clear();
            Give(t, "Wood", 5);
            Give(t, "StoneAxe", 1);
            Give(t, "item.allowance", 3);
            var screen = Open(t);
            ClickTab(t, 1);
            Assert.That(screen.Tab, Is.EqualTo(1));
            Assert.That(screen.Model.Rows.Select(r => r.Item.Id), Is.EqualTo(new[] { "StoneAxe" }));
            Assert.That(((UiLabel)t.Node(InventoryScreen.CategoryId)).Text, Is.EqualTo(t.Text.Get("ui.inventory.category.tools")));
            ClickTab(t, 2);
            Assert.That(screen.Model.Rows.Select(r => r.Item.Id), Is.EqualTo(new[] { "Wood" }));
            ClickTab(t, 3);
            Assert.That(screen.Model.Rows.Select(r => r.Item.Id), Is.EqualTo(new[] { "item.allowance" }));
            ClickTab(t, 0);
            Assert.That(screen.Model.Rows.Count, Is.EqualTo(3));
            Assert.That(((UiTabBar)t.Node(InventoryScreen.TabsId)).Selected, Is.EqualTo(0));
        }

        [Test]
        public void Search_FiltersByName_WhileTyping_AndIcKeysAreIgnoredWhileTheFieldHasFocus()
        {
            using var t = new TestSession();
            t.Session.Inventory.Clear();
            Give(t, "Wood", 5);
            Give(t, "Stone", 2);
            var screen = Open(t);
            Assert.That(screen.Model.Rows.Count, Is.EqualTo(2));

            UiRect field = t.Node(InventoryScreen.SearchId).Rect;
            ClickAt(t, field.CenterX, field.CenterY);
            Assert.That(t.Stack.TextInputFocused, Is.True);
            Assert.That(t.Stack.Key(ScreenKey.Inventory, false), Is.True);
            Assert.That(t.Stack.Key(ScreenKey.Status, false), Is.True);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Inventory), Is.True, "I does not close it while typing");
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Status), Is.False, "C does not open status while typing");
            Assert.That(t.Stack.MovementBlocked, Is.True, "no walking while typing");

            string woodName = t.Text.Get(t.Session.Item("Wood").DisplayKey);
            t.Stack.TextInput(woodName);
            t.Emit();
            Assert.That(screen.Query, Is.EqualTo(woodName));
            Assert.That(screen.Model.Rows.Select(r => r.Item.Id), Is.EqualTo(new[] { "Wood" }));

            t.Stack.TextInput("zzzz");
            t.Emit();
            Assert.That(screen.Model.Rows, Is.Empty);
            Assert.That(t.Node(InventoryScreen.EmptyId).Visible, Is.True);
        }

        [Test]
        public void SearchIgnoresCaseAndOuterSpaces_AndTabsGroupByCategory()
        {
            Assert.That(InventoryFilter.Matches(ItemCategory.Resource, "Wood Plank", 0, "  PLANK "), Is.True);
            Assert.That(InventoryFilter.Matches(ItemCategory.Resource, "Wood Plank", 1, ""), Is.False);
            Assert.That(InventoryFilter.Matches(ItemCategory.Seed, "seed", 2, "se"), Is.True);
            Assert.That(InventoryFilter.Matches(ItemCategory.Misc, "money", 3, null), Is.True);
        }

        [Test]
        public void ClickingASlot_OpensTheDetailPopup_ClosingClosesIt_EmptySlotsDoNothing()
        {
            using var t = new TestSession();
            t.Session.Inventory.Clear();
            Give(t, "Wood", 7);
            var screen = Open(t);
            Assert.That(screen.PopupOpen, Is.False);

            ClickCell(t, 20);
            Assert.That(screen.PopupOpen, Is.False, "an empty slot does nothing");

            ClickCell(t, 0);
            Assert.That(screen.PopupOpen, Is.True);
            Assert.That(((UiLabel)t.Node(InventoryScreen.PopupCountId)).Text, Is.EqualTo(t.Text.Format("ui.inventory.item-count", 7)));
            UiRect popup = t.Node(InventoryScreen.PopupId).Rect;
            Assert.That((popup.W, popup.H), Is.EqualTo((480f, 400f)));
            Assert.That(popup.CenterX, Is.EqualTo(960f + 552f), "520 + 32 right of the panel center");
            Assert.That(t.Texts(t.Emit()), Does.Contain(t.Text.Get(t.Session.Item("Wood").DisplayKey)));

            t.Click(InventoryScreen.PopupCloseId);
            Assert.That(screen.PopupOpen, Is.False);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Inventory), Is.True, "closing the detail does not close the bag");
        }

        [Test]
        public void TheCloseButton_ClosesTheBag_AndReopeningStartsWithAnEmptySearch()
        {
            using var t = new TestSession();
            var screen = Open(t);
            UiRect field = t.Node(InventoryScreen.SearchId).Rect;
            ClickAt(t, field.CenterX, field.CenterY);
            t.Stack.TextInput("abc");
            t.Emit();
            Assert.That(screen.Query, Is.EqualTo("abc"));
            t.Stack.Key(ScreenKey.Escape, false);
            t.Click(InventoryScreen.CloseId);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Inventory), Is.False);
            Open(t);
            Assert.That(screen.Query, Is.Empty);
            Assert.That(((UiTextField)t.Node(InventoryScreen.SearchId)).Text, Is.Empty);
        }

        [Test]
        public void BagChangesWhileOpen_ShowAtOnce_WithoutAScreenNotice()
        {
            using var t = new TestSession();
            t.Session.Inventory.Clear();
            var screen = Open(t);
            Assert.That(screen.Model.Rows, Is.Empty);
            Give(t, "Stone", 4);
            Assert.That(screen.Model.Rows.Select(r => r.Count), Is.EqualTo(new[] { 4 }));
        }

        [Test]
        public void BagAndStatusAndSettingsNeverOpenTogether()
        {
            using var t = new TestSession();
            Open(t);
            t.Stack.Key(ScreenKey.Status, false);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Inventory), Is.False);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Status), Is.True);
            t.Stack.Key(ScreenKey.Inventory, false);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Status), Is.False);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Inventory), Is.True);
            // Esc는 맨 위 패널(가방)을 닫는다. 닫을 화면이 없을 때의 Esc가 설정을 연다(EscapeKeyTests, 2026-11-13 임시 규칙).
            t.Stack.Key(ScreenKey.Escape, false);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Inventory), Is.False);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Settings), Is.False);
            t.Stack.Key(ScreenKey.Escape, false);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Settings), Is.True);
            t.Stack.Key(ScreenKey.Inventory, false);
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Settings), Is.False, "설정과 가방은 함께 열리지 않는다");
            Assert.That(t.Stack.IsOpen(ScreenCatalog.Inventory), Is.True);
        }

        [Test]
        public void KeyboardCanPickASlot_WithShiftArrowsAndEnter()
        {
            using var t = new TestSession();
            t.Session.Inventory.Clear();
            Give(t, "Wood", 7);
            var screen = Open(t);
            var grid = (UiCardGrid)t.Node(InventoryScreen.GridId);
            t.Stack.Tree.SetFocus(grid);
            Assert.That(t.Stack.Key(ScreenKey.Right, shift: true), Is.True);
            Assert.That(grid.SelectedIndex, Is.GreaterThanOrEqualTo(0));
            grid.SelectedIndex = 0;
            Assert.That(t.Stack.Key(ScreenKey.Enter, false), Is.True);
            Assert.That(screen.PopupOpen, Is.True);
        }

        [Test]
        public void English_RedrawsTheText_AndOnlyKnownKeysAreMissing()
        {
            using var t = new TestSession();
            t.Session.Inventory.Clear();
            Give(t, "Wood", 7);
            var screen = Open(t);
            ClickCell(t, 0);
            t.Text.Language = "en";
            Assert.That(((UiLabel)t.Node(InventoryScreen.TitleId)).Text, Is.EqualTo("Inventory"));
            Assert.That(((UiLabel)t.Node(InventoryScreen.CategoryId)).Text, Is.EqualTo("All"));
            Assert.That(((UiTextField)t.Node(InventoryScreen.SearchId)).Placeholder, Is.EqualTo("Search by name"));
            t.Emit();
            KnownMissingKeys.AssertOnlyKnownMissing(t.Text, screen.TextKeys);
        }

        [Test]
        public void LongTexts_StayInsideTheirBoxes()
        {
            using var t = new TestSession();
            t.Session.Inventory.Clear();
            Give(t, "Wood", 7);
            Open(t);
            ClickCell(t, 0);
            var name = (UiShrinkLabel)t.Node(InventoryScreen.PopupNameId);
            name.Text = new string('A', 80);
            var measurer = new TestMeasurer();
            var list = new DrawList();
            name.Emit(list, measurer);
            DrawCommand drawn = list.Commands.Single();
            Assert.That(measurer.Width(drawn.Text, drawn.FontSize), Is.LessThanOrEqualTo(name.Rect.W + 0.01f));
            Assert.That(drawn.FontSize, Is.EqualTo(24f), "shrinks to the minimum, then ellipsis");
            var description = (UiTextBlock)t.Node(InventoryScreen.PopupDescId);
            description.Text = new string('B', 400);
            Assert.That(description.LineCount(measurer), Is.GreaterThan(1), "the description wraps");
        }
    }
}
