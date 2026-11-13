using System;
using System.Collections.Generic;
using System.Linq;
using Rootborn.Core.StudentLife;
using Rootborn.Game;
using Rootborn.Game.Tests;

namespace Rootborn.Balance.Tests
{
    /// <summary>
    /// 한 판 돌리기(설계 6.2·6.3·13절): 전략의 행동을 하루 동안 맨 앞부터 되는 것 하나씩 하고, 할 것이 없으면 잔다 — 그 판의 잠 규칙이 자지 않는 밤이면
    /// 날이 넘어갈 때까지 시계만 돌린다. 모든 행동은 GameSession 명령이다. 잠·지침·회복은 게임이 판단한다(Sleep, AdvanceTime, IsExhausted만 쓴다).
    /// 수입은 보상 식을 다시 계산하지 않고 지갑 잔액의 변화로만 센다(시작 지갑은 번 것이 아니다).
    /// </summary>
    internal static class BalanceRunner
    {
        private const int MaxStepsPerDay = 200;
        private const int MaxRollOverTries = 8;
        // 날이 다 갔을 때 날만 넘기는 초. 0이면 시계의 남은 조각에 더하지 않아 분이 생기지 않고, AdvanceTime은 날이 다 간 것을 보고 날만 넘긴다.
        private const float RollOverOnlySeconds = 0f;

        /// <summary>1단계 보고: 전략 × 대상(id 순) × 점수 단계(Scored일 때만, 아니면 기준 가정 한 번). 점수 단계는 기준 가정에서 점수만 "fixed N"으로 바꾼다. 순서는 설정 순서.</summary>
        public static IEnumerable<RunResult> RunAll(BalanceConfig config, int days)
        {
            foreach (var strategy in config.Strategies)
            {
                var setups = strategy.Scored ? config.Tiers.Select(t => TierSetup(config, t)).ToList() : new List<RunSetup> { RunSetup.Baseline(config) };
                foreach (var target in RunTargets.All(strategy.Each, TestWorld.Definitions))
                    foreach (var setup in setups)
                        yield return Run(strategy, target, setup, config, days);
            }
        }

        /// <summary>기준 가정에 1단계 점수 단계 하나를 넣은 것.</summary>
        public static RunSetup TierSetup(BalanceConfig config, ScoreTier tier)
        {
            var setup = RunSetup.Baseline(config);
            setup.Score = ScoreModels.Fixed(tier.Score);
            return setup;
        }

        public static RunResult Run(StrategySpec strategy, object target, RunSetup setup, BalanceConfig config, int days)
            => Run(strategy, target, setup, config, days, strategy.Do.Select(BalanceActions.Get).ToList(), RunStarts.Get(strategy.Start));

        /// <summary>행동 목록을 직접 받는다(시험용 가짜 행동을 넣을 때). strategy.Do는 보지 않는다.</summary>
        public static RunResult Run(StrategySpec strategy, object target, RunSetup setup, BalanceConfig config, int days, IReadOnlyList<IBalanceAction> actions)
            => Run(strategy, target, setup, config, days, actions, RunStarts.Get(strategy.Start));

        /// <summary>시작 상태를 직접 받는다(시험용 시작 상태를 넣을 때). strategy.Start는 보지 않는다.</summary>
        public static RunResult Run(StrategySpec strategy, object target, RunSetup setup, BalanceConfig config, int days, IRunStart start)
            => Run(strategy, target, setup, config, days, strategy.Do.Select(BalanceActions.Get).ToList(), start);

