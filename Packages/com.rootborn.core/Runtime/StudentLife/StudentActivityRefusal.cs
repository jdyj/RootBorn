namespace Rootborn.Core.StudentLife
{
    public enum StudentActivityRefusalReason
    {
        None,
        NoTime,
        NotToday,
        DoneToday,
        NoEnergy,
        /// <summary>체력이 0이 되어 지쳤다. 자야 풀린다.</summary>
        Exhausted,
        /// <summary>같이 하는 방에서 앞의 활동을 하는 중이다(끝나는 시각까지). 걷기·대화는 된다.</summary>
        Busy
    }

    /// <summary>
    /// 시간·체력이 드는 활동을 왜 못 하는지 판정한다(원본 StudentActivityRefusalText의 판정 부분).
    /// 요구 조건 같은 다른 이유는 None이다. 문장은 화면 쪽이 아래 현지화 키로 찾는다.
    /// </summary>
    public static class StudentActivityRefusal
    {
        public static StudentActivityRefusalReason Explain(StudentLifeProgress progress, ActivityTimePolicy timePolicy, int energyCost, int focusCost)
        {
            if (progress == null) return StudentActivityRefusalReason.None;
            if (progress.DayState == StudentDayState.InProgress && progress.IsExhausted) return StudentActivityRefusalReason.Exhausted;
            if (progress.DayState == StudentDayState.InProgress && progress.IsBusy) return StudentActivityRefusalReason.Busy;
            if (progress.DayState != StudentDayState.InProgress || (timePolicy != null && !progress.CanSpendTimePoints(timePolicy.TimePointCost))
                || !progress.CanStartActivity) return StudentActivityRefusalReason.NoTime;
            if (timePolicy != null && !timePolicy.IsAllowedDay(progress.CurrentDay)) return StudentActivityRefusalReason.NotToday;
            if (!progress.CanUseTimePolicy(timePolicy)) return StudentActivityRefusalReason.DoneToday;
            if (!progress.CanSpend(energyCost, focusCost)) return StudentActivityRefusalReason.NoEnergy;
            return StudentActivityRefusalReason.None;
        }

        /// <summary>원본과 같은 현지화 키. None이면 빈 문자열.</summary>
        public static string LocalizationKey(StudentActivityRefusalReason reason)
        {
            switch (reason)
            {
                case StudentActivityRefusalReason.NoTime: return "ui.prompt.no-time";
                case StudentActivityRefusalReason.NotToday: return "ui.prompt.not-today";
                case StudentActivityRefusalReason.DoneToday: return "ui.prompt.done-today";
                case StudentActivityRefusalReason.NoEnergy: return "ui.prompt.no-energy";
                case StudentActivityRefusalReason.Exhausted: return "ui.prompt.exhausted";
                case StudentActivityRefusalReason.Busy: return "ui.prompt.busy";
                default: return string.Empty;
            }
        }

        /// <summary>현지화 문장이 없을 때 쓰는 원본의 기본 영어 문장. None이면 빈 문자열.</summary>
        public static string FallbackText(StudentActivityRefusalReason reason)
        {
            switch (reason)
            {
                case StudentActivityRefusalReason.NoTime: return "No time left today. Try again tomorrow.";
                case StudentActivityRefusalReason.NotToday: return "You can't do this today.";
                case StudentActivityRefusalReason.DoneToday: return "You already did this today. Try again tomorrow.";
                case StudentActivityRefusalReason.NoEnergy: return "Not enough energy or focus. Rest first.";
                case StudentActivityRefusalReason.Exhausted: return "You are exhausted. Go to sleep.";
                case StudentActivityRefusalReason.Busy: return "You are still busy with your last activity.";
                default: return string.Empty;
            }
        }
    }
}
