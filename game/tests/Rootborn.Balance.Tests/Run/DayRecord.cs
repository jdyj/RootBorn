using System.Collections.Generic;

namespace Rootborn.Balance.Tests
{
    /// <summary>판 한 번의 하루 기록. 판을 돌리는 쪽이 채우고 요약은 읽기만 한다.</summary>
    internal sealed class DayRecord
    {
        public int Day;
        public bool Weekend;
        public int WalletAtEnd, Earned, Spent;                                   // 통화
        public int MinigameEarned;                                               // Earned 중 미니게임을 한 행동이 번 것(자동 넘기기·올리기·잠은 빼고)
        public int Actions;
        public SortedDictionary<string, int> ActionCounts = new SortedDictionary<string, int>();  // 행동 id(일·알바 id, 올리기는 종류 이름) → 횟수
        public SortedDictionary<string, int> OtherItems = new SortedDictionary<string, int>();    // 통화가 아닌 아이템 id → 그날 끝에 가진 개수(그날 얻은 수가 아니다)
        public int WorkMinutes, TravelMinutes;                                   // 게임 분
        public float TravelRealSeconds, MinigameRealSeconds;                     // 걷기·미니게임에 쓴 실제 초
        public int EndedAtMinutes;                                               // 그날 마지막 행동이 끝난 시각
        public int FacilityLevel;
        public bool Upgraded;
        public int AutoSkips;
        public int EnergyAtEnd, FocusAtEnd;
        public string Stop;                                                      // StopReasons의 값
    }

    /// <summary>하루가 끝난 까닭.</summary>
    internal static class StopReasons
    {
        public const string Bedtime = "bedtime", BagFull = "bag-full", Body = "body", NothingToDo = "nothing-to-do";
    }

    /// <summary>판 한 번의 결과: 시작 조건과 날마다의 기록.</summary>
    internal sealed class RunResult
    {
        public string StrategyId, TargetId, TierId;      // TargetId는 대상이 없으면 "", TierId는 점수를 안 쓰면 ""
        public int StartDay, StartWallet;
        public bool StartWeekend;
        public List<DayRecord> Days = new List<DayRecord>();
    }
}
