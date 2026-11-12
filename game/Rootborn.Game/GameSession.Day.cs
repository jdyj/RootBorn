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
        /// 흐르는 시간에서는 지금 시각에 자는 것이다(<see cref="Sleep"/>, GameSession.Time). 이미 끝낸 날이면 아무것도 바꾸지 않는다.
        /// </summary>
        public GameDayResult EndDay() => Sleep();

        /// <summary>마지막으로 끝낸 날의 결과(이 GameSession에서 끝낸 경우).</summary>
        public GameDayResult LastDayResult { get; private set; }

        /// <summary>다음 날을 시작할 수 있다: 오늘을 마감했고 방이 다음 날로 넘어갔다(혼자면 마감하는 순간 넘어간다).</summary>
        public bool CanStartNextDay => Student.DayState == StudentDayState.ResultReady && Room.Day > Student.CurrentDay;

        /// <summary>
        /// 다음 날을 시작한다. 방이 다음 날로 넘어가기 전(같이 하는 방에서 아직 깨어 있는 사람이 있고 07:00 전)에는 하지 않는다(<see cref="CanStartNextDay"/>).
        /// 순서(Unity StudentDayResultPanel.StartNextDay): 날 넘기기(학생 미니게임 예약 포기는 Core가 한다)
        /// → 앞선 날의 성인 일 예약 포기 → 날 시작 이벤트, 장래희망이 있으면 장래희망 이벤트 → 집에 들어와 있으면 이미 놓은 가구 다시 알리기
        /// (Unity PublishDayStartEvents) → 저장.
        /// </summary>
        public bool StartNextDay()
        {
            if (!CanStartNextDay || !Student.TryStartNextDay()) return false;
            // 방이 그보다 더 앞서 있으면(방에 없던 사이 며칠이 지났다) 사이의 날은 쉰 날로 넘겨 방의 날에서 시작한다.
            if (Room.Day > Student.CurrentDay) Student.RestUntil(Room.Day, _dayEndRules);
            BeginNewDay();
            SaveNewDay();
            return true;
        }

        // 새 날을 시작한 뒤 할 일(다음 날 시작과 쉰 날 넘기기가 같이 쓴다). 저장은 SaveNewDay.
        private void BeginNewDay()
        {

            _adultService.ForfeitWorkLeftFromEarlierDays(Student, Adult);
            Campaign?.SetCurrentDayNumber(Student.CurrentDay);
            Announce(NewEvent(QuestEventKind.DayStarted, "day-started", null), true);
            if (!string.IsNullOrEmpty(CareerInterests.CurrentInterestId))
                Announce(NewEvent(QuestEventKind.CareerInterestSelected, "career-interest-kept", CareerInterests.CurrentInterestId), true);
            ReplayPlacedFurniture();
        }

        private void SaveNewDay()
        {
            Save();
            KeepDayCopy();
            Notify(AnnounceTopics, GameChangeTopics.Day, GameChangeTopics.Career);
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
            return Shared(() =>
            {
                var result = _adultService.TryTransition(facility, Student, CareerCandidates, Adult, requestId);
                return Saved(result, result.Applied, CareerTopics);
            }, r => r.Applied);
        }

        // 시설 일 명령은 일터의 공간 이득(WorkExtrasFor)을 넘긴다. 자기 시설의 일이 아니거나 일터가 없으면 이득이 없다(default).
        // 예약은 그때의 보상 비율을 적어 두고, 정산은 그 비율로 보상을 내되 명성 이득·기술 성장은 정산 때의 이득을 쓴다.

        /// <summary>미니게임으로 할 일을 예약한다. 보상 비율은 지금 일터의 이득을 더해 정한다.</summary>
        public AdultCareerResult ReserveFacilityWork(string workId, string requestId, int seed)
        {
            _definitions.TryGet<FacilityWork>(workId, out var work);
            return Shared(() =>
            {
                var result = _adultService.TryReserveWork(work, Student, Adult, requestId, seed, WorkExtrasFor(workId));
                return Saved(result, result.Applied, CareerTopics);
            }, r => r.Applied);
        }

        /// <summary>예약한 일을 끝낸 판의 점수(0~100)로 정산한다. 명성 이득·기술 성장은 지금 일터의 이득(예약에 적힌 일 기준)을 쓴다.</summary>
        public AdultCareerResult SettleFacilityWork(string requestId, int score)
        {
            var pending = Adult.PendingWork;
            FacilityWork work = null;
            if (pending != null) _definitions.TryGet(pending.WorkId, out work);
            return Shared(() =>
            {
                var result = _adultService.TrySettleReservedWork(work, Student, Adult, CareerInventory, requestId, score, WorkExtrasFor(pending?.WorkId));
                return Saved(result, result.Applied, CareerTopics);
            }, r => r.Applied);
        }

        public AdultCareerResult ForfeitFacilityWork(string requestId)
        {
            var result = _adultService.TryForfeitReservedWork(Student, Adult, requestId);
            return Saved(result, result.Applied, CareerTopics);
        }

        /// <summary>미니게임 없이 바로 하는 일, 또는 자동 완료(autoSkip). 둘 다 지금 일터의 이득을 받는다.</summary>
        public AdultCareerResult CompleteFacilityWork(string workId, string requestId, int score, bool autoSkip)
        {
            _definitions.TryGet<FacilityWork>(workId, out var work);
            return Shared(() =>
            {
                var result = _adultService.TryCompleteWork(work, Student, Adult, CareerInventory, requestId, score, autoSkip, WorkExtrasFor(workId));
                return Saved(result, result.Applied, CareerTopics);
            }, r => r.Applied);
        }

        /// <summary>지금 시설을 한 단계 올린다. 비용은 인벤토리에서, 단계의 월드 깃발은 월드 상태에 켠다.</summary>
        public AdultCareerResult UpgradeFacility(string requestId)
        {
            _definitions.TryGet<Facility>(Adult.FacilityId, out var facility);
            return Shared(() =>
            {
                var result = _adultService.TryUpgrade(facility, Student, Adult, CareerInventory, WorldAccess, requestId);
                return Saved(result, result.Applied, CareerTopics, GameChangeTopics.World);
            }, r => r.Applied);
        }
    }
}
