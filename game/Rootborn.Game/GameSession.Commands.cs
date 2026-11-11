using System;
using Rootborn.Core.Careers;
using Rootborn.Core.Dialogue;
using Rootborn.Core.Quests;
using Rootborn.Core.StudentLife;

namespace Rootborn.Game
{
    /// <summary>학생 생활 명령. 각 명령은 Core 결과를 그대로 돌려주고, 적용되면 저장한다.</summary>
    public sealed partial class GameSession
    {
        // ---------- 장소·대화 ----------

        /// <summary>
        /// 장소에 들어간다(Unity LocationActivityInteractor 순서): 첫 방문 기록 → 방문 이벤트(퀘스트·체인) → 캠페인 → 장소 상태.
        /// 방문 이벤트는 다시 와도 매번 알린다.
        /// </summary>
        public LocationVisitResult VisitLocation(string locationId)
        {
            new LocationVisitProgress(Student).TryRecordLocationVisit(locationId, out var result);
            if (result.Kind == LocationVisitResultKind.InvalidRequest) return result;
            Announce(NewEvent(QuestEventKind.LocationVisit, "location-visit", locationId), true);
            ApplyCampaign("campaign-location:" + locationId);
            LastLocationState = _locationStates.Resolve(new LocationStateContext(locationId, null, Student, WorldAccess, QuestLog, _npcSchedules, LocationStates));
            SaveAndNotify(AnnounceTopics);
            return result;
        }

        /// <summary>마지막으로 들어간 장소의 상태 요약(장소 패널이 그린다).</summary>
        public LocationStateSummaryModel LastLocationState { get; private set; }

        /// <summary>
        /// NPC에게 말을 건다(Unity NpcInteractor 순서): 퀘스트 로그에 대화(키 "talk:&lt;npc&gt;") → 체인(시작 NPC면 체인 시작) → 캠페인
        /// → 첫 만남 기록 → 저장. 보여 줄 대화를 돌려준다(없으면 null).
        /// </summary>
        public DialogueDefinition TalkTo(string npcId)
        {
            if (!_definitions.TryGet<NpcDefinition>(npcId, out var npc)) return null;
            var dialogue = npc.ResolveDialogue(Student);
            CurrentDialogue = dialogue;
            CurrentDialogueNpcId = npc.Id;
            QuestLog.RecordEvent(new QuestEvent(QuestEventKind.Talk, "talk:" + npc.Id, npc.Id));
            Announce(NewEvent(QuestEventKind.Talk, "npc-talk", npc.Id), false);
            ApplyCampaign("campaign-npc:" + npc.Id);
            new LocationVisitProgress(Student).TryRecordNpcMeeting(npc.Id, out _);
            SaveAndNotify(AnnounceTopics, GameChangeTopics.Dialogue);
            return dialogue;
        }

        public DialogueDefinition CurrentDialogue { get; private set; }
        public string CurrentDialogueNpcId { get; private set; }

        /// <summary>열린 대화의 선택지를 고른다(퀘스트 받기·보상 받기). 받기는 퀘스트의 제공 조건·선행 조건을 따른다.</summary>
        public bool ChooseDialogueOption(int choiceIndex)
        {
            var dialogue = CurrentDialogue;
            if (dialogue == null || choiceIndex < 0 || choiceIndex >= dialogue.Choices.Count) return false;
            var applied = dialogue.Choices[choiceIndex].TryExecute(new DialogueChoiceContext(QuestLog, Rewards()));
            return Saved(applied, applied, RewardTopics, GameChangeTopics.Dialogue);
        }

        public void CloseDialogue()
        {
            bool wasOpen = CurrentDialogue != null || CurrentDialogueNpcId != null;
            CurrentDialogue = null;
            CurrentDialogueNpcId = null;
            if (wasOpen) Notify(null, GameChangeTopics.Dialogue);
        }

        // ---------- 퀘스트 ----------

        public bool AcceptQuest(string questId)
        {
            if (!_definitions.TryGet<QuestDefinition>(questId, out var quest)) return false;
            var applied = QuestLog.Accept(quest, QuestContext(quest));
            return Saved(applied, applied, null, GameChangeTopics.Quests);
        }

        public bool ClaimQuestReward(string questId)
        {
            if (!_definitions.TryGet<QuestDefinition>(questId, out var quest)) return false;
            var applied = QuestLog.ClaimReward(quest, Rewards());
            return Saved(applied, applied, RewardTopics);
        }

        /// <summary>체인을 직접 받는다(Unity QuestChainLogPanel). 받으면 추적한다.</summary>
        public bool AcceptQuestChain(string chainId)
        {
            if (!_definitions.TryGet<QuestChainDefinition>(chainId, out var chain) || !QuestChains.AcceptWithContext(chain, QuestContext())) return false;
            QuestChains.Track(chain);
            return Saved(true, true, null, GameChangeTopics.Quests);
        }

