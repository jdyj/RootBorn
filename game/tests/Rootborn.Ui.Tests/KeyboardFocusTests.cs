using NUnit.Framework;

namespace Rootborn.Ui.Tests
{
    /// <summary>키보드만으로 일하기 위한 포커스 규칙(UiTree 한 곳): 조용한 처음 포커스, 처음 요소 고르기, 스크롤 글, 조합 중인 글자 칸.</summary>
    public sealed class KeyboardFocusTests
    {
        private static (UiTree tree, UiButton a, UiButton b) TwoButtons()
        {
            var tree = new UiTree();
            var a = tree.Root.Add(new UiButton { Id = "a" });
            a.Place(0, 0, 0, 0, 100, 40);
            var b = tree.Root.Add(new UiButton { Id = "b" });
            b.Place(0, 0, 200, 0, 100, 40);
            tree.Layout(800, 600);
            return (tree, a, b);
        }

        [Test]
        public void QuietFocus_HasNoRing_FirstArrowShowsTheRingInPlace_ThenArrowsMove()
        {
            var (tree, a, b) = TwoButtons();
            tree.SetFocusQuietly(a);
            Assert.That(tree.Focused, Is.SameAs(a));
            Assert.That(a.FocusRing, Is.False);

            tree.Navigate(UiNav.Right);
            Assert.That(tree.Focused, Is.SameAs(a), "첫 키는 옮기지 않는다");
            Assert.That(a.FocusRing, Is.True);
            tree.Navigate(UiNav.Right);
            Assert.That(tree.Focused, Is.SameAs(b));
        }

        [Test]
        public void QuietFocus_EnterPressesItRightAway()
        {
            var (tree, a, _) = TwoButtons();
            int clicks = 0;
            a.Clicked += () => clicks++;
            tree.SetFocusQuietly(a);
            tree.Submit();
            Assert.That(clicks, Is.EqualTo(1));
        }

        [Test]
        public void PointerFocus_IsNotQuiet_ArrowsMoveAtOnce()
        {
            var (tree, a, b) = TwoButtons();
            tree.UpdatePointer(50, 20, false);
            tree.UpdatePointer(50, 20, true);
            tree.UpdatePointer(50, 20, false);
            Assert.That(tree.Focused, Is.SameAs(a));
            tree.Navigate(UiNav.Right);
            Assert.That(tree.Focused, Is.SameAs(b), "포인터로 받은 포커스는 지금처럼 바로 옮긴다");
        }

        [Test]
        public void FirstFocusable_SkipsTextFieldsHiddenAndTheGivenNode()
        {
            var tree = new UiTree();
            var close = tree.Root.Add(new UiButton { Id = "close" });
            tree.Root.Add(new UiTextField { Id = "field" });
            tree.Root.Add(new UiButton { Id = "hidden", Visible = false });
            var choice = tree.Root.Add(new UiButton { Id = "choice" });
            Assert.That(UiTree.FirstFocusable(tree.Root), Is.SameAs(close));
            Assert.That(UiTree.FirstFocusable(tree.Root, close), Is.SameAs(choice));
        }

        [Test]
        public void SectionScrollView_TakesFocusOnlyWhenItOverflows_ScrollsWithUpDown_LeavesAtTheEnds()
        {
            var tree = new UiTree();
            var view = tree.Root.Add(new UiSectionScrollView { Id = "view", FontSize = 20, WheelStep = 40 });
            view.Place(0, 0, 0, 0, 300, 100);
            var below = tree.Root.Add(new UiButton { Id = "below" });
            below.Place(0, 0, 0, 200, 300, 40);
            tree.Layout(800, 600);
            var measurer = new FakeMeasurer();

            view.SetSections(new[] { "a" });
            view.Emit(new DrawList(), measurer);
            Assert.That(view.Focusable, Is.False, "넘치지 않으면 지나치는 칸이 되지 않는다");

            view.SetSections(new[] { "1\n2\n3\n4\n5\n6\n7\n8\n9\n10" });
            view.Emit(new DrawList(), measurer);
            Assert.That(view.Focusable, Is.True);
            tree.SetFocus(view);
            tree.Navigate(UiNav.Down);
            Assert.That(view.ScrollOffset, Is.EqualTo(40), "아래 키가 한 걸음 스크롤한다");
            for (int i = 0; i < 50 && tree.Focused == view; i++) tree.Navigate(UiNav.Down);
            Assert.That(view.ScrollOffset, Is.EqualTo(view.MaxScroll));
            Assert.That(tree.Focused, Is.SameAs(below), "끝에서 누르면 다음 요소로 나간다");

            tree.SetFocus(view);
            var list = new DrawList();
            view.Emit(list, measurer);
            Assert.That(list.Commands.Exists(c => c.Kind == DrawKind.Fill && c.Color.Equals(UiTheme.Accent)), Is.True, "키보드 포커스 테두리");
        }

        [Test]
        public void CardGrid_DrawsTheFocusRingInsideTheChosenCell_SoTheEdgeCellIsNotClipped()
        {
            var tree = new UiTree();
            var grid = tree.Root.Add(new UiCardGrid { Padding = 0, CellWidth = 100, CellHeight = 50, ItemCount = 6 });
            grid.Place(0, 0, 0, 0, 340, 200);
            tree.Layout(800, 600);
            tree.SetFocus(grid);
            tree.Navigate(UiNav.Down); // 첫 칸을 고른다
            Assert.That(grid.SelectedIndex, Is.EqualTo(0));
            var list = new DrawList();
            grid.Emit(list, new FakeMeasurer());
            var ring = list.Commands.FindAll(c => c.Kind == DrawKind.Fill && c.Color.Equals(UiTheme.Accent));
            Assert.That(ring.Count, Is.EqualTo(4), "고른 칸에 테두리 하나(칸 그리기가 따로 그리지 않아도)");
            UiRect cell = grid.CellRect(0), view = grid.View;
            foreach (DrawCommand c in ring)
                Assert.That(c.Rect.X >= view.X && c.Rect.Y >= view.Y && c.Rect.Right <= cell.Right && c.Rect.Bottom <= cell.Bottom, Is.True, "칸 안쪽, 보이는 영역 안: " + c.Rect);
        }

        [Test]
        public void TextField_WhileComposing_KeepsArrowsAndEnter()
        {
            var field = new UiTextField();
            string sent = null;
            field.Submitted += t => sent = t;
            field.OnTextComposition("ㄱ");
            Assert.That(field.Composing, Is.True);
            Assert.That(field.OnNavigate(UiNav.Up), Is.True, "조합 중에는 위 키도 칸 안의 것");
            field.OnSubmit();
            Assert.That(sent, Is.Null, "조합 중 Enter는 보내지 않는다");
            field.OnTextInput("가");
            Assert.That(field.OnNavigate(UiNav.Up), Is.False, "조합이 끝나면 위 키로 칸을 나간다");
            field.OnSubmit();
            Assert.That(sent, Is.EqualTo("가"));
        }
    }
}
