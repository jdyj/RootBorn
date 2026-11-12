using System;

namespace Rootborn.Ui
{
    /// <summary>카드 하나를 그릴 때 넘기는 상태.</summary>
    public readonly record struct CardState(bool Selected, bool Hovered, bool Focused, bool Active);

    /// <summary>
    /// 카드 하나를 그린다. 보이는 카드에만 불린다. rect는 카드 전체 사각형(보이는 영역 밖으로 걸칠 수 있다 — 잘라 내기는 부품이 건다).
    /// </summary>
    public delegate void CardPainter(int index, UiRect rect, CardState state, DrawList list, ITextMeasurer text);

    /// <summary>
    /// 카드 목록의 공통 부분: 스크롤 거리, 끌기, 휠, 골라 둔 카드, 보이는 카드만 그리기. 카드는 요소(UiNode)가 아니다.
    /// 카드 수만 알고 있다가 보이는 카드만 Painter로 그린다. 1,500개여도 한 번에 다루는 것은 화면에 걸친 카드뿐이다.
    /// </summary>
    public abstract class UiCardScroller : UiNode
    {
        public float Padding = 16;
        public float DragThreshold = 8;
        /// <summary>휠 한 칸에 넘기는 줄(가로 줄이면 카드) 수.</summary>
        public float StepsPerNotch = 1;
        public string Skin = UiSkin.Field;
        public bool DrawBackground = true;

        /// <summary>카드를 클릭했거나, 고른 카드에서 실행 키를 눌렀다.</summary>
        public event Action<int> CardClickedEvent;

        protected int _count;
        protected float _scroll;
        protected int _selected = -1, _hover = -1;
        private float _pressScroll;
        private bool _dragging;

        protected UiCardScroller() { Interactive = true; Focusable = true; }

        public UiRect View => Rect.Inset(Padding);

        /// <summary>스크롤 축으로 한 칸(줄 또는 카드) 크기.</summary>
        protected abstract float StepSize { get; }
        protected abstract float ContentLength { get; }
        protected abstract float ViewLength { get; }
        protected abstract bool Horizontal { get; }
        public abstract int IndexAt(float x, float y);
        protected abstract void EnsureVisible(int index);
        protected abstract int Move(int from, UiNav nav);
        protected abstract void EmitCards(DrawList list, ITextMeasurer text);
        /// <summary>그 카드(칸)가 화면에서 차지하는 사각형(보이는 영역 밖일 수 있다).</summary>
        public abstract UiRect ItemRect(int index);

        public float MaxScroll => MathF.Max(0, ContentLength - ViewLength);

        public float ScrollOffset
        {
            get => _scroll;
            set => _scroll = MathF.Max(0, MathF.Min(value, MaxScroll));
        }

        public int SelectedIndex
        {
            get => _selected;
            set => _selected = value >= 0 && value < _count ? value : -1;
        }

        protected void SetCount(int count)
        {
            _count = Math.Max(0, count);
            if (_selected >= _count) _selected = -1;
            ScrollOffset = _scroll;
        }

        protected CardState StateOf(int index) => new CardState(index == _selected, index == _hover, FocusRing, IsActive);

        public override bool OnWheel(float notches)
        {
            ScrollOffset = _scroll - notches * StepSize * StepsPerNotch;
            return true;
        }

        public override void OnPress(float x, float y)
        {
            _pressScroll = _scroll;
            _dragging = false;
        }

        public override bool OnDrag(float x, float y, float dx, float dy)
        {
            float along = Horizontal ? dx : dy;
            if (!_dragging && MathF.Abs(along) > DragThreshold) _dragging = true;
            if (_dragging) ScrollOffset = _pressScroll - along;
            return _dragging;
        }

        public override void OnDragEnd() => _dragging = false;

        public override void OnPointer(float x, float y, bool inside) => _hover = inside ? IndexAt(x, y) : -1;

        public override void OnClick(float x, float y)
        {
            int index = IndexAt(x, y);
            if (index < 0) return;
            _selected = index;
            CardClickedEvent?.Invoke(index);
        }

        public override void OnSubmit()
        {
            if (_selected >= 0) CardClickedEvent?.Invoke(_selected);
        }

