using System;
using Rootborn.Core.Minigames;
using Rootborn.Ui;

namespace Rootborn.Screens.Furniture
{
    /// <summary>
    /// 월드 위 배치 화면이 Host에게 묻는 것: 화면 좌표(논리 1920x1080 기준)와 가구 칸 사이 바꾸기. Host가 지금 카메라로 계산한다.
    /// 칸 좌표는 가구 칸(FurnitureRoom, y가 위로).
    /// </summary>
    public interface IPlacementView
    {
        /// <summary>지금 방(가구를 놓을 수 있는 맵에 있을 때). 없으면 null.</summary>
        FurnitureRoom Room { get; }

        /// <summary>화면 좌표가 덮는 칸. 맵 밖이면 false.</summary>
        bool TryCellAt(float x, float y, out GridPoint cell);

        /// <summary>칸의 화면 사각형(논리 좌표).</summary>
        UiRect CellRect(GridPoint cell);
    }

    /// <summary>
    /// 사각형 안에 비율을 지켜 맞춘 그림(Unity Image.preserveAspect). 그림 크기는 좌표표에서 읽은 값이고, 그릴 때 지금 사각형으로 계산한다
    /// (쪽을 넘겨 그림이 바뀌어도 배치를 다시 할 필요가 없다). 그림이 없으면 아무것도 그리지 않는다. 포인터를 받지 않는다.
    /// </summary>
    public sealed class FittedImage : UiNode
    {
        public string Image;
        public float SourceWidth, SourceHeight;
        public UiColor Tint = UiTheme.White;

        public void Show(string image, float width, float height)
        {
            Image = image;
            SourceWidth = width;
            SourceHeight = height;
        }

        public void Clear() => Image = null;

        /// <summary>지금 사각형 안에 비율을 지켜 가운데 놓은 사각형.</summary>
        public UiRect FittedRect
        {
            get
            {
                if (SourceWidth <= 0 || SourceHeight <= 0) return Rect;
                float scale = MathF.Min(Rect.W / SourceWidth, Rect.H / SourceHeight);
                float w = SourceWidth * scale, h = SourceHeight * scale;
                return new UiRect(Rect.X + (Rect.W - w) / 2f, Rect.Y + (Rect.H - h) / 2f, w, h);
            }
        }

        public override void Emit(DrawList list, ITextMeasurer text)
        {
            if (string.IsNullOrEmpty(Image)) return;
            list.Image(FittedRect, Image, default, Tint);
        }
    }

    /// <summary>뿌리 노드: 자식(패널들)보다 먼저 월드 위 미리보기를 그린다. 포인터를 받지 않는다(빈 곳의 포인터는 월드로 간다).</summary>
    internal sealed class PlacementRoot : UiNode
    {
        private readonly Action<DrawList, ITextMeasurer> _emit;

        public PlacementRoot(Action<DrawList, ITextMeasurer> emit) { _emit = emit; }

        public override void Emit(DrawList list, ITextMeasurer text) => _emit(list, text);
    }
}
