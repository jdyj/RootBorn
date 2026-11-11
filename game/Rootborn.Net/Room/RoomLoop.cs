using System;
using System.Diagnostics;

namespace Rootborn.Net.Room
{
    /// <summary>방 루프가 읽는 시계(초). 실제 서버는 <see cref="StopwatchRoomClock"/>, 테스트는 손으로 돌리는 시계를 넘긴다.</summary>
    public interface IRoomClock
    {
        double Now { get; }
    }

    /// <summary>프로세스가 시작한 뒤 흐른 실제 시간.</summary>
    public sealed class StopwatchRoomClock : IRoomClock
    {
        private readonly Stopwatch _watch = Stopwatch.StartNew();

        public double Now => _watch.Elapsed.TotalSeconds;
    }

    /// <summary>
    /// 방을 고정 간격으로 돌린다. <see cref="RunDue"/>는 시계가 지난 틱만큼 <see cref="RoomServer.Tick"/>을 부르고,
    /// 틱 시각은 시계 값이 아니라 고정 간격으로 센 값이다(같은 시계 진행이면 같은 틱 시각). 실시간을 기다리지 않는다 —
    /// 기다리는 일은 부르는 쪽이 <see cref="SecondsUntilNextTick"/>만큼 한다.
    /// 너무 밀리면(<see cref="MaxCatchUpTicks"/>개 넘게) 밀린 틱은 버리고 지금부터 다시 센다.
    /// </summary>
    public sealed class RoomLoop
    {
        public const int MaxCatchUpTicks = 10;

        private readonly RoomServer _room;
        private readonly IRoomClock _clock;
        private readonly double _tickSeconds;
        private double _nextTickAt;

        public RoomLoop(RoomServer room, IRoomClock clock, double tickSeconds)
        {
            _room = room ?? throw new ArgumentNullException(nameof(room));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            if (tickSeconds <= 0) throw new ArgumentException("틱 간격은 양수여야 한다", nameof(tickSeconds));
            _tickSeconds = tickSeconds;
            _nextTickAt = clock.Now;
        }

        public long Ticks { get; private set; }

        public double SecondsUntilNextTick => Math.Max(0, _nextTickAt - _clock.Now);

        /// <returns>이번에 돌린 틱 수.</returns>
        public int RunDue()
        {
            double now = _clock.Now;
            int ran = 0;
            while (now >= _nextTickAt - 1e-9 && ran < MaxCatchUpTicks)
            {
                _room.Tick(_nextTickAt);
                _nextTickAt += _tickSeconds;
                Ticks++;
                ran++;
            }

            if (now >= _nextTickAt) _nextTickAt = now + _tickSeconds;
            return ran;
        }
    }
}
