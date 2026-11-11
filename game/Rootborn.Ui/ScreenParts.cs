using System;
using System.Collections.Generic;

namespace Rootborn.Ui
{
    // 화면 폴더에서 올린 공용 부품: 세로 스크롤 구역 글, 가운데 여러 줄 글자, 스크롤 글, 줄어드는 한 줄 이름, 비율 맞춘 그림, 고르는 줄 바탕.

    /// <summary>
    /// 세로로 스크롤하는 글자 묶음(Unity의 ScrollRect + VerticalLayoutGroup + 줄바꿈 Text 구역들). 구역마다 줄바꿈해 쌓고,
    /// 보이는 줄만 그린다. 휠·끌기로 스크롤한다. 줄바꿈 결과는 글자나 폭이 바뀔 때만 다시 계산한다.
    /// </summary>
    public sealed class UiSectionScrollView : UiNode
    {
        public float FontSize = 20;
        public float LineSpacing = 4;
        public float SectionSpacing = 22;
        public float PaddingLeft = 14, PaddingTop = 10, PaddingRight = 14, PaddingBottom = 20;
        public float WheelStep = 48;
        public float DragThreshold = 8;
        public UiColor Color = UiTheme.Ink;

        private readonly List<string> _sections = new List<string>(16);
        private readonly List<List<string>> _wrapped = new List<List<string>>(16);
        private float _wrappedWidth = -1, _wrappedSize;
        private bool _dirty = true;
        private float _scroll, _pressScroll, _contentHeight;
        private bool _dragging;

        public const float ScrollbarWidth = 18;
        private static readonly UiColor Track = new UiColor(64, 46, 31, 38);
        private static readonly UiColor Thumb = new UiColor(117, 87, 54);

        public UiSectionScrollView() { Interactive = true; }

        public IReadOnlyList<string> Sections => _sections;

        public void SetSections(IEnumerable<string> sections)
        {
            _sections.Clear();
            foreach (string s in sections) if (!string.IsNullOrEmpty(s)) _sections.Add(s);
            _dirty = true;
        }

        public float ScrollOffset { get => _scroll; set => _scroll = MathF.Max(0, MathF.Min(value, MaxScroll)); }
        public float MaxScroll => MathF.Max(0, _contentHeight - TextArea.H);
        public float ContentHeight => _contentHeight;

        private UiRect TextArea => new UiRect(Rect.X + PaddingLeft, Rect.Y + PaddingTop, MathF.Max(1, Rect.W - PaddingLeft - PaddingRight - ScrollbarWidth), MathF.Max(1, Rect.H - PaddingTop - PaddingBottom));

        /// <summary>줄바꿈한 줄 전부(테스트와 결함 확인용). 측정기가 필요하다.</summary>
        public IReadOnlyList<string> Lines(ITextMeasurer text)
        {
            Ensure(text);
            var all = new List<string>();
            foreach (List<string> s in _wrapped) all.AddRange(s);
            return all;
        }

        private void Ensure(ITextMeasurer text)
        {
            UiRect area = TextArea;
            if (!_dirty && _wrappedWidth == area.W && _wrappedSize == FontSize) return;
            while (_wrapped.Count < _sections.Count) _wrapped.Add(new List<string>(8));
            float lineHeight = text.LineHeight(FontSize), height = 0;
            for (int i = 0; i < _sections.Count; i++)
            {
                TextWrap.Wrap(_sections[i], area.W, text, FontSize, _wrapped[i]);
                int n = _wrapped[i].Count;
                if (i > 0) height += SectionSpacing;
                height += n * lineHeight + Math.Max(0, n - 1) * LineSpacing;
            }
            for (int i = _sections.Count; i < _wrapped.Count; i++) _wrapped[i].Clear();
            _contentHeight = height;
            _wrappedWidth = area.W; _wrappedSize = FontSize; _dirty = false;
            ScrollOffset = _scroll;
        }

        public override bool OnWheel(float notches)
        {
            ScrollOffset = _scroll - notches * WheelStep;
            return true;
        }

        public override void OnPress(float x, float y) { _pressScroll = _scroll; _dragging = false; }

