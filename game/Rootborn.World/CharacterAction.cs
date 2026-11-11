using System;
using Rootborn.Core.World;

namespace Rootborn.World
{
    /// <summary>
    /// 클릭 행동의 그림 칸(Unity GrowingCharacterVisualView.PlayAction/Advance): seconds 동안 firstColumn부터 columnCount칸을 차례로 보이고,
    /// 행동하는 동안은 누른 쪽을 바라본다. 다시 누르면 처음부터. 걷기를 막지 않는다(Unity 판도 행동 중에 걸었다). 시간은 Advance로만 흐른다.
    /// </summary>
    public sealed class CharacterActionTimer
    {
        private readonly float _seconds;
        private readonly int _firstColumn, _columnCount;
        private float _remaining;

        public CharacterActionTimer(float seconds, int firstColumn, int columnCount)
        {
            _seconds = Math.Max(0f, seconds);
            _firstColumn = Math.Max(0, firstColumn);
            _columnCount = Math.Max(1, columnCount);
        }

        public bool Active => _remaining > 0f;
        public Facing Facing { get; private set; } = Facing.Down;
        /// <summary>시작한 행동 수(시험용).</summary>
        public int Played { get; private set; }

        public void Play(Facing facing)
        {
            Facing = facing;
            _remaining = _seconds;
            Played++;
        }

        public void Advance(float dt) { if (_remaining > 0f) _remaining = Math.Max(0f, _remaining - Math.Max(0f, dt)); }

        /// <summary>행동 중이면 행동 칸, 아니면 받은 칸(걷기·서기).</summary>
        public int ColumnOr(int column)
        {
            if (_remaining <= 0f || _seconds <= 0f) return column;
            float progress = 1f - Math.Clamp(_remaining / _seconds, 0f, 1f);
            return _firstColumn + Math.Min(_columnCount - 1, (int)(progress * _columnCount));
        }
    }

    /// <summary>누른 점이 캐릭터의 어느 쪽인가(Unity PlayerController: 긴 축 쪽. 맵 픽셀은 아래로 +y). 캐릭터 위면 지금 방향 그대로.</summary>
    public static class PointerFacing
    {
        public static Facing From(float dx, float dy, Facing current)
        {
            if (dx * dx + dy * dy < 0.0001f) return current;
            if (Math.Abs(dx) > Math.Abs(dy)) return dx < 0f ? Facing.Left : Facing.Right;
            return dy > 0f ? Facing.Down : Facing.Up;
        }
    }
}
