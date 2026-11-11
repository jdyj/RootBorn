using System;
using System.Collections.Generic;
using Rootborn.Ui;

namespace Rootborn.Screens.Objectives
{
    // 이 묶음의 새 부품들(Rootborn.Ui로 올릴 후보). 그림은 스킨 이름과 아이콘 키만 쓴다.

    /// <summary>분류 단추: 아이콘과 이름. 고른 분류는 밝게 그리고 테두리를 둘러 표시한다(ui-standards: 선택 표시).</summary>
    public sealed class JournalRailButton : UiNode
    {
        public string Icon;
        public string Text;
        public bool Selected;
        public string Skin = UiSkin.Button;
        public float IconX = 14f, IconY = 18f, IconSize = 72f, TextX = 94f, TextY = 36f, TextW = 96f, TextH = 36f, FontSize = 22f;
        public UiColor Ink = UiTheme.Ink;

        public JournalRailButton() { Interactive = true; Focusable = true; }

        public override void Emit(DrawList list, ITextMeasurer text)
        {
            UiColor tint = !IsActive ? UiTheme.ButtonDisabled : Selected ? UiTheme.ButtonHovered : Pressed ? UiTheme.ButtonPressed : Hovered ? UiTheme.ButtonHovered : UiTheme.ButtonNormal;
            list.Panel(Rect, Skin, tint);
            if (!string.IsNullOrEmpty(Icon)) list.Image(new UiRect(Rect.X + IconX, Rect.Y + IconY, IconSize, IconSize), Icon, default, UiTheme.White);
            if (!string.IsNullOrEmpty(Text))
            {
                var area = new UiRect(Rect.X + TextX, Rect.Y + TextY, Math.Min(TextW, Rect.W - TextX - 4f), TextH);
                list.PushClip(area);
                list.Text(area.X, area.Y + (area.H - text.LineHeight(FontSize)) / 2f, Text, FontSize, Ink, Skin);
                list.PopClip();
            }
            if (Selected) list.Outline(Rect.Inset(6f), UiTheme.FocusOutline, Ink);
            if (FocusRing) list.Outline(Rect, UiTheme.FocusOutline, UiTheme.Accent);
        }
    }

    /// <summary>
    /// 수첩 목록(Unity ObjectiveList + ObjectiveRow): 높이 88 줄을 96 간격으로, 줄마다 아이콘과 두 줄 글자(제목, 진행).
    /// 줄은 요소가 아니다: 보이는 줄만 RowText로 물어 그린다(200줄이어도 화면에 걸친 7~8줄만). 휠·끌기로 넘기고 눌러서 고른다.
    /// </summary>
    public sealed class JournalList : UiNode
    {
        public float Padding = 12f, RowStep = 96f, RowWidth = 396f, RowHeight = 88f;
        public float IconX = 10f, IconY = 18f, IconSize = 56f, TextX = 80f, TextY = 13f, TextW = 302f, TextH = 64f, FontSize = 18f, LineGap = 4f;
        public float RowsPerNotch = 2f, DragThreshold = 8f, ScrollbarGap = 14f;
        public string Skin = UiSkin.Field;
        public string Icon;
        public UiColor Ink = UiTheme.Ink;
        public UiColor SelectedColor = new UiColor(230, 215, 178);
        public UiColor HoverColor = new UiColor(236, 225, 196);
        public Func<int, (string Title, string Progress)> RowText;
        public event Action<int> RowClicked;

        private int _count, _selected = -1, _hover = -1;
        private float _scroll, _pressScroll;
        private bool _dragging;

        public JournalList() { Interactive = true; Focusable = true; }

        public int RowCount { get => _count; set { _count = Math.Max(0, value); if (_selected >= _count) _selected = -1; _scroll = Clamp(_scroll); } }
        public int SelectedIndex { get => _selected; set => _selected = value >= 0 && value < _count ? value : -1; }
        public float ScrollOffset { get => _scroll; set => _scroll = Clamp(value); }
        /// <summary>지난번 그릴 때 RowText로 물은 줄 수(보이는 줄만 만드는지 보는 시험용).</summary>
        public int LastEmittedRows { get; private set; }

