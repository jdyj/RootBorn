using System.Collections.Generic;

namespace Rootborn.Ui
{
    /// <summary>
    /// 폭에 맞춘 줄바꿈 — 여러 줄 글자의 줄바꿈 규칙은 여기 한 곳이다(UiTextBlock, UiMultilineButton이 쓴다). 끊을 수 있는 곳: 공백 뒤, 그리고 한글·한자·가나 글자 앞(글자 단위).
    /// 줄이 넘치면 마지막으로 끊을 수 있던 곳에서 끊고, 그런 곳이 없으면(긴 영어 단어) 글자 단위로 자른다.
    /// 끊은 자리의 공백은 버린다. 줄바꿈 문자(\n)는 언제나 새 줄이다.
    /// </summary>
    public static class TextWrap
    {
        public static void Wrap(string text, float width, ITextMeasurer measurer, float fontSize, List<string> into)
        {
            into.Clear();
            if (string.IsNullOrEmpty(text)) return;
            string[] paragraphs = text.Replace("\r\n", "\n").Split('\n');
            foreach (string paragraph in paragraphs) WrapParagraph(paragraph, width, measurer, fontSize, into);
        }

        private static void WrapParagraph(string text, float width, ITextMeasurer measurer, float fontSize, List<string> into)
        {
            int start = 0;
            while (start < text.Length)
            {
                int breakAt = -1; // 이 위치 앞에서 끊을 수 있다
                int end = start;
                while (end < text.Length)
                {
                    int next = end + (char.IsHighSurrogate(text[end]) && end + 1 < text.Length ? 2 : 1);
                    if (end > start && IsCjk(text[end])) breakAt = end;
                    if (measurer.Width(text.Substring(start, next - start), fontSize) > width && end > start) break;
                    if (text[end] == ' ') breakAt = next;
                    end = next;
                }
                if (end >= text.Length)
                {
                    into.Add(text.Substring(start).TrimEnd(' '));
                    return;
                }
                int cut = breakAt > start ? breakAt : end;
                into.Add(text.Substring(start, cut - start).TrimEnd(' '));
                start = cut;
                while (start < text.Length && text[start] == ' ') start++;
            }
            if (text.Length == 0) into.Add(string.Empty);
        }

        private static bool IsCjk(char c) =>
            (c >= '가' && c <= '힣') || (c >= 'ᄀ' && c <= 'ᇿ') || (c >= '㄰' && c <= '㆏')
            || (c >= '぀' && c <= 'ヿ') || (c >= '一' && c <= '鿿');
    }

    /// <summary>
    /// 한 줄 글자를 폭에 맞춘다: 넘치면 뒤를 잘라 말줄임표(…)를 붙인다. 자른 끝의 공백은 버리고, UTF-16 서로게이트 쌍은 가르지 않는다.
    /// 말줄임표조차 들어가지 않으면 빈 글자. 글자 단위로 자르므로 언어와 무관하다.
    /// </summary>
    public static class TextFit
    {
        public const string Ellipsis = "…";

