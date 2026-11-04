using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Bootstrap;
using Rootborn.Game.Careers;
using Rootborn.Game.Common;
using Rootborn.Game.Player;
using Rootborn.Game.Placement;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.Game.WorldState;
using Rootborn.UI.StudentLife;
using Rootborn.Network.Session;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Careers
{
    public sealed class AdultCareerRuntimeTests
    {
        private readonly List<UnityEngine.Object> _objects = new List<UnityEngine.Object>();
        private string _root; private AppConfig _config; private SaveSlotMetadata _metadata; private INetworkSession _session;
        [SetUp] public void Setup()
        {
            _root = Path.Combine(Path.GetTempPath(), "rootborn-adult-runtime-" + Guid.NewGuid().ToString("N"));
            _config = GameBootstrap.Config; _metadata = ActiveSaveContext.Metadata; _session = NetworkBootstrap.ActiveSession;
            typeof(NetworkBootstrap).GetProperty("ActiveSession").SetValue(null, null);
            typeof(GameBootstrap).GetProperty("Config").SetValue(null, new AppConfig { Mode = SessionMode.Single, SaveSlot = "slot" });
            SaveService.SetRootDirectoryForTests(_root); ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = "slot" });
        }
        [TearDown] public void Cleanup()
        {
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) UnityEngine.Object.DestroyImmediate(_objects[i]);
            _objects.Clear(); ActiveSaveContext.Set(_metadata); SaveService.SetRootDirectoryForTests(null); typeof(GameBootstrap).GetProperty("Config").SetValue(null, _config); typeof(NetworkBootstrap).GetProperty("ActiveSession").SetValue(null, _session);
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test] public void ADULT_RUNTIME_001_BindRestoresOneSnapshotIntoExistingPlayerComponents()
        {
            var f = Fixture(); SaveSnapshot(f); var inventory = f.PlayerInventory.Inventory; Bind(f);
            Assert.That(f.Player.GetComponent<StudentLifeProgressComponent>(), Is.SameAs(f.Student));
            Assert.That(f.Player.GetComponent<PlayerInventory>(), Is.SameAs(f.PlayerInventory)); Assert.That(f.PlayerInventory.Inventory, Is.SameAs(inventory));
            Assert.That(f.Student.Progress.TimePointsRemaining, Is.EqualTo(1)); Assert.That(inventory.CountOf(f.Coin), Is.EqualTo(17));
            Assert.That(Get<WorldStateProgress>(f.Runtime, "World").IsActive(f.Flag), Is.True);
            Assert.That(f.PlayerInventory.EquippedToolItem, Is.SameAs(f.Coin)); Assert.That(Get<bool>(f.Runtime, "CanMutate"), Is.True);
        }

        [TestCase(SessionMode.Client)] [TestCase(SessionMode.Host)] [TestCase(SessionMode.Server)]
        public void ADULT_RUNTIME_002_ActualApplicationNetworkModeDeniesLocalRewards(SessionMode mode)
        {
            var f = Fixture(); SaveSnapshot(f); Bind(f); GameBootstrap.Config.Mode = mode;
            Assert.That(Get<bool>(f.Runtime, "CanMutate"), Is.False);
            Assert.That(Work(f, "network-rejected").Kind.ToString(), Is.EqualTo("AuthorityDenied")); Assert.That(f.PlayerInventory.Inventory.CountOf(f.Coin), Is.EqualTo(17));
        }

        [Test] public void ADULT_RUNTIME_003_CorruptCommittedSnapshotBlocksWithoutApplyingLegacyParts()
        {
            var f = Fixture(); f.PlayerInventory.Inventory.Add(f.Coin, 3);
            File.WriteAllText(Path.Combine(f.Storage.DirectoryPath, AdultCareerSnapshotPersistence.FileNameFor(PlayerIdentity.DefaultPlayerId)), "{ broken");
            Bind(f); Assert.That(Get<bool>(f.Runtime, "RecoveryRequired"), Is.True); Assert.That(Get<bool>(f.Runtime, "CanMutate"), Is.False);
            Assert.That(f.PlayerInventory.Inventory.CountOf(f.Coin), Is.EqualTo(3)); Assert.That(f.Student.Progress.TimePointsRemaining, Is.EqualTo(2));
        }

        [Test] public void ADULT_RUNTIME_004_FailedDurableWorkLeavesLiveTimeInventoryAndMasteryUnchanged()
        {
            var f = Fixture(); SaveSnapshot(f); Bind(f);
            Directory.Delete(f.Storage.DirectoryPath, true); File.WriteAllText(f.Storage.DirectoryPath, "blocked");
            Assert.That(Work(f, "failed-write").Kind.ToString(), Is.EqualTo("PersistenceFailed"));
            Assert.That(f.Student.Progress.TimePointsRemaining, Is.EqualTo(1)); Assert.That(f.PlayerInventory.Inventory.CountOf(f.Coin), Is.EqualTo(17));
            Assert.That(Get<AdultCareerProgress>(f.Runtime, "Progress").GetMastery(f.Work), Is.Zero);
        }

        [Test] public void ADULT_RUNTIME_005_InventoryObserverSeesAllCommittedDomainsAndDiskOnce()
        {
            var f = Fixture(); SaveSnapshot(f); Bind(f); int changed = 0;
            f.PlayerInventory.Inventory.OnChanged += () =>
            {
                changed++; Assert.That(f.Student.Progress.TimePointsRemaining, Is.Zero); Assert.That(Get<AdultCareerProgress>(f.Runtime, "Progress").GetMastery(f.Work), Is.EqualTo(1));
                Assert.That(f.PlayerInventory.EquippedToolItem, Is.SameAs(f.Coin)); Assert.That(AdultCareerSnapshotPersistence.Load(f.Storage, PlayerIdentity.DefaultPlayerId).Inventory.Items[0].Count, Is.EqualTo(19));
            };
            Assert.That(Work(f, "paid-work").Applied, Is.True); Assert.That(changed, Is.EqualTo(1));
            Assert.That(Work(f, "paid-work").Kind, Is.EqualTo(AdultCareerResultKind.DuplicateRequest));
        }

        [Test] public void ADULT_RUNTIME_006_StudentLegacyLoaderPrefersAuthoritativeSnapshot()
        {
            var f = Fixture(); f.Storage.WriteJson("student-life-progress.json", JsonUtility.ToJson(new StudentLifeProgress("slot", PlayerIdentity.DefaultPlayerId, 1, 1).ToSaveData())); SaveSnapshot(f);
            Assert.That(StudentLifeProgressPersistence.TryLoad(f.Student, null, null, null), Is.True);
            Assert.That(f.Student.Progress.Energy, Is.EqualTo(12)); Assert.That(f.Student.Progress.ToSaveData().TimePointsSpentToday, Is.EqualTo(1));
        }

        [Test] public void ADULT_RUNTIME_007_WorldLegacyLoaderPrefersAuthoritativeSnapshot()
        {
            var f = Fixture(); WorldStateProgressPersistence.Save(new WorldStateProgress("slot", PlayerIdentity.DefaultPlayerId)); SaveSnapshot(f);
            Assert.That(WorldStateProgressPersistence.LoadOrCreate("slot", PlayerIdentity.DefaultPlayerId).IsActive(f.Flag), Is.True);
        }

        [Test] public void ADULT_RUNTIME_008_OngoingStudentSaveUpdatesAuthoritativeBundle()
        {
            var f = Fixture(); SaveSnapshot(f); Bind(f); f.Student.Progress.TrySpendTimePoints(1); StudentLifeProgressPersistence.Save(f.Student);
            Assert.That(AdultCareerSnapshotPersistence.Load(f.Storage, PlayerIdentity.DefaultPlayerId).Student.TimePointsSpentToday, Is.EqualTo(2));
        }

        [Test] public void ADULT_RUNTIME_009_OngoingWorldSaveUpdatesAuthoritativeBundle()
        {
            var f = Fixture(); SaveSnapshot(f); Bind(f); var world = WorldStateProgressPersistence.LoadOrCreate("slot", PlayerIdentity.DefaultPlayerId);
            var next = New<WorldStateFlagDefinition>(); Set(next, "_id", "world.next"); world.TryActivate(next, new WorldStateActivationSource("", "", "next", 1)); WorldStateProgressPersistence.Save(world);
            var snapshot = AdultCareerSnapshotPersistence.Load(f.Storage, PlayerIdentity.DefaultPlayerId);
            Assert.That(WorldStateProgress.FromSaveData(snapshot.World, "slot", PlayerIdentity.DefaultPlayerId).IsActive(next), Is.True);
        }

        [Test] public void ADULT_RUNTIME_010_IdentityChangeCannotReuseBoundAdultAuthority()
        {
            var f = Fixture(); SaveSnapshot(f); Bind(f); f.Player.GetComponent<PlayerIdentity>().Configure("other-player");
            Assert.That(Get<bool>(f.Runtime, "CanMutate"), Is.False); Assert.That(Work(f, "wrong-player").Applied, Is.False);
            Assert.That(f.PlayerInventory.Inventory.CountOf(f.Coin), Is.EqualTo(17));
        }

        [Test] public void ADULT_RUNTIME_011_ReentrantInventoryObserverCannotClaimAnotherWork()
        {
            var f = Fixture(); SaveSnapshot(f); Bind(f); AdultCareerResult nested = default;
            f.PlayerInventory.Inventory.OnChanged += () => nested = Work(f, "nested-work");
            Assert.That(Work(f, "outer-work").Applied, Is.True); Assert.That(nested.Applied, Is.False); Assert.That(f.PlayerInventory.Inventory.CountOf(f.Coin), Is.EqualTo(19));
        }

        [Test] public void ADULT_RUNTIME_012_OngoingFurnitureSaveUpdatesAuthoritativeBundle()
        {
            var f = Fixture(); SaveSnapshot(f); Bind(f); var furniture = FurnitureUnlockProgressPersistence.LoadOrCreate("slot", PlayerIdentity.DefaultPlayerId);
            furniture.TryUnlock("furniture.new"); FurnitureUnlockProgressPersistence.Save(furniture);
            var snapshot = AdultCareerSnapshotPersistence.Load(f.Storage, PlayerIdentity.DefaultPlayerId);
            var field = snapshot.GetType().GetField("FurnitureUnlocks"); Assert.That(field, Is.Not.Null, "FurnitureUnlocks missing");
            Assert.That(((FurnitureUnlockProgressSaveData)field.GetValue(snapshot)).UnlockTokens, Does.Contain("furniture.new"));
        }

        [Test] public void ADULT_RUNTIME_013_OrdinaryInventoryPickupUpdatesThePrimarySnapshot()
        {
            var f = Fixture(); SaveSnapshot(f); Bind(f); f.PlayerInventory.Inventory.Add(f.Coin, 4);
            Assert.That(AdultCareerSnapshotPersistence.Load(f.Storage, PlayerIdentity.DefaultPlayerId).Inventory.Items[0].Count, Is.EqualTo(21));
        }

        [Test] public void ADULT_RUNTIME_014_HeldWorldAndFurnitureModelsRemainStableAcrossCommands()
        {
            var f = Fixture(); SaveSnapshot(f); Bind(f); var world = WorldStateProgressPersistence.LoadOrCreate("slot", PlayerIdentity.DefaultPlayerId); var furniture = FurnitureUnlockProgressPersistence.LoadOrCreate("slot", PlayerIdentity.DefaultPlayerId);
            Assert.That(world, Is.SameAs(Get<WorldStateProgress>(f.Runtime, "World"))); Assert.That(furniture, Is.SameAs(Get<FurnitureUnlockProgress>(f.Runtime, "FurnitureUnlocks")));
            Assert.That(Work(f, "keep-models").Applied, Is.True);
            Assert.That(world, Is.SameAs(Get<WorldStateProgress>(f.Runtime, "World"))); Assert.That(furniture, Is.SameAs(Get<FurnitureUnlockProgress>(f.Runtime, "FurnitureUnlocks")));
        }

        [Test] public void ADULT_RUNTIME_015_StaleWorldSaveCannotRemoveNewerActivatedFlags()
        {
            var f = Fixture(); SaveSnapshot(f); Bind(f); var current = Get<WorldStateProgress>(f.Runtime, "World"); var stale = WorldStateProgress.FromSaveData(current.ToSaveData(), "slot", PlayerIdentity.DefaultPlayerId);
            var newer = New<WorldStateFlagDefinition>(); Set(newer, "_id", "world.newer"); current.TryActivate(newer, new WorldStateActivationSource("", "", "newer", 1)); WorldStateProgressPersistence.Save(current);
            var older = New<WorldStateFlagDefinition>(); Set(older, "_id", "world.from-stale"); stale.TryActivate(older, new WorldStateActivationSource("", "", "stale", 1)); WorldStateProgressPersistence.Save(stale);
            Assert.That(current.IsActive(newer), Is.True); Assert.That(current.IsActive(older), Is.True);
            var saved = AdultCareerSnapshotPersistence.Load(f.Storage, PlayerIdentity.DefaultPlayerId); var restored = WorldStateProgress.FromSaveData(saved.World, "slot", PlayerIdentity.DefaultPlayerId);
            Assert.That(restored.IsActive(newer), Is.True); Assert.That(restored.IsActive(older), Is.True);
        }

        [Test] public void ADULT_RUNTIME_016_StaleFurnitureSaveMergesUnlocksWithoutReplacingLiveModel()
        {
            var f = Fixture(); SaveSnapshot(f); Bind(f); var current = Get<FurnitureUnlockProgress>(f.Runtime, "FurnitureUnlocks"); var stale = FurnitureUnlockProgress.FromSaveData(current.ToSaveData(), "slot", PlayerIdentity.DefaultPlayerId);
            current.TryUnlock("newer"); FurnitureUnlockProgressPersistence.Save(current); stale.TryUnlock("from-stale"); FurnitureUnlockProgressPersistence.Save(stale);
            Assert.That(Get<FurnitureUnlockProgress>(f.Runtime, "FurnitureUnlocks"), Is.SameAs(current)); Assert.That(current.IsUnlocked("newer"), Is.True); Assert.That(current.IsUnlocked("from-stale"), Is.True);
        }

        [Test] public void ADULT_RUNTIME_017_RouterIdentityKeysCannotAliasAcrossDelimiterCharacters()
        {
            var type = typeof(AdultCareerProgress).Assembly.GetType("Rootborn.Game.Careers.AdultCareerPersistenceRouter"); Assert.That(type, Is.Not.Null, "AdultCareerPersistenceRouter missing");
            object first = new object(), second = new object(); int firstCalls = 0, secondCalls = 0;
            Func<StudentLifeProgress, WorldStateProgress, FurnitureUnlockProgress, bool> firstSave = (s, w, f) => { firstCalls++; return true; };
            Func<StudentLifeProgress, WorldStateProgress, FurnitureUnlockProgress, bool> secondSave = (s, w, f) => { secondCalls++; return true; };
            var register = type.GetMethod("Register");
            try
            {
                var one = new object[register.GetParameters().Length]; one[0] = "a|b"; one[1] = "c"; one[2] = first; one[3] = firstSave;
                var two = new object[register.GetParameters().Length]; two[0] = "a"; two[1] = "b|c"; two[2] = second; two[3] = secondSave;
                register.Invoke(null, one); register.Invoke(null, two);
                type.GetMethod("TrySaveStudent").Invoke(null, new object[] { new StudentLifeProgress("a|b", "c", 1, 1), false });
                Assert.That(firstCalls, Is.EqualTo(1)); Assert.That(secondCalls, Is.Zero);
            }
            finally { type.GetMethod("Unregister").Invoke(null, new[] { first }); type.GetMethod("Unregister").Invoke(null, new[] { second }); }
        }

        [Test] public void ADULT_RUNTIME_018_LegacyQuestBinderCannotReplacePrimaryInventoryWithStaleCache()
        {
            var f = Fixture(); f.Storage.WriteJson("inventory.json", JsonUtility.ToJson(new PlayerInventorySaveData { Items = new[] { new PlayerInventoryItemSaveData { ItemId = f.Coin.Id, Count = 1 } } })); SaveSnapshot(f); Bind(f);
            var binder = typeof(CareerInterestPanel).Assembly.GetType("Rootborn.UI.Quests.QuestPlayflowRuntimeBinder"); Assert.That(binder, Is.Not.Null);
            binder.GetMethod("LoadInventory", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { f.PlayerInventory, PlayerIdentity.DefaultPlayerId });
            Assert.That(f.PlayerInventory.Inventory.CountOf(f.Coin), Is.EqualTo(17)); Assert.That(AdultCareerSnapshotPersistence.Load(f.Storage, PlayerIdentity.DefaultPlayerId).Inventory.Items[0].Count, Is.EqualTo(17));
        }

        [Test] public void ADULT_RUNTIME_019_NewLiveActorTakesExclusivePersistenceOwnershipForSameIdentity()
        {
            var f = Fixture(); SaveSnapshot(f); Bind(f); var original = f.Runtime;
            var other = new GameObject("OverlappingPortalPlayer"); _objects.Add(other);
            other.AddComponent<PlayerIdentity>().Configure(PlayerIdentity.DefaultPlayerId);
            var nextStudent = other.AddComponent<StudentLifeProgressComponent>(); nextStudent.ConfigureForTests("slot", PlayerIdentity.DefaultPlayerId, 12, 10, 0, 480); nextStudent.Progress.ConfigureCalendar(f.Calendar);
            other.AddComponent<PlayerInventory>(); var next = other.AddComponent(original.GetType());
            next.GetType().GetMethod("Bind").Invoke(next, new object[] { f.Registry, f.Storage });
            Assert.That(Get<bool>(original, "CanMutate"), Is.False, "Old live actor must lose its persistence lease when another actor owns this identity");
            Assert.That(Get<bool>(next, "CanMutate"), Is.True);
            Assert.That(((AdultCareerResult)next.GetType().GetMethod("TryCompleteWork").Invoke(next, new object[] { f.Work, "portal-work", 100, false })).Applied, Is.True);
            var saved = AdultCareerSnapshotPersistence.Load(f.Storage, PlayerIdentity.DefaultPlayerId);
            Assert.That(saved.Student.TimePointsSpentToday, Is.EqualTo(2)); Assert.That(saved.Adult.Works[0].Mastery, Is.EqualTo(1)); Assert.That(saved.Inventory.Items[0].Count, Is.EqualTo(19));
            Assert.That(Work(f, "old-player-work").Applied, Is.False);
        }

        private sealed class Data
        {
            public GameObject Player; public Component Runtime; public StudentLifeProgressComponent Student; public PlayerInventory PlayerInventory;
            public GameDataRegistry Registry; public SaveService Storage; public LifeCalendarDefinition Calendar; public ItemDefinition Coin;
            public FacilityDefinition Facility; public FacilityWorkDefinition Work; public WorldStateFlagDefinition Flag;
        }
        private Data Fixture()
        {
            var type = typeof(CareerInterestPanel).Assembly.GetType("Rootborn.UI.Careers.AdultCareerRuntimeComponent"); Assert.That(type, Is.Not.Null, "AdultCareerRuntimeComponent missing");
            var f = new Data(); f.Calendar = New<LifeCalendarDefinition>(); f.Calendar.ConfigureForTests(2, 3); f.Coin = New<ItemDefinition>(); Set(f.Coin, "_id", "item.allowance"); Set(f.Coin, "_maxStack", 9999);
            f.Facility = New<FacilityDefinition>(); Set(f.Facility, "_id", "facility.fixture"); Set(f.Facility, "_career", New<CareerDefinition>()); Set(f.Facility, "_candidate", New<CareerCandidateDefinition>());
            var levels = new FacilityLevelDefinition[3]; for (int i = 0; i < 3; i++) { levels[i] = New<FacilityLevelDefinition>(); Set(levels[i], "_id", "level-" + (i + 1)); Set(levels[i], "_level", i + 1); } Set(levels[2], "_completesRoute", true); Set(f.Facility, "_levels", levels);
            f.Work = New<FacilityWorkDefinition>(); Set(f.Work, "_id", "work.fixture"); Set(f.Work, "_facility", f.Facility); Set(f.Work, "_rewardItems", new[] { f.Coin }); Set(f.Work, "_maximumRewardCounts", new[] { 2 }); Set(f.Facility, "_works", new[] { f.Work });
            f.Registry = New<GameDataRegistry>(); Set(f.Registry, "_items", new[] { f.Coin }); Set(f.Registry, "_studentLifeCalendar", f.Calendar); Set(f.Registry, "_facilityDefinitions", new[] { f.Facility });
            f.Flag = New<WorldStateFlagDefinition>(); Set(f.Flag, "_id", "world.loaded"); f.Storage = new SaveService("slot", _root);
            f.Player = new GameObject("AdultFixturePlayer"); _objects.Add(f.Player); f.Player.AddComponent<PlayerIdentity>().Configure(PlayerIdentity.DefaultPlayerId);
            f.Student = f.Player.AddComponent<StudentLifeProgressComponent>(); f.Student.ConfigureForTests("slot", PlayerIdentity.DefaultPlayerId, 12, 10, 0, 480); f.Student.Progress.ConfigureCalendar(f.Calendar);
            f.PlayerInventory = f.Player.AddComponent<PlayerInventory>(); f.Runtime = f.Player.AddComponent(type); return f;
        }
        private void SaveSnapshot(Data f)
        {
            var adult = AdultCareerProgress.FromSaveData(new AdultCareerProgressSaveData { SaveSlot = "slot", PlayerId = PlayerIdentity.DefaultPlayerId, FacilityId = f.Facility.Id, FacilityLevel = 1 });
            var student = new StudentLifeProgress("slot", PlayerIdentity.DefaultPlayerId, 12, 10); student.ConfigureCalendar(f.Calendar); student.TrySpendTimePoints(1);
            var inventory = new Inventory(); inventory.Add(f.Coin, 17); var world = new WorldStateProgress("slot", PlayerIdentity.DefaultPlayerId); world.TryActivate(f.Flag, new WorldStateActivationSource("", "", "saved", 1));
            var snapshot = AdultCareerSnapshotPersistence.Capture(adult, student, inventory, world); snapshot.Inventory.EquippedToolItemId = f.Coin.Id;
            Assert.That(AdultCareerSnapshotPersistence.Save(f.Storage, snapshot), Is.True);
        }
        private static void Bind(Data f) => f.Runtime.GetType().GetMethod("Bind").Invoke(f.Runtime, new object[] { f.Registry, f.Storage });
        private static AdultCareerResult Work(Data f, string request) => (AdultCareerResult)f.Runtime.GetType().GetMethod("TryCompleteWork").Invoke(f.Runtime, new object[] { f.Work, request, 100, false });
        private T New<T>() where T : ScriptableObject { var value = ScriptableObject.CreateInstance<T>(); _objects.Add(value); return value; }
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static T Get<T>(object target, string property) => (T)target.GetType().GetProperty(property).GetValue(target);
    }
}
