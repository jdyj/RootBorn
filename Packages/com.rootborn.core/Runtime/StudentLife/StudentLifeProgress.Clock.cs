using System;
using Rootborn.Core.Save;
using Rootborn.Core.Time;

namespace Rootborn.Core.StudentLife
{
    /// <summary>활동이 시계를 어떻게 쓰는가.</summary>
    public enum ActivityClockMode
    {
        /// <summary>혼자 하기: 시계가 활동이 끝나는 시각으로 곧바로 건너뛴다.</summary>
        SkipAhead,

        /// <summary>같이 하기(설계 6절 4단계): 시계는 그대로 흐르고, 끝나는 시각까지 "하는 중"이라 다른 활동을 시작하지 못한다.</summary>
        StayBusy,
    }

    /// <summary>
    /// 흐르는 시계의 개인 상태(student-clock-*.json). 시각과 날짜는 학생 진행(<see cref="StudentLifeProgressSaveData.TimeMinutes"/>)에 있고,
    /// 여기는 흐르는 시간이 더한 것만 둔다. 다른 날(<see cref="Day"/>)의 것이면 버린다.
    /// </summary>
    [Serializable]
    public sealed class StudentClockSaveData : IVersionedSave
    {
        public const int KnownVersion = 1;

        /// <summary>형식 버전. 새로 만든 모양은 0(버전 없는 파일과 같다)이고 쓸 때 <see cref="KnownVersion"/>을 넣는다.</summary>
        public int Version;
        public int Day;
        public bool Exhausted;
        public int NightHoursCharged;
        public int BusyUntilMinutes;

        int IVersionedSave.Version { get => Version; set => Version = value; }
        int IVersionedSave.KnownVersion => KnownVersion;
    }

    // 흐르는 시계: 시각이 가고, 밤에 체력이 줄고, 체력 0이면 지치고, 자야 풀린다. 시간 규칙이 없으면(옛 시험) 시각만 간다.
    public sealed partial class StudentLifeProgress
    {
        private const int LegacyDayStartMinutes = 8 * 60;

        /// <summary>시간 규칙. 세이브에 없다(달력처럼 읽은 뒤 다시 넣는다). 없으면 밤의 체력 감소·지침·날 넘김이 없다.</summary>
        public TimeRules TimeRules { get; private set; }

        public ActivityClockMode ActivityClock { get; private set; } = ActivityClockMode.SkipAhead;

        /// <summary>체력이 0이 되어 지쳤다. 걷기·대화·잠만 된다. 잠으로만 풀린다.</summary>
        public bool IsExhausted { get; private set; }

        /// <summary>오늘 밤 이미 체력을 깎은 시간 수(취침 시각 뒤 다 채운 시간).</summary>
        public int NightHoursCharged { get; private set; }

        /// <summary>마지막 활동이 끝나는 시각(그날 0시부터 센 분). 혼자 하기에서는 곧바로 그 시각이 된다.</summary>
        public int BusyUntilMinutes { get; private set; }

        public bool IsBusy => BusyUntilMinutes > TimeMinutes;

        /// <summary>깨어 있는 채로 다음 날 하루 시작 시각이 되었다. 하루를 끝내야(<see cref="TryEndDayAwake"/>) 다음 날이 온다.</summary>
        public bool HasDayRunOut => TimeRules != null && DayState == StudentDayState.InProgress && TimeMinutes >= TimeRules.DayEndMinutes;

        /// <summary>날이 넘어가기까지 남은 분. 시간 규칙이 없으면 int.MaxValue.</summary>
        public int MinutesUntilDayEnd => TimeRules == null ? int.MaxValue : Math.Max(0, TimeRules.DayEndMinutes - TimeMinutes);

        /// <summary>활동을 시작할 수 있는 몸 상태: 하루가 진행 중이고, 지치지 않았고, 하는 중이 아니고, 날이 다 가지 않았다.</summary>
        public bool CanStartActivity => DayState == StudentDayState.InProgress && !IsExhausted && !IsBusy && !HasDayRunOut;

