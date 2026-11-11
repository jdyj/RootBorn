using System;
using System.Collections.Generic;
using System.Linq;
using Rootborn.Core.Careers;
using Rootborn.Core.Dialogue;
using Rootborn.Core.Inventory;
using Rootborn.Core.Quests;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;
using Rootborn.Core.WorldState;
using Rootborn.Data;

namespace Rootborn.Game
{
    /// <summary>
    /// 게임 한 판(한 세이브 슬롯의 한 플레이어). 영역 진행 상태를 갖고, 화면이 부를 명령을 Core 규칙으로 실행한다.
    /// 규칙 판단은 Core가 한다. 여기서는 정의를 찾아 넘기고, 결과를 다른 영역(퀘스트·체인·캠페인·장소 상태·일과·진로 후보)에
    /// Unity 판과 같은 순서로 알리고, 적용된 명령 뒤에 저장한다.
    /// 진행 상태 객체는 읽기용으로 내준다. 화면은 상태를 바꿀 때 명령만 부른다.
    /// </summary>
    public sealed partial class GameSession
    {
        private readonly GameDefinitions _definitions;
        private readonly GameStartSettings _settings;
        private readonly ISaveStore _store;
        private readonly Func<string, string> _newEventKey;
        private readonly DayEndRuleSet _dayEndRules;
        private readonly LifeCalendar _calendar;
        private readonly Campaign _campaign;
        private readonly IReadOnlyList<QuestChainDefinition> _chains;
        private readonly IReadOnlyList<Milestone> _milestones;
        private readonly DailyPlanRouteSet[] _routeSets;
        private readonly AdultCareerService _adultService;
        private readonly StudentMinigameService _minigames;
        private readonly LocationStateResolver _locationStates;
        private readonly NpcScheduleAccess _npcSchedules;

        private GameSession(GameDefinitions definitions, GameStartSettings settings, ISaveStore store, Func<string, string> newEventKey)
        {
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _newEventKey = newEventKey ?? QuestGameplayEventBridge.CreateRequestKey;

            _dayEndRules = _definitions.Get<DayEndRuleSet>(settings.DayEndRuleId);
            _calendar = _definitions.Get<LifeCalendar>(settings.CalendarId);
            _campaign = string.IsNullOrEmpty(settings.CampaignId) ? null : _definitions.Get<Campaign>(settings.CampaignId);
            _chains = _definitions.All<QuestChainDefinition>();
            _milestones = _definitions.All<Milestone>();
            _routeSets = _definitions.All<DailyPlanRouteSet>().ToArray();
            _adultService = new AdultCareerService(_definitions.All<Facility>().ToArray());
            _minigames = new StudentMinigameService(_definitions.All<CareerPractice>(), _definitions.All<IMinigamePartTimeWork>(),
                _definitions.All<CareerHint>(), _definitions.All<CareerCandidate>());

            var policies = _definitions.All<LocationStateConflictPolicy>();
            var policy = policies.FirstOrDefault(p => p.Id == settings.LocationStateConflictPolicyId) ?? policies.FirstOrDefault();
            var stateCache = new LocationStateLookupCache(_definitions.All<LocationState>());
            // Unity LocationStateRuntimeInstaller: 정책이 없으면 HighestPriority.
            _locationStates = policy != null ? policy.CreateResolver(stateCache) : new LocationStateResolver(stateCache, LocationStateConflictMode.HighestPriority);
            _npcSchedules = new NpcScheduleAccess(new NpcScheduleLookupCache(_definitions.All<NpcSchedule>()));
        }

        public string SaveSlot { get; private set; }
        public string PlayerId { get; private set; }

