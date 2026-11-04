using System.Collections.Generic;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    public sealed partial class StudentLifeProgress
    {
        private LifeCalendarDefinition _calendar;
        private readonly HashSet<string> _dailyTimePolicyIds = new HashSet<string>();

        public LifeCalendarDefinition Calendar => _calendar;
        public int TimePointsSpentToday { get; private set; }
        public int DailyTimePointBudget => _calendar != null ? _calendar.GetBudget(CurrentDay) : 0;
        public int TimePointsRemaining => Mathf.Max(0, DailyTimePointBudget - TimePointsSpentToday);

        public void ConfigureCalendar(LifeCalendarDefinition calendar) => _calendar = calendar;

        public bool CanSpendTimePoints(int cost)
        {
            return cost >= 0 && DayState == StudentDayState.InProgress && cost <= TimePointsRemaining;
        }

        public bool TrySpendTimePoints(int cost)
        {
            if (!CanSpendTimePoints(cost)) return false;
            TimePointsSpentToday += cost;
            return true;
        }

        public bool CanUseTimePolicy(ActivityTimePolicyDefinition policy)
        {
            if (DayState != StudentDayState.InProgress) return false;
            if (policy == null) return true;
            return policy.IsAllowedDay(CurrentDay)
                && (!policy.OncePerDay || !_dailyTimePolicyIds.Contains(policy.Id))
                && CanSpendTimePoints(policy.TimePointCost);
        }

        public bool TryUseTimePolicy(ActivityTimePolicyDefinition policy)
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
