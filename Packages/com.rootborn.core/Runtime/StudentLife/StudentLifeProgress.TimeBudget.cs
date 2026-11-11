using System;
using System.Collections.Generic;

namespace Rootborn.Core.StudentLife
{
    // 하루 시간 포인트 — 흐르는 시간(2026-11-12)부터는 한도가 아니고 아무 판단에도 쓰지 않는다. 하루에 할 수 있는 양은 깨어 있는 시간과
    // 체력이 정한다(StudentLifeProgress.Clock). TimePointsSpentToday는 오늘 한 큰 활동 수의 기록으로만 세고 옛 세이브 필드(이름·형 그대로)로 남는다.
    // 방의 시계에 묻던 문(ITimePointGate)은 방의 "때"와 함께 없앴다(흐르는 시간 4단계). 하루 한 번(OncePerDay)과 요일 정책은 그대로다.
    public sealed partial class StudentLifeProgress
    {
        private LifeCalendar _calendar;
        private readonly HashSet<string> _dailyTimePolicyIds = new HashSet<string>();

        public LifeCalendar Calendar => _calendar;
        public int TimePointsSpentToday { get; private set; }
        public int DailyTimePointBudget => _calendar != null ? _calendar.GetBudget(CurrentDay) : 0;
        public int TimePointsRemaining => Math.Max(0, DailyTimePointBudget - TimePointsSpentToday);

        public void ConfigureCalendar(LifeCalendar calendar) => _calendar = calendar;

        /// <summary>활동을 시작할 수 있다(<see cref="CanStartActivity"/>). 개인 예산은 보지 않는다.</summary>
        public bool CanSpendTimePoints(int cost) => cost >= 0 && CanStartActivity;

        public bool TrySpendTimePoints(int cost)
        {
            if (!CanSpendTimePoints(cost)) return false;
            TimePointsSpentToday += cost;
            return true;
        }

        /// <summary>정책이 없으면 활동을 시작할 수 있는 몸 상태이기만 하면 된다(하루 진행 중, 지치지 않음, 하는 중 아님).</summary>
        public bool CanUseTimePolicy(ActivityTimePolicy policy)
        {
            if (!CanStartActivity) return false;
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
