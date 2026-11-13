namespace Rootborn.Balance.Tests
{
    /// <summary>
    /// 훑기의 한 줄: 판 하나(전략·대상·가정)의 한 시점까지의 요약. 판이 예외로 멈췄으면 Failure에 그 까닭이 있고 Summary는 빈 값이다.
    /// </summary>
    internal sealed class SweepRow
    {
        public string StrategyId, TargetId;     // TargetId는 대상이 없으면 ""
        public RunSetup Setup;
        public int Checkpoint;                  // 판 안에서 몇째 날까지(1부터)
        public RunSummary Summary;
        public bool IsBaseline;                 // 모든 축이 기준 값인 판
        public string Failure;                  // 판이 멈춘 까닭(예외 형과 문구). 끝까지 돌았으면 null
    }
}
