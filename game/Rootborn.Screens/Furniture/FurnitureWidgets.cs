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
}