        private static RunResult Run(StrategySpec strategy, object target, RunSetup setup, BalanceConfig config, int days, IReadOnlyList<IBalanceAction> actions, IRunStart start)
        {
            var game = start.Start(new MemorySaveStore(), target);
            var rule = SleepRules.Parse(setup.Sleep, game.TimeRules);
            int dayEnd = game.TimeRules.DayEndMinutes;
            int runDay = 0;
            var context = new RunContext
            {
                Target = target,
                Score = ScoreModels.Create(setup.Score, config.Axes.FailScore, config.Axes.PassScore),
                Success = SuccessModels.Create(setup.PartTime),
                MinigameShare = setup.MinigameShare,
                FitsNow = (g, minutes, minigame) => rule.MayStart(new SleepMoment { RunDay = runDay, NowMinutes = g.Student.TimeMinutes, ActionMinutes = minutes, MinigameMinutes = minigame, DayEndMinutes = dayEnd }),
            };

            var run = new RunResult
            {
                StrategyId = strategy.Id,
                TargetId = RunTargets.IdOf(target),
                Setup = setup.Copy(),
                StartDay = game.Student.CurrentDay,
                StartWallet = game.Wallet.Balance,
                StartWeekend = game.Student.Calendar.IsWeekend(game.Student.CurrentDay),
            };
            string name = run.StrategyId + "/" + run.TargetId + "/" + setup.Id;

            // 지갑 추적: 잔액이 바뀔 때마다 차를 번 것(+)이나 쓴 것(−)에 더한다.
            int lastBalance = game.Wallet.Balance, earned = 0, spent = 0;
            game.Wallet.Changed += () =>
            {
                int now = game.Wallet.Balance, delta = now - lastBalance;
                lastBalance = now;
                if (delta > 0) earned += delta;
                else spent -= delta;
            };

            setup.Observer?.Started(game);
            // 다음 날을 시작할 때 생긴 변화도 빠지지 않게 누적 값에서 그날 몫을 뺀다.
            int request = 0, earnedBefore = 0, spentBefore = 0;
            for (int d = 0; d < days; d++)
            {
                runDay = d + 1;
                bool sleeps = rule.SleepsTonight(runDay);
                var record = new DayRecord
                {
                    Day = game.Student.CurrentDay,
                    Weekend = game.Student.Calendar.IsWeekend(game.Student.CurrentDay),
                    StartedAtMinutes = game.Student.TimeMinutes,
                    EndedAtMinutes = game.Student.TimeMinutes,
                };
                PlayDay(game, actions, context, rule, runDay, sleeps, setup, record, name, ref request, () => earned);
                int nightDay = game.Student.CurrentDay, nightMinutes = game.Student.TimeMinutes;

                record.FacilityLevel = game.Adult.IsAdult ? game.Adult.FacilityLevel : 0;
                record.EnergyAtEnd = game.Student.Energy;
                record.FocusAtEnd = game.Student.Focus;
                foreach (var slot in game.Inventory.Slots)
                    if (slot.Item != null && slot.Count > 0 && slot.Item != game.Inventory.Currency)
                        record.OtherItems[slot.Item.Id] = (record.OtherItems.TryGetValue(slot.Item.Id, out int c) ? c : 0) + slot.Count;

                if (sleeps)
                {
                    // 잠(하루 결과의 마일스톤·캠페인 보상도 그날 것으로 센다) → 그날의 지갑 → 다음 날.
                    record.Slept = true;
                    record.SleptAtMinutes = game.Student.TimeMinutes;
                    if (!game.Sleep().Ended) throw new InvalidOperationException(name + ": " + record.Day + "일째에 잠들지 못했다");
                    Close(game, record, earned, spent, ref earnedBefore, ref spentBefore);
                    run.Days.Add(record);
                    if (!game.StartNextDay()) throw new InvalidOperationException(name + ": " + record.Day + "일째 다음 날을 시작하지 못했다");
                    setup.Observer?.Night(game, true, nightDay, nightMinutes);
                }
                else
                {
                    // 자지 않는 밤: 날이 넘어갈 때까지 시계를 돌린다(혼자인 방은 AdvanceTime이 회복 없이 마감하고 다음 날을 연다).
                    // 다음 날을 여는 것까지 한 부름이라, 그때의 지갑 변화(있다면)는 자는 판과 달리 이 밤의 날에 들어간다.
                    // 넘겨 잇는 밤에 행동 도중 날이 이미 넘어갔으면 여기서는 아무것도 보내지 않는다(그날의 체력·집중 칸은 넘어간 뒤의 값이다).
                    // 07:00을 넘겨 끝난 행동 뒤에는 날이 다 간 상태라 날만 넘기고, 다음 날은 게임이 07:00 + 넘긴 분에 시작한다.
                    RollOverAwake(game, record.Day, name);
                    Close(game, record, earned, spent, ref earnedBefore, ref spentBefore);
                    run.Days.Add(record);
                    setup.Observer?.Night(game, false, nightDay, nightMinutes);
                }
            }
            return run;
        }

