using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Rootborn.Core.Quests;
using Rootborn.Core.StudentLife;
using Rootborn.Game;

namespace Rootborn.Screens.Day
{
    /// <summary>
    /// 하루 동안의 퀘스트·물건 변화를 재는 기준점(Unity StudentDayQuestInventoryTracker). 게임 한 판마다 하나이고,
    /// 게임을 시작할 때(Unity 판은 마을 씬을 불러올 때)와 다음 날을 시작할 때 지금 상태를 찍어 둔다. 세이브에 넣지 않는다(Unity 판과 같음).
    /// 기준점을 찍은 적이 없으면 처음 물을 때 찍는다(그날 변화는 비게 된다).
    /// </summary>
    public sealed class DayBaseline
    {
        private static readonly ConditionalWeakTable<GameSession, DayBaseline> Baselines = new ConditionalWeakTable<GameSession, DayBaseline>();

        private readonly GameSession _session;
        private StudentDayQuestInventorySnapshot _snapshot;

        private DayBaseline(GameSession session) { _session = session; }

        /// <summary>그 게임 한 판의 기준점. 처음 부르면 지금 상태를 찍는다.</summary>
        public static DayBaseline For(GameSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            DayBaseline baseline = Baselines.GetValue(session, s => new DayBaseline(s));
            if (baseline._snapshot == null) baseline.Capture();
            return baseline;
        }

        public void Capture() => _snapshot = Now();

        public StudentDayQuestInventorySummary Summary() => StudentDayQuestInventorySummaryBuilder.Build(_snapshot, Now());

        private StudentDayQuestInventorySnapshot Now() =>
            StudentDayQuestInventorySnapshot.Capture(_session.QuestLog, _session.Definitions.All<QuestDefinition>(), _session.Inventory);
    }

    /// <summary>하루 결과의 일과 칸 하나(Unity DailyPlanDaySummary).</summary>
    public readonly struct DayPlanSummary
    {
        public DayPlanSummary(string routeNameKey, string activityNameKey, bool completed, string[] resultLogs, string nextHintKey)
        {
            RouteNameKey = routeNameKey ?? string.Empty;
            ActivityNameKey = activityNameKey ?? string.Empty;
            Completed = completed;
            ResultLogs = resultLogs ?? Array.Empty<string>();
            NextHintKey = nextHintKey ?? string.Empty;
        }

        public string RouteNameKey { get; }
        public string ActivityNameKey { get; }
        public bool Completed { get; }
        public string[] ResultLogs { get; }
        public string NextHintKey { get; }
    }

    /// <summary>추적하던 체인 요약(Unity QuestChainRuntimeInstaller.BuildDayEndSummary): 받은 첫 체인, 그 단계 첫 목표의 진행.</summary>
    public readonly struct DayChainSummary
    {
        public DayChainSummary(QuestChainDefinition chain, QuestChainState state, string objectiveKey, int progress, int required, bool hasObjective)
        {
            Chain = chain; State = state; ObjectiveKey = objectiveKey ?? string.Empty; Progress = progress; Required = required; HasObjective = hasObjective;
        }

        public QuestChainDefinition Chain { get; }
        public QuestChainState State { get; }
        public string ObjectiveKey { get; }
        public int Progress { get; }
        public int Required { get; }
        public bool HasObjective { get; }
    }

    /// <summary>
    /// 하루 결과 화면의 표시 모델(Unity StudentDayEndInteractor가 모아 StudentDayResultPanel에 넘기던 것). 값은 Core의 요약기로 만든다:
    /// 하루 요약, 퀘스트·물건 변화, 관계·상태 변화와 내일 영향, 마일스톤, 일과 칸, 장소 상태 기록, 캠페인, 체인, 진로 후보, 장래희망.
    /// 월드 상태 카드(Unity ShowWorldStateChanges)는 아직 옮기지 않았다(월드 상태 기록 묶음).
    /// </summary>
    public sealed class DayResultModel
    {
        private DayResultModel() { }

        public StudentDaySummary Summary { get; private set; }
        public StudentDayQuestInventorySummary QuestInventory { get; private set; }
        public StudentDayRelationshipConditionSummary RelationshipCondition { get; private set; }
        public MilestoneDaySummary Milestones { get; private set; }
        public IReadOnlyList<DayPlanSummary> DailyPlan { get; private set; }
        public LocationStateTodaySummarySaveData[] LocationStates { get; private set; }
        public CampaignDayResultSummary Campaign { get; private set; }
        public bool HasChain { get; private set; }
        public DayChainSummary Chain { get; private set; }
        public string[] CareerCandidateLines { get; private set; }
        public string CareerInterestId { get; private set; }
        public string[] CareerInterestRecommendations { get; private set; }
        /// <summary>다음 날 버튼을 누를 수 있다(하루 결과가 준비됐고 방이 다음 날로 넘어감 — GameSession.CanStartNextDay).</summary>
        public bool CanStartNextDay { get; private set; }

        /// <summary>하루 결과는 준비됐지만 같이 하는 방의 다른 사람이 아직 마감하지 않았다.</summary>
        public bool WaitingForOthers { get; private set; }

        /// <summary>
        /// result는 방금 끝낸 날의 결과(GameSession.EndDay). 이미 끝난 날을 다시 보는 경우(Ended=false)는 Unity 판처럼
        /// 앞 날의 기록으로 요약을 만들고 마일스톤·캠페인 변화는 비운다.
        /// </summary>
        public static DayResultModel From(GameSession session, GameDayResult result, StudentDayQuestInventorySummary questInventory)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            StudentLifeProgress student = session.Student;
            StudentDaySummary summary = result.Ended ? result.Summary
                : new StudentDaySummary(student.CurrentDay, student.GetPreviousDayActivityIds(), student.GetPreviousDayResultLogIds(),
                    student.NextDayEntryPointId, student.TutorialStageId, student.NextObjectiveId, student.NextGuideText);

