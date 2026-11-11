using System;

namespace Rootborn.Ui
{
    /// <summary>논리 좌표(1920x1080 기준)의 사각형. y는 아래로 커진다.</summary>
    public readonly record struct UiRect(float X, float Y, float W, float H)
    {
        public float Right => X + W;
        public float Bottom => Y + H;
        public float CenterX => X + W / 2f;
        public float CenterY => Y + H / 2f;

        public bool Contains(float px, float py) => px >= X && py >= Y && px < X + W && py < Y + H;

        public UiRect Inset(float amount) => new UiRect(X + amount, Y + amount, MathF.Max(0, W - amount * 2), MathF.Max(0, H - amount * 2));
    }

    /// <summary>화면 픽셀의 사각형.</summary>
    public readonly record struct PixelRect(int X, int Y, int W, int H);

    public readonly record struct UiColor(byte R, byte G, byte B, byte A = 255);

    /// <summary>
    /// 창 크기에 맞춘 화면 배율. 기준 1920x1080이 늘 창 안에 통째로 들어가게 가로·세로 배율 중 작은 쪽을 쓴다
    /// (Unity CanvasScaler의 Expand). 16:9에서는 Unity 판(Match 0.5)과 같은 값이고, 다른 비율에서는 긴 쪽에 논리 공간이 더 생긴다.
    /// 배율은 하나라 UI가 늘어나지 않고, 논리 크기 × 배율 = 창 크기라 창 전체가 UI 영역이다(남는 띠 없음 — 가장자리에 붙인 HUD는 창 끝으로 간다).
    /// Match 0.5를 쓰지 않는 까닭: 21:9(2560x1080)에서 논리 높이가 935가 되어, 가운데 기준으로 1080 높이에 맞춰 둔 요소(HUD 가구 배치 버튼)가
    /// 화면 위로 나갔다. 16:10에서는 논리 너비가 1822가 되어 1920 너비를 가정한 배치가 잘릴 수 있었다.
    /// 글자와 배치는 Scale을 그대로 쓰고, 픽셀아트 타일은 TileScale(정수)로만 키운다.
    /// </summary>
    public readonly record struct ScreenFit(float Scale, float LogicalWidth, float LogicalHeight, int TileScale)
    {
        public const float ReferenceWidth = 1920f, ReferenceHeight = 1080f;

        /// <summary>16px 타일을 기준 해상도에서 몇 배로 그리는가.</summary>
        public const int ReferenceTileScale = 3;

        public static ScreenFit For(int windowWidth, int windowHeight)
        {
            float scale = MathF.Min(windowWidth / ReferenceWidth, windowHeight / ReferenceHeight);
            int tile = Math.Max(1, (int)MathF.Round(ReferenceTileScale * scale));
            return new ScreenFit(scale, windowWidth / scale, windowHeight / scale, tile);
        }

        public int ToPixel(float logical) => (int)MathF.Round(logical * Scale);

        /// <summary>변마다 따로 반올림한다. 맞닿은 두 사각형이 픽셀에서도 맞닿는다.</summary>
        public PixelRect ToPixels(UiRect rect)
        {
            int x0 = ToPixel(rect.X), y0 = ToPixel(rect.Y);
            return new PixelRect(x0, y0, ToPixel(rect.Right) - x0, ToPixel(rect.Bottom) - y0);
        }
    }
}
