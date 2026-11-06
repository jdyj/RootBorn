using System.Collections;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.Game.WorldState;
using Rootborn.Tests.PlayMode.EndToEnd.Support;
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
    public sealed class QuestRouteRewardE2EScenarioTests : Rootborn.Tests.PlayMode.PlayModeInputFixture
    {
        private const string TestSlotId = "slot-0";
        private const string ChainId = "questchain.test.career-interest.library";
        private const string ChainCardName = "QuestChainCard_questchain_test_career_interest_library";
        private const string WorldStateId = "world.questchain.library-route-reward";
        private const string RouteId = "daily.route.questchain.library-reward";
        private const string RouteLabel = "daily.route.questchain.library-reward";
        private const string RouteObjectiveTitle = "objective.daily.questchain.library-reward.title";
        private const string ScreenshotPath = "Builds/Logs/quest-route-reward-e2e.png";

        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.timeScale = 1f;
            CleanupResidue();
            PlayerFlowE2EDriver.EnsurePassiveEventSystem();
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-quest-route-reward-e2e", System.Guid.NewGuid().ToString("N"));
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

        [Ignore(LegacyPremise.ObjectiveJournal + "퀘스트 목록 열기 버튼(QuestChainLogOpenButton)은 더 이상 만들지 않음")]
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator QUEST_ROUTE_REWARD_E2E_001_CompleteChainUnlockRouteReload()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            yield return StartNewTownFromSaveSlotUi();
            var runtime = FindRuntime();
            string playerId = ResolvePlayerId(runtime.Player);
            var flag = FindWorldState(WorldStateId);
            Assert.IsFalse(WorldStateProgressPersistence.LoadOrCreate(TestSlotId, playerId).IsActive(flag), "QUEST-ROUTE-REWARD-E2E-001 failed: reward route world-state should start inactive.");

            yield return OpenDailyPlanBoard(runtime, keyboard, "QUEST-ROUTE-REWARD-E2E-001 failed: Daily Plan board did not open before quest reward.");
            AssertRouteMissing(runtime.DailyPanel, RouteLabel, "QUEST-ROUTE-REWARD-E2E-001 failed: quest reward route was visible before reward claim.");
            runtime.DailyPanel.gameObject.SetActive(false);

            yield return PlayerFlowE2EDriver.ClickButton(FindRootButton("QuestChainLogOpenButton"));
            AssertPanelContains(runtime.ChainPanel.transform, ChainId, "QUEST-ROUTE-REWARD-E2E-001 failed: quest-chain panel did not show the route reward chain.");
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(runtime.ChainPanel.transform, ChainCardName));
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(runtime.ChainPanel.transform, "QuestChainAcceptButton"));
            AssertPanelContains(runtime.ChainPanel.transform, "Active", "QUEST-ROUTE-REWARD-E2E-001 failed: accept button did not activate quest chain.");
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(runtime.ChainPanel.transform, "QuestChainTrackButton"));
            AssertPanelContains(runtime.ChainPanel.transform, "Tracked", "QUEST-ROUTE-REWARD-E2E-001 failed: track button did not track quest chain.");
            runtime.ChainPanel.Hide();

            yield return PlayerFlowE2EDriver.WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.Library.transform.position, 0.25f, 720, "QUEST-ROUTE-REWARD-E2E-001 failed: keyboard movement could not reach Library.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "QUEST-ROUTE-REWARD-E2E-001 failed: Library prompt was not visible.");
            yield return PlayerFlowE2EDriver.PressInteractKey(keyboard);
            yield return WaitForLocationPanel(3f, "QUEST-ROUTE-REWARD-E2E-001 failed: location activity panel did not open.");
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(runtime.LocationPanel.transform, "LocationActivityButton_0"));
            yield return null;
            AssertPanelContains(runtime.ChainPanel.transform, "Completed", "QUEST-ROUTE-REWARD-E2E-001 failed: actual location activity did not complete quest chain.");

            yield return PlayerFlowE2EDriver.ClickButton(FindRootButton("QuestChainLogOpenButton"));
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(runtime.ChainPanel.transform, "QuestChainClaimRewardButton"));
            AssertPanelContains(runtime.ChainPanel.transform, "Reward: Claimed", "QUEST-ROUTE-REWARD-E2E-001 failed: claim button did not mark quest-chain reward claimed.");
            Assert.IsFalse(PlayerFlowE2EDriver.FindButton(runtime.ChainPanel.transform, "QuestChainClaimRewardButton").interactable, "QUEST-ROUTE-REWARD-E2E-001 failed: completion reward stayed claimable after claim.");
            Assert.IsTrue(WorldStateProgressPersistence.LoadOrCreate(TestSlotId, playerId).IsActive(flag), "QUEST-ROUTE-REWARD-E2E-001 failed: reward claim did not persist route unlock world-state.");
            runtime.ChainPanel.Hide();

            yield return OpenDailyPlanBoard(runtime, keyboard, "QUEST-ROUTE-REWARD-E2E-001 failed: Daily Plan board did not open after quest reward claim.");
            var routeButton = DailyPlanE2EDriver.FindRouteButton(runtime.DailyPanel, RouteLabel, "QUEST-ROUTE-REWARD-E2E-001 failed: rewarded route was not visible in Daily Plan choices.");
            yield return PlayerFlowE2EDriver.ClickButton(routeButton);
            Assert.AreEqual(RouteId, runtime.Daily.Progress.SelectedRouteId, "QUEST-ROUTE-REWARD-E2E-001 failed: clicked rewarded route was not selected.");

            runtime.ObjectiveProvider.RefreshNow();
            yield return PlayerFlowE2EDriver.PressTab(keyboard);
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(runtime.Journal.transform, "ObjectiveCategory_Goals"));
            yield return PlayerFlowE2EDriver.ClickButton(ObjectiveJournalE2EDriver.FindRowButtonContaining(runtime.Journal, RouteObjectiveTitle, "QUEST-ROUTE-REWARD-E2E-001 failed: Objective Journal Goals did not include selected rewarded route."));
            StringAssert.Contains(RouteObjectiveTitle, runtime.Journal.VisibleText, "QUEST-ROUTE-REWARD-E2E-001 failed: Objective Journal did not show selected rewarded route detail.");
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(runtime.Journal.transform, "ObjectiveTrackButton"));
            Assert.AreEqual(1, Object.FindObjectsByType<TrackedObjectiveHud>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, "QUEST-ROUTE-REWARD-E2E-001 failed: tracking created more than one HUD instance.");
            StringAssert.Contains(RouteObjectiveTitle, runtime.TrackedHud.VisibleText, "QUEST-ROUTE-REWARD-E2E-001 failed: tracked HUD did not include selected rewarded route title.");
            ScreenshotEvidence.Capture(ScreenshotPath);
            yield return ScreenshotEvidence.WaitForNonZeroFile(ScreenshotPath, 3f, "QUEST-ROUTE-REWARD-E2E-001 failed: Game View screenshot evidence was not written.");

            AssertFileContains("quest-chain-progress.json", "completion", "QUEST-ROUTE-REWARD-E2E-001 failed: quest-chain reward claimed flag was not saved.");
            AssertFileContains(WorldStateProgressPersistence.FileNameFor(playerId), WorldStateId, "QUEST-ROUTE-REWARD-E2E-001 failed: world-state reward flag was not saved.");
            AssertFileContains(DailyPlanProgressPersistence.FileNameFor(playerId), RouteId, "QUEST-ROUTE-REWARD-E2E-001 failed: selected rewarded route was not saved.");

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return PlayerFlowE2EDriver.LoadScene("MainMenu");
            PlayerFlowE2EDriver.EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton("SaveSlotCard_" + TestSlotId, "LoadButton"));
            yield return PlayerFlowE2EDriver.WaitForScene("Town", 20f, "QUEST-ROUTE-REWARD-E2E-001 failed: SaveSlot Load did not re-enter Town.");
            EnsureTownInstallers();
            yield return WaitForRuntime(15f);

            var reloaded = FindRuntime();
            string reloadedPlayerId = ResolvePlayerId(reloaded.Player);
            Assert.IsTrue(WorldStateProgressPersistence.LoadOrCreate(TestSlotId, reloadedPlayerId).IsActive(flag), "QUEST-ROUTE-REWARD-E2E-001 failed: reward route world-state did not survive reload.");
            Assert.AreEqual(RouteId, reloaded.Daily.Progress.SelectedRouteId, "QUEST-ROUTE-REWARD-E2E-001 failed: selected rewarded route did not survive reload.");
            yield return PlayerFlowE2EDriver.ClickButton(FindRootButton("QuestChainLogOpenButton"));
            AssertPanelContains(reloaded.ChainPanel.transform, "Reward: Claimed", "QUEST-ROUTE-REWARD-E2E-001 failed: reward claim state did not survive reload.");
            Assert.IsFalse(PlayerFlowE2EDriver.FindButton(reloaded.ChainPanel.transform, "QuestChainClaimRewardButton").interactable, "QUEST-ROUTE-REWARD-E2E-001 failed: reward became claimable again after reload.");
            reloaded.ChainPanel.Hide();
            yield return OpenDailyPlanBoard(reloaded, keyboard, "QUEST-ROUTE-REWARD-E2E-001 failed: Daily Plan board did not open after reload.");
            DailyPlanE2EDriver.FindRouteButton(reloaded.DailyPanel, RouteLabel, "QUEST-ROUTE-REWARD-E2E-001 failed: rewarded route availability did not restore after reload.");
        }

        private IEnumerator StartNewTownFromSaveSlotUi()
        {
            yield return PlayerFlowE2EDriver.LoadScene("MainMenu");
            PlayerFlowE2EDriver.EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton("SaveSlotCard_" + TestSlotId, "NewGameButton"));
            var creator = GameObject.Find("SpumCharacterCreatorRoot") ?? GameObject.Find("SpumCharacterCreatorPanel");
            if (creator != null) yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(creator.transform, "ConfirmButton"));
            yield return PlayerFlowE2EDriver.WaitForScene("Town", 20f, "QUEST-ROUTE-REWARD-E2E-001 failed: SaveSlot New Game did not enter Town.");
            EnsureTownInstallers();
            yield return WaitForRuntime(15f);
        }

        private static void EnsureTownInstallers()
        {
            StudentDayRuntimeInstaller.EnsureForActiveScene();
            LocationNpcRuntimeInstaller.EnsureForActiveScene();
            DailyPlanRuntimeInstaller.EnsureForActiveScene();
            QuestChainRuntimeInstaller.EnsureForActiveScene();
            CampaignRuntimeInstaller.EnsureForActiveScene();
        }

        private static IEnumerator WaitForRuntime(float timeoutSeconds)
        {
            float elapsed = 0f;
            string lastState = string.Empty;
            while (elapsed < timeoutSeconds)
            {
                var player = GameObject.Find("Player");
                var dailyBoard = GameObject.Find("DailyPlanBoard");
                var library = GameObject.Find(LocationNpcRuntimeInstaller.LocationObjectName("location.library"));
                var chainPanel = Object.FindFirstObjectByType<QuestChainLogPanel>(FindObjectsInactive.Include);
                var dailyPanel = Object.FindFirstObjectByType<DailyPlanPanel>(FindObjectsInactive.Include);
                var locationPanel = Object.FindFirstObjectByType<LocationIdentityPanel>(FindObjectsInactive.Include);
                var journal = Object.FindFirstObjectByType<ObjectiveJournalPanel>(FindObjectsInactive.Include);
                var provider = Object.FindFirstObjectByType<ObjectiveJournalRuntimeProvider>(FindObjectsInactive.Include);
                var hud = Object.FindFirstObjectByType<TrackedObjectiveHud>(FindObjectsInactive.Include);
                lastState = "player=" + (player != null)
                    + " router=" + (player != null && player.GetComponent<PlayerInteractionRouter>() != null)
                    + " daily=" + (player != null && player.GetComponent<DailyPlanProgressComponent>() != null)
                    + " dailyBoard=" + (dailyBoard != null)
                    + " library=" + (library != null)
                    + " chainPanel=" + (chainPanel != null)
                    + " dailyPanel=" + (dailyPanel != null)
                    + " locationPanel=" + (locationPanel != null)
                    + " journal=" + (journal != null)
                    + " provider=" + (provider != null)
                    + " hud=" + (hud != null)
                    + " eventSystem=" + (EventSystem.current != null)
                    + " questButton=" + (GameObject.Find("QuestChainLogOpenButton") != null);

                if (player != null && player.GetComponent<PlayerInteractionRouter>() != null && player.GetComponent<DailyPlanProgressComponent>() != null && dailyBoard != null && library != null && chainPanel != null && dailyPanel != null && locationPanel != null && journal != null && provider != null && hud != null && EventSystem.current != null && GameObject.Find("QuestChainLogOpenButton") != null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("QUEST-ROUTE-REWARD-E2E-001 failed: Town runtime did not install within timeout. Last state: " + lastState);
        }

        private static QuestRouteRuntime FindRuntime()
        {
            var player = GameObject.Find("Player");
            return new QuestRouteRuntime(
                player,
                player.GetComponent<PlayerInteractionRouter>(),
                player.GetComponent<DailyPlanProgressComponent>(),
                GameObject.Find("DailyPlanBoard").GetComponent<DailyPlanBoardInteractor>(),
                GameObject.Find(LocationNpcRuntimeInstaller.LocationObjectName("location.library")).GetComponent<LocationActivityInteractor>(),
                Object.FindFirstObjectByType<QuestChainLogPanel>(FindObjectsInactive.Include),
                Object.FindFirstObjectByType<DailyPlanPanel>(FindObjectsInactive.Include),
                Object.FindFirstObjectByType<LocationIdentityPanel>(FindObjectsInactive.Include),
                Object.FindFirstObjectByType<ObjectiveJournalPanel>(FindObjectsInactive.Include),
                Object.FindFirstObjectByType<ObjectiveJournalRuntimeProvider>(FindObjectsInactive.Include),
                Object.FindFirstObjectByType<TrackedObjectiveHud>(FindObjectsInactive.Include));
        }

        private static IEnumerator OpenDailyPlanBoard(QuestRouteRuntime runtime, Keyboard keyboard, string failure)
        {
            yield return PlayerFlowE2EDriver.WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.DailyBoard.transform.position, 0.25f, 720, failure);
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, failure + " Prompt was not visible.");
            yield return PlayerFlowE2EDriver.PressInteractKey(keyboard);
            float elapsed = 0f;
            while (elapsed < 3f)
            {
                if (runtime.DailyPanel != null && runtime.DailyPanel.gameObject.activeSelf) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail(failure);
        }

        private static IEnumerator WaitForLocationPanel(float timeoutSeconds, string failure)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var panel = Object.FindFirstObjectByType<LocationIdentityPanel>(FindObjectsInactive.Include);
                if (panel != null && panel.IsOpen) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail(failure);
        }

        private static WorldStateFlagDefinition FindWorldState(string id)
        {
            var registry = Rootborn.Game.Managers.Managers.Data.Registry;
            for (int i = 0; i < registry.WorldStateFlags.Length; i++) if (registry.WorldStateFlags[i] != null && registry.WorldStateFlags[i].Id == id) return registry.WorldStateFlags[i];
            Assert.Fail("Missing world-state flag: " + id);
            return null;
        }

        private static string ResolvePlayerId(GameObject player)
        {
            var identity = player != null ? player.GetComponent<PlayerIdentity>() : null;
            return identity != null && !string.IsNullOrEmpty(identity.PlayerId) ? identity.PlayerId : PlayerIdentity.DefaultPlayerId;
        }

        private static void AssertRouteMissing(DailyPlanPanel panel, string label, string failure)
        {
            for (int i = 0; i < panel.RouteRowCountForTests; i++)
            {
                var button = panel.GetRouteButtonForTests(i);
                var text = button != null ? button.GetComponentInChildren<Text>(true) : null;
                if (text != null && text.text == label) Assert.Fail(failure + "\n" + DailyPlanE2EDriver.DescribeRouteRows(panel));
            }
        }

        private void AssertFileContains(string fileName, string expected, string failure)
        {
            string path = Path.Combine(_saveRoot, TestSlotId, fileName);
            Assert.IsTrue(File.Exists(path), failure + " Missing file: " + path);
            StringAssert.Contains(expected, File.ReadAllText(path), failure);
        }

        private static Button FindRootButton(string name)
        {
            var go = GameObject.Find(name);
            Assert.IsNotNull(go, "UI button root missing: " + name);
            var button = go.GetComponent<Button>();
            Assert.IsNotNull(button, name + " does not have Button component.");
            return button;
        }

        private static void AssertPanelContains(Transform root, string expected, string failure)
        {
            var texts = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++) if (texts[i].text.Contains(expected)) return;
            Assert.Fail(failure + " Missing text: " + expected + "\n" + PlayerFlowE2EDriver.DescribeButtons(root));
        }

        private static void CleanupResidue()
        {
            PlayerFlowE2EDriver.DestroyAllNamed("SaveSlotSelectPanel");
            PlayerFlowE2EDriver.DestroyAllNamed("SaveSlotCanvas");
            PlayerFlowE2EDriver.DestroyAllNamed("SaveSlotSelectRoot");
            PlayerFlowE2EDriver.DestroyAllNamed("EventSystem");
            PlayerFlowE2EDriver.DestroyAllNamed("Player");
        }

        private readonly struct QuestRouteRuntime
        {
            public readonly GameObject Player;
            public readonly PlayerInteractionRouter Router;
            public readonly DailyPlanProgressComponent Daily;
            public readonly DailyPlanBoardInteractor DailyBoard;
            public readonly LocationActivityInteractor Library;
            public readonly QuestChainLogPanel ChainPanel;
            public readonly DailyPlanPanel DailyPanel;
            public readonly LocationIdentityPanel LocationPanel;
            public readonly ObjectiveJournalPanel Journal;
            public readonly ObjectiveJournalRuntimeProvider ObjectiveProvider;
            public readonly TrackedObjectiveHud TrackedHud;

            public QuestRouteRuntime(GameObject player, PlayerInteractionRouter router, DailyPlanProgressComponent daily, DailyPlanBoardInteractor dailyBoard, LocationActivityInteractor library, QuestChainLogPanel chainPanel, DailyPlanPanel dailyPanel, LocationIdentityPanel locationPanel, ObjectiveJournalPanel journal, ObjectiveJournalRuntimeProvider objectiveProvider, TrackedObjectiveHud trackedHud)
            {
                Player = player;
                Router = router;
                Daily = daily;
                DailyBoard = dailyBoard;
                Library = library;
                ChainPanel = chainPanel;
                DailyPanel = dailyPanel;
                LocationPanel = locationPanel;
                Journal = journal;
                ObjectiveProvider = objectiveProvider;
                TrackedHud = trackedHud;
            }
        }
    }
}
