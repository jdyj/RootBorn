using System;
using System.Collections.Generic;
using System.Linq;
using Rootborn.Game;
using Rootborn.Game.Tests;

namespace Rootborn.Balance.Tests
{
    /// <summary>
    /// 잠 규칙(설정의 "sleep")의 등록부: 지금 시각에 걷기 + 행동 길이를 더한 행동을 시작해도 되는지.
    /// 새 규칙은 등록에 한 줄을 더한다.
    /// </summary>
    internal static class SleepRules
    {
        // (지금 시각, 걷기 분 + 행동 분, 취침 시각) → 시작해도 되는가
        private static readonly Dictionary<string, Func<int, int, int, bool>> Rules = new Dictionary<string, Func<int, int, int, bool>>(StringComparer.Ordinal)
        {
            { "finish-by-bedtime", (now, minutes, bedtime) => now + minutes <= bedtime },
            { "start-before-bedtime", (now, minutes, bedtime) => now < bedtime },
        };

        public static bool Knows(string name) => name != null && Rules.ContainsKey(name);

        public static Func<int, int, int, bool> Get(string name)
        {
            if (Knows(name)) return Rules[name];
            throw new ArgumentException("모르는 잠 규칙이다: " + name, nameof(name));
        }
    }

    /// <summary>
    /// 한 판 돌리기(설계 6.2·6.3절): 전략의 행동을 하루 동안 맨 앞부터 되는 것 하나씩 하고, 할 것이 없으면 잔다. 모든 행동은 GameSession 명령이다.
    /// 수입은 보상 식을 다시 계산하지 않고 지갑 잔액의 변화로만 센다(시작 지갑은 번 것이 아니다).
    /// </summary>
    internal static class BalanceRunner
    {
        private const int MaxStepsPerDay = 200;

        /// <summary>전략 × 대상(id 순) × 점수 단계(Scored일 때만, 아니면 한 번). 순서는 설정 순서.</summary>
        public static IEnumerable<RunResult> RunAll(BalanceConfig config, int days)
        {
            foreach (var strategy in config.Strategies)
            {
                var tiers = strategy.Scored ? config.Tiers : new ScoreTier[] { null };
                foreach (var target in RunTargets.All(strategy.Each, TestWorld.Definitions))
                    foreach (var tier in tiers)
                        yield return Run(strategy, target, tier, config, days);
            }
        }

        public static RunResult Run(StrategySpec strategy, object target, ScoreTier tier, BalanceConfig config, int days)
            => Run(strategy, target, tier, config, days, strategy.Do.Select(BalanceActions.Get).ToList());

        /// <summary>행동 목록을 직접 받는다(시험용 가짜 행동을 넣을 때). strategy.Do는 보지 않는다.</summary>
        public static RunResult Run(StrategySpec strategy, object target, ScoreTier tier, BalanceConfig config, int days, IReadOnlyList<IBalanceAction> actions)
        {
            var game = RunStarts.Get(strategy.Start).Start(new MemorySaveStore(), target);
            var fits = SleepRules.Get(config.Sleep);
            var assumptions = DayAssumptions.Current;
            int bedtime = game.TimeRules.BedtimeMinutes;
            var context = new RunContext
            {
                Target = target,
                Score = tier?.Score ?? 100, // 점수를 안 쓰는 판(알바)은 점수가 쓰이지 않는다
                Assumptions = assumptions,
                FitsNow = (g, minutes) => fits(g.Student.TimeMinutes, minutes, bedtime),
            };

            var run = new RunResult
            {
                StrategyId = strategy.Id,
                TargetId = RunTargets.IdOf(target),
                TierId = tier?.Id ?? "",
                StartDay = game.Student.CurrentDay,
                StartWallet = game.Wallet.Balance,
                StartWeekend = game.Student.Calendar.IsWeekend(game.Student.CurrentDay),
            };
            string name = run.StrategyId + "/" + run.TargetId + "/" + run.TierId;

            // 지갑 추적: 잔액이 바뀔 때마다 차를 번 것(+)이나 쓴 것(−)에 더한다.
            int lastBalance = game.Wallet.Balance, earned = 0, spent = 0;
            game.Wallet.Changed += () =>
            {
                int now = game.Wallet.Balance, delta = now - lastBalance;
                lastBalance = now;
                if (delta > 0) earned += delta;
                else spent -= delta;
            };

            // 다음 날을 시작할 때 생긴 변화도 빠지지 않게 누적 값에서 그날 몫을 뺀다.
            int request = 0, earnedBefore = 0, spentBefore = 0;
            for (int d = 0; d < days; d++)
            {
                var record = new DayRecord
                {
                    Day = game.Student.CurrentDay,
                    Weekend = game.Student.Calendar.IsWeekend(game.Student.CurrentDay),
                    EndedAtMinutes = game.Student.TimeMinutes,
                };
                PlayDay(game, actions, context, config.Sleep, bedtime, record, name, ref request);

                record.FacilityLevel = game.Adult.IsAdult ? game.Adult.FacilityLevel : 0;
                record.EnergyAtEnd = game.Student.Energy;
                record.FocusAtEnd = game.Student.Focus;
                foreach (var slot in game.Inventory.Slots)
                    if (slot.Item != null && slot.Count > 0 && slot.Item != game.Inventory.Currency)
                        record.OtherItems[slot.Item.Id] = (record.OtherItems.TryGetValue(slot.Item.Id, out int c) ? c : 0) + slot.Count;

                // 잠(하루 결과의 마일스톤·캠페인 보상도 그날 것으로 센다) → 그날의 지갑 → 다음 날.
                if (!game.Sleep().Ended) throw new InvalidOperationException(name + ": " + record.Day + "일째에 잠들지 못했다");
                record.WalletAtEnd = game.Wallet.Balance;
                record.Earned = earned - earnedBefore;
                record.Spent = spent - spentBefore;
                earnedBefore = earned;
                spentBefore = spent;
                run.Days.Add(record);
                if (!game.StartNextDay()) throw new InvalidOperationException(name + ": " + record.Day + "일째 다음 날을 시작하지 못했다");
            }
            return run;
        }

