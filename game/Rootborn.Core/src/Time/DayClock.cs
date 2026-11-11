using System;
using Rootborn.Core.Common;

namespace Rootborn.Core.Time
{
    /// <summary>
    /// 지난 실제 시간으로 일차와 하루 진행률을 계산한다. 시간은 스스로 흐르지 않고 Tick으로 받은 만큼만 간다.
    /// </summary>
    public sealed class DayClock
    {
        private float _realSecondsPerGameDay;
        private int _startDay;

        public DayClock(float realSecondsPerGameDay = 600f, int startDay = 1)
        {
            Configure(realSecondsPerGameDay, startDay);
        }

        public float ElapsedRealSeconds { get; private set; }
        public int Day { get; private set; }
        public float DayProgress01 { get; private set; }
        public float RealSecondsPerGameDay => _realSecondsPerGameDay;
        public float ElapsedGameDays => ElapsedRealSeconds / _realSecondsPerGameDay;
        public float CurrentHour => DayProgress01 * 24f;
        public int Hour => CoreMath.FloorToInt(CurrentHour);
        public int Minute => CoreMath.FloorToInt((CurrentHour - Hour) * 60f);

        /// <summary>하루 길이(최소 1초)와 시작 일차(최소 1)를 바꾼다. 지난 시간은 그대로 두고 일차를 다시 계산한다.</summary>
        public void Configure(float realSecondsPerGameDay, int startDay)
        {
            _realSecondsPerGameDay = MathF.Max(1f, realSecondsPerGameDay);
            _startDay = Math.Max(1, startDay);
            Recalculate();
        }

        /// <summary>시간을 보낸다. 음수는 무시한다. 넘어간 날 수를 돌려준다.</summary>
        public int Tick(float deltaSeconds)
        {
            int previousDay = Day;
            ElapsedRealSeconds += MathF.Max(0f, deltaSeconds);
            Recalculate();
            return Day - previousDay;
        }

        /// <summary>같은 날 안에서 시각만 옮긴다.</summary>
        public void SetCurrentHour(float hour)
        {
            float wrapped = DayParts.WrapHour(hour);
            float dayStartSeconds = MathF.Floor(ElapsedRealSeconds / _realSecondsPerGameDay) * _realSecondsPerGameDay;
            ElapsedRealSeconds = dayStartSeconds + wrapped / 24f * _realSecondsPerGameDay;
            Recalculate();
        }

        private void Recalculate()
        {
            DayProgress01 = (ElapsedRealSeconds % _realSecondsPerGameDay) / _realSecondsPerGameDay;
            Day = _startDay + CoreMath.FloorToInt(ElapsedRealSeconds / _realSecondsPerGameDay);
        }
    }
}