        public bool ClaimQuestChainRewards(string chainId)
        {
            if (!_definitions.TryGet<QuestChainDefinition>(chainId, out var chain)) return false;
            var applied = QuestChains.ClaimCompletionRewards(chain, Rewards());
            return Saved(applied, applied, RewardTopics);
        }

        // ---------- 활동 ----------

        /// <summary>생활 활동. Unity 판처럼 퀘스트·캠페인에는 알리지 않는다.</summary>
        public LifeActivityResult PerformLifeActivity(string activityId, string requestId, int choiceIndex = -1)
        {
            var result = new LifeActivityResult(LifeActivityResultKind.InvalidRequest, SaveSlot, PlayerId, activityId, requestId);
            if (!_definitions.TryGet<LifeActivity>(activityId, out var activity)) return result;
            var runner = new LifeActivityRunner();
            bool applied = choiceIndex < 0
                ? runner.TryPerform(activity, Student, requestId, out result)
                : runner.TryPerformChoice(activity, activity.GetChoice(choiceIndex), Student, requestId, out result);
            return Saved(result, applied, null, GameChangeTopics.Student);
        }

        /// <summary>
        /// 장소 활동(Unity LocationIdentityPanel.PerformActivity 순서): 실행 → 진로 힌트 → 일과 칸 채우기 → 퀘스트 로그 → 체인.
        /// </summary>
        public LocationActivityResult PerformLocationActivity(string activityId, string requestId)
        {
            if (!_definitions.TryGet<LocationActivity>(activityId, out var activity))
                return new LocationActivityResult(LifeActivityResultKind.InvalidRequest, SaveSlot, PlayerId, activityId, string.Empty, string.Empty, requestId, null);
            if (!new LocationActivityRunner().TryPerform(activity, Student, requestId, out var result)) return result;
            CareerCandidates.ApplyFirstMatchingHint(_definitions.All<CareerHint>(), Student, result.RequestId, result.OutcomeLogIds);
            new DailyPlanRouteRunner().TryRecordScheduledActivity(DailyPlan, _routeSets, activity, Student, Student.CurrentDay, requestId, out _);
            Announce(new QuestEvent(QuestEventKind.LocationActivity, requestId, activity.Id), true);
            SaveAndNotify(AnnounceTopics, GameChangeTopics.Career);
            return result;
        }

        /// <summary>학교 밖 활동. Unity 판처럼 실행하고 저장만 한다.</summary>
        public OutsideSchoolActivityResult PerformOutsideSchoolActivity(string activityId, string requestId)
        {
            _definitions.TryGet<OutsideSchoolActivity>(activityId, out var activity);
            bool applied = new OutsideSchoolActivityRunner().TryPerform(activity, Student, requestId, out var result);
            return Saved(result, applied, null, GameChangeTopics.Student);
        }

        /// <summary>마을 돕기. Unity 판처럼 실행하고 저장만 한다.</summary>
        public TownHelpActionResult PerformTownHelp(string actionId, string requestId)
        {
            _definitions.TryGet<TownHelpAction>(actionId, out var action);
            bool applied = new TownHelpActionRunner().TryPerform(action, Student, requestId, out var result);
            return Saved(result, applied, null, GameChangeTopics.Student);
        }

        /// <summary>미니게임 없는 알바: 실행 → 퀘스트 로그·체인.</summary>
        public PartTimeWorkResult PerformPartTimeWork(string workId, string requestId)
        {
            _definitions.TryGet<PartTimeWork>(workId, out var work);
            if (!new PartTimeWorkRunner().TryPerform(work, Student, Inventory, requestId, out var result)) return result;
            Announce(new QuestEvent(QuestEventKind.PartTimeWork, requestId, work.Id), true);
            SaveAndNotify(AnnounceTopics);
            return result;
        }

        /// <summary>미니게임 없는 진로 연습. 진로 힌트는 Core 실행기가 연다.</summary>
        public LifeActivityResult PerformPractice(string practiceId, string requestId)
        {
            var result = new LifeActivityResult(LifeActivityResultKind.InvalidRequest, SaveSlot, PlayerId, practiceId, requestId);
            if (!_definitions.TryGet<CareerPractice>(practiceId, out var practice)) return result;
            bool applied = new CareerPracticeRunner().TryPerform(practice, Student, requestId, out result);
            return Saved(result, applied, null, GameChangeTopics.Student, GameChangeTopics.Career);
        }

        // ---------- 미니게임 ----------

        /// <summary>미니게임 알바를 예약한다. 시간·체력·집중은 지금 쓴다. seed는 미니게임 판의 시드다.</summary>
        public AdultCareerResult BeginPartTimeWork(string workId, string requestId, int seed)
        {
            _definitions.TryGet<IMinigamePartTimeWork>(workId, out var work);
            var result = _minigames.TryBeginPartTimeWork(work, Student, Adult, new PartTimeWorkPayroll(Inventory), requestId, seed);
            return Saved(result, result.Applied, null, GameChangeTopics.Student, GameChangeTopics.Career);
        }