        public override bool OnDrag(float x, float y, float dx, float dy)
        {
            if (!_dragging && MathF.Abs(dy) > DragThreshold) _dragging = true;
            if (_dragging) ScrollOffset = _pressScroll - dy;
            return _dragging;
        }

        public override void OnDragEnd() => _dragging = false;

        public override void Emit(DrawList list, ITextMeasurer text)
        {
            Ensure(text);
            UiRect area = TextArea;
            float lineHeight = text.LineHeight(FontSize);
            list.PushClip(new UiRect(Rect.X, Rect.Y, Rect.W - ScrollbarWidth, Rect.H));
            float y = area.Y - _scroll;
            for (int s = 0; s < _sections.Count; s++)
            {
                if (s > 0) y += SectionSpacing;
                List<string> lines = _wrapped[s];
                for (int i = 0; i < lines.Count; i++)
                {
                    if (y + lineHeight >= Rect.Y && y <= Rect.Bottom) list.Text(area.X, y, lines[i], FontSize, Color);
                    y += lineHeight + (i < lines.Count - 1 ? LineSpacing : 0);
                }
            }
            list.PopClip();
            var track = new UiRect(Rect.Right - ScrollbarWidth, Rect.Y, ScrollbarWidth, Rect.H);
            list.Fill(track, Track);
            float max = MaxScroll;
            float thumbH = max <= 0 ? track.H : MathF.Max(24, track.H * area.H / MathF.Max(area.H, _contentHeight));
            float thumbY = max <= 0 ? track.Y : track.Y + (track.H - thumbH) * (_scroll / max);
            list.Fill(new UiRect(track.X, thumbY, track.W, thumbH), Thumb);
        }
    }

    /// <summary>
    /// 사각형 가운데에 놓는 여러 줄 글자(Unity Text의 MiddleCenter·MiddleLeft + 줄바꿈). 버튼·카드 안 글자에 쓴다. 포인터를 받지 않는다.
    /// </summary>
    public sealed class UiCenteredText : UiNode
    {
        public string Text;
        public float FontSize = 20;
        public float LineSpacing = 2;
        public UiColor Color = UiTheme.Ink;
        public UiAlign Align = UiAlign.Center;
        /// <summary>글자가 놓이는 바탕 스킨(버튼 위면 UiSkin.Button). 스킨이 정한 글자색을 쓰게 Host에 넘긴다.</summary>
        public string OnSkin;

        private readonly List<string> _lines = new List<string>(4);
        private string _wrappedText;
        private float _wrappedWidth = -1, _wrappedSize;

        public override void Emit(DrawList list, ITextMeasurer text)
        {
            if (string.IsNullOrEmpty(Text)) return;
            if (_wrappedText != Text || _wrappedWidth != Rect.W || _wrappedSize != FontSize)
            {
                // 줄바꿈은 글자·폭·크기가 바뀔 때만(프레임마다 줄 글자를 새로 만들지 않는다).
                TextWrap.Wrap(Text, Rect.W, text, FontSize, _lines);
                _wrappedText = Text; _wrappedWidth = Rect.W; _wrappedSize = FontSize;
            }
            float lineHeight = text.LineHeight(FontSize);
            float total = _lines.Count * lineHeight + Math.Max(0, _lines.Count - 1) * LineSpacing;
            float y = Rect.Y + (Rect.H - total) / 2f;
            foreach (string line in _lines)
            {
                float w = text.Width(line, FontSize);
                float x = Align == UiAlign.Left ? Rect.X : Align == UiAlign.Right ? Rect.Right - w : Rect.X + (Rect.W - w) / 2f;
                list.Text(x, y, line, FontSize, Color, OnSkin);
                y += lineHeight + LineSpacing;
            }
        }
    }

    /// <summary>
    /// 폭에 맞춰 줄바꿈하고 넘치면 휠로 스크롤하는 여러 줄 글자(Unity 상세 영역의 ScrollRect + Text).
    /// 글자가 바뀌면 맨 위로 돌아간다. 줄바꿈은 글자·폭·크기가 바뀔 때만 다시 계산한다.
    /// </summary>
    public sealed class UiScrollText : UiNode
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

        public UiScrollText() { Interactive = true; }

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

