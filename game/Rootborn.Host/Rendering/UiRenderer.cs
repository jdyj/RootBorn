using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rootborn.Host.Text;
using Rootborn.Ui;

namespace Rootborn.Host.Rendering
{
    /// <summary>
    /// 패널 그림이 Modern_UI_Style_2.png(16x16 판)의 어디에 있는가. 모든 값은 PNG 픽셀을 직접 재서 정했다(2026-11-11).
    /// 16px 칸 안에서 그림은 일부만 차지하므로 칸 단위(r, c)로 자르면 안 된다.
    /// </summary>
    public static class UiSkins
    {
        // 공통 패널(r2~r4, c0~c2): 그림 x11~37, y42~69. 모서리 그림이 들어가는 폭: 왼쪽 5, 오른쪽 5, 위 6, 아래 5.
        private static readonly NineSliceSource CommonPanel = new NineSliceSource(11, 16, 32, 37, 42, 48, 64, 69);

        // 둥근 밝은 상자(r14~r16, c3~c5 대화 패널): 그림 x60~84, y235~260.
        // 왼쪽 위 모서리의 밝은 점이 x63, y239까지 내려와 왼쪽 4, 위 5로 잡는다. 오른쪽 3, 아래 3.
        private static readonly NineSliceSource RoundedLight = new NineSliceSource(60, 64, 81, 84, 235, 240, 257, 260);

        // 둥근 어두운 상자(r14~r16, c0~c2): 그림 x12~36, y235~260. 밝은 상자와 모양이 같고 안쪽 색만 다르다.
        private static readonly NineSliceSource RoundedDark = new NineSliceSource(12, 16, 33, 36, 235, 240, 257, 260);

        public static NineSliceSource Resolve(string skin)
        {
            switch (skin)
            {
                case UiSkin.Panel: return CommonPanel;
                case UiSkin.Button: return RoundedLight;
                case UiSkin.Field: return RoundedLight;
                case UiSkin.Slot: return RoundedDark;
                default: throw new InvalidOperationException("모르는 패널 그림 이름: " + skin);
            }
        }
    }

    /// <summary>UI 토대가 낸 그리기 명령을 화면 픽셀로 그린다. 패널 타일은 정수 배율, 글자는 찍힐 크기 그대로.</summary>
    public sealed class UiRenderer : IDisposable
    {
        // 색은 알파를 곱하지 않은 채로 섞고, 알파는 불투명하게 쌓는다(저장한 화면이 반투명해지지 않게).
        private static readonly BlendState Blend = new BlendState
        {
            ColorSourceBlend = Microsoft.Xna.Framework.Graphics.Blend.SourceAlpha,
            ColorDestinationBlend = Microsoft.Xna.Framework.Graphics.Blend.InverseSourceAlpha,
            AlphaSourceBlend = Microsoft.Xna.Framework.Graphics.Blend.One,
            AlphaDestinationBlend = Microsoft.Xna.Framework.Graphics.Blend.InverseSourceAlpha,
        };
        private static readonly RasterizerState Scissor = new RasterizerState { CullMode = CullMode.None, ScissorTestEnable = true };

        private readonly GraphicsDevice _device;
        private readonly SpriteBatch _batch;
        private readonly Texture2D _sheet, _white;
        private readonly TextRenderer _text;
        private readonly List<SlicePiece> _pieces = new List<SlicePiece>(1024);
        private readonly Stack<Rectangle> _clips = new Stack<Rectangle>();

        public UiRenderer(GraphicsDevice device, Texture2D sheet, TextRenderer text)
        {
            _device = device;
            _batch = new SpriteBatch(device);
            _sheet = sheet;
            _text = text;
            _white = new Texture2D(device, 1, 1);
            _white.SetData(new[] { Color.White });
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
                        _pieces.Clear();
                        NineSlice.Build(fit.ToPixels(command.Rect), UiSkins.Resolve(command.Skin), fit.TileScale, _pieces);
                        Color tint = ToColor(command.Color);
                        for (int i = 0; i < _pieces.Count; i++)
                            _batch.Draw(_sheet, ToRectangle(_pieces[i].Dest), ToRectangle(_pieces[i].Source), tint);
                        break;
                    case DrawKind.Fill:
                        _batch.Draw(_white, ToRectangle(fit.ToPixels(command.Rect)), ToColor(command.Color));
                        break;
                    case DrawKind.Text:
                        _text.Draw(_batch, command.Text, TextRenderer.PixelSize(command.FontSize, fit.Scale),
                            command.Rect.X * fit.Scale, command.Rect.Y * fit.Scale, ToColor(command.Color));
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

        private void Begin() => _batch.Begin(SpriteSortMode.Deferred, Blend, SamplerState.PointClamp, null, Scissor);

        public void Dispose()
        {
            _batch.Dispose();
            _white.Dispose();
        }
    }
}
