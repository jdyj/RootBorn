using System;
using System.Collections.Generic;
using Rootborn.Game.Bootstrap;
using Rootborn.Game.Careers;
using Rootborn.Game.Common;
using Rootborn.Game.Minigames;
using Rootborn.Game.Placement;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.Game.WorldState;
using Rootborn.Network.Session;
using Rootborn.UI.Quests;
using Unity.Netcode;
using UnityEngine;

namespace Rootborn.UI.Careers
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerIdentity), typeof(StudentLifeProgressComponent), typeof(PlayerInventory))]
    public sealed class AdultCareerRuntimeComponent : MonoBehaviour
    {
        private GameDataRegistry _registry;
        private SaveService _storage;
        private AdultCareerCommandCoordinator _coordinator;
        private PlayerIdentity _identity;
        private CareerCandidateProgressComponent _candidates;
        private string _slot;
        private string _player;
        private bool _bound;
        private bool _publishing;
        private Inventory _observedInventory;
        private FacilityWorkDefinition _activeWork;
        private CareerPracticeDefinition _activePractice;
        private PartTimeWorkDefinition _activePartTimeWork;
        private StudentMinigameService _studentMinigames;
        private string _activeWorkRequest;
        public MinigameSession ActiveWorkSession { get; private set; }
        public bool InterruptedWorkForfeited { get; private set; }
        public AdultCareerProgress Progress { get; private set; }
        public WorldStateProgress World { get; private set; }
        public FurnitureUnlockProgress FurnitureUnlocks { get; private set; }
        public QuestChainLog QuestChains { get; private set; }
        public StudentLifeProgressComponent StudentComponent { get; private set; }
        public PlayerInventory PlayerInventory { get; private set; }
        public GameDataRegistry Registry => _registry;
        public bool IsBound => _bound;
        public bool RecoveryRequired { get; private set; }
        public AdultCareerResult LastResult { get; private set; }
        public event Action Committed;

        public bool SceneTransferPending { get; private set; }
        public bool CanMutate => HasBoundIdentity && !SceneTransferPending && AdultCareerPersistenceRouter.IsOwner(_slot, _player, this) && IsOfflineAuthority();
        private bool HasBoundIdentity => this != null && _bound && !RecoveryRequired && !_publishing
            && _identity != null && _identity.PlayerId == _player && ActiveSaveContext.SlotId == _slot
            && StudentComponent != null && StudentComponent.Progress != null
            && StudentComponent.Progress.SaveSlot == _slot && StudentComponent.Progress.PlayerId == _player;

        public static AdultCareerRuntimeComponent Ensure(GameObject player, GameDataRegistry registry)
        {
            if (player == null || registry == null || string.IsNullOrEmpty(ActiveSaveContext.SlotId)) return null;
            if (player.GetComponent<PlayerIdentity>() == null || player.GetComponent<StudentLifeProgressComponent>() == null || player.GetComponent<PlayerInventory>() == null) return null;
            var component = player.GetComponent<AdultCareerRuntimeComponent>();
            if (component == null) component = player.AddComponent<AdultCareerRuntimeComponent>();
            if (!component.IsBound) component.Bind(registry, new SaveService(ActiveSaveContext.SlotId));
            if (component._slot != ActiveSaveContext.SlotId || component._player != player.GetComponent<PlayerIdentity>().PlayerId) return null;
            return component;
        }

        public void Bind(GameDataRegistry registry, SaveService storage)
        {
            CancelSceneTransfer();
            AdultCareerPersistenceRouter.Unregister(this);
            UnsubscribeInventory();
            _bound = false; RecoveryRequired = false;
            ActiveWorkSession = null; _activeWork = null; _activePractice = null; _activePartTimeWork = null; _activeWorkRequest = null; InterruptedWorkForfeited = false;
            _registry = registry; _storage = storage;
            _identity = GetComponent<PlayerIdentity>(); StudentComponent = GetComponent<StudentLifeProgressComponent>(); PlayerInventory = GetComponent<PlayerInventory>();
            if (_registry == null || _storage == null || _registry.StudentLifeCalendar == null || _identity == null || StudentComponent == null || PlayerInventory == null) { RecoveryRequired = true; return; }
            _slot = storage.Slot; _player = _identity.PlayerId;
            var student = StudentComponent.EnsureProgress();
            if (student.SaveSlot != _slot || student.PlayerId != _player) { RecoveryRequired = true; return; }
            student.ConfigureCalendar(_registry.StudentLifeCalendar);
            PlayerInventory.Bind(_registry);
            _candidates = GetComponent<CareerCandidateProgressComponent>();
            if (_candidates == null) _candidates = gameObject.AddComponent<CareerCandidateProgressComponent>();
            _candidates.Bind(_registry.CareerCandidates, _registry.CareerHints);
            _coordinator = new AdultCareerCommandCoordinator(_registry.FacilityDefinitions, _registry.Items, _registry.StudentLifeCalendar, storage);
            _studentMinigames = new StudentMinigameService(_registry);
            Progress = new AdultCareerProgress(_slot, _player);
            QuestChains = NewScopedQuestLog();
            bool primaryExists = AdultCareerSnapshotPersistence.HasCommittedFile(storage, _player);
            if (primaryExists)
            {
                var snapshot = AdultCareerSnapshotPersistence.Load(storage, _player);
                var session = snapshot == null ? null : AdultCareerSnapshotPersistence.Restore(snapshot, _slot, _player, _registry.Items, _registry.StudentLifeCalendar);
                if (session == null) { RecoveryRequired = true; return; }
                if (session.Progress.HasPendingWork || session.Student.HasPendingMinigame)
                {
                    var result = session.Progress.HasPendingWork
                        ? new AdultCareerService(_registry.FacilityDefinitions).TryForfeitReservedWork(session.Student, session.Progress, session.Progress.PendingWork.RequestId)
                        : _studentMinigames.Forfeit(session.Student, session.Student.PendingMinigame.RequestId);
                    if (!result.Applied || !AdultCareerSnapshotPersistence.SaveSession(storage, AdultCareerSnapshotPersistence.CaptureSession(session))) { RecoveryRequired = true; return; }
                    InterruptedWorkForfeited = true;
                }
                _bound = true;
                RegisterOwner();
                Publish(session);
            }
            else
            {
                if (!LoadLegacyInventory()) { RecoveryRequired = true; return; }
                DailyPlanProgressPersistence.MigrateInto(student);
                World = WorldStateProgressPersistence.LoadOrCreate(_slot, _player);
                FurnitureUnlocks = FurnitureUnlockProgressPersistence.LoadOrCreate(_slot, _player);
                // The unbound store is used once for legacy migration; this component owns the resulting live log.
                var legacy = new QuestChainProgressStore().GetOrCreate(_registry, student);
                if (legacy != null) QuestChains.LoadFromSaveData(legacy.ToSaveData());
                ScopeQuestLog();
                student.RestoreCareerCandidates(_candidates.EnsureProgress().ToSaveData());
                _candidates.BindProgress(student.CareerCandidates);
                _bound = true;
                RegisterOwner();
            }
            _observedInventory = PlayerInventory.Inventory;
            _observedInventory.OnChanged += HandleInventoryChanged;
        }

        public AdultCareerResult TryTransition(FacilityDefinition facility, string requestId)
        {
            return ExecuteSessionCommand((session, service) => service.TryTransition(facility, session.Student, session.Student.CareerCandidates, session.Progress, requestId));
        }
        public AdultCareerResult TryCompleteWork(FacilityWorkDefinition work, string requestId, int score, bool autoSkip)
        {
            return ExecuteSessionCommand((session, service) => service.TryCompleteWork(work, session.Student, session.Progress, session.Inventory, requestId, score, autoSkip));
        }
        public AdultCareerResult TryBeginWork(FacilityWorkDefinition work, string requestId, int seed)
        {
            var result = ExecuteSessionCommand((session, service) => service.TryReserveWork(work, session.Student, session.Progress, requestId, seed));
            if (!result.Applied) return result;
            _activeWork = work; _activeWorkRequest = requestId;
            _activePractice = null; _activePartTimeWork = null;
            // Bonus inputs are detached at attempt start, so live stat changes cannot alter later stages.
            var attemptStudent = StudentLifeProgress.FromSaveData(StudentComponent.Progress.ToSaveData(), null, null, null);
            attemptStudent.ConfigureCalendar(_registry.StudentLifeCalendar);
            ActiveWorkSession = new MinigameSession(work.Minigame, attemptStudent, seed);
            return result;
        }
        public AdultCareerResult TrySettlePendingWork(MinigameSession completed)
        {
            if (completed == null || !ReferenceEquals(completed, ActiveWorkSession) || !completed.IsComplete || string.IsNullOrEmpty(_activeWorkRequest)) return SetResult(AdultCareerResultKind.InvalidRequest);
            if (_activePractice != null || _activePartTimeWork != null) return SettleStudentMinigame(completed);
            if (_activeWork == null) return SetResult(AdultCareerResultKind.InvalidRequest);
            var result = ExecuteSessionCommand((session, service) => completed.IsCancelled
                ? service.TryForfeitReservedWork(session.Student, session.Progress, _activeWorkRequest)
                : service.TrySettleReservedWork(_activeWork, session.Student, session.Progress, session.Inventory, _activeWorkRequest, completed.Score));
            if (result.Applied) { ActiveWorkSession = null; _activeWork = null; _activeWorkRequest = null; }
            return result;
        }
        public AdultCareerResult TryBeginPractice(CareerPracticeDefinition practice, string requestId, int seed)
        {
            var result = ExecuteSessionCommand((session, service) => _studentMinigames.TryBeginPractice(practice, session.Student, session.Progress, requestId, seed));
            if (!result.Applied) return result;
            _activePractice = practice; _activePartTimeWork = null; _activeWork = null; _activeWorkRequest = requestId;
            StartStudentSession(practice.Minigame, seed); return result;
        }
        public AdultCareerResult TryBeginPartTimeWork(PartTimeWorkDefinition work, string requestId, int seed)
        {
            var result = ExecuteSessionCommand((session, service) => _studentMinigames.TryBeginPartTimeWork(work, session.Student, session.Progress, session.Inventory, requestId, seed));
            if (!result.Applied) return result;
            _activePartTimeWork = work; _activePractice = null; _activeWork = null; _activeWorkRequest = requestId;
            StartStudentSession(work.Minigame, seed); return result;
        }
        private void StartStudentSession(MinigameDefinition minigame, int seed)
        {
            var attemptStudent = StudentLifeProgress.FromSaveData(StudentComponent.Progress.ToSaveData(), null, null, null);
            attemptStudent.ConfigureCalendar(_registry.StudentLifeCalendar);
            ActiveWorkSession = new MinigameSession(minigame, attemptStudent, seed);
        }
        private AdultCareerResult SettleStudentMinigame(MinigameSession completed)
        {
            bool succeeded = completed.Succeeded;
            bool recordsWork = succeeded && _activePartTimeWork != null;
            var result = ExecuteSessionCommand((session, service) =>
            {
                var settled = _activePractice != null
                    ? _studentMinigames.TrySettlePractice(_activePractice, session.Student, _activeWorkRequest, succeeded)
                    : _studentMinigames.TrySettlePartTimeWork(_activePartTimeWork, session.Student, session.Inventory, _activeWorkRequest, succeeded);
                if (settled.Applied && succeeded)
                {
                    var diaryRunner = new DailyPlanRouteRunner();
                    if (_activePractice != null)
                        diaryRunner.TryRecordPractice(session.Student.DailyPlan, _registry.DailyPlanRouteSets,
                            _activePractice, session.Student, session.Student.CurrentDay, _activeWorkRequest, out _);
                    else if (_activePartTimeWork != null)
                        diaryRunner.TryRecordPartTimeWork(session.Student.DailyPlan, _registry.DailyPlanRouteSets,
                            _activePartTimeWork, session.Student, session.Student.CurrentDay, _activeWorkRequest, out _);
                }
                if (settled.Applied && recordsWork && !QuestChainRuntimeService.ApplyRegisteredEventToSession(_registry, session, new QuestEvent(QuestEventKind.PartTimeWork, _activeWorkRequest, work: _activePartTimeWork)))
                    return new AdultCareerResult(AdultCareerResultKind.InvalidRequest);
                return settled;
            });
            if (result.Applied)
            {
                ActiveWorkSession = null; _activeWork = null; _activePractice = null; _activePartTimeWork = null; _activeWorkRequest = null;
                if (recordsWork)
                {
                    var questService = FindFirstObjectByType<QuestChainRuntimeService>();
                    if (questService != null) questService.NotifyCommitted(this);
                }
            }
            return result;
        }
        public AdultCareerResult TryUpgrade(FacilityDefinition facility, string requestId)
        {
            return ExecuteSessionCommand((session, service) => service.TryUpgrade(facility, session.Student, session.Progress, session.Inventory, session.World, requestId));
        }
        // Runs inside the session so the money, the unlock and the save succeed or fail as one.
        public FurniturePurchaseResult TryPurchaseFurniture(FurnitureDefinition furniture)
        {
            if (furniture == null || _registry == null || _registry.FurnitureDefinitions == null || Array.IndexOf(_registry.FurnitureDefinitions, furniture) < 0) return FurniturePurchaseResult.NotForSale;
            var purchase = FurniturePurchaseResult.Unavailable;
            var result = ExecuteSessionCommand((session, service) =>
            {
                purchase = FurniturePurchase.Try(furniture, session.Inventory, session.FurnitureUnlocks);
                return new AdultCareerResult(purchase == FurniturePurchaseResult.Purchased ? AdultCareerResultKind.Applied : AdultCareerResultKind.InsufficientResources);
            });
            return result.Applied ? FurniturePurchaseResult.Purchased : purchase == FurniturePurchaseResult.Purchased ? FurniturePurchaseResult.Unavailable : purchase;
        }

        public AdultCareerResult ExecuteSessionCommand(Func<AdultCareerSession, AdultCareerService, AdultCareerResult> command)
        {
            if (_publishing) return SetResult(AdultCareerResultKind.InvalidRequest);
            if (RecoveryRequired) return SetResult(AdultCareerResultKind.RecoveryRequired);
            if (!CanMutate) return SetResult(AdultCareerResultKind.AuthorityDenied);
            var snapshot = CaptureCurrent(null, null, null);
            if (snapshot == null) return SetResult(AdultCareerResultKind.InvalidRequest);
            LastResult = _coordinator.Execute(snapshot, (session, service) =>
            {
                if (command == null) return new AdultCareerResult(AdultCareerResultKind.InvalidRequest);
                var result = command(session, service);
                var pending = session.Progress.PendingWork;
                if (result.Applied && pending != null && (session.Student.CurrentDay != pending.Day || session.Student.DayState != StudentDayState.InProgress || session.Student.TimePointsSpentToday < pending.ReservedTimePoints))
                    return new AdultCareerResult(AdultCareerResultKind.InvalidRequest);
                var studentPending = session.Student.PendingMinigame;
                if (result.Applied && studentPending != null && (session.Student.CurrentDay != studentPending.Day || session.Student.DayState != StudentDayState.InProgress || session.Student.TimePointsSpentToday < studentPending.ReservedTimePoints))
                    return new AdultCareerResult(AdultCareerResultKind.InvalidRequest);
                return result;
            }, Publish);
            return LastResult;
        }

        public bool CanTransition(FacilityDefinition facility)
        {
            if (!CanMutate || Progress.IsAdult || StudentComponent.Progress.HasPendingMinigame || !IsRegistered(facility) || _candidates == null) return false;
            var context = new FacilityTransitionContext(facility, StudentComponent.Progress, _candidates.EnsureProgress());
            return facility.CanTransition(in context);
        }
        public bool CanWork(FacilityWorkDefinition work)
        {
            return CanMutate && Progress.IsAdult && !Progress.HasPendingWork && work != null && work.IsValid && IsRegistered(work.Facility)
                && work.Facility.ContainsWork(work) && Progress.FacilityId == work.Facility.Id
                && Progress.FacilityLevel >= work.MinimumFacilityLevel && StudentComponent.Progress.CanSpendTimePoints(work.TimePointCost);
        }
        public bool CanAutoSkip(FacilityWorkDefinition work)
        {
            if (!CanWork(work)) return false;
            var context = new AutoSkipContext(work, Progress, PlayerInventory.Inventory);
            return work.CanAutoSkip(in context);
        }
        public bool CanUpgrade(FacilityDefinition facility)
        {
            if (!CanMutate || !Progress.IsAdult || Progress.HasPendingWork || !IsRegistered(facility) || Progress.FacilityId != facility.Id || Progress.FacilityLevel >= 3) return false;
            var next = facility.GetLevel(Progress.FacilityLevel + 1);
            return next != null && next.IsValid && PlayerInventory.Inventory.CanExchange(next.BuildCosts(), null);
        }

        // Preserve this exact owner while destination scene objects are created.
        public bool TryBeginSceneTransfer()
        {
            if (!CanMutate || Progress == null || Progress.HasPendingWork || StudentComponent.Progress.HasPendingMinigame || ActiveWorkSession != null) return false;
            if (!SaveCurrent() || !AdultCareerPersistenceRouter.TryPinOwner(_slot, _player, this)) return false;
            SceneTransferPending = true;
            return true;
        }
        public bool TryCompleteSceneTransfer()
        {
            if (!SceneTransferPending || !HasBoundIdentity || !IsOfflineAuthority()
                || !AdultCareerPersistenceRouter.IsOwner(_slot, _player, this)) return false;
            if (!AdultCareerPersistenceRouter.ReleaseOwnerPin(_slot, _player, this)) return false;
            SceneTransferPending = false;
            return CanMutate;
        }
        public void CancelSceneTransfer()
        {
            AdultCareerPersistenceRouter.ReleaseOwnerPin(_slot, _player, this);
            SceneTransferPending = false;
        }

        public bool SaveCurrent() => PersistOverrides(null, null, null);

        private bool PersistOverrides(StudentLifeProgress student, WorldStateProgress world, FurnitureUnlockProgress furniture)
        {
            if (_publishing) return true;
            if (!CanMutate) return false;
            // A previously retained student model is not a mergeable delta. Reject it
            // instead of allowing it to replace newer time points/stats in the owner.
            if (student != null && !ReferenceEquals(student, StudentComponent.Progress)) return false;
            WorldStateProgress mergedWorld = World;
            FurnitureUnlockProgress mergedFurniture = FurnitureUnlocks;
            if (world != null && !ReferenceEquals(world, World))
            {
                mergedWorld = WorldStateProgress.FromSaveData(World.ToSaveData(), _slot, _player);
                if (!mergedWorld.MergeFromSaveData(world.ToSaveData())) return false;
            }
            if (furniture != null && !ReferenceEquals(furniture, FurnitureUnlocks))
            {
                mergedFurniture = FurnitureUnlockProgress.FromSaveData(FurnitureUnlocks.ToSaveData(), _slot, _player);
                if (!mergedFurniture.MergeFromSaveData(furniture.ToSaveData())) return false;
            }
            var snapshot = CaptureCurrent(student, mergedWorld, mergedFurniture);
            bool saved = snapshot != null && AdultCareerSnapshotPersistence.SaveSession(_storage, snapshot);
            if (!saved) { RecoveryRequired = true; return false; }
            if (!ReferenceEquals(mergedWorld, World)) World.RestoreFromSaveData(mergedWorld.ToSaveData());
            if (!ReferenceEquals(mergedFurniture, FurnitureUnlocks)) FurnitureUnlocks.RestoreFromSaveData(mergedFurniture.ToSaveData());
            return true;
        }

        private AdultCareerSnapshot CaptureCurrent(StudentLifeProgress student, WorldStateProgress world, FurnitureUnlockProgress furniture)
        {
            if (Progress == null || QuestChains == null || PlayerInventory == null || StudentComponent == null) return null;
            var sourceStudent = student ?? StudentComponent.Progress;
            sourceStudent?.ConfigureCalendar(_registry.StudentLifeCalendar);
            var snapshot = AdultCareerSnapshotPersistence.Capture(Progress, sourceStudent, PlayerInventory.Inventory, world ?? World);
            if (snapshot == null || (furniture ?? FurnitureUnlocks) == null) return null;
            snapshot.Inventory = PlayerInventory.ToSaveData();
            snapshot.QuestChains = QuestChains.ToSaveData();
            snapshot.FurnitureUnlocks = (furniture ?? FurnitureUnlocks).ToSaveData();
            return snapshot;
        }

        private void Publish(AdultCareerSession session)
        {
            var costs = Grants(PlayerInventory.Inventory);
            var rewards = Grants(session.Inventory);
            _publishing = true;
            try
            {
                bool applied = PlayerInventory.Inventory.TryExchange(costs, rewards, () =>
                {
                    Progress = session.Progress;
                    if (World == null) World = session.World;
                    else World.RestoreFromSaveData(session.World.ToSaveData());
                    if (FurnitureUnlocks == null) FurnitureUnlocks = session.FurnitureUnlocks;
                    else FurnitureUnlocks.RestoreFromSaveData(session.FurnitureUnlocks.ToSaveData());
                    StudentComponent.RestoreFromSaveData(session.Student.ToSaveData(), _registry.StudentLifeTraits, _registry.StudentLifeSkills, _registry.Careers);
                    StudentComponent.Progress.ConfigureCalendar(_registry.StudentLifeCalendar);
                    _candidates.BindProgress(StudentComponent.Progress.CareerCandidates);
                    QuestChains.LoadFromSaveData(session.QuestChains); ScopeQuestLog();
                    PlayerInventory.EquipTool(PlayerInventory.FindById(session.EquippedToolItemId));
                });
                if (!applied) { RecoveryRequired = true; throw new InvalidOperationException("Committed player snapshot could not be published to its bound inventory."); }
            }
            // The save already succeeded, so live state may now differ from it. Stop further saves; a reload restores the saved state.
            catch { RecoveryRequired = true; throw; }
            finally { _publishing = false; }
            Committed?.Invoke();
        }

        private QuestChainLog NewScopedQuestLog()
        {
            var log = new QuestChainLog(_registry.QuestChains);
            var data = log.ToSaveData();
            for (int i = 0; i < data.Chains.Length; i++) { data.Chains[i].SaveSlot = _slot; data.Chains[i].PlayerId = _player; }
            log.LoadFromSaveData(data); return log;
        }
        private void ScopeQuestLog()
        {
            var data = QuestChains.ToSaveData();
            for (int i = 0; i < data.Chains.Length; i++) { data.Chains[i].SaveSlot = _slot; data.Chains[i].PlayerId = _player; }
            QuestChains.LoadFromSaveData(data);
        }
        private static InventoryGrant[] Grants(Inventory inventory)
        {
            var result = new List<InventoryGrant>(inventory.Slots.Count);
            for (int i = 0; i < inventory.Slots.Count; i++) { var slot = inventory.Slots[i]; if (slot.Item != null && slot.Count > 0) result.Add(new InventoryGrant(slot.Item, slot.Count)); }
            return result.ToArray();
        }
        private bool IsRegistered(FacilityDefinition facility)
        {
            if (facility == null || !facility.IsValid || _registry == null) return false;
            var facilities = _registry.FacilityDefinitions;
            for (int i = 0; i < facilities.Length; i++) if (ReferenceEquals(facilities[i], facility)) return true;
            return false;
        }
        private static bool IsOfflineAuthority()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) return false;
            var active = NetworkBootstrap.ActiveSession;
            if (active != null && active.Mode != SessionMode.Single && active.Mode != SessionMode.None) return false;
            var mode = GameBootstrap.Config != null ? GameBootstrap.Config.Mode : SessionMode.None;
            return mode == SessionMode.Single || mode == SessionMode.None;
        }
        private AdultCareerResult SetResult(AdultCareerResultKind kind) { LastResult = new AdultCareerResult(kind); return LastResult; }
        private void RegisterOwner()
        {
            AdultCareerPersistenceRouter.Register(_slot, _player, this, PersistOverrides, () => this != null ? World : null, () => this != null ? FurnitureUnlocks : null, () => this != null && StudentComponent != null ? StudentComponent.Progress : null);
        }
        private void HandleInventoryChanged()
        {
            // Destroyed Unity owners can remain in managed delegates when lifecycle callbacks were skipped.
            if (this == null) { UnsubscribeInventory(); return; }
            // A scene transfer may keep two player objects alive until the old scene unloads.
            if (!AdultCareerPersistenceRouter.IsOwner(_slot, _player, this)) { UnsubscribeInventory(); return; }
            if (_publishing || !_bound || RecoveryRequired || SceneTransferPending || !IsOfflineAuthority()) return;
            if (!SaveCurrent()) Debug.LogWarning("[ROOTBORN] Player inventory snapshot save failed; further player commands require recovery.");
        }
        private void UnsubscribeInventory()
        {
            if (_observedInventory != null) _observedInventory.OnChanged -= HandleInventoryChanged;
            _observedInventory = null;
        }
        private bool LoadLegacyInventory()
        {
            var legacyPlayer = _player.ToCharArray();
            for (int i = 0; i < legacyPlayer.Length; i++)
                if (!char.IsLetterOrDigit(legacyPlayer[i]) && legacyPlayer[i] != '-' && legacyPlayer[i] != '_') legacyPlayer[i] = '_';
            string fileName = _player == PlayerIdentity.DefaultPlayerId ? "inventory.json" : "inventory-" + new string(legacyPlayer) + ".json";
            string json = _storage.ReadJson(fileName);
            if (string.IsNullOrEmpty(json)) return true;
            PlayerInventorySaveData data;
            try { data = JsonUtility.FromJson<PlayerInventorySaveData>(json); }
            catch (ArgumentException) { return false; }
            if (data?.Items == null) return false;
            var grants = new List<InventoryGrant>(data.Items.Length);
            for (int i = 0; i < data.Items.Length; i++)
            {
                var saved = data.Items[i];
                var item = PlayerInventory.FindById(saved.ItemId);
                if (item == null || saved.Count <= 0) return false;
                grants.Add(new InventoryGrant(item, saved.Count));
            }
            var equipped = PlayerInventory.FindById(data.EquippedToolItemId);
            if (!string.IsNullOrEmpty(data.EquippedToolItemId) && equipped == null) return false;
            _publishing = true;
            try { return PlayerInventory.Inventory.TryExchange(Grants(PlayerInventory.Inventory), grants, () => PlayerInventory.EquipTool(equipped)); }
            finally { _publishing = false; }
        }
        private void OnDestroy()
        {
            UnsubscribeInventory();
            AdultCareerPersistenceRouter.Unregister(this);
        }
    }
}