        // 진행 상태(읽기용).
        public StudentLifeProgress Student { get; private set; }
        public ItemInventory Inventory { get; } = new ItemInventory();
        public QuestLog QuestLog { get; private set; }
        public QuestChainLog QuestChains { get; private set; }
        public AdultCareerProgress Adult { get; private set; }
        public WorldStateProgress World { get; private set; }
        public CampaignProgress Campaign { get; private set; }
        public MilestoneProgress Milestones { get; private set; }
        public CareerInterestProgress CareerInterests { get; private set; }
        public LocationStateProgress LocationStates { get; private set; }
        public CareerCandidateProgress CareerCandidates => Student.CareerCandidates;
        public DailyPlanProgress DailyPlan => Student.DailyPlan;

        /// <summary>
        /// 슬롯의 세이브가 있으면 읽어 이어 가고, 없으면 새 게임으로 시작한다. 새 게임은 첫 명령이 적용될 때 처음 저장한다(Unity 판과 같음).
        /// newEventKey는 퀘스트 이벤트 키를 만든다(기본: 행동 이름 + 새 GUID, Unity 판과 같음).
        /// </summary>
        public static GameSession Start(GameDefinitions definitions, GameStartSettings settings, ISaveStore store, string saveSlot, string playerId,
            Func<string, string> newEventKey = null)
        {
            if (string.IsNullOrEmpty(saveSlot)) throw new ArgumentException("세이브 슬롯이 비어 있다", nameof(saveSlot));
            var session = new GameSession(definitions, settings, store, newEventKey);
            session.SaveSlot = saveSlot;
            session.PlayerId = string.IsNullOrEmpty(playerId) ? SaveFileNames.DefaultPlayerId : playerId;
            session.Load();
            return session;
        }

        public QuestChainDefinition Chain(string chainId) => _definitions.Get<QuestChainDefinition>(chainId);
        public ItemDefinition Item(string itemId) => _definitions.Get<ItemDefinition>(itemId);
        public GameDefinitions Definitions => _definitions;

        private WorldStateAccess WorldAccess => new WorldStateAccess(World, _definitions.All<WorldStateFlag>());
        private CareerInventoryAdapter CareerInventory => new CareerInventoryAdapter(Inventory, _definitions);

        private RewardRuntimeContext Rewards() => new RewardRuntimeContext(QuestLog, Inventory, null, null, Student, WorldAccess, null);
        private QuestRuntimeContext QuestContext(QuestDefinition quest = null) => new QuestRuntimeContext(QuestLog, quest, null, null, Student);

        // ---------- 읽기 ----------

        private void Load()
        {
            var snapshotName = SaveFileNames.AdultCareerSnapshot(PlayerId);
            bool hasSnapshot = _store.TryRead<AdultCareerSnapshotSaveData>(SaveSlot, snapshotName, out var snapshot);

            Student = hasSnapshot && snapshot.Student != null
                ? StudentLifeProgress.FromSaveData(Rebind(snapshot.Student))
                : NewStudent();
            // 하루 예산과 하루 집중은 세이브에 없다. 읽은 진행 상태에도 다시 넣는다.
            Student.ConfigureCalendar(_calendar);
            Student.ConfigureDailyFocus(_dayEndRules.DailyFocus);

            Adult = hasSnapshot && snapshot.Adult != null ? AdultCareerProgress.FromSaveData(snapshot.Adult) : new AdultCareerProgress(SaveSlot, PlayerId);
            if (hasSnapshot) Inventory.LoadFromSaveData(snapshot.Inventory, FindItem);

            World = hasSnapshot && snapshot.World != null
                ? WorldStateProgress.FromSaveData(snapshot.World, SaveSlot, PlayerId)
                : WorldStateSaveFiles.LoadOrCreate(_store, SaveSlot, PlayerId);

            QuestChains = LoadChains(hasSnapshot ? snapshot.QuestChains : null);

            QuestLog = new QuestLog(_definitions.All<QuestDefinition>());
            if (_store.TryRead<QuestLogSaveData>(SaveSlot, SaveFileNames.QuestLog, out var questLog)) QuestLog.LoadFromSaveData(questLog);

            if (_campaign != null)
            {
                Campaign = _store.TryRead<CampaignProgressSaveData>(SaveSlot, SaveFileNames.Campaign(PlayerId), out var campaign)
                    ? CampaignProgress.FromSaveData(campaign, _campaign)
                    : new CampaignProgress(SaveSlot, PlayerId, _campaign);
                Campaign.SetCurrentDayNumber(Student.CurrentDay);
            }

            Milestones = _store.TryRead<MilestoneProgressSaveData>(SaveSlot, SaveFileNames.Milestone(PlayerId), out var milestones)
                ? MilestoneProgress.FromSaveData(milestones, _milestones)
                : new MilestoneProgress(SaveSlot, PlayerId, _milestones);
            CareerInterests = _store.TryRead<CareerInterestProgressSaveData>(SaveSlot, SaveFileNames.CareerInterest(PlayerId), out var interests)
                ? CareerInterestProgress.FromSaveData(interests)
                : new CareerInterestProgress(SaveSlot, PlayerId);
            LocationStates = _store.TryRead<LocationStateProgressSaveData>(SaveSlot, SaveFileNames.LocationState(PlayerId), out var states)
                ? LocationStateProgress.FromSaveData(states, SaveSlot, PlayerId)
                : new LocationStateProgress(SaveSlot, PlayerId);

            LoadOptionalDomains();
        }

