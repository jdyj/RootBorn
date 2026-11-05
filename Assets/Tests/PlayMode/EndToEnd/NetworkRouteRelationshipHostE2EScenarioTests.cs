using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Bootstrap;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.Tests.PlayMode.EndToEnd.Support;
using Rootborn.UI.MainMenu;
using Rootborn.UI.Objectives;
using Rootborn.UI.StudentLife;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    public sealed class NetworkRouteRelationshipHostE2EScenarioTests : Rootborn.Tests.PlayMode.PlayModeInputFixture
    {
        private const string TestSlotId = "host-route-relationship-slot";
        private const string CareerId = "career.interior";
        private const string RouteId = "daily.route.career.interior-practice";
        private const string RouteLabel = "daily.route.career.interior-practice";
        private const string RouteObjectiveTitle = "objective.daily.career.interior-practice.title";
        private const string RelationshipId = "relationship.first-guide.trust";
        private const string HelpActionId = "town.help.first-guide-errand";
        private const string MilestoneId = "milestone.first-guide-trust";
        private const string ScreenshotPath = "Builds/Logs/network-route-relationship-host-e2e.png";

        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.timeScale = 1f;
            CleanupResidue();
            ShutdownNetworkManagers();
            PlayerFlowE2EDriver.EnsurePassiveEventSystem();
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-host-route-relationship-e2e", System.Guid.NewGuid().ToString("N"));
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
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) NetworkManager.Singleton.Shutdown();
            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            SaveService.SetRootDirectoryForTests(null);
            SaveSlotSelectPanel.SetSaveRootForTests(null);
            CleanupResidue();
            ShutdownNetworkManagers();
            InputSystemUiModulePlayModeTestGuard.UninstallForCurrentTest();
            LogAssert.ignoreFailingMessages = false;
            yield return null;
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator NETWORK_ROUTE_RELATIONSHIP_PM_001_HostProgressSaveReload()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            yield return StartHostTownFromMainMenu();
            yield return WaitForHostNetwork(5f, "NETWORK-ROUTE-REL-HOST-001 failed: HostButton did not start a listening host session.");
            EnsureTownInstallers();
            yield return WaitForRuntime(15f);

            var runtime = FindRuntime();
            var identity = runtime.Player.GetComponent<PlayerIdentity>();
            Assert.IsNotNull(identity, "NETWORK-ROUTE-REL-HOST-001 failed: host Town player is missing PlayerIdentity.");
            Assert.AreEqual(0UL, NetworkManager.Singleton.LocalClientId, "NETWORK-ROUTE-REL-HOST-001 failed: host local client id should be 0.");
            Assert.AreEqual("client-0", identity.PlayerId, "NETWORK-ROUTE-REL-HOST-001 failed: host player identity did not bind to the network local client.");

            yield return UnlockAndSelectCareerRoute(runtime, keyboard);
            yield return AdvanceFirstGuideRelationship(runtime, keyboard);
            yield return OpenJournalAndTrackRoute(runtime, keyboard);
            yield return CaptureScreenshotEvidence();
            yield return CompleteDayEnd(runtime, keyboard);

            int relationshipAfter = runtime.Student.Progress.GetRelationshipValue(FindRelationshipOutcome(runtime).Relationship);
            Assert.Greater(relationshipAfter, 0, "NETWORK-ROUTE-REL-HOST-001 failed: First Guide relationship did not advance under host mode.");
            Assert.AreEqual(RouteId, runtime.Daily.Progress.SelectedRouteId, "NETWORK-ROUTE-REL-HOST-001 failed: career route was not selected under host mode.");
            Assert.AreEqual(1, Object.FindObjectsByType<TrackedObjectiveHud>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, "NETWORK-ROUTE-REL-HOST-001 failed: host tracking created duplicate HUD instances before reload.");

            string studentPath = Path.Combine(_saveRoot, TestSlotId, StudentLifeFileNameFor(identity.PlayerId));
            string dailyPath = Path.Combine(_saveRoot, TestSlotId, DailyPlanProgressPersistence.FileNameFor(identity.PlayerId));
            Assert.IsTrue(File.Exists(studentPath), "NETWORK-ROUTE-REL-HOST-001 failed: host student save file was not written. Path: " + studentPath);
            Assert.IsTrue(File.Exists(dailyPath), "NETWORK-ROUTE-REL-HOST-001 failed: host Daily Plan save file was not written. Path: " + dailyPath);
            StringAssert.Contains(RelationshipId, File.ReadAllText(studentPath), "NETWORK-ROUTE-REL-HOST-001 failed: host student save missing relationship state.");
            StringAssert.Contains(RouteId, File.ReadAllText(dailyPath), "NETWORK-ROUTE-REL-HOST-001 failed: host Daily Plan save missing selected route.");

            yield return RestartHostTownFromSameSlot();
            yield return WaitForHostNetwork(5f, "NETWORK-ROUTE-REL-HOST-001 failed: restarted host session did not listen.");
            EnsureTownInstallers();
            yield return WaitForRuntime(15f);

            var reloaded = FindRuntime();
            Assert.AreEqual(RouteId, reloaded.Daily.Progress.SelectedRouteId, "NETWORK-ROUTE-REL-HOST-001 failed: host route selection did not survive restart.");
            Assert.AreEqual(relationshipAfter, reloaded.Student.Progress.GetRelationshipValue(FindRelationshipOutcome(reloaded).Relationship), "NETWORK-ROUTE-REL-HOST-001 failed: host relationship state did not survive restart or was duplicated.");
            yield return PlayerFlowE2EDriver.PressTab(keyboard);
            Assert.AreEqual(1, Object.FindObjectsByType<TrackedObjectiveHud>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, "NETWORK-ROUTE-REL-HOST-001 failed: host restart duplicated tracked HUD instances.");
        }

        private IEnumerator StartHostTownFromMainMenu()
        {
            yield return PlayerFlowE2EDriver.LoadScene("Boot");
            yield return PlayerFlowE2EDriver.WaitForScene("MainMenu", 15f, "NETWORK-ROUTE-REL-HOST-001 failed: Boot did not reach MainMenu.");
            PlayerFlowE2EDriver.EnsurePassiveEventSystem();
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            if (GameBootstrap.Config != null)
            {
                GameBootstrap.Config.SaveSlot = TestSlotId;
                GameBootstrap.Config.Port = 7792;
            }

            yield return PlayerFlowE2EDriver.ClickButton(FindRootButton("HostButton"));
            yield return PlayerFlowE2EDriver.WaitForScene("Town", 20f, "NETWORK-ROUTE-REL-HOST-001 failed: HostButton did not enter Town.");
        }

        private IEnumerator RestartHostTownFromSameSlot()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) NetworkManager.Singleton.Shutdown();
            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            CleanupResidue();
            yield return null;
            yield return StartHostTownFromMainMenu();
        }

        private static void EnsureTownInstallers()
        {
            StudentDayRuntimeInstaller.EnsureForActiveScene();
            LocationNpcRuntimeInstaller.EnsureForActiveScene();
            TownHelpActionRuntimeInstaller.EnsureForActiveScene();
            DailyPlanRuntimeInstaller.EnsureForActiveScene();
            CampaignRuntimeInstaller.EnsureForActiveScene();
        }

        private IEnumerator UnlockAndSelectCareerRoute(HostRouteRelationshipRuntime runtime, Keyboard keyboard)
        {
            Assert.IsFalse(runtime.Student.Progress.IsCareerHintUnlocked(FindCareer(runtime.CareerBoard, CareerId)), "NETWORK-ROUTE-REL-HOST-001 failed: career route condition should start locked under host mode.");
            yield return OpenDailyPlanBoard(runtime, keyboard, "NETWORK-ROUTE-REL-HOST-001 failed: Daily Plan board did not open before host career practice.");
            AssertRouteMissing(runtime.DailyPanel, RouteLabel, "NETWORK-ROUTE-REL-HOST-001 failed: career route was visible before host career practice.");
            runtime.DailyPanel.gameObject.SetActive(false);

            yield return PlayerFlowE2EDriver.WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.CareerBoard.transform.position, 0.25f, 720, "NETWORK-ROUTE-REL-HOST-001 failed: host keyboard movement could not reach Career Practice board.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "NETWORK-ROUTE-REL-HOST-001 failed: host Career Practice prompt was not visible.");
            yield return PlayerFlowE2EDriver.PressInteractKey(keyboard);
            yield return null;
            yield return PlayerFlowE2EDriver.PressInteractKey(keyboard);
            yield return null;
            Assert.IsTrue(runtime.Student.Progress.IsCareerHintUnlocked(FindCareer(runtime.CareerBoard, CareerId)), "NETWORK-ROUTE-REL-HOST-001 failed: host career practice did not unlock career hint.");

            yield return OpenDailyPlanBoard(runtime, keyboard, "NETWORK-ROUTE-REL-HOST-001 failed: Daily Plan board did not reopen after host career practice.");
            var routeButton = DailyPlanE2EDriver.FindRouteButton(runtime.DailyPanel, RouteLabel, "NETWORK-ROUTE-REL-HOST-001 failed: host career route was not visible after practice.");
            yield return PlayerFlowE2EDriver.ClickButton(routeButton);
        }

        private IEnumerator AdvanceFirstGuideRelationship(HostRouteRelationshipRuntime runtime, Keyboard keyboard)
        {
            var relationshipOutcome = FindRelationshipOutcome(runtime);
            int before = runtime.Student.Progress.GetRelationshipValue(relationshipOutcome.Relationship);
            yield return PlayerFlowE2EDriver.WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.Help.transform.position, 0.25f, 720, "NETWORK-ROUTE-REL-HOST-001 failed: host keyboard movement could not reach First Guide help action.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "NETWORK-ROUTE-REL-HOST-001 failed: host help prompt was not visible.");
            StringAssert.Contains("First Guide", runtime.Router.PromptText, "NETWORK-ROUTE-REL-HOST-001 failed: host help prompt did not expose First Guide action.");
            yield return PlayerFlowE2EDriver.PressInteractKey(keyboard);
            yield return null;
            Assert.AreEqual(LifeActivityResultKind.Applied, runtime.Help.LastResult.Kind, "NETWORK-ROUTE-REL-HOST-001 failed: host help action did not apply through player input.");
            Assert.GreaterOrEqual(runtime.Student.Progress.GetRelationshipValue(relationshipOutcome.Relationship), before + relationshipOutcome.Delta, "NETWORK-ROUTE-REL-HOST-001 failed: host help action did not advance First Guide relationship.");
        }

        private IEnumerator CompleteDayEnd(HostRouteRelationshipRuntime runtime, Keyboard keyboard)
        {
            yield return PlayerFlowE2EDriver.WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.DayEnd.transform.position, 0.25f, 720, "NETWORK-ROUTE-REL-HOST-001 failed: host keyboard movement could not reach day-end board.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "NETWORK-ROUTE-REL-HOST-001 failed: host day-end prompt was not visible.");
            yield return PlayerFlowE2EDriver.PressInteractKey(keyboard);
            float elapsed = 0f;
            while (elapsed < 3f)
            {
                var panel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
                if (panel != null && panel.IsOpen) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("NETWORK-ROUTE-REL-HOST-001 failed: day result panel did not open under host mode.");
        }

        private IEnumerator OpenJournalAndTrackRoute(HostRouteRelationshipRuntime runtime, Keyboard keyboard)
        {
            runtime.ObjectiveProvider.RefreshNow();
            yield return PlayerFlowE2EDriver.PressTab(keyboard);
            Assert.IsTrue(runtime.Journal.IsVisible, "NETWORK-ROUTE-REL-HOST-001 failed: Tab did not open Objective Journal under host mode.");
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(runtime.Journal.transform, "ObjectiveCategory_Goals"));
            yield return PlayerFlowE2EDriver.ClickButton(ObjectiveJournalE2EDriver.FindRowButtonContaining(runtime.Journal, RouteObjectiveTitle, "NETWORK-ROUTE-REL-HOST-001 failed: host Journal Goals did not include selected route."));
            StringAssert.Contains(RouteObjectiveTitle, runtime.Journal.VisibleText, "NETWORK-ROUTE-REL-HOST-001 failed: host Journal did not show selected route detail.");
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(runtime.Journal.transform, "ObjectiveTrackButton"));
            StringAssert.Contains(RouteObjectiveTitle, runtime.TrackedHud.VisibleText, "NETWORK-ROUTE-REL-HOST-001 failed: host tracked HUD did not show selected route.");
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(runtime.Journal.transform, "ObjectiveCategory_Hints"));
            yield return PlayerFlowE2EDriver.ClickButton(ObjectiveJournalE2EDriver.FindRowButtonContaining(runtime.Journal, MilestoneId, "NETWORK-ROUTE-REL-HOST-001 failed: host Journal Hints did not include First Guide milestone."));
            StringAssert.Contains(HelpActionId, runtime.Journal.VisibleText, "NETWORK-ROUTE-REL-HOST-001 failed: host Journal did not expose First Guide relationship action.");
        }

        private static IEnumerator OpenDailyPlanBoard(HostRouteRelationshipRuntime runtime, Keyboard keyboard, string failure)
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

        private static IEnumerator WaitForHostNetwork(float timeoutSeconds, string failure)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var manager = NetworkManager.Singleton;
                if (manager != null && manager.IsListening && manager.IsServer && manager.IsClient) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail(failure + " manager=" + (NetworkManager.Singleton != null) + " scene=" + SceneManager.GetActiveScene().name);
        }

        private static IEnumerator WaitForRuntime(float timeoutSeconds)
        {
            float elapsed = 0f;
            string lastState = string.Empty;
            while (elapsed < timeoutSeconds)
            {
                var runtime = TryFindRuntime(out lastState);
                if (runtime.IsValid) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("NETWORK-ROUTE-REL-HOST-001 setup failed: runtime objects were not ready. Last state: " + lastState);
        }

        private static HostRouteRelationshipRuntime FindRuntime()
        {
            var runtime = TryFindRuntime(out var lastState);
            Assert.IsTrue(runtime.IsValid, "NETWORK-ROUTE-REL-HOST-001 failed: runtime missing. Last state: " + lastState);
            return runtime;
        }

        private static HostRouteRelationshipRuntime TryFindRuntime(out string lastState)
        {
            var player = GameObject.Find("Player");
            var dailyBoard = GameObject.Find("DailyPlanBoard");
            var careerBoard = GameObject.Find("CareerPracticeBoard");
            var help = GameObject.Find("TownHelpActionPoint");
            var dayEnd = GameObject.Find("StudentDayEndBoard");
            var journal = Object.FindFirstObjectByType<ObjectiveJournalPanel>(FindObjectsInactive.Include);
            var provider = Object.FindFirstObjectByType<ObjectiveJournalRuntimeProvider>(FindObjectsInactive.Include);
            var trackedHud = Object.FindFirstObjectByType<TrackedObjectiveHud>(FindObjectsInactive.Include);
            var dailyPanel = Object.FindFirstObjectByType<DailyPlanPanel>(FindObjectsInactive.Include);
            lastState = "scene=" + SceneManager.GetActiveScene().name
                + " player=" + (player != null)
                + " network=" + (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                + " router=" + (player != null && player.GetComponent<PlayerInteractionRouter>() != null)
                + " student=" + (player != null && player.GetComponent<StudentLifeProgressComponent>() != null)
                + " daily=" + (player != null && player.GetComponent<DailyPlanProgressComponent>() != null)
                + " dailyBoard=" + (dailyBoard != null)
                + " careerBoard=" + (careerBoard != null)
                + " help=" + (help != null)
                + " dayEnd=" + (dayEnd != null)
                + " dailyPanel=" + (dailyPanel != null)
                + " journal=" + (journal != null)
                + " provider=" + (provider != null)
                + " trackedHud=" + (trackedHud != null)
                + " eventSystem=" + (EventSystem.current != null);
            if (player == null || dailyBoard == null || careerBoard == null || help == null || dayEnd == null || journal == null || provider == null || trackedHud == null || dailyPanel == null || EventSystem.current == null) return default;

            var runtime = new HostRouteRelationshipRuntime(
                player,
                player.GetComponent<PlayerInteractionRouter>(),
                player.GetComponent<StudentLifeProgressComponent>(),
                player.GetComponent<DailyPlanProgressComponent>(),
                dailyBoard.GetComponent<DailyPlanBoardInteractor>(),
                careerBoard.GetComponent<CareerPracticeBoard>(),
                help.GetComponent<TownHelpActionInteractor>(),
                dayEnd.GetComponent<StudentDayEndInteractor>(),
                dailyPanel,
                journal,
                provider,
                trackedHud);
            return runtime.IsValid ? runtime : default;
        }

        private static CareerDefinition FindCareer(CareerPracticeBoard board, string id)
        {
            for (int i = 0; i < board.Practices.Count; i++)
            {
                var practice = board.Practices[i];
                if (practice != null && practice.CareerHint != null && practice.CareerHint.Id == id) return practice.CareerHint;
            }

            Assert.Fail("Missing career on CareerPracticeBoard: " + id);
            return null;
        }

        private static HelpActionRelationshipDeltaOutcome FindRelationshipOutcome(HostRouteRelationshipRuntime runtime)
        {
            var relationshipOutcome = runtime.Help.Action.Outcomes.OfType<HelpActionRelationshipDeltaOutcome>().FirstOrDefault(outcome => outcome.Relationship != null && outcome.Relationship.Id == RelationshipId);
            Assert.IsNotNull(relationshipOutcome, "NETWORK-ROUTE-REL-HOST-001 failed: First Guide help action is missing a relationship outcome.");
            return relationshipOutcome;
        }

        private static string StudentLifeFileNameFor(string playerId)
        {
            return string.IsNullOrEmpty(playerId) || playerId == PlayerIdentity.DefaultPlayerId ? "student-life-progress.json" : "student-life-progress-" + playerId + ".json";
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

        private static Button FindRootButton(string name)
        {
            var go = GameObject.Find(name);
            Assert.IsNotNull(go, "UI button root missing: " + name);
            var button = go.GetComponent<Button>();
            Assert.IsNotNull(button, name + " does not have Button component.");
            return button;
        }

        private static IEnumerator CaptureScreenshotEvidence()
        {
            ScreenshotEvidence.Capture(ScreenshotPath);
            yield return ScreenshotEvidence.WaitForNonZeroFile(ScreenshotPath, 3f, "NETWORK-ROUTE-REL-HOST-001 failed: Game View screenshot evidence was not written.");
        }

        private static void CleanupResidue()
        {
            PlayerFlowE2EDriver.DestroyAllNamed("SaveSlotSelectPanel");
            PlayerFlowE2EDriver.DestroyAllNamed("SaveSlotCanvas");
            PlayerFlowE2EDriver.DestroyAllNamed("SaveSlotSelectRoot");
            PlayerFlowE2EDriver.DestroyAllNamed("SpumCharacterCreatorPanel");
            PlayerFlowE2EDriver.DestroyAllNamed("EventSystem");
            PlayerFlowE2EDriver.DestroyAllNamed("Player");
            PlayerFlowE2EDriver.DestroyAllNamed("LocationNpcCanvas");
            PlayerFlowE2EDriver.DestroyAllNamed("TownHelpActionPoint");
        }

        private static void ShutdownNetworkManagers()
        {
            var managers = Object.FindObjectsByType<NetworkManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = managers.Length - 1; i >= 0; i--)
            {
                var manager = managers[i];
                if (manager == null) continue;
                if (manager.IsListening) manager.Shutdown();
                Object.DestroyImmediate(manager.gameObject);
            }
        }

        private readonly struct HostRouteRelationshipRuntime
        {
            public readonly GameObject Player;
            public readonly PlayerInteractionRouter Router;
            public readonly StudentLifeProgressComponent Student;
            public readonly DailyPlanProgressComponent Daily;
            public readonly DailyPlanBoardInteractor DailyBoard;
            public readonly CareerPracticeBoard CareerBoard;
            public readonly TownHelpActionInteractor Help;
            public readonly StudentDayEndInteractor DayEnd;
            public readonly DailyPlanPanel DailyPanel;
            public readonly ObjectiveJournalPanel Journal;
            public readonly ObjectiveJournalRuntimeProvider ObjectiveProvider;
            public readonly TrackedObjectiveHud TrackedHud;

            public HostRouteRelationshipRuntime(GameObject player, PlayerInteractionRouter router, StudentLifeProgressComponent student, DailyPlanProgressComponent daily, DailyPlanBoardInteractor dailyBoard, CareerPracticeBoard careerBoard, TownHelpActionInteractor help, StudentDayEndInteractor dayEnd, DailyPlanPanel dailyPanel, ObjectiveJournalPanel journal, ObjectiveJournalRuntimeProvider objectiveProvider, TrackedObjectiveHud trackedHud)
            {
                Player = player;
                Router = router;
                Student = student;
                Daily = daily;
                DailyBoard = dailyBoard;
                CareerBoard = careerBoard;
                Help = help;
                DayEnd = dayEnd;
                DailyPanel = dailyPanel;
                Journal = journal;
                ObjectiveProvider = objectiveProvider;
                TrackedHud = trackedHud;
            }

            public bool IsValid => Player != null && Router != null && Student != null && Daily != null && DailyBoard != null && CareerBoard != null && Help != null && DayEnd != null && DailyPanel != null && Journal != null && ObjectiveProvider != null && TrackedHud != null;
        }
    }
}
