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
using Rootborn.Tests.PlayMode.EndToEnd.Support;
using Rootborn.UI.DiscoveryClues;
using Rootborn.UI.MainMenu;
using Rootborn.UI.Objectives;
using Rootborn.UI.Quests;
using Rootborn.UI.StudentLife;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    public sealed class DiscoveryRouteUnlockE2EScenarioTests : Rootborn.Tests.PlayMode.PlayModeInputFixture
    {
        private const string TestSlotId = "slot-0";
        private const string ClueId = "clue.discovery.play-loop";
        private const string WorldStateId = "world.library.archive-open";
        private const string RouteId = "daily.route.library.archive-research";
        private const string RouteLabel = "daily.route.library.archive-research";
        private const string RouteObjectiveTitle = "objective.daily.library.archive.title";
        private const string InterpretationButtonText = "restore";
        private const string ScreenshotPath = "Builds/Logs/discovery-route-unlock-e2e.png";
        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.timeScale = 1f;
            CleanupResidue();
            PlayerFlowE2EDriver.EnsurePassiveEventSystem();
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-discovery-route-unlock-e2e", System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveRoot);
            SaveService.SetRootDirectoryForTests(_saveRoot);
            SaveSlotSelectPanel.SetSaveRootForTests(_saveRoot);
            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            ScreenshotEvidence.DeleteIfExists(ScreenshotPath);
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
            yield return PlayerFlowE2EDriver.WalkPlayerWithKeyboardTo(runtime.Player, keyboard, clueInteractor.transform.position + new Vector3(0.15f, 0f, 0f), 0.3f, 720, "DISCOVERY-ROUTE-E2E-001 failed: keyboard movement could not reach clue source.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "DISCOVERY-ROUTE-E2E-001 failed: clue source prompt was not visible.");
            yield return PlayerFlowE2EDriver.PressInteractKey(keyboard);
            var clueRuntime = clueInteractor.GetComponent<DiscoveryClueInteractor>();
            Assert.AreEqual(DiscoveryClueResultKind.Revealed, clueRuntime.LastRevealResult.Kind, "DISCOVERY-ROUTE-E2E-001 failed: player-facing clue interaction did not reveal the clue.");
            Assert.IsTrue(DiscoveryClueProgressPersistence.LoadOrCreate(TestSlotId, playerId).GetRecord(ClueId).Seen, "DISCOVERY-ROUTE-E2E-001 failed: clue seen record was not saved.");

            var interpretationInteractor = SpawnInterpretationInteractorNearPlayer(runtime.Player, registry.ClueInterpretations, runtime.QuestLogPanel.QuestLog);
            yield return PlayerFlowE2EDriver.WalkPlayerWithKeyboardTo(runtime.Player, keyboard, interpretationInteractor.transform.position + new Vector3(0.15f, 0f, 0f), 0.3f, 720, "DISCOVERY-ROUTE-E2E-001 failed: keyboard movement could not reach interpretation point.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "DISCOVERY-ROUTE-E2E-001 failed: interpretation prompt was not visible.");
            yield return PlayerFlowE2EDriver.PressInteractKey(keyboard);
            var choicePanel = Object.FindFirstObjectByType<ClueInterpretationChoicePanel>(FindObjectsInactive.Include);
            Assert.IsNotNull(choicePanel, "DISCOVERY-ROUTE-E2E-001 failed: interpretation choice UI did not open.");
            yield return PlayerFlowE2EDriver.ClickButton(FindChoiceButton(choicePanel, InterpretationButtonText));
            var interpretationRuntime = interpretationInteractor.GetComponent<ClueInterpretationInteractor>();
            Assert.AreEqual(ClueInterpretationResultKind.Completed, interpretationRuntime.LastResult.Kind, "DISCOVERY-ROUTE-E2E-001 failed: visible interpretation choice did not complete.");
            Assert.IsTrue(ClueInterpretationProgressPersistence.LoadOrCreate(TestSlotId, playerId).GetRecord(interpretationRuntime.LastResult.InterpretationId).Completed, "DISCOVERY-ROUTE-E2E-001 failed: interpretation completion was not saved.");
            Assert.IsTrue(WorldStateProgressPersistence.LoadOrCreate(TestSlotId, playerId).IsActive(worldFlag), "DISCOVERY-ROUTE-E2E-001 failed: interpretation did not activate route unlock world state.");

            yield return PlayerFlowE2EDriver.WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.Board.transform.position, 0.25f, 720, "DISCOVERY-ROUTE-E2E-001 failed: keyboard movement could not reach Daily Plan board.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "DISCOVERY-ROUTE-E2E-001 failed: Daily Plan board prompt was not visible.");
            yield return PlayerFlowE2EDriver.PressInteractKey(keyboard);
            yield return WaitForDailyPlanPanelOpen(3f, "DISCOVERY-ROUTE-E2E-001 failed: Daily Plan panel did not open.");
            var routeButton = DailyPlanE2EDriver.FindRouteButton(runtime.DailyPanel, RouteLabel, "DISCOVERY-ROUTE-E2E-001 failed: unlocked route was not visible in Daily Plan choices.");
            yield return PlayerFlowE2EDriver.ClickButton(routeButton);
            Assert.AreEqual(RouteId, runtime.Daily.Progress.SelectedRouteId, "DISCOVERY-ROUTE-E2E-001 failed: clicked unlocked route was not selected.");

            runtime.ObjectiveProvider.RefreshNow();
            yield return PlayerFlowE2EDriver.PressTab(keyboard);
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(runtime.Journal.transform, "ObjectiveCategory_Goals"));
            yield return PlayerFlowE2EDriver.ClickButton(ObjectiveJournalE2EDriver.FindRowButtonContaining(runtime.Journal, RouteObjectiveTitle, "DISCOVERY-ROUTE-E2E-001 failed: Objective Journal Goals did not include selected unlocked route guidance."));
            StringAssert.Contains(RouteObjectiveTitle, runtime.Journal.VisibleText, "DISCOVERY-ROUTE-E2E-001 failed: Objective Journal Goals did not include selected unlocked route guidance.");
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(runtime.Journal.transform, "ObjectiveTrackButton"));
            Assert.AreEqual(1, Object.FindObjectsByType<TrackedObjectiveHud>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, "DISCOVERY-ROUTE-E2E-001 failed: tracking created more than one HUD instance.");
            Assert.IsTrue(runtime.TrackedHud.IsVisible, "DISCOVERY-ROUTE-E2E-001 failed: tracked HUD did not show one tracked route objective.");
            StringAssert.Contains(RouteObjectiveTitle, runtime.TrackedHud.VisibleText);
            ScreenshotEvidence.Capture(ScreenshotPath);
            yield return ScreenshotEvidence.WaitForNonZeroFile(ScreenshotPath, 3f, "DISCOVERY-ROUTE-E2E-001 failed: Game View screenshot evidence was not written.");

            int interpretationCount = ClueInterpretationProgressPersistence.LoadOrCreate(TestSlotId, playerId).ToSaveData().Records.Length;
            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return PlayerFlowE2EDriver.LoadScene("MainMenu");
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton("SaveSlotCard_" + TestSlotId, "LoadButton"));
            yield return PlayerFlowE2EDriver.WaitForScene("Town", 20f, "DISCOVERY-ROUTE-E2E-001 failed: SaveSlot Load did not re-enter Town.");
            EnsureTownInstallers();
            yield return WaitForRuntime(15f);

            var reloaded = FindRuntime();
            string reloadedPlayerId = ResolvePlayerId(reloaded.Player);
            Assert.IsTrue(WorldStateProgressPersistence.LoadOrCreate(TestSlotId, reloadedPlayerId).IsActive(worldFlag), "DISCOVERY-ROUTE-E2E-001 failed: world-state unlock did not survive reload.");
            Assert.AreEqual(interpretationCount, ClueInterpretationProgressPersistence.LoadOrCreate(TestSlotId, reloadedPlayerId).ToSaveData().Records.Length, "DISCOVERY-ROUTE-E2E-001 failed: interpretation records duplicated after reload.");
            yield return PlayerFlowE2EDriver.WalkPlayerWithKeyboardTo(reloaded.Player, keyboard, reloaded.Board.transform.position, 0.25f, 720, "DISCOVERY-ROUTE-E2E-001 failed: keyboard movement could not reach Daily Plan board after reload.");
            reloaded.Router.RefreshPromptNow();
            yield return PlayerFlowE2EDriver.PressInteractKey(keyboard);
            yield return WaitForDailyPlanPanelOpen(3f, "DISCOVERY-ROUTE-E2E-001 failed: Daily Plan panel did not open after reload.");
            DailyPlanE2EDriver.FindRouteButton(reloaded.DailyPanel, RouteLabel, "DISCOVERY-ROUTE-E2E-001 failed: unlocked route availability did not restore after reload.");
        }

        private IEnumerator StartNewTownFromSaveSlotUi()
        {
            yield return PlayerFlowE2EDriver.LoadScene("MainMenu");
            PlayerFlowE2EDriver.EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton("SaveSlotCard_" + TestSlotId, "NewGameButton"));
            var creator = GameObject.Find("SpumCharacterCreatorRoot");
            if (creator != null) yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(creator.transform, "ConfirmButton"));
            yield return PlayerFlowE2EDriver.WaitForScene("Town", 20f, "DISCOVERY-ROUTE-E2E-001 failed: SaveSlot New Game did not enter Town.");
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

        private static Button FindChoiceButton(ClueInterpretationChoicePanel panel, string containsLower)
        {
            for (int i = 0; i < panel.ButtonCountForTests; i++)
            {
                var button = panel.GetButtonForTests(i);
                var text = button != null ? button.GetComponentInChildren<Text>(true) : null;
                if (text != null && text.text.ToLowerInvariant().Contains(containsLower)) return button;
            }

            Assert.Fail("Missing visible clue interpretation choice containing: " + containsLower + "\n" + panel.TextForTests);
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

        private static string ResolvePlayerId(GameObject player)
        {
            var identity = player.GetComponent<PlayerIdentity>();
            return identity != null && !string.IsNullOrEmpty(identity.PlayerId) ? identity.PlayerId : PlayerIdentity.DefaultPlayerId;
        }

        private static void CleanupResidue()
        {
            PlayerFlowE2EDriver.DestroyAllNamed("SaveSlotSelectPanel");
            PlayerFlowE2EDriver.DestroyAllNamed("SaveSlotCanvas");
            PlayerFlowE2EDriver.DestroyAllNamed("SaveSlotSelectRoot");
            PlayerFlowE2EDriver.DestroyAllNamed("EventSystem");
            PlayerFlowE2EDriver.DestroyAllNamed("Player");
            PlayerFlowE2EDriver.DestroyAllNamed("DiscoveryRouteUnlock_ClueSource");
            PlayerFlowE2EDriver.DestroyAllNamed("DiscoveryRouteUnlock_InterpretationPoint");
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