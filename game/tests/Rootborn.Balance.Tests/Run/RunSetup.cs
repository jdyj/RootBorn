using System.Globalization;
using Rootborn.Game;

namespace Rootborn.Balance.Tests
{
    /// <summary>
    /// 한 판의 가정 전부(설계 13.1절의 축 값 하나씩): 잠 규칙, 시설 일 점수 모델, 알바 성공 모델(설정 글자 그대로), 활동마다 걷는 분, 미니게임을 하는 몫.
    /// </summary>
    internal sealed class RunSetup
    {
        public string Sleep, Score, PartTime;
        public int TravelMinutes;
        public float MinigameShare;

        /// <summary>
        /// 속도 기록(먼 층, 없으면 null): 판을 돌리는 쪽이 걷기·행동·밤마다 알린다. 판 이름(Id)과 사본에는 들지 않는다.
        /// </summary>
        public IRunObserver Observer;

        /// <summary>상호작용 종류마다 걷는 게임 분(먼 층이 가까운 층에서 잰 값으로 채운다). 없는 종류와 null이면 <see cref="TravelMinutes"/>.</summary>
        public System.Collections.Generic.IReadOnlyDictionary<string, int> TravelByKind;

        /// <summary>그 행동 앞에 걷는 게임 분.</summary>
        public int TravelFor(PlannedStep step)
            => TravelByKind != null && step.InteractionKind != null && TravelByKind.TryGetValue(step.InteractionKind, out int minutes) ? minutes : TravelMinutes;

        /// <summary>판 이름에 붙이는 한 줄: 잠/점수/알바/걷기/몫.</summary>
        public string Id => Sleep + "/" + Score + "/" + PartTime + "/" + TravelMinutes.ToString(CultureInfo.InvariantCulture) + "/" + MinigameShare.ToString("0.###", CultureInfo.InvariantCulture);

        /// <summary>설정의 기준 가정(money-balance.json "baseline", 걷기·몫은 day-balance.json)의 새 사본. 고쳐 써도 설정은 그대로다.</summary>
        public static RunSetup Baseline(BalanceConfig config) => config.BaselineSetup.Copy();

        public RunSetup Copy() => (RunSetup)MemberwiseClone();
    }

    /// <summary>판 하나를 지켜보는 쪽(먼 층의 속도 기록). 모든 알림은 GameSession 명령을 부른 뒤의 상태로 온다. 상태를 바꾸지 않는다.</summary>
    internal interface IRunObserver
    {
        /// <summary>판이 시작됐다(첫 날 첫 행동 전).</summary>
        void Started(GameSession game);
        /// <summary>그 행동 앞에 걸었다(게임 분).</summary>
        void Walked(GameSession game, PlannedStep step, int travelMinutes);
        /// <summary>행동을 했다(Done이 아니면 거절 — 아무것도 얻지 않았다). day·minutes는 행동 앞의 날·시각.</summary>
        void Acted(GameSession game, PlannedStep step, StepOutcome outcome, int day, int minutes);
        /// <summary>밤: 잤거나(slept) 자지 않고 날을 넘겼다. day·minutes는 밤 앞의 날·시각.</summary>
        void Night(GameSession game, bool slept, int day, int minutes);
    }
}
