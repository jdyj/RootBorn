using System;
using Rootborn.Core.Careers;
using Rootborn.Game;
using Rootborn.Game.Tests;

namespace Rootborn.Balance.Tests
{
    /// <summary>지금 시설을 올릴 수 있으면 올린다(CanUpgradeFacility가 된다고 할 때만). 걷지 않고 시간도 쓰지 않는다.</summary>
    internal sealed class UpgradeFacilityAction : IBalanceAction
    {
        public string Kind => "upgrade-facility";

        public PlannedStep Plan(GameSession game, RunContext context)
        {
            if (!game.Adult.IsAdult || game.CanUpgradeFacility() != AdultCareerResultKind.Applied) return null;
            return new PlannedStep
            {
                Id = Kind,
                Minutes = 0,
                Walks = false,
                InteractionKind = PlannedStep.FacilityKind,
                Do = (g, request) =>
                {
                    var result = g.UpgradeFacility(request);
                    return result.Applied ? new StepOutcome { Done = true, Upgraded = true } : BalanceActions.Refused(result);
                },
            };
        }
    }

    /// <summary>
    /// 지금 할 수 있는 시설 일 중 최소 레벨이 가장 높은 것(같으면 id 순 첫째)을 미니게임으로 한다.
    /// 할 수 있는 일이 없고 가방이 차서 못 하는 일만 있으면 그것을 계획으로 내어, 명령의 거절로 하루가 "가방 참"으로 끝나게 한다.
    /// </summary>
    internal sealed class BestFacilityWorkAction : IBalanceAction
    {
        public string Kind => "best-facility-work";

        public PlannedStep Plan(GameSession game, RunContext context)
        {
            var work = FacilityWorks.Highest(game, w => w.CanWork)?.Work
                ?? FacilityWorks.Highest(game, w => w.Direct == AdultCareerResultKind.InsufficientCapacity)?.Work;
            return work == null ? null : FacilityWorks.PlanPlay(game, work, context);
        }
    }

    /// <summary>최소 레벨이 정확히 그 레벨인 일 중 할 수 있는 것(같으면 id 순 첫째)만 미니게임으로 한다. 시설을 올려도 다른 레벨의 일은 하지 않는다.</summary>
    internal sealed class FacilityWorkAtLevelAction : IBalanceAction
    {
        public const string KindName = "facility-work-level";
        private readonly int _level;

        public FacilityWorkAtLevelAction(int level) { _level = level; }

        public string Kind => KindName;

        public PlannedStep Plan(GameSession game, RunContext context)
        {
            var work = FacilityWorks.Highest(game, w => w.CanWork && w.Work.MinimumFacilityLevel == _level)?.Work;
            return work == null ? null : FacilityWorks.PlanPlay(game, work, context);
        }
    }

    /// <summary>자동 넘기기가 되는 일(CanAutoSkip) 중 최소 레벨이 가장 높은 것을 넘긴다. 미니게임 실제 시간은 0이다.</summary>
    internal sealed class AutoSkipFacilityWorkAction : IBalanceAction
    {
        public string Kind => "auto-skip-facility-work";

        public PlannedStep Plan(GameSession game, RunContext context)
        {
            var work = FacilityWorks.Highest(game, w => w.CanAutoSkip)?.Work;
            return work == null ? null : FacilityWorks.PlanAutoSkip(work);
        }
    }

    /// <summary>
    /// 지금 할 수 있는 일(CanWork) 중 최소 레벨이 가장 높은 것이 넘길 수 있을 때만 그것을 넘긴다. 아니면 null —
    /// 다음 행동(직접 하기)이 그 일의 숙련을 쌓는다.
    /// </summary>
    internal sealed class AutoSkipBestFacilityWorkAction : IBalanceAction
    {
        public string Kind => "auto-skip-best-facility-work";

        public PlannedStep Plan(GameSession game, RunContext context)
        {
            var best = FacilityWorks.Highest(game, w => w.CanWork);
            return best.HasValue && best.Value.CanAutoSkip ? FacilityWorks.PlanAutoSkip(best.Value.Work) : null;
        }
    }

