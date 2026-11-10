using System.Collections.Generic;

namespace Rootborn.Ui
{
    /// <summary>글자 폭은 그리는 쪽만 안다. 값은 논리 좌표 단위.</summary>
    public interface ITextMeasurer
    {
        float Width(string text, float fontSize);
        float LineHeight(float fontSize);
    }

    public enum DrawKind { Panel, Text, Fill, PushClip, PopClip }

    /// <summary>
    /// 그리기 명령 하나. Panel: Rect에 Skin 9조각을 Color로 물들여 그린다. Text: (Rect.X, Rect.Y)가 글줄 상자의 왼쪽 위.
    /// Fill: Rect를 Color로 칠한다. PushClip/PopClip: Rect 밖을 잘라 낸다.
    /// </summary>
    public readonly record struct DrawCommand(DrawKind Kind, UiRect Rect, string Skin, string Text, float FontSize, UiColor Color);

    public sealed class DrawList
    {
        public readonly List<DrawCommand> Commands = new List<DrawCommand>(256);

        public void Clear() => Commands.Clear();

        public void Panel(UiRect rect, string skin, UiColor tint) =>
            Commands.Add(new DrawCommand(DrawKind.Panel, rect, skin, null, 0, tint));

        public void Text(float x, float y, string text, float fontSize, UiColor color)
        {
            if (!string.IsNullOrEmpty(text)) Commands.Add(new DrawCommand(DrawKind.Text, new UiRect(x, y, 0, 0), null, text, fontSize, color));
        }

        public void Fill(UiRect rect, UiColor color) =>
            Commands.Add(new DrawCommand(DrawKind.Fill, rect, null, null, 0, color));

        /// <summary>사각형 바깥쪽에 두께만큼 테두리를 두른다(칠하기 4번).</summary>
        public void Outline(UiRect rect, float thickness, UiColor color)
        {
            Fill(new UiRect(rect.X - thickness, rect.Y - thickness, rect.W + thickness * 2, thickness), color);
            Fill(new UiRect(rect.X - thickness, rect.Bottom, rect.W + thickness * 2, thickness), color);
            Fill(new UiRect(rect.X - thickness, rect.Y, thickness, rect.H), color);
            Fill(new UiRect(rect.Right, rect.Y, thickness, rect.H), color);
        }

        public void PushClip(UiRect rect) => Commands.Add(new DrawCommand(DrawKind.PushClip, rect, null, null, 0, default));
        public void PopClip() => Commands.Add(new DrawCommand(DrawKind.PopClip, default, null, null, 0, default));
    }

    /// <summary>패널 그림의 이름. 어느 PNG의 어느 좌표인지는 그리는 쪽(Host)이 정한다.</summary>
    public static class UiSkin
    {
        public const string Panel = "panel";
        public const string Button = "button";
        public const string Slot = "slot";
        public const string Field = "field";
    }

    public static class UiTheme
    {
        public static readonly UiColor Ink = new UiColor(40, 36, 52);
        public static readonly UiColor InkMuted = new UiColor(132, 128, 150);
        public static readonly UiColor InkOnDark = new UiColor(235, 228, 242);
        public static readonly UiColor Accent = new UiColor(45, 110, 215);
        public static readonly UiColor White = new UiColor(255, 255, 255);

        public static readonly UiColor ButtonNormal = new UiColor(226, 226, 234);
        public static readonly UiColor ButtonHovered = new UiColor(255, 255, 255);
        public static readonly UiColor ButtonPressed = new UiColor(168, 168, 186);
        public static readonly UiColor ButtonDisabled = new UiColor(150, 150, 156, 150);

        public const float FocusOutline = 3f;
        public const float PressedOffset = 2f;
    }
}
