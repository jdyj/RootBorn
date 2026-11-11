using System;

namespace Rootborn.Core.World
{
    public enum Facing { Down, Up, Left, Right }

    /// <summary>걷는 몸의 크기와 속도. 칸 단위라 칸 크기가 다른 맵에서도 같은 값을 쓴다.</summary>
    public sealed class WalkerSettings
    {
        /// <summary>Unity 판 플레이어: BoxCollider2D 1x1 유닛(=1칸), 속도 4유닛/초.</summary>
        public static readonly WalkerSettings UnityPlayer = new WalkerSettings(1f, 4f);

        public readonly float BodyCells, SpeedCellsPerSecond;

        public WalkerSettings(float bodyCells, float speedCellsPerSecond)
        {
            if (bodyCells <= 0f || speedCellsPerSecond <= 0f) throw new ArgumentException("몸 크기와 속도는 양수여야 한다");
            BodyCells = bodyCells;
            SpeedCellsPerSecond = speedCellsPerSecond;
        }
    }

    /// <summary>
    /// 발 상자 하나로 걷는다. 위치(X, Y)는 상자 가운데 픽셀 좌표다(Unity 판의 transform 위치 = 콜라이더 가운데).
    /// 축을 나눠 움직여 벽을 따라 미끄러진다. 시간은 Step에 넘긴 dt만큼만 흐른다.
    /// </summary>
    public sealed class Walker
    {
        private const float MaxStepSeconds = 0.1f;
        private readonly float _half, _speed;

        public float X { get; private set; }
        public float Y { get; private set; }
        public Facing Facing { get; private set; } = Facing.Down;
        public bool Moving { get; private set; }
        public float WalkSeconds { get; private set; }

        public Walker(WalkerSettings settings, int cellSize)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (cellSize <= 0) throw new ArgumentException("칸 크기는 양수여야 한다");
            _half = settings.BodyCells * cellSize / 2f;
            _speed = settings.SpeedCellsPerSecond * cellSize;
        }

        public AreaRect Box => BoxAt(X, Y);
        private AreaRect BoxAt(float x, float y) => new AreaRect(x - _half, y - _half, _half * 2f, _half * 2f);

        public void PlaceAt(float x, float y)
        {
            X = x;
            Y = y;
            Moving = false;
            WalkSeconds = 0f;
        }

        public void PlaceAt((float X, float Y) point) => PlaceAt(point.X, point.Y);

        public void Face(Facing facing) => Facing = facing;

        /// <summary>입력 방향(-1, 0, 1)으로 dt초 걷는다. 대각선은 길이 1로 맞춘다.</summary>
        public void Step(int dx, int dy, float dt, CollisionGrid grid)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            dx = Math.Sign(dx);
            dy = Math.Sign(dy);
            Moving = dx != 0 || dy != 0;
            if (!Moving)
            {
                WalkSeconds = 0f;
                return;
            }
            // Unity 판 FacingToDirKey: |x| > |y| 일 때만 옆을 본다.
            Facing = dx != 0 && dy == 0 ? (dx > 0 ? Facing.Right : Facing.Left) : (dy > 0 ? Facing.Down : Facing.Up);
            dt = Math.Min(Math.Max(dt, 0f), MaxStepSeconds);
            if (dt <= 0f) return;
            WalkSeconds += dt;

            float length = dx != 0 && dy != 0 ? 0.70710678f : 1f;
            float distance = _speed * length * dt;
            int cell = grid.Grid.CellSize;
            int steps = Math.Max(1, (int)Math.Ceiling(distance / (cell * 0.25f)));
            float part = distance / steps;
            for (int i = 0; i < steps; i++)
            {
                if (dx != 0) MoveX(dx * part, dy == 0, grid);
                if (dy != 0) MoveY(dy * part, dx == 0, grid);
            }
        }

        private void MoveX(float delta, bool alone, CollisionGrid grid)
        {
            float nx = X + delta;
            if (!grid.AnyBlocked(BoxAt(nx, Y))) { X = nx; return; }
            int cell = grid.Grid.CellSize;
            // 벽에 닿을 때까지는 간다.
            float edge = delta > 0 ? (float)Math.Floor((nx + _half) / cell) * cell - _half : (float)Math.Ceiling((nx - _half) / cell) * cell + _half;
            if (delta > 0 ? edge > X : edge < X)
                if (!grid.AnyBlocked(BoxAt(edge, Y))) X = edge;
            if (alone) SlipToward(Y, cell, target => !grid.AnyBlocked(BoxAt(X + delta, target)), Math.Abs(delta), vertical: true);
        }

        private void MoveY(float delta, bool alone, CollisionGrid grid)
        {
            float ny = Y + delta;
            if (!grid.AnyBlocked(BoxAt(X, ny))) { Y = ny; return; }
            int cell = grid.Grid.CellSize;
            float edge = delta > 0 ? (float)Math.Floor((ny + _half) / cell) * cell - _half : (float)Math.Ceiling((ny - _half) / cell) * cell + _half;
            if (delta > 0 ? edge > Y : edge < Y)
                if (!grid.AnyBlocked(BoxAt(X, edge))) Y = edge;
            if (alone) SlipToward(X, cell, target => !grid.AnyBlocked(BoxAt(target, Y + delta)), Math.Abs(delta), vertical: false);
        }

        /// <summary>
        /// 모서리 보정: 한 축으로만 걷다 막혔을 때, 몸이 있는 칸의 가운데로 옮기면 지나갈 수 있으면 그쪽으로 조금 민다.
        /// Unity 판은 물리 엔진의 밀어내기로 틈에 미끄러져 들어갔다. 이것은 그 근사다(반 칸 넘게 어긋나면 돕지 않는다).
        /// </summary>
        private void SlipToward(float current, int cell, Func<float, bool> passable, float amount, bool vertical)
        {
            float center = ((float)Math.Floor(current / cell) + 0.5f) * cell;
            if (Math.Abs(center - current) < 0.0001f || !passable(center)) return;
            float moved = Math.Abs(center - current) <= amount ? center : current + Math.Sign(center - current) * amount;
            if (vertical) Y = moved; else X = moved;
        }
    }
}
