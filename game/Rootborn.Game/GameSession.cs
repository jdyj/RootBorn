using System;
using System.Collections.Generic;
using System.Linq;
using Rootborn.Core.Careers;
using Rootborn.Core.Dialogue;
using Rootborn.Core.Gathering;
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
            Func<string, string> newEventKey = null, Rootborn.Core.Time.IRoomTime room = null)
        {
            if (string.IsNullOrEmpty(saveSlot)) throw new ArgumentException("세이브 슬롯이 비어 있다", nameof(saveSlot));
            var session = new GameSession(definitions, settings, store, newEventKey);
            session.SaveSlot = saveSlot;
            session.PlayerId = string.IsNullOrEmpty(playerId) ? SaveFileNames.DefaultPlayerId : playerId;
            session.Load();
            session.UseRoom(room);
            return session;
        }

        /// <summary>
        /// data/를 이 저장소의 실행 서비스(RuleServices: 레시피 공개처럼 다른 세이브 파일을 쓰는 결과)와 함께 읽어 시작한다.
        /// 시작 값은 registry.json의 기본값 키에서 온다. 정의 읽기가 실패하면 예외를 낸다(일부만 읽힌 채 시작하지 않는다).
        /// 정의를 직접 넘기는 <see cref="Start"/>는 서비스 없이 읽은 정의면 레시피 공개를 저장하지 않는다.
        /// </summary>
        public static GameSession StartFromData(string dataDirectory, ISaveStore store, string saveSlot, string playerId, Func<string, string> newEventKey = null,
            Rootborn.Core.Time.IRoomTime room = null)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            var services = new RuleServices(store);
            var loaded = GameDataLoader.Load(dataDirectory, b => Rootborn.Data.Builders.RootbornBuilders.Register(b, services));
            loaded.ThrowIfFailed();
            return Start(loaded.Definitions, GameStartSettings.FromDefinitions(loaded.Definitions), store, saveSlot, playerId, newEventKey, room);
        }

        /// <summary>자원 레시피 공개 진행(resource-recipe-progress*.json)을 읽는다. 공개는 장소 활동 결과가 바로 저장한다.</summary>
        public ResourceRecipeProgress ResourceRecipes() => ResourceRecipeSaveFiles.LoadOrCreate(_store, SaveSlot, PlayerId);

        public QuestChainDefinition Chain(string chainId) => _definitions.Get<QuestChainDefinition>(chainId);
        public ItemDefinition Item(string itemId) => _definitions.Get<ItemDefinition>(itemId);
        public GameDefinitions Definitions => _definitions;

        private WorldStateAccess WorldAccess => new WorldStateAccess(World, _definitions.All<WorldStateFlag>());
        private ICareerInventory CareerInventory => new ItemInventoryCareerInventory(Inventory, FindItem);

        // Unity PlayerQuestSession.RewardContext처럼 이야기 깃발은 그때마다 빈 묶음이다(어디에도 저장되지 않는다).
        // null을 넘기면 이야기 깃발 효과가 있는 퀘스트(quest.gatherWood)의 보상을 받을 수 없다.
        private RewardRuntimeContext Rewards() => new RewardRuntimeContext(QuestLog, Inventory, null, new UnsavedStoryFlags(), Student, WorldAccess, FurnitureUnlocks);

        /// <summary>Unity StoryFlagSet(new StoryFlagSet())과 같은 메모리 전용 묶음.</summary>
        private sealed class UnsavedStoryFlags : IQuestStoryFlags
        {
            private readonly HashSet<string> _set = new HashSet<string>(StringComparer.Ordinal);
            public bool IsSet(string flagId) => !string.IsNullOrEmpty(flagId) && _set.Contains(flagId);
            public bool Set(string flagId) => !string.IsNullOrEmpty(flagId) && _set.Add(flagId);
        }
        private QuestRuntimeContext QuestContext(QuestDefinition quest = null) => new QuestRuntimeContext(QuestLog, quest, null, null, Student);

        // ---------- 읽기 ----------

        /// <summary>
        /// 슬롯을 읽는다. 읽기 문제는 하나에서 멈추지 않고 모두 모아 끝에 <see cref="SaveSlotDamagedException"/>으로 알린다(StartReads).
        /// 이 플레이어의 영역 파일은 있는데 스냅샷이 없으면(옛 Unity 영역 파일도 없으면) 손상으로 본다. 새 게임으로 남은 파일을 덮지 않기 위해서다.
        /// </summary>
        private void Load()
        {
            var store = new StartReads(_store, SaveSlot);
            var snapshotName = SaveFileNames.AdultCareerSnapshot(PlayerId);
            bool hasSnapshot = store.TryRead<AdultCareerSnapshotSaveData>(SaveSlot, snapshotName, out var snapshot);

            // 스냅샷이 없는 슬롯은 Unity 판의 옛 영역 파일(학생 진행·인벤토리·일과·진로 후보)에서 이어 간다(Core PreSnapshotSlotFiles).
            var olderStudent = hasSnapshot ? null : PreSnapshotSlotFiles.ReadStudent(store, SaveSlot, PlayerId);
            Student = hasSnapshot && snapshot.Student != null
                ? StudentLifeProgress.FromSaveData(Rebind(snapshot.Student))
                : olderStudent != null ? StudentLifeProgress.FromSaveData(olderStudent) : NewStudent();
            // 하루 예산과 하루 집중은 세이브에 없다. 읽은 진행 상태에도 다시 넣는다.
            Student.ConfigureCalendar(_calendar);
            Student.ConfigureDailyFocus(_dayEndRules.DailyFocus);
            if (!hasSnapshot) PreSnapshotSlotFiles.ImportInto(Student, store);

            Adult = hasSnapshot && snapshot.Adult != null ? AdultCareerProgress.FromSaveData(snapshot.Adult) : new AdultCareerProgress(SaveSlot, PlayerId);
            var inventory = hasSnapshot ? snapshot.Inventory : PreSnapshotSlotFiles.ReadInventory(store, SaveSlot, PlayerId);
            if (inventory != null) Inventory.LoadFromSaveData(inventory, FindItem);

            World = hasSnapshot && snapshot.World != null
                ? WorldStateProgress.FromSaveData(snapshot.World, SaveSlot, PlayerId)
                : WorldStateSaveFiles.LoadOrCreate(store, SaveSlot, PlayerId);
            // 스냅샷이 월드 상태의 원본이어도 사본 파일(플레이어·슬롯 공유)이 깨졌으면 다른 영역처럼 알린다. 다음 저장이 조용히 덮지 않게.
            if (hasSnapshot && snapshot.World != null) WorldStateSaveFiles.LoadOrCreate(store, SaveSlot, PlayerId);

            QuestChains = LoadChains(store, hasSnapshot ? snapshot.QuestChains : null);
            LoadFurniture(store, hasSnapshot ? snapshot : null);

            QuestLog = new QuestLog(_definitions.All<QuestDefinition>());
            if (store.TryRead<QuestLogSaveData>(SaveSlot, SaveFileNames.PlayerQuestLog(PlayerId), out var questLog)) QuestLog.LoadFromSaveData(questLog);

            if (_campaign != null)
            {
                Campaign = store.TryRead<CampaignProgressSaveData>(SaveSlot, SaveFileNames.Campaign(PlayerId), out var campaign)
                    ? CampaignProgress.FromSaveData(campaign, _campaign)
                    : new CampaignProgress(SaveSlot, PlayerId, _campaign);
                Campaign.SetCurrentDayNumber(Student.CurrentDay);
            }

            Milestones = store.TryRead<MilestoneProgressSaveData>(SaveSlot, SaveFileNames.Milestone(PlayerId), out var milestones)
                ? MilestoneProgress.FromSaveData(milestones, _milestones)
                : new MilestoneProgress(SaveSlot, PlayerId, _milestones);
            CareerInterests = store.TryRead<CareerInterestProgressSaveData>(SaveSlot, SaveFileNames.CareerInterest(PlayerId), out var interests)
                ? CareerInterestProgress.FromSaveData(interests)
                : new CareerInterestProgress(SaveSlot, PlayerId);
            LocationStates = store.TryRead<LocationStateProgressSaveData>(SaveSlot, SaveFileNames.LocationState(PlayerId), out var states)
                ? LocationStateProgress.FromSaveData(states, SaveSlot, PlayerId)
                : new LocationStateProgress(SaveSlot, PlayerId);

            LoadOptionalDomains(store);

            store.ThrowIfDamaged(hasSnapshot ? null : snapshotName, SlotSharedFiles(), PreSnapshotFiles());
        }

        // 이 플레이어 것이 아닌 슬롯 공용 파일. 다른 플레이어가 논 슬롯에 처음 들어온 플레이어는 이것만 있어도 새 게임이다.
        private string[] SlotSharedFiles() => new[]
        {
            SaveFileNames.WorldStateShared, SaveFileNames.ObjectiveJournalTracking, SaveFileNames.HouseState,
            SaveFileNames.HouseFurnitureLayoutBeforeStages, SaveFileNames.HouseFurnitureLayout(HouseStage),
        };

        // 이 판(Save)은 쓰지 않고 스냅샷 전 Unity 판만 쓰던 영역 파일. 이것이 있으면 스냅샷 없이 이어 가는 옛 슬롯이다.
        private string[] PreSnapshotFiles() => new[]
        {
            SaveFileNames.UnityStudentLife(PlayerId), SaveFileNames.UnityInventory(PlayerId), SaveFileNames.UnityCareerCandidates(PlayerId),
            SaveFileNames.UnityDailyPlan(PlayerId), SaveFileNames.UnityDailyPlanSanitized(PlayerId),
            SaveFileNames.FurnitureUnlocks(PlayerId), SaveFileNames.QuestChain(PlayerId),
        };

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

        private QuestChainLog LoadChains(ISaveStore store, QuestChainLogSaveData snapshotChains)
        {
            var log = new QuestChainLog(_chains);
            var initial = log.ToSaveData();
            foreach (var record in initial.Chains) SetIdentity(record);
            log.LoadFromSaveData(initial);

            var saved = snapshotChains;
            if (saved == null) store.TryRead(SaveSlot, SaveFileNames.QuestChain(PlayerId), out saved);
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
        /// 한 묶음(SaveBatches)으로 써서, 묶음을 지원하는 저장소(FileSaveStore)에서는 도중에 꺼져도 슬롯이 전부 옛것이거나 전부 새것이다.
        /// </summary>
        private void Save()
        {
            using (var store = SaveBatches.Begin(_store, SaveSlot))
            {
                store.Write(SaveSlot, SaveFileNames.AdultCareerSnapshot(PlayerId), new AdultCareerSnapshotSaveData
                {
                    Adult = Adult.ToSaveData(),
                    Student = Student.ToSaveData(),
                    Inventory = Inventory.ToSaveData(string.Empty),
                    World = World.ToSaveData(),
                    QuestChains = QuestChains.ToSaveData(),
                    FurnitureUnlocks = FurnitureUnlocks.ToSaveData(),
                });
                WorldStateSaveFiles.Save(store, World);
                store.Write(SaveSlot, SaveFileNames.PlayerQuestLog(PlayerId), QuestLog.ToSaveData());
                if (Campaign != null) store.Write(SaveSlot, SaveFileNames.Campaign(PlayerId), Campaign.ToSaveData());
                store.Write(SaveSlot, SaveFileNames.Milestone(PlayerId), Milestones.ToSaveData());
                store.Write(SaveSlot, SaveFileNames.CareerInterest(PlayerId), CareerInterests.ToSaveData());
                store.Write(SaveSlot, SaveFileNames.LocationState(PlayerId), LocationStates.ToSaveData());
                SaveOptionalDomains(store);
                store.Commit();
            }
        }

        // ---------- 퀘스트 이벤트 ----------

        /// <summary>
        /// 행동 하나를 퀘스트에 알린다. toQuestLog면 일반 퀘스트 로그에도 센다(다시 알리는 이벤트는 로그가 세지 않는다).
        /// 체인에 적용하는 순서(시작 NPC 체인 받기 → 받기 전 일과 소급 → 기록 → 모든 체인 평가)는 Core QuestChainEvents가 정한다.
        /// </summary>
        private void Announce(QuestEvent questEvent, bool toQuestLog)
        {
            if (!QuestChainEvents.IsApplicable(in questEvent)) return;
            if (toQuestLog) QuestLog.RecordEvent(in questEvent);
            QuestChainEvents.Apply(QuestChains, _chains, _routeSets, questEvent, QuestContext(), Rewards());
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
