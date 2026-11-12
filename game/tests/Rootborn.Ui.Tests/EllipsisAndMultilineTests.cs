using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Rootborn.Ui.Tests
{
    /// <summary>한 줄 글자의 말줄임(…)과 여러 줄 버튼. 측정기는 글자 하나 = 글자 크기의 절반이다(크기 20이면 10).</summary>
    public sealed class EllipsisTests
    {
        private static readonly FakeMeasurer M = new FakeMeasurer();

        [Test]
        public void TextField_Placeholder_IsEllipsized_ToTheFieldWidth()
        {
            // 폭 120, 안쪽 여백 양쪽 → 글자 칸. 긴 안내 문구(영어 2배 등)가 칸 밖으로 나가지 않는다.
            var field = new UiTextField { FontSize = 20, Placeholder = "Search furniture by name Search furniture by name" };
            field.Place(0, 0, 0, 0, 120, 40);
            field.Layout(new UiRect(0, 0, 1920, 1080));
            var list = new DrawList();
            field.Emit(list, M);
            DrawCommand placeholder = list.Commands.Single(c => c.Kind == DrawKind.Text);
            Assert.That(placeholder.Text, Does.EndWith(TextFit.Ellipsis));
            Assert.That(placeholder.Rect.X + M.Width(placeholder.Text, 20), Is.LessThanOrEqualTo(field.Rect.Right - field.PaddingX + 0.01f));

            field.Placeholder = "Find";
            list = new DrawList();
            field.Emit(list, M);
            Assert.That(list.Commands.Single(c => c.Kind == DrawKind.Text).Text, Is.EqualTo("Find"), "들어가는 문구는 그대로");
        }

        [Test]
        public void Fit_KeepsTextThatFits()
        {
            Assert.That(TextFit.Ellipsize("abcd", 40, M, 20), Is.EqualTo("abcd"));
            Assert.That(TextFit.Ellipsize("", 40, M, 20), Is.EqualTo(""));
            Assert.That(TextFit.Ellipsize(null, 40, M, 20), Is.EqualTo(""));
        }

        [Test]
        public void Fit_CutsAndAddsTheEllipsis_WithinTheWidth()
        {
            // 폭 50 = 5글자. "…" 1글자 + 앞 4글자.
            Assert.That(TextFit.Ellipsize("abcdefghij", 50, M, 20), Is.EqualTo("abcd" + TextFit.Ellipsis));
            Assert.That(TextFit.Ellipsize("가나다라마바사", 50, M, 20), Is.EqualTo("가나다라" + TextFit.Ellipsis));
        }

        [Test]
        public void Fit_DropsSpacesBeforeTheEllipsis_AndNeverSplitsASurrogatePair()
        {
            Assert.That(TextFit.Ellipsize("ab   cdefgh", 50, M, 20), Is.EqualTo("ab" + TextFit.Ellipsis));
            string emoji = "a\U0001F600bcdefg"; // 뒤 글자는 UTF-16 둘
            string fitted = TextFit.Ellipsize(emoji, 30, M, 20); // 3칸: "a" + 서로게이트 둘(2칸) 넘침 → "a…"
            Assert.That(fitted, Is.EqualTo("a" + TextFit.Ellipsis));
        }

        [Test]
        public void Fit_TooNarrowForEvenTheEllipsis_GivesEmpty()
        {
            Assert.That(TextFit.Ellipsize("abc", 5, M, 20), Is.EqualTo(""));
        }

        [Test]
        public void Label_ShortensLongText_ByDefault_AndCanBeTurnedOff()
        {
            var tree = new UiTree();
            var label = tree.Root.Add(new UiLabel { Text = "abcdefghij", FontSize = 20 });
            label.Place(0, 0, 0, 0, 50, 30);
            tree.Layout(1920, 1080);
            Assert.That(label.Ellipsis, Is.True, "기본은 켬");
            Assert.That(Texts(tree), Is.EqualTo(new[] { "abcd" + TextFit.Ellipsis }));

            label.Ellipsis = false;
            Assert.That(Texts(tree), Is.EqualTo(new[] { "abcdefghij" }));
        }

        [Test]
        public void Label_CentersTheShortenedText()
        {
            var tree = new UiTree();
            tree.Root.Add(new UiLabel { Text = "abcdefghij", FontSize = 20, Align = UiAlign.Center }).Place(0, 0, 0, 0, 55, 30);
            tree.Layout(1920, 1080);
            var text = Pointer.Emit(tree).First(c => c.Kind == DrawKind.Text);
            Assert.That(text.Text, Is.EqualTo("abcd" + TextFit.Ellipsis));
            Assert.That(text.Rect.X, Is.EqualTo(2.5f), "줄인 글자 폭(50)으로 가운데");
        }

        [Test]
        public void Button_ShortensLongText_ByDefault_AndCanBeTurnedOff()
        {
            var tree = new UiTree();
            var button = tree.Root.Add(new UiButton { Text = "abcdefghij", FontSize = 20 });
            button.Place(0, 0, 0, 0, 50, 30);
            tree.Layout(1920, 1080);
            Assert.That(button.Ellipsis, Is.True);
            Assert.That(Texts(tree), Is.EqualTo(new[] { "abcd" + TextFit.Ellipsis }));

            button.Ellipsis = false;
            Assert.That(Texts(tree), Is.EqualTo(new[] { "abcdefghij" }));
        }

        [Test]
        public void TextThatFits_IsDrawnUnchanged()
        {
            var tree = new UiTree();
            tree.Root.Add(new UiButton { Text = "abc", FontSize = 20 }).Place(0, 0, 0, 0, 50, 30);
            tree.Root.Add(new UiLabel { Text = "abcde", FontSize = 20 }).Place(0, 0, 0, 40, 50, 30);
            tree.Layout(1920, 1080);
            Assert.That(Texts(tree), Is.EqualTo(new[] { "abc", "abcde" }));
        }

        internal static List<string> Texts(UiTree tree) => Pointer.Emit(tree).Where(c => c.Kind == DrawKind.Text).Select(c => c.Text).ToList();
    }

    public sealed class MultilineButtonTests
    {
        [Test]
        public void WrapsInsideThePadding_AndCentersTheLines()
        {
            var tree = new UiTree();
            // 폭 70, 좌우 여백 10 → 줄 폭 50 = 5글자
            tree.Root.Add(new UiMultilineButton { Text = "가나다라마바사", FontSize = 20, PaddingX = 10, LineSpacing = 2 }).Place(0, 0, 0, 0, 70, 100);
            tree.Layout(1920, 1080);
            var texts = Pointer.Emit(tree).Where(c => c.Kind == DrawKind.Text).ToList();
            Assert.That(texts.Select(t => t.Text), Is.EqualTo(new[] { "가나다라마", "바사" }));
            Assert.That(texts[0].Rect.X, Is.EqualTo(10f));
            Assert.That(texts[1].Rect.X, Is.EqualTo(25f));
            Assert.That(texts[0].Rect.Y, Is.EqualTo((100f - 42f) / 2f), "두 줄(20+2+20)을 세로 가운데");
        }

        [Test]
        public void IsAClickableFocusableButton()
        {
            var button = new UiMultilineButton();
            Assert.That(button.Interactive && button.Focusable, Is.True);
        }
    }
}
