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
            var work = FacilityWorks.Highest(game, w => w.CanWork)
                ?? FacilityWorks.Highest(game, w => w.Direct == AdultCareerResultKind.InsufficientCapacity);
            if (work == null) return null;
            return new PlannedStep { Id = work.Id, Minutes = work.TimeCostMinutes, Walks = true, Do = (g, request) => FacilityWorks.Play(g, work, request, context.Score) };
        }
    }

    /// <summary>자동 넘기기가 되는 일(CanAutoSkip) 중 최소 레벨이 가장 높은 것을 넘긴다. 미니게임 실제 시간은 0이다.</summary>
    internal sealed class AutoSkipFacilityWorkAction : IBalanceAction
    {
        public string Kind => "auto-skip-facility-work";

        public PlannedStep Plan(GameSession game, RunContext context)
        {
            var work = FacilityWorks.Highest(game, w => w.CanAutoSkip);
            if (work == null) return null;
            return new PlannedStep
            {
                Id = work.Id,
                Minutes = work.TimeCostMinutes,
                Walks = true,
                Do = (g, request) =>
                {
                    var result = g.CompleteFacilityWork(work.Id, request, context.Score, autoSkip: true);
                    return result.Applied ? new StepOutcome { Done = true, AutoSkipped = true } : BalanceActions.Refused(result);
                },
            };
        }
    }

    internal static class FacilityWorks
    {
        /// <summary>지금 시설의 일 중 조건에 맞는 것에서 최소 레벨이 가장 높은 것(같으면 id 순 첫째). 없으면 null.</summary>
        public static FacilityWork Highest(GameSession game, Func<FacilityWorkAvailability, bool> can)
        {
            if (!game.Adult.IsAdult) return null;
            var status = game.FacilityStatus(game.Adult.FacilityId);
            if (status == null) return null;
            FacilityWork best = null;
            foreach (var w in status.Works)
            {
                if (!can(w)) continue;
                var work = w.Work;
                if (best == null || work.MinimumFacilityLevel > best.MinimumFacilityLevel
                    || (work.MinimumFacilityLevel == best.MinimumFacilityLevel && string.CompareOrdinal(work.Id, best.Id) < 0))
                    best = work;
            }
            return best;
        }

        /// <summary>예약 → 미니게임 시간 → 그 점수로 정산. 정산이 거절되면 예약을 포기해 남기지 않는다. 미니게임 없는 일은 바로 한다.</summary>
        public static StepOutcome Play(GameSession game, FacilityWork work, string request, int score)
        {
            if (work.Minigame == null)
            {
                var direct = game.CompleteFacilityWork(work.Id, request, score, autoSkip: false);
                return direct.Applied ? new StepOutcome { Done = true } : BalanceActions.Refused(direct);
            }

            var reserved = game.ReserveFacilityWork(work.Id, request, 1);
            if (!reserved.Applied) return BalanceActions.Refused(reserved);
            DayAssumptions.PlayMinigame(game, work.Minigame);
            var settled = game.SettleFacilityWork(request, score);
            if (settled.Applied) return new StepOutcome { Done = true, MinigameRealSeconds = DayAssumptions.MinigameSeconds(work.Minigame) };
            game.ForfeitFacilityWork(request);
            return BalanceActions.Refused(settled);
        }
    }
}