        public void ConfigureTimeRules(TimeRules rules)
        {
            TimeRules = rules;
            if (rules != null && !_clockRestored) NightHoursCharged = rules.HoursPastBedtime(TimeMinutes);
        }

        public void ConfigureActivityClock(ActivityClockMode mode) => ActivityClock = mode;

        /// <summary>
        /// 시계를 minutes만큼 보낸다. 하루가 진행 중일 때만 간다(결과를 보는 동안은 멈춘다). 날이 넘어가는 시각에서 멈추지 않는다 —
        /// 넘어간 것은 <see cref="HasDayRunOut"/>로 알리고, 하루를 끝내는 것은 부르는 쪽이 한다. 간 분을 돌려준다.
        /// </summary>
        public int AdvanceClock(int minutes)
        {
            if (minutes <= 0 || DayState != StudentDayState.InProgress) return 0;
            int previous = TimeMinutes;
            TimeMinutes = previous > int.MaxValue - minutes ? int.MaxValue : previous + minutes;
            ChargeNight();
            if (TimeMinutes != previous) TimeChanged?.Invoke();
            return TimeMinutes - previous;
        }

        /// <summary>
        /// 같이 하는 방에 들어와(낮 도중, 또는 방에 없던 사이) 방의 시각으로 옮긴다: 앞으로만 가고, 방에 없던 동안의 밤 시간은 거슬러 깎지 않는다
        /// (옛 세이브를 읽을 때와 같다 — <see cref="RestoreClock"/>). 하루가 진행 중일 때만. 간 분을 돌려준다.
        /// </summary>
        public int JumpClockTo(int minutes)
        {
            if (DayState != StudentDayState.InProgress || minutes <= TimeMinutes) return 0;
            int previous = TimeMinutes;
            TimeMinutes = minutes;
            if (TimeRules != null) NightHoursCharged = Math.Max(NightHoursCharged, TimeRules.HoursPastBedtime(TimeMinutes));
            TimeChanged?.Invoke();
            return TimeMinutes - previous;
        }

        /// <summary>잠: 지금 시각에 자고 하루를 끝낸다. 회복은 하루 끝 규칙이 잔 시각(<see cref="LastNight"/>)으로 정한다. 지침이 풀린다.</summary>
        public bool TrySleep(DayEndRuleSet rules, out StudentDaySummary summary) => TryEndDay(rules, NightRecord.Slept(TimeMinutes), out summary);

        /// <summary>자지 않고 날이 넘어갔다: 하루를 끝내지만 회복은 없고 지침은 그대로다.</summary>
        public bool TryEndDayAwake(DayEndRuleSet rules, out StudentDaySummary summary) => TryEndDay(rules, NightRecord.Awake, out summary);

        /// <summary>마지막으로 끝낸 날의 밤(잤는지, 언제 잤는지). 하루 끝 규칙이 읽는다.</summary>
        public NightRecord LastNight { get; private set; } = NightRecord.Slept(-1);

        public StudentClockSaveData ToClockSaveData() => new StudentClockSaveData
        {
            Version = StudentClockSaveData.KnownVersion, Day = CurrentDay, Exhausted = IsExhausted, NightHoursCharged = NightHoursCharged, BusyUntilMinutes = BusyUntilMinutes,
        };

        /// <summary>
        /// 읽은 시계 상태를 넣는다. 없거나 다른 날 것이면 지금 시각에서 새로 센다(옛 세이브: 이미 지난 밤 시간은 거슬러 깎지 않는다).
        /// 시간 규칙을 넣은 뒤에 부른다.
        /// </summary>
        public void RestoreClock(StudentClockSaveData data)
        {
            _clockRestored = true;
            if (data == null || data.Day != CurrentDay)
            {
                IsExhausted = false;
                NightHoursCharged = TimeRules != null ? TimeRules.HoursPastBedtime(TimeMinutes) : 0;
                BusyUntilMinutes = TimeMinutes;
                return;
            }

            IsExhausted = data.Exhausted;
            NightHoursCharged = Math.Max(0, data.NightHoursCharged);
            BusyUntilMinutes = Math.Max(0, data.BusyUntilMinutes);
        }

