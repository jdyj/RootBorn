using System.Collections.Generic;
using NUnit.Framework;

namespace Rootborn.Ui.Tests
{
    /// <summary>글자 하나의 폭을 글자 크기의 절반으로 치는 측정기.</summary>
    public sealed class FakeMeasurer : ITextMeasurer
    {
        public float Width(string text, float fontSize) => (text?.Length ?? 0) * fontSize * 0.5f;
        public float LineHeight(float fontSize) => fontSize;
    }

    public sealed class LayoutTests
    {
        [Test]
        public void Stretch_FillsParentMinusInsets()
        {
            var tree = new UiTree();
            UiNode node = tree.Root.Add(new UiNode()).Stretch(60, 40, 20, 10);
            tree.Layout(1920, 1080);
            Assert.That(node.Rect, Is.EqualTo(new UiRect(60, 40, 1840, 1030)));
        }

        [Test]
        public void Place_AnchorsAtAPointAndOffsetsDownAndRight()
        {
            var tree = new UiTree();
            UiNode node = tree.Root.Add(new UiNode()).Place(0.5f, 1f, -100, -80, 200, 60);
            tree.Layout(1920, 1080);
            Assert.That(node.Rect, Is.EqualTo(new UiRect(860, 1000, 200, 60)));
        }

        [Test]
        public void Children_AreLaidOutInsideTheirParent()
        {
            var tree = new UiTree();
            UiNode parent = tree.Root.Add(new UiNode()).Place(0, 0, 100, 200, 400, 300);
            UiNode child = parent.Add(new UiNode()).Place(1, 1, -50, -40, 30, 20);
            tree.Layout(1920, 1080);
            Assert.That(child.Rect, Is.EqualTo(new UiRect(450, 460, 30, 20)));
        }

        [Test]
        public void Find_ReturnsNodeById()
        {
            var tree = new UiTree();
            UiNode parent = tree.Root.Add(new UiNode { Id = "a" });
            UiNode child = parent.Add(new UiNode { Id = "b" });
            Assert.That(tree.Root.Find("b"), Is.SameAs(child));
            Assert.That(tree.Root.Find("none"), Is.Null);
        }
    }

    public sealed class ScreenFitTests
    {
        [Test]
        public void FullHd_IsOneToOne_WithTileScaleThree()
        {
            ScreenFit fit = ScreenFit.For(1920, 1080);
            Assert.That(fit.Scale, Is.EqualTo(1f).Within(1e-5));
            Assert.That(fit.LogicalWidth, Is.EqualTo(1920f).Within(1e-3));
            Assert.That(fit.LogicalHeight, Is.EqualTo(1080f).Within(1e-3));
            Assert.That(fit.TileScale, Is.EqualTo(3));
        }

        [Test]
        public void Hd_KeepsLogicalSize_AndUsesIntegerTileScale()
        {
            ScreenFit fit = ScreenFit.For(1280, 720);
            Assert.That(fit.Scale, Is.EqualTo(2f / 3f).Within(1e-5));
            Assert.That(fit.LogicalWidth, Is.EqualTo(1920f).Within(1e-2));
            Assert.That(fit.TileScale, Is.EqualTo(2));
        }

        [Test]
        public void OtherAspect_KeepsWholeReferenceAreaOnScreen_LikeCanvasScalerExpand()
        {
            ScreenFit fit = ScreenFit.For(2560, 1080);
            Assert.That(fit.Scale, Is.EqualTo(1f).Within(1e-5), "세로가 기준과 같으니 배율 1, 남는 가로는 논리 공간이 된다");
            Assert.That(fit.LogicalHeight, Is.GreaterThanOrEqualTo(1080f - 1e-2));
            ScreenFit tall = ScreenFit.For(1920, 1200);
            Assert.That(tall.LogicalWidth, Is.GreaterThanOrEqualTo(1920f - 1e-2));
            Assert.That(tall.LogicalHeight, Is.EqualTo(1200f).Within(1e-2));
            Assert.That(fit.LogicalWidth * fit.Scale, Is.EqualTo(2560f).Within(1e-2));
            Assert.That(fit.LogicalHeight * fit.Scale, Is.EqualTo(1080f).Within(1e-2));
        }

        [Test]
        public void TileScale_NeverDropsBelowOne()
        {
            Assert.That(ScreenFit.For(320, 180).TileScale, Is.EqualTo(1));
        }

        [Test]
        public void ToPixels_RoundsEdges_SoNeighboursShareAnEdge()
        {
            ScreenFit fit = ScreenFit.For(1280, 720);
            PixelRect a = fit.ToPixels(new UiRect(10, 0, 100, 50));
            PixelRect b = fit.ToPixels(new UiRect(110, 0, 100, 50));
            Assert.That(a.X + a.W, Is.EqualTo(b.X));
        }
    }

