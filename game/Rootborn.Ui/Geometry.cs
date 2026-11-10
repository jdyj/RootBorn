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
    /// 창 크기에 맞춘 화면 배율. Unity CanvasScaler(Scale With Screen Size, 1920x1080, Match 0.5)와 같은 계산이다.
    /// 글자와 배치는 Scale을 그대로 쓰고, 픽셀아트 타일은 TileScale(정수)로만 키운다.
    /// </summary>
    public readonly record struct ScreenFit(float Scale, float LogicalWidth, float LogicalHeight, int TileScale)
    {
        public const float ReferenceWidth = 1920f, ReferenceHeight = 1080f;

        /// <summary>16px 타일을 기준 해상도에서 몇 배로 그리는가.</summary>
        public const int ReferenceTileScale = 3;

        public static ScreenFit For(int windowWidth, int windowHeight)
        {
            float scale = MathF.Sqrt(windowWidth / ReferenceWidth * (windowHeight / ReferenceHeight));
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