        public static string Ellipsize(string text, float width, ITextMeasurer measurer, float fontSize)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            if (measurer.Width(text, fontSize) <= width) return text;
            if (measurer.Width(Ellipsis, fontSize) > width) return string.Empty;
            string best = Ellipsis;
            int end = 0;
            while (end < text.Length)
            {
                int next = end + (char.IsHighSurrogate(text[end]) && end + 1 < text.Length ? 2 : 1);
                string candidate = text.Substring(0, next).TrimEnd(' ') + Ellipsis;
                if (measurer.Width(candidate, fontSize) > width) break;
                best = candidate;
                end = next;
            }
            return best;
        }
    }

    /// <summary>한 줄 글자의 말줄임 결과를 글자·폭·크기가 바뀔 때만 다시 계산한다.</summary>
    internal struct FittedText
    {
        private string _source, _fitted;
        private float _width, _size;

        public string Get(string text, float width, float fontSize, ITextMeasurer measurer)
        {
            if (_fitted == null || _source != text || _width != width || _size != fontSize)
            {
                _fitted = TextFit.Ellipsize(text, width, measurer, fontSize);
                _source = text; _width = width; _size = fontSize;
            }
            return _fitted;
        }
    }

    /// <summary>
    /// 여러 줄 글자를 가운데(가로·세로)에 놓는 버튼. UiButton은 한 줄만 그린다. 줄은 줄바꿈 문자와 폭으로 나눈다(TextWrap).
    /// 줄바꿈 결과는 글자·폭·크기가 바뀔 때만 다시 계산한다.
    /// </summary>
    public class UiMultilineButton : UiNode
    {
        public string Text;
        public float FontSize = 20;
        public float LineSpacing = 2;
        public float PaddingX = 16;
        public string Skin = UiSkin.Button;

        private readonly List<string> _lines = new List<string>(4);
        private string _wrappedText;
        private float _wrappedWidth = -1, _wrappedSize;

        public UiMultilineButton() { Interactive = true; Focusable = true; }

        public override bool IsSurface => true;

        public override void Emit(DrawList list, ITextMeasurer text)
        {
            bool active = IsActive;
            UiColor tint = !active ? UiTheme.ButtonDisabled : Pressed ? UiTheme.ButtonPressed : Hovered ? UiTheme.ButtonHovered : UiTheme.ButtonNormal;
            list.Panel(Rect, Skin, tint);
            if (!string.IsNullOrEmpty(Text))
            {
                if (_wrappedText != Text || _wrappedWidth != Rect.W || _wrappedSize != FontSize)
                {
                    TextWrap.Wrap(Text, Rect.W - PaddingX * 2f, text, FontSize, _lines);
                    _wrappedText = Text; _wrappedWidth = Rect.W; _wrappedSize = FontSize;
                }
                float lineHeight = text.LineHeight(FontSize);
                float total = _lines.Count * lineHeight + (_lines.Count - 1) * LineSpacing;
                float y = Rect.Y + (Rect.H - total) / 2f + (Pressed ? UiTheme.PressedOffset : 0f);
                foreach (string line in _lines)
                {
                    float x = Rect.X + (Rect.W - text.Width(line, FontSize)) / 2f;
                    list.Text(x, y, line, FontSize, active ? UiTheme.Ink : UiTheme.InkDisabled, Skin);
                    y += lineHeight + LineSpacing;
                }
            }
            if (FocusRing) list.Outline(Rect, UiTheme.FocusOutline, UiTheme.Accent);
        }
    }

    /// <summary>여러 줄 글자. 폭에 맞춰 줄바꿈하고 위에서부터 쌓는다. 줄바꿈 결과는 글자·폭·크기가 바뀔 때만 다시 계산한다.</summary>
    public sealed class UiTextBlock : UiNode
    {
        public string Text;
        public float FontSize = 20;
        public float LineSpacing = 4;
        public UiColor Color = UiTheme.Ink;
        public UiAlign Align = UiAlign.Left;

        private readonly List<string> _lines = new List<string>(8);
        private string _wrappedText;
        private float _wrappedWidth = -1, _wrappedSize;

        public int LineCount(ITextMeasurer text)
        {
            EnsureWrapped(text);
            return _lines.Count;
        }

        /// <summary>모든 줄을 다 쓰는 데 드는 높이.</summary>
        public float ContentHeight(ITextMeasurer text)
        {
            EnsureWrapped(text);
            return _lines.Count == 0 ? 0 : _lines.Count * text.LineHeight(FontSize) + (_lines.Count - 1) * LineSpacing;
        }

        private void EnsureWrapped(ITextMeasurer text)
        {
            if (_wrappedText == Text && _wrappedWidth == Rect.W && _wrappedSize == FontSize) return;
            TextWrap.Wrap(Text, Rect.W, text, FontSize, _lines);
            _wrappedText = Text; _wrappedWidth = Rect.W; _wrappedSize = FontSize;
        }

        public override void Emit(DrawList list, ITextMeasurer text)
        {
            if (string.IsNullOrEmpty(Text)) return;
            EnsureWrapped(text);
            float lineHeight = text.LineHeight(FontSize);
            float y = Rect.Y;
            for (int i = 0; i < _lines.Count; i++)
            {
                string line = _lines[i];
                float x = Rect.X;
                if (Align != UiAlign.Left && line.Length > 0)
                {
                    float w = text.Width(line, FontSize);
                    x = Align == UiAlign.Center ? Rect.X + (Rect.W - w) / 2f : Rect.Right - w;
                }
                list.Text(x, y, line, FontSize, Color);
                y += lineHeight + LineSpacing;
            }
        }
    }

    /// <summary>
    /// 그림 하나(아이콘·스프라이트). Image는 그림 id(예: "Assets/Art/UI/x.png#이름", 또는 "icon:journal"),
    /// Source는 그 그림 안의 칸(비우면 그림 id가 가리키는 전체). 어느 PNG의 어디인지는 Host가 정한다.
    /// </summary>
    public sealed class UiImage : UiNode
    {
        public string Image;
        public PixelRect Source;
        public UiColor Tint = UiTheme.White;

        public override void Emit(DrawList list, ITextMeasurer text) => list.Image(Rect, Image, Source, Tint);
    }
}
