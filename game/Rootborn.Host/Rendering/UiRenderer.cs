using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rootborn.Host.Text;
using Rootborn.Ui;

namespace Rootborn.Host.Rendering
{
    /// <summary>
    /// UI 토대가 낸 그리기 명령을 화면 픽셀로 그린다. 패널 타일은 정수 배율, 글자는 찍힐 크기 그대로.
    /// 패널 그림은 UiSkins, 아이콘·스프라이트는 ImageCatalog가 정한 PNG를 그 자리에서 읽어 둔다(변환 단계 없음).
    /// </summary>
    public sealed class UiRenderer : IDisposable
    {
        // 색은 알파를 곱하지 않은 채로 섞고, 알파는 불투명하게 쌓는다(저장한 화면이 반투명해지지 않게).
        // 상태 객체는 그래픽 장치에 묶이므로 렌더러마다 만들고 버린다(한 프로세스에서 창을 다시 띄우는 테스트가 있다).
        private readonly BlendState _blend = new BlendState
        {
            ColorSourceBlend = Microsoft.Xna.Framework.Graphics.Blend.SourceAlpha,
            ColorDestinationBlend = Microsoft.Xna.Framework.Graphics.Blend.InverseSourceAlpha,
            AlphaSourceBlend = Microsoft.Xna.Framework.Graphics.Blend.One,
            AlphaDestinationBlend = Microsoft.Xna.Framework.Graphics.Blend.InverseSourceAlpha,
        };
        private readonly RasterizerState _scissor = new RasterizerState { CullMode = CullMode.None, ScissorTestEnable = true };

        private readonly GraphicsDevice _device;
        private readonly SpriteBatch _batch;
        private readonly Texture2D _white;
        private readonly TextRenderer _text;
        private readonly UiSkins _skins;
        private readonly ImageCatalog _images;
        private readonly SpriteAtlases _atlases;
        private readonly Dictionary<string, Texture2D> _textures = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _missing = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<SlicePiece> _pieces = new List<SlicePiece>(1024);
        private readonly Stack<Rectangle> _clips = new Stack<Rectangle>();

        public UiRenderer(GraphicsDevice device, TextRenderer text, UiSkins skins, ImageCatalog images, SpriteAtlases atlases)
        {
            _device = device;
            _batch = new SpriteBatch(device);
            _text = text;
            _skins = skins;
            _images = images;
            _atlases = atlases;
            _white = new Texture2D(device, 1, 1);
            _white.SetData(new[] { Color.White });
        }

        public UiSkins Skins => _skins;

        /// <summary>그리려 했지만 찾지 못한 그림 id(빈 그림 검사용).</summary>
        public IReadOnlyCollection<string> MissingImages => _missing;

        private Texture2D Texture(string texture)
        {
            if (_textures.TryGetValue(texture, out Texture2D loaded)) return loaded;
            using (FileStream stream = File.OpenRead(_atlases.FullPath(texture))) loaded = Texture2D.FromStream(_device, stream);
            _textures[texture] = loaded;
            return loaded;
        }

        private static Color ToColor(UiColor c) => new Color(c.R, c.G, c.B, c.A);
        private static Rectangle ToRectangle(PixelRect r) => new Rectangle(r.X, r.Y, r.W, r.H);

        /// <summary>지금 걸려 있는 렌더 타깃 전체에 그린다.</summary>
        public void Draw(DrawList list, ScreenFit fit)
        {
            Rectangle full = _device.Viewport.Bounds;
            _clips.Clear();
            _device.ScissorRectangle = full;
            Begin();
            foreach (DrawCommand command in list.Commands)
            {
                switch (command.Kind)
                {
                    case DrawKind.Panel:
                    {
                        SkinSource skin = _skins.Get(command.Skin);
                        _pieces.Clear();
                        if (skin.Stretch) NineSlice.BuildStretched(fit.ToPixels(command.Rect), skin.Slices, skin.Scale(fit), _pieces);
                        else NineSlice.Build(fit.ToPixels(command.Rect), skin.Slices, skin.Scale(fit), _pieces);
                        Texture2D sheet = Texture(skin.Texture);
                        Color tint = ToColor(command.Color);
                        for (int i = 0; i < _pieces.Count; i++)
                            _batch.Draw(sheet, ToRectangle(_pieces[i].Dest), ToRectangle(_pieces[i].Source), tint);
                        break;
                    }
                    case DrawKind.Image:
                    {
                        if (!_images.TryResolve(command.Skin, command.Source, out string path, out PixelRect source)) { _missing.Add(command.Skin); break; }
                        Texture2D texture = Texture(path);
                        Rectangle from = source.W > 0 ? ToRectangle(source) : texture.Bounds;
                        _batch.Draw(texture, ToRectangle(fit.ToPixels(command.Rect)), from, ToColor(command.Color));
                        break;
                    }
                    case DrawKind.Fill:
                        _batch.Draw(_white, ToRectangle(fit.ToPixels(command.Rect)), ToColor(command.Color));
                        break;
                    case DrawKind.Text:
                        _text.Draw(_batch, command.Text, TextRenderer.PixelSize(command.FontSize, fit.Scale),
                            command.Rect.X * fit.Scale, command.Rect.Y * fit.Scale, ToColor(TextColor(command)));
                        break;
                    case DrawKind.PushClip:
                        _batch.End();
                        _clips.Push(_device.ScissorRectangle);
                        _device.ScissorRectangle = Rectangle.Intersect(_device.ScissorRectangle, ToRectangle(fit.ToPixels(command.Rect)));
                        Begin();
                        break;
                    case DrawKind.PopClip:
                        _batch.End();
                        _device.ScissorRectangle = _clips.Count > 0 ? _clips.Pop() : full;
                        Begin();
                        break;
                }
            }
            _batch.End();
            _device.ScissorRectangle = full;
        }

        /// <summary>글자가 올라앉은 그림에 글자 색이 정해져 있으면 그 색(흐린 글자는 반투명으로).</summary>
        private UiColor TextColor(DrawCommand command)
        {
            if (command.Skin == null) return command.Color;
            UiColor? themed = _skins.Get(command.Skin).TextColor;
            if (!themed.HasValue) return command.Color;
            UiColor c = themed.Value;
            return command.Color == UiTheme.InkMuted ? new UiColor(c.R, c.G, c.B, (byte)(c.A / 2)) : c;
        }

        private void Begin() => _batch.Begin(SpriteSortMode.Deferred, _blend, SamplerState.PointClamp, null, _scissor);

        public void Dispose()
        {
            _batch.Dispose();
            _white.Dispose();
            foreach (Texture2D texture in _textures.Values) texture.Dispose();
            _textures.Clear();
            _blend.Dispose();
            _scissor.Dispose();
        }
    }
}
