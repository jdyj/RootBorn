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
        public void TextField_LongText_ShowsTheEndThatFits_InsideTheField()
        {
            // 칸보다 긴 입력(가구 id 검색 등)은 칸 밖으로 나가지 않고 끝 쪽(치는 자리)을 말줄임 뒤에 보인다. 입력 위치도 칸 안이다.
            var field = new UiTextField { FontSize = 20, MaxLength = 200 };
            field.Place(0, 0, 0, 0, 200, 40);
            field.Layout(new UiRect(0, 0, 1920, 1080));
            field.OnTextInput("modern.interiors.shadowless.livingroom.48x48.27");
            field.OnFocusChanged(true);
            var list = new DrawList();
            field.Emit(list, M);
            DrawCommand shown = list.Commands.Single(c => c.Kind == DrawKind.Text);
            Assert.That(shown.Text, Does.StartWith(TextFit.Ellipsis).And.EndWith("48x48.27"));
            Assert.That(shown.Rect.X + M.Width(shown.Text, 20), Is.LessThanOrEqualTo(field.Rect.Right - field.PaddingX + 0.01f));
            Assert.That(field.Text, Is.EqualTo("modern.interiors.shadowless.livingroom.48x48.27"), "입력값은 그대로");

            field.SetText("desk");
            list = new DrawList();
            field.Emit(list, M);
            Assert.That(list.Commands.Single(c => c.Kind == DrawKind.Text).Text, Is.EqualTo("desk"), "들어가는 글자는 그대로");
            Assert.That(TextFit.Tail("abcdefgh", 40, M, 20), Is.EqualTo(TextFit.Ellipsis + "fgh"));
            Assert.That(TextFit.Tail("abc", 40, M, 20), Is.EqualTo("abc"));
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

namespace Rootborn.Ui.Tests
{
    /// <summary>여러 줄 글자의 높이 맞추기(FitHeight): 칸 높이를 넘으면 글자를 줄이고(최소 크기까지), 그래도 넘으면 마지막 줄을 말줄임한다.</summary>
    public sealed class TextBlockFitHeightTests
    {
        private static readonly FakeMeasurer M = new FakeMeasurer();

        private static List<DrawCommand> Emit(UiTextBlock block)
        {
            block.Layout(new UiRect(0, 0, 1920, 1080));
            var list = new DrawList();
            block.Emit(list, M);
            return list.Commands.Where(c => c.Kind == DrawKind.Text).ToList();
        }

        [Test]
        public void TooTall_ShrinksTheFont_UntilItFits()
        {
            // 폭 100, 크기 20이면 한 줄 10글자. 30글자 = 3줄 × (20 + 간격 4) > 높이 60. 크기 16이면 한 줄 12글자 → 3줄 16·3+8=56 ≤ 60.
            var block = new UiTextBlock { FontSize = 20, LineSpacing = 4, FitHeight = true, MinFontSize = 14, Text = new string('가', 30) };
            block.Place(0, 0, 0, 0, 100, 60);
            List<DrawCommand> texts = Emit(block);
            Assert.That(texts.Select(t => t.FontSize).Distinct().Single(), Is.LessThan(20f).And.GreaterThanOrEqualTo(14f));
            Assert.That(texts.Max(t => t.Rect.Y + M.LineHeight(t.FontSize)), Is.LessThanOrEqualTo(60.5f), "줄이 칸 아래로 나가지 않는다");
        }

        [Test]
        public void StillTooTall_AtTheMinimum_EllipsizesTheLastLine()
        {
            var block = new UiTextBlock { FontSize = 20, LineSpacing = 4, FitHeight = true, MinFontSize = 18, Text = new string('가', 200) };
            block.Place(0, 0, 0, 0, 100, 60);
            List<DrawCommand> texts = Emit(block);
            Assert.That(texts.All(t => t.FontSize >= 18f), Is.True, "최소 크기보다 작아지지 않는다");
            Assert.That(texts.Max(t => t.Rect.Y + M.LineHeight(t.FontSize)), Is.LessThanOrEqualTo(60.5f));
            Assert.That(texts.Last().Text, Does.EndWith(TextFit.Ellipsis), "남은 글은 말줄임으로 보인다");
            Assert.That(texts.All(t => M.Width(t.Text, t.FontSize) <= 100.5f), Is.True);
        }

        [Test]
        public void WithoutFitHeight_KeepsTheFontAndAllLines()
        {
            var block = new UiTextBlock { FontSize = 20, LineSpacing = 4, Text = new string('가', 30) };
            block.Place(0, 0, 0, 0, 100, 60);
            List<DrawCommand> texts = Emit(block);
            Assert.That(texts.Count, Is.EqualTo(3));
            Assert.That(texts.All(t => t.FontSize == 20f), Is.True);
        }
    }
}