        private StudentLifeProgress NewStudent()
        {
            var student = new StudentLifeProgress(SaveSlot, PlayerId, _settings.StartEnergy, _settings.StartFocus, 0, _settings.StartTimeMinutes);
            if (_definitions.TryGet<TutorialStage>(_settings.TutorialStageId, out var stage)) student.SetTutorialStage(stage, string.Empty);
            return student;
        }

        // Unity 판처럼 세이브의 슬롯 이름이 지금 슬롯과 달라도(복사한 슬롯) 지금 슬롯·플레이어로 읽는다.
        private StudentLifeProgressSaveData Rebind(StudentLifeProgressSaveData data)
        {
            data.SaveSlot = SaveSlot;
            data.PlayerId = PlayerId;
            if (data.CareerCandidates != null)
            {
                data.CareerCandidates.SaveSlot = SaveSlot;
                data.CareerCandidates.PlayerId = PlayerId;
            }

            if (data.DailyPlan != null)
            {
                data.DailyPlan.SaveSlot = SaveSlot;
                data.DailyPlan.PlayerId = PlayerId;
            }

            return data;
        }

        private QuestChainLog LoadChains(QuestChainLogSaveData snapshotChains)
        {
            var log = new QuestChainLog(_chains);
            var initial = log.ToSaveData();
            foreach (var record in initial.Chains) SetIdentity(record);
            log.LoadFromSaveData(initial);

            var saved = snapshotChains;
            if (saved == null) _store.TryRead(SaveSlot, SaveFileNames.QuestChain(PlayerId), out saved);
            if (saved?.Chains != null)
            {
                foreach (var record in saved.Chains) if (record != null) SetIdentity(record);
                log.LoadFromSaveData(saved);
            }

            return log;
        }

        private void SetIdentity(QuestChainProgressSaveData record)
        {
            record.SaveSlot = SaveSlot;
            record.PlayerId = PlayerId;
        }

        private ItemDefinition FindItem(string id) => _definitions.TryGet<ItemDefinition>(id, out var item) ? item : null;

        // ---------- 저장 ----------

