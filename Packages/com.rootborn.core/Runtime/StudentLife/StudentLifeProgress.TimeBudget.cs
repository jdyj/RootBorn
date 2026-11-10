using System;
using System.Collections.Generic;

namespace Rootborn.Core.StudentLife
{
    // 하루 시간 포인트. 달력이 없으면 예산은 0이다.
    public sealed partial class StudentLifeProgress
    {
        private LifeCalendar _calendar;
        private readonly HashSet<string> _dailyTimePolicyIds = new HashSet<string>();

        public LifeCalendar Calendar => _calendar;
        public int TimePointsSpentToday { get; private set; }
        public int DailyTimePointBudget => _calendar != null ? _calendar.GetBudget(CurrentDay) : 0;
        public int TimePointsRemaining => Math.Max(0, DailyTimePointBudget - TimePointsSpentToday);

        public void ConfigureCalendar(LifeCalendar calendar) => _calendar = calendar;

        public bool CanSpendTimePoints(int cost) => cost >= 0 && DayState == StudentDayState.InProgress && cost <= TimePointsRemaining;

        public bool TrySpendTimePoints(int cost)
        {
            if (!CanSpendTimePoints(cost)) return false;
            TimePointsSpentToday += cost;
            return true;
        }

        /// <summary>정책이 없으면 하루가 진행 중이기만 하면 된다.</summary>
        public bool CanUseTimePolicy(ActivityTimePolicy policy)
        {
            if (DayState != StudentDayState.InProgress) return false;
            if (policy == null) return true;
            return policy.IsAllowedDay(CurrentDay)
                && (!policy.OncePerDay || !_dailyTimePolicyIds.Contains(policy.Id))
                && CanSpendTimePoints(policy.TimePointCost);
        }

        public bool TryUseTimePolicy(ActivityTimePolicy policy)
        {
            if (!CanUseTimePolicy(policy)) return false;
            if (policy == null) return true;
            TimePointsSpentToday += policy.TimePointCost;
            if (policy.OncePerDay) _dailyTimePolicyIds.Add(policy.Id);
            return true;
        }

        private void ResetDailyTimeBudget()
        {
            TimePointsSpentToday = 0;
            _dailyTimePolicyIds.Clear();
        }
    }
}
