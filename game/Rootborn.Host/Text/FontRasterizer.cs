using System;
using System.IO;
using StbTrueTypeSharp;

namespace Rootborn.Host.Text
{
    public sealed class GlyphBitmap
    {
        public int Width, Height, OffsetX, OffsetY;
        public float Advance;
        public byte[] Alpha; // Width*Height, 0~255
    }

    /// <summary>TTF/TTC/OTF에서 글자 하나의 그림을 뽑는다. 화면이 없어도 돈다.</summary>
    public sealed unsafe class FontRasterizer
    {
        private readonly StbTrueType.stbtt_fontinfo _info;

        public FontRasterizer(string path)
        {
            byte[] data = File.ReadAllBytes(path);
            int offset;
            fixed (byte* p = data) offset = StbTrueType.stbtt_GetFontOffsetForIndex(p, 0);
            _info = offset < 0 ? null : StbTrueType.CreateFont(data, offset);
            if (_info == null) throw new InvalidOperationException("글꼴을 읽지 못했다: " + path);
        }

        public bool HasGlyph(int codepoint) => StbTrueType.stbtt_FindGlyphIndex(_info, codepoint) != 0;

        /// <summary>pixelHeight는 글줄 상자(위 끝~아래 끝)의 높이. ascent는 상자 위에서 기준선까지.</summary>
        public float Ascent(float pixelHeight)
        {
            float scale = StbTrueType.stbtt_ScaleForPixelHeight(_info, pixelHeight);
            int ascent, descent, gap;
            StbTrueType.stbtt_GetFontVMetrics(_info, &ascent, &descent, &gap);
            return ascent * scale;
        }

        public GlyphBitmap Rasterize(int codepoint, float pixelHeight)
        {
            float scale = StbTrueType.stbtt_ScaleForPixelHeight(_info, pixelHeight);
            int glyph = StbTrueType.stbtt_FindGlyphIndex(_info, codepoint);
            int advance, bearing, x0, y0, x1, y1;
            StbTrueType.stbtt_GetGlyphHMetrics(_info, glyph, &advance, &bearing);
            StbTrueType.stbtt_GetGlyphBitmapBox(_info, glyph, scale, scale, &x0, &y0, &x1, &y1);
            var result = new GlyphBitmap { Width = x1 - x0, Height = y1 - y0, OffsetX = x0, OffsetY = y0, Advance = advance * scale };
            result.Alpha = new byte[Math.Max(1, result.Width * result.Height)];
            if (result.Width > 0 && result.Height > 0)
                fixed (byte* p = result.Alpha)
                    StbTrueType.stbtt_MakeGlyphBitmap(_info, p, result.Width, result.Height, result.Width, scale, scale, glyph);
            return result;
        }
    }
}
