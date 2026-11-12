using System;
using Rootborn.Core.Minigames;
using Rootborn.Game;
using Rootborn.Game.Tests;

namespace Rootborn.Balance.Tests
{
    /// <summary>판 하나가 행동에 넘기는 것: 대상, 그 판의 점수·알바 성공 모델(판마다 새 것), 미니게임 몫, 지금 시작해도 잠 규칙에 맞는지.</summary>
    internal sealed class RunContext
    {
        public object Target;
        // 모델의 차례는 정산을 시도할 때마다 넘어간다(정산이 거절돼도 — 그 점수·결과는 쓰이지 않고 다음 차례로 간다).
        public IScoreModel Score;        // 시설 일을 정산할 때마다 Next()
        public ISuccessModel Success;    // 알바를 정산할 때마다 Next()
        public float MinigameShare;

        /// <summary>걷기가 끝난 지금 그 길이(게임 분)의 행동을 시작해도 되는지(그 판의 잠 규칙). 행동이 계획과 다른 것으로 바꿔 할 때 쓴다.</summary>
        public Func<GameSession, int, bool> FitsNow;
    }

    /// <summary>
    /// 미니게임으로 하는 행동의 길이(게임 분): max(정의의 분, 미니게임을 하는 실제 초를 게임 분으로 올림한 값).
    /// Core는 예약한 분과 미니게임 동안 실제로 흐른 분 중 큰 것으로 정산한다 — 잠 규칙 판정과 기록에 같은 값을 쓴다.
    /// 자동 넘기기·올리기·미니게임 없는 일은 정의의 분 그대로다.
    /// </summary>
    internal static class ActionLength
    {
        public static int WithMinigame(GameSession game, int definitionMinutes, Minigame minigame, float share)
            => minigame == null ? definitionMinutes : Math.Max(definitionMinutes, DayAssumptions.GameMinutesUp(game.TimeRules, DayAssumptions.MinigameSeconds(minigame, share)));
    }

    /// <summary>행동 하나의 계획. 판을 돌리는 쪽이 잠 규칙 판정 → 걷기 → (걷기 뒤 다시 판정) → <see cref="Do"/> 순으로 쓴다.</summary>
    internal sealed class PlannedStep
    {
        public string Id;                                  // 기록에 남길 이름(일·알바 id, 올리기는 종류 이름)
        public int Minutes;                                // 게임 분(미니게임이 더 길면 그 길이, ActionLength). 취침 판정에 쓴다
        public bool Walks;                                 // 시작 전에 걷는 시간을 보내는가(올리기는 false)
        public Func<GameSession, string, StepOutcome> Do;  // (game, 요청 id)
    }

    /// <summary>
    /// 행동을 한 결과. Done이 아니면 아무것도 얻지 않았다(시작했던 예약은 포기했다). BagFull은 가방이 차서 거절된 것.
    /// Id·Minutes는 계획과 다른 것을 했을 때만 채운다(돌아가며 하는 알바가 다음 알바로 넘어간 경우). 비면 계획의 것이다.
    /// </summary>
    internal readonly struct StepOutcome
    {
        public bool Done { get; init; }
        public bool BagFull { get; init; }
        public bool AutoSkipped { get; init; }
        public bool Upgraded { get; init; }
        public float MinigameRealSeconds { get; init; }
        public string Id { get; init; }
        public int? Minutes { get; init; }
    }

    /// <summary>행동 종류 하나. 새 종류는 클래스 하나와 <see cref="BalanceActions"/> 등록 한 줄로 더한다.</summary>
    internal interface IBalanceAction
    {
        string Kind { get; }

        /// <summary>지금 할 것. 없으면 null. 아무것도 바꾸지 않는다(조회만 한다).</summary>
        PlannedStep Plan(GameSession game, RunContext context);
    }
}
