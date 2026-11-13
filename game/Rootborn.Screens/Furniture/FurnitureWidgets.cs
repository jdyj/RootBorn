using System;
using Rootborn.Core.Minigames;
using Rootborn.Ui;

namespace Rootborn.Screens.Furniture
{
    /// <summary>
    /// 월드 위 배치 화면이 Host에게 묻는 것: 화면 좌표(논리 1920x1080 기준)와 가구 칸 사이 바꾸기. Host가 지금 카메라로 계산한다.
    /// 칸 좌표는 가구 칸(FurnitureRoom, y가 위로).
    /// </summary>
    public interface IPlacementView
    {
        /// <summary>지금 방(가구를 놓을 수 있는 맵에 있을 때). 없으면 null.</summary>
        FurnitureRoom Room { get; }

        /// <summary>화면 좌표가 덮는 칸. 맵 밖이면 false.</summary>
        bool TryCellAt(float x, float y, out GridPoint cell);

        /// <summary>칸의 화면 사각형(논리 좌표).</summary>
        UiRect CellRect(GridPoint cell);

        /// <summary>
        /// 캐릭터가 바라보는 칸(키보드로 놓기·고르기·옮기기 — 상호작용 키). 방 밖을 보거나 모르면 false. 기본은 모른다.
        /// </summary>
        bool TryFacingCell(out GridPoint cell)
        {
            cell = default;
            return false;
        }
    }

    /// <summary>
    /// 분류 단추(가구 / 보관): 고른 분류는 밝게 그리고 안쪽에 테두리를 두른다(ui-standards: 고른 것 표시). 글자가 단추보다 길면 말줄임으로 줄인다
    /// (글자나 단추 크기가 바뀔 때만 다시 줄인다). 그림은 스킨 이름만 쓴다.
    /// </summary>
    public sealed class FurnitureCategoryButton : UiNode
    {
        public string Text;
        public float FontSize = 20f;
        public bool Selected;
        public string Skin = UiSkin.Button;
        /// <summary>단추 안쪽 여백(글자 좌우).</summary>
        public float TextInset = 10f;

        private string _fitFor, _fitted;
        private float _fitWidth, _fitSize;

        public FurnitureCategoryButton() { Interactive = true; Focusable = true; }

        public override bool IsSurface => true;

        public override void Emit(DrawList list, ITextMeasurer text)
        {
            UiColor tint = !IsActive ? UiTheme.ButtonDisabled : Selected ? UiTheme.ButtonHovered : Pressed ? UiTheme.ButtonPressed : Hovered ? UiTheme.ButtonHovered : UiTheme.ButtonNormal;
            list.Panel(Rect, Skin, tint);
            string shown = Fit(text);
            if (shown.Length > 0)
            {
                float x = Rect.X + (Rect.W - text.Width(shown, FontSize)) / 2f;
                float y = Rect.Y + (Rect.H - text.LineHeight(FontSize)) / 2f + (Pressed ? UiTheme.PressedOffset : 0f);
                list.Text(x, y, shown, FontSize, IsActive ? UiTheme.Ink : UiTheme.InkDisabled, Skin);
            }
            if (Selected) list.Outline(Rect.Inset(6f), UiTheme.FocusOutline, UiTheme.Ink);
            if (FocusRing) list.Outline(Rect, UiTheme.FocusOutline, UiTheme.Accent);
        }

        private string Fit(ITextMeasurer text)
        {
            if (string.IsNullOrEmpty(Text)) return string.Empty;
            float width = Rect.W - TextInset * 2f;
            if (_fitFor == Text && _fitWidth == width && _fitSize == FontSize) return _fitted;
            _fitFor = Text; _fitWidth = width; _fitSize = FontSize;
            if (text.Width(Text, FontSize) <= width) return _fitted = Text;
            int length = Text.Length;
            while (length > 0 && text.Width(Text.Substring(0, length) + "…", FontSize) > width) length--;
            return _fitted = Text.Substring(0, length) + "…";
        }
    }
}
