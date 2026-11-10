using System;
using System.Collections.Generic;

namespace Rootborn.Ui
{
    public enum UiNav { Up, Down, Left, Right, Next, Previous }

    /// <summary>
    /// UI 요소 하나. 배치 모델은 Unity RectTransform과 같다: 부모 사각형 안의 앵커(0~1) + 픽셀 오프셋.
    /// 좌표는 논리 해상도 기준이고 y는 아래로 커진다(오프셋은 왼쪽 위 모서리와 오른쪽 아래 모서리).
    /// </summary>
    public class UiNode
    {
        public string Id;
        public float AnchorMinX, AnchorMinY, AnchorMaxX = 1f, AnchorMaxY = 1f;
        public float OffsetMinX, OffsetMinY, OffsetMaxX, OffsetMaxY;
        public bool Visible = true;
        public bool Enabled = true;

        /// <summary>포인터를 받는다. 받는 요소는 뒤에 있는 요소를 가린다.</summary>
        public bool Interactive;

        /// <summary>키보드 포커스를 받는다.</summary>
        public bool Focusable;

        public readonly List<UiNode> Children = new List<UiNode>();
        public UiNode Parent { get; private set; }

        /// <summary>UiTree.Layout이 채운다.</summary>
        public UiRect Rect { get; private set; }

        public bool Hovered { get; internal set; }
        public bool Pressed { get; internal set; }
        public bool Focused { get; internal set; }

        public event Action Clicked;

        /// <summary>자신과 모든 조상이 보이고 활성일 때만 입력을 받는다.</summary>
        public bool IsActive
        {
            get
            {
                for (UiNode n = this; n != null; n = n.Parent)
                    if (!n.Visible || !n.Enabled) return false;
                return true;
            }
        }

        public T Add<T>(T child) where T : UiNode
        {
            child.Parent = this;
            Children.Add(child);
            return child;
        }

        /// <summary>고정 크기: 부모 안의 앵커 한 점에서 (x, y)만큼 떨어진 곳에 w x h.</summary>
        public UiNode Place(float anchorX, float anchorY, float x, float y, float w, float h)
        {
            AnchorMinX = AnchorMaxX = anchorX;
            AnchorMinY = AnchorMaxY = anchorY;
            OffsetMinX = x; OffsetMinY = y; OffsetMaxX = x + w; OffsetMaxY = y + h;
            return this;
        }

        /// <summary>부모를 채우고 변마다 안쪽으로 들인다.</summary>
        public UiNode Stretch(float left, float top, float right, float bottom)
        {
            AnchorMinX = AnchorMinY = 0f;
            AnchorMaxX = AnchorMaxY = 1f;
            OffsetMinX = left; OffsetMinY = top; OffsetMaxX = -right; OffsetMaxY = -bottom;
            return this;
        }

        public void Layout(UiRect parent)
        {
            float x0 = parent.X + parent.W * AnchorMinX + OffsetMinX;
            float y0 = parent.Y + parent.H * AnchorMinY + OffsetMinY;
            float x1 = parent.X + parent.W * AnchorMaxX + OffsetMaxX;
            float y1 = parent.Y + parent.H * AnchorMaxY + OffsetMaxY;
            Rect = new UiRect(x0, y0, x1 - x0, y1 - y0);
            for (int i = 0; i < Children.Count; i++) Children[i].Layout(Rect);
        }

        public UiNode Find(string id)
        {
            if (Id == id) return this;
            for (int i = 0; i < Children.Count; i++)
            {
                UiNode found = Children[i].Find(id);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>자기 모습을 그리기 명령으로 낸다. 자식은 UiTree가 이어서 부른다.</summary>
        public virtual void Emit(DrawList list, ITextMeasurer text) { }

        /// <summary>같은 요소에서 누르고 뗐을 때, 또는 포커스된 채 실행 키를 눌렀을 때.</summary>
        public virtual void OnClick(float x, float y) => Clicked?.Invoke();

        /// <summary>포커스된 채 실행 키를 눌렀을 때. 기본은 가운데를 클릭한 것과 같다.</summary>
        public virtual void OnSubmit() => OnClick(Rect.CenterX, Rect.CenterY);

        public virtual void OnPress(float x, float y) { }

        /// <summary>누른 채 움직이는 동안 매번 불린다. dx, dy는 누른 곳에서의 거리. true를 돌려주면 끌기가 되어 클릭이 나지 않는다.</summary>
        public virtual bool OnDrag(float x, float y, float dx, float dy) => false;

        public virtual void OnDragEnd() { }

        /// <summary>휠 한 칸이 1. 위로 굴리면 양수. 받았으면 true.</summary>
        public virtual bool OnWheel(float notches) => false;

        /// <summary>포커스된 요소가 방향키를 직접 처리했으면 true. false면 UiTree가 다른 요소로 옮긴다.</summary>
        public virtual bool OnNavigate(UiNav nav) => false;

        /// <summary>포인터가 위에 있는 동안 매번, 그리고 벗어날 때 한 번(inside=false) 불린다.</summary>
        public virtual void OnPointer(float x, float y, bool inside) { }

        public virtual void OnFocusChanged(bool focused) { }
        public virtual void OnTextInput(string text) { }
        public virtual void OnTextBackspace() { }
        public virtual void OnTextComposition(string text) { }
    }
}
