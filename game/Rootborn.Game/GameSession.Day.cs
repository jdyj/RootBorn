using Rootborn.Core.Careers;
using Rootborn.Core.Quests;
using Rootborn.Core.StudentLife;

namespace Rootborn.Game
{
    /// <summary>하루 끝 결과. 하루 결과 화면이 그린다.</summary>
    public readonly struct GameDayResult
    {
        public GameDayResult(bool ended, StudentDaySummary summary, MilestoneApplyResult milestones, CampaignApplyResult campaign)
        {
            Ended = ended;
            Summary = summary;
            Milestones = milestones;
            Campaign = campaign;
        }

        public bool Ended { get; }
        public StudentDaySummary Summary { get; }
        public MilestoneApplyResult Milestones { get; }
        public CampaignApplyResult Campaign { get; }
    }

    /// <summary>하루 끝과 다음 날, 성인 직업 명령.</summary>
    public sealed partial class GameSession
    {
        /// <summary>
        /// 하루를 끝낸다(Unity StudentDayEndInteractor 순서): 하루 끝 규칙 → 마일스톤 하루 결과("day:N") → 캠페인 하루 결과("campaign-result-day-N") → 저장.
        /// 이미 끝낸 날이면 아무것도 바꾸지 않는다.
        /// </summary>
        public GameDayResult EndDay()
        {
            int day = Student.CurrentDay;
            if (!Student.TryEndDay(_dayEndRules, out var summary)) return new GameDayResult(false, summary, default, default);
            var milestones = Milestones.ApplyDayResult(_milestones, Student, Inventory, "day:" + day);
            var campaign = ApplyCampaign("campaign-result-day-" + day);
            LastDayResult = new GameDayResult(true, summary, milestones, campaign);
            Save();
            return LastDayResult;
        }

        /// <summary>마지막으로 끝낸 날의 결과(이 GameSession에서 끝낸 경우).</summary>
        public GameDayResult LastDayResult { get; private set; }

        /// <summary>
        /// 다음 날을 시작한다(Unity StudentDayResultPanel.StartNextDay 순서): 날 넘기기(학생 미니게임 예약 포기는 Core가 한다)
        /// → 앞선 날의 성인 일 예약 포기 → 날 시작 이벤트, 장래희망이 있으면 장래희망 이벤트 → 집에 들어와 있으면 이미 놓은 가구 다시 알리기
        /// (Unity PublishDayStartEvents) → 저장.
        /// </summary>
        public bool StartNextDay()
        {
            if (!Student.TryStartNextDay()) return false;
            _adultService.ForfeitWorkLeftFromEarlierDays(Student, Adult);
            Campaign?.SetCurrentDayNumber(Student.CurrentDay);
            Announce(NewEvent(QuestEventKind.DayStarted, "day-started", null), true);
            if (!string.IsNullOrEmpty(CareerInterests.CurrentInterestId))
                Announce(NewEvent(QuestEventKind.CareerInterestSelected, "career-interest-kept", CareerInterests.CurrentInterestId), true);
            ReplayPlacedFurniture();
            Save();
            return true;
        }

        // ---------- 성인 직업 ----------

        public AdultCareerResult ChooseFacility(string facilityId, string requestId)
        {
            _definitions.TryGet<Facility>(facilityId, out var facility);
            var result = _adultService.TryTransition(facility, Student, CareerCandidates, Adult, requestId);
            return Saved(result, result.Applied);
        }

        public AdultCareerResult ReserveFacilityWork(string workId, string requestId, int seed)
        {
            _definitions.TryGet<FacilityWork>(workId, out var work);
            var result = _adultService.TryReserveWork(work, Student, Adult, requestId, seed);
            return Saved(result, result.Applied);
        }

        /// <summary>예약한 일을 끝낸 판의 점수(0~100)로 정산한다.</summary>
        public AdultCareerResult SettleFacilityWork(string requestId, int score)
        {
            var pending = Adult.PendingWork;
            FacilityWork work = null;
            if (pending != null) _definitions.TryGet(pending.WorkId, out work);
            var result = _adultService.TrySettleReservedWork(work, Student, Adult, CareerInventory, requestId, score);
            return Saved(result, result.Applied);
        }

        public AdultCareerResult ForfeitFacilityWork(string requestId)
        {
            var result = _adultService.TryForfeitReservedWork(Student, Adult, requestId);
            return Saved(result, result.Applied);
        }

        /// <summary>미니게임 없이 바로 하는 일, 또는 자동 완료(autoSkip).</summary>
        public AdultCareerResult CompleteFacilityWork(string workId, string requestId, int score, bool autoSkip)
        {
            _definitions.TryGet<FacilityWork>(workId, out var work);
            var result = _adultService.TryCompleteWork(work, Student, Adult, CareerInventory, requestId, score, autoSkip);
            return Saved(result, result.Applied);
        }

        /// <summary>지금 시설을 한 단계 올린다. 비용은 인벤토리에서, 단계의 월드 깃발은 월드 상태에 켠다.</summary>
        public AdultCareerResult UpgradeFacility(string requestId)
        {
            _definitions.TryGet<Facility>(Adult.FacilityId, out var facility);
            var result = _adultService.TryUpgrade(facility, Student, Adult, CareerInventory, WorldAccess, requestId);
            return Saved(result, result.Applied);
        }
    }
}
