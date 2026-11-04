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
using Rootborn.Game.Quests;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.Network.Session;
using Rootborn.UI.StudentLife;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Careers
{
    public sealed class StudentMinigameReservationTests
    {
        private readonly List<UnityEngine.Object> _objects = new List<UnityEngine.Object>();
        private string _root; private AppConfig _config; private SaveSlotMetadata _metadata; private INetworkSession _network; private GameDataRegistry _priorRegistry;
        [SetUp] public void Setup()
        {
            _root = Path.Combine(Path.GetTempPath(), "rootborn-student-minigame-" + Guid.NewGuid().ToString("N")); _config = GameBootstrap.Config; _metadata = ActiveSaveContext.Metadata; _network = NetworkBootstrap.ActiveSession; GameDataRegistryProvider.TryGet(out _priorRegistry);
            typeof(NetworkBootstrap).GetProperty("ActiveSession").SetValue(null, null); typeof(GameBootstrap).GetProperty("Config").SetValue(null, new AppConfig { Mode = SessionMode.Single, SaveSlot = "slot" }); SaveService.SetRootDirectoryForTests(_root); ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = "slot" });
        }
        [TearDown] public void Cleanup()
        {
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) UnityEngine.Object.DestroyImmediate(_objects[i]); _objects.Clear();
            GameDataRegistryProvider.SetOverrideForTests(_priorRegistry); ActiveSaveContext.Set(_metadata); SaveService.SetRootDirectoryForTests(null); typeof(GameBootstrap).GetProperty("Config").SetValue(null, _config); typeof(NetworkBootstrap).GetProperty("ActiveSession").SetValue(null, _network); if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }
        [Test] public void STUDENT_GAME_001_PracticeBeginReservesAllCostsBeforeGrowth()
        {
            var f = Fixture(); Assert.That(Practice(f, "practice-1").Applied, Is.True); Assert.That(f.Student.Progress.TimePointsRemaining, Is.EqualTo(2)); Assert.That(f.Student.Progress.Energy, Is.EqualTo(11)); Assert.That(f.Student.Progress.GetTraitValue(f.Trait), Is.Zero);
            Assert.That(Session(f), Is.Not.Null); Assert.That(Get<object>(f.Student.Progress, "PendingMinigame"), Is.Not.Null); Assert.That(AdultCareerSnapshotPersistence.Load(f.Storage, PlayerIdentity.DefaultPlayerId).Student.TimePointsSpentToday, Is.EqualTo(1));
        }
        [Test] public void STUDENT_GAME_002_PracticeSuccessCommitsGrowthCandidateUnderstandingAndReceiptTogether()
        {
            var f = Fixture(); Practice(f, "practice-1"); var session = Session(f); session.Choose(0); Assert.That(Settle(f, session).Applied, Is.True);
            Assert.That(f.Student.Progress.GetTraitValue(f.Trait), Is.EqualTo(2)); Assert.That(f.Student.Progress.TimePointsRemaining, Is.EqualTo(2)); Assert.That(f.Student.Progress.Energy, Is.EqualTo(11)); Assert.That(f.Student.Progress.HasAppliedRequest("practice-1"), Is.True);
            Assert.That(Candidates(f).GetUnderstandingScore(f.Candidate), Is.EqualTo(1)); Assert.That(f.Player.GetComponent<CareerCandidateProgressComponent>().Progress, Is.SameAs(Candidates(f)));
            var saved = AdultCareerSnapshotPersistence.Load(f.Storage, PlayerIdentity.DefaultPlayerId); var dto = (CareerCandidateProgressSaveData)saved.Student.GetType().GetField("CareerCandidates").GetValue(saved.Student);
            Assert.That(CareerCandidateProgress.FromSaveData(dto).GetUnderstandingScore(f.Candidate), Is.EqualTo(1)); Assert.That(Settle(f, session).Applied, Is.False);
        }
        [Test] public void STUDENT_GAME_003_PartTimeSuccessPaysOnceWithGrowthAndDailyWorkLogs()
        {
            var f = Fixture(); Assert.That(Job(f, "job-1").Applied, Is.True); Assert.That(f.Inventory.Inventory.CountOf(f.Coin), Is.Zero); var session = Session(f); session.Choose(0);
            Assert.That(Settle(f, session).Applied, Is.True); Assert.That(f.Inventory.Inventory.CountOf(f.Coin), Is.EqualTo(5)); Assert.That(f.Student.Progress.GetTraitValue(f.Trait), Is.EqualTo(1)); Assert.That(f.Student.Progress.TimePointsRemaining, Is.EqualTo(2));
            Assert.That(f.Student.Progress.GetTodayActivityIds(), Does.Contain(f.Work.Id)); Assert.That(PartTimeWorkDayLog.FromResultLogs(f.Student.Progress.GetTodayResultLogIds()).WorkIds, Does.Contain(f.Work.Id)); Assert.That(Settle(f, session).Applied, Is.False);
        }
        [TestCase(false)] [TestCase(true)] public void STUDENT_GAME_004_FailedOrCancelledGameRetainsCostsWithoutIncomeOrGrowth(bool cancel)
        {
            var f = Fixture(); Job(f, "job-1"); var session = Session(f); if (cancel) session.Cancel(); else session.Choose(1); Assert.That(Settle(f, session).Applied, Is.True);
            Assert.That(f.Student.Progress.TimePointsRemaining, Is.EqualTo(2)); Assert.That(f.Inventory.Inventory.CountOf(f.Coin), Is.Zero); Assert.That(f.Student.Progress.GetTraitValue(f.Trait), Is.Zero); Assert.That(f.Student.Progress.HasAppliedRequest("job-1"), Is.True); Assert.That(Job(f, "job-1").Applied, Is.False);
        }
        [Test] public void STUDENT_GAME_005_FailedPracticeSaveLeavesCandidateAndGrowthUnchangedUntilRetry()
        {
            var f = Fixture(); Practice(f, "practice-1"); var session = Session(f); session.Choose(0); Block(f);
            Assert.That(Settle(f, session).Kind, Is.EqualTo(AdultCareerResultKind.PersistenceFailed)); Assert.That(f.Student.Progress.GetTraitValue(f.Trait), Is.Zero); Assert.That(Candidates(f).GetUnderstandingScore(f.Candidate), Is.Zero);
            Unblock(f); Assert.That(Settle(f, session).Applied, Is.True); Assert.That(Candidates(f).GetUnderstandingScore(f.Candidate), Is.EqualTo(1)); Assert.That(f.Student.Progress.GetTraitValue(f.Trait), Is.EqualTo(2));
        }
        [Test] public void STUDENT_GAME_006_ReloadForfeitsStudentReservationWithoutRefund()
        {
            var f = Fixture(); Practice(f, "practice-1"); var type = f.Owner.GetType(); UnityEngine.Object.DestroyImmediate(f.Owner); f.Owner = f.Player.AddComponent(type); Call(f.Owner, "Bind", f.Registry, f.Storage);
            Assert.That(f.Student.Progress.TimePointsRemaining, Is.EqualTo(2)); Assert.That(f.Student.Progress.GetTraitValue(f.Trait), Is.Zero); Assert.That(Candidates(f).GetUnderstandingScore(f.Candidate), Is.Zero); Assert.That(Session(f), Is.Null); Assert.That(f.Student.Progress.HasAppliedRequest("practice-1"), Is.True);
        }
        [Test] public void STUDENT_GAME_007_OneSharedAttemptBlocksOtherPracticeAndPartTimeWork()
        {
            var f = Fixture(); Practice(f, "practice-1"); Assert.That(Job(f, "job-1").Applied, Is.False); Assert.That(Practice(f, "practice-2").Applied, Is.False); Assert.That(f.Student.Progress.TimePointsRemaining, Is.EqualTo(2));
        }
        [Test] public void STUDENT_GAME_008_FailedBeginCannotSpendOrCreateSession()
        {
            var f = Fixture(); Assert.That((bool)Call(f.Owner, "SaveCurrent"), Is.True); Block(f); Assert.That(Practice(f, "practice-1").Kind, Is.EqualTo(AdultCareerResultKind.PersistenceFailed)); Assert.That(f.Student.Progress.TimePointsRemaining, Is.EqualTo(3)); Assert.That(f.Student.Progress.Energy, Is.EqualTo(12)); Assert.That(Session(f), Is.Null);
        }
        [Test] public void STUDENT_GAME_009_UnregisteredPracticeCannotEnterPaidSession()
        {
            var f = Fixture(); var unregistered = New<CareerPracticeDefinition>(); unregistered.ConfigureForTests("other", "other", f.Practice.Activity, f.Practice.CareerHint, Array.Empty<PracticeStepDefinition>()); Set(unregistered, "_minigame", f.Minigame);
            Assert.That(((AdultCareerResult)Call(f.Owner, "TryBeginPractice", unregistered, "unregistered", 123)).Applied, Is.False); Assert.That(f.Student.Progress.TimePointsRemaining, Is.EqualTo(3));
        }
        [Test] public void STUDENT_GAME_010_CurrentPracticeSourceDoesNotMatchOnlyHistoricalActivity()
        {
            var f = Fixture(); f.Student.Progress.RecordActivityCompleted(f.Practice.Activity.Id, Array.Empty<string>()); var other = New<LifeActivityDefinition>(); other.ConfigureForTests("other-activity", "other", default, 0, 0, 0, 0, null, null);
            var context = new CareerCandidateEvaluationContext(f.Candidate, Candidates(f), f.Student.Progress, "new-attempt", new[] { other.Id });
            Assert.That(f.HintSource.Matches(context), Is.False, "Historical activity must not steal another practice's current hint");
        }
        [Test] public void STUDENT_GAME_011_CandidateLegacyReadCannotOverwriteCommittedUnderstanding()
        {
            var f = Fixture(); Practice(f, "practice-1"); Session(f).Choose(0); Settle(f, Session(f));
            f.Storage.WriteJson("career-candidate-progress.json", JsonUtility.ToJson(new CareerCandidateProgress("slot", PlayerIdentity.DefaultPlayerId).ToSaveData()));
            CareerCandidateProgressPersistence.TryLoad(f.Player.GetComponent<CareerCandidateProgressComponent>());
            Assert.That(f.Player.GetComponent<CareerCandidateProgressComponent>().Progress.GetUnderstandingScore(f.Candidate), Is.EqualTo(1));
        }
        [Test] public void STUDENT_GAME_012_AuthoredPracticeBoardRequestsGameWithoutImmediateGrowth()
        {
            var f = Fixture(); var bridge = typeof(CareerPracticeDefinition).Assembly.GetType("Rootborn.Game.StudentLife.StudentMinigameRequestBridge"); Assert.That(bridge, Is.Not.Null);
            var evt = bridge.GetEvent("PracticeRequested"); Assert.That(evt, Is.Not.Null); int requested = 0; Func<GameObject, CareerPracticeDefinition, string, bool> handler = (player, practice, request) => { requested++; return true; }; evt.AddEventHandler(null, handler);
            var go = new GameObject("PracticeBoard"); _objects.Add(go); var board = go.AddComponent<CareerPracticeBoard>(); board.Bind(new[] { f.Practice });
            try { Assert.That(board.RunPractice(f.Student, 0, "request-game"), Is.True); } finally { evt.RemoveEventHandler(null, handler); }
            Assert.That(requested, Is.EqualTo(1)); Assert.That(f.Student.Progress.GetTraitValue(f.Trait), Is.Zero); Assert.That(f.Student.Progress.TimePointsRemaining, Is.EqualTo(3));
        }
        [Test] public void STUDENT_GAME_013_AuthoredPartTimeInteractorRequestsGameWithoutImmediateIncome()
        {
            var f = Fixture(); var bridge = typeof(PartTimeWorkDefinition).Assembly.GetType("Rootborn.Game.StudentLife.StudentMinigameRequestBridge"); Assert.That(bridge, Is.Not.Null); var evt = bridge.GetEvent("PartTimeWorkRequested"); Assert.That(evt, Is.Not.Null);
            int requested = 0; Func<GameObject, PartTimeWorkDefinition, string, bool> handler = (player, work, request) => { requested++; return true; }; evt.AddEventHandler(null, handler);
            var go = new GameObject("PartTimeBoard"); _objects.Add(go); var board = go.AddComponent<PartTimeWorkInteractor>(); board.Bind(f.Work);
            try { Assert.That(board.Interact(f.Student, f.Inventory, "request-game"), Is.True); } finally { evt.RemoveEventHandler(null, handler); }
            Assert.That(requested, Is.EqualTo(1)); Assert.That(f.Inventory.Inventory.CountOf(f.Coin), Is.Zero); Assert.That(f.Student.Progress.TimePointsRemaining, Is.EqualTo(3));
        }
        private sealed class Data
        {
            public GameObject Player; public Component Owner; public StudentLifeProgressComponent Student; public PlayerInventory Inventory; public GameDataRegistry Registry; public SaveService Storage;
            public ItemDefinition Coin; public TraitDefinition Trait; public CareerPracticeDefinition Practice; public PartTimeWorkDefinition Work; public MinigameDefinition Minigame; public CareerCandidateDefinition Candidate; public CareerActivityLogHintSource HintSource;
        }
        private Data Fixture()
        {
            var type = typeof(CareerInterestPanel).Assembly.GetType("Rootborn.UI.Careers.AdultCareerRuntimeComponent"); Assert.That(type.GetMethod("TryBeginPractice"), Is.Not.Null, "TryBeginPractice missing");
            var f = new Data(); var calendar = New<LifeCalendarDefinition>(); calendar.ConfigureForTests(3, 3); var policy = New<ActivityTimePolicyDefinition>(); policy.ConfigureForTests("practice-time", 1, 127, false);
            f.Coin = New<ItemDefinition>(); Set(f.Coin, "_id", "item.allowance"); Set(f.Coin, "_maxStack", 9999); f.Trait = New<TraitDefinition>(); f.Trait.ConfigureForTests("trait.fixture", "trait.fixture");
            var effect = New<TraitDeltaActivityEffect>(); effect.ConfigureForTests(f.Trait, 2); var activity = New<LifeActivityDefinition>(); activity.ConfigureForTests("activity.fixture", "activity.fixture", default, 30, 1, 0, 0, null, new LifeActivityEffectBase[] { effect }); Set(activity, "_timePolicy", policy);
            f.Practice = New<CareerPracticeDefinition>(); f.Practice.ConfigureForTests("practice.fixture", "practice.fixture", activity, null, Array.Empty<PracticeStepDefinition>());
            f.Candidate = New<CareerCandidateDefinition>(); f.Candidate.ConfigureForTests("candidate.fixture", "candidate.fixture", "desc", null, null, null, revealedHintThreshold: 1, deepeningHintThreshold: 2, readyHintThreshold: 3);
            f.HintSource = New<CareerActivityLogHintSource>(); f.HintSource.ConfigureForTests(activity); var hint = New<CareerHintDefinition>(); hint.ConfigureForTests("hint.fixture", "hint.fixture", f.Candidate, null, "hint", 1, new CareerHintSourceBase[] { f.HintSource });
            var choice = New<ChoiceChallengeDefinition>(); Set(choice, "_symbols", new[] { "a", "b" }); Set(choice, "_answers", new[] { 0 }); Set(choice, "_prompts", new[] { "prompt" });
            f.Minigame = New<MinigameDefinition>(); Set(f.Minigame, "_id", "minigame.fixture"); Set(f.Minigame, "_challenges", new ChallengeDefinitionBase[] { choice }); Set(f.Practice, "_minigame", f.Minigame);
            var outcome = New<WorkTraitDeltaOutcome>(); outcome.ConfigureForTests(f.Trait, 1); f.Work = New<PartTimeWorkDefinition>(); f.Work.ConfigureForTests("job.fixture", "job.fixture", null, 30, 1, 0, 0, new[] { new InventoryGrant(f.Coin, 5) }, null, new WorkOutcomeBase[] { outcome }); Set(f.Work, "_timePolicy", policy); Set(f.Work, "_minigame", f.Minigame);
            f.Registry = New<GameDataRegistry>(); Set(f.Registry, "_items", new[] { f.Coin }); Set(f.Registry, "_studentLifeCalendar", calendar); Set(f.Registry, "_careerPractices", new[] { f.Practice }); Set(f.Registry, "_partTimeWorks", new[] { f.Work }); Set(f.Registry, "_careerCandidates", new[] { f.Candidate }); Set(f.Registry, "_careerHints", new[] { hint }); Set(f.Registry, "_studentLifeTraits", new[] { f.Trait }); GameDataRegistryProvider.SetOverrideForTests(f.Registry);
            f.Storage = new SaveService("slot", _root); f.Player = new GameObject("StudentGamePlayer"); _objects.Add(f.Player); f.Player.AddComponent<PlayerIdentity>().Configure(PlayerIdentity.DefaultPlayerId); f.Student = f.Player.AddComponent<StudentLifeProgressComponent>(); f.Student.ConfigureForTests("slot", PlayerIdentity.DefaultPlayerId, 12, 10, 0, 480); f.Student.Progress.ConfigureCalendar(calendar); f.Inventory = f.Player.AddComponent<PlayerInventory>(); f.Inventory.Inventory.Clear(); f.Owner = f.Player.AddComponent(type); Call(f.Owner, "Bind", f.Registry, f.Storage); return f;
        }
        private static AdultCareerResult Practice(Data f, string request) => (AdultCareerResult)Call(f.Owner, "TryBeginPractice", f.Practice, request, 123);
        private static AdultCareerResult Job(Data f, string request) => (AdultCareerResult)Call(f.Owner, "TryBeginPartTimeWork", f.Work, request, 123);
        private static AdultCareerResult Settle(Data f, MinigameSession session) => (AdultCareerResult)Call(f.Owner, "TrySettlePendingWork", session);
        private static MinigameSession Session(Data f) => Get<MinigameSession>(f.Owner, "ActiveWorkSession");
        private static CareerCandidateProgress Candidates(Data f) => Get<CareerCandidateProgress>(f.Student.Progress, "CareerCandidates");
        private static void Block(Data f) { Directory.Delete(f.Storage.DirectoryPath, true); File.WriteAllText(f.Storage.DirectoryPath, "blocked"); }
        private static void Unblock(Data f) { File.Delete(f.Storage.DirectoryPath); Directory.CreateDirectory(f.Storage.DirectoryPath); }
        private T New<T>() where T : ScriptableObject { var value = ScriptableObject.CreateInstance<T>(); _objects.Add(value); return value; }
        private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name).Invoke(target, args);
        private static T Get<T>(object target, string name) { var property = target.GetType().GetProperty(name); Assert.That(property, Is.Not.Null, name); return (T)property.GetValue(target); }
        private static void Set(object target, string name, object value) { var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic); Assert.That(field, Is.Not.Null, name); field.SetValue(target, value); }
    }
}
