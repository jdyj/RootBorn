using System;
using Rootborn.Core.World;

namespace Rootborn.Simulation
{
    /// <summary>
    /// 문 앞에서 누를 방향 키: 문 사각형에서 몸 가운데와 가장 가까운 점 쪽(큰 축 하나). 문 가운데 쪽으로 누르면 맵 가장자리를 따라 긴 문
    /// (예: 해변으로 가는 맵 아래쪽 문)에서는 옆으로만 걷고 문에 닿지 않는다 — Host 테스트의 PlayerDriver.WalkThroughDoor가 가운데 쪽을 쓴다.
    /// </summary>
    public static class DoorDirection
    {
        public static (int Dx, int Dy) Toward(AreaRect door, float x, float y)
        {
            float nx = Math.Max(door.X, Math.Min(x, door.X + door.W));
            float ny = Math.Max(door.Y, Math.Min(y, door.Y + door.H));
            float ddx = nx - x, ddy = ny - y;
            if (Math.Abs(ddx) < 0.001f && Math.Abs(ddy) < 0.001f) { ddx = door.CenterX - x; ddy = door.CenterY - y; }
            int dx = Math.Abs(ddx) > Math.Abs(ddy) ? Math.Sign(ddx) : 0;
            int dy = dx == 0 ? Math.Sign(ddy) : 0;
            return (dx, dy);
        }
    }
}