        public UiRect View => Rect.Inset(Padding);
        public float ContentHeight => _count <= 0 ? 0 : (_count - 1) * RowStep + RowHeight;
        public float MaxScroll => MathF.Max(0, ContentHeight - View.H);
        private float Clamp(float v) => MathF.Max(0, MathF.Min(v, MaxScroll));

        public UiRect RowRect(int index)
        {
            UiRect view = View;
            return new UiRect(view.X + (view.W - RowWidth) / 2f, view.Y + index * RowStep - _scroll, RowWidth, RowHeight);
        }

        public int RowAt(float x, float y)
        {
            if (!View.Contains(x, y)) return -1;
            int row = (int)MathF.Floor((y - View.Y + _scroll) / RowStep);
            if (row < 0 || row >= _count) return -1;
            return RowRect(row).Contains(x, y) ? row : -1;
        }

        public void EnsureVisible(int index)
        {
            float top = index * RowStep, bottom = top + RowHeight;
            if (top < _scroll) ScrollOffset = top;
            else if (bottom > _scroll + View.H) ScrollOffset = bottom - View.H;
        }

        public override bool OnWheel(float notches) { ScrollOffset = _scroll - notches * RowStep * RowsPerNotch; return true; }
        public override void OnPress(float x, float y) { _pressScroll = _scroll; _dragging = false; }

        public override bool OnDrag(float x, float y, float dx, float dy)
        {
            if (!_dragging && MathF.Abs(dy) > DragThreshold) _dragging = true;
            if (_dragging) ScrollOffset = _pressScroll - dy;
            return _dragging;
        }

        public override void OnDragEnd() => _dragging = false;
        public override void OnPointer(float x, float y, bool inside) => _hover = inside ? RowAt(x, y) : -1;

        public override void OnClick(float x, float y)
        {
            int row = RowAt(x, y);
            if (row >= 0) RowClicked?.Invoke(row);
        }

        public override void OnSubmit() { if (_selected >= 0) RowClicked?.Invoke(_selected); }

        public override bool OnNavigate(UiNav nav)
        {
            if (nav != UiNav.Up && nav != UiNav.Down || _count == 0) return false;
            int next = _selected < 0 ? 0 : _selected + (nav == UiNav.Down ? 1 : -1);
            if (next < 0 || next >= _count) return false;
            EnsureVisible(next);
            RowClicked?.Invoke(next);
            return true;
        }

        public override void Emit(DrawList list, ITextMeasurer text)
        {
            list.Panel(Rect, Skin, IsActive ? UiTheme.ButtonHovered : UiTheme.ButtonDisabled);
            UiRect view = View;
            list.PushClip(view);
            int first = Math.Max(0, (int)MathF.Floor(_scroll / RowStep));
            int last = Math.Min(_count - 1, (int)MathF.Floor((_scroll + view.H) / RowStep));
            int emitted = 0;
            bool scrollable = MaxScroll > 0;
            float lineHeight = text.LineHeight(FontSize);
            for (int row = first; row <= last; row++)
            {
                UiRect r = RowRect(row);
                // 스크롤 막대가 있으면 줄 바탕이 막대를 덮지 않게 줄인다.
                var back = scrollable ? new UiRect(r.X, r.Y, MathF.Min(r.W, view.Right - ScrollbarGap - r.X), r.H) : r;
                if (row == _selected) list.Fill(back, SelectedColor);
                else if (row == _hover) list.Fill(back, HoverColor);
                if (!string.IsNullOrEmpty(Icon)) list.Image(new UiRect(r.X + IconX, r.Y + IconY, IconSize, IconSize), Icon, default, UiTheme.White);
                if (RowText == null) continue;
                (string title, string progress) = RowText(row);
                emitted++;
                var area = new UiRect(r.X + TextX, r.Y + TextY, TextW, TextH);
                list.PushClip(area);
                float y = area.Y + (area.H - lineHeight * 2f - LineGap) / 2f;
                // 칸보다 긴 글은 글자 중간에서 잘리지 않게 말줄임으로 줄인다(넘칠 때만 다시 잰다).
                list.Text(area.X, y, TextFit.Ellipsize(title, area.W, text, FontSize), FontSize, Ink);
                list.Text(area.X, y + lineHeight + LineGap, TextFit.Ellipsize(progress, area.W, text, FontSize), FontSize, Ink);
                list.PopClip();
            }
            list.PopClip();
            LastEmittedRows = emitted;
            if (MaxScroll > 0)
            {
                const float bar = 8f;
                float content = ContentHeight;
                float thumb = MathF.Max(24f, view.H * view.H / content);
                list.Fill(new UiRect(view.Right - bar, view.Y, bar, view.H), new UiColor(60, 60, 100, 40));
                list.Fill(new UiRect(view.Right - bar, view.Y + (view.H - thumb) * (_scroll / MaxScroll), bar, thumb), new UiColor(86, 89, 114));
            }
            if (FocusRing) list.Outline(Rect, UiTheme.FocusOutline, UiTheme.Accent);
        }
    }