        public override bool OnNavigate(UiNav nav)
        {
            if (_count == 0) return false;
            int next = _selected < 0 ? FirstVisible() : Move(_selected, nav);
            if (next < 0 || next >= _count) return false; // 끝에서는 부품을 벗어난다
            if (_selected >= 0 && next == _selected) return false;
            _selected = next;
            EnsureVisible(next);
            return true;
        }

        protected abstract int FirstVisible();

        protected void ScrollToShow(float start, float end)
        {
            if (start < _scroll) ScrollOffset = start;
            else if (end > _scroll + ViewLength) ScrollOffset = end - ViewLength;
        }

        /// <summary>스크롤 막대 두께와 카드와의 간격(UiScrollList와 같은 모양).</summary>
        public const float ScrollbarWidth = 8, ScrollbarGap = 6;
        private static readonly UiColor ScrollTrack = new UiColor(60, 60, 100, 40);
        private static readonly UiColor ScrollThumb = new UiColor(86, 89, 114);

        public override void Emit(DrawList list, ITextMeasurer text)
        {
            if (DrawBackground) list.Panel(Rect, Skin, IsActive ? UiTheme.ButtonHovered : UiTheme.ButtonDisabled);
            UiRect view = View;
            list.PushClip(view);
            EmitCards(list, text);
            // 키보드로 고른 칸의 포커스 테두리는 여기 한 곳에서 그린다. 칸 안쪽에 그려 보이는 영역 가장자리의 칸에서도 잘리지 않는다.
            if (FocusRing && _selected >= 0 && _selected < _count)
                list.Outline(ItemRect(_selected).Inset(UiTheme.FocusOutline), UiTheme.FocusOutline, UiTheme.Accent);
            list.PopClip();
            float max = MaxScroll;
            if (max > 0)
            {
                float length = Horizontal ? view.W : view.H;
                float thumb = MathF.Max(24, length * length / ContentLength);
                float at = (length - thumb) * (_scroll / max);
                if (Horizontal)
                {
                    float y = view.Bottom - ScrollbarWidth;
                    list.Fill(new UiRect(view.X, y, view.W, ScrollbarWidth), ScrollTrack);
                    list.Fill(new UiRect(view.X + at, y, thumb, ScrollbarWidth), ScrollThumb);
                }
                else
                {
                    float x = view.Right - ScrollbarWidth;
                    list.Fill(new UiRect(x, view.Y, ScrollbarWidth, view.H), ScrollTrack);
                    list.Fill(new UiRect(x, view.Y + at, ScrollbarWidth, thumb), ScrollThumb);
                }
            }
            // 고른 칸이 있으면 위에서 그 칸에 테두리를 그렸다. 목록 전체에 또 두르지 않는다.
            if (FocusRing && _selected < 0) list.Outline(Rect, UiTheme.FocusOutline, UiTheme.Accent);
        }
    }

    /// <summary>가로로 스크롤하는 카드 한 줄. 카드 높이는 보이는 영역 높이에서 아래 스크롤 막대 자리를 뺀 것.</summary>
    public sealed class UiCardRow : UiCardScroller
    {
        public float CardWidth = 160;
        public float Spacing = 16;
        public CardPainter DrawCard;

        public event Action<int> CardClicked { add => CardClickedEvent += value; remove => CardClickedEvent -= value; }

        public int CardCount { get => _count; set => SetCount(value); }

        private float Pitch => CardWidth + Spacing;
        protected override float StepSize => Pitch;
        protected override float ContentLength => _count == 0 ? 0 : _count * Pitch - Spacing;
        protected override float ViewLength => View.W;
        protected override bool Horizontal => true;

        public override UiRect ItemRect(int index) => CardRect(index);

        public UiRect CardRect(int index)
        {
            UiRect view = View;
            return new UiRect(view.X + index * Pitch - _scroll, view.Y, CardWidth, view.H - ScrollbarWidth - ScrollbarGap);
        }

        public override int IndexAt(float x, float y)
        {
            UiRect view = View;
            if (!view.Contains(x, y)) return -1;
            float along = x - view.X + _scroll;
            int index = (int)MathF.Floor(along / Pitch);
            if (index < 0 || index >= _count || along - index * Pitch >= CardWidth) return -1;
            return index;
        }

        protected override int FirstVisible() => Math.Min(_count - 1, (int)MathF.Floor(_scroll / Pitch));

