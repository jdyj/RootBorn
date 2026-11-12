using System;
using System.Collections.Generic;

namespace Rootborn.Balance.Tests
{
    /// <summary>늘어난 수입으로 되찾는 날(<see cref="RunSummary.PaybackDays"/>)을 셀 수 없었던 까닭.</summary>
    internal static class PaybackGap
    {
        public const string NoSpan = "no-span";   // 앞이나 뒤에 올리지 않은 날이 없다(이어 올렸거나 판의 처음·끝)
        public const string NoGain = "no-gain";   // 뒤 평균 − 앞 평균이 0 이하
    }

    /// <summary>판 하나를 한 줄로 줄인 값. <see cref="RunResult.Days"/>만 읽는 순수 계산이다.</summary>
    internal sealed class RunSummary
    {
        public float IncomePerDay;                       // 번 것 합 ÷ 날 수
        public float? IncomePerGameHour;                 // 번 것 합 ÷ (WorkMinutes 합 ÷ 60). 분모 0이면 null
        public float? IncomePerRealMinuteWithTravel;     // 번 것 합 ÷ ((걷기 + 미니게임 실제 초) ÷ 60). 분모 0이면 null
        public float? IncomePerRealMinuteMinigameOnly;   // 미니게임으로 번 것 합 ÷ (미니게임 실제 초 ÷ 60). 분모 0이면 null
        public int FinalWallet;
        public int? Level2Day, Level3Day;                // 판 안에서 몇째 날(1부터). 닿지 않았으면 null
        public int? FirstAutoSkipDay, FirstBagFullDay;
        public int?[] PaybackDays = new int?[0];         // 올린 것마다: 쓴 값 ÷ (뒤 평균 − 앞 평균). 셀 수 없으면 null
        public string[] PaybackGaps = new string[0];     // PaybackDays와 같은 길이: null이면 값이 있고, 아니면 PaybackGap의 값
        public int?[] RecoverDays = new int?[0];         // 올린 것마다: 올린 날부터 번 것의 합이 쓴 값 이상이 되기까지의 날 수(올린 날 포함). 판 안에서 못 채우면 null
        public float ActionsPerDay;
        public float NetIncomePerDay;                    // (번 것 합 − 쓴 것 합) ÷ 날 수
        public int ExhaustionSeenDays;                   // 지침을 본 날 수(DayRecord.Exhausted — 마지막 일 도중에 지쳐 손해가 없던 날도 든다)
        public int StoppedExhaustedDays;                 // 지쳐서 멈춘 날 수(Stop == exhausted, 자는 밤에 지쳐 그 자리에서 잠)
        public int ZeroActionDays;                       // 행동이 하나도 없던 날 수(하루를 잃은 날)
        public int NightsAwake;                          // 자지 않은 밤 수
        public float AverageEnergyAtEnd;                 // 밤(잠·날 넘김) 전 체력의 평균
        public float? AverageSleptAtMinutes;             // 잔 날의 잔 시각 평균(그날 0시부터 센 분). 잔 날이 없으면 null

        /// <summary>판 전체.</summary>
        public static RunSummary From(RunResult run) => run.Days.Count == 0 ? new RunSummary() : From(run, run.Days.Count);

