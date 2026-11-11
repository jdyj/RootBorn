using System;

namespace Rootborn.Core.StudentLife
{
    /// <summary>
    /// 요일별 하루 시간 포인트. 1일째가 주의 첫날이고 기본 주말은 6·7번째 날이다.
    /// 흐르는 시간(2026-11-12)부터 시간 포인트는 활동 한도가 아니다. 하루의 칸 수는 일과 계획판의 칸 수(DailyPlanSchedule)로만 쓴다.
    /// </summary>
    public sealed class LifeCalendar
    {
        private readonly int _weekdayBudget, _weekendBudget, _weekendDaysMask;

        public LifeCalendar(int weekdayBudget = 2, int weekendBudget = 3, int weekendDaysMask = 96)
        {
            _weekdayBudget = Math.Max(0, weekdayBudget);
            _weekendBudget = Math.Max(0, weekendBudget);
            _weekendDaysMask = weekendDaysMask & 127;
        }

        /// <summary>0~6. 1보다 작은 날은 1일로 본다.</summary>
        public static int WeekdayIndexForDay(int day) => (Math.Max(1, day) - 1) % 7;

        public int GetWeekdayIndex(int day) => WeekdayIndexForDay(day);
        public bool IsWeekend(int day) => (_weekendDaysMask & (1 << WeekdayIndexForDay(day))) != 0;
        public int GetBudget(int day) => IsWeekend(day) ? _weekendBudget : _weekdayBudget;
    }

    /// <summary>활동이 시간 포인트를 얼마나 쓰고, 어느 요일에 되고, 하루 한 번인지.</summary>
    public sealed class ActivityTimePolicy
    {
        private readonly int _allowedDaysMask;

        public ActivityTimePolicy(string id, int timePointCost = 1, int allowedDaysMask = 127, bool oncePerDay = false)
        {
            Id = id ?? string.Empty;
            TimePointCost = Math.Max(0, timePointCost);
            _allowedDaysMask = allowedDaysMask & 127;
            OncePerDay = oncePerDay;
        }

        public string Id { get; }
        public int TimePointCost { get; }
        public bool OncePerDay { get; }
        public bool IsAllowedDay(int day) => (_allowedDaysMask & (1 << LifeCalendar.WeekdayIndexForDay(day))) != 0;
    }

    public sealed class TutorialStage
    {
        public TutorialStage(string id, string guideText, string displayNameKey = null)
        {
            Id = id ?? string.Empty;
            DisplayNameKey = string.IsNullOrEmpty(displayNameKey) ? Id : displayNameKey;
            GuideText = string.IsNullOrEmpty(guideText) ? DisplayNameKey : guideText;
        }

        public string Id { get; }
        public string DisplayNameKey { get; }

        /// <summary>안내 문구. 비어 있으면 표시 이름 키를, 그것도 없으면 id를 쓴다.</summary>
        public string GuideText { get; }
    }
}
