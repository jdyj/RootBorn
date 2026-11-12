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
        public int StartedAtMinutes;                                             // 그날 첫 행동 전 시각(그날 0시부터 센 분)
        public int EndedAtMinutes;                                               // 그날 마지막 행동이 끝난 시각
        public int FacilityLevel;
        public bool Upgraded;
        public int AutoSkips;
        public int RefusedAfterDayEnd;                                           // 넘겨 잇는 밤에 행동 도중 날이 넘어가 정산이 거절된 행동 수
        public int EnergyAtEnd, FocusAtEnd;                                      // 밤(잠·날 넘김) 전의 값
        public bool Slept;                                                       // 그 밤에 잤는가
        public int SleptAtMinutes = -1;                                          // 잔 시각(그날 0시부터 센 분). 안 잤으면 -1
        public bool Exhausted;                                                   // 그날 지침(game.IsExhausted)을 한 번이라도 봤는가. 밤을 기다리는 동안 지치면 다음 날에 센다
        // 자지 않는 밤: Earned·Spent에는 다음 날을 시작할 때의 지갑 변화도 들어간다(날 넘김이 한 부름 안에서 다음 날을 연다).
        public string Stop;                                                      // StopReasons의 값
    }

    /// <summary>하루가 끝난 까닭.</summary>
    internal static class StopReasons
    {
        public const string Bedtime = "bedtime", BagFull = "bag-full", Body = "body", NothingToDo = "nothing-to-do";

        /// <summary>지쳐서 아무것도 시작하지 못해 그 자리에서 잤다(자는 밤).</summary>
        public const string Exhausted = "exhausted";

        /// <summary>자지 않는 밤: 날이 넘어가기 전에 끝낼 것을 다 했거나 지쳐서 날이 넘어가기를 기다렸다.</summary>
        public const string DayRanOut = "day-ran-out";
    }

    /// <summary>판 한 번의 결과: 시작 조건, 그 판의 가정, 날마다의 기록.</summary>
    internal sealed class RunResult
    {
        public string StrategyId, TargetId;              // TargetId는 대상이 없으면 ""
        public RunSetup Setup;                           // 손으로 만든 판(보고 시험)은 null일 수 있다
        public int StartDay, StartWallet;
        public bool StartWeekend;
        public List<DayRecord> Days = new List<DayRecord>();
    }
}
