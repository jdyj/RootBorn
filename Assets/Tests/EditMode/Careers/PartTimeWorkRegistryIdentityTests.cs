using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
using Rootborn.Game.WorldState;
using Rootborn.Network.Session;
using Rootborn.UI.Careers;
using Rootborn.UI.Minigames;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor.SceneManagement;
using UnityEditor;
using System.Text.RegularExpressions;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.Careers
{
    // Scene/bundle identity boundary tests use isolated fixture state, never the live QA actor/save.
    public sealed class PartTimeWorkRegistryIdentityTests
    {
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
        private readonly List<Object> _owned = new List<Object>();
        private readonly Dictionary<FieldInfo, object> _priorStatics = new Dictionary<FieldInfo, object>();
        private readonly List<KeyValuePair<IDictionary, DictionaryEntry[]>> _priorPlayerState = new List<KeyValuePair<IDictionary, DictionaryEntry[]>>();
        private AppConfig _config;
        private SaveSlotMetadata _metadata;
        private INetworkSession _network;
        private string _root, _slot;
        private GameDataRegistry _registry;
        private AdultCareerRuntimeComponent _owner;
        private GameObject _actor;
        private Canvas _canvas;
        private Scene _ownedScene, _hostScene;
        private string _ownedScenePath;
        private ItemDefinition _coin;
        private PartTimeWorkDefinition _canonical;
        private MinigamePanel Panel => _canvas.GetComponentInChildren<MinigamePanel>(true);

        [SetUp]
        public void SetUp()
        {
            _hostScene = SceneManager.GetActiveScene();
            _config = GameBootstrap.Config; _metadata = ActiveSaveContext.Metadata; _network = NetworkBootstrap.ActiveSession;
            SnapshotStatic(typeof(SaveService), "s_rootDirectoryOverride");
            foreach (var name in new[] { "s_provider", "s_overrideRegistry", "s_cachedRegistry" }) SnapshotStatic(typeof(GameDataRegistryProvider), name);
            SnapshotStatic(typeof(StudentMinigameRequestBridge), "PracticeRequested");
            SnapshotStatic(typeof(StudentMinigameRequestBridge), "PartTimeWorkRequested");
            SnapshotStatic(typeof(FacilityManagementPanel), "WorkRequested");
            foreach (var name in new[] { "s_inventoriesByPlayer", "s_questLogsByPlayer" })
            {
                var field = typeof(PlayerGlobalState).GetField(name, StaticPrivate);
                Assert.That(field, Is.Not.Null, name);
                var dictionary = (IDictionary)field.GetValue(null);
                var entries = new List<DictionaryEntry>();
                foreach (DictionaryEntry entry in dictionary) entries.Add(entry);
                _priorPlayerState.Add(new KeyValuePair<IDictionary, DictionaryEntry[]>(dictionary, entries.ToArray()));
            }
            // Load a uniquely owned saved empty scene additively. Unlike NewScene(Additive),
            // OpenScene does not require replacing another fixture's untitled scene.
            _ownedScenePath = "Assets/Temp/JobIdentity-" + Guid.NewGuid().ToString("N") + ".unity";
            Directory.CreateDirectory("Assets/Temp");
            var bootYaml = File.ReadAllText("Assets/Scenes/Boot.unity");
            var blocks = Regex.Split(bootYaml, @"(?m)(?=^--- !u!)");
            var settings = blocks.Where(block => Regex.IsMatch(block, @"^--- !u!(29|104|157|196) "));
            File.WriteAllText(_ownedScenePath, "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n" + string.Concat(settings)
                + "--- !u!1660057539 &9223372036854775807\nSceneRoots:\n  m_ObjectHideFlags: 0\n  m_Roots: []\n");
            AssetDatabase.ImportAsset(_ownedScenePath, ImportAssetOptions.ForceSynchronousImport);
            _ownedScene = EditorSceneManager.OpenScene(_ownedScenePath, OpenSceneMode.Additive);
            Assert.That(_ownedScene.GetRootGameObjects(), Is.Empty);
            if (_hostScene.IsValid() && _hostScene.isLoaded && SceneManager.GetActiveScene() == _ownedScene) SceneManager.SetActiveScene(_hostScene);
            _slot = "job-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            _root = Path.Combine(Path.GetTempPath(), "rootborn-job-identity-" + Guid.NewGuid().ToString("N"));
            typeof(GameBootstrap).GetProperty("Config").SetValue(null, new AppConfig { Mode = SessionMode.Single, SaveSlot = _slot });
            typeof(NetworkBootstrap).GetProperty("ActiveSession").SetValue(null, null);
            SaveService.SetRootDirectoryForTests(_root);
            ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = _slot });
            BuildFixture();
            MinigamePresentationBridge.Install();
        }

        [TearDown]
        public void TearDown()
        {
            if (_canvas != null && Panel != null) Panel.Close();
            MinigamePresentationBridge.Uninstall();
            if (_owner != null) AdultCareerPersistenceRouter.Unregister(_owner);
            for (int i = _owned.Count - 1; i >= 0; i--)
                if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
            _owned.Clear();
            if (_ownedScene.IsValid() && _ownedScene.isLoaded)
            {
                if (SceneManager.GetActiveScene() == _ownedScene && _hostScene.IsValid() && _hostScene.isLoaded) SceneManager.SetActiveScene(_hostScene);
                Assert.That(_ownedScene.path, Is.EqualTo(_ownedScenePath), "Close only this fixture-created scene.");
                Assert.That(EditorSceneManager.CloseScene(_ownedScene, true), Is.True);
            }
            if (!string.IsNullOrEmpty(_ownedScenePath) && File.Exists(_ownedScenePath))
                Assert.That(AssetDatabase.DeleteAsset(_ownedScenePath), Is.True);
            _ownedScenePath = null;
            ActiveSaveContext.Set(_metadata);
            foreach (var pair in _priorPlayerState)
            {
                pair.Key.Clear();
                foreach (var entry in pair.Value) pair.Key.Add(entry.Key, entry.Value);
            }
            _priorPlayerState.Clear();
            foreach (var pair in _priorStatics) pair.Key.SetValue(null, pair.Value);
            _priorStatics.Clear();
            typeof(GameBootstrap).GetProperty("Config").SetValue(null, _config);
            typeof(NetworkBootstrap).GetProperty("ActiveSession").SetValue(null, _network);
            if (!string.IsNullOrEmpty(_root) && Directory.Exists(_root))
            {
                var path = Path.GetFullPath(_root);
                var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                Assert.That(path.StartsWith(temp, StringComparison.OrdinalIgnoreCase)
                    && Path.GetFileName(path).StartsWith("rootborn-job-identity-", StringComparison.Ordinal), Is.True);
                Directory.Delete(path, true);
            }
        }

        [Test]
        public void SameIdSceneCopyUsesOnlyCanonicalCostGameAndRewardWhenRequestedThroughBridge()
        {
            var scene = SceneCopy(_canonical.Id);
            string sceneBefore = JsonUtility.ToJson(scene);
            Assert.That(object.ReferenceEquals(scene, _canonical), Is.False);
            Assert.That(scene.Minigame, Is.Not.SameAs(_canonical.Minigame));
            Assert.That(Request(scene, "scene-copy"), Is.True,
                "The presentation boundary must resolve the single runtime-registry definition before strict domain reservation.");
            AssertCanonicalReservation();
            AnswerCanonicalChoice();
            Assert.That(_owner.StudentComponent.Progress.HasPendingMinigame, Is.False);
            Assert.That(_owner.ActiveWorkSession, Is.Null);
            Assert.That(_owner.PlayerInventory.Inventory.CountOf(_coin), Is.EqualTo(5), "Never trust the scene copy's999-coin reward.");
            Assert.That(_owner.LastResult.Kind, Is.EqualTo(AdultCareerResultKind.Applied));
            Assert.That(JsonUtility.ToJson(scene), Is.EqualTo(sceneBefore), "Resolution must not rewrite the scene definition.");
        }

        [Test]
        public void ExactRegisteredReferenceStillReservesAndSettlesOnlyOnce()
        {
            Assert.That(Request(_canonical, "exact-reference"), Is.True);
            AssertCanonicalReservation();
            AnswerCanonicalChoice();
            var after = OwnedDtos();
            var button = Panel.GetComponentsInChildren<Button>(true).Single(b => b.name == "Choice_0");
            button.onClick.Invoke();
            Panel.Close(); Panel.Close();
            Assert.That(OwnedDtos(), Is.EqualTo(after));
            Assert.That(_owner.PlayerInventory.Inventory.CountOf(_coin), Is.EqualTo(5));
        }

        [Test]
        public void UnknownSceneWorkIdCannotReserveOrMutateAnyOwnedState()
        {
            var before = OwnedDtos();
            Assert.That(Request(SceneCopy("fixture.unregistered-job"), "unknown"), Is.False);
            AssertRejectedUnchanged(before);
        }

        [Test]
        public void DuplicateRegistryIdsAreRejectedEvenWhenTheRequestHasAnExactKnownReference()
        {
            var duplicate = Own(Object.Instantiate(_canonical));
            Set(_registry, "_partTimeWorks", new[] { _canonical, duplicate });
            var before = OwnedDtos();
            Assert.That(Request(_canonical, "duplicate"), Is.False,
                "A duplicate ID is ambiguous; picking the first registered definition would make resource rules enumeration-dependent.");
            AssertRejectedUnchanged(before);
        }

        [Test]
        public void NullWorkAndMissingIdsAreRejectedWithoutReservingResources()
        {
            var before = OwnedDtos();
            Assert.That(Request(null, "null-work"), Is.False);
            AssertRejectedUnchanged(before);
            Assert.That(Request(SceneCopy(null), "null-id"), Is.False);
            AssertRejectedUnchanged(before);
            Assert.That(Request(SceneCopy(string.Empty), "empty-id"), Is.False);
            AssertRejectedUnchanged(before);
        }

        private bool Request(PartTimeWorkDefinition work, string request)
            => StudentMinigameRequestBridge.TryRequestPartTimeWork(_actor, work, request);
        private void AssertCanonicalReservation()
        {
            Assert.That(Panel, Is.Not.Null);
            Assert.That(Panel.IsOpen && Panel.gameObject.activeInHierarchy, Is.True);
            Assert.That(_owner.ActiveWorkSession, Is.SameAs(Panel.Session));
            Assert.That(_owner.ActiveWorkSession.Current.Symbols, Is.EqualTo(new[] { "canonical-a", "canonical-b" }));
            Assert.That(_owner.StudentComponent.Progress.TimePointsRemaining, Is.EqualTo(2));
            Assert.That(_owner.StudentComponent.Progress.Energy, Is.EqualTo(11));
            Assert.That(_owner.StudentComponent.Progress.Focus, Is.EqualTo(9));
            Assert.That(_owner.StudentComponent.Progress.TimeMinutes, Is.EqualTo(510));
            Assert.That(_owner.PlayerInventory.Inventory.CountOf(_coin), Is.Zero, "The reward is paid only after actual fixture answer input.");
        }
        private void AnswerCanonicalChoice()
        {
            var session = Panel.Session;
            Panel.GetComponentsInChildren<Button>(true).Single(b => b.name == "Choice_0").onClick.Invoke();
            Assert.That(session.IsComplete && session.Succeeded, Is.True);
            Assert.That(session.Score, Is.EqualTo(100), "The alternate scene game expects answer1; only the canonical game accepts0.");
        }
        private void AssertRejectedUnchanged(string[] before)
        {
            Assert.That(_owner.StudentComponent.Progress.HasPendingMinigame, Is.False);
            Assert.That(_owner.ActiveWorkSession, Is.Null);
            Assert.That(Panel == null || !Panel.IsOpen, Is.True);
            Assert.That(OwnedDtos(), Is.EqualTo(before));
        }
        private string[] OwnedDtos() => new[] {
            JsonUtility.ToJson(_owner.StudentComponent.Progress.ToSaveData()), JsonUtility.ToJson(_owner.PlayerInventory.ToSaveData()),
            JsonUtility.ToJson(_owner.Progress.ToSaveData()), JsonUtility.ToJson(_owner.World.ToSaveData()), JsonUtility.ToJson(_owner.FurnitureUnlocks.ToSaveData()) };

        private void BuildFixture()
        {
            var calendar = New<LifeCalendarDefinition>(); calendar.ConfigureForTests(3, 3);
            var policy = New<ActivityTimePolicyDefinition>(); policy.ConfigureForTests("fixture.job-time", 1, 127, false);
            _coin = New<ItemDefinition>(); Set(_coin, "_id", "fixture.job-coin"); Set(_coin, "_maxStack", 9999);
            _canonical = New<PartTimeWorkDefinition>();
            _canonical.ConfigureForTests("fixture.job", "fixture.job", null, 30, 1, 1, 0,
                new[] { new InventoryGrant(_coin, 5) }, null, Array.Empty<WorkOutcomeBase>());
            Set(_canonical, "_timePolicy", policy); Set(_canonical, "_minigame", Game("canonical", 0));
            _registry = New<GameDataRegistry>();
            Set(_registry, "_items", new[] { _coin }); Set(_registry, "_studentLifeCalendar", calendar);
            Set(_registry, "_partTimeWorks", new[] { _canonical });
            GameDataRegistryProvider.SetOverrideForTests(_registry);
            const string playerId = "job-identity-unit";
            var student = new StudentLifeProgress(_slot, playerId, 12, 10, 0, 480); student.ConfigureCalendar(calendar);
            var storage = new SaveService(_slot, _root);
            Assert.That(AdultCareerSnapshotPersistence.SaveSession(storage,
                AdultCareerSnapshotPersistence.Capture(new AdultCareerProgress(_slot, playerId), student, new Inventory(), new WorldStateProgress(_slot, playerId))), Is.True);
            _actor = Own(new GameObject("RegistryIdentityFixtureOwner"));
            SceneManager.MoveGameObjectToScene(_actor, _ownedScene);
            _actor.AddComponent<PlayerIdentity>().Configure(playerId);
            _actor.AddComponent<StudentLifeProgressComponent>().ConfigureForTests(_slot, playerId, 12, 10, 0, 480);
            _actor.AddComponent<PlayerInventory>();
            _owner = _actor.AddComponent<AdultCareerRuntimeComponent>(); _owner.Bind(_registry, storage);
            Assert.That(_owner.IsBound && _owner.CanMutate, Is.True);
            var canvasRoot = Own(new GameObject("RegistryIdentityFixtureCanvas", typeof(RectTransform), typeof(Canvas)));
            SceneManager.MoveGameObjectToScene(canvasRoot, _ownedScene);
            _canvas = canvasRoot.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Assert.That(_actor.scene, Is.EqualTo(_ownedScene));
            Assert.That(Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                .Where(c => c.isRootCanvas && c.isActiveAndEnabled && c.gameObject.scene == _actor.scene).ToArray(),
                Is.EquivalentTo(new[] { _canvas }), "The production search must see only the owned canvas in the actor scene.");
        }
        private PartTimeWorkDefinition SceneCopy(string id)
        {
            var scene = Own(Object.Instantiate(_canonical));
            scene.ConfigureForTests(id, "untrusted scene copy", null, 200, 9, 8, 7,
                new[] { new InventoryGrant(_coin, 999) }, null, Array.Empty<WorkOutcomeBase>());
            if (string.IsNullOrEmpty(id)) scene.name = string.Empty; // Id otherwise falls back to the SO's name.
            var costly = New<ActivityTimePolicyDefinition>(); costly.ConfigureForTests("untrusted-time", 2, 127, false);
            Set(scene, "_timePolicy", costly); Set(scene, "_minigame", Game("untrusted", 1));
            return scene;
        }
        private MinigameDefinition Game(string prefix, int answer)
        {
            var challenge = New<ChoiceChallengeDefinition>();
            Set(challenge, "_symbols", new[] { prefix + "-a", prefix + "-b" });
            Set(challenge, "_prompts", new[] { prefix + "-prompt" }); Set(challenge, "_answers", new[] { answer });
            var game = New<MinigameDefinition>(); Set(game, "_id", "fixture.game." + prefix);
            Set(game, "_challenges", new ChallengeDefinitionBase[] { challenge });
            Assert.That(game.IsValid, Is.True);
            return game;
        }
        private void SnapshotStatic(Type type, string name)
        {
            var field = type.GetField(name, StaticPrivate); Assert.That(field, Is.Not.Null, name); _priorStatics.Add(field, field.GetValue(null));
        }
        private T Own<T>(T value) where T : Object { _owned.Add(value); return value; }
        private T New<T>() where T : ScriptableObject => Own(ScriptableObject.CreateInstance<T>());
        private static void Set(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, InstancePrivate); Assert.That(field, Is.Not.Null, name); field.SetValue(target, value);
        }
    }
}
