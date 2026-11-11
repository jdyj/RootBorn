using System.Collections.Generic;
using Rootborn.Ui;

namespace Rootborn.Screens.Common
{
    /// <summary>
    /// 여러 줄 글자를 가운데(가로·세로)에 놓는 버튼. UiButton은 한 줄만 그린다. 줄은 '\n'과 폭으로 나눈다(TextWrap).
    /// Rootborn.Ui에 없는 부품이라 화면 폴더에 둔다 — 조정 담당이 합칠 때 Rootborn.Ui로 올릴 후보(보고서).
    /// </summary>
    public sealed class MultilineButton : UiNode
    {
        public string Text;
        public float FontSize = 20;
        public float LineSpacing = 2;
        public float PaddingX = 16;
        public string Skin = UiSkin.Button;

        private readonly List<string> _lines = new List<string>(4);
        private string _wrappedText;
        private float _wrappedWidth = -1, _wrappedSize;

        public MultilineButton() { Interactive = true; Focusable = true; }

        public override void Emit(DrawList list, ITextMeasurer text)
        {
            bool active = IsActive;
            UiColor tint = !active ? UiTheme.ButtonDisabled : Pressed ? UiTheme.ButtonPressed : Hovered ? UiTheme.ButtonHovered : UiTheme.ButtonNormal;
            list.Panel(Rect, Skin, tint);
            if (!string.IsNullOrEmpty(Text))
            {
                if (_wrappedText != Text || _wrappedWidth != Rect.W || _wrappedSize != FontSize)
                {
                    TextWrap.Wrap(Text, Rect.W - PaddingX * 2f, text, FontSize, _lines); // 글자·폭·크기가 바뀔 때만
                    _wrappedText = Text; _wrappedWidth = Rect.W; _wrappedSize = FontSize;
                }
                float lineHeight = text.LineHeight(FontSize);
                float total = _lines.Count * lineHeight + (_lines.Count - 1) * LineSpacing;
                float y = Rect.Y + (Rect.H - total) / 2f + (Pressed ? UiTheme.PressedOffset : 0f);
                foreach (string line in _lines)
                {
                    float x = Rect.X + (Rect.W - text.Width(line, FontSize)) / 2f;
                    list.Text(x, y, line, FontSize, active ? UiTheme.Ink : UiTheme.InkMuted, Skin);
                    y += lineHeight + LineSpacing;
                }
            }
            if (Focused) list.Outline(Rect, UiTheme.FocusOutline, UiTheme.Accent);
        }
    }
}