        /// <summary>
        /// 적용된 명령마다 부른다. Unity 판은 집합체 스냅샷(학생·성인·인벤토리·월드·체인)을 명령마다 쓰고, 나머지 영역 파일은 그 영역이 바뀔 때 썼다.
        /// 여기서는 명령마다 전부 쓴다(내용은 같고, 바뀌지 않은 파일도 다시 쓴다).
        /// </summary>
        private void Save()
        {
            _store.Write(SaveSlot, SaveFileNames.AdultCareerSnapshot(PlayerId), new AdultCareerSnapshotSaveData
            {
                Adult = Adult.ToSaveData(),
                Student = Student.ToSaveData(),
                Inventory = Inventory.ToSaveData(string.Empty),
                World = World.ToSaveData(),
                QuestChains = QuestChains.ToSaveData(),
            });
            WorldStateSaveFiles.Save(_store, World);
            _store.Write(SaveSlot, SaveFileNames.QuestLog, QuestLog.ToSaveData());
            if (Campaign != null) _store.Write(SaveSlot, SaveFileNames.Campaign(PlayerId), Campaign.ToSaveData());
            _store.Write(SaveSlot, SaveFileNames.Milestone(PlayerId), Milestones.ToSaveData());
            _store.Write(SaveSlot, SaveFileNames.CareerInterest(PlayerId), CareerInterests.ToSaveData());
            _store.Write(SaveSlot, SaveFileNames.LocationState(PlayerId), LocationStates.ToSaveData());
            SaveOptionalDomains();
        }

        private T Saved<T>(T result, bool applied)
        {
            if (applied) Save();
            return result;
        }

        // ---------- 퀘스트 이벤트 ----------

        /// <summary>
        /// 행동 하나를 퀘스트에 알린다. toQuestLog면 일반 퀘스트 로그에도 센다(다시 알리는 이벤트는 로그가 세지 않는다).
        /// 체인 쪽은 Unity AdultCareerRuntime의 ApplyRegisteredEventToSession과 같은 순서: 대화 상대가 시작 NPC인 체인을 받고 추적하고,
        /// 받기 전에 고른 일과를 소급해 세고, 이벤트를 기록한 뒤 모든 체인의 단계 진행을 평가한다.
        /// </summary>
        private void Announce(QuestEvent questEvent, bool toQuestLog)
        {
            if (string.IsNullOrEmpty(questEvent.EventKey) || questEvent.Count <= 0) return;
            if (toQuestLog) QuestLog.RecordEvent(in questEvent);

            var context = QuestContext();
            var rewards = Rewards();
            if (questEvent.Kind == QuestEventKind.Talk && !string.IsNullOrEmpty(questEvent.SubjectId))
            {
                foreach (var chain in _chains)
                {
                    if (chain == null || !chain.IsStartedByTalkingTo(questEvent.SubjectId) || QuestChains.GetState(chain) != QuestChainState.Available
                        || !QuestChains.AcceptWithContext(chain, context)) continue;
                    QuestChains.Track(chain);
                    CreditPlanChosenBeforeStart(context, rewards);
                }
            }

            QuestChains.RecordEventWithContext(questEvent, context, rewards);
            foreach (var chain in _chains) if (chain != null) QuestChains.EvaluateStepProgress(chain, context, rewards);
        }

        // Unity QuestChainRuntimeService.CreditPlanChosenBeforeStart.
        private void CreditPlanChosenBeforeStart(QuestRuntimeContext context, RewardRuntimeContext rewards)
        {
            string routeId = DailyPlan.SelectedRouteId;
            if (string.IsNullOrEmpty(routeId) || new DailyPlanRouteRunner().FindRoute(_routeSets, routeId) == null) return;
            QuestChains.RecordEventWithContext(new QuestEvent(QuestEventKind.DailyPlanSelected, "daily-plan-before-start:" + Student.CurrentDay + ":" + routeId, routeId),
                context, rewards);
        }

        private QuestEvent NewEvent(QuestEventKind kind, string action, string subjectId) => new QuestEvent(kind, _newEventKey(action), subjectId);

        // 캠페인 목표를 지금 진행 상태로 평가한다(Unity CampaignRuntimeInstaller.ApplyProgress). 방문한 장소는 진행 상태의 방문 기록에서 읽는다.
        private CampaignApplyResult ApplyCampaign(string resultId)
        {
            var result = new CampaignApplyResult(Array.Empty<string>());
            if (_campaign == null || Campaign == null) return result;
            var visited = new LocationVisitProgress(Student).VisitedLocationIds;
            new CampaignRunner().TryApplyProgress(_campaign, new CampaignEvaluationContext(Student, Campaign, visited, Array.Empty<string>()), resultId, out result);
            return result;
        }
    }
}