        protected override int Move(int from, UiNav nav) =>
            nav == UiNav.Right ? from + 1 : nav == UiNav.Left ? from - 1 : -1;

        protected override void EnsureVisible(int index) => ScrollToShow(index * Pitch, index * Pitch + CardWidth);

        protected override void EmitCards(DrawList list, ITextMeasurer text)
        {
            if (DrawCard == null || _count == 0) return;
            float viewW = View.W;
            int first = (int)MathF.Floor(_scroll / Pitch);
            for (int i = Math.Max(0, first); i < _count; i++)
            {
                float left = i * Pitch - _scroll;
                if (left >= viewW) break;
                if (left + CardWidth <= 0) continue;
                DrawCard(i, CardRect(i), StateOf(i), list, text);
            }
        }
    }

    /// <summary>
    /// 카드 격자: 줄마다 여러 칸, 세로로 스크롤. 칸 수는 보이는 폭에 들어가는 만큼(최소 1). 보이는 줄의 칸만 그린다.
    /// </summary>
    public sealed class UiCardGrid : UiCardScroller
    {
        public float CellWidth = 120, CellHeight = 120;
        public float Spacing = 8;
        public CardPainter DrawCell;

        public event Action<int> CellClicked { add => CardClickedEvent += value; remove => CardClickedEvent -= value; }

        public int ItemCount { get => _count; set => SetCount(value); }

        /// <summary>오른쪽에 스크롤 막대 자리를 남기고 들어가는 칸 수.</summary>
        public int Columns => Math.Max(1, (int)MathF.Floor((View.W - ScrollbarWidth - ScrollbarGap + Spacing) / (CellWidth + Spacing)));
        public int RowCount => (_count + Columns - 1) / Columns;

        private float RowPitch => CellHeight + Spacing;
        protected override float StepSize => RowPitch;
        protected override float ContentLength => RowCount == 0 ? 0 : RowCount * RowPitch - Spacing;
        protected override float ViewLength => View.H;
        protected override bool Horizontal => false;

        public override UiRect ItemRect(int index) => CellRect(index);

        public UiRect CellRect(int index)
        {
            UiRect view = View;
            int columns = Columns;
            int row = index / columns, col = index % columns;
            return new UiRect(view.X + col * (CellWidth + Spacing), view.Y + row * RowPitch - _scroll, CellWidth, CellHeight);
        }

        public override int IndexAt(float x, float y)
        {
            UiRect view = View;
            if (!view.Contains(x, y)) return -1;
            float ax = x - view.X, ay = y - view.Y + _scroll;
            int col = (int)MathF.Floor(ax / (CellWidth + Spacing)), row = (int)MathF.Floor(ay / RowPitch);
            if (col >= Columns || ax - col * (CellWidth + Spacing) >= CellWidth || ay - row * RowPitch >= CellHeight) return -1;
            int index = row * Columns + col;
            return index >= 0 && index < _count ? index : -1;
        }

        protected override int FirstVisible() => Math.Min(_count - 1, (int)MathF.Floor(_scroll / RowPitch) * Columns);

        protected override int Move(int from, UiNav nav)
        {
            int columns = Columns;
            switch (nav)
            {
                case UiNav.Right: return from % columns == columns - 1 ? -1 : from + 1;
                case UiNav.Left: return from % columns == 0 ? -1 : from - 1;
                case UiNav.Down: return from + columns;
                case UiNav.Up: return from - columns;
                default: return -1;
            }
        }

        protected override void EnsureVisible(int index)
        {
            int row = index / Columns;
            ScrollToShow(row * RowPitch, row * RowPitch + CellHeight);
        }

        protected override void EmitCards(DrawList list, ITextMeasurer text)
        {
            if (DrawCell == null || _count == 0) return;
            int columns = Columns;
            int firstRow = (int)MathF.Floor(_scroll / RowPitch);
            int lastRow = Math.Min(RowCount - 1, (int)MathF.Ceiling((_scroll + View.H) / RowPitch) - 1);
            for (int row = Math.Max(0, firstRow); row <= lastRow; row++)
            {
                if (row * RowPitch + CellHeight - _scroll <= 0) continue;
                for (int col = 0; col < columns; col++)
                {
                    int index = row * columns + col;
                    if (index >= _count) return;
                    DrawCell(index, CellRect(index), StateOf(index), list, text);
                }
            }
        }
    }
}
