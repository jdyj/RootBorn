using System;

namespace Rootborn.Ui
{
    /// <summary>
    /// 높이가 같은 줄을 세로로 늘어놓은 스크롤 목록. 줄은 요소(UiNode)가 아니다:
    /// 줄 수만 알고 있다가 보이는 줄만 RowText로 물어 그리기 명령을 낸다. 줄이 500개여도 한 번에 다루는 것은 화면에 걸친 줄뿐이다.
    /// 휠, 누른 채 끌기, 방향키로 움직이고 줄을 클릭하거나 Enter로 실행한다.
    /// </summary>
    public sealed class UiScrollList : UiNode
    {
        public float RowHeight = 48;
        /// <summary>테두리 그림 안쪽으로 들이는 거리. 패널 테두리는 정수 배율이라 창 크기에 따라 논리 좌표로 15~18이 된다.</summary>
        public float Padding = 20;
        public float FontSize = 20;
        public float TextPaddingX = 16;
        public string Skin = UiSkin.Field;

        /// <summary>휠 한 칸에 넘기는 줄 수.</summary>
        public float RowsPerNotch = 3;

        /// <summary>이만큼 넘게 움직여야 끌기로 본다. 그 안쪽은 클릭이다.</summary>
        public float DragThreshold = 8;

        /// <summary>줄 번호(0부터)를 받아 그 줄의 글자를 돌려준다. 보이는 줄만 물어본다.</summary>
        public Func<int, string> RowText;

        /// <summary>
        /// 줄 번호를 받아 그 줄 왼쪽에 그릴 그림 id를 돌려준다(없으면 null — 그 줄은 그림 자리만 비운다). 보이는 줄만 물어본다.
        /// 이것이 null이면 그림 자리가 없고 글자는 지금처럼 줄 왼쪽 여백에서 시작한다.
        /// </summary>
        public Func<int, string> RowIcon;

        /// <summary>줄 그림의 원래 크기(비율만 쓴다). null이거나 0이면 그림 칸(IconSize 정사각형)에 맞춰 늘린다.</summary>
        public Func<int, (float W, float H)> RowIconSource;

        /// <summary>줄 그림 칸의 한 변. 글자는 그림 칸 + IconGap 오른쪽에서 시작한다.</summary>
        public float IconSize = 32;
        public float IconGap = 10;

        /// <summary>줄을 클릭했거나, 고른 줄에서 실행 키를 눌렀다.</summary>
        public event Action<int> RowClicked;

        private static readonly UiColor RowEven = new UiColor(0, 0, 0, 0);
        private static readonly UiColor RowOdd = new UiColor(60, 60, 100, 22);
        private static readonly UiColor RowHover = new UiColor(45, 110, 215, 50);
        private static readonly UiColor RowSelected = new UiColor(45, 110, 215, 110);
        private static readonly UiColor ScrollTrack = new UiColor(60, 60, 100, 40);
        private static readonly UiColor ScrollThumb = new UiColor(86, 89, 114);
        private const float ScrollbarWidth = 8;

        private int _rowCount;
        private float _scroll, _pressScroll;
        private int _selected = -1, _hoverRow = -1;
        private bool _dragging;

        public UiScrollList() { Interactive = true; Focusable = true; }

        public int RowCount
        {
            get => _rowCount;
            set
            {
                _rowCount = Math.Max(0, value);
                if (_selected >= _rowCount) _selected = -1;
                _scroll = Clamp(_scroll);
            }
        }

        /// <summary>내용이 위로 밀려 올라간 거리.</summary>
        public float ScrollOffset
        {
            get => _scroll;
            set => _scroll = Clamp(value);
        }

        /// <summary>키보드로 고른 줄. 없으면 -1.</summary>
        public int SelectedIndex
        {
            get => _selected;
            set => _selected = value >= 0 && value < _rowCount ? value : -1;
        }

        public UiRect View => Rect.Inset(Padding);
        public float MaxScroll => MathF.Max(0, _rowCount * RowHeight - View.H);
        public int FirstVisibleRow => (int)MathF.Floor(_scroll / RowHeight);
        public int LastVisibleRow => Math.Min(_rowCount, (int)MathF.Ceiling((_scroll + View.H) / RowHeight)) - 1;

        private float Clamp(float offset) => MathF.Max(0, MathF.Min(offset, MaxScroll));

        /// <summary>그 점에 있는 줄 번호. 보이는 영역 밖이거나 줄이 없으면 -1.</summary>
        public int RowAt(float x, float y)
        {
            UiRect view = View;
            if (!view.Contains(x, y)) return -1;
            int row = (int)MathF.Floor((y - view.Y + _scroll) / RowHeight);
            return row >= 0 && row < _rowCount ? row : -1;
        }

        /// <summary>줄이 화면에서 차지하는 사각형(보이는 부분만). 전혀 보이지 않으면 false.</summary>
        public bool TryGetRowRect(int index, out UiRect rect)
        {
            UiRect view = View;
            float top = MathF.Max(view.Y, view.Y + index * RowHeight - _scroll);
            float bottom = MathF.Min(view.Bottom, view.Y + (index + 1) * RowHeight - _scroll);
            rect = new UiRect(view.X, top, view.W, bottom - top);
            return index >= 0 && index < _rowCount && bottom > top;
        }

