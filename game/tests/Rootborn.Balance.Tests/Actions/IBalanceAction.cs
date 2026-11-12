using System;
using Rootborn.Game;

namespace Rootborn.Balance.Tests
{
    /// <summary>판 하나가 행동에 넘기는 것: 대상, 그 판의 점수·알바 성공 모델(판마다 새 것), 미니게임 몫, 지금 시작해도 잠 규칙에 맞는지.</summary>
    internal sealed class RunContext
    {
        public object Target;
        public IScoreModel Score;        // 시설 일을 정산할 때마다 Next()
        public ISuccessModel Success;    // 알바를 정산할 때마다 Next()
        public float MinigameShare;

        /// <summary>걷기가 끝난 지금 그 길이(게임 분)의 행동을 시작해도 되는지(그 판의 잠 규칙). 행동이 계획과 다른 것으로 바꿔 할 때 쓴다.</summary>
        public Func<GameSession, int, bool> FitsNow;
    }

    /// <summary>행동 하나의 계획. 판을 돌리는 쪽이 잠 규칙 판정 → 걷기 → <see cref="Do"/> 순으로 쓴다.</summary>
    internal sealed class PlannedStep
    {
        public string Id;                                  // 기록에 남길 이름(일·알바 id, 올리기는 종류 이름)
        public int Minutes;                                // 게임 분. 취침 판정에 쓴다
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
