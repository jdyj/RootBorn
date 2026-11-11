using System;
using System.Collections.Generic;
using Rootborn.Ui;

namespace Rootborn.Screens.Career
{
    /// <summary>
    /// 폭에 맞춰 줄바꿈하고 넘치면 휠로 스크롤하는 여러 줄 글자(Unity 상세 영역의 ScrollRect + Text).
    /// 글자가 바뀌면 맨 위로 돌아간다. 줄바꿈은 글자·폭·크기가 바뀔 때만 다시 계산한다.
    /// Rootborn.Ui에 없는 부품이라 화면 폴더에 둔다 — 조정 담당이 합칠 때 Rootborn.Ui로 올릴 후보(보고서).
    /// </summary>
    public sealed class ScrollText : UiNode
    {
        public float FontSize = 20;
        public float LineSpacing = 4;
        public UiColor Color = UiTheme.Ink;
        /// <summary>왼쪽·오른쪽 여백과 위 여백.</summary>
        public float PaddingX = 20, PaddingTop = 10, PaddingBottom = 10;
        public float WheelStep = 48;

        private readonly List<string> _lines = new List<string>(16);
        private string _text = string.Empty;
        private string _wrappedText;
        private float _wrappedWidth = -1, _wrappedSize;
        private float _scroll, _maxScroll;

        public ScrollText() { Interactive = true; }

        public string Text
        {
            get => _text;
            set
            {
                value ??= string.Empty;
                if (value == _text) return;
                _text = value;
                _scroll = 0;
            }
        }

        public float ScrollOffset => _scroll;
        /// <summary>마지막으로 그렸을 때 계산한 가장 아래 스크롤 거리. 그리기 전에는 0.</summary>
        public float MaxScroll => _maxScroll;
        public IReadOnlyList<string> Lines => _lines;

        /// <summary>글을 다 쓰는 데 드는 높이(위아래 여백 포함). 줄바꿈은 지금 폭으로 센다.</summary>
        public float ContentHeight(ITextMeasurer text)
        {
            EnsureWrapped(text);
            float lineHeight = text.LineHeight(FontSize);
            return PaddingTop + PaddingBottom + (_lines.Count == 0 ? 0 : _lines.Count * lineHeight + (_lines.Count - 1) * LineSpacing);
        }

        private void EnsureWrapped(ITextMeasurer text)
        {
            float width = Math.Max(1f, Rect.W - PaddingX * 2f);
            if (_wrappedText == _text && _wrappedWidth == width && _wrappedSize == FontSize) return;
            TextWrap.Wrap(_text, width, text, FontSize, _lines);
            _wrappedText = _text; _wrappedWidth = width; _wrappedSize = FontSize;
        }

        public override bool OnWheel(float notches)
        {
            if (_maxScroll <= 0) return false;
            _scroll = Math.Clamp(_scroll - notches * WheelStep, 0, _maxScroll);
            return true;
        }

        public override void Emit(DrawList list, ITextMeasurer text)
        {
            EnsureWrapped(text);
            float lineHeight = text.LineHeight(FontSize);
            float content = ContentHeight(text);
            _maxScroll = Math.Max(0, content - Rect.H);
            _scroll = Math.Clamp(_scroll, 0, _maxScroll);
            list.PushClip(Rect);
            float y = Rect.Y + PaddingTop - _scroll;
            for (int i = 0; i < _lines.Count; i++)
            {
                if (y + lineHeight >= Rect.Y && y <= Rect.Bottom && _lines[i].Length > 0)
                    list.Text(Rect.X + PaddingX, y, _lines[i], FontSize, Color);
                y += lineHeight + LineSpacing;
            }
            if (_maxScroll > 0)
            {
                const float width = 6f;
                float thumb = Math.Max(24f, Rect.H * Rect.H / content);
                float at = (Rect.H - thumb) * (_scroll / _maxScroll);
                list.Fill(new UiRect(Rect.Right - width - 2f, Rect.Y + at, width, thumb), new UiColor(86, 89, 114));
            }
            list.PopClip();
        }
    }
}
