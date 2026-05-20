using System.Collections;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.Tests.PlayMode.EndToEnd.Support;
using Rootborn.UI.MainMenu;
using Rootborn.UI.Objectives;
using Rootborn.UI.StudentLife;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    public sealed class DailyPlanRouteSelectionE2EScenarioTests : InputTestFixture
    {
        private const string TestSlotId = "slot-0";
        private const string RouteId = "daily.library.self-study";
        private const string RouteLabel = "daily.route.library.self-study";
        private const string RouteObjectiveTitle = "objective.daily.library.title";
        private const string ActivityId = "location.activity.library-hard-book";
        private const string ScreenshotPath = "Builds/Logs/daily-plan-route-e2e.png";

        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.timeScale = 1f;
            CleanupResidue();
            PlayerFlowE2EDriver.EnsurePassiveEventSystem();
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-daily-plan-route-e2e", System.Guid.NewGuid().ToString("N"));
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
        [Timeout(600000)]
        public IEnumerator DAILY_PLAN_E2E_001_SaveSlotSelectRoutePlayActivityResultReload()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            yield return StartNewTownFromSaveSlotUi();
            var runtime = FindRuntime();

            yield return OpenDailyPlanBoard(runtime, keyboard, "DAILY-PLAN-E2E-001 failed: Daily Plan board did not open.");
            Assert.GreaterOrEqual(runtime.DailyPanel.RouteRowCountForTests, 3, "DAILY-PLAN-E2E-001 failed: Daily Plan panel did not show data-defined route choices.");
            var routeButton = DailyPlanE2EDriver.FindRouteButton(runtime.DailyPanel, RouteLabel, "DAILY-PLAN-E2E-001 failed: library route was not visible.");
            yield return PlayerFlowE2EDriver.ClickButton(routeButton);
            Assert.IsFalse(runtime.DailyPanel.gameObject.activeSelf, "DAILY-PLAN-E2E-001 failed: route selection did not close the Daily Plan panel for continued play.");
            Assert.AreEqual(RouteId, runtime.Daily.Progress.SelectedRouteId, "DAILY-PLAN-E2E-001 failed: clicked route button did not select the library route.");
            AssertDailySaveContains(runtime.Daily, RouteId, "DAILY-PLAN-E2E-001 failed: Daily Plan route selection was not saved.");

            runtime.ObjectiveProvider.RefreshNow();
            yield return PlayerFlowE2EDriver.PressTab(keyboard);
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(runtime.Journal.transform, "ObjectiveCategory_Goals"));
            yield return PlayerFlowE2EDriver.ClickButton(ObjectiveJournalE2EDriver.FindRowButtonContaining(runtime.Journal, RouteObjectiveTitle, "DAILY-PLAN-E2E-001 failed: selected Daily Plan route was not in Goals before completion."));
            StringAssert.Contains(RouteObjectiveTitle, runtime.Journal.VisibleText, "DAILY-PLAN-E2E-001 failed: Objective Journal did not show selected Daily Plan route.");
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(runtime.Journal.transform, "ObjectiveTrackButton"));
            StringAssert.Contains(RouteObjectiveTitle, runtime.TrackedHud.VisibleText, "DAILY-PLAN-E2E-001 failed: tracked HUD did not show the selected Daily Plan route.");
            yield return PlayerFlowE2EDriver.PressTab(keyboard);

            yield return PlayerFlowE2EDriver.WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.LibraryLocation.transform.position, 0.15f, 720, "DAILY-PLAN-E2E-001 failed: keyboard movement could not reach the Library location.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "DAILY-PLAN-E2E-001 failed: Library location prompt was not visible.");
            StringAssert.Contains("Library", runtime.Router.PromptText, "DAILY-PLAN-E2E-001 failed: Library prompt did not target the selected route location.");
            yield return PlayerFlowE2EDriver.PressInteractKey(keyboard);
            yield return WaitForLocationPanelOpen(3f, "DAILY-PLAN-E2E-001 failed: Library activity panel did not open from E interaction.");
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(runtime.LocationPanel.transform, "LocationActivityButton_0"));
            Assert.AreEqual(RouteId, runtime.Daily.Progress.CompletedRouteId, "DAILY-PLAN-E2E-001 failed: matching Library activity did not complete the selected Daily Plan.");
            Assert.AreEqual(ActivityId, runtime.Daily.Progress.MatchedActivityId, "DAILY-PLAN-E2E-001 failed: Daily Plan did not record the selected activity.");
            Assert.AreEqual(1, runtime.Daily.Progress.AppliedCompletionKeys.Count, "DAILY-PLAN-E2E-001 failed: Daily Plan completion keys were duplicated after one activity.");

            runtime.ObjectiveProvider.RefreshNow();
            Assert.IsFalse(runtime.TrackedHud.IsVisible, "DAILY-PLAN-E2E-001 failed: completed route stayed in tracked HUD after Objective Journal refresh.");
            yield return PlayerFlowE2EDriver.PressTab(keyboard);
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(runtime.Journal.transform, "ObjectiveCategory_Goals"));
            StringAssert.DoesNotContain(RouteObjectiveTitle, runtime.Journal.VisibleText, "DAILY-PLAN-E2E-001 failed: completed route remained as active Goal detail.");
            yield return PlayerFlowE2EDriver.PressTab(keyboard);

            yield return PlayerFlowE2EDriver.WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.DayEnd.transform.position, 0.25f, 720, "DAILY-PLAN-E2E-001 failed: keyboard movement could not reach day-end board.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "DAILY-PLAN-E2E-001 failed: day-end prompt was not visible.");
            yield return PlayerFlowE2EDriver.PressInteractKey(keyboard);
            yield return WaitForDayResultPanel(3f, "DAILY-PLAN-E2E-001 failed: day result panel did not open from day-end interaction.");
            var resultPanel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
            StringAssert.Contains("Daily Plan", resultPanel.ResultsTextForTests, "DAILY-PLAN-E2E-001 failed: day result UI did not include Daily Plan summary.");
            StringAssert.Contains(RouteLabel, resultPanel.ResultsTextForTests, "DAILY-PLAN-E2E-001 failed: day result UI did not include selected route.");
            StringAssert.Contains(ActivityId, resultPanel.ResultsTextForTests, "DAILY-PLAN-E2E-001 failed: day result UI did not include matched activity.");
            StringAssert.Contains("Completed", resultPanel.ResultsTextForTests, "DAILY-PLAN-E2E-001 failed: day result UI did not show completed Daily Plan state.");
            StringAssert.Contains("objective.daily.library.next", resultPanel.NextGuideTextForTests, "DAILY-PLAN-E2E-001 failed: day result UI did not include Daily Plan next hint.");
            ScreenshotEvidence.Capture(ScreenshotPath);
            yield return ScreenshotEvidence.WaitForNonZeroFile(ScreenshotPath, 3f, "DAILY-PLAN-E2E-001 failed: Game View screenshot evidence was not written.");

            string saveFile = DailyPlanSavePath(runtime.Daily);
            string savedJson = File.ReadAllText(saveFile);
            StringAssert.Contains(RouteId, savedJson, "DAILY-PLAN-E2E-001 failed: saved Daily Plan route id missing before reload.");
            StringAssert.Contains(ActivityId, savedJson, "DAILY-PLAN-E2E-001 failed: saved Daily Plan activity id missing before reload.");

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return PlayerFlowE2EDriver.LoadScene("MainMenu");
            PlayerFlowE2EDriver.EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton("SaveSlotCard_" + TestSlotId, "LoadButton"));
            yield return PlayerFlowE2EDriver.WaitForScene("Town", 15f, "DAILY-PLAN-E2E-001 failed: SaveSlot Load did not re-enter Town.");
            EnsureTownInstallers();
            yield return WaitForRuntime(15f);

            var reloaded = FindRuntime();
            Assert.AreEqual(RouteId, reloaded.Daily.Progress.SelectedRouteId, "DAILY-PLAN-E2E-001 failed: selected route did not survive reload.");
            Assert.AreEqual(RouteId, reloaded.Daily.Progress.CompletedRouteId, "DAILY-PLAN-E2E-001 failed: completed route did not survive reload.");
            Assert.AreEqual(ActivityId, reloaded.Daily.Progress.MatchedActivityId, "DAILY-PLAN-E2E-001 failed: matched activity did not survive reload.");
            Assert.AreEqual(1, reloaded.Daily.Progress.AppliedCompletionKeys.Count, "DAILY-PLAN-E2E-001 failed: Daily Plan completion duplicated after reload.");
            reloaded.ObjectiveProvider.RefreshNow();
            Assert.IsFalse(reloaded.TrackedHud.IsVisible, "DAILY-PLAN-E2E-001 failed: completed Daily Plan HUD restored after reload.");
            yield return PlayerFlowE2EDriver.PressTab(keyboard);
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(reloaded.Journal.transform, "ObjectiveCategory_Goals"));
            StringAssert.DoesNotContain(RouteObjectiveTitle, reloaded.Journal.VisibleText, "DAILY-PLAN-E2E-001 failed: completed Daily Plan restored as active Goal after reload.");
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
            yield return PlayerFlowE2EDriver.WaitForScene("Town", 15f, "DAILY-PLAN-E2E-001 failed: SaveSlot New Game did not enter Town.");
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
            string lastState = string.Empty;
            while (elapsed < timeoutSeconds)
            {
                var player = GameObject.Find("Player");
                var board = GameObject.Find("DailyPlanBoard");
                var library = GameObject.Find(LocationNpcRuntimeInstaller.LocationObjectName("location.library"));
                var dayEnd = GameObject.Find("StudentDayEndBoard");
                var panel = Object.FindFirstObjectByType<DailyPlanPanel>(FindObjectsInactive.Include);
                var locationPanel = Object.FindFirstObjectByType<LocationIdentityPanel>(FindObjectsInactive.Include);
                var resultPanel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
                var journal = Object.FindFirstObjectByType<ObjectiveJournalPanel>(FindObjectsInactive.Include);
                var provider = Object.FindFirstObjectByType<ObjectiveJournalRuntimeProvider>(FindObjectsInactive.Include);
                var hud = Object.FindFirstObjectByType<TrackedObjectiveHud>(FindObjectsInactive.Include);
                lastState = "scene=" + SceneManager.GetActiveScene().name
                    + " player=" + (player != null)
                    + " router=" + (player != null && player.GetComponent<PlayerInteractionRouter>() != null)
                    + " student=" + (player != null && player.GetComponent<StudentLifeProgressComponent>() != null)
                    + " daily=" + (player != null && player.GetComponent<DailyPlanProgressComponent>() != null)
                    + " board=" + (board != null)
                    + " dailyPanel=" + (panel != null)
                    + " library=" + (library != null && library.GetComponent<LocationActivityInteractor>() != null)
                    + " locationPanel=" + (locationPanel != null)
                    + " dayEnd=" + (dayEnd != null && dayEnd.GetComponent<StudentDayEndInteractor>() != null)
                    + " resultPanel=" + (resultPanel != null)
                    + " journal=" + (journal != null)
                    + " provider=" + (provider != null)
                    + " hud=" + (hud != null)
                    + " eventSystem=" + (EventSystem.current != null);

                if (player != null && player.GetComponent<PlayerController>() != null && player.GetComponent<PlayerInteractionRouter>() != null &&
                    player.GetComponent<StudentLifeProgressComponent>() != null && player.GetComponent<DailyPlanProgressComponent>() != null &&
                    board != null && board.GetComponent<DailyPlanBoardInteractor>() != null && panel != null &&
                    library != null && library.GetComponent<LocationActivityInteractor>() != null && locationPanel != null &&
                    dayEnd != null && dayEnd.GetComponent<StudentDayEndInteractor>() != null && resultPanel != null &&
                    journal != null && provider != null && hud != null && EventSystem.current != null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("Daily Plan E2E runtime did not install within timeout. Last state: " + lastState);
        }

        private static DailyPlanRuntime FindRuntime()
        {
            var player = GameObject.Find("Player");
            return new DailyPlanRuntime(
                player,
                player.GetComponent<PlayerInteractionRouter>(),
                player.GetComponent<StudentLifeProgressComponent>(),
                player.GetComponent<DailyPlanProgressComponent>(),
                GameObject.Find("DailyPlanBoard").GetComponent<DailyPlanBoardInteractor>(),
                Object.FindFirstObjectByType<DailyPlanPanel>(FindObjectsInactive.Include),
                GameObject.Find(LocationNpcRuntimeInstaller.LocationObjectName("location.library")).GetComponent<LocationActivityInteractor>(),
                Object.FindFirstObjectByType<LocationIdentityPanel>(FindObjectsInactive.Include),
                GameObject.Find("StudentDayEndBoard").GetComponent<StudentDayEndInteractor>(),
                Object.FindFirstObjectByType<ObjectiveJournalPanel>(FindObjectsInactive.Include),
                Object.FindFirstObjectByType<ObjectiveJournalRuntimeProvider>(FindObjectsInactive.Include),
                Object.FindFirstObjectByType<TrackedObjectiveHud>(FindObjectsInactive.Include));
        }

        private static IEnumerator OpenDailyPlanBoard(DailyPlanRuntime runtime, Keyboard keyboard, string failure)
        {
            yield return PlayerFlowE2EDriver.WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.Board.transform.position, 0.25f, 720, failure);
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, failure + " Prompt was not visible.");
            StringAssert.Contains("Plan Day", runtime.Router.PromptText, failure + " Prompt text did not target Daily Plan board.");
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

        private static IEnumerator WaitForLocationPanelOpen(float timeoutSeconds, string failure)
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

        private static IEnumerator WaitForDayResultPanel(float timeoutSeconds, string failure)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var panel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
                if (panel != null && panel.IsOpen) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail(failure);
        }

        private void AssertDailySaveContains(DailyPlanProgressComponent daily, string expected, string failure)
        {
            string path = DailyPlanSavePath(daily);
            Assert.IsTrue(File.Exists(path), failure + " Missing file: " + path);
            StringAssert.Contains(expected, File.ReadAllText(path), failure);
        }

        private string DailyPlanSavePath(DailyPlanProgressComponent daily)
        {
            Assert.IsNotNull(daily);
            Assert.IsNotNull(daily.Progress);
            return Path.Combine(_saveRoot, TestSlotId, DailyPlanProgressPersistence.FileNameFor(daily.Progress.PlayerId));
        }

        private static void CleanupResidue()
        {
            PlayerFlowE2EDriver.DestroyAllNamed("SaveSlotSelectPanel");
            PlayerFlowE2EDriver.DestroyAllNamed("SaveSlotCanvas");
            PlayerFlowE2EDriver.DestroyAllNamed("SaveSlotSelectRoot");
            PlayerFlowE2EDriver.DestroyAllNamed("EventSystem");
            PlayerFlowE2EDriver.DestroyAllNamed("Player");
        }

        private readonly struct DailyPlanRuntime
        {
            public readonly GameObject Player;
            public readonly PlayerInteractionRouter Router;
            public readonly StudentLifeProgressComponent Student;
            public readonly DailyPlanProgressComponent Daily;
            public readonly DailyPlanBoardInteractor Board;
            public readonly DailyPlanPanel DailyPanel;
            public readonly LocationActivityInteractor LibraryLocation;
            public readonly LocationIdentityPanel LocationPanel;
            public readonly StudentDayEndInteractor DayEnd;
            public readonly ObjectiveJournalPanel Journal;
            public readonly ObjectiveJournalRuntimeProvider ObjectiveProvider;
            public readonly TrackedObjectiveHud TrackedHud;

            public DailyPlanRuntime(GameObject player, PlayerInteractionRouter router, StudentLifeProgressComponent student, DailyPlanProgressComponent daily, DailyPlanBoardInteractor board, DailyPlanPanel dailyPanel, LocationActivityInteractor libraryLocation, LocationIdentityPanel locationPanel, StudentDayEndInteractor dayEnd, ObjectiveJournalPanel journal, ObjectiveJournalRuntimeProvider objectiveProvider, TrackedObjectiveHud trackedHud)
            {
                Player = player;
                Router = router;
                Student = student;
                Daily = daily;
                Board = board;
                DailyPanel = dailyPanel;
                LibraryLocation = libraryLocation;
                LocationPanel = locationPanel;
                DayEnd = dayEnd;
                Journal = journal;
                ObjectiveProvider = objectiveProvider;
                TrackedHud = trackedHud;
            }
        }
    }
}
