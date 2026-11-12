using System.Globalization;

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

        /// <summary>판 이름에 붙이는 한 줄: 잠/점수/알바/걷기/몫.</summary>
        public string Id => Sleep + "/" + Score + "/" + PartTime + "/" + TravelMinutes.ToString(CultureInfo.InvariantCulture) + "/" + MinigameShare.ToString("0.###", CultureInfo.InvariantCulture);

        /// <summary>설정의 기준 가정(money-balance.json "baseline", 걷기·몫은 day-balance.json)의 새 사본. 고쳐 써도 설정은 그대로다.</summary>
        public static RunSetup Baseline(BalanceConfig config) => config.BaselineSetup.Copy();

        public RunSetup Copy() => (RunSetup)MemberwiseClone();
    }
}
