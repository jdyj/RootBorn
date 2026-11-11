using System.Collections.Generic;

namespace Rootborn.Ui
{
    /// <summary>
    /// 폭에 맞춘 줄바꿈. 끊을 수 있는 곳: 공백 뒤, 그리고 한글·한자·가나 글자 앞(글자 단위).
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
