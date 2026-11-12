using System;
using System.Collections.Generic;

namespace Rootborn.Core.StudentLife
{
    // 옛 "하루 시간 포인트"의 자리. 흐르는 시간(2026-11-12)부터 시간 포인트는 없다 — 하루에 할 수 있는 양은 깨어 있는 시간과 체력이 정한다
    // (StudentLifeProgress.Clock). 여기 남은 이름의 "TimePoint"는 옛 이름이고 지금 뜻은 아래 각 줄의 주석이다. 이름은 세이브 필드
    // (StudentLifeProgressSaveData.TimePointsSpentToday)와 짝이라 그대로 둔다. 이 파일의 값은 활동을 막는 판단에 쓰지 않는다(막는 것은 CanStartActivity).
    // 방의 시계에 묻던 문(ITimePointGate)은 방의 "때"와 함께 없앴다(흐르는 시간 4단계). 하루 한 번(OncePerDay)과 요일 정책은 그대로다.
    public sealed partial class StudentLifeProgress
    {
        private LifeCalendar _calendar;
        private readonly HashSet<string> _dailyTimePolicyIds = new HashSet<string>();

        public LifeCalendar Calendar => _calendar;
        /// <summary>오늘 한 큰 활동 수: 쓴 활동 정책의 <see cref="ActivityTimePolicy.TimePointCost"/> 합(데이터에서는 0 또는 1). 기록·저장만 하고 한도로 쓰지 않는다.</summary>
        public int TimePointsSpentToday { get; private set; }
        /// <summary>달력의 그날 칸 수(<see cref="LifeCalendar.GetBudget"/>, 평일·주말). 일과 계획판의 칸 수로 쓰고 활동 한도가 아니다.</summary>
        public int DailyTimePointBudget => _calendar != null ? _calendar.GetBudget(CurrentDay) : 0;
        /// <summary>그날 칸 수 − 오늘 한 큰 활동 수(0 아래로 내려가지 않음). 판단에 쓰지 않는다 — 시뮬레이션 기록(GameSession.TimePoints)과 테스트만 읽는다.</summary>
        public int TimePointsRemaining => Math.Max(0, DailyTimePointBudget - TimePointsSpentToday);

        public void ConfigureCalendar(LifeCalendar calendar) => _calendar = calendar;

        /// <summary>활동을 시작할 수 있다(<see cref="CanStartActivity"/>, 음수 몫은 거절). 개인 예산은 없다 — 이름의 "시간 포인트를 쓴다"는 옛 이름이다.</summary>
        public bool CanSpendTimePoints(int cost) => cost >= 0 && CanStartActivity;

        /// <summary>시작할 수 있으면 오늘 한 큰 활동 수에 <paramref name="cost"/>를 더한다(성인 시설 일).</summary>
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
