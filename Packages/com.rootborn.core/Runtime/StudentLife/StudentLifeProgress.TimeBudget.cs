using System;
using System.Collections.Generic;

namespace Rootborn.Core.StudentLife
{
    /// <summary>
    /// 활동이 방의 시계(때)에 묻는 쪽. 방이 막으면 시작하지 못하고, 쓴 몫은 알린다. 혼자인 방은 막지 않는다.
    /// 흐르는 시간(2026-11-12)에서 개인 시간 포인트 한도는 없어졌고, 이것은 방의 "때"를 없앨 때(설계 6절 4단계) 함께 없앤다.
    /// </summary>
    public interface ITimePointGate
    {
        bool CanSpend(int points);
        void Spent(int points);
    }

    // 하루 시간 포인트 — 흐르는 시간(2026-11-12)부터는 한도가 아니다. 하루에 할 수 있는 양은 깨어 있는 시간과 체력이 정한다
    // (StudentLifeProgress.Clock). TimePointsSpentToday는 방의 시계에 알린 몫과 성인 일·미니게임 예약 확인에만 세고, 옛 세이브 필드로 남는다.
    // 하루 한 번(OncePerDay)과 요일 정책은 그대로다.
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

        /// <summary>활동을 시작할 수 있고(<see cref="CanStartActivity"/>) 방이 막지 않는다. 개인 예산은 보지 않는다.</summary>
        public bool CanSpendTimePoints(int cost)
            => cost >= 0 && CanStartActivity && (_timeGate == null || _timeGate.CanSpend(cost));

        public bool TrySpendTimePoints(int cost)
        {
            if (!CanSpendTimePoints(cost)) return false;
            TimePointsSpentToday += cost;
            _timeGate?.Spent(cost);
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
