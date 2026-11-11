using System;
using System.Collections.Generic;

namespace Rootborn.Core.StudentLife
{
    /// <summary>
    /// 개인 시간 포인트를 쓸 수 있는지 한 번 더 묻는 쪽(방의 시계). 개인 예산이 허락해도 방이 막으면 쓰지 못하고, 쓴 포인트는 알린다.
    /// 혼자인 방은 개인 예산과 같은 답을 준다.
    /// </summary>
    public interface ITimePointGate
    {
        bool CanSpend(int points);
        void Spent(int points);
    }

    // 하루 시간 포인트. 달력이 없으면 예산은 0이다.
    public sealed partial class StudentLifeProgress
    {
        private LifeCalendar _calendar;
        private ITimePointGate _timeGate;
        private readonly HashSet<string> _dailyTimePolicyIds = new HashSet<string>();

        public LifeCalendar Calendar => _calendar;
        public int TimePointsSpentToday { get; private set; }
        public int DailyTimePointBudget => _calendar != null ? _calendar.GetBudget(CurrentDay) : 0;
        public int TimePointsRemaining => Math.Max(0, DailyTimePointBudget - TimePointsSpentToday);

        public void ConfigureCalendar(LifeCalendar calendar) => _calendar = calendar;

        /// <summary>방의 시계. 세이브에 없다(달력처럼 읽은 뒤 다시 넣는다). 없으면 개인 예산만 본다.</summary>
        public void ConfigureTimeGate(ITimePointGate gate) => _timeGate = gate;

        public bool CanSpendTimePoints(int cost)
            => cost >= 0 && DayState == StudentDayState.InProgress && cost <= TimePointsRemaining && (_timeGate == null || _timeGate.CanSpend(cost));

        public bool TrySpendTimePoints(int cost)
        {
            if (!CanSpendTimePoints(cost)) return false;
            TimePointsSpentToday += cost;
            _timeGate?.Spent(cost);
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
            _timeGate?.Spent(policy.TimePointCost);
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