        public void EnsureVisible(int index)
        {
            float top = index * RowHeight, bottom = top + RowHeight;
            if (top < _scroll) ScrollOffset = top;
            else if (bottom > _scroll + View.H) ScrollOffset = bottom - View.H;
        }

        public override bool OnWheel(float notches)
        {
            ScrollOffset = _scroll - notches * RowHeight * RowsPerNotch;
            return true;
        }

        public override void OnPress(float x, float y)
        {
            _pressScroll = _scroll;
            _dragging = false;
        }

        public override bool OnDrag(float x, float y, float dx, float dy)
        {
            if (!_dragging && MathF.Abs(dy) > DragThreshold) _dragging = true;
            if (_dragging) ScrollOffset = _pressScroll - dy;
            return _dragging;
        }

        public override void OnDragEnd() => _dragging = false;

        public override void OnPointer(float x, float y, bool inside) => _hoverRow = inside ? RowAt(x, y) : -1;

        public override void OnClick(float x, float y)
        {
            int row = RowAt(x, y);
            if (row < 0) return;
            _selected = row;
            RowClicked?.Invoke(row);
        }

        public override void OnSubmit()
        {
            if (_selected >= 0) RowClicked?.Invoke(_selected);
        }

        public override bool OnNavigate(UiNav nav)
        {
            if (nav != UiNav.Up && nav != UiNav.Down || _rowCount == 0) return false;
            int next;
            if (_selected < 0) next = Math.Max(0, Math.Min(FirstVisibleRow, _rowCount - 1));
            else next = _selected + (nav == UiNav.Down ? 1 : -1);
            if (next < 0 || next >= _rowCount) return false; // 끝에서는 목록을 벗어난다
            _selected = next;
            EnsureVisible(next);
            return true;
        }

        private FittedText[] _fit = new FittedText[0];

        // 보이는 줄 수만큼 맞춤 자리를 든다(목록 크기가 바뀌어 보이는 줄이 늘 때만 새로 만든다).
        private void EnsureFitSlots(int visible)
        {
            int need = Math.Max(1, visible + 1);
            if (_fit.Length < need) _fit = new FittedText[need];
        }

        private void EmitIcon(DrawList list, int row, float x, float y)
        {
            string image = RowIcon(row);
            if (string.IsNullOrEmpty(image)) return;
            float w = IconSize, h = IconSize;
            if (RowIconSource != null)
            {
                (float sw, float sh) = RowIconSource(row);
                if (sw > 0 && sh > 0)
                {
                    float scale = MathF.Min(IconSize / sw, IconSize / sh);
                    w = sw * scale;
                    h = sh * scale;
                }
            }
            list.Image(new UiRect(x + (IconSize - w) / 2f, y + (IconSize - h) / 2f, w, h), image, default, UiTheme.White);
        }

        public override void Emit(DrawList list, ITextMeasurer text)
        {
            list.Panel(Rect, Skin, IsActive ? UiTheme.ButtonHovered : UiTheme.ButtonDisabled);
            UiRect view = View;
            list.PushClip(view);
            float lineHeight = text.LineHeight(FontSize);
            bool scrollable = MaxScroll > 0;
            float rowWidth = scrollable ? view.W - ScrollbarWidth - 6 : view.W;
            int first = FirstVisibleRow, last = LastVisibleRow;
            float textX = view.X + TextPaddingX + (RowIcon != null ? IconSize + IconGap : 0f);
            float textRoom = view.X + rowWidth - TextPaddingX - textX;
            EnsureFitSlots(last - first + 1);
            for (int row = first; row <= last; row++)
            {
                float y = view.Y + row * RowHeight - _scroll;
                UiColor back = row == _selected ? RowSelected : row == _hoverRow ? RowHover : (row & 1) == 1 ? RowOdd : RowEven;
                if (back.A > 0) list.Fill(new UiRect(view.X, y, rowWidth, RowHeight), back);
                if (RowIcon != null) EmitIcon(list, row, view.X + TextPaddingX, y + (RowHeight - IconSize) / 2f);
                if (RowText != null)
                {
                    // 넘치는 줄 글은 말줄임. 맞춘 결과는 줄 글·폭·크기가 바뀔 때만 다시 잰다(보이는 줄 자리마다 하나씩 들고 있다).
                    string shown = _fit[row % _fit.Length].Get(RowText(row), textRoom, FontSize, text);
                    list.Text(textX, y + (RowHeight - lineHeight) / 2f, shown, FontSize, UiTheme.Ink);
                }
            }
            list.PopClip();

            if (scrollable)
            {
                float trackX = view.Right - ScrollbarWidth;
                list.Fill(new UiRect(trackX, view.Y, ScrollbarWidth, view.H), ScrollTrack);
                float content = _rowCount * RowHeight;
                float thumbH = MathF.Max(24, view.H * view.H / content);
                float thumbY = view.Y + (view.H - thumbH) * (_scroll / MaxScroll);
                list.Fill(new UiRect(trackX, thumbY, ScrollbarWidth, thumbH), ScrollThumb);
            }
            if (FocusRing) list.Outline(Rect, UiTheme.FocusOutline, UiTheme.Accent);
        }
    }
}
