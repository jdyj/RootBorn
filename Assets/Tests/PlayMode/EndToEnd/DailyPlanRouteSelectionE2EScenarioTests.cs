using System.Collections;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.Tests.PlayMode.EndToEnd.Support;
using Rootborn.UI.MainMenu;
using Rootborn.Tests.PlayMode.EndToEnd.Support;
using Rootborn.UI.Objectives;
using Rootborn.Tests.PlayMode.EndToEnd.Support;
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
    public sealed class DailyPlanRouteSelectionE2EScenarioTests : InputTestFixture
    {
        private const string TestSlotId = "slot-0";
        private const string ScreenshotPath = "Builds/Logs/daily-plan-route-e2e.png";
        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.timeScale = 1f;
            CleanupResidue();
            EnsurePassiveEventSystem();
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-daily-plan-route-e2e", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator DAILY_PLAN_E2E_001_SaveSlotSelectRoutePlayActivityResultReload()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            yield return LoadScene("MainMenu");
            EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "NewGameButton"));
            yield return ClickButton(FindButton(GameObject.Find("SpumCharacterCreatorRoot").transform, "ConfirmButton"));
            yield return WaitForScene("Town", 15f, "DAILY-PLAN-E2E-001 failed: SaveSlot New Game did not enter Town.");
            EnsureTownInstallers();
            yield return WaitForDailyPlanRuntime(15f);

            var runtime = FindRuntime();
            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.Board.transform.position, 0.25f, "DAILY-PLAN-E2E-001 failed: keyboard movement could not reach Daily Plan board.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "DAILY-PLAN-E2E-001 failed: Daily Plan board prompt was not visible.");
            StringAssert.Contains("Plan Day", runtime.Router.PromptText, "DAILY-PLAN-E2E-001 failed: Daily Plan board prompt text was not player-facing.");
            yield return PressInteractKey(keyboard);
            yield return WaitForDailyPlanPanelOpen(2f, "DAILY-PLAN-E2E-001 failed: Daily Plan panel did not open from E interaction.");
            Assert.GreaterOrEqual(runtime.DailyPanel.RouteRowCountForTests, 3, "DAILY-PLAN-E2E-001 failed: Daily Plan panel did not show data-defined route choices.");
            yield return ClickButton(runtime.DailyPanel.GetRouteButtonForTests(0));
            Assert.IsFalse(runtime.DailyPanel.gameObject.activeSelf, "DAILY-PLAN-E2E-001 failed: route selection did not close the Daily Plan panel for continued play.");
            Assert.AreEqual("daily.library.self-study", runtime.Daily.Progress.SelectedRouteId, "DAILY-PLAN-E2E-001 failed: clicked route button did not select the library route.");
            AssertDailySaveContains(runtime.Daily, "daily.library.self-study", "DAILY-PLAN-E2E-001 failed: Daily Plan route selection was not saved.");

            yield return PressTab(keyboard);
            yield return ClickButton(FindButton(runtime.Journal.transform, "ObjectiveCategory_Goals"));
            yield return ClickButton(FindButton(runtime.Journal.transform, "ObjectiveRow_1"));
            StringAssert.Contains("objective.daily.library.title", runtime.Journal.VisibleText, "DAILY-PLAN-E2E-001 failed: Objective Journal did not show selected Daily Plan route.");
            yield return ClickButton(FindButton(runtime.Journal.transform, "ObjectiveTrackButton"));
            StringAssert.Contains("objective.daily.library.title", runtime.TrackedHud.VisibleText, "DAILY-PLAN-E2E-001 failed: tracked HUD did not show the selected Daily Plan route.");
            yield return PressTab(keyboard);

            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, LocationApproachPosition(runtime.LibraryLocation), 0.15f, "DAILY-PLAN-E2E-001 failed: keyboard movement could not reach the Library location.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "DAILY-PLAN-E2E-001 failed: Library location prompt was not visible.");
            StringAssert.Contains("Library", runtime.Router.PromptText, "DAILY-PLAN-E2E-001 failed: Library prompt did not target the selected route location.");
            yield return PressInteractKey(keyboard);
            yield return WaitForLocationPanelOpen(3f, "DAILY-PLAN-E2E-001 failed: Library activity panel did not open from E interaction.");
            yield return ClickButton(FindButton(runtime.LocationPanel.transform, "LocationActivityButton_0"));
            Assert.AreEqual("daily.library.self-study", runtime.Daily.Progress.CompletedRouteId, "DAILY-PLAN-E2E-001 failed: matching Library activity did not complete the selected Daily Plan.");
            Assert.AreEqual("location.activity.library-hard-book", runtime.Daily.Progress.MatchedActivityId, "DAILY-PLAN-E2E-001 failed: Daily Plan did not record the selected activity.");
            Assert.AreEqual(1, runtime.Daily.Progress.AppliedCompletionKeys.Count, "DAILY-PLAN-E2E-001 failed: Daily Plan completion keys were duplicated after one activity.");

            runtime.ObjectiveProvider.RefreshNow();
            yield return PressTab(keyboard);
            yield return ClickButton(FindButton(runtime.Journal.transform, "ObjectiveCategory_Goals"));
            yield return ClickButton(FindButton(runtime.Journal.transform, "ObjectiveRow_1"));
            StringAssert.Contains("Completed with location.activity.library-hard-book", runtime.Journal.VisibleText, "DAILY-PLAN-E2E-001 failed: Objective Journal did not refresh Daily Plan completion.");
            yield return PressTab(keyboard);

            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.DayEnd.transform.position, 0.25f, "DAILY-PLAN-E2E-001 failed: keyboard movement could not reach day-end board.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "DAILY-PLAN-E2E-001 failed: day-end prompt was not visible.");
            yield return PressInteractKey(keyboard);
            yield return WaitForDayResultPanel(3f, "DAILY-PLAN-E2E-001 failed: day result panel did not open from day-end interaction.");
            var resultPanel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
            StringAssert.Contains("Daily Plan", resultPanel.ResultsTextForTests, "DAILY-PLAN-E2E-001 failed: day result UI did not include Daily Plan summary.");
            StringAssert.Contains("daily.route.library.self-study", resultPanel.ResultsTextForTests, "DAILY-PLAN-E2E-001 failed: day result UI did not include selected route.");
            StringAssert.Contains("Completed", resultPanel.ResultsTextForTests, "DAILY-PLAN-E2E-001 failed: day result UI did not show completed Daily Plan state.");
            StringAssert.Contains("objective.daily.library.next", resultPanel.NextGuideTextForTests, "DAILY-PLAN-E2E-001 failed: day result UI did not include Daily Plan next hint.");

            CaptureScreenshot();
            yield return WaitForScreenshot(3f);

            string saveFile = DailyPlanSavePath(runtime.Daily);
            string savedJson = File.ReadAllText(saveFile);
            StringAssert.Contains("daily.library.self-study", savedJson, "DAILY-PLAN-E2E-001 failed: saved Daily Plan route id missing before reload.");
            StringAssert.Contains("location.activity.library-hard-book", savedJson, "DAILY-PLAN-E2E-001 failed: saved Daily Plan activity id missing before reload.");

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return LoadScene("MainMenu");
            EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "LoadButton"));
            yield return WaitForScene("Town", 15f, "DAILY-PLAN-E2E-001 failed: SaveSlot Load did not re-enter Town.");
            EnsureTownInstallers();
            yield return WaitForDailyPlanRuntime(15f);

            var reloaded = FindRuntime();
            Assert.AreEqual("daily.library.self-study", reloaded.Daily.Progress.SelectedRouteId, "DAILY-PLAN-E2E-001 failed: selected route did not survive reload.");
            Assert.AreEqual("daily.library.self-study", reloaded.Daily.Progress.CompletedRouteId, "DAILY-PLAN-E2E-001 failed: completed route did not survive reload.");
            Assert.AreEqual("location.activity.library-hard-book", reloaded.Daily.Progress.MatchedActivityId, "DAILY-PLAN-E2E-001 failed: matched activity did not survive reload.");
            Assert.AreEqual(1, reloaded.Daily.Progress.AppliedCompletionKeys.Count, "DAILY-PLAN-E2E-001 failed: Daily Plan completion duplicated after reload.");
            yield return PressTab(keyboard);
            Assert.IsTrue(reloaded.TrackedHud.IsVisible, "DAILY-PLAN-E2E-001 failed: tracked Daily Plan HUD did not restore after reload.");
            StringAssert.Contains("objective.daily.library.title", reloaded.TrackedHud.VisibleText, "DAILY-PLAN-E2E-001 failed: tracked HUD did not restore Daily Plan route context after reload.");
            yield return ClickButton(FindButton(reloaded.Journal.transform, "ObjectiveCategory_Goals"));
            yield return ClickButton(FindButton(reloaded.Journal.transform, "ObjectiveRow_1"));
            StringAssert.Contains("Completed with location.activity.library-hard-book", reloaded.Journal.VisibleText, "DAILY-PLAN-E2E-001 failed: Objective Journal did not restore completed Daily Plan context after reload.");
        }

        private static void EnsureTownInstallers()
        {
            StudentDayRuntimeInstaller.EnsureForActiveScene();
            LocationNpcRuntimeInstaller.EnsureForActiveScene();
            DailyPlanRuntimeInstaller.EnsureForActiveScene();
            CampaignRuntimeInstaller.EnsureForActiveScene();
        }

        private static IEnumerator WaitForDailyPlanRuntime(float timeoutSeconds)
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
                lastState = "player=" + (player != null)
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
            var board = GameObject.Find("DailyPlanBoard").GetComponent<DailyPlanBoardInteractor>();
            var library = GameObject.Find(LocationNpcRuntimeInstaller.LocationObjectName("location.library")).GetComponent<LocationActivityInteractor>();
            var dayEnd = GameObject.Find("StudentDayEndBoard").GetComponent<StudentDayEndInteractor>();
            return new DailyPlanRuntime(
                player,
                player.GetComponent<PlayerInteractionRouter>(),
                player.GetComponent<StudentLifeProgressComponent>(),
                player.GetComponent<DailyPlanProgressComponent>(),
                board,
                Object.FindFirstObjectByType<DailyPlanPanel>(FindObjectsInactive.Include),
                library,
                Object.FindFirstObjectByType<LocationIdentityPanel>(FindObjectsInactive.Include),
                dayEnd,
                Object.FindFirstObjectByType<ObjectiveJournalPanel>(FindObjectsInactive.Include),
                Object.FindFirstObjectByType<ObjectiveJournalRuntimeProvider>(FindObjectsInactive.Include),
                Object.FindFirstObjectByType<TrackedObjectiveHud>(FindObjectsInactive.Include));
        }

        private static Vector3 LocationApproachPosition(LocationActivityInteractor location)
        {
            return location.transform.position;
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

                private IEnumerator PressTab(Keyboard keyboard)
        {
            return PlayerFlowE2EDriver.PressTab(keyboard);
        }

                private IEnumerator PressInteractKey(Keyboard keyboard)
        {
            return PlayerFlowE2EDriver.PressInteractKey(keyboard);
        }

                private IEnumerator WalkPlayerWithKeyboardTo(GameObject player, Keyboard keyboard, Vector3 targetPosition, float tolerance, string failure)
        {
            return PlayerFlowE2EDriver.WalkPlayerWithKeyboardTo(player, keyboard, targetPosition, tolerance, 720, failure);
        }

                private static IEnumerator LoadScene(string sceneName)
        {
            return PlayerFlowE2EDriver.LoadScene(sceneName);
        }

                private static IEnumerator WaitForScene(string sceneName, float timeoutSeconds, string failure)
        {
            return PlayerFlowE2EDriver.WaitForScene(sceneName, timeoutSeconds, failure);
        }

                private static IEnumerator ClickButton(Button button)
        {
            return PlayerFlowE2EDriver.ClickButton(button);
        }

                private static Button FindButton(string cardName, string buttonName)
        {
            return PlayerFlowE2EDriver.FindButton(cardName, buttonName);
        }

                private static Button FindButton(Transform root, string name)
        {
            return PlayerFlowE2EDriver.FindButton(root, name);
        }

                private static Transform FindChild(Transform root, string name)
        {
            return PlayerFlowE2EDriver.FindChild(root, name);
        }

                private static KeyControl ControlFor(Keyboard keyboard, Key key)
        {
            return PlayerFlowE2EDriver.ControlFor(keyboard, key);
        }

        private void AssertDailySaveContains(DailyPlanProgressComponent daily, string expected, string failure)
        {
            var path = DailyPlanSavePath(daily);
            Assert.IsTrue(File.Exists(path), failure + " Missing file: " + path);
            StringAssert.Contains(expected, File.ReadAllText(path), failure);
        }

        private string DailyPlanSavePath(DailyPlanProgressComponent daily)
        {
            Assert.IsNotNull(daily);
            Assert.IsNotNull(daily.Progress);
            return Path.Combine(_saveRoot, TestSlotId, DailyPlanProgressPersistence.FileNameFor(daily.Progress.PlayerId));
        }

                private static void CaptureScreenshot()
        {
            ScreenshotEvidence.Capture(ScreenshotPath);
        }

                private static IEnumerator WaitForScreenshot(float timeoutSeconds)
        {
            return ScreenshotEvidence.WaitForNonZeroFile(ScreenshotPath, timeoutSeconds, "Game View screenshot evidence was not written");
        }

                private static void DeleteScreenshot()
        {
            ScreenshotEvidence.DeleteIfExists(ScreenshotPath);
        }

                private static void EnsurePassiveEventSystem()
        {
            PlayerFlowE2EDriver.EnsurePassiveEventSystem();
        }

        private static void CleanupResidue()
        {
            DestroyAllNamed("SaveSlotSelectPanel");
            DestroyAllNamed("SaveSlotCanvas");
            DestroyAllNamed("SaveSlotSelectRoot");
            DestroyAllNamed("EventSystem");
            DestroyAllNamed("Player");
        }

                private static void DestroyAllNamed(string objectName)
        {
            PlayerFlowE2EDriver.DestroyAllNamed(objectName);
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
