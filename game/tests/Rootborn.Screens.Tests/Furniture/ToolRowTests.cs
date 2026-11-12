using System.Linq;
using NUnit.Framework;
using Rootborn.Screens.Furniture;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests.Furniture
{
    /// <summary>
    /// 가구 배치의 위·아래 도구 줄: 줄 패널이 든 단추들의 폭에 맞춰 줄고(단추 영역 + 테마 여백), 단추가 하나도 보이지 않으면 패널도 숨는다.
    /// 단추는 화면에서 놓인 자리를 그대로 지킨다(가운데/왼쪽 기준 유지). 창 없이 — 자동 테스트.
    /// </summary>
    public sealed class ToolRowTests
    {
        private static TestSession OpenPlacement()
        {
            var t = new TestSession();
            t.Stack.EnterMap("House", new[] { MapFeatures.FurniturePlacement });
            t.Stack.OpenHud();
            t.Stack.Open(ScreenCatalog.FurniturePlacement);
            t.Stack.Layout(1920, 1080);
            return t;
        }

        private static UiRect Span(params UiNode[] nodes)
        {
            float x0 = nodes.Min(n => n.Rect.X), x1 = nodes.Max(n => n.Rect.Right);
            return new UiRect(x0, nodes.Min(n => n.Rect.Y), x1 - x0, nodes.Max(n => n.Rect.Bottom) - nodes.Min(n => n.Rect.Y));
        }

        [TestCase(FurniturePlacementScreen.ToolbarId, new[] { FurniturePlacementScreen.DeleteId, FurniturePlacementScreen.RotateId })]
        [TestCase(FurniturePlacementScreen.BottomBarId, new[] { FurniturePlacementScreen.MoveId })]
        public void ToolBar_IsAsWideAsItsButtons_PlusThePanelPadding(string barId, string[] buttonIds)
        {
            using TestSession t = OpenPlacement();
            UiRect bar = t.Node(barId).Rect;
            UiRect buttons = Span(buttonIds.Select(t.Node).ToArray());
            UiInsets pad = t.Services.PaddingOf(UiSkin.Panel);
            Assert.That(bar.X, Is.EqualTo(buttons.X - pad.Left).Within(0.5f), "왼쪽: 단추 + 테마 여백");
            Assert.That(bar.Right, Is.EqualTo(buttons.Right + pad.Right).Within(0.5f), "오른쪽: 단추 + 테마 여백");
            Assert.That(buttons.Y >= bar.Y && buttons.Bottom <= bar.Bottom, Is.True, "단추가 줄 안에");
        }

        [Test]
        public void ToolBar_KeepsTheButtonsWhereTheyWere()
        {
            using TestSession t = OpenPlacement();
            // 1920x1080 가운데 기준(960, 540): 단추 가운데 x는 Metrics의 자리 그대로(줄이 줄어도 단추는 움직이지 않는다).
            Assert.That(t.Node(FurniturePlacementScreen.DeleteId).Rect.CenterX, Is.EqualTo(960f + FurniturePlacementScreen.Metrics.ToolbarX + FurniturePlacementScreen.Metrics.DeleteX).Within(0.5f));
            Assert.That(t.Node(FurniturePlacementScreen.RotateId).Rect.CenterX, Is.EqualTo(960f + FurniturePlacementScreen.Metrics.ToolbarX + FurniturePlacementScreen.Metrics.RotateX).Within(0.5f));
            Assert.That(t.Node(FurniturePlacementScreen.MoveId).Rect.CenterX, Is.EqualTo(960f + FurniturePlacementScreen.Metrics.BottomX + FurniturePlacementScreen.Metrics.MoveX).Within(0.5f));
        }

        [Test]
        public void ToolRow_WithNoVisibleButton_HidesThePanel()
        {
            var panel = new UiPanel();
            var a = panel.Add(new UiButton { Visible = false });
            var row = new ToolRow(panel, centerY: 0f, height: 86f, padding: new UiInsets(10, 0, 10, 0));
            row.Add(a, centerX: 100f, w: 150f, h: 70f);
            row.Fit();
            Assert.That(panel.Visible, Is.False, "단추가 없으면 빈 상자를 보이지 않는다");
            a.Visible = true;
            row.Fit();
            Assert.That(panel.Visible, Is.True);
        }
    }
}
