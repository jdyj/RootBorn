using System.Collections;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.DiscoveryClues;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.Game.WorldState;
using Rootborn.UI.DiscoveryClues;
using Rootborn.UI.MainMenu;
using Rootborn.UI.Quests;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    public sealed class DiscoveryClueLoopE2EScenarioTests : InputTestFixture
    {
        private const string TestSlotId = "slot-0";
        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.timeScale = 1f;
            CleanupResidue();
            EnsurePassiveEventSystem();
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-discovery-clue-e2e", System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveRoot);
            SaveService.SetRootDirectoryForTests(_saveRoot);
            SaveSlotSelectPanel.SetSaveRootForTests(_saveRoot);
            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return null;
        }

        [UnityTearDown]
        public new IEnumerator TearDown()
        {
            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            SaveService.SetRootDirectoryForTests(null);
            SaveSlotSelectPanel.SetSaveRootForTests(null);
            CleanupResidue();
            InputSystemUiModulePlayModeTestGuard.UninstallForCurrentTest();
            LogAssert.ignoreFailingMessages = false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator DISCOVERY_CLUE_E2E_001_011_SaveSlotKeyboardClueLoopDisplaysPersistsAndDedupes()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return StartNewTownFromSaveSlotUi();
            var runtime = FindRuntime();
            var clue = LoadRegisteredPlayClue(out var source);
            Assert.GreaterOrEqual(clue.Sources.Count, 5, "DISCOVERY-CLUE-E2E-011 failed: registered SO clue must expose all required source paths.");
            yield return OpenSourcePanelAndClick(runtime, clue, DiscoveryClueSourceKind.NpcDialogue, DiscoveryClueSummarySurface.NpcDialogue);
            yield return OpenSourcePanelAndClick(runtime, clue, DiscoveryClueSourceKind.BoardPost, DiscoveryClueSummarySurface.Board);
            yield return OpenSourcePanelAndClick(runtime, clue, DiscoveryClueSourceKind.MapHint, DiscoveryClueSummarySurface.Map);
            yield return OpenSourcePanelAndClick(runtime, clue, DiscoveryClueSourceKind.EncyclopediaUnknown, DiscoveryClueSummarySurface.Encyclopedia);
            var clueObject = SpawnClueObjectNearPlayer(runtime.Player, clue, source, runtime.QuestLogPanel.QuestLog);

            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, clueObject.transform.position + new Vector3(0.35f, 0f, 0f), 0.3f, "DISCOVERY-CLUE-E2E-003 failed: keyboard movement could not reach clue object.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "DISCOVERY-CLUE-E2E-004 failed: clue object did not expose an interaction prompt.");
            yield return PressInteractKey(keyboard);
            yield return null;

            var interactor = clueObject.GetComponent<DiscoveryClueInteractor>();
            Assert.AreEqual(DiscoveryClueResultKind.Completed, interactor.LastCompleteResult.Kind, "DISCOVERY-CLUE-E2E-006 failed: actual input path did not complete the clue.");
            string playerId = ResolvePlayerId(runtime.Inventory);
            var clueProgress = DiscoveryClueProgressPersistence.LoadOrCreate(TestSlotId, playerId);
            var record = clueProgress.GetRecord(clue.Id);
            Assert.IsTrue(record.Seen, "DISCOVERY-CLUE-E2E-008 failed: clue seen flag was not saved.");
            Assert.IsTrue(record.Completed, "DISCOVERY-CLUE-E2E-008 failed: clue completed flag was not saved.");
            Assert.IsTrue(record.RewardClaimed, "DISCOVERY-CLUE-E2E-008 failed: reward claimed flag was not saved.");
            Assert.GreaterOrEqual(record.SourceIdsSeen.Length, 5, "DISCOVERY-CLUE-E2E-005 failed: NPC, board, map, encyclopedia, and location trace sources were not all recorded.");
            Assert.AreEqual(QuestState.Active, runtime.QuestLogPanel.QuestLog.GetState(clue.RelatedQuest));
            Assert.GreaterOrEqual(runtime.StudentLife.EnsureProgress().GetCareerHintIds().Length, 1, "DISCOVERY-CLUE-E2E-007 failed: registered clue outcome did not grant a personal career hint.");
            Assert.GreaterOrEqual(Rootborn.Game.Encyclopedia.EncyclopediaProgressPersistence.LoadOrCreate(TestSlotId, playerId).UnlockedCount, 1, "DISCOVERY-CLUE-E2E-007 failed: registered clue outcome did not unlock encyclopedia data.");
            Assert.IsTrue(WorldStateProgressPersistence.LoadOrCreate(TestSlotId, playerId).IsActive(clue.RelatedWorldState));
            Assert.GreaterOrEqual(record.RelatedDiscoveredEncyclopediaEntryIds.Length + record.RelatedCareerHintGrantHistory.Length + record.RelatedFollowUpQuestIds.Length + record.RelatedWorldStateFlagIds.Length, 4, "DISCOVERY-CLUE-E2E-007 failed: registered clue did not record multiple outcome categories.");

            var context = new DiscoveryClueContext(WorldStateProgressPersistence.LoadOrCreate(TestSlotId, playerId), clueProgress, runtime.StudentLife.EnsureProgress(), runtime.QuestLogPanel.QuestLog, Rootborn.Game.Encyclopedia.EncyclopediaProgressPersistence.LoadOrCreate(TestSlotId, playerId), runtime.StudentLife.EnsureProgress().CurrentDay);
            var summaries = DiscoveryClueSummaryBuilder.BuildForClue(new DiscoveryClueLookupCache(new[] { clue }), clue.Id, context, DiscoveryClueSummarySurface.WorldLog);
            var log = DiscoveryClueLogPanel.EnsureInScene(Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include));
            log.Show(summaries);
            Assert.GreaterOrEqual(log.CardCountForTests, 5, "DISCOVERY-CLUE-E2E-008 failed: clue log did not render all source summaries.");
            StringAssert.Contains("Play clue", log.TextForTests);
            StringAssert.Contains("LocationTrace", log.TextForTests);

            yield return PressInteractKey(keyboard);
            yield return null;
            var afterDuplicate = DiscoveryClueProgressPersistence.LoadOrCreate(TestSlotId, playerId).GetRecord(clue.Id);
            Assert.AreEqual(1, afterDuplicate.CompletedCount, "DISCOVERY-CLUE-E2E-010 failed: repeated interaction duplicated clue completion rewards.");

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return LoadScene("MainMenu");
            EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "LoadButton"));
            yield return WaitForScene("Town", 15f, "DISCOVERY-CLUE-E2E-009 failed: save slot UI did not reload Town.");
            yield return WaitForTownRuntime(15f);
            var reloaded = FindRuntime();
            playerId = ResolvePlayerId(reloaded.Inventory);
            var restored = DiscoveryClueProgressPersistence.LoadOrCreate(TestSlotId, playerId).GetRecord(clue.Id);
            Assert.IsTrue(restored.Seen && restored.Completed && restored.RewardClaimed, "DISCOVERY-CLUE-E2E-009 failed: clue state did not survive save-slot reload.");
        }

        private IEnumerator OpenSourcePanelAndClick(RuntimeRefs runtime, DiscoveryClueDefinition clue, DiscoveryClueSourceKind kind, DiscoveryClueSummarySurface surface)
        {
            var source = FindSource(clue, kind);
            string playerId = ResolvePlayerId(runtime.Inventory);
            var world = WorldStateProgressPersistence.LoadOrCreate(TestSlotId, playerId);
            var clueProgress = DiscoveryClueProgressPersistence.LoadOrCreate(TestSlotId, playerId);
            var encyclopedia = Rootborn.Game.Encyclopedia.EncyclopediaProgressPersistence.LoadOrCreate(TestSlotId, playerId);
            var context = new DiscoveryClueContext(world, clueProgress, runtime.StudentLife.EnsureProgress(), runtime.QuestLogPanel.QuestLog, encyclopedia, runtime.StudentLife.EnsureProgress().CurrentDay);
            var summaries = DiscoveryClueSummaryBuilder.BuildForSource(new DiscoveryClueLookupCache(new[] { clue }), kind, source.Id, context, surface);
            Assert.AreEqual(1, summaries.Length, "DISCOVERY-CLUE-E2E-005 failed: source summary was not built from registered SO data for " + kind + ".");
            DiscoveryClueResult revealResult = default;
            var panel = DiscoveryClueSourcePanel.EnsureInScene(Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include));
            panel.Show(summaries, selected =>
            {
                var runner = new DiscoveryClueRunner();
                runner.TryRevealSource(clue, source, context, out revealResult);
                DiscoveryClueProgressPersistence.Save(clueProgress);
            });
            yield return ClickButton(panel.GetButtonForTests(0));
            Assert.AreEqual(DiscoveryClueResultKind.Revealed, revealResult.Kind, "DISCOVERY-CLUE-E2E-005 failed: clue source did not reveal through a real UI button for " + kind + ".");
        }

        private static DiscoveryClueDefinition LoadRegisteredPlayClue(out DiscoveryClueSourceDefinition source)
        {
            var registry = Rootborn.Game.Managers.Managers.Data != null ? Rootborn.Game.Managers.Managers.Data.Registry : null;
            Assert.IsNotNull(registry, "DISCOVERY-CLUE-E2E-011 failed: Town runtime did not expose GameDataRegistry.");
            var clues = GameDataRegistryDiscoveryClueExtensions.GetDiscoveryClues(registry);
            Assert.AreSame(registry.DiscoveryClues, clues, "DISCOVERY-CLUE-E2E-011 failed: clue E2E must load from registered GameDataRegistry arrays.");
            var cache = new DiscoveryClueLookupCache(clues);
            Assert.IsTrue(cache.TryGetById("clue.discovery.play-loop", out var clue), "DISCOVERY-CLUE-E2E-011 failed: SO-only play clue was not registered.");
            source = FindSource(clue, DiscoveryClueSourceKind.LocationTrace);
            Assert.IsNotNull(clue.RelatedWorldState, "DISCOVERY-CLUE-E2E-007 failed: registered clue must reference a world-state flag.");
            Assert.IsNotNull(clue.RelatedQuest, "DISCOVERY-CLUE-E2E-007 failed: registered clue must reference a follow-up quest.");
            return clue;
        }

        private static DiscoveryClueSourceDefinition FindSource(DiscoveryClueDefinition clue, DiscoveryClueSourceKind kind)
        {
            for (int i = 0; i < clue.Sources.Count; i++)
            {
                var candidate = clue.Sources[i];
                if (candidate != null && candidate.Kind == kind) return candidate;
            }
            Assert.Fail("DISCOVERY-CLUE-E2E-005 failed: registered clue must include a " + kind + " source.");
            return null;
        }

        private static GameObject SpawnClueObjectNearPlayer(GameObject player, DiscoveryClueDefinition clue, DiscoveryClueSourceDefinition source, QuestLog questLog)
        {
            var go = new GameObject("DiscoveryClue_PlayTrace", typeof(BoxCollider2D));
            go.transform.position = player.transform.position + new Vector3(1.2f, 0f, 0f);
            var collider = go.GetComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(0.8f, 0.8f);
            var interactor = go.AddComponent<DiscoveryClueInteractor>();
            interactor.Bind(clue, source, questLog, DiscoveryClueCompletionKind.LocationVisited, "location.discovery.play-trace");
            return go;
        }

        private static IEnumerator StartNewTownFromSaveSlotUi()
        {
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "NewGameButton"));
            yield return WaitForScene("Town", 15f, "DISCOVERY-CLUE-E2E-001 failed: save slot UI did not enter Town.");
            yield return WaitForTownRuntime(15f);
        }

        private static IEnumerator WaitForTownRuntime(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var player = GameObject.Find("Player");
                var dialoguePanel = Object.FindFirstObjectByType<DialoguePanel>(FindObjectsInactive.Include);
                var questLogPanel = Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include);
                if (player != null && player.GetComponent<PlayerInteractionRouter>() != null && player.GetComponent<PlayerInventory>() != null && player.GetComponent<StudentLifeProgressComponent>() != null && dialoguePanel != null && questLogPanel != null && questLogPanel.QuestLog != null && EventSystem.current != null) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.Fail("Discovery clue E2E runtime did not expose required Town objects within timeout.");
        }

        private static RuntimeRefs FindRuntime()
        {
            var player = GameObject.Find("Player");
            Assert.IsNotNull(player);
            return new RuntimeRefs(player, player.GetComponent<PlayerInteractionRouter>(), player.GetComponent<PlayerInventory>(), player.GetComponent<StudentLifeProgressComponent>(), Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include));
        }

        private IEnumerator WalkPlayerWithKeyboardTo(GameObject player, Keyboard keyboard, Vector3 targetPosition, float tolerance, string failure)
        {
            Key activeKey = Key.None;
            for (int i = 0; i < 720 && Vector3.Distance(player.transform.position, targetPosition) > tolerance; i++)
            {
                Vector3 delta = targetPosition - player.transform.position;
                Key key = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y) ? (delta.x >= 0f ? Key.D : Key.A) : (delta.y >= 0f ? Key.W : Key.S);
                if (activeKey != key)
                {
                    if (activeKey != Key.None) Release(ControlFor(keyboard, activeKey));
                    Press(ControlFor(keyboard, key));
                    activeKey = key;
                }
                InputSystem.Update();
                yield return null;
                yield return new WaitForFixedUpdate();
            }
            if (activeKey != Key.None) Release(ControlFor(keyboard, activeKey));
            InputSystem.Update();
            yield return new WaitForFixedUpdate();
            Assert.LessOrEqual(Vector3.Distance(player.transform.position, targetPosition), tolerance, failure);
        }

        private IEnumerator PressInteractKey(Keyboard keyboard)
        {
            Press(keyboard.eKey);
            InputSystem.Update();
            yield return null;
            yield return new WaitForFixedUpdate();
            Release(keyboard.eKey);
            InputSystem.Update();
            yield return null;
        }

        private static IEnumerator LoadScene(string sceneName)
        {
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            Assert.IsNotNull(op);
            while (!op.isDone) yield return null;
        }

        private static IEnumerator WaitForScene(string sceneName, float timeoutSeconds, string failure)
        {
            float elapsed = 0f;
            while (SceneManager.GetActiveScene().name != sceneName && elapsed < timeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.AreEqual(sceneName, SceneManager.GetActiveScene().name, failure);
        }

        private static IEnumerator ClickButton(Button button)
        {
            Assert.IsTrue(button.gameObject.activeInHierarchy);
            Assert.IsTrue(button.interactable);
            ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
            yield return null;
        }

        private static Button FindButton(string cardName, string buttonName)
        {
            var card = GameObject.Find(cardName);
            Assert.IsNotNull(card, "Save slot UI card missing: " + cardName);
            return FindButton(card.transform, buttonName);
        }

        private static Button FindButton(Transform root, string name)
        {
            var child = FindChild(root, name);
            Assert.IsNotNull(child, "UI button missing: " + name);
            return child.GetComponent<Button>();
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var match = FindChild(root.GetChild(i), name);
                if (match != null) return match;
            }
            return null;
        }

        private static string ResolvePlayerId(PlayerInventory inventory)
        {
            var identity = inventory.GetComponent<PlayerIdentity>();
            return identity != null ? identity.PlayerId : "player";
        }

        private static KeyControl ControlFor(Keyboard keyboard, Key key)
        {
            if (key == Key.A) return keyboard.aKey;
            if (key == Key.D) return keyboard.dKey;
            if (key == Key.W) return keyboard.wKey;
            if (key == Key.S) return keyboard.sKey;
            Assert.Fail("Unsupported key: " + key);
            return keyboard.eKey;
        }

        private static void EnsurePassiveEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            go.AddComponent<PassiveInputModule>();
        }

        private static void CleanupResidue()
        {
            DestroyAllNamed("SaveSlotSelectPanel");
            DestroyAllNamed("SaveSlotCanvas");
            DestroyAllNamed("SaveSlotSelectRoot");
            DestroyAllNamed("EventSystem");
            DestroyAllNamed("Player");
            DestroyAllNamed("DiscoveryClueLogPanel");
            DestroyAllNamed("DiscoveryClueSourcePanel");
            DestroyAllNamed("DiscoveryClue_PlayTrace");
        }

        private static void DestroyAllNamed(string objectName)
        {
            var transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = transforms.Length - 1; i >= 0; i--) if (transforms[i] != null && transforms[i].name == objectName) Object.DestroyImmediate(transforms[i].gameObject);
        }

        private readonly struct RuntimeRefs
        {
            public readonly GameObject Player;
            public readonly PlayerInteractionRouter Router;
            public readonly PlayerInventory Inventory;
            public readonly StudentLifeProgressComponent StudentLife;
            public readonly QuestLogPanel QuestLogPanel;
            public RuntimeRefs(GameObject player, PlayerInteractionRouter router, PlayerInventory inventory, StudentLifeProgressComponent studentLife, QuestLogPanel questLogPanel)
            {
                Player = player;
                Router = router;
                Inventory = inventory;
                StudentLife = studentLife;
                QuestLogPanel = questLogPanel;
            }
        }
    }
}
