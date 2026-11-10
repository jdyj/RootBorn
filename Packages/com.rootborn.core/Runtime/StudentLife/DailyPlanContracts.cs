using Rootborn.Core.Minigames;

namespace Rootborn.Core.StudentLife
{
    // 일과와 미니게임이 아직 Core에 없는 다른 영역(장소 활동, 알바, 세계 상태)에서 받는 최소한의 모양.
    // 그 영역을 옮기면 그쪽 정의가 이 인터페이스를 구현해 잇는다.

    /// <summary>장소 활동. 성장 길(GrowthRoute)은 원본 열거형 이름 그대로("FormalStudy", "SelfStudy" 등)를 쓴다.</summary>
    public interface IDailyPlanLocationActivity
    {
        string Id { get; }
        string LocationId { get; }
        string GrowthRoute { get; }
    }

    /// <summary>미니게임으로 하는 알바. 일과 칸 맞추기와 미니게임 예약에 필요한 값만 가진다.</summary>
    public interface IMinigamePartTimeWork
    {
        string Id { get; }
        Minigame Minigame { get; }
        ActivityTimePolicy TimePolicy { get; }
        int TimeCostMinutes { get; }
        int EnergyCost { get; }
        int FocusCost { get; }
        int StressDelta { get; }
        bool HasSatisfiedRequirements(StudentLifeProgress progress);
    }

    /// <summary>하루 결과 기록에서 알바를 한 기록만 골라 그 알바 id들을 준다(원본 PartTimeWorkDayLog.FromResultLogs(...).WorkIds).</summary>
    public interface IPartTimeWorkLogReader
    {
        string[] ReadWorkIds(string[] resultLogIds);
    }

    /// <summary>세계 상태 깃발 조회.</summary>
    public interface IDailyPlanWorldState
    {
        bool IsActive(string flagId);
    }

    /// <summary>일과 길을 고를 수 있는지 판정할 때 보는 값. 세계 상태가 없으면 null이다.</summary>
    public readonly struct DailyPlanRouteContext
    {
        public DailyPlanRouteContext(StudentLifeProgress studentProgress, IDailyPlanWorldState worldState = null)
        {
            StudentProgress = studentProgress;
            WorldState = worldState;
        }

        public StudentLifeProgress StudentProgress { get; }
        public IDailyPlanWorldState WorldState { get; }
    }

    public interface IDailyPlanRouteRequirement
    {
        bool IsSatisfied(DailyPlanRouteContext context, DailyPlanRoute route);
    }

    /// <summary>계획한 칸을 채웠을 때 주는 결과. 기록 한 줄을 돌려준다(비면 기록하지 않는다).</summary>
    public interface IDailyPlanRouteOutcome
    {
        string Apply(StudentLifeProgress progress, DailyPlanRoute route);
    }
}
