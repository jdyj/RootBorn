using System.Collections.Generic;
using Rootborn.Core.Careers;
using Rootborn.Core.StudentLife;
using Rootborn.Game;
using Rootborn.Game.Tests;

namespace Rootborn.Balance.Tests
{
    /// <summary>판의 대상인 알바를 한다(성공인지는 그 판의 성공 모델).</summary>
    internal sealed class PartTimeWorkAction : IBalanceAction
    {
        public string Kind => "part-time-work";

        public PlannedStep Plan(GameSession game, RunContext context)
        {
            if (!(context.Target is IMinigamePartTimeWork work)) return null;
            return new PlannedStep { Id = work.Id, Minutes = work.TimeCostMinutes, Walks = true, Do = (g, request) => PartTimeWorks.Play(g, work, request, context) };
        }
    }

    /// <summary>
    /// 데이터의 알바를 돌아가며 한다(판 안에서 차례를 기억한다). 차례인 알바의 시작이 거절되면 다음 알바를 본다 —
    /// 걷기를 마친 지금 시작해도 잠 규칙에 맞는 것만.
    /// </summary>
    internal sealed class AnyPartTimeWorkAction : IBalanceAction
    {
        private IReadOnlyList<object> _works;
        private int _turn;

        public string Kind => "any-part-time-work";

        public PlannedStep Plan(GameSession game, RunContext context)
        {
            _works ??= RunTargets.All("part-time-work", game.Definitions);
            if (_works.Count == 0) return null;
            var planned = (IMinigamePartTimeWork)_works[_turn % _works.Count];
            return new PlannedStep { Id = planned.Id, Minutes = planned.TimeCostMinutes, Walks = true, Do = (g, request) => Rotate(g, context, request) };
        }

        private StepOutcome Rotate(GameSession game, RunContext context, string request)
        {
            bool bagFull = false;
            for (int k = 0; k < _works.Count; k++)
            {
                int index = (_turn + k) % _works.Count;
                var work = (IMinigamePartTimeWork)_works[index];
                if (k > 0 && !context.FitsNow(game, work.TimeCostMinutes)) continue;
                var outcome = PartTimeWorks.Play(game, work, request + ":" + work.Id, context);
                if (outcome.Done)
                {
                    _turn = index + 1;
                    return new StepOutcome { Done = true, MinigameRealSeconds = outcome.MinigameRealSeconds, Id = work.Id, Minutes = work.TimeCostMinutes };
                }
                bagFull |= outcome.BagFull;
            }
            return new StepOutcome { Done = false, BagFull = bagFull };
        }
    }

    internal static class PartTimeWorks
    {
        /// <summary>
        /// 시작 → 미니게임 시간 → 성공 모델이 낸 결과로 정산(시작이 된 것마다 한 차례). 성공 정산이 거절되면 실패로 끝내 예약을 남기지 않는다.
        /// 실패로 정산된 알바도 한 행동이다(시간을 썼다).
        /// </summary>
        public static StepOutcome Play(GameSession game, IMinigamePartTimeWork work, string request, RunContext context)
        {
            var begun = game.BeginPartTimeWork(work.Id, request, 1);
            if (!begun.Applied) return BalanceActions.Refused(begun);
            DayAssumptions.PlayMinigame(game, work.Minigame, context.MinigameShare);
            bool succeeded = context.Success.Next();
            var settled = game.SettleMinigame(request, succeeded);
            if (settled.Applied) return new StepOutcome { Done = true, MinigameRealSeconds = DayAssumptions.MinigameSeconds(work.Minigame, context.MinigameShare) };
            if (succeeded) game.SettleMinigame(request, succeeded: false);
            return BalanceActions.Refused(settled);
        }
    }
}