        // 그날의 지갑과 번 것·쓴 것.
        private static void Close(GameSession game, DayRecord record, int earned, int spent, ref int earnedBefore, ref int spentBefore)
        {
            record.WalletAtEnd = game.Wallet.Balance;
            record.Earned = earned - earnedBefore;
            record.Spent = spent - spentBefore;
            earnedBefore = earned;
            spentBefore = spent;
        }

        // 남은 분만큼의 실제 초를 보내 날을 넘긴다. 날이 이미 다 갔으면(마지막 행동이 날 넘김 시각에 딱 끝남) 분을 보내지 않고
        // 날만 넘긴다 — 1분을 보내면 새 날이 07:01에 시작한다. 몇 번 보내도 넘어가지 않으면 판 이름·날을 말하며 실패한다.
        private static void RollOverAwake(GameSession game, int day, string name)
        {
            var rules = game.TimeRules;
            for (int tries = 0; game.Student.CurrentDay == day; tries++)
            {
                if (tries >= MaxRollOverTries) throw new InvalidOperationException(name + ": " + day + "일째가 자지 않은 채로 넘어가지 않는다(시각 " + game.Student.TimeMinutes + ")");
                if (game.Student.HasDayRunOut)
                {
                    game.AdvanceTime(RollOverOnlySeconds, false);
                    continue;
                }
                int left = Math.Max(1, rules.DayEndMinutes - game.Student.TimeMinutes);
                game.AdvanceTime(DayAssumptions.RealSeconds(rules, left) + 0.001f, false);
            }
            if (game.Student.DayState != StudentDayState.InProgress)
                throw new InvalidOperationException(name + ": " + day + "일째를 자지 않고 넘긴 뒤 다음 날이 진행 중이 아니다(" + game.Student.DayState + ")");
        }

