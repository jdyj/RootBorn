using System;
using System.Collections.Generic;

namespace Rootborn.Ui
{
    /// <summary>
    /// 화면 하나의 UI. 배치, 포인터 판정, 클릭, 포커스 이동, 그리기 명령 내기를 맡는다.
    /// 입력은 값으로만 받는다(포인터 좌표·버튼·휠, 이동 방향, 실행, 글자). 장치는 모른다.
    /// </summary>
    public sealed class UiTree
    {
        public readonly UiNode Root = new UiNode { Id = "root" };

        public UiNode Focused { get; private set; }
        public UiNode Hovered { get; private set; }

        /// <summary>포인터 클릭이나 실행 키로 요소가 실행됐다.</summary>
        public event Action<UiNode> Clicked;
        public event Action<UiNode> FocusChanged;

        private readonly List<UiNode> _focusables = new List<UiNode>(64);
        private UiNode _pressed;
        private float _pressX, _pressY;
        private bool _wasDown, _dragging;

        public void Layout(float width, float height) => Root.Layout(new UiRect(0, 0, width, height));

        /// <summary>그 점에서 맨 위에 있는 요소. 나중에 추가된 자식이 위다.</summary>
        public UiNode HitTest(float x, float y) => HitTest(Root, x, y);

        private static UiNode HitTest(UiNode node, float x, float y)
        {
            if (!node.Visible) return null;
            for (int i = node.Children.Count - 1; i >= 0; i--)
            {
                UiNode hit = HitTest(node.Children[i], x, y);
                if (hit != null) return hit;
            }
            return node.Interactive && node.Rect.Contains(x, y) ? node : null;
        }

        /// <summary>프레임마다 한 번. 좌표는 논리 좌표, wheel은 이번 프레임에 굴린 칸 수.</summary>
        public void UpdatePointer(float x, float y, bool down, float wheel = 0f)
        {
            UiNode over = HitTest(x, y);
            UiNode activeOver = over != null && over.IsActive ? over : null;

            if (Hovered != activeOver)
            {
                if (Hovered != null) { Hovered.Hovered = false; Hovered.OnPointer(x, y, false); }
                Hovered = activeOver;
                if (Hovered != null) Hovered.Hovered = true;
            }
            Hovered?.OnPointer(x, y, true);

            if (wheel != 0f)
                for (UiNode n = activeOver; n != null; n = n.Parent)
                    if (n.OnWheel(wheel)) break;

            if (down && !_wasDown)
            {
                _pressed = activeOver;
                _pressX = x; _pressY = y;
                _dragging = false;
                SetFocus(activeOver != null && activeOver.Focusable ? activeOver : null);
                _pressed?.OnPress(x, y);
            }

            if (down && _pressed != null)
            {
                if (_pressed.OnDrag(x, y, x - _pressX, y - _pressY)) _dragging = true;
                _pressed.Pressed = !_dragging && activeOver == _pressed;
            }

            if (!down && _wasDown && _pressed != null)
            {
                UiNode released = _pressed;
                _pressed = null;
                released.Pressed = false;
                if (_dragging) released.OnDragEnd();
                else if (activeOver == released) Activate(released, x, y);
                _dragging = false;
            }
            _wasDown = down;
        }

        private void Activate(UiNode node, float x, float y)
        {
            node.OnClick(x, y);
            Clicked?.Invoke(node);
        }

        /// <summary>포커스된 요소를 실행한다(Enter).</summary>
        public void Submit()
        {
            if (Focused == null || !Focused.IsActive) return;
            UiNode node = Focused;
            node.OnSubmit();
            Clicked?.Invoke(node);
        }

        public void SetFocus(UiNode node)
        {
            if (node != null && (!node.Focusable || !node.IsActive)) return;
            if (node == Focused) return;
            if (Focused != null) { Focused.Focused = false; Focused.OnFocusChanged(false); }
            Focused = node;
            if (Focused != null) { Focused.Focused = true; Focused.OnFocusChanged(true); }
            FocusChanged?.Invoke(Focused);
        }

        /// <summary>방향키와 Tab. 방향키는 포커스된 요소가 먼저 처리할 기회를 갖는다.</summary>
        public void Navigate(UiNav nav)
        {
            _focusables.Clear();
            Collect(Root, _focusables);
            if (_focusables.Count == 0) return;

            int current = Focused == null ? -1 : _focusables.IndexOf(Focused);
            if (nav == UiNav.Next || nav == UiNav.Previous)
            {
                int count = _focusables.Count;
                int next = current < 0 ? (nav == UiNav.Next ? 0 : count - 1) : (current + (nav == UiNav.Next ? 1 : count - 1)) % count;
                SetFocus(_focusables[next]);
                return;
            }

            if (current < 0) { SetFocus(_focusables[0]); return; }
            if (Focused.OnNavigate(nav)) return;
            UiNode target = Nearest(Focused, nav);
            if (target != null) SetFocus(target);
        }

        private static void Collect(UiNode node, List<UiNode> into)
        {
            if (!node.Visible || !node.Enabled) return;
            if (node.Focusable) into.Add(node);
            for (int i = 0; i < node.Children.Count; i++) Collect(node.Children[i], into);
        }

        /// <summary>
        /// 그 방향에 있는 요소 중, 같은 줄(가로 이동) 또는 같은 칸(세로 이동)에 걸친 것을 먼저 고르고 그중 가장 가까운 것.
        /// 걸친 것이 없으면 비스듬한 요소 중 가까운 것.
        /// </summary>
        private UiNode Nearest(UiNode from, UiNav nav)
        {
            bool horizontal = nav == UiNav.Left || nav == UiNav.Right;
            float sign = nav == UiNav.Right || nav == UiNav.Down ? 1f : -1f;
            UiRect a = from.Rect;
            UiNode best = null;
            float bestScore = float.MaxValue;
            bool bestAligned = false;
            for (int i = 0; i < _focusables.Count; i++)
            {
                UiNode candidate = _focusables[i];
                if (candidate == from) continue;
                UiRect b = candidate.Rect;
                float along = horizontal ? (b.CenterX - a.CenterX) * sign : (b.CenterY - a.CenterY) * sign;
                if (along <= 0.5f) continue;
                float gap = horizontal
                    ? MathF.Max(0, MathF.Max(b.Y - a.Bottom, a.Y - b.Bottom))
                    : MathF.Max(0, MathF.Max(b.X - a.Right, a.X - b.Right));
                bool aligned = gap <= 0f;
                float score = along + gap * 2f;
                if (best == null || (aligned && !bestAligned) || (aligned == bestAligned && score < bestScore))
                {
                    best = candidate; bestScore = score; bestAligned = aligned;
                }
            }
            return best;
        }

        public void TextInput(string text) { if (Focused != null && Focused.IsActive) Focused.OnTextInput(text); }
        public void TextBackspace() { if (Focused != null && Focused.IsActive) Focused.OnTextBackspace(); }
        public void TextComposition(string text) { if (Focused != null && Focused.IsActive) Focused.OnTextComposition(text); }

        /// <summary>보이는 요소의 그리기 명령을 뒤에서 앞 순서로 채운다.</summary>
        public void Emit(DrawList list, ITextMeasurer text)
        {
            list.Clear();
            Emit(Root, list, text);
        }

        private static void Emit(UiNode node, DrawList list, ITextMeasurer text)
        {
            if (!node.Visible) return;
            node.Emit(list, text);
            for (int i = 0; i < node.Children.Count; i++) Emit(node.Children[i], list, text);
        }
    }
}
