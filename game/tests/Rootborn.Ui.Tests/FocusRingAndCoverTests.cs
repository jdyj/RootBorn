using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Rootborn.Ui.Tests
{
    /// <summary>
    /// 포커스 테두리는 키보드로 옮긴 포커스에만 그린다(포인터로 누른 줄에 선택 표시와 목록 테두리가 둘 다 그려지지 않게).
    /// 덮인 요소(Covered)는 그리지도 누르지도 않는다. 가장 바깥 겉면만 모은다(UiSurfaces).
    /// </summary>
    public sealed class FocusRingAndCoverTests
    {
        private static readonly UiColor Accent = UiTheme.Accent;

        private static int AccentOutlines(UiTree tree)
        {
            var list = new DrawList();
            tree.Emit(list, new FakeMeasurer());
            // 테두리 하나는 칠하기 4번이다.
            return list.Commands.Count(c => c.Kind == DrawKind.Fill && c.Color == Accent) / 4;
        }

        private static void Click(UiTree tree, float x, float y)
        {
            tree.UpdatePointer(x, y, false);
            tree.UpdatePointer(x, y, true);
            tree.UpdatePointer(x, y, false);
        }

        [Test]
        public void PointerFocus_DrawsNoRing_KeyboardFocus_DrawsOne()
        {
            var tree = new UiTree();
            var a = tree.Root.Add(new UiButton { Text = "a" });
            a.Place(0, 0, 0, 0, 100, 40);
            var b = tree.Root.Add(new UiButton { Text = "b" });
            b.Place(0, 0, 0, 60, 100, 40);
            tree.Layout(400, 300);

            Click(tree, 50, 20);
            Assert.That(tree.Focused, Is.SameAs(a), "누르면 포커스는 옮긴다(Enter로 다시 누를 수 있게)");
            Assert.That(a.FocusRing, Is.False);
            Assert.That(AccentOutlines(tree), Is.Zero, "포인터로 누른 단추에는 포커스 테두리가 없다");

            tree.Navigate(UiNav.Down);
            Assert.That(tree.Focused, Is.SameAs(b));
            Assert.That(b.FocusRing, Is.True);
            Assert.That(AccentOutlines(tree), Is.EqualTo(1), "키보드로 옮기면 테두리 하나");

            Click(tree, 50, 80);
            Assert.That(b.FocusRing, Is.False, "같은 요소를 포인터로 누르면 테두리를 지운다");
        }

        [Test]
        public void CardGrid_WithASelectedCell_DoesNotOutlineTheWholeGrid()
        {
            var tree = new UiTree();
            var grid = tree.Root.Add(new UiCardGrid { CellWidth = 100, CellHeight = 40, ItemCount = 3 });
            grid.Place(0, 0, 0, 0, 300, 200);
            grid.DrawCell = (index, rect, state, list, text) =>
            {
                if (state.Selected && state.Focused) list.Outline(rect, UiTheme.FocusOutline, UiTheme.Accent);
            };
            tree.Layout(400, 300);
            tree.SetFocus(grid);
            Assert.That(AccentOutlines(tree), Is.EqualTo(1), "고른 칸이 없으면 목록 테두리");
            grid.SelectedIndex = 1;
            Assert.That(AccentOutlines(tree), Is.EqualTo(1), "고른 칸이 있으면 그 칸만 테두리");

            Click(tree, grid.CellRect(0).CenterX, grid.CellRect(0).CenterY);
            Assert.That(AccentOutlines(tree), Is.Zero, "포인터로 누르면 테두리가 없다");
        }

        [Test]
        public void CoveredNode_IsNotDrawn_NotHit_AndNotFocusable()
        {
            var tree = new UiTree();
            var panel = tree.Root.Add(new UiPanel { Id = "p" });
            panel.Place(0, 0, 0, 0, 100, 100);
            var button = panel.Add(new UiButton { Id = "b", Text = "x" });
            button.Place(0, 0, 10, 10, 50, 30);
            tree.Layout(400, 300);

            panel.Covered = true;
            var list = new DrawList();
            tree.Emit(list, new FakeMeasurer());
            Assert.That(list.Commands, Is.Empty);
            Assert.That(tree.HitTest(20, 20), Is.Null);
            Assert.That(button.IsActive, Is.False);
            tree.Navigate(UiNav.Next);
            Assert.That(tree.Focused, Is.Null);

            panel.Covered = false;
            Assert.That(tree.HitTest(20, 20), Is.SameAs(button));
        }

        [Test]
        public void Surfaces_AreTheOutermostVisibleBoxes()
        {
            var root = new UiNode { Id = "root" };
            var panel = root.Add(new UiPanel { Id = "panel" });
            panel.Place(0, 0, 0, 0, 100, 100);
            panel.Add(new UiButton { Id = "inner" }).Place(0, 0, 10, 10, 20, 20);
            var loose = root.Add(new UiNode { Id = "group" });
            loose.Add(new UiButton { Id = "loose" }).Place(0, 0, 200, 0, 50, 50);
            root.Add(new UiPanel { Id = "hidden", Visible = false }).Place(0, 0, 300, 0, 50, 50);
            root.Add(new UiLabel { Id = "label", Text = "글자" }).Place(0, 0, 400, 0, 50, 50);
            root.Layout(new UiRect(0, 0, 800, 600));

            var into = new List<UiNode>();
            UiSurfaces.Collect(root, into);
            Assert.That(into.Select(n => n.Id), Is.EqualTo(new[] { "panel", "loose" }));
            Assert.That(UiSurfaces.Overlap(new UiRect(0, 0, 10, 10), new UiRect(10, 0, 10, 10)), Is.False, "맞닿기만 하면 겹치지 않는다");
            Assert.That(UiSurfaces.Overlap(new UiRect(0, 0, 10, 10), new UiRect(9, 0, 10, 10)), Is.True);
        }
    }
}