        public AdultCareerResult BeginPractice(string practiceId, string requestId, int seed)
        {
            _definitions.TryGet<CareerPractice>(practiceId, out var practice);
            var result = _minigames.TryBeginPractice(practice, Student, Adult, requestId, seed);
            return Saved(result, result.Applied, null, GameChangeTopics.Student, GameChangeTopics.Career);
        }

        /// <summary>
        /// 예약한 미니게임 판을 끝낸다(Unity AdultCareerRuntimeComponent.SettleStudentMinigame): 정산 → 일과 칸 채우기 → 체인에 알림.
        /// 미니게임 알바는 Unity 판처럼 일반 퀘스트 로그에는 알리지 않는다. 실패하면 포기와 같다(쓴 비용은 돌아오지 않는다).
        /// </summary>
        public AdultCareerResult SettleMinigame(string requestId, bool succeeded)
        {
            var pending = Student.PendingMinigame;
            if (pending == null || pending.RequestId != requestId) return new AdultCareerResult(AdultCareerResultKind.InvalidRequest);
            int day = Student.CurrentDay;
            AdultCareerResult result;
            if (pending.Kind == StudentMinigameKind.PartTimeWork)
            {
                _definitions.TryGet<IMinigamePartTimeWork>(pending.DefinitionId, out var work);
                result = _minigames.TrySettlePartTimeWork(work, Student, new PartTimeWorkPayroll(Inventory), requestId, succeeded);
                if (result.Applied && succeeded)
                {
                    new DailyPlanRouteRunner().TryRecordPartTimeWork(DailyPlan, _routeSets, work, Student, day, requestId, out _);
                    Announce(new QuestEvent(QuestEventKind.PartTimeWork, requestId, work.Id), false);
                }
            }
            else
            {
                _definitions.TryGet<CareerPractice>(pending.DefinitionId, out var practice);
                result = _minigames.TrySettlePractice(practice, Student, requestId, succeeded);
                if (result.Applied && succeeded) new DailyPlanRouteRunner().TryRecordPractice(DailyPlan, _routeSets, practice, Student, day, requestId, out _);
            }

            return Saved(result, result.Applied, AnnounceTopics, GameChangeTopics.Career);
        }

        public AdultCareerResult ForfeitMinigame(string requestId)
        {
            var result = _minigames.Forfeit(Student, requestId);
            return Saved(result, result.Applied, null, GameChangeTopics.Student, GameChangeTopics.Career);
        }

        // ---------- 일과 ----------

        public DailyPlanRoute[] AvailableDailyPlanRoutes() => new DailyPlanRouteRunner().GetAvailableRoutes(_routeSets, new DailyPlanRouteContext(Student, WorldAccess));

        /// <summary>일과 길을 고른다(빈 첫 칸). 고르면 일과 선택 이벤트를 퀘스트·체인에 알린다.</summary>
        public bool SelectDailyPlanRoute(string routeId)
        {
            var route = new DailyPlanRouteRunner().FindRoute(_routeSets, routeId);
            if (!new DailyPlanRouteRunner().TrySelectRoute(DailyPlan, route, new DailyPlanRouteContext(Student, WorldAccess))) return false;
            Announce(NewEvent(QuestEventKind.DailyPlanSelected, "daily-plan-selected", route.Id), true);
            return Saved(true, true, AnnounceTopics);
        }

        /// <summary>일과 칸 하나에 길을 넣는다. 넣으면 일과 선택 이벤트를 알린다.</summary>
        public bool AssignDailyPlanSlot(int slotIndex, string routeId)
        {
            var route = new DailyPlanRouteRunner().FindRoute(_routeSets, routeId);
            if (route == null || !route.IsAvailable(new DailyPlanRouteContext(Student, WorldAccess)) || !DailyPlan.TryAssignSlot(slotIndex, route, Student)) return false;
            Announce(NewEvent(QuestEventKind.DailyPlanSelected, "daily-plan-selected", route.Id), true);
            return Saved(true, true, AnnounceTopics);
        }

        // ---------- 장래희망 ----------

        /// <summary>장래희망을 고른다. 바뀌면 장래희망 선택 이벤트를 퀘스트·체인에 알린다.</summary>
        public CareerInterestSelectionResult SelectCareerInterest(string interestId, string requestId)
        {
            _definitions.TryGet<CareerInterest>(interestId, out var interest);
            var result = CareerInterests.TrySelect(interest, CareerCandidates, Student, Student.CurrentDay, requestId);
            if (result.Applied) Announce(new QuestEvent(QuestEventKind.CareerInterestSelected, "career-interest:" + requestId, interest.Id), true);
            return Saved(result, result.Applied, AnnounceTopics, GameChangeTopics.Career);
        }
    }
}
