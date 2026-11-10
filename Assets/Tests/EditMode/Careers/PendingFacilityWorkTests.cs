using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Bootstrap;
using Rootborn.Game.Careers;
using Rootborn.Game.Common;
using Rootborn.Game.Minigames;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.Game.WorldState;
using Rootborn.Network.Session;
using Rootborn.UI.StudentLife;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Careers
{
    public sealed class PendingFacilityWorkTests
    {
        private readonly List<UnityEngine.Object> _objects = new List<UnityEngine.Object>();
        private string _root; private AppConfig _config; private SaveSlotMetadata _metadata; private INetworkSession _network; private GameDataRegistry _priorRegistry;
        [SetUp] public void Setup()
        {
            _root = Path.Combine(Path.GetTempPath(), "rootborn-pending-work-" + Guid.NewGuid().ToString("N"));
            _config = GameBootstrap.Config; _metadata = ActiveSaveContext.Metadata; _network = NetworkBootstrap.ActiveSession; GameDataRegistryProvider.TryGet(out _priorRegistry);
            typeof(NetworkBootstrap).GetProperty("ActiveSession").SetValue(null, null);
            typeof(GameBootstrap).GetProperty("Config").SetValue(null, new AppConfig { Mode = SessionMode.Single, SaveSlot = "slot" });
            SaveService.SetRootDirectoryForTests(_root); ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = "slot" });
        }
        [TearDown] public void Cleanup()
        {
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) UnityEngine.Object.DestroyImmediate(_objects[i]);
            _objects.Clear(); GameDataRegistryProvider.SetOverrideForTests(_priorRegistry); ActiveSaveContext.Set(_metadata); SaveService.SetRootDirectoryForTests(null);
            typeof(GameBootstrap).GetProperty("Config").SetValue(null, _config); typeof(NetworkBootstrap).GetProperty("ActiveSession").SetValue(null, _network);
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }
        [Test] public void PENDING_WORK_001_BeginDurablyReservesTimeBeforeCreatingPlayableSession()
        {
            var f = Fixture(); Assert.That(Begin(f, "attempt-1").Applied, Is.True);
            Assert.That(f.Student.Progress.TimePointsRemaining, Is.EqualTo(1)); Assert.That(f.Inventory.Inventory.CountOf(f.Coin), Is.EqualTo(10)); Assert.That(Session(f), Is.Not.Null);
            var saved = AdultCareerSnapshotPersistence.Load(f.Storage, PlayerIdentity.DefaultPlayerId); var pending = Pending(saved.Adult);
            Assert.That(Field<string>(pending, "RequestId"), Is.EqualTo("attempt-1")); Assert.That(Field<string>(pending, "WorkId"), Is.EqualTo(f.Work.Id)); Assert.That(Field<int>(pending, "ReservedTimePoints"), Is.EqualTo(1));
            Assert.That(saved.Student.TimePointsSpentToday, Is.EqualTo(1));
        }
        [Test] public void PENDING_WORK_002_SecondBeginCannotReplaceOrSpendAnotherReservation()
        {
            var f = Fixture(); Begin(f, "attempt-1"); var original = Session(f);
            Assert.That(Begin(f, "attempt-2").Applied, Is.False); Assert.That(Begin(f, "attempt-1").Applied, Is.False);
            Assert.That(Session(f), Is.SameAs(original)); Assert.That(f.Student.Progress.TimePointsRemaining, Is.EqualTo(1));
        }
        [Test] public void PENDING_WORK_003_CompletedOwnerSessionSettlesRewardAndReceiptWithoutSecondTimeDebit()
        {
            var f = Fixture(); Begin(f, "attempt-1"); var session = Session(f); session.Choose(0);
            Assert.That(Settle(f, session).Applied, Is.True); Assert.That(f.Inventory.Inventory.CountOf(f.Coin), Is.EqualTo(12)); Assert.That(f.Student.Progress.TimePointsRemaining, Is.EqualTo(1));
            var saved = AdultCareerSnapshotPersistence.Load(f.Storage, PlayerIdentity.DefaultPlayerId);
            Assert.That(Pending(saved.Adult), Is.Null); Assert.That(saved.Adult.AppliedRequestIds, Does.Contain("attempt-1")); Assert.That(saved.Adult.Works[0].SuccessfulCompletions, Is.EqualTo(1));
            Assert.That(Settle(f, session).Applied, Is.False); Assert.That(f.Inventory.Inventory.CountOf(f.Coin), Is.EqualTo(12));
        }
        [Test] public void PENDING_WORK_004_ForgedOrUnfinishedSessionCannotSettleOwnerReservation()
        {
            var f = Fixture(); Begin(f, "attempt-1"); Assert.That(Settle(f, Session(f)).Applied, Is.False);
            var forged = new MinigameSession(f.Minigame, f.Student.Progress, 123); forged.Choose(0);
            Assert.That(Settle(f, forged).Applied, Is.False); Assert.That(f.Inventory.Inventory.CountOf(f.Coin), Is.EqualTo(10)); Assert.That(Session(f).IsComplete, Is.False);
        }
        [Test] public void PENDING_WORK_005_FailedBeginWriteLeavesLiveTimeAndSessionUnchanged()
        {
            var f = Fixture(); BlockWrites(f); Assert.That(Begin(f, "attempt-1").Kind, Is.EqualTo(AdultCareerResultKind.PersistenceFailed));
            Assert.That(f.Student.Progress.TimePointsRemaining, Is.EqualTo(2)); Assert.That(Session(f), Is.Null); Assert.That(f.Inventory.Inventory.CountOf(f.Coin), Is.EqualTo(10));
        }
        [Test] public void PENDING_WORK_006_FailedSettlementRetainsCompletedSessionForExactlyOnceRetry()
        {
            var f = Fixture(); Begin(f, "attempt-1"); var session = Session(f); session.Choose(0); BlockWrites(f);
            Assert.That(Settle(f, session).Kind, Is.EqualTo(AdultCareerResultKind.PersistenceFailed)); Assert.That(Session(f), Is.SameAs(session)); Assert.That(f.Inventory.Inventory.CountOf(f.Coin), Is.EqualTo(10));
            Assert.That(f.Student.Progress.TimePointsRemaining, Is.EqualTo(1)); UnblockWrites(f);
            Assert.That(Settle(f, session).Applied, Is.True); Assert.That(Settle(f, session).Applied, Is.False); Assert.That(f.Inventory.Inventory.CountOf(f.Coin), Is.EqualTo(12));
        }
        [Test] public void PENDING_WORK_007_CancellationForfeitsReservedTimeAndCannotGrantOrReplay()
        {
            var f = Fixture(); Begin(f, "attempt-1"); var session = Session(f); session.Cancel(); Assert.That(Settle(f, session).Applied, Is.True);
            Assert.That(f.Student.Progress.TimePointsRemaining, Is.EqualTo(1)); Assert.That(f.Inventory.Inventory.CountOf(f.Coin), Is.EqualTo(10));
            Assert.That(Get<AdultCareerProgress>(f.Owner, "Progress").GetSuccessfulCompletions(f.Work), Is.Zero); Assert.That(Begin(f, "attempt-1").Applied, Is.False);
        }
        [Test] public void PENDING_WORK_008_ReloadForfeitsUnsettledAttemptWithoutRefundOrReward()
        {
            var f = Fixture(); Begin(f, "attempt-1"); var stale = Session(f); var ownerType = f.Owner.GetType(); UnityEngine.Object.DestroyImmediate(f.Owner);
            f.Owner = f.Player.AddComponent(ownerType); Call(f.Owner, "Bind", f.Registry, f.Storage);
            Assert.That(f.Student.Progress.TimePointsRemaining, Is.EqualTo(1)); Assert.That(f.Inventory.Inventory.CountOf(f.Coin), Is.EqualTo(10)); Assert.That(Session(f), Is.Null);
            var saved = AdultCareerSnapshotPersistence.Load(f.Storage, PlayerIdentity.DefaultPlayerId); Assert.That(Pending(saved.Adult), Is.Null); Assert.That(saved.Adult.AppliedRequestIds, Does.Contain("attempt-1"));
            stale.Choose(0); Assert.That(Settle(f, stale).Applied, Is.False); Assert.That(Begin(f, "attempt-1").Applied, Is.False);
        }
        [Test] public void PENDING_WORK_017_StartingAPlayedShiftAdvancesTheClockOnceAndSavesIt()
        {
            var f = Fixture(); Set(f.Work, "_timeCostMinutes", 90); int start = f.Student.Progress.TimeMinutes;
            Assert.That(Begin(f, "clock").Applied, Is.True);
            Assert.That(f.Student.Progress.TimeMinutes, Is.EqualTo(start + 90), "starting the shift must move the clock with the spent time");
            Assert.That(AdultCareerSnapshotPersistence.Load(f.Storage, PlayerIdentity.DefaultPlayerId).Student.TimeMinutes, Is.EqualTo(start + 90));
            var session = Session(f); session.Choose(0); Assert.That(Settle(f, session).Applied, Is.True);
            Assert.That(f.Student.Progress.TimeMinutes, Is.EqualTo(start + 90), "settling must not move the clock a second time");
        }
        [Test] public void PENDING_WORK_009_MissingAuthoredMinigameRejectsBeforeSpendingTime()
        {
            var f = Fixture(); Set(f.Work, "_minigame", null); Assert.That(Begin(f, "attempt-1").Applied, Is.False);
            Assert.That(f.Student.Progress.TimePointsRemaining, Is.EqualTo(2)); Assert.That(Session(f), Is.Null);
        }
        [Test] public void PENDING_WORK_010_LegacyWorkAndUpgradeCannotBypassActiveReservation()
        {
            var f = Fixture(); Begin(f, "attempt-1");
            Assert.That(((AdultCareerResult)Call(f.Owner, "TryCompleteWork", f.Work, "bypass", 100, false)).Applied, Is.False);
            Assert.That(((AdultCareerResult)Call(f.Owner, "TryUpgrade", f.Facility, "upgrade-bypass")).Applied, Is.False);
            Assert.That(f.Student.Progress.TimePointsRemaining, Is.EqualTo(1)); Assert.That(f.Inventory.Inventory.CountOf(f.Coin), Is.EqualTo(10));
        }
        [Test] public void PENDING_WORK_011_ReentrantObserverCannotBeginOrSettleTwice()
        {
            var f = Fixture(); Begin(f, "attempt-1"); var session = Session(f); session.Choose(0); int nestedApplied = 0;
            f.Inventory.Inventory.OnChanged += () => { if (Settle(f, session).Applied) nestedApplied++; if (Begin(f, "nested").Applied) nestedApplied++; };
            Assert.That(Settle(f, session).Applied, Is.True); Assert.That(nestedApplied, Is.Zero); Assert.That(f.Inventory.Inventory.CountOf(f.Coin), Is.EqualTo(12));
        }
        [Test] public void PENDING_WORK_012_CrossDayGenericCommandCannotEraseReservationTimeCost()
        {
            var f = Fixture(); Begin(f, "attempt-1"); var command = new Func<AdultCareerSession, AdultCareerService, AdultCareerResult>((session, service) =>
            {
                Assert.That(session.Student.TryEndDay(null, out _), Is.True);
                Assert.That(session.Student.TryStartNextDay(), Is.True);
                return new AdultCareerResult(AdultCareerResultKind.Applied);
            });
            var result = (AdultCareerResult)Call(f.Owner, "ExecuteSessionCommand", command);
            Assert.That(result.Applied, Is.False); Assert.That(f.Student.Progress.CurrentDay, Is.EqualTo(1)); Assert.That(f.Student.Progress.TimePointsRemaining, Is.EqualTo(1));
        }
        [Test] public void PENDING_WORK_014_SettledWorkIsIncludedInDailyActivityAndDurableReceipt()
        {
            var f=Fixture(); Begin(f,"daily-work"); var played=Session(f); played.Choose(0);
            Assert.That(Settle(f,played).Applied,Is.True);
            Assert.That(f.Student.Progress.GetTodayActivityIds(),Is.EqualTo(new[]{f.Work.Id}));
            var saved=AdultCareerSnapshotPersistence.Load(f.Storage,PlayerIdentity.DefaultPlayerId);
            Assert.That(saved.Student.TodayActivityIds,Is.EqualTo(new[]{f.Work.Id}));
            Assert.That(Settle(f,played).Applied,Is.False);
            Assert.That(f.Student.Progress.GetTodayActivityIds(),Has.Length.EqualTo(1));
        }
        [Test] public void PENDING_WORK_015_FailedSettlementDoesNotPublishDailyActivityBeforeRetry()
        {
            var f=Fixture(); Begin(f,"daily-retry"); var played=Session(f); played.Choose(0); BlockWrites(f);
            Assert.That(Settle(f,played).Applied,Is.False);
            Assert.That(f.Student.Progress.GetTodayActivityIds(),Is.Empty);
            UnblockWrites(f); Assert.That(Settle(f,played).Applied,Is.True);
            Assert.That(f.Student.Progress.GetTodayActivityIds(),Is.EqualTo(new[]{f.Work.Id}));
        }
        [Test] public void PENDING_WORK_016_ReservationAloneDoesNotCountAsCompletedDailyActivity()
        {
            var f=Fixture(); Begin(f,"daily-reserved");
            Assert.That(f.Student.Progress.GetTodayActivityIds(),Is.Empty);
        }
        private sealed class Data
        {
            public GameObject Player; public Component Owner; public StudentLifeProgressComponent Student; public PlayerInventory Inventory;
            public GameDataRegistry Registry; public SaveService Storage; public ItemDefinition Coin; public FacilityDefinition Facility; public FacilityWorkDefinition Work; public MinigameDefinition Minigame;
        }
        private Data Fixture()
        {
            var type = typeof(CareerInterestPanel).Assembly.GetType("Rootborn.UI.Careers.AdultCareerRuntimeComponent"); Assert.That(type, Is.Not.Null);
            Assert.That(type.GetMethod("TryBeginWork"), Is.Not.Null, "TryBeginWork reservation API missing");
            var f = new Data(); var calendar = New<LifeCalendarDefinition>(); calendar.ConfigureForTests(2, 3); f.Coin = New<ItemDefinition>(); Set(f.Coin, "_id", "item.allowance"); Set(f.Coin, "_maxStack", 9999);
            f.Facility = New<FacilityDefinition>(); Set(f.Facility, "_id", "facility.fixture"); Set(f.Facility, "_career", New<CareerDefinition>()); Set(f.Facility, "_candidate", New<CareerCandidateDefinition>());
            var levels = new FacilityLevelDefinition[3]; for (int i = 0; i < 3; i++) { levels[i] = New<FacilityLevelDefinition>(); Set(levels[i], "_id", "level-" + (i+1)); Set(levels[i], "_level", i+1); } Set(levels[2], "_completesRoute", true); Set(f.Facility, "_levels", levels);
            var choice = New<ChoiceChallengeDefinition>(); Set(choice, "_symbols", new[] { "a", "b" }); Set(choice, "_answers", new[] { 0 }); Set(choice, "_prompts", new[] { "prompt" });
            f.Minigame = New<MinigameDefinition>(); Set(f.Minigame, "_id", "minigame.fixture"); Set(f.Minigame, "_challenges", new ChallengeDefinitionBase[] { choice });
            f.Work = New<FacilityWorkDefinition>(); Set(f.Work, "_id", "work.fixture"); Set(f.Work, "_facility", f.Facility); Set(f.Work, "_minigame", f.Minigame); Set(f.Work, "_rewardItems", new[] { f.Coin }); Set(f.Work, "_maximumRewardCounts", new[] { 2 }); Set(f.Facility, "_works", new[] { f.Work });
            f.Registry = New<GameDataRegistry>(); Set(f.Registry, "_items", new[] { f.Coin }); Set(f.Registry, "_studentLifeCalendar", calendar); Set(f.Registry, "_facilityDefinitions", new[] { f.Facility }); GameDataRegistryProvider.SetOverrideForTests(f.Registry);
            f.Storage = new SaveService("slot", _root); var inventory = new Inventory(); inventory.Add(f.Coin, 10); var student = new StudentLifeProgress("slot", PlayerIdentity.DefaultPlayerId, 12, 10); student.ConfigureCalendar(calendar);
            var adult = AdultCareerProgress.FromSaveData(new AdultCareerProgressSaveData { SaveSlot = "slot", PlayerId = PlayerIdentity.DefaultPlayerId, FacilityId = f.Facility.Id, FacilityLevel = 1 });
            Assert.That(AdultCareerSnapshotPersistence.Save(f.Storage, AdultCareerSnapshotPersistence.Capture(adult, student, inventory, new WorldStateProgress("slot", PlayerIdentity.DefaultPlayerId))), Is.True);
            f.Player = new GameObject("PendingWorkPlayer"); _objects.Add(f.Player); f.Player.AddComponent<PlayerIdentity>().Configure(PlayerIdentity.DefaultPlayerId); f.Student = f.Player.AddComponent<StudentLifeProgressComponent>(); f.Inventory = f.Player.AddComponent<PlayerInventory>(); f.Owner = f.Player.AddComponent(type);
            Call(f.Owner, "Bind", f.Registry, f.Storage); return f;
        }
        private static AdultCareerResult Begin(Data f, string id) => (AdultCareerResult)Call(f.Owner, "TryBeginWork", f.Work, id, 123);
        private static AdultCareerResult Settle(Data f, MinigameSession session) => (AdultCareerResult)Call(f.Owner, "TrySettlePendingWork", session);
        private static MinigameSession Session(Data f) => Get<MinigameSession>(f.Owner, "ActiveWorkSession");
        private static object Pending(AdultCareerProgressSaveData adult) { var field = adult.GetType().GetField("PendingWork"); Assert.That(field, Is.Not.Null); var value = field.GetValue(adult); return value != null && string.IsNullOrEmpty(Field<string>(value, "RequestId")) ? null : value; }
        private static void BlockWrites(Data f) { Directory.Delete(f.Storage.DirectoryPath, true); File.WriteAllText(f.Storage.DirectoryPath, "blocked"); }
        private static void UnblockWrites(Data f) { File.Delete(f.Storage.DirectoryPath); Directory.CreateDirectory(f.Storage.DirectoryPath); }
        private T New<T>() where T : ScriptableObject { var value = ScriptableObject.CreateInstance<T>(); _objects.Add(value); return value; }
        private static object Call(object target, string name, params object[] args) { var method = target.GetType().GetMethod(name); Assert.That(method, Is.Not.Null, name); return method.Invoke(target, args); }
        private static T Get<T>(object target, string name) { var property = target.GetType().GetProperty(name); Assert.That(property, Is.Not.Null, name); return (T)property.GetValue(target); }
        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name).GetValue(target);
        private static void Set(object target, string name, object value) { var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic); Assert.That(field, Is.Not.Null, name); field.SetValue(target, value); }
    }
}