    /// <summary>
    /// 한 줄 이름 글자: 폭에 맞을 때까지 글자 크기를 MaxFontSize에서 MinFontSize까지 줄인다(Unity resizeTextForBestFit 24~36).
    /// 그래도 넘치면 말줄임표로 줄인다.
    /// </summary>
    public sealed class UiShrinkLabel : UiNode
    {
        public string Text;
        public float MaxFontSize = 36f, MinFontSize = 24f;
        public UiColor Color = UiTheme.Ink;

        private string _fitText, _shown;
        private float _fitWidth = -1, _fitMax, _fitMin, _size;

        public override void Emit(DrawList list, ITextMeasurer text)
        {
            if (string.IsNullOrEmpty(Text)) return;
            if (_shown == null || _fitText != Text || _fitWidth != Rect.W || _fitMax != MaxFontSize || _fitMin != MinFontSize)
            {
                // 크기 줄이기·말줄임은 글자·폭·크기 범위가 바뀔 때만(넘치는 이름이 프레임마다 잘린 글자를 새로 만들지 않는다).
                Fit(text);
                _fitText = Text; _fitWidth = Rect.W; _fitMax = MaxFontSize; _fitMin = MinFontSize;
            }
            list.Text(Rect.X, Rect.Y + (Rect.H - text.LineHeight(_size)) / 2f, _shown, _size, Color);
        }

        private void Fit(ITextMeasurer text)
        {
            float size = MaxFontSize;
            while (size > MinFontSize && text.Width(Text, size) > Rect.W) size -= 2f;
            string shown = Text;
            if (text.Width(shown, size) > Rect.W)
            {
                const string ellipsis = "…";
                int length = shown.Length;
                while (length > 1 && text.Width(shown.Substring(0, length) + ellipsis, size) > Rect.W) length--;
                shown = shown.Substring(0, length) + ellipsis;
            }
            _shown = shown;
            _size = size;
        }
    }

    /// <summary>
    /// 사각형 안에 비율을 지켜 맞춘 그림(Unity Image.preserveAspect). 그림 크기는 좌표표에서 읽은 값이고, 그릴 때 지금 사각형으로 계산한다
    /// (쪽을 넘겨 그림이 바뀌어도 배치를 다시 할 필요가 없다). 그림이 없으면 아무것도 그리지 않는다. 포인터를 받지 않는다.
    /// </summary>
    public sealed class UiFittedImage : UiNode
    {
        public string Image;
        public float SourceWidth, SourceHeight;
        public UiColor Tint = UiTheme.White;

        public void Show(string image, float width, float height)
        {
            Image = image;
            SourceWidth = width;
            SourceHeight = height;
        }

        public void Clear() => Image = null;

        /// <summary>지금 사각형 안에 비율을 지켜 가운데 놓은 사각형.</summary>
        public UiRect FittedRect
        {
            get
            {
                if (SourceWidth <= 0 || SourceHeight <= 0) return Rect;
                float scale = MathF.Min(Rect.W / SourceWidth, Rect.H / SourceHeight);
                float w = SourceWidth * scale, h = SourceHeight * scale;
                return new UiRect(Rect.X + (Rect.W - w) / 2f, Rect.Y + (Rect.H - h) / 2f, w, h);
            }
        }

        public override void Emit(DrawList list, ITextMeasurer text)
        {
            if (string.IsNullOrEmpty(Image)) return;
            list.Image(FittedRect, Image, default, Tint);
        }
    }

    /// <summary>
    /// 눌러 고르는 줄의 바탕(Unity 단계 줄: 투명한 버튼, 고른 줄만 옅은 색). 포커스를 받는다.
    /// </summary>
    public sealed class UiSelectableRow : UiNode
    {
        public bool Selected;
        public UiColor SelectedColor = UiTheme.Accent;

        public UiSelectableRow() { Interactive = true; Focusable = true; }

        public override void Emit(DrawList list, ITextMeasurer text)
        {
            if (Selected) list.Fill(Rect, SelectedColor);
            if (FocusRing) list.Outline(Rect, UiTheme.FocusOutline, UiTheme.Accent);
        }
    }
}
