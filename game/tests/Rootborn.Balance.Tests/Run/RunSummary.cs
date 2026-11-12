using System;

namespace Rootborn.Balance.Tests
{
    /// <summary>판 하나를 한 줄로 줄인 값. <see cref="RunResult.Days"/>만 읽는 순수 계산이다.</summary>
    internal sealed class RunSummary
    {
        public float IncomePerDay;                       // 번 것 합 ÷ 날 수
        public float? IncomePerGameHour;                 // 번 것 합 ÷ (WorkMinutes 합 ÷ 60). 분모 0이면 null
        public float? IncomePerRealMinuteWithTravel;     // 번 것 합 ÷ ((걷기 + 미니게임 실제 초) ÷ 60). 분모 0이면 null
        public float? IncomePerRealMinuteMinigameOnly;   // 번 것 합 ÷ (미니게임 실제 초 ÷ 60). 분모 0이면 null
        public int FinalWallet;
        public int? Level2Day, Level3Day;                // 판 안에서 몇째 날(1부터). 닿지 않았으면 null
        public int? FirstAutoSkipDay, FirstBagFullDay;
        public int?[] PaybackDays = new int?[0];         // 올린 것마다. 차가 0 이하거나 앞·뒤 날이 없으면 null
        public float ActionsPerDay;

        public static RunSummary From(RunResult run)
        {
            var days = run.Days;
            var s = new RunSummary();
            if (days.Count == 0) return s;

            long earned = 0, actions = 0, workMinutes = 0;
            double travelSeconds = 0, minigameSeconds = 0;
            var upgrades = new System.Collections.Generic.List<int>();
            for (int i = 0; i < days.Count; i++)
            {
                var d = days[i];
                earned += d.Earned;
                actions += d.Actions;
                workMinutes += d.WorkMinutes;
                travelSeconds += d.TravelRealSeconds;
                minigameSeconds += d.MinigameRealSeconds;
                if (d.Upgraded) upgrades.Add(i);
                if (s.Level2Day == null && d.FacilityLevel >= 2) s.Level2Day = i + 1;
                if (s.Level3Day == null && d.FacilityLevel >= 3) s.Level3Day = i + 1;
                if (s.FirstAutoSkipDay == null && d.AutoSkips > 0) s.FirstAutoSkipDay = i + 1;
                if (s.FirstBagFullDay == null && d.Stop == StopReasons.BagFull) s.FirstBagFullDay = i + 1;
            }

            s.IncomePerDay = (float)earned / days.Count;
            s.ActionsPerDay = (float)actions / days.Count;
            s.FinalWallet = days[days.Count - 1].WalletAtEnd;
            if (workMinutes > 0) s.IncomePerGameHour = (float)(earned / (workMinutes / 60.0));
            if (travelSeconds + minigameSeconds > 0) s.IncomePerRealMinuteWithTravel = (float)(earned / ((travelSeconds + minigameSeconds) / 60.0));
            if (minigameSeconds > 0) s.IncomePerRealMinuteMinigameOnly = (float)(earned / (minigameSeconds / 60.0));

            s.PaybackDays = new int?[upgrades.Count];
            for (int u = 0; u < upgrades.Count; u++)
            {
                int at = upgrades[u];
                int from = u == 0 ? 0 : upgrades[u - 1] + 1;                       // 앞 구간: 이전 올린 날 다음부터
                int to = u + 1 < upgrades.Count ? upgrades[u + 1] : days.Count;    // 뒤 구간: 다음 올린 날 앞까지
                if (at - from <= 0 || to - (at + 1) <= 0) continue;
                double before = Mean(days, from, at), after = Mean(days, at + 1, to);
                double gain = after - before;
                if (gain <= 0) continue;
                s.PaybackDays[u] = (int)Math.Ceiling(days[at].Spent / gain);
            }
            return s;
        }

        private static double Mean(System.Collections.Generic.List<DayRecord> days, int from, int toExclusive)
        {
            long sum = 0;
            for (int i = from; i < toExclusive; i++) sum += days[i].Earned;
            return (double)sum / (toExclusive - from);
        }
    }
}
