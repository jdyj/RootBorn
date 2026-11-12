using Rootborn.Ui;

namespace Rootborn.Screens
{
    /// <summary>
    /// 패널 하나의 글자 자리(패널 왼쪽 위 기준): 패널 크기 − 테마의 스킨 여백(ScreenServices.PaddingOf). 화면이 패널 안 여백을 숫자로
    /// 적지 않고 이것으로 자리를 잡는다(ui-standards "패널 안 여백은 테마의 테두리에서 얻는다"). 여백이 두꺼운 테마에서는 안쪽이 좁아진다.
    /// </summary>
    public readonly struct PanelArea
    {
        public PanelArea(float width, float height, UiInsets padding)
        {
            Width = width; Height = height; Padding = padding;
        }

        public float Width { get; }
        public float Height { get; }
        public UiInsets Padding { get; }

        public float Left => Padding.Left;
        public float Top => Padding.Top;
        /// <summary>안쪽 영역의 오른쪽 끝(패널 왼쪽 기준 x).</summary>
        public float Right => Width - Padding.Right;
        /// <summary>안쪽 영역의 아래 끝(패널 위 기준 y).</summary>
        public float Bottom => Height - Padding.Bottom;
        public float InnerWidth => Width - Padding.Horizontal;
        public float InnerHeight => Height - Padding.Vertical;

        // Unity 가운데 기준 좌표(UnityLayout.Centered: x는 오른쪽, y는 위로 +)로 옮긴 화면을 위한 도움.
        /// <summary>높이 h인 요소의 위가 안쪽 위에서 fromTop만큼 아래일 때 그 가운데의 Unity y.</summary>
        public float CenterYFromTop(float h, float fromTop = 0f) => Height / 2f - Top - fromTop - h / 2f;
        /// <summary>높이 h인 요소의 아래가 안쪽 아래에서 fromBottom만큼 위일 때 그 가운데의 Unity y.</summary>
        public float CenterYFromBottom(float h, float fromBottom = 0f) => -Height / 2f + Padding.Bottom + fromBottom + h / 2f;
        /// <summary>폭 w인 요소의 왼쪽이 안쪽 왼쪽에서 fromLeft만큼 오른쪽일 때 그 가운데의 Unity x.</summary>
        public float CenterXFromLeft(float w, float fromLeft = 0f) => -Width / 2f + Left + fromLeft + w / 2f;
        /// <summary>폭 w인 요소의 오른쪽이 안쪽 오른쪽에서 fromRight만큼 왼쪽일 때 그 가운데의 Unity x.</summary>
        public float CenterXFromRight(float w, float fromRight = 0f) => Width / 2f - Padding.Right - fromRight - w / 2f;
    }
}
