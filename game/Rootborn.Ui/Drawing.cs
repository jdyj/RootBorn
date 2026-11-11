using System.Collections.Generic;

namespace Rootborn.Ui
{
    /// <summary>글자 폭은 그리는 쪽만 안다. 값은 논리 좌표 단위.</summary>
    public interface ITextMeasurer
    {
        float Width(string text, float fontSize);
        float LineHeight(float fontSize);
    }

    public enum DrawKind { Panel, Text, Fill, PushClip, PopClip, Image }

    /// <summary>
    /// 그리기 명령 하나. Panel: Rect에 Skin 9조각을 Color로 물들여 그린다. Text: (Rect.X, Rect.Y)가 글줄 상자의 왼쪽 위, Skin은 글자가 올라앉은 패널 그림(없으면 null).
    /// Fill: Rect를 Color로 칠한다. PushClip/PopClip: Rect 밖을 잘라 낸다.
    /// Image: Rect에 그림(Skin = 그림 id)을 Color로 물들여 그린다. Source가 비어 있지 않으면 그 그림 안의 칸만 그린다.
    /// 그림 id가 어느 PNG의 어디인지는 그리는 쪽(Host)이 정한다.
    /// </summary>
    public readonly record struct DrawCommand(DrawKind Kind, UiRect Rect, string Skin, string Text, float FontSize, UiColor Color, PixelRect Source = default);

    public sealed class DrawList
    {
        public readonly List<DrawCommand> Commands = new List<DrawCommand>(256);

        public void Clear() => Commands.Clear();

        public void Panel(UiRect rect, string skin, UiColor tint) =>
            Commands.Add(new DrawCommand(DrawKind.Panel, rect, skin, null, 0, tint));

        /// <param name="onSkin">글자가 올라앉은 패널 그림 이름(버튼 글자 등). 그리는 쪽이 그 그림에 맞는 글자 색을 정해 두었으면 그 색으로 그린다
        /// (Unity 도시형 몰딩 테마의 버튼 글자 색). 비우면 color 그대로.</param>
        public void Text(float x, float y, string text, float fontSize, UiColor color, string onSkin = null)
        {
            if (!string.IsNullOrEmpty(text)) Commands.Add(new DrawCommand(DrawKind.Text, new UiRect(x, y, 0, 0), onSkin, text, fontSize, color));
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

        public void Image(UiRect rect, string imageId, PixelRect source, UiColor tint)
        {
            if (!string.IsNullOrEmpty(imageId)) Commands.Add(new DrawCommand(DrawKind.Image, rect, imageId, null, 0, tint, source));
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
        /// <summary>대화창(둥근 밝은 상자).</summary>
        public const string Dialogue = "dialogue";
    }

    public static class UiTheme
    {
        public static readonly UiColor Ink = new UiColor(40, 36, 52);
        public static readonly UiColor InkMuted = new UiColor(132, 128, 150);
        /// <summary>
        /// 누를 수 없는 단추·줄의 글자(이름 붙은 색). 흐린 글자(InkMuted)와 따로 둔다: 비활성 바탕 위에서도 읽혀야 한다.
        /// 실제 색은 테마가 정한다(스킨의 disabledTextColor, 없으면 textColors.inkDisabled).
        /// </summary>
        public static readonly UiColor InkDisabled = new UiColor(84, 80, 98);
        public static readonly UiColor InkOnDark = new UiColor(235, 228, 242);
        public static readonly UiColor Accent = new UiColor(45, 110, 215);
        public static readonly UiColor White = new UiColor(255, 255, 255);

        public static readonly UiColor ButtonNormal = new UiColor(226, 226, 234);
        public static readonly UiColor ButtonHovered = new UiColor(255, 255, 255);
        public static readonly UiColor ButtonPressed = new UiColor(168, 168, 186);
        /// <summary>누를 수 없는 단추의 바탕 물들임(이름 붙은 색). 테마가 스킨마다 바꿀 수 있다(disabledTint).</summary>
        public static readonly UiColor ButtonDisabled = new UiColor(150, 150, 156, 150);

        public const float FocusOutline = 3f;
        public const float PressedOffset = 2f;
    }
}