        private static void PlayDay(GameSession game, IReadOnlyList<IBalanceAction> actions, RunContext context, ISleepRule rule, int runDay, bool sleeps, RunSetup setup,
            DayRecord record, string name, ref int request, Func<int> earnedSoFar)
        {
            var skipped = new bool[actions.Count]; // 이 날 거절된 행동 종류는 다시 보지 않는다
            int dayEnd = game.TimeRules.DayEndMinutes;
            for (int guard = 0; ; guard++)
            {
                if (guard >= MaxStepsPerDay) throw new InvalidOperationException(name + ": " + record.Day + "일째가 " + MaxStepsPerDay + "번을 넘겨 끝나지 않는다");

                // 지치면 아무것도 시작하지 못한다(자야 풀린다): 자는 밤이면 그 자리에서 자고, 아니면 날이 넘어가기를 기다린다.
                // 날 넘김을 기다리는 동안(자지 않는 밤) 지친 것은 그 날이 아니라 다음 날의 첫 검사에서 보여 다음 날의 지친 날로 센다.
                if (game.IsExhausted)
                {
                    record.Exhausted = true;
                    record.Stop = sleeps ? StopReasons.Exhausted : StopReasons.DayRanOut;
                    return;
                }

                PlannedStep step = null;
                int chosen = -1;
                bool anyPlan = false;
                for (int i = 0; i < actions.Count && step == null; i++)
                {
                    if (skipped[i]) continue;
                    var plan = actions[i].Plan(game, context);
                    if (plan == null) continue;
                    anyPlan = true;
                    var moment = new SleepMoment
                    {
                        RunDay = runDay, NowMinutes = game.Student.TimeMinutes, WalkMinutes = plan.Walks ? setup.TravelFor(plan) : 0, ActionMinutes = plan.Minutes, MinigameMinutes = plan.MinigameMinutes, DayEndMinutes = dayEnd,
                    };
                    if (!rule.MayStart(moment)) continue;
                    step = plan;
                    chosen = i;
                }

                if (step == null)
                {
                    record.Stop = anyPlan ? (sleeps ? StopReasons.Bedtime : StopReasons.DayRanOut) : BodyOrNothing(game);
                    return;
                }

                if (step.Walks)
                {
                    int travel = setup.TravelFor(step);
                    DayAssumptions.Walk(game, travel);
                    setup.Observer?.Walked(game, step, travel);
                    record.TravelMinutes += travel;
                    record.TravelRealSeconds += DayAssumptions.RealSeconds(game.TimeRules, travel);
                    if (game.Student.CurrentDay != record.Day) throw new InvalidOperationException(name + ": " + record.Day + "일째 걷기가 날을 넘겼다");

                    // 시계의 남은 조각 때문에 걷기가 1분 더 걸릴 수 있다: 걸은 뒤 다시 보고, 규칙 끝을 넘으면 하지 않는다(걷기는 남는다).
                    if (!context.FitsNow(game, step.Minutes, step.MinigameMinutes))
                    {
                        skipped[chosen] = true;
                        continue;
                    }
                }

                int earnedBefore = earnedSoFar();
                int actDay = game.Student.CurrentDay, actMinutes = game.Student.TimeMinutes;
                // 요청 id는 그 판(세션) 안에서만 겹치지 않으면 된다. 게임은 쓴 요청 id를 모두 세이브에 남기고 저장할 때마다 다시 쓰므로
                // 짧게 둔다(판 이름을 넣으면 100일 판에서 저장이 무거워진다).
                var outcome = step.Do(game, record.Day + ":" + request++);
                setup.Observer?.Acted(game, step, outcome, actDay, actMinutes);
                // 날 넘김: 넘겨 잇는 밤이면 게임이 하는 대로 둔다(미니게임 도중에 07:00이 되면 게임이 날을 넘기고 정산을 거절한다 — CarryFactsTests).
                // 그 행동은 시작한 날의 줄에 적고 그날을 끝낸다. 아니면 판을 멈춘다.
                bool rolledOver = game.Student.CurrentDay != record.Day;
                if (rolledOver && !rule.CarriesTonight(runDay)) throw new InvalidOperationException(name + ": " + record.Day + "일째 행동이 날을 넘겼다(07:00)");
                if (!rolledOver)
                {
                    // 행동 뒤 검사: 잠 규칙이 허용한 끝을 넘겼으면 계획 길이 예측이 실제보다 짧았다는 뜻이다.
                    int? latest = rule.LatestEnd(runDay, dayEnd);
                    if (latest.HasValue && game.Student.TimeMinutes > latest.Value)
                        throw new InvalidOperationException(name + ": " + record.Day + "일째 " + step.Id + "이 " + game.Student.TimeMinutes + "분에 끝나 잠 규칙의 끝("
                            + latest.Value + "분)을 넘겼다 — 계획 길이 " + step.Minutes + "분이 실제보다 짧았다");
                }
                if (outcome.Done)
                {
                    // 미니게임을 한 행동이 번 것만(지갑 누적의 앞뒤 차). 자동 넘기기·올리기는 미니게임 초가 0이라 들어가지 않는다.
                    if (outcome.MinigameRealSeconds > 0) record.MinigameEarned += earnedSoFar() - earnedBefore;
                    string id = outcome.Id ?? step.Id;
                    record.Actions++;
                    record.ActionCounts[id] = (record.ActionCounts.TryGetValue(id, out int c) ? c : 0) + 1;
                    record.WorkMinutes += outcome.Minutes ?? step.Minutes;
                    record.MinigameRealSeconds += outcome.MinigameRealSeconds;
                    record.EndedAtMinutes = game.Student.TimeMinutes;
                    if (outcome.AutoSkipped) record.AutoSkips++;
                    if (outcome.Upgraded) record.Upgraded = true;
                    if (rolledOver) { record.Stop = StopReasons.DayRanOut; return; }
                    continue;
                }

                if (rolledOver)
                {
                    record.RefusedAfterDayEnd++;
                    record.Stop = StopReasons.DayRanOut;
                    return;
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
