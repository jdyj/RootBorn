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
    public sealed class MinigamePresentationBridgeTests
    {
        private readonly List<UnityEngine.Object> _objects = new List<UnityEngine.Object>();
        private string _root; private AppConfig _config; private SaveSlotMetadata _metadata; private INetworkSession _network; private GameDataRegistry _priorRegistry;
        [SetUp] public void Setup()
        {
            // Tests in other classes can leave a root Canvas behind; the bridge picks any active root Canvas in the player's scene.
            foreach (var stale in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (stale != null && stale.isRootCanvas) UnityEngine.Object.DestroyImmediate(stale.gameObject);
            _root = Path.Combine(Path.GetTempPath(), "rootborn-student-minigame-" + Guid.NewGuid().ToString("N")); _config = GameBootstrap.Config; _metadata = ActiveSaveContext.Metadata; _network = NetworkBootstrap.ActiveSession; GameDataRegistryProvider.TryGet(out _priorRegistry);
            typeof(NetworkBootstrap).GetProperty("ActiveSession").SetValue(null, null); typeof(GameBootstrap).GetProperty("Config").SetValue(null, new AppConfig { Mode = SessionMode.Single, SaveSlot = "slot" }); SaveService.SetRootDirectoryForTests(_root); ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = "slot" });
        }
        [TearDown] public void Cleanup()
        {
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) UnityEngine.Object.DestroyImmediate(_objects[i]); _objects.Clear();
            var bridge=typeof(CareerInterestPanel).Assembly.GetType("Rootborn.UI.Minigames.MinigamePresentationBridge");bridge?.GetMethod("Uninstall")?.Invoke(null,null);
            GameDataRegistryProvider.SetOverrideForTests(_priorRegistry); ActiveSaveContext.Set(_metadata); SaveService.SetRootDirectoryForTests(null); typeof(GameBootstrap).GetProperty("Config").SetValue(null, _config); typeof(NetworkBootstrap).GetProperty("ActiveSession").SetValue(null, _network); if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }
        private Type BridgeType(){var type=typeof(CareerInterestPanel).Assembly.GetType("Rootborn.UI.Minigames.MinigamePresentationBridge");Assert.That(type,Is.Not.Null,"Missing playable presentation bridge");return type;}
        private void Install()=>BridgeType().GetMethod("Install").Invoke(null,null);
        private Canvas Canvas(){var go=new GameObject("MinigameCanvas",typeof(Canvas));_objects.Add(go);return go.GetComponent<Canvas>();}
        private Rootborn.UI.Minigames.MinigamePanel Panel()=>UnityEngine.Object.FindFirstObjectByType<Rootborn.UI.Minigames.MinigamePanel>(FindObjectsInactive.Include);
        private void Answer(){var panel=Panel();var button=Array.Find(panel.GetComponentsInChildren<UnityEngine.UI.Button>(true),b=>b.name=="Choice_0");Assert.That(button,Is.Not.Null);button.onClick.Invoke();}
        [Test]public void MissingCanvas_RejectsBeforeReservingTime(){var f=Fixture();Install();Assert.That(StudentMinigameRequestBridge.TryRequestPractice(f.Player,f.Practice,"no-canvas"),Is.False);Assert.That(f.Student.Progress.TimePointsRemaining,Is.EqualTo(3));}
        [Test]public void PracticeRequest_OpensGameAndButtonCompletionCommitsGrowth(){var f=Fixture();Canvas();Install();Assert.That(StudentMinigameRequestBridge.TryRequestPractice(f.Player,f.Practice,"play"),Is.True);Assert.That(Panel().IsOpen,Is.True);Assert.That(f.Student.Progress.GetTraitValue(f.Trait),Is.Zero);Assert.That(f.Student.Progress.TimePointsRemaining,Is.EqualTo(2));Answer();Assert.That(f.Student.Progress.GetTraitValue(f.Trait),Is.EqualTo(2));Assert.That(Session(f),Is.Null);}
        [Test]public void WorkRequest_OnlyPaysAfterActualAnswerButton(){var f=Fixture();Canvas();Install();Assert.That(StudentMinigameRequestBridge.TryRequestPartTimeWork(f.Player,f.Work,"job"),Is.True);Assert.That(f.Inventory.Inventory.CountOf(f.Coin),Is.Zero);Answer();Assert.That(f.Inventory.Inventory.CountOf(f.Coin),Is.EqualTo(5));}
        [Test]public void SecondRequestWhileGameOpen_DoesNotConsumeAnotherPoint(){var f=Fixture();Canvas();Install();StudentMinigameRequestBridge.TryRequestPractice(f.Player,f.Practice,"first");Assert.That(StudentMinigameRequestBridge.TryRequestPractice(f.Player,f.Practice,"second"),Is.False);Assert.That(f.Student.Progress.TimePointsRemaining,Is.EqualTo(2));}
        [Test]public void ClosingActualPanel_ForfeitsReservationWithoutGrowth(){var f=Fixture();Canvas();Install();StudentMinigameRequestBridge.TryRequestPractice(f.Player,f.Practice,"close");Panel().Close();Assert.That(f.Student.Progress.GetTraitValue(f.Trait),Is.Zero);Assert.That(f.Student.Progress.TimePointsRemaining,Is.EqualTo(2));Assert.That(Session(f),Is.Null);}
        [Test]public void InstallTwice_DoesNotRegisterDuplicateRequestHandlers(){var f=Fixture();Canvas();Install();Install();StudentMinigameRequestBridge.TryRequestPartTimeWork(f.Player,f.Work,"once");Answer();Assert.That(f.Inventory.Inventory.CountOf(f.Coin),Is.EqualTo(5));Assert.That(f.Student.Progress.TimePointsRemaining,Is.EqualTo(2));}
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