    /// <summary>
    /// 상세 글 묶음(Unity ObjectiveDetailContent): 문단(제목 28, 나머지 20)을 폭에 맞춰 줄바꿈해 위에서부터 20 간격으로 쌓는다.
    /// 넘치면 휠로 넘긴다. 줄바꿈은 글자·폭이 바뀔 때만 다시 한다.
    /// </summary>
    public sealed class JournalDetail : UiNode
    {
        public float PaddingX = 16f, PaddingTop = 16f, Gap = 20f, LineSpacing = 4f, RowsPerNotch = 40f;
        public UiColor Ink = UiTheme.Ink;

        private readonly List<(string Text, float Size)> _paragraphs = new List<(string, float)>(6);
        private readonly List<List<string>> _wrapped = new List<List<string>>(6);
        private float _wrappedWidth = -1;
        private float _scroll, _contentHeight;

        public JournalDetail() { Interactive = true; }

        public IReadOnlyList<(string Text, float Size)> Paragraphs => _paragraphs;

        public void SetParagraphs(IReadOnlyList<(string Text, float Size)> paragraphs)
        {
            _paragraphs.Clear();
            if (paragraphs != null) _paragraphs.AddRange(paragraphs);
            _wrappedWidth = -1;
            _scroll = 0;
        }

        public string VisibleText
        {
            get
            {
                var parts = new List<string>(_paragraphs.Count);
                foreach ((string text, float _) in _paragraphs) if (!string.IsNullOrEmpty(text)) parts.Add(text);
                return string.Join("\n", parts);
            }
        }

        public override bool OnWheel(float notches)
        {
            _scroll = MathF.Max(0, MathF.Min(_scroll - notches * RowsPerNotch, MathF.Max(0, _contentHeight - Rect.H)));
            return true;
        }

        public override void Emit(DrawList list, ITextMeasurer text)
        {
            float width = MathF.Max(100f, Rect.W - PaddingX * 2f);
            if (_wrappedWidth != width)
            {
                _wrapped.Clear();
                foreach ((string t, float size) in _paragraphs)
                {
                    var lines = new List<string>(4);
                    TextWrap.Wrap(t, width, text, size, lines);
                    _wrapped.Add(lines);
                }
                _wrappedWidth = width;
            }
            list.PushClip(Rect);
            float y = Rect.Y + PaddingTop - _scroll;
            for (int p = 0; p < _paragraphs.Count; p++)
            {
                float size = _paragraphs[p].Size, lineHeight = text.LineHeight(size);
                List<string> lines = _wrapped[p];
                float height = Math.Max(size + 10f, lines.Count * lineHeight + Math.Max(0, lines.Count - 1) * LineSpacing + 4f);
                float lineY = y;
                foreach (string line in lines)
                {
                    if (lineY + lineHeight >= Rect.Y && lineY <= Rect.Bottom) list.Text(Rect.X + PaddingX, lineY, line, size, Ink);
                    lineY += lineHeight + LineSpacing;
                }
                y += height + Gap;
            }
            list.PopClip();
            _contentHeight = y + _scroll - Rect.Y;
        }
    }
}
