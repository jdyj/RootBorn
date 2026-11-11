using System;

namespace Rootborn.Core.Time
{
    /// <summary>
    /// 흐르는 시간의 규칙 수치(설계 2026-11-12-flowing-time-design.md 3절). 값은 데이터에서 온다(하루 길이는 data/time, 나머지는 game/time.json).
    /// 시각은 "그날 0시부터 센 분"이다. 하루는 <see cref="DayStartMinutes"/>(07:00)에 시작해 다음 날 같은 시각(<see cref="DayEndMinutes"/> = 31:00)에 넘어간다.
    /// 그래서 자정을 넘긴 02:00은 같은 날의 26:00(1560분)이고, 보이는 시각은 <see cref="ClockMinutes"/>로 24시간에 감는다.
    /// </summary>
    public sealed class TimeRules
    {
        public const int MinutesPerDay = 24 * 60;

        /// <param name="realSecondsPerGameDay">실제 몇 초가 게임 하루(24시간)인가. 1초보다 작으면 1초.</param>
        /// <param name="dayStartMinutes">하루가 시작하는 시각(0~1439분).</param>
        /// <param name="bedtimeMinutes">이때까지 자면 다 회복하는 취침 시각(그날 0시부터 센 분). 하루 시작보다 이르면 다음 날 새벽으로 본다.</param>
        /// <param name="nightEnergyLossPerHour">취침 시각 뒤 깨어 있는 게임 1시간마다 잃는 체력.</param>
        /// <param name="lateSleepRecoveryLossPerHour">취침 시각보다 1시간 늦게 잘 때마다 덜 돌아오는 체력.</param>
        public TimeRules(float realSecondsPerGameDay, int dayStartMinutes, int bedtimeMinutes, int nightEnergyLossPerHour, int lateSleepRecoveryLossPerHour)
        {
            RealSecondsPerGameDay = float.IsNaN(realSecondsPerGameDay) || realSecondsPerGameDay < 1f ? 1f : realSecondsPerGameDay;
            DayStartMinutes = ClockMinutes(dayStartMinutes);
            int bedtime = ClockMinutes(bedtimeMinutes);
            BedtimeMinutes = bedtime < DayStartMinutes ? bedtime + MinutesPerDay : bedtime;
            NightEnergyLossPerHour = Math.Max(0, nightEnergyLossPerHour);
            LateSleepRecoveryLossPerHour = Math.Max(0, lateSleepRecoveryLossPerHour);
        }

        public float RealSecondsPerGameDay { get; }
        public int DayStartMinutes { get; }

        /// <summary>날이 넘어가는 시각: 다음 날의 하루 시작(그날 0시부터 센 분).</summary>
        public int DayEndMinutes => DayStartMinutes + MinutesPerDay;

        public int BedtimeMinutes { get; }
        public int NightEnergyLossPerHour { get; }
        public int LateSleepRecoveryLossPerHour { get; }

        /// <summary>그 시각이 취침 시각을 다 채운 몇 시간 뒤인가. 날이 넘어가는 시각 뒤는 세지 않는다.</summary>
        public int HoursPastBedtime(int minutes)
        {
            int capped = Math.Min(minutes, DayEndMinutes);
            return capped <= BedtimeMinutes ? 0 : (capped - BedtimeMinutes) / 60;
        }

        /// <summary>그 시각에 자면 돌아오는 체력. 늦은 시간만큼 덜 돌아오고 0보다 작지 않다.</summary>
        public int SleepRecovery(int fullRecovery, int sleptAtMinutes)
            => Math.Max(0, fullRecovery - LateSleepRecoveryLossPerHour * HoursPastBedtime(sleptAtMinutes));

        /// <summary>보이는 시각(0~1439분). 그날 0시부터 센 분을 24시간으로 감는다.</summary>
        public static int ClockMinutes(int minutes)
        {
            int wrapped = minutes % MinutesPerDay;
            return wrapped < 0 ? wrapped + MinutesPerDay : wrapped;
        }
    }

    /// <summary>
    /// 지난 실제 초를 게임 분으로 바꾼다. 엔진·전역 시계에 닿지 않고 부르는 쪽이 넘긴 초만큼만 간다(테스트는 손으로 돌린다).
    /// 1분이 안 되는 조각은 다음 부름까지 남긴다(세이브에 넣지 않는다 — 1분이 안 되는 시간은 버려도 된다).
    /// </summary>
    public sealed class FlowingClock
    {
        private readonly double _secondsPerMinute;
        private double _pendingSeconds;

        public FlowingClock(float realSecondsPerGameDay)
        {
            double day = double.IsNaN(realSecondsPerGameDay) || realSecondsPerGameDay < 1f ? 1d : realSecondsPerGameDay;
            _secondsPerMinute = day / TimeRules.MinutesPerDay;
        }

        /// <summary>실제 초를 보낸다. 멈춤이면 흐르지 않고 남은 조각도 그대로다. 음수·NaN은 무시한다. 지난 게임 분을 돌려준다.</summary>
        public int Advance(float realSeconds, bool paused)
        {
            if (paused || float.IsNaN(realSeconds) || realSeconds <= 0f) return 0;
            _pendingSeconds += realSeconds;
            // 실수 오차로 25초가 59.999분이 되지 않게 아주 작은 여유를 둔다.
            int minutes = (int)Math.Floor(_pendingSeconds / _secondsPerMinute + 1e-9);
            if (minutes <= 0) return 0;
            _pendingSeconds = Math.Max(0d, _pendingSeconds - minutes * _secondsPerMinute);
            return minutes;
        }
    }
}
