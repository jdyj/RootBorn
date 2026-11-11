using System;
using Rootborn.Core.Careers;
using Rootborn.Core.Save;
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
        // 성인 직업 명령은 직업 진행·생활 수치·가방(급여·비용)을 바꾼다.
        private static readonly string[] CareerTopics = { GameChangeTopics.Career, GameChangeTopics.Student, GameChangeTopics.Inventory };

        /// <summary>
        /// 하루를 끝낸다(Unity StudentDayEndInteractor 순서): 하루 끝 규칙 → 방의 시계에 마감 알림 → 마일스톤 하루 결과("day:N") → 캠페인 하루 결과("campaign-result-day-N") → 저장.
        /// 이미 끝낸 날이면 아무것도 바꾸지 않는다.
        /// </summary>
        public GameDayResult EndDay()
        {
            int day = Student.CurrentDay;
            if (!Student.TryEndDay(_dayEndRules, out var summary)) return new GameDayResult(false, summary, default, default);
            Room.EndDay(PlayerId); // 혼자인 방은 여기서 다음 날로 넘어간다. 같이 하는 방은 모두 마감해야 넘어간다.
            var milestones = Milestones.ApplyDayResult(_milestones, Student, Inventory, "day:" + day);
            var campaign = ApplyCampaign("campaign-result-day-" + day);
            LastDayResult = new GameDayResult(true, summary, milestones, campaign);
            SaveAndNotify(RewardTopics, GameChangeTopics.Day);
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
            KeepDayCopy();
            Notify(AnnounceTopics, GameChangeTopics.Day, GameChangeTopics.Career);
            return true;
        }

        // ---------- 날마다 슬롯 사본 ----------

        /// <summary>날마다 남기는 슬롯 사본 개수의 기본값.</summary>
        public const int DefaultDayCopiesToKeep = 3;

        /// <summary>다음 날을 시작할 때 남기는 슬롯 사본 개수(최근 것부터). 0이면 남기지 않는다.</summary>
        public int DayCopiesToKeep { get; private set; } = DefaultDayCopiesToKeep;

        /// <summary>마지막 사본 남기기의 오류(디스크가 찼거나 폴더를 쓸 수 없음). 사본이 실패해도 그 날의 저장은 이미 끝나 있어 명령은 적용된다. 성공하면 null.</summary>
        public Exception DayCopyError { get; private set; }

        /// <summary>
        /// 슬롯 사본 개수를 정한다(실행 인자·설정에서 Host가 넘긴다). 줄이면 다음 날 사본을 남길 때 오래된 것을 지운다.
        /// 저장소가 사본을 지원하지 않으면(ISaveSlotDayCopies가 아님) 아무것도 남기지 않는다.
        /// </summary>
        public void KeepDayCopies(int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), count, "사본 개수는 0 이상이다");
            DayCopiesToKeep = count;
        }

        private void KeepDayCopy()
        {
            DayCopyError = null;
            if (DayCopiesToKeep <= 0 || !(_store is ISaveSlotDayCopies copies)) return;
            try
            {
                copies.KeepDayCopy(SaveSlot, Student.CurrentDay, DayCopiesToKeep);
            }
            catch (System.IO.IOException error)
            {
                DayCopyError = error;
            }
            catch (UnauthorizedAccessException error)
            {
                DayCopyError = error;
            }
        }

        // ---------- 성인 직업 ----------

        public AdultCareerResult ChooseFacility(string facilityId, string requestId)
        {
            _definitions.TryGet<Facility>(facilityId, out var facility);
            var result = _adultService.TryTransition(facility, Student, CareerCandidates, Adult, requestId);
            return Saved(result, result.Applied, CareerTopics);
        }

        public AdultCareerResult ReserveFacilityWork(string workId, string requestId, int seed)
        {
            _definitions.TryGet<FacilityWork>(workId, out var work);
            var result = _adultService.TryReserveWork(work, Student, Adult, requestId, seed);
            return Saved(result, result.Applied, CareerTopics);
        }

        /// <summary>예약한 일을 끝낸 판의 점수(0~100)로 정산한다.</summary>
        public AdultCareerResult SettleFacilityWork(string requestId, int score)
        {
            var pending = Adult.PendingWork;
            FacilityWork work = null;
            if (pending != null) _definitions.TryGet(pending.WorkId, out work);
            var result = _adultService.TrySettleReservedWork(work, Student, Adult, CareerInventory, requestId, score);
            return Saved(result, result.Applied, CareerTopics);
        }

        public AdultCareerResult ForfeitFacilityWork(string requestId)
        {
            var result = _adultService.TryForfeitReservedWork(Student, Adult, requestId);
            return Saved(result, result.Applied, CareerTopics);
        }

        /// <summary>미니게임 없이 바로 하는 일, 또는 자동 완료(autoSkip).</summary>
        public AdultCareerResult CompleteFacilityWork(string workId, string requestId, int score, bool autoSkip)
        {
            _definitions.TryGet<FacilityWork>(workId, out var work);
            var result = _adultService.TryCompleteWork(work, Student, Adult, CareerInventory, requestId, score, autoSkip);
            return Saved(result, result.Applied, CareerTopics);
        }

        /// <summary>지금 시설을 한 단계 올린다. 비용은 인벤토리에서, 단계의 월드 깃발은 월드 상태에 켠다.</summary>
        public AdultCareerResult UpgradeFacility(string requestId)
        {
            _definitions.TryGet<Facility>(Adult.FacilityId, out var facility);
            var result = _adultService.TryUpgrade(facility, Student, Adult, CareerInventory, WorldAccess, requestId);
            return Saved(result, result.Applied, CareerTopics, GameChangeTopics.World);
        }
    }
}