    public sealed class PointerTests
    {
        private UiTree _tree;
        private UiNode _back, _front, _side;
        private List<string> _clicks;

        [SetUp]
        public void SetUp()
        {
            _tree = new UiTree();
            _clicks = new List<string>();
            _back = _tree.Root.Add(new UiNode { Id = "back", Interactive = true, Focusable = true }).Place(0, 0, 0, 0, 200, 200);
            _front = _tree.Root.Add(new UiNode { Id = "front", Interactive = true }).Place(0, 0, 100, 100, 200, 200);
            _side = _tree.Root.Add(new UiNode { Id = "side", Interactive = true }).Place(0, 0, 500, 0, 100, 100);
            _tree.Clicked += node => _clicks.Add(node.Id);
            _tree.Layout(1920, 1080);
        }

        private void Click(float x, float y)
        {
            _tree.UpdatePointer(x, y, false);
            _tree.UpdatePointer(x, y, true);
            _tree.UpdatePointer(x, y, false);
        }

        [Test]
        public void HitTest_ReturnsTopmostElement()
        {
            Assert.That(_tree.HitTest(150, 150), Is.SameAs(_front));
            Assert.That(_tree.HitTest(50, 50), Is.SameAs(_back));
            Assert.That(_tree.HitTest(400, 400), Is.Null);
        }

        [Test]
        public void HitTest_SkipsInvisibleElements()
        {
            _front.Visible = false;
            Assert.That(_tree.HitTest(150, 150), Is.SameAs(_back));
        }

        [Test]
        public void Click_NeedsPressAndReleaseOnTheSameElement()
        {
            _tree.UpdatePointer(50, 50, true);
            _tree.UpdatePointer(550, 50, true);
            _tree.UpdatePointer(550, 50, false);
            Assert.That(_clicks, Is.Empty);

            Click(550, 50);
            Assert.That(_clicks, Is.EqualTo(new[] { "side" }));
        }

        [Test]
        public void Click_RaisesTheNodesOwnEvent()
        {
            int count = 0;
            _side.Clicked += () => count++;
            Click(550, 50);
            Assert.That(count, Is.EqualTo(1));
        }

        [Test]
        public void HoverAndPressedStates_FollowThePointer()
        {
            _tree.UpdatePointer(550, 50, false);
            Assert.That(_side.Hovered, Is.True);
            Assert.That(_side.Pressed, Is.False);

            _tree.UpdatePointer(550, 50, true);
            Assert.That(_side.Pressed, Is.True);

            _tree.UpdatePointer(50, 50, true);
            Assert.That(_side.Pressed, Is.False, "누른 채 밖으로 나가면 눌린 모습이 풀린다");
            Assert.That(_side.Hovered, Is.False);
            Assert.That(_back.Hovered, Is.True);
            Assert.That(_back.Pressed, Is.False, "다른 요소에서 시작한 누름은 옮겨 가지 않는다");

            _tree.UpdatePointer(50, 50, false);
            Assert.That(_clicks, Is.Empty);
        }

        [Test]
        public void DisabledElement_IsNotClicked_NotHovered_AndStillBlocksWhatIsBehind()
        {
            _front.Enabled = false;
            Click(150, 150);
            Assert.That(_clicks, Is.Empty);
            Assert.That(_front.Hovered, Is.False);
            Assert.That(_back.Hovered, Is.False);
        }

        [Test]
        public void DisabledParent_DisablesChildren()
        {
            UiNode child = _side.Add(new UiNode { Id = "child", Interactive = true }).Stretch(0, 0, 0, 0);
            _tree.Layout(1920, 1080);
            _side.Enabled = false;
            Click(550, 50);
            Assert.That(_clicks, Is.Empty);
            Assert.That(child.IsActive, Is.False);
        }

        [Test]
        public void PressingAFocusableElement_FocusesIt_AndPressingElsewhereClearsFocus()
        {
            _tree.UpdatePointer(50, 50, true);
            Assert.That(_tree.Focused, Is.SameAs(_back));
            Assert.That(_back.Focused, Is.True);
            _tree.UpdatePointer(50, 50, false);

            _tree.UpdatePointer(550, 50, true);
            Assert.That(_tree.Focused, Is.Null);
            Assert.That(_back.Focused, Is.False);
        }

        [Test]
        public void Wheel_GoesToTheNearestAncestorThatTakesIt()
        {
            var scroller = new WheelTaker { Interactive = true };
            _tree.Root.Add(scroller).Place(0, 0, 800, 0, 300, 300);
            UiNode inner = scroller.Add(new UiNode { Interactive = true }).Stretch(10, 10, 10, 10);
            _tree.Layout(1920, 1080);
            _tree.UpdatePointer(900, 100, false, 2f);
            Assert.That(scroller.Total, Is.EqualTo(2f));
            Assert.That(_tree.HitTest(900, 100), Is.SameAs(inner));
        }

