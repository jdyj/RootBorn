using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Rootborn.Ui.Tests
{
    public sealed class NineSliceTests
    {
        // Style2 공통 패널 실측: 왼쪽 5, 가운데 16, 오른쪽 5 / 위 6, 가운데 16, 아래 5.
        private static readonly NineSliceSource Common = new NineSliceSource(11, 16, 32, 37, 42, 48, 64, 69);

        private static List<SlicePiece> Build(PixelRect dest, int scale)
        {
            var pieces = new List<SlicePiece>();
            NineSlice.Build(dest, Common, scale, pieces);
            return pieces;
        }

        [Test]
        public void Source_ReportsBorderThicknessPerSide()
        {
            Assert.That(Common.ColW(0), Is.EqualTo(5));
            Assert.That(Common.ColW(2), Is.EqualTo(5));
            Assert.That(Common.RowH(0), Is.EqualTo(6));
            Assert.That(Common.RowH(2), Is.EqualTo(5));
        }

        [Test]
        public void Corners_KeepTheirSize_AndSitOnTheCorners()
        {
            var dest = new PixelRect(100, 50, 300, 200);
            List<SlicePiece> pieces = Build(dest, 3);
            Assert.That(pieces, Does.Contain(new SlicePiece(new PixelRect(100, 50, 15, 18), new PixelRect(11, 42, 5, 6))));
            Assert.That(pieces, Does.Contain(new SlicePiece(new PixelRect(385, 50, 15, 18), new PixelRect(32, 42, 5, 6))));
            Assert.That(pieces, Does.Contain(new SlicePiece(new PixelRect(100, 235, 15, 15), new PixelRect(11, 64, 5, 5))));
            Assert.That(pieces, Does.Contain(new SlicePiece(new PixelRect(385, 235, 15, 15), new PixelRect(32, 64, 5, 5))));
        }

        [Test]
        public void Pieces_CoverTheDestinationExactlyOnce()
        {
            var dest = new PixelRect(7, 9, 301, 211);
            List<SlicePiece> pieces = Build(dest, 3);
            long area = pieces.Sum(p => (long)p.Dest.W * p.Dest.H);
            Assert.That(area, Is.EqualTo((long)dest.W * dest.H));
            foreach (SlicePiece p in pieces)
            {
                Assert.That(p.Dest.W, Is.GreaterThan(0));
                Assert.That(p.Dest.H, Is.GreaterThan(0));
                Assert.That(p.Dest.X, Is.GreaterThanOrEqualTo(dest.X));
                Assert.That(p.Dest.Y, Is.GreaterThanOrEqualTo(dest.Y));
                Assert.That(p.Dest.X + p.Dest.W, Is.LessThanOrEqualTo(dest.X + dest.W));
                Assert.That(p.Dest.Y + p.Dest.H, Is.LessThanOrEqualTo(dest.Y + dest.H));
            }
            for (int i = 0; i < pieces.Count; i++)
                for (int j = i + 1; j < pieces.Count; j++)
                    Assert.That(Overlap(pieces[i].Dest, pieces[j].Dest), Is.False, $"조각 {i}와 {j}가 겹친다");
        }

        private static bool Overlap(PixelRect a, PixelRect b) =>
            a.X < b.X + b.W && b.X < a.X + a.W && a.Y < b.Y + b.H && b.Y < a.Y + a.H;

        [Test]
        public void EdgesAndFill_RepeatTheTile_AndCropTheLastOne()
        {
            // 안쪽 폭 = 130 - 15 - 15 = 100 → 48px 타일 2개 + 4px 남음(원본 2px로 잘라 그림)
            List<SlicePiece> pieces = Build(new PixelRect(0, 0, 130, 81), 3);
            List<SlicePiece> top = pieces.Where(p => p.Source.Y == 42 && p.Source.X == 16).ToList();
            Assert.That(top.Select(p => p.Dest.X), Is.EqualTo(new[] { 15, 63, 111 }));
            Assert.That(top.Select(p => p.Dest.W), Is.EqualTo(new[] { 48, 48, 4 }));
            Assert.That(top[2].Source.W, Is.EqualTo(2));
            // 안쪽 높이 = 81 - 18 - 15 = 48 → 가운데 줄은 한 번
            Assert.That(pieces.Count(p => p.Source.Y == 48 && p.Source.X == 16), Is.EqualTo(3));
        }

        [Test]
        public void Build_AppendsIntoTheGivenList_WithoutClearing()
        {
            var pieces = new List<SlicePiece> { default };
            NineSlice.Build(new PixelRect(0, 0, 100, 100), Common, 2, pieces);
            Assert.That(pieces.Count, Is.GreaterThan(9));
            Assert.That(pieces[0], Is.EqualTo(default(SlicePiece)));
        }
    }

    public sealed class ButtonTests
    {
        private static (UiTree tree, UiButton button, DrawList list) Make()
        {
            var tree = new UiTree();
            var button = new UiButton { Id = "ok", Text = "확인", FontSize = 20 };
            tree.Root.Add(button).Place(0, 0, 100, 100, 200, 60);
            tree.Layout(1920, 1080);
            return (tree, button, new DrawList());
        }

        private static DrawCommand Panel(DrawList list) => list.Commands.First(c => c.Kind == DrawKind.Panel);
        private static DrawCommand Text(DrawList list) => list.Commands.First(c => c.Kind == DrawKind.Text);

        [Test]
        public void Emit_DrawsAButtonPanel_AndCentersTheLabel()
        {
            (UiTree tree, UiButton button, DrawList list) = Make();
            tree.Emit(list, new FakeMeasurer());
            Assert.That(Panel(list).Skin, Is.EqualTo(UiSkin.Button));
            Assert.That(Panel(list).Rect, Is.EqualTo(button.Rect));
            DrawCommand text = Text(list);
            Assert.That(text.Text, Is.EqualTo("확인"));
            Assert.That(text.Rect.X, Is.EqualTo(100 + (200 - 20) / 2f)); // 글자 2개 x 10
            Assert.That(text.Rect.Y, Is.EqualTo(100 + (60 - 20) / 2f));
        }

        [Test]
        public void PressedHoveredDisabled_EachLookDifferent()
        {
            (UiTree tree, UiButton button, DrawList list) = Make();
            var m = new FakeMeasurer();
            tree.Emit(list, m);
            UiColor normal = Panel(list).Color;
            float normalY = Text(list).Rect.Y;

            tree.UpdatePointer(150, 120, false);
            tree.Emit(list, m);
            UiColor hovered = Panel(list).Color;

            tree.UpdatePointer(150, 120, true);
            tree.Emit(list, m);
            UiColor pressed = Panel(list).Color;
            Assert.That(Text(list).Rect.Y, Is.GreaterThan(normalY), "눌리면 글자가 내려간다");
            tree.UpdatePointer(150, 120, false);
            tree.UpdatePointer(900, 900, false);

            button.Enabled = false;
            tree.Emit(list, m);
            UiColor disabled = Panel(list).Color;
            UiColor disabledInk = Text(list).Color;

            Assert.That(new[] { normal, hovered, pressed, disabled }.Distinct().Count(), Is.EqualTo(4));
            Assert.That(disabledInk, Is.EqualTo(UiTheme.InkDisabled), "비활성 글자 색 이름(테마가 정한다)");
        }

        [Test]
        public void Focused_DrawsAnOutline()
        {
            (UiTree tree, UiButton button, DrawList list) = Make();
            tree.Emit(list, new FakeMeasurer());
            int before = list.Commands.Count(c => c.Kind == DrawKind.Fill);
            tree.SetFocus(button);
            tree.Emit(list, new FakeMeasurer());
            Assert.That(list.Commands.Count(c => c.Kind == DrawKind.Fill), Is.EqualTo(before + 4));
        }

        [Test]
        public void Emit_SkipsInvisibleSubtrees()
        {
            (UiTree tree, UiButton button, DrawList list) = Make();
            button.Visible = false;
            tree.Emit(list, new FakeMeasurer());
            Assert.That(list.Commands, Is.Empty);
        }
    }

    public sealed class TextFieldTests
    {
        private UiTree _tree;
        private UiTextField _field;
        private UiButton _other;

        [SetUp]
        public void SetUp()
        {
            _tree = new UiTree();
            _field = new UiTextField { Id = "name", MaxLength = 4 };
            _other = new UiButton { Id = "other", Text = "x" };
            _tree.Root.Add(_field).Place(0, 0, 0, 0, 300, 60);
            _tree.Root.Add(_other).Place(0, 0, 0, 100, 300, 60);
            _tree.Layout(1920, 1080);
        }

        [Test]
        public void Text_GoesOnlyToTheFocusedField()
        {
            _tree.TextInput("가");
            Assert.That(_field.Text, Is.EqualTo(""));
            _tree.SetFocus(_other);
            _tree.TextInput("가");
            Assert.That(_field.Text, Is.EqualTo(""));
            _tree.SetFocus(_field);
            _tree.TextInput("하늘");
            Assert.That(_field.Text, Is.EqualTo("하늘"));
        }

        [Test]
        public void Text_StopsAtMaxLength_AndDropsControlCharacters()
        {
            _tree.SetFocus(_field);
            _tree.TextInput("가\t나\r다라마바");
            Assert.That(_field.Text, Is.EqualTo("가나다라"));
        }

        [Test]
        public void Backspace_RemovesTheLastCharacter_AndStopsAtEmpty()
        {
            _tree.SetFocus(_field);
            _tree.TextInput("하늘");
            _tree.TextBackspace();
            Assert.That(_field.Text, Is.EqualTo("하"));
            _tree.TextBackspace();
            _tree.TextBackspace();
            Assert.That(_field.Text, Is.EqualTo(""));
        }

        [Test]
        public void Backspace_RemovesASurrogatePairAsOne()
        {
            _tree.SetFocus(_field);
            _tree.TextInput("가😀");
            _tree.TextBackspace();
            Assert.That(_field.Text, Is.EqualTo("가"));
        }

        [Test]
        public void Composition_IsShownUntilCommitted_AndBackspaceLeavesItToTheIme()
        {
            _tree.SetFocus(_field);
            _tree.TextInput("하");
            _tree.TextComposition("느");
            Assert.That(_field.Composition, Is.EqualTo("느"));
            _tree.TextBackspace();
            Assert.That(_field.Text, Is.EqualTo("하"), "조합 중 백스페이스는 입력기가 처리한다");

            var list = new DrawList();
            _tree.Emit(list, new FakeMeasurer());
            Assert.That(list.Commands.Where(c => c.Kind == DrawKind.Text).Select(c => c.Text), Is.EqualTo(new[] { "하", "느", "x" }));

            _tree.TextInput("늘");
            Assert.That(_field.Composition, Is.EqualTo(""));
            Assert.That(_field.Text, Is.EqualTo("하늘"));
        }

        [Test]
        public void LosingFocus_DropsTheComposition()
        {
            _tree.SetFocus(_field);
            _tree.TextComposition("ㅎ");
            _tree.SetFocus(null);
            Assert.That(_field.Composition, Is.EqualTo(""));
        }

        [Test]
        public void Submit_RaisesSubmittedWithTheText()
        {
            string submitted = null;
            _field.Submitted += text => submitted = text;
            _tree.SetFocus(_field);
            _tree.TextInput("하늘");
            _tree.Submit();
            Assert.That(submitted, Is.EqualTo("하늘"));
        }

        [Test]
        public void LeftAndRight_StayInTheField_UpAndDownLeave()
        {
            _tree.SetFocus(_field);
            _tree.Navigate(UiNav.Right);
            Assert.That(_tree.Focused, Is.SameAs(_field));
            _tree.Navigate(UiNav.Down);
            Assert.That(_tree.Focused, Is.SameAs(_other));
        }

        [Test]
        public void EmptyUnfocusedField_ShowsThePlaceholder()
        {
            _field.Placeholder = "이름";
            var list = new DrawList();
            _tree.Emit(list, new FakeMeasurer());
            DrawCommand text = list.Commands.First(c => c.Kind == DrawKind.Text);
            Assert.That(text.Text, Is.EqualTo("이름"));
            Assert.That(text.Color, Is.EqualTo(UiTheme.InkMuted));
        }
    }

    public sealed class ScrollListTests
    {
        private UiTree _tree;
        private UiScrollList _list;
        private List<int> _clicked;
        private int _textCalls;

        // 목록: (100,100) 400x320, 안쪽 여백 10 → 보이는 높이 300, 줄 높이 50 → 6줄
        private void Make(int rows)
        {
            _tree = new UiTree();
            _clicked = new List<int>();
            _textCalls = 0;
            _list = new UiScrollList { Id = "list", RowCount = rows, RowHeight = 50, Padding = 10 };
            _list.RowText = i => { _textCalls++; return "줄 " + (i + 1); };
            _list.RowClicked += _clicked.Add;
            _tree.Root.Add(_list).Place(0, 0, 100, 100, 400, 320);
            _tree.Layout(1920, 1080);
        }

        private void Click(float x, float y)
        {
            _tree.UpdatePointer(x, y, false);
            _tree.UpdatePointer(x, y, true);
            _tree.UpdatePointer(x, y, false);
        }

        [Test]
        public void MaxScroll_IsContentMinusView()
        {
            Make(200);
            Assert.That(_list.MaxScroll, Is.EqualTo(200 * 50 - 300));
            Make(3);
            Assert.That(_list.MaxScroll, Is.EqualTo(0));
        }

        [Test]
        public void Wheel_ScrollsThreeRowsPerNotch_AndClamps()
        {
            Make(200);
            _tree.UpdatePointer(200, 200, false, -1f);
            Assert.That(_list.ScrollOffset, Is.EqualTo(150));
            _tree.UpdatePointer(200, 200, false, 5f);
            Assert.That(_list.ScrollOffset, Is.EqualTo(0));
            _tree.UpdatePointer(200, 200, false, -1000f);
            Assert.That(_list.ScrollOffset, Is.EqualTo(_list.MaxScroll));
        }

        [Test]
        public void Wheel_OutsideTheList_DoesNothing()
        {
            Make(200);
            _tree.UpdatePointer(900, 900, false, -1f);
            Assert.That(_list.ScrollOffset, Is.EqualTo(0));
        }

        [Test]
        public void Click_ReportsTheRowUnderThePointer_AfterScrolling()
        {
            Make(200);
            Click(200, 110 + 50 * 2 + 5);
            Assert.That(_clicked, Is.EqualTo(new[] { 2 }));
            Assert.That(_list.SelectedIndex, Is.EqualTo(2));

            _list.ScrollOffset = 149 * 50;
            Click(200, 115);
            Assert.That(_clicked.Last(), Is.EqualTo(149));
        }

        [Test]
        public void Click_BelowTheLastRow_ReportsNothing()
        {
            Make(2);
            Click(200, 110 + 50 * 3);
            Assert.That(_clicked, Is.Empty);
        }

        [Test]
        public void Drag_ScrollsWithThePointer_AndDoesNotClick()
        {
            Make(200);
            _tree.UpdatePointer(200, 300, false);
            _tree.UpdatePointer(200, 300, true);
            _tree.UpdatePointer(200, 250, true);
            _tree.UpdatePointer(200, 180, true);
            Assert.That(_list.ScrollOffset, Is.EqualTo(120));
            _tree.UpdatePointer(200, 180, false);
            Assert.That(_clicked, Is.Empty);
            Assert.That(_list.ScrollOffset, Is.EqualTo(120));

            Click(200, 115);
            Assert.That(_clicked.Count, Is.EqualTo(1), "끌기가 끝난 뒤에는 다시 클릭된다");
        }

        [Test]
        public void SmallPointerJitter_IsStillAClick()
        {
            Make(200);
            _tree.UpdatePointer(200, 115, true);
            _tree.UpdatePointer(202, 118, true);
            _tree.UpdatePointer(202, 118, false);
            Assert.That(_clicked, Is.EqualTo(new[] { 0 }));
            Assert.That(_list.ScrollOffset, Is.EqualTo(0));
        }

        [Test]
        public void Emit_AsksOnlyForVisibleRows_EvenWithFiveHundred()
        {
            Make(500);
            var list = new DrawList();
            var m = new FakeMeasurer();
            _tree.Emit(list, m);
            Assert.That(_textCalls, Is.EqualTo(6));
            Assert.That(list.Commands.Count(c => c.Kind == DrawKind.Text), Is.EqualTo(6));

            _textCalls = 0;
            _list.ScrollOffset = 10025; // 줄 사이에 걸치면 한 줄 더 보인다
            _tree.Emit(list, m);
            Assert.That(_textCalls, Is.EqualTo(7));
            Assert.That(list.Commands.First(c => c.Kind == DrawKind.Text).Text, Is.EqualTo("줄 201"));
        }

        [Test]
        public void Emit_ClipsRowsToTheView()
        {
            Make(500);
            var list = new DrawList();
            _tree.Emit(list, new FakeMeasurer());
            DrawCommand clip = list.Commands.Single(c => c.Kind == DrawKind.PushClip);
            Assert.That(clip.Rect, Is.EqualTo(new UiRect(110, 110, 380, 300)));
            int push = list.Commands.FindIndex(c => c.Kind == DrawKind.PushClip);
            int pop = list.Commands.FindIndex(c => c.Kind == DrawKind.PopClip);
            Assert.That(list.Commands.FindIndex(c => c.Kind == DrawKind.Text), Is.InRange(push, pop));
        }

        [Test]
        public void TryGetRowRect_GivesTheVisiblePartOnly()
        {
            Make(200);
            _list.ScrollOffset = 25;
            Assert.That(_list.TryGetRowRect(0, out UiRect first), Is.True);
            Assert.That(first.Y, Is.EqualTo(110));
            Assert.That(first.H, Is.EqualTo(25));
            Assert.That(_list.TryGetRowRect(3, out UiRect third), Is.True);
            Assert.That(third.Y, Is.EqualTo(110 + 150 - 25));
            Assert.That(third.H, Is.EqualTo(50));
            Assert.That(_list.TryGetRowRect(100, out _), Is.False);
        }

        [Test]
        public void UpAndDown_MoveTheSelection_AndScrollItIntoView()
        {
            Make(200);
            _tree.SetFocus(_list);
            _tree.Navigate(UiNav.Down);
            Assert.That(_list.SelectedIndex, Is.EqualTo(0));
            for (int i = 0; i < 6; i++) _tree.Navigate(UiNav.Down);
            Assert.That(_list.SelectedIndex, Is.EqualTo(6));
            Assert.That(_list.ScrollOffset, Is.EqualTo(7 * 50 - 300));
            for (int i = 0; i < 6; i++) _tree.Navigate(UiNav.Up);
            Assert.That(_list.SelectedIndex, Is.EqualTo(0));
            Assert.That(_list.ScrollOffset, Is.EqualTo(0));
        }

        [Test]
        public void Submit_ActivatesTheSelectedRow()
        {
            Make(200);
            _tree.SetFocus(_list);
            _tree.Submit();
            Assert.That(_clicked, Is.Empty);
            _tree.Navigate(UiNav.Down);
            _tree.Navigate(UiNav.Down);
            _tree.Submit();
            Assert.That(_clicked, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void UpAtTheTop_LeavesTheList()
        {
            Make(200);
            var above = new UiButton { Id = "above", Text = "x" };
            _tree.Root.Add(above).Place(0, 0, 100, 0, 400, 60);
            _tree.Layout(1920, 1080);
            _tree.SetFocus(_list);
            _tree.Navigate(UiNav.Down);
            _tree.Navigate(UiNav.Up);
            Assert.That(_tree.Focused, Is.SameAs(above));
        }

        [Test]
        public void ShrinkingTheList_ClampsScrollAndSelection()
        {
            Make(200);
            _list.ScrollOffset = 5000;
            _list.SelectedIndex = 150;
            _list.RowCount = 10;
            Assert.That(_list.ScrollOffset, Is.EqualTo(10 * 50 - 300));
            Assert.That(_list.SelectedIndex, Is.EqualTo(-1));
        }
    }
}