            var statuses = new List<StatusDefinition>();
            foreach (string id in session.Definitions.RegistryIds("studentConditionStatuses"))
                if (session.Definitions.TryGet<StatusDefinition>(id, out StatusDefinition status)) statuses.Add(status);

            MilestoneApplyResult milestones = result.Ended ? result.Milestones : new MilestoneApplyResult(Array.Empty<MilestoneProgressChange>(), Array.Empty<string>());
            if (milestones.ProgressChanges == null) milestones = new MilestoneApplyResult(Array.Empty<MilestoneProgressChange>(), milestones.RecommendedActionIds ?? Array.Empty<string>());

            CampaignDayResultSummary campaign = default;
            if (session.Campaign != null && session.Definitions.TryGet<Campaign>(session.Campaign.ActiveCampaignId, out Campaign campaignDefinition))
                campaign = CampaignSummaryBuilder.BuildDayResultSummary(campaignDefinition, session.Campaign,
                    result.Ended ? result.Campaign : new CampaignApplyResult(Array.Empty<string>()));

            var model = new DayResultModel
            {
                Summary = summary,
                QuestInventory = questInventory,
                RelationshipCondition = StudentDayRelationshipConditionSummaryBuilder.BuildFromResultLogs(student, summary.ResultLogIds,
                    session.Definitions.All<RelationshipDefinition>(), statuses),
                Milestones = MilestoneDaySummary.FromApplyResult(milestones, session.Definitions.All<Milestone>()),
                DailyPlan = PlanSummaries(session),
                LocationStates = session.LocationStates?.TodaySummary ?? Array.Empty<LocationStateTodaySummarySaveData>(),
                Campaign = campaign,
                CareerCandidateLines = session.CareerCandidates?.TodayHintResultLines ?? Array.Empty<string>(),
                CareerInterestId = session.CareerInterests?.CurrentInterestId ?? string.Empty,
                CareerInterestRecommendations = Array.Empty<string>(),
                CanStartNextDay = session.CanStartNextDay,
                WaitingForOthers = student.DayState == StudentDayState.ResultReady && !session.CanStartNextDay,
            };

            if (model.CareerInterestId.Length > 0 && session.Definitions.TryGet<CareerInterest>(model.CareerInterestId, out CareerInterest interest))
                model.CareerInterestRecommendations = interest.BuildRecommendations(session.CareerInterests, null, null, session.CareerInterests.LastChangedDay) ?? Array.Empty<string>();

            foreach (QuestChainDefinition chain in session.Definitions.All<QuestChainDefinition>())
            {
                if (chain == null) continue;
                QuestChainState state = session.QuestChains.GetState(chain);
                if (state == QuestChainState.Available) continue;
                QuestStepDefinition step = session.QuestChains.GetCurrentStep(chain);
                bool has = step != null && step.Objectives.Count > 0;
                model.Chain = new DayChainSummary(chain, state, has ? step.Objectives[0].DisplayKey : string.Empty,
                    has ? session.QuestChains.GetObjectiveProgress(chain, 0) : 0, has ? step.Objectives[0].RequiredCount : 0, has);
                model.HasChain = true;
                break;
            }

            return model;
        }

        private static IReadOnlyList<DayPlanSummary> PlanSummaries(GameSession session)
        {
            DailyPlanProgress daily = session.DailyPlan;
            var list = new List<DayPlanSummary>(daily.Slots.Count);
            IReadOnlyList<DailyPlanRouteSet> all = session.Definitions.All<DailyPlanRouteSet>();
            var sets = new DailyPlanRouteSet[all.Count];
            for (int i = 0; i < sets.Length; i++) sets[i] = all[i];
            var runner = new DailyPlanRouteRunner();
            foreach (DailyPlanSlotRecord slot in daily.Slots)
            {
                if (string.IsNullOrEmpty(slot.RouteId)) continue;
                DailyPlanRoute route = runner.FindRoute(sets, slot.RouteId);
                var logs = new string[slot.ResultLogs.Count];
                for (int i = 0; i < logs.Length; i++) logs[i] = slot.ResultLogs[i];
                list.Add(new DayPlanSummary(route != null ? route.DisplayNameKey : slot.RouteId, ActivityName(session, slot), slot.Completed, logs,
                    route != null ? route.NextHintKey : slot.NextHintKey));
            }
            return list;
        }

        // Unity FindDailyPlanActivityName: 맞춘 활동의 종류마다 그 정의의 이름 키, 없으면 id 그대로.
        private static string ActivityName(GameSession session, DailyPlanSlotRecord slot)
        {
            string id = slot.MatchedActivityId;
            if (string.IsNullOrEmpty(id)) return string.Empty;
            switch (slot.MatchedActivityKind)
            {
                case DailyPlanActivityKind.Practice:
                    return session.Definitions.TryGet<CareerPractice>(id, out CareerPractice practice) ? practice.DisplayNameKey : id;
                case DailyPlanActivityKind.PartTimeWork:
                    return session.Definitions.TryGet<PartTimeWork>(id, out PartTimeWork work) ? work.DisplayNameKey : id;
                default:
                    return session.Definitions.TryGet<LocationActivity>(id, out LocationActivity activity) ? activity.DisplayNameKey : id;
            }
        }
    }
}