        private sealed class WheelTaker : UiNode
        {
            public float Total;
            public override bool OnWheel(float notches) { Total += notches; return true; }
        }
    }

    public sealed class FocusTests
    {
        private UiTree _tree;
        private UiNode _a, _b, _c, _d;
        private List<string> _clicks;

        // a b
        // c d
        [SetUp]
        public void SetUp()
        {
            _tree = new UiTree();
            _clicks = new List<string>();
            _a = Add("a", 0, 0);
            _b = Add("b", 200, 0);
            _c = Add("c", 0, 200);
            _d = Add("d", 200, 200);
            _tree.Clicked += node => _clicks.Add(node.Id);
            _tree.Layout(1920, 1080);
        }

        private UiNode Add(string id, float x, float y) =>
            _tree.Root.Add(new UiNode { Id = id, Interactive = true, Focusable = true }).Place(0, 0, x, y, 100, 100);

        [Test]
        public void Tab_WalksInTreeOrder_AndWraps()
        {
            _tree.Navigate(UiNav.Next);
            Assert.That(_tree.Focused, Is.SameAs(_a));
            _tree.Navigate(UiNav.Next);
            _tree.Navigate(UiNav.Next);
            _tree.Navigate(UiNav.Next);
            Assert.That(_tree.Focused, Is.SameAs(_d));
            _tree.Navigate(UiNav.Next);
            Assert.That(_tree.Focused, Is.SameAs(_a));
            _tree.Navigate(UiNav.Previous);
            Assert.That(_tree.Focused, Is.SameAs(_d));
        }

        [Test]
        public void Arrows_MoveToTheNearestElementInThatDirection()
        {
            _tree.SetFocus(_a);
            _tree.Navigate(UiNav.Right);
            Assert.That(_tree.Focused, Is.SameAs(_b));
            _tree.Navigate(UiNav.Down);
            Assert.That(_tree.Focused, Is.SameAs(_d));
            _tree.Navigate(UiNav.Left);
            Assert.That(_tree.Focused, Is.SameAs(_c));
            _tree.Navigate(UiNav.Up);
            Assert.That(_tree.Focused, Is.SameAs(_a));
        }

        [Test]
        public void Arrow_WithNothingInThatDirection_KeepsFocus()
        {
            _tree.SetFocus(_a);
            _tree.Navigate(UiNav.Left);
            Assert.That(_tree.Focused, Is.SameAs(_a));
        }

        [Test]
        public void Arrow_WithNoFocus_FocusesTheFirstElement()
        {
            _tree.Navigate(UiNav.Down);
            Assert.That(_tree.Focused, Is.SameAs(_a));
        }

        [Test]
        public void Navigation_SkipsDisabledAndInvisibleElements()
        {
            _b.Enabled = false;
            _c.Visible = false;
            _tree.SetFocus(_a);
            _tree.Navigate(UiNav.Next);
            Assert.That(_tree.Focused, Is.SameAs(_d));
            _tree.SetFocus(_a);
            _tree.Navigate(UiNav.Right);
            Assert.That(_tree.Focused, Is.SameAs(_d), "같은 줄의 b는 비활성이라 건너뛰고 대각선의 d로 간다");
        }

        [Test]
        public void Arrow_PrefersAlignedElementOverCloserDiagonalOne()
        {
            UiNode far = Add("far", 600, 0);
            _tree.Layout(1920, 1080);
            _b.Visible = false;
            _tree.SetFocus(_a);
            _tree.Navigate(UiNav.Right);
            Assert.That(_tree.Focused, Is.SameAs(far), "같은 줄의 먼 요소가 대각선의 가까운 요소(d)보다 먼저다");
        }

        [Test]
        public void Submit_ClicksTheFocusedElement()
        {
            _tree.Submit();
            Assert.That(_clicks, Is.Empty);
            _tree.SetFocus(_c);
            _tree.Submit();
            Assert.That(_clicks, Is.EqualTo(new[] { "c" }));
        }

        [Test]
        public void FocusChanged_IsRaisedOncePerChange()
        {
            var seen = new List<UiNode>();
            _tree.FocusChanged += seen.Add;
            _tree.SetFocus(_a);
            _tree.SetFocus(_a);
            _tree.SetFocus(null);
            Assert.That(seen, Is.EqualTo(new[] { _a, null }));
        }

        [Test]
        public void SetFocus_IgnoresElementsThatCannotTakeFocus()
        {
            _b.Enabled = false;
            _tree.SetFocus(_b);
            Assert.That(_tree.Focused, Is.Null);
        }
    }
}
