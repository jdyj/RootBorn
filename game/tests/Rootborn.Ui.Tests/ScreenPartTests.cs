using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Rootborn.Ui.Tests
{
    /// <summary>화면 폴더에서 올린 공용 부품과 글자 칸의 바뀜 알림.</summary>
    public sealed class ScreenPartTests
    {
        private static readonly FakeMeasurer M = new FakeMeasurer(); // 글자 하나 = 크기의 절반

        private static List<DrawCommand> Emit(UiNode node)
        {
            var list = new DrawList();
            node.Emit(list, M);
            return list.Commands;
        }

        private static T Placed<T>(T node, float w, float h) where T : UiNode
        {
            var tree = new UiTree();
            tree.Root.Add(node).Place(0, 0, 100, 50, w, h);
            tree.Layout(1920, 1080);
            return node;
        }

        [Test]
        public void TextField_TextChanged_FiresForTypingBackspaceAndSetText_NotForCompositionOrNoChange()
        {
            var field = new UiTextField { MaxLength = 10 };
            var seen = new List<string>();
            field.TextChanged += seen.Add;
            field.OnTextComposition("ㄱ");
            Assert.That(seen, Is.Empty, "조합 중인 글자는 아직 글자가 아니다");
            field.OnTextInput("가");
            field.OnTextInput("나");
            field.OnTextBackspace();
            field.SetText("가");
            field.SetText("다라");
            Assert.That(seen, Is.EqualTo(new[] { "가", "가나", "가", "다라" }), "같은 글자로 바꾸면 알리지 않는다");
            field.OnTextBackspace();
            field.OnTextBackspace();
            field.OnTextBackspace();
            Assert.That(seen.Last(), Is.EqualTo(string.Empty));
            Assert.That(seen.Count, Is.EqualTo(6), "빈 칸에서 지우기는 알리지 않는다");
        }

        [Test]
        public void CenteredText_WrapsAndCentersInItsRect()
        {
            var text = Placed(new UiCenteredText { Text = "가나다라", FontSize = 20, LineSpacing = 0 }, 30, 100);
            List<DrawCommand> drawn = Emit(text);
            Assert.That(drawn.Select(c => c.Text), Is.EqualTo(new[] { "가나다", "라" }));
            Assert.That(drawn[0].Rect.Y, Is.EqualTo(50 + (100 - 40) / 2f), "두 줄 높이 40을 세로 가운데로");
            Assert.That(drawn[1].Rect.X, Is.EqualTo(100 + (30 - 10) / 2f), "줄마다 가로 가운데");
        }

        [Test]
        public void ShrinkLabel_ShrinksToFit_ThenEllipsizes()
        {
            var label = Placed(new UiShrinkLabel { Text = "가나다라", MaxFontSize = 36, MinFontSize = 24 }, 100, 40);
            Assert.That(Emit(label).Single().FontSize, Is.EqualTo(36), "들어가면 가장 큰 크기");
            label.Text = "가나다라마바사";
            DrawCommand small = Emit(label).Single();
            Assert.That(small.FontSize, Is.EqualTo(28), "폭 100에 맞을 때까지 2씩 줄인다");
            label.Text = "가나다라마바사아자차카";
            DrawCommand cut = Emit(label).Single();
            Assert.That(cut.FontSize, Is.EqualTo(24));
            Assert.That(cut.Text, Does.EndWith("…"));
            Assert.That(M.Width(cut.Text, 24), Is.LessThanOrEqualTo(100));
        }

        [Test]
        public void ScrollText_WrapsScrollsWithTheWheel_AndGoesBackToTopWhenTheTextChanges()
        {
            var text = Placed(new UiScrollText { FontSize = 20, LineSpacing = 0, PaddingX = 0, PaddingTop = 0, PaddingBottom = 0, WheelStep = 10 }, 30, 40);
            text.Text = "가나다라마바사아자";
            Emit(text);
            Assert.That(text.Lines, Is.EqualTo(new[] { "가나다", "라마바", "사아자" }));
            Assert.That(text.MaxScroll, Is.EqualTo(20));
            Assert.That(text.OnWheel(-1), Is.True);
            Assert.That(text.ScrollOffset, Is.EqualTo(10));
            text.Text = "다른 글";
            Assert.That(text.ScrollOffset, Is.Zero);
        }

        [Test]
        public void SectionScrollView_StacksSections_AndClampsTheScroll()
        {
            var view = Placed(new UiSectionScrollView { FontSize = 20, LineSpacing = 0, SectionSpacing = 10, PaddingLeft = 0, PaddingTop = 0, PaddingRight = 0, PaddingBottom = 0 },
                30 + UiSectionScrollView.ScrollbarWidth, 40);
            view.SetSections(new[] { "가나다라", "", "마바" });
            Assert.That(view.Lines(M), Is.EqualTo(new[] { "가나다", "라", "마바" }), "빈 구역은 버린다");
            Assert.That(view.ContentHeight, Is.EqualTo(20 + 20 + 10 + 20));
            view.ScrollOffset = 1000;
            Assert.That(view.ScrollOffset, Is.EqualTo(view.MaxScroll));
        }

        [Test]
        public void SelectableRow_FillsOnlyWhenSelected_AndTakesFocus()
        {
            var row = Placed(new UiSelectableRow(), 100, 40);
            Assert.That(row.Focusable && row.Interactive, Is.True);
            Assert.That(Emit(row), Is.Empty);
            row.Selected = true;
            Assert.That(Emit(row).Single().Kind, Is.EqualTo(DrawKind.Fill));
        }

        [Test]
        public void FittedImage_KeepsTheAspect_CenteredInItsRect()
        {
            var image = Placed(new UiFittedImage(), 100, 50);
            image.Show("icon:x", 20, 20);
            Assert.That(image.FittedRect, Is.EqualTo(new UiRect(125, 50, 50, 50)));
            Assert.That(Emit(image).Single().Rect, Is.EqualTo(image.FittedRect));
            image.Clear();
            Assert.That(Emit(image), Is.Empty);
        }
    }
}