        private bool _clockRestored;

        // 활동의 시간: 끝나는 시각을 정하고, 혼자면 시계를 그 시각으로 보낸다.
        private void SpendActivityTime(int minutes)
        {
            int start = Math.Max(TimeMinutes, BusyUntilMinutes);
            BusyUntilMinutes = start > int.MaxValue - minutes ? int.MaxValue : start + minutes;
            if (ActivityClock == ActivityClockMode.SkipAhead) AdvanceClock(BusyUntilMinutes - TimeMinutes);
        }

        /// <summary>
        /// 미니게임이 있는 활동의 시간(설계 3.2절): 시계를 건너뛰지 않고 끝나는 시각만 정한다(하는 중). 미니게임을 하는 동안 시계는 스스로 흐르고,
        /// 정산·포기 때 <see cref="FinishReservedActivityTime"/>이 남은 몫만 쓴다. 그래서 걸리는 시간은 활동 분과 미니게임 동안 흐른 분 중 큰 것이다.
        /// </summary>
        internal void ReserveActivityTime(int minutes)
        {
            int start = Math.Max(TimeMinutes, BusyUntilMinutes);
            int cost = Math.Max(0, minutes);
            BusyUntilMinutes = start > int.MaxValue - cost ? int.MaxValue : start + cost;
        }

        /// <summary>예약한 활동 시간을 끝낸다: 혼자면 끝나는 시각까지 남은 몫만 건너뛰고(0 아래로는 안 감), 같이 하면 그 시각까지 하는 중으로 둔다.</summary>
        internal void FinishReservedActivityTime()
        {
            if (ActivityClock == ActivityClockMode.SkipAhead && BusyUntilMinutes > TimeMinutes) AdvanceClock(BusyUntilMinutes - TimeMinutes);
        }

        private void ChargeNight()
        {
            if (TimeRules == null) return;
            int hours = TimeRules.HoursPastBedtime(TimeMinutes);
            if (hours <= NightHoursCharged) return;
            int lost = (hours - NightHoursCharged) * TimeRules.NightEnergyLossPerHour;
            NightHoursCharged = hours;
            if (lost <= 0) return;
            int previous = Energy;
            Energy = Math.Max(0, Energy - lost);
            if (Energy != previous) VitalsChanged?.Invoke();
            CheckExhaustion();
        }

        private void CheckExhaustion()
        {
            if (TimeRules != null && Energy == 0) IsExhausted = true;
        }

        // 하루를 끝낼 때: 잤으면 지침이 풀리고(회복 뒤에도 0이면 다시 지친다), 자지 않았으면 그대로.
        private void AfterNight()
        {
            if (!LastNight.HasSlept) return;
            IsExhausted = false;
            CheckExhaustion();
        }

        // 다음 날 시작 시각. 날이 넘어가는 시각을 넘긴 활동 시간은 다음 날 아침으로 잇는다.
        private int NextDayStartMinutes()
        {
            if (TimeRules == null) return LegacyDayStartMinutes;
            int carried = Math.Max(0, TimeMinutes - TimeRules.DayEndMinutes);
            return TimeRules.DayStartMinutes + Math.Min(carried, TimeRules.MinutesPerDay - 1);
        }

        private void ResetNight()
        {
            NightHoursCharged = TimeRules != null ? TimeRules.HoursPastBedtime(TimeMinutes) : 0;
            BusyUntilMinutes = TimeMinutes;
        }
    }

    /// <summary>끝낸 날의 밤: 잤는지와 잠든 시각(그날 0시부터 센 분, 모르면 -1).</summary>
    public readonly struct NightRecord
    {
        private NightRecord(bool slept, int sleptAtMinutes)
        {
            HasSlept = slept;
            SleptAtMinutes = sleptAtMinutes;
        }

        public bool HasSlept { get; }
        public int SleptAtMinutes { get; }

        public static NightRecord Awake => new NightRecord(false, -1);
        public static NightRecord Slept(int atMinutes) => new NightRecord(true, atMinutes);
    }
}
