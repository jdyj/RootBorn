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
using Rootborn.UI.Objectives;
using Rootborn.UI.Quests;
using Rootborn.UI.StudentLife;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    public sealed class DiscoveryRouteUnlockE2EScenarioTests : InputTestFixture
    {
        private const string TestSlotId = "slot-0";
        private const string ClueId = "clue.discovery.play-loop";
        private const string WorldStateId = "world.library.archive-open";
        private const string RouteId = "daily.route.library.archive-research";
        private const string RouteLabel = "daily.route.library.archive-research";
        private const string ScreenshotPath = "Builds/Logs/discovery-route-unlock-e2e.png";
        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.timeScale = 1f;
            CleanupResidue();
            EnsurePassiveEventSystem();
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-discovery-route-unlock-e2e", System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveRoot);
            SaveService.SetRootDirectoryForTests(_saveRoot);
            SaveSlotSelectPanel.SetSaveRootForTests(_saveRoot);
            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            DeleteScreenshot();
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
        public IEnumerator DISCOVERY_ROUTE_E2E_001_SaveSlotClueInterpretationUnlocksDailyPlanRouteAndReload()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return StartNewTownFromSaveSlotUi();
            var runtime = FindRuntime();
            var registry = Rootborn.Game.Managers.Managers.Data.Registry;
            var clue = FindClue(registry, ClueId);
            var worldFlag = FindWorldState(registry, WorldStateId);
            string playerId = ResolvePlayerId(runtime.Player);

            Assert.IsFalse(WorldStateProgressPersistence.LoadOrCreate(TestSlotId, playerId).IsActive(worldFlag));

            var clueInteractor = SpawnClueInteractorNearPlayer(runtime.Player, clue, runtime.QuestLogPanel.QuestLog);
            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, clueInteractor.transform.position + new Vector3(0.15f, 0f, 0f), 0.3f, "DISCOVERY-ROUTE-E2E-001 failed: keyboard movement could not reach clue source.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "DISCOVERY-ROUTE-E2E-001 failed: clue source prompt was not visible.");
            yield return PressInteractKey(keyboard);
            var clueRuntime = clueInteractor.GetComponent<DiscoveryClueInteractor>();
            Assert.AreEqual(DiscoveryClueResultKind.Revealed, clueRuntime.LastRevealResult.Kind, "DISCOVERY-ROUTE-E2E-001 failed: player-facing clue interaction did not reveal the clue.");
            Assert.IsTrue(DiscoveryClueProgressPersistence.LoadOrCreate(TestSlotId, playerId).GetRecord(ClueId).Seen, "DISCOVERY-ROUTE-E2E-001 failed: clue seen record was not saved.");

            var interpretationInteractor = SpawnInterpretationInteractorNearPlayer(runtime.Player, registry.ClueInterpretations, runtime.QuestLogPanel.QuestLog);
            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, interpretationInteractor.transform.position + new Vector3(0.15f, 0f, 0f), 0.3f, "DISCOVERY-ROUTE-E2E-001 failed: keyboard movement could not reach interpretation point.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "DISCOVERY-ROUTE-E2E-001 failed: interpretation prompt was not visible.");
            yield return PressInteractKey(keyboard);
            var choicePanel = Object.FindFirstObjectByType<ClueInterpretationChoicePanel>(FindObjectsInactive.Include);
            Assert.IsNotNull(choicePanel, "DISCOVERY-ROUTE-E2E-001 failed: interpretation choice UI did not open.");
            Assert.GreaterOrEqual(choicePanel.ButtonCountForTests, 1);
            yield return ClickButton(choicePanel.GetButtonForTests(0));
            var interpretationRuntime = interpretationInteractor.GetComponent<ClueInterpretationInteractor>();
            Assert.AreEqual(ClueInterpretationResultKind.Completed, interpretationRuntime.LastResult.Kind, "DISCOVERY-ROUTE-E2E-001 failed: visible interpretation choice did not complete.");
            Assert.IsTrue(ClueInterpretationProgressPersistence.LoadOrCreate(TestSlotId, playerId).GetRecord(interpretationRuntime.LastResult.InterpretationId).Completed, "DISCOVERY-ROUTE-E2E-001 failed: interpretation completion was not saved.");
            Assert.IsTrue(WorldStateProgressPersistence.LoadOrCreate(TestSlotId, playerId).IsActive(worldFlag), "DISCOVERY-ROUTE-E2E-001 failed: interpretation did not activate route unlock world state.");

            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.Board.transform.position, 0.25f, "DISCOVERY-ROUTE-E2E-001 failed: keyboard movement could not reach Daily Plan board.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "DISCOVERY-ROUTE-E2E-001 failed: Daily Plan board prompt was not visible.");
            yield return PressInteractKey(keyboard);
            yield return WaitForDailyPlanPanelOpen(3f, "DISCOVERY-ROUTE-E2E-001 failed: Daily Plan panel did not open.");
            Assert.GreaterOrEqual(runtime.DailyPanel.RouteRowCountForTests, 4, "DISCOVERY-ROUTE-E2E-001 failed: unlocked route was not added to visible Daily Plan choices.");
            yield return ClickButton(FindRouteButton(runtime.DailyPanel, RouteLabel));
            Assert.AreEqual(RouteId, runtime.Daily.Progress.SelectedRouteId, "DISCOVERY-ROUTE-E2E-001 failed: clicked unlocked route was not selected.");

            runtime.ObjectiveProvider.RefreshNow();
            yield return PressTab(keyboard);
            yield return ClickButton(FindButton(runtime.Journal.transform, "ObjectiveCategory_Goals"));
            yield return ClickButton(FindButton(runtime.Journal.transform, "ObjectiveRow_1"));
            StringAssert.Contains("objective.daily.library.archive.title", runtime.Journal.VisibleText, "DISCOVERY-ROUTE-E2E-001 failed: Objective Journal Goals did not include selected unlocked route guidance.");
            yield return ClickButton(FindButton(runtime.Journal.transform, "ObjectiveTrackButton"));
            Assert.IsTrue(runtime.TrackedHud.IsVisible, "DISCOVERY-ROUTE-E2E-001 failed: tracked HUD did not show one tracked route objective.");
            StringAssert.Contains("objective.daily.library.archive.title", runtime.TrackedHud.VisibleText);
            CaptureScreenshot();
            yield return WaitForScreenshot(3f);

            int interpretationCount = ClueInterpretationProgressPersistence.LoadOrCreate(TestSlotId, playerId).ToSaveData().Records.Length;
            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return LoadScene("MainMenu");
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "LoadButton"));
            yield return WaitForScene("Town", 15f, "DISCOVERY-ROUTE-E2E-001 failed: SaveSlot Load did not re-enter Town.");
            EnsureTownInstallers();
            yield return WaitForRuntime(15f);

            var reloaded = FindRuntime();
            string reloadedPlayerId = ResolvePlayerId(reloaded.Player);
            Assert.IsTrue(WorldStateProgressPersistence.LoadOrCreate(TestSlotId, reloadedPlayerId).IsActive(worldFlag), "DISCOVERY-ROUTE-E2E-001 failed: world-state unlock did not survive reload.");
            Assert.AreEqual(interpretationCount, ClueInterpretationProgressPersistence.LoadOrCreate(TestSlotId, reloadedPlayerId).ToSaveData().Records.Length, "DISCOVERY-ROUTE-E2E-001 failed: interpretation records duplicated after reload.");
            yield return WalkPlayerWithKeyboardTo(reloaded.Player, keyboard, reloaded.Board.transform.position, 0.25f, "DISCOVERY-ROUTE-E2E-001 failed: keyboard movement could not reach Daily Plan board after reload.");
            reloaded.Router.RefreshPromptNow();
            yield return PressInteractKey(keyboard);
            yield return WaitForDailyPlanPanelOpen(3f, "DISCOVERY-ROUTE-E2E-001 failed: Daily Plan panel did not open after reload.");
            Assert.IsNotNull(FindRouteButton(reloaded.DailyPanel, RouteLabel), "DISCOVERY-ROUTE-E2E-001 failed: unlocked route availability did not restore after reload.");
        }

        private IEnumerator StartNewTownFromSaveSlotUi()
        {
            yield return LoadScene("MainMenu");
            EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "NewGameButton"));
            var creator = GameObject.Find("SpumCharacterCreatorRoot");
            if (creator != null) yield return ClickButton(FindButton(creator.transform, "ConfirmButton"));
            yield return WaitForScene("Town", 15f, "DISCOVERY-ROUTE-E2E-001 failed: SaveSlot New Game did not enter Town.");
            EnsureTownInstallers();
            yield return WaitForRuntime(15f);
        }

        private static void EnsureTownInstallers()
        {
            StudentDayRuntimeInstaller.EnsureForActiveScene();
            LocationNpcRuntimeInstaller.EnsureForActiveScene();
            DailyPlanRuntimeInstaller.EnsureForActiveScene();
            CampaignRuntimeInstaller.EnsureForActiveScene();
        }

        private static IEnumerator WaitForRuntime(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var player = GameObject.Find("Player");
                if (player != null && player.GetComponent<PlayerInteractionRouter>() != null && player.GetComponent<StudentLifeProgressComponent>() != null && player.GetComponent<DailyPlanProgressComponent>() != null && player.GetComponent<PlayerInventory>() != null &&
                    GameObject.Find("DailyPlanBoard") != null && Object.FindFirstObjectByType<DailyPlanPanel>(FindObjectsInactive.Include) != null && Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include) != null &&
                    Object.FindFirstObjectByType<ObjectiveJournalPanel>(FindObjectsInactive.Include) != null && Object.FindFirstObjectByType<ObjectiveJournalRuntimeProvider>(FindObjectsInactive.Include) != null && Object.FindFirstObjectByType<TrackedObjectiveHud>(FindObjectsInactive.Include) != null && EventSystem.current != null)
                {
                    yield break;
                }
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.Fail("DISCOVERY-ROUTE-E2E-001 failed: Town runtime did not install within timeout.");
        }

        private static RuntimeRefs FindRuntime()
        {
            var player = GameObject.Find("Player");
            return new RuntimeRefs(
                player,
                player.GetComponent<PlayerInteractionRouter>(),
                player.GetComponent<DailyPlanProgressComponent>(),
                GameObject.Find("DailyPlanBoard").GetComponent<DailyPlanBoardInteractor>(),
                Object.FindFirstObjectByType<DailyPlanPanel>(FindObjectsInactive.Include),
                Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include),
                Object.FindFirstObjectByType<ObjectiveJournalPanel>(FindObjectsInactive.Include),
                Object.FindFirstObjectByType<ObjectiveJournalRuntimeProvider>(FindObjectsInactive.Include),
                Object.FindFirstObjectByType<TrackedObjectiveHud>(FindObjectsInactive.Include));
        }

        private static GameObject SpawnClueInteractorNearPlayer(GameObject player, DiscoveryClueDefinition clue, QuestLog questLog)
        {
            var go = new GameObject("DiscoveryRouteUnlock_ClueSource", typeof(BoxCollider2D));
            go.transform.position = player.transform.position + new Vector3(1.2f, 0f, 0f);
            var collider = go.GetComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(0.8f, 0.8f);
            var interactor = go.AddComponent<DiscoveryClueInteractor>();
            interactor.Bind(clue, FindSource(clue, DiscoveryClueSourceKind.NpcDialogue), questLog, DiscoveryClueCompletionKind.ObjectInteracted, string.Empty);
            return go;
        }

        private static GameObject SpawnInterpretationInteractorNearPlayer(GameObject player, ClueInterpretationDefinition[] interpretations, QuestLog questLog)
        {
            var go = new GameObject("DiscoveryRouteUnlock_InterpretationPoint", typeof(BoxCollider2D));
            go.transform.position = player.transform.position + new Vector3(1.2f, 0f, 0f);
            var collider = go.GetComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(0.8f, 0.8f);
            var interactor = go.AddComponent<ClueInterpretationInteractor>();
            interactor.Bind(interpretations, ClueInterpretationSourceKind.ObjectInteraction, "object.workbench", questLog);
            return go;
        }

        private static DiscoveryClueDefinition FindClue(GameDataRegistry registry, string id)
        {
            for (int i = 0; i < registry.DiscoveryClues.Length; i++) if (registry.DiscoveryClues[i] != null && registry.DiscoveryClues[i].Id == id) return registry.DiscoveryClues[i];
            Assert.Fail("Missing discovery clue: " + id);
            return null;
        }

        private static WorldStateFlagDefinition FindWorldState(GameDataRegistry registry, string id)
        {
            for (int i = 0; i < registry.WorldStateFlags.Length; i++) if (registry.WorldStateFlags[i] != null && registry.WorldStateFlags[i].Id == id) return registry.WorldStateFlags[i];
            Assert.Fail("Missing world state: " + id);
            return null;
        }

        private static DiscoveryClueSourceDefinition FindSource(DiscoveryClueDefinition clue, DiscoveryClueSourceKind kind)
        {
            for (int i = 0; i < clue.Sources.Count; i++) if (clue.Sources[i] != null && clue.Sources[i].Kind == kind) return clue.Sources[i];
            Assert.Fail("Missing clue source: " + kind);
            return null;
        }

        private static Button FindRouteButton(DailyPlanPanel panel, string label)
        {
            for (int i = 0; i < panel.RouteRowCountForTests; i++)
            {
                var button = panel.GetRouteButtonForTests(i);
                if (button == null) continue;
                var text = button.GetComponentInChildren<Text>();
                if (text != null && text.text == label) return button;
            }
            return null;
        }

        private static IEnumerator WaitForDailyPlanPanelOpen(float timeoutSeconds, string failure)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var panel = Object.FindFirstObjectByType<DailyPlanPanel>(FindObjectsInactive.Include);
                if (panel != null && panel.gameObject.activeSelf) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.Fail(failure);
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

        private IEnumerator PressTab(Keyboard keyboard)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab));
            InputSystem.Update();
            yield return null;
            yield return new WaitForFixedUpdate();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
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
            Assert.IsNotNull(button);
            Assert.IsTrue(button.gameObject.activeInHierarchy);
            Assert.IsTrue(button.interactable);
            ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
            yield return null;
        }

        private static Button FindButton(string cardName, string buttonName)
        {
            var card = GameObject.Find(cardName);
            Assert.IsNotNull(card, "UI card missing: " + cardName);
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
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var match = FindChild(root.GetChild(i), name);
                if (match != null) return match;
            }
            return null;
        }

        private static string ResolvePlayerId(GameObject player)
        {
            var identity = player.GetComponent<PlayerIdentity>();
            return identity != null && !string.IsNullOrEmpty(identity.PlayerId) ? identity.PlayerId : PlayerIdentity.DefaultPlayerId;
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

        private static void CaptureScreenshot()
        {
            var dir = Path.GetDirectoryName(ScreenshotPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            ScreenCapture.CaptureScreenshot(ScreenshotPath);
        }

        private static IEnumerator WaitForScreenshot(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                if (File.Exists(ScreenshotPath) && new FileInfo(ScreenshotPath).Length > 0) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.Fail("DISCOVERY-ROUTE-E2E-001 failed: Game View screenshot evidence was not written: " + ScreenshotPath);
        }

        private static void DeleteScreenshot()
        {
            if (File.Exists(ScreenshotPath)) File.Delete(ScreenshotPath);
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
            DestroyAllNamed("DiscoveryRouteUnlock_ClueSource");
            DestroyAllNamed("DiscoveryRouteUnlock_InterpretationPoint");
        }

        private static void DestroyAllNamed(string objectName)
        {
            var transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = transforms.Length - 1; i >= 0; i--)
            {
                var transform = transforms[i];
                if (transform != null && transform.name == objectName) Object.DestroyImmediate(transform.gameObject);
            }
        }

        private readonly struct RuntimeRefs
        {
            public readonly GameObject Player;
            public readonly PlayerInteractionRouter Router;
            public readonly DailyPlanProgressComponent Daily;
            public readonly DailyPlanBoardInteractor Board;
            public readonly DailyPlanPanel DailyPanel;
            public readonly QuestLogPanel QuestLogPanel;
            public readonly ObjectiveJournalPanel Journal;
            public readonly ObjectiveJournalRuntimeProvider ObjectiveProvider;
            public readonly TrackedObjectiveHud TrackedHud;

            public RuntimeRefs(GameObject player, PlayerInteractionRouter router, DailyPlanProgressComponent daily, DailyPlanBoardInteractor board, DailyPlanPanel dailyPanel, QuestLogPanel questLogPanel, ObjectiveJournalPanel journal, ObjectiveJournalRuntimeProvider objectiveProvider, TrackedObjectiveHud trackedHud)
            {
                Player = player;
                Router = router;
                Daily = daily;
                Board = board;
                DailyPanel = dailyPanel;
                QuestLogPanel = questLogPanel;
                Journal = journal;
                ObjectiveProvider = objectiveProvider;
                TrackedHud = trackedHud;
            }
        }
    }
}