    internal static class FacilityWorks
    {
        /// <summary>지금 시설의 일 중 조건에 맞는 것에서 최소 레벨이 가장 높은 것(같으면 id 순 첫째). 없으면 null.</summary>
        public static FacilityWorkAvailability? Highest(GameSession game, Func<FacilityWorkAvailability, bool> can)
        {
            if (!game.Adult.IsAdult) return null;
            var status = game.FacilityStatus(game.Adult.FacilityId);
            if (status == null) return null;
            FacilityWorkAvailability? best = null;
            foreach (var w in status.Works)
            {
                if (!can(w)) continue;
                var work = w.Work;
                var top = best?.Work;
                if (top == null || work.MinimumFacilityLevel > top.MinimumFacilityLevel
                    || (work.MinimumFacilityLevel == top.MinimumFacilityLevel && string.CompareOrdinal(work.Id, top.Id) < 0))
                    best = w;
            }
            return best;
        }

        /// <summary>그 일을 미니게임으로 하는 계획(길이는 미니게임이 더 길면 그 길이). 점수는 정산할 때 그 판의 점수 모델에서 하나 얻는다.</summary>
        public static PlannedStep PlanPlay(GameSession game, FacilityWork work, RunContext context)
            => new PlannedStep
            {
                Id = work.Id,
                Minutes = ActionLength.WithMinigame(game, work.TimeCostMinutes, work.Minigame, context.MinigameShare),
                MinigameMinutes = ActionLength.MinigameMinutes(game, work.Minigame, context.MinigameShare),
                Walks = true,
                InteractionKind = PlannedStep.FacilityKind,
                Do = (g, request) => Play(g, work, request, context),
            };

        /// <summary>
        /// 그 일을 자동으로 넘기는 계획. 점수 모델은 쓰지 않는다(차례도 넘기지 않는다) — 자동 넘기기는 게임이 점수를 따로 정하므로
        /// 명령에는 범위 안의 값(100)만 넘긴다.
        /// </summary>
        public static PlannedStep PlanAutoSkip(FacilityWork work)
            => new PlannedStep
            {
                Id = work.Id,
                Minutes = work.TimeCostMinutes,
                Walks = true,
                InteractionKind = PlannedStep.FacilityKind,
                Do = (g, request) =>
                {
                    var result = g.CompleteFacilityWork(work.Id, request, AutoSkipScoreArgument, autoSkip: true);
                    return result.Applied ? new StepOutcome { Done = true, AutoSkipped = true } : BalanceActions.Refused(result);
                },
            };

        private const int AutoSkipScoreArgument = 100;

        /// <summary>
        /// 예약 → 미니게임 시간 → 점수 모델의 점수로 정산. 정산이 거절되면 예약을 포기해 남기지 않는다. 미니게임 없는 일은 바로 한다.
        /// 점수 모델의 차례는 정산을 시도할 때마다 넘어간다(거절돼도).
        /// </summary>
        public static StepOutcome Play(GameSession game, FacilityWork work, string request, RunContext context)
        {
            if (work.Minigame == null)
            {
                var direct = game.CompleteFacilityWork(work.Id, request, context.Score.Next(), autoSkip: false);
                return direct.Applied ? new StepOutcome { Done = true } : BalanceActions.Refused(direct);
            }

            var reserved = game.ReserveFacilityWork(work.Id, request, 1);
            if (!reserved.Applied) return BalanceActions.Refused(reserved);
            DayAssumptions.PlayMinigame(game, work.Minigame, context.MinigameShare);
            var settled = game.SettleFacilityWork(request, context.Score.Next());
            if (settled.Applied) return new StepOutcome { Done = true, MinigameRealSeconds = DayAssumptions.MinigameSeconds(work.Minigame, context.MinigameShare) };
            game.ForfeitFacilityWork(request);
            return BalanceActions.Refused(settled);
        }
    }
}