        private static void PlayDay(GameSession game, IReadOnlyList<IBalanceAction> actions, RunContext context, string sleep, int bedtime,
            DayRecord record, string name, ref int request)
        {
            var fits = SleepRules.Get(sleep);
            var skipped = new bool[actions.Count]; // 이 날 거절된 행동 종류는 다시 보지 않는다
            int travel = context.Assumptions.TravelMinutes;
            for (int guard = 0; ; guard++)
            {
                if (guard >= MaxStepsPerDay) throw new InvalidOperationException(name + ": " + record.Day + "일째가 " + MaxStepsPerDay + "번을 넘겨 끝나지 않는다");

                PlannedStep step = null;
                int chosen = -1;
                bool anyPlan = false;
                for (int i = 0; i < actions.Count && step == null; i++)
                {
                    if (skipped[i]) continue;
                    var plan = actions[i].Plan(game, context);
                    if (plan == null) continue;
                    anyPlan = true;
                    if (!fits(game.Student.TimeMinutes, (plan.Walks ? travel : 0) + plan.Minutes, bedtime)) continue;
                    step = plan;
                    chosen = i;
                }

                if (step == null)
                {
                    record.Stop = anyPlan ? StopReasons.Bedtime : BodyOrNothing(game);
                    return;
                }

                if (step.Walks)
                {
                    DayAssumptions.Walk(game, travel);
                    record.TravelMinutes += travel;
                    record.TravelRealSeconds += travel * game.TimeRules.RealSecondsPerGameDay / (24 * 60);
                }

                var outcome = step.Do(game, name + ":" + record.Day + ":" + request++ + ":" + step.Id);
                if (game.Student.CurrentDay != record.Day) throw new InvalidOperationException(name + ": " + record.Day + "일째 행동이 날을 넘겼다(07:00)");
                if (outcome.Done)
                {
                    string id = outcome.Id ?? step.Id;
                    record.Actions++;
                    record.ActionCounts[id] = (record.ActionCounts.TryGetValue(id, out int c) ? c : 0) + 1;
                    record.WorkMinutes += outcome.Minutes ?? step.Minutes;
                    record.MinigameRealSeconds += outcome.MinigameRealSeconds;
                    record.EndedAtMinutes = game.Student.TimeMinutes;
                    if (outcome.AutoSkipped) record.AutoSkips++;
                    if (outcome.Upgraded) record.Upgraded = true;
                    continue;
                }

                if (outcome.BagFull)
                {
                    record.Stop = StopReasons.BagFull;
                    return;
                }
                skipped[chosen] = true;
            }
        }

        private static string BodyOrNothing(GameSession game)
            => game.IsExhausted || game.Student.Energy <= 1 || game.Student.Focus <= 1 ? StopReasons.Body : StopReasons.NothingToDo;
    }
}
