using System;
using System.Collections.Generic;
using Rootborn.Ui;

namespace Rootborn.Screens.Furniture
{
    /// <summary>
    /// 단추 줄 패널: 단추들의 화면 자리(Unity 가운데 기준 x — 패널 부모 기준)를 지키고, 패널은 보이는 단추들의 폭 + 테마 여백(좌우)으로 줄인다.
    /// 보이는 단추가 없으면 패널을 숨긴다(빈 긴 상자를 보이지 않는다). 높이와 세로 자리는 정한 값 그대로. 단추가 바뀔 때 Fit을 다시 부른다.
    /// </summary>
    public sealed class ToolRow
    {
        private readonly UiNode _panel;
        private readonly float _centerY, _height;
        private readonly UiInsets _padding;
        private readonly List<(UiNode Button, float CenterX, float W, float H)> _buttons = new List<(UiNode, float, float, float)>(4);

        public ToolRow(UiNode panel, float centerY, float height, UiInsets padding)
        {
            _panel = panel ?? throw new ArgumentNullException(nameof(panel));
            _centerY = centerY; _height = height; _padding = padding;
        }

        /// <summary>단추 하나: 패널 부모 가운데 기준 x(Unity 좌표)와 크기. 단추는 이미 패널의 자식이어야 한다.</summary>
        public ToolRow Add(UiNode button, float centerX, float w, float h)
        {
            _buttons.Add((button, centerX, w, h));
            return this;
        }

        public void Fit()
        {
            float left = float.MaxValue, right = float.MinValue;
            foreach (var b in _buttons)
            {
                if (!b.Button.Visible) continue;
                left = MathF.Min(left, b.CenterX - b.W / 2f);
                right = MathF.Max(right, b.CenterX + b.W / 2f);
            }
            _panel.Visible = left <= right;
            if (!_panel.Visible) return;
            float x0 = left - _padding.Left, x1 = right + _padding.Right, center = (x0 + x1) / 2f;
            _panel.Centered(center, _centerY, x1 - x0, _height);
            foreach (var b in _buttons) b.Button.Centered(b.CenterX - center, 0f, b.W, b.H);
        }
    }
}