        /// <summary>판의 첫 upToDay일만(훑기의 시점). 1 ≤ upToDay ≤ 판의 날 수.</summary>
        public static RunSummary From(RunResult run, int upToDay)
        {
            var days = run.Days;
            if (upToDay < 1 || upToDay > days.Count) throw new ArgumentOutOfRangeException(nameof(upToDay), "시점은 1~판의 날 수(" + days.Count + ")다: " + upToDay);
            int count = upToDay;
            var s = new RunSummary();

            long earned = 0, spent = 0, minigameEarned = 0, actions = 0, workMinutes = 0, energy = 0, sleptAt = 0;
            int sleptDays = 0;
            double travelSeconds = 0, minigameSeconds = 0;
            var upgrades = new List<int>();
            for (int i = 0; i < count; i++)
            {
                var d = days[i];
                earned += d.Earned;
                minigameEarned += d.MinigameEarned;
                actions += d.Actions;
                workMinutes += d.WorkMinutes;
                travelSeconds += d.TravelRealSeconds;
                minigameSeconds += d.MinigameRealSeconds;
                energy += d.EnergyAtEnd;
                spent += d.Spent;
                if (d.Exhausted) s.ExhaustionSeenDays++;
                if (d.Stop == StopReasons.Exhausted) s.StoppedExhaustedDays++;
                if (d.Actions == 0) s.ZeroActionDays++;
                if (d.Slept) { sleptDays++; sleptAt += d.SleptAtMinutes; }
                else s.NightsAwake++;
                if (d.Upgraded) upgrades.Add(i);
                if (s.Level2Day == null && d.FacilityLevel >= 2) s.Level2Day = i + 1;
                if (s.Level3Day == null && d.FacilityLevel >= 3) s.Level3Day = i + 1;
                if (s.FirstAutoSkipDay == null && d.AutoSkips > 0) s.FirstAutoSkipDay = i + 1;
                if (s.FirstBagFullDay == null && d.Stop == StopReasons.BagFull) s.FirstBagFullDay = i + 1;
            }

            s.IncomePerDay = (float)earned / count;
            s.NetIncomePerDay = (float)(earned - spent) / count;
            s.ActionsPerDay = (float)actions / count;
            s.AverageEnergyAtEnd = (float)energy / count;
            if (sleptDays > 0) s.AverageSleptAtMinutes = (float)sleptAt / sleptDays;
            s.FinalWallet = days[count - 1].WalletAtEnd;
            if (workMinutes > 0) s.IncomePerGameHour = (float)(earned / (workMinutes / 60.0));
            if (travelSeconds + minigameSeconds > 0) s.IncomePerRealMinuteWithTravel = (float)(earned / ((travelSeconds + minigameSeconds) / 60.0));
            if (minigameSeconds > 0) s.IncomePerRealMinuteMinigameOnly = (float)(minigameEarned / (minigameSeconds / 60.0));

            s.PaybackDays = new int?[upgrades.Count];
            s.PaybackGaps = new string[upgrades.Count];
            s.RecoverDays = new int?[upgrades.Count];
            for (int u = 0; u < upgrades.Count; u++)
            {
                int at = upgrades[u];
                s.RecoverDays[u] = Recover(days, at, count);

                int from = u == 0 ? 0 : upgrades[u - 1] + 1;                       // 앞 구간: 이전 올린 날 다음부터
                int to = u + 1 < upgrades.Count ? upgrades[u + 1] : count;         // 뒤 구간: 다음 올린 날 앞까지
                if (at - from <= 0 || to - (at + 1) <= 0) { s.PaybackGaps[u] = PaybackGap.NoSpan; continue; }
                double before = Mean(days, from, at), after = Mean(days, at + 1, to);
                double gain = after - before;
                if (gain <= 0) { s.PaybackGaps[u] = PaybackGap.NoGain; continue; }
                s.PaybackDays[u] = (int)Math.Ceiling(days[at].Spent / gain);
            }
            return s;
        }

        // 올린 날부터 번 것을 더해 그날 쓴 값 이상이 되는 날까지의 날 수(올린 날이 1). 못 채우면 null.
        private static int? Recover(List<DayRecord> days, int at, int count)
        {
            long cost = days[at].Spent, sum = 0;
            for (int i = at; i < count; i++)
            {
                sum += days[i].Earned;
                if (sum >= cost) return i - at + 1;
            }
            return null;
        }

        private static double Mean(List<DayRecord> days, int from, int toExclusive)
        {
            long sum = 0;
            for (int i = from; i < toExclusive; i++) sum += days[i].Earned;
            return (double)sum / (toExclusive - from);
        }
    }
}
