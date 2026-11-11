using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Rootborn.Ui.Tests
{
    internal static class Pointer
    {
        public static void Click(UiTree tree, float x, float y)
        {
            tree.UpdatePointer(x, y, false);
            tree.UpdatePointer(x, y, true);
            tree.UpdatePointer(x, y, false);
        }

        public static List<DrawCommand> Emit(UiTree tree)
        {
            var list = new DrawList();
            tree.Emit(list, new FakeMeasurer());
            return list.Commands;
        }
    }

    public sealed class TextWrapTests
    {
        private static readonly FakeMeasurer M = new FakeMeasurer(); // 글자 하나 = 크기의 절반 → 크기 20이면 10

        private static List<string> Wrap(string text, float width)
        {
            var lines = new List<string>();
            TextWrap.Wrap(text, width, M, 20, lines);
            return lines;
        }

        [Test]
        public void Korean_BreaksBetweenAnyTwoSyllables()
        {
            Assert.That(Wrap("가나다라마바사", 30), Is.EqualTo(new[] { "가나다", "라마바", "사" }));
        }

        [Test]
        public void Latin_BreaksAtSpaces_AndDropsTheSpaceAtTheBreak()
        {
            Assert.That(Wrap("one two three", 80), Is.EqualTo(new[] { "one two", "three" }));
        }

        [Test]
        public void LatinWordLongerThanTheLine_IsCutByCharacters()
        {
            Assert.That(Wrap("abcdefghij", 40), Is.EqualTo(new[] { "abcd", "efgh", "ij" }));
        }

        [Test]
        public void Newlines_StartNewLines_AndEmptyLinesStay()
        {
            Assert.That(Wrap("가나\n\n다", 100), Is.EqualTo(new[] { "가나", "", "다" }));
        }

        [Test]
        public void KoreanSentenceWithSpaces_PrefersSpacesThenSyllables()
        {
            // "안녕 하세요" 폭 30: "안녕" 다음 공백에서 끊고, "하세요"는 3글자 = 30으로 한 줄
            Assert.That(Wrap("안녕 하세요", 30), Is.EqualTo(new[] { "안녕", "하세요" }));
        }
    }

    public sealed class TextBlockTests
    {
        [Test]
        public void Emits_OneTextCommandPerWrappedLine_StackedByLineHeight()
        {
            var tree = new UiTree();
            var block = tree.Root.Add(new UiTextBlock { Text = "가나다라마바사", FontSize = 20, LineSpacing = 4 });
            block.Place(0, 0, 10, 10, 30, 200);
            tree.Layout(1920, 1080);
            List<DrawCommand> texts = Pointer.Emit(tree).Where(c => c.Kind == DrawKind.Text).ToList();
            Assert.That(texts.Select(t => t.Text), Is.EqualTo(new[] { "가나다", "라마바", "사" }));
            Assert.That(texts.Select(t => t.Rect.Y), Is.EqualTo(new[] { 10f, 34f, 58f }));
            Assert.That(block.LineCount(new FakeMeasurer()), Is.EqualTo(3));
        }

        [Test]
        public void CenterAlign_CentersEachLine()
        {
            var tree = new UiTree();
            tree.Root.Add(new UiTextBlock { Text = "가나다라", FontSize = 20, Align = UiAlign.Center }).Place(0, 0, 0, 0, 30, 100);
            tree.Layout(1920, 1080);
            List<DrawCommand> texts = Pointer.Emit(tree).Where(c => c.Kind == DrawKind.Text).ToList();
            Assert.That(texts[0].Rect.X, Is.EqualTo(0f));
            Assert.That(texts[1].Rect.X, Is.EqualTo(10f)); // "라" 폭 10 → (30-10)/2
        }
    }

    public sealed class ImageTests
    {
        [Test]
        public void Emits_ImageCommand_WithImageIdAndSourceCell()
        {
            var tree = new UiTree();
            tree.Root.Add(new UiImage { Image = "Assets/a.png#icon", Source = new PixelRect(16, 32, 16, 16) }).Place(0, 0, 5, 6, 48, 48);
            tree.Layout(1920, 1080);
            DrawCommand image = Pointer.Emit(tree).Single();
            Assert.That(image.Kind, Is.EqualTo(DrawKind.Image));
            Assert.That(image.Skin, Is.EqualTo("Assets/a.png#icon"));
            Assert.That(image.Source, Is.EqualTo(new PixelRect(16, 32, 16, 16)));
            Assert.That(image.Rect, Is.EqualTo(new UiRect(5, 6, 48, 48)));
        }

        [Test]
        public void EmptyImageId_DrawsNothing()
        {
            var tree = new UiTree();
            tree.Root.Add(new UiImage()).Place(0, 0, 0, 0, 10, 10);
            tree.Layout(1920, 1080);
            Assert.That(Pointer.Emit(tree), Is.Empty);
        }
    }

    public sealed class TabBarTests
    {
        private UiTree _tree;
        private UiTabBar _tabs;
        private List<int> _changes;

        // 탭 셋, 줄 (0,0) 300x50 → 탭 하나 폭 100
        [SetUp]
        public void Make()
        {
            _tree = new UiTree();
            _changes = new List<int>();
            _tabs = _tree.Root.Add(new UiTabBar { Id = "tabs" });
            _tabs.SetTabs(new[] { "가", "나", "다" });
            _tabs.SelectionChanged += _changes.Add;
            _tabs.Place(0, 0, 0, 0, 300, 50);
            _tree.Layout(1920, 1080);
        }

        [Test]
        public void PointerClick_SelectsTheTabUnderThePointer()
        {
            Pointer.Click(_tree, 250, 25);
            Assert.That(_tabs.Selected, Is.EqualTo(2));
            Assert.That(_changes, Is.EqualTo(new[] { 2 }));
            Pointer.Click(_tree, 250, 25);
            Assert.That(_changes, Is.EqualTo(new[] { 2 }), "이미 고른 탭은 다시 알리지 않는다");
        }

        [Test]
        public void Keyboard_LeftRightMovesSelection_AndStopsAtTheEnds()
        {
            _tree.SetFocus(_tabs);
            _tree.Navigate(UiNav.Right);
            _tree.Navigate(UiNav.Right);
            Assert.That(_tabs.Selected, Is.EqualTo(2));
            _tree.Navigate(UiNav.Right); // 끝에서는 탭 줄을 벗어나려 한다(다른 요소 없음 → 그대로)
            Assert.That(_tabs.Selected, Is.EqualTo(2));
            _tree.Navigate(UiNav.Left);
            Assert.That(_changes, Is.EqualTo(new[] { 1, 2, 1 }));
        }

        [Test]
        public void SelectedTab_IsRaised()
        {
            _tabs.Selected = 1;
            Assert.That(_tabs.TabRect(1).Y, Is.LessThan(_tabs.TabRect(0).Y), "고른 탭은 위로 튀어나온다(ui-standards 선택 표시)");
            Assert.That(_tabs.TabRect(2).X, Is.EqualTo(200f));
        }
    }

    public sealed class GaugeTests
    {
        [Test]
        public void Fill_IsProportional_AndClamped()
        {
            var tree = new UiTree();
            var gauge = tree.Root.Add(new UiGauge { Max = 10, Value = 4, Padding = 0 });
            gauge.Place(0, 0, 0, 0, 200, 20);
            tree.Layout(1920, 1080);
            Assert.That(gauge.FillRect.W, Is.EqualTo(80f));
            gauge.Value = 25;
            Assert.That(gauge.FillRect.W, Is.EqualTo(200f));
            gauge.Value = -3;
            Assert.That(gauge.FillRect.W, Is.EqualTo(0f));
            gauge.Max = 0;
            Assert.That(gauge.FillRect.W, Is.EqualTo(0f), "최대가 0이면 비운다");
        }

        [Test]
        public void Gauge_DoesNotTakePointerOrFocus()
        {
            var gauge = new UiGauge();
            Assert.That(gauge.Interactive, Is.False);
            Assert.That(gauge.Focusable, Is.False);
        }
    }

    public sealed class ToggleTests
    {
        private UiTree _tree;
        private UiToggle _toggle;
        private List<bool> _changes;

        [SetUp]
        public void Make()
        {
            _tree = new UiTree();
            _changes = new List<bool>();
            _toggle = _tree.Root.Add(new UiToggle { Id = "t", IsOn = true });
            _toggle.Toggled += _changes.Add;
            _toggle.Place(0, 0, 100, 100, 78, 36);
            _tree.Layout(1920, 1080);
        }

        [Test]
        public void PointerClick_Flips()
        {
            Pointer.Click(_tree, 120, 110);
            Assert.That(_toggle.IsOn, Is.False);
            Pointer.Click(_tree, 120, 110);
            Assert.That(_changes, Is.EqualTo(new[] { false, true }));
        }

        [Test]
        public void Enter_Flips()
        {
            _tree.SetFocus(_toggle);
            _tree.Submit();
            Assert.That(_toggle.IsOn, Is.False);
            Assert.That(_changes, Is.EqualTo(new[] { false }));
        }

        [Test]
        public void SettingIsOnFromCode_DoesNotRaiseToggled()
        {
            _toggle.IsOn = false;
            Assert.That(_changes, Is.Empty);
        }

        [Test]
        public void Knob_SitsLeftWhenOn_RightWhenOff()
        {
            // Unity SettingsPanel: 켬이면 손잡이가 -18, 끔이면 +18(가운데 기준).
            Assert.That(_toggle.KnobRect.CenterX, Is.EqualTo(100 + 39 - 18f));
            _toggle.IsOn = false;
            Assert.That(_toggle.KnobRect.CenterX, Is.EqualTo(100 + 39 + 18f));
        }
    }

    public sealed class SliderTests
    {
        private UiTree _tree;
        private UiSlider _slider;
        private List<float> _changes;

        // 막대 (100,100) 220x30, 손잡이 폭 20 → 움직이는 폭 200(x 110~310)
        [SetUp]
        public void Make()
        {
            _tree = new UiTree();
            _changes = new List<float>();
            _slider = _tree.Root.Add(new UiSlider { Id = "s", Min = 0, Max = 10, Step = 1, KnobWidth = 20 });
            _slider.ValueChanged += _changes.Add;
            _slider.Place(0, 0, 100, 100, 220, 30);
            _tree.Layout(1920, 1080);
        }

        [Test]
        public void Press_JumpsToThePointer_SnappedToStep()
        {
            _tree.UpdatePointer(212, 110, false);
            _tree.UpdatePointer(212, 110, true); // (212-110)/200 = 0.51 → 5.1 → 5
            Assert.That(_slider.Value, Is.EqualTo(5f));
            _tree.UpdatePointer(212, 110, false);
            Assert.That(_changes, Is.EqualTo(new[] { 5f }));
        }

        [Test]
        public void Drag_FollowsThePointer_AndClamps()
        {
            _tree.UpdatePointer(110, 110, false);
            _tree.UpdatePointer(110, 110, true);
            _tree.UpdatePointer(250, 110, true);
            Assert.That(_slider.Value, Is.EqualTo(7f));
            _tree.UpdatePointer(900, 110, true);
            Assert.That(_slider.Value, Is.EqualTo(10f));
            _tree.UpdatePointer(900, 110, false);
            Assert.That(_changes, Is.EqualTo(new[] { 7f, 10f }), "0에서 시작했으니 처음 누른 0은 바뀜이 아니다");
        }

        [Test]
        public void Keyboard_LeftRightStep_WhenFocused()
        {
            _tree.SetFocus(_slider);
            _tree.Navigate(UiNav.Right);
            _tree.Navigate(UiNav.Right);
            Assert.That(_slider.Value, Is.EqualTo(2f));
            _tree.Navigate(UiNav.Left);
            Assert.That(_changes, Is.EqualTo(new[] { 1f, 2f, 1f }));
        }

        [Test]
        public void SettingValueFromCode_Clamps_AndDoesNotRaise()
        {
            _slider.Value = 99;
            Assert.That(_slider.Value, Is.EqualTo(10f));
            Assert.That(_changes, Is.Empty);
        }
    }

    public sealed class CardRowTests
    {
        private UiTree _tree;
        private UiCardRow _row;
        private List<int> _clicked, _drawn;

        // 줄 (0,0) 520x200, 여백 10 → 보이는 폭 500. 카드 폭 100 + 간격 25 = 한 칸 125 → 4칸(마지막은 일부)
        private void Make(int count)
        {
            _tree = new UiTree();
            _clicked = new List<int>();
            _drawn = new List<int>();
            _row = _tree.Root.Add(new UiCardRow { Id = "row", CardCount = count, CardWidth = 100, Spacing = 25, Padding = 10 });
            _row.DrawCard = (i, rect, state, list, text) => _drawn.Add(i);
            _row.CardClicked += _clicked.Add;
            _row.Place(0, 0, 0, 0, 520, 200);
            _tree.Layout(1920, 1080);
        }

        [Test]
        public void OnlyVisibleCards_AreDrawn()
        {
            Make(1000);
            Pointer.Emit(_tree);
            Assert.That(_drawn, Is.EqualTo(new[] { 0, 1, 2, 3 }));
            _row.ScrollOffset = 1250; // 10번째 카드부터
            _drawn.Clear();
            Pointer.Emit(_tree);
            Assert.That(_drawn, Is.EqualTo(new[] { 10, 11, 12, 13 }));
        }

        [Test]
        public void Click_HitsTheCard_ButNotTheGap()
        {
            Make(10);
            Pointer.Click(_tree, 10 + 125 + 50, 100);
            Assert.That(_clicked, Is.EqualTo(new[] { 1 }));
            Pointer.Click(_tree, 10 + 110, 100); // 0번과 1번 사이 간격
            Assert.That(_clicked, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void Wheel_ScrollsSideways_AndDragScrolls()
        {
            Make(20);
            _tree.UpdatePointer(200, 100, false, -1f);
            Assert.That(_row.ScrollOffset, Is.EqualTo(125f));
            _tree.UpdatePointer(300, 100, true);
            _tree.UpdatePointer(200, 100, true); // 왼쪽으로 100 끌기
            _tree.UpdatePointer(200, 100, false);
            Assert.That(_row.ScrollOffset, Is.EqualTo(225f));
            Assert.That(_clicked, Is.Empty, "끌기는 클릭이 아니다");
        }

        [Test]
        public void Keyboard_RightSelectsNext_ScrollsIntoView_EnterClicks()
        {
            Make(20);
            _tree.SetFocus(_row);
            for (int i = 0; i < 6; i++) _tree.Navigate(UiNav.Right);
            Assert.That(_row.SelectedIndex, Is.EqualTo(5));
            Assert.That(_row.ScrollOffset, Is.EqualTo(5 * 125 + 100 - 500f));
            _tree.Submit();
            Assert.That(_clicked, Is.EqualTo(new[] { 5 }));
        }
    }

    public sealed class CardGridTests
    {
        private UiTree _tree;
        private UiCardGrid _grid;
        private List<int> _clicked, _drawn;

        // 격자 (0,0) 440x420, 여백 10 → 보이는 영역 420x400. 칸 100x100, 간격 0 → 줄마다 4칸, 보이는 줄 4
        private void Make(int count)
        {
            _tree = new UiTree();
            _clicked = new List<int>();
            _drawn = new List<int>();
            _grid = _tree.Root.Add(new UiCardGrid { Id = "grid", ItemCount = count, CellWidth = 100, CellHeight = 100, Spacing = 0, Padding = 10 });
            _grid.DrawCell = (i, rect, state, list, text) => _drawn.Add(i);
            _grid.CellClicked += _clicked.Add;
            _grid.Place(0, 0, 0, 0, 440, 420);
            _tree.Layout(1920, 1080);
        }

        [Test]
        public void ColumnsFitTheWidth_AndRowsRoundUp()
        {
            Make(1503);
            Assert.That(_grid.Columns, Is.EqualTo(4));
            Assert.That(_grid.RowCount, Is.EqualTo(376));
        }

        [Test]
        public void OnlyVisibleRows_AreDrawn_EvenWith1500Items()
        {
            Make(1503);
            Pointer.Emit(_tree);
            Assert.That(_drawn, Is.EqualTo(Enumerable.Range(0, 16)));
            _grid.ScrollOffset = 150; // 1번 줄 반부터 5번 줄 반까지
            _drawn.Clear();
            Pointer.Emit(_tree);
            Assert.That(_drawn, Is.EqualTo(Enumerable.Range(4, 20)));
        }

        [Test]
        public void LastRow_DrawsOnlyExistingItems()
        {
            Make(6);
            Pointer.Emit(_tree);
            Assert.That(_drawn, Is.EqualTo(Enumerable.Range(0, 6)));
        }

        [Test]
        public void Click_PicksTheCell()
        {
            Make(100);
            Pointer.Click(_tree, 10 + 250, 10 + 150);
            Assert.That(_clicked, Is.EqualTo(new[] { 6 }));
            Assert.That(_grid.SelectedIndex, Is.EqualTo(6));
        }

        [Test]
        public void Keyboard_MovesInFourDirections_AndScrolls()
        {
            Make(100);
            _tree.SetFocus(_grid);
            _tree.Navigate(UiNav.Down); // 처음엔 0번을 고른다
            Assert.That(_grid.SelectedIndex, Is.EqualTo(0));
            _tree.Navigate(UiNav.Right);
            _tree.Navigate(UiNav.Down);
            Assert.That(_grid.SelectedIndex, Is.EqualTo(5));
            for (int i = 0; i < 5; i++) _tree.Navigate(UiNav.Down);
            Assert.That(_grid.SelectedIndex, Is.EqualTo(25));
            Assert.That(_grid.ScrollOffset, Is.EqualTo(700 - 400f), "6번 줄 아래끝이 보이게");
            _tree.Navigate(UiNav.Left);
            Assert.That(_grid.SelectedIndex, Is.EqualTo(24));
            _tree.Submit();
            Assert.That(_clicked, Is.EqualTo(new[] { 24 }));
        }

        [Test]
        public void Scrollbar_IsDrawnOnTheRight_OnlyWhenScrollable()
        {
            Make(100);
            var view = _grid.View;
            Assert.That(Pointer.Emit(_tree).Count(c => c.Kind == DrawKind.Fill && c.Rect.X == view.Right - UiCardScroller.ScrollbarWidth), Is.EqualTo(2), "막대와 손잡이");
            Make(6);
            Assert.That(Pointer.Emit(_tree).Count(c => c.Kind == DrawKind.Fill), Is.EqualTo(0));
        }

        [Test]
        public void Wheel_ScrollsRows()
        {
            Make(100);
            _tree.UpdatePointer(100, 100, false, -1f);
            Assert.That(_grid.ScrollOffset, Is.EqualTo(100f));
        }

        [Test]
        public void ItemCountShrink_ClampsScrollAndSelection()
        {
            Make(100);
            _grid.ScrollOffset = 2000;
            _grid.SelectedIndex = 90;
            _grid.ItemCount = 8;
            Assert.That(_grid.ScrollOffset, Is.EqualTo(0f));
            Assert.That(_grid.SelectedIndex, Is.EqualTo(-1));
        }
    }

    public sealed class FocusScopeTests
    {
        [Test]
        public void Navigate_StaysInsideTheFocusScope()
        {
            var tree = new UiTree();
            var outside = tree.Root.Add(new UiButton { Id = "out" });
            outside.Place(0, 0, 0, 0, 50, 50);
            var scope = tree.Root.Add(new UiNode { Id = "scope" });
            scope.Place(0, 0, 100, 0, 300, 300);
            var a = scope.Add(new UiButton { Id = "a" });
            a.Place(0, 0, 0, 0, 50, 50);
            var b = scope.Add(new UiButton { Id = "b" });
            b.Place(0, 0, 0, 100, 50, 50);
            tree.Layout(1920, 1080);
            tree.FocusScope = scope;
            tree.Navigate(UiNav.Next);
            tree.Navigate(UiNav.Next);
            Assert.That(tree.Focused, Is.SameAs(b));
            tree.Navigate(UiNav.Next); // 범위 끝에서 범위 처음으로 돈다(바깥 버튼으로 가지 않는다)
            Assert.That(tree.Focused, Is.SameAs(a));
            tree.Navigate(UiNav.Previous);
            Assert.That(tree.Focused, Is.SameAs(b));
        }
    }
}

namespace Rootborn.Ui.Tests
{
    public sealed class StretchedNineSliceTests
    {
        // 도시형 몰딩 패널: 원본 (8,8) 96x96, 테두리 18.
        private static readonly NineSliceSource Urban = new NineSliceSource(8, 26, 86, 104, 8, 26, 86, 104);

        [Test]
        public void NinePieces_BordersScaled_CenterStretched()
        {
            var pieces = new List<SlicePiece>();
            NineSlice.BuildStretched(new PixelRect(100, 50, 660, 510), Urban, 2, pieces);
            Assert.That(pieces.Count, Is.EqualTo(9));
            Assert.That(pieces[0], Is.EqualTo(new SlicePiece(new PixelRect(100, 50, 36, 36), new PixelRect(8, 8, 18, 18))));
            Assert.That(pieces[4], Is.EqualTo(new SlicePiece(new PixelRect(136, 86, 588, 438), new PixelRect(26, 26, 60, 60))));
            Assert.That(pieces[8], Is.EqualTo(new SlicePiece(new PixelRect(724, 524, 36, 36), new PixelRect(86, 86, 18, 18))));
        }

        [Test]
        public void SmallerThanBorders_ShrinksBordersAndDropsTheMiddle()
        {
            var pieces = new List<SlicePiece>();
            NineSlice.BuildStretched(new PixelRect(0, 0, 40, 200), Urban, 2, pieces);
            Assert.That(pieces.Where(p => p.Dest.W == 0 || p.Dest.H == 0), Is.Empty, "빈 조각은 내지 않는다");
            Assert.That(pieces.Sum(p => (long)p.Dest.W * p.Dest.H), Is.EqualTo(40L * 200));
        }
    }
}
