using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rootborn.Ui;

namespace Rootborn.Host.Text
{
    /// <summary>
    /// 글자를 처음 쓸 때 그림으로 만들어 텍스처에 모아 두고 그린다.
    /// 항상 화면에 찍힐 픽셀 크기로 만들어 1:1로 그린다(늘리거나 줄이지 않는다). 창 배율이 바뀌면 그 크기로 새로 만든다.
    /// </summary>
    public sealed class TextRenderer : IDisposable
    {
        private readonly struct Glyph
        {
            public readonly int Page, OffsetX, OffsetY;
            public readonly Rectangle Source;
            public readonly float Advance;
            public Glyph(int page, Rectangle source, int offsetX, int offsetY, float advance)
            {
                Page = page; Source = source; OffsetX = offsetX; OffsetY = offsetY; Advance = advance;
            }
        }

        private const int PageSize = 1024;
        private const int MinPixelSize = 8;

        private readonly GraphicsDevice _device;
        private readonly FontRasterizer _font;
        private readonly List<Texture2D> _pages = new List<Texture2D>();
        private readonly Dictionary<(int codepoint, int px), Glyph> _glyphs = new Dictionary<(int, int), Glyph>(512);
        private readonly Dictionary<int, float> _ascents = new Dictionary<int, float>();
        private int _penX, _penY, _rowHeight;

        public TextRenderer(GraphicsDevice device, string fontPath)
        {
            _device = device;
            _font = new FontRasterizer(fontPath);
            AddPage();
        }

        /// <summary>논리 글자 크기가 이 배율에서 몇 픽셀로 찍히는가.</summary>
        public static int PixelSize(float fontSize, float scale) => Math.Max(MinPixelSize, (int)MathF.Round(fontSize * scale));

        private void AddPage()
        {
            var page = new Texture2D(_device, PageSize, PageSize, false, SurfaceFormat.Color);
            page.SetData(new Color[PageSize * PageSize]);
            _pages.Add(page);
            _penX = 1; _penY = 1; _rowHeight = 0;
        }

        private Glyph Get(int codepoint, int px)
        {
            if (_glyphs.TryGetValue((codepoint, px), out Glyph glyph)) return glyph;
            GlyphBitmap bitmap = _font.Rasterize(codepoint, px);
            Rectangle source = Rectangle.Empty;
            if (bitmap.Width > 0 && bitmap.Height > 0)
            {
                if (bitmap.Width + 2 > PageSize || bitmap.Height + 2 > PageSize)
                    throw new InvalidOperationException($"글자가 너무 크다: {px}px");
                if (_penX + bitmap.Width + 1 > PageSize) { _penX = 1; _penY += _rowHeight + 1; _rowHeight = 0; }
                if (_penY + bitmap.Height + 1 > PageSize) AddPage();
                source = new Rectangle(_penX, _penY, bitmap.Width, bitmap.Height);
                var pixels = new Color[bitmap.Width * bitmap.Height];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color((byte)255, (byte)255, (byte)255, bitmap.Alpha[i]);
                _pages[_pages.Count - 1].SetData(0, source, pixels, 0, pixels.Length);
                _penX += bitmap.Width + 1;
                _rowHeight = Math.Max(_rowHeight, bitmap.Height);
            }
            glyph = new Glyph(_pages.Count - 1, source, bitmap.OffsetX, bitmap.OffsetY, bitmap.Advance);
            _glyphs[(codepoint, px)] = glyph;
            return glyph;
        }

        /// <summary>글줄의 폭(픽셀).</summary>
        public float Measure(string text, int px)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            float width = 0;
            for (int i = 0; i < text.Length; i += char.IsSurrogatePair(text, i) ? 2 : 1)
                width += Get(CodepointAt(text, i), px).Advance;
            return width;
        }

        private static int CodepointAt(string text, int index) =>
            char.IsSurrogatePair(text, index) ? char.ConvertToUtf32(text, index) : char.IsSurrogate(text[index]) ? '?' : text[index];

        /// <summary>(x, y)는 글줄 상자의 왼쪽 위(픽셀). 상자 높이는 px.</summary>
        public void Draw(SpriteBatch batch, string text, int px, float x, float y, Color color)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (!_ascents.TryGetValue(px, out float ascent)) _ascents[px] = ascent = _font.Ascent(px);
            float pen = MathF.Round(x), baseline = MathF.Round(y + ascent);
            for (int i = 0; i < text.Length; i += char.IsSurrogatePair(text, i) ? 2 : 1)
            {
                Glyph glyph = Get(CodepointAt(text, i), px);
                if (glyph.Source.Width > 0)
                    batch.Draw(_pages[glyph.Page], new Vector2(MathF.Round(pen) + glyph.OffsetX, baseline + glyph.OffsetY), glyph.Source, color);
                pen += glyph.Advance;
            }
        }

        public void Dispose()
        {
            foreach (Texture2D page in _pages) page.Dispose();
            _pages.Clear();
        }
    }

    /// <summary>UI 토대에 글자 폭을 알려 준다. UI는 논리 좌표로 묻고, 답은 실제로 찍힐 픽셀 크기에서 잰 값이다.</summary>
    public sealed class HostTextMeasurer : ITextMeasurer
    {
        private readonly TextRenderer _text;
        public float Scale = 1f;

        public HostTextMeasurer(TextRenderer text) { _text = text; }

        public float Width(string text, float fontSize) => _text.Measure(text, TextRenderer.PixelSize(fontSize, Scale)) / Scale;
        public float LineHeight(float fontSize) => TextRenderer.PixelSize(fontSize, Scale) / Scale;
    }
}
