using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Bootstrap;
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
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    public sealed class CareerDailyPlanRouteE2EScenarioTests : Rootborn.Tests.PlayMode.PlayModeInputFixture
    {
        private const string TestSlotId = "slot-0";
        private const string CareerId = "career.interior";
        private const string RouteId = "daily.route.career.interior-practice";
        private const string RouteLabel = "daily.route.career.interior-practice";
        private const string RouteObjectiveTitle = "objective.daily.career.interior-practice.title";
        private const string ScreenshotPath = "Builds/Logs/career-daily-plan-route-e2e.png";

        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.timeScale = 1f;
            CleanupResidue();
            PlayerFlowE2EDriver.EnsurePassiveEventSystem();
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-career-daily-plan-route-e2e", System.Guid.NewGuid().ToString("N"));
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

        [Ignore(LegacyPremise.AuthoredTown + "Last state careerBoard=False")]
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator CAREER_DAILY_PLAN_E2E_001_SelectInterestUnlockPracticeRouteReload()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            yield return StartNewTownFromSaveSlotUi();
            var runtime = FindRuntime();
            Assert.IsFalse(runtime.Student.Progress.IsCareerHintUnlocked(FindCareer(runtime.Board, CareerId)), "CAREER-DAILY-PLAN-E2E-001 failed: interior career hint should start locked.");

            yield return OpenDailyPlanBoard(runtime, keyboard, "CAREER-DAILY-PLAN-E2E-001 failed: Daily Plan board did not open before career practice.");
            AssertRouteMissing(runtime.DailyPanel, RouteLabel, "CAREER-DAILY-PLAN-E2E-001 failed: career route was visible before career practice unlock.");
            runtime.DailyPanel.gameObject.SetActive(false);

            yield return PlayerFlowE2EDriver.WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.Board.transform.position, 0.25f, 720, "CAREER-DAILY-PLAN-E2E-001 failed: keyboard movement could not reach Career Practice board.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "CAREER-DAILY-PLAN-E2E-001 failed: Career Practice board prompt was not visible.");
            StringAssert.Contains("Career", runtime.Router.PromptText, "CAREER-DAILY-PLAN-E2E-001 failed: Career Practice prompt did not expose player-facing career text.");
            yield return PlayerFlowE2EDriver.PressInteractKey(keyboard);
            yield return null;
            Assert.AreEqual("practice.chef", runtime.Board.LastPracticeId, "CAREER-DAILY-PLAN-E2E-001 failed: first player interaction did not run the first data-defined practice.");
            yield return PlayerFlowE2EDriver.PressInteractKey(keyboard);
            yield return null;
            Assert.AreEqual("practice.interior", runtime.Board.LastPracticeId, "CAREER-DAILY-PLAN-E2E-001 failed: second player interaction did not run the interior practice.");
            Assert.AreEqual(CareerId, runtime.Board.LastCareerHintId, "CAREER-DAILY-PLAN-E2E-001 failed: interior practice did not expose the expected career hint id.");
            Assert.IsTrue(runtime.Student.Progress.IsCareerHintUnlocked(FindCareer(runtime.Board, CareerId)), "CAREER-DAILY-PLAN-E2E-001 failed: interior career hint was not unlocked by player-facing practice.");

            yield return OpenDailyPlanBoard(runtime, keyboard, "CAREER-DAILY-PLAN-E2E-001 failed: Daily Plan board did not reopen after career practice.");
            var routeButton = DailyPlanE2EDriver.FindRouteButton(runtime.DailyPanel, RouteLabel, "CAREER-DAILY-PLAN-E2E-001 failed: career route was not visible after career practice.");
            yield return PlayerFlowE2EDriver.ClickButton(routeButton);
            Assert.AreEqual(RouteId, runtime.Daily.Progress.SelectedRouteId, "CAREER-DAILY-PLAN-E2E-001 failed: clicked career route was not selected.");

            runtime.ObjectiveProvider.RefreshNow();
            yield return PlayerFlowE2EDriver.PressTab(keyboard);
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(runtime.Journal.transform, "ObjectiveCategory_Goals"));
            AssertObjectiveRowsContainExactlyOnce(runtime.Journal, RouteObjectiveTitle, "CAREER-DAILY-PLAN-E2E-001 failed: Objective Journal Goals duplicated or omitted selected career route.");
            yield return PlayerFlowE2EDriver.ClickButton(ObjectiveJournalE2EDriver.FindRowButtonContaining(runtime.Journal, RouteObjectiveTitle, "CAREER-DAILY-PLAN-E2E-001 failed: Objective Journal Goals did not include selected career route."));
            StringAssert.Contains(RouteObjectiveTitle, runtime.Journal.VisibleText, "CAREER-DAILY-PLAN-E2E-001 failed: Objective Journal did not show selected career route detail.");
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(runtime.Journal.transform, "ObjectiveTrackButton"));
            AssertOneTrackedHud("CAREER-DAILY-PLAN-E2E-001 failed: tracking created more than one HUD instance.");
            Assert.IsTrue(runtime.TrackedHud.IsVisible, "CAREER-DAILY-PLAN-E2E-001 failed: tracked HUD did not show selected career route.");
            StringAssert.Contains(RouteObjectiveTitle, runtime.TrackedHud.VisibleText, "CAREER-DAILY-PLAN-E2E-001 failed: tracked HUD did not include selected career route title.");
            ScreenshotEvidence.Capture(ScreenshotPath);
            yield return ScreenshotEvidence.WaitForNonZeroFile(ScreenshotPath, 3f, "CAREER-DAILY-PLAN-E2E-001 failed: Game View screenshot evidence was not written.");

            AssertSaveContainsExactlyOnce("student-life-progress.json", CareerId, "CAREER-DAILY-PLAN-E2E-001 failed: StudentLife save must include the unlocked interior career hint exactly once.");
            AssertSaveContainsExactlyOnce(DailyPlanProgressPersistence.FileNameFor(PlayerIdentity.DefaultPlayerId), RouteId, "CAREER-DAILY-PLAN-E2E-001 failed: Daily Plan save must include selected career route exactly once.");

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return PlayerFlowE2EDriver.LoadScene("MainMenu");
            PlayerFlowE2EDriver.EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton("SaveSlotCard_" + TestSlotId, "LoadButton"));
            yield return PlayerFlowE2EDriver.WaitForScene("Town", 20f, "CAREER-DAILY-PLAN-E2E-001 failed: SaveSlot Load did not re-enter Town.");
            EnsureTownInstallers();
            yield return WaitForRuntime(15f);

            var reloaded = FindRuntime();
            Assert.IsTrue(reloaded.Student.Progress.IsCareerHintUnlocked(FindCareer(reloaded.Board, CareerId)), "CAREER-DAILY-PLAN-E2E-001 failed: interior career hint did not survive reload.");
            Assert.AreEqual(RouteId, reloaded.Daily.Progress.SelectedRouteId, "CAREER-DAILY-PLAN-E2E-001 failed: selected career route did not survive reload.");
            AssertSaveContainsExactlyOnce("student-life-progress.json", CareerId, "CAREER-DAILY-PLAN-E2E-001 failed: reload duplicated or removed the saved interior career hint.");
            AssertSaveContainsExactlyOnce(DailyPlanProgressPersistence.FileNameFor(PlayerIdentity.DefaultPlayerId), RouteId, "CAREER-DAILY-PLAN-E2E-001 failed: reload duplicated or removed the selected career route.");
            yield return OpenDailyPlanBoard(reloaded, keyboard, "CAREER-DAILY-PLAN-E2E-001 failed: Daily Plan board did not open after reload.");
            DailyPlanE2EDriver.FindRouteButton(reloaded.DailyPanel, RouteLabel, "CAREER-DAILY-PLAN-E2E-001 failed: career route availability did not restore after reload.");
            reloaded.DailyPanel.gameObject.SetActive(false);
            yield return PlayerFlowE2EDriver.PressTab(keyboard);
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(reloaded.Journal.transform, "ObjectiveCategory_Goals"));
            AssertObjectiveRowsContainExactlyOnce(reloaded.Journal, RouteObjectiveTitle, "CAREER-DAILY-PLAN-E2E-001 failed: reload duplicated or omitted selected career route Objective Journal row.");
            AssertOneTrackedHud("CAREER-DAILY-PLAN-E2E-001 failed: reload created duplicate tracked HUD instances.");
            Assert.IsTrue(reloaded.TrackedHud.IsVisible, "CAREER-DAILY-PLAN-E2E-001 failed: tracked career route HUD did not restore after reload.");
            StringAssert.Contains(RouteObjectiveTitle, reloaded.TrackedHud.VisibleText, "CAREER-DAILY-PLAN-E2E-001 failed: tracked HUD did not restore career route context after reload.");
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
            yield return PlayerFlowE2EDriver.WaitForScene("Town", 20f, "CAREER-DAILY-PLAN-E2E-001 failed: SaveSlot New Game did not enter Town.");
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
                var dailyBoard = GameObject.Find("DailyPlanBoard");
                var careerBoard = GameObject.Find("CareerPracticeBoard");
                var journal = Object.FindFirstObjectByType<ObjectiveJournalPanel>(FindObjectsInactive.Include);
                var provider = Object.FindFirstObjectByType<ObjectiveJournalRuntimeProvider>(FindObjectsInactive.Include);
                var hud = Object.FindFirstObjectByType<TrackedObjectiveHud>(FindObjectsInactive.Include);
                var dailyPanel = Object.FindFirstObjectByType<DailyPlanPanel>(FindObjectsInactive.Include);
                lastState = "player=" + (player != null)
                    + " router=" + (player != null && player.GetComponent<PlayerInteractionRouter>() != null)
                    + " student=" + (player != null && player.GetComponent<StudentLifeProgressComponent>() != null)
                    + " daily=" + (player != null && player.GetComponent<DailyPlanProgressComponent>() != null)
                    + " dailyBoard=" + (dailyBoard != null)
                    + " careerBoard=" + (careerBoard != null && careerBoard.GetComponent<CareerPracticeBoard>() != null)
                    + " dailyPanel=" + (dailyPanel != null)
                    + " journal=" + (journal != null)
                    + " provider=" + (provider != null)
                    + " hud=" + (hud != null)
                    + " eventSystem=" + (EventSystem.current != null);

                if (player != null && player.GetComponent<PlayerInteractionRouter>() != null && player.GetComponent<StudentLifeProgressComponent>() != null && player.GetComponent<DailyPlanProgressComponent>() != null &&
                    dailyBoard != null && dailyBoard.GetComponent<DailyPlanBoardInteractor>() != null && careerBoard != null && careerBoard.GetComponent<CareerPracticeBoard>() != null &&
                    dailyPanel != null && journal != null && provider != null && hud != null && EventSystem.current != null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("CAREER-DAILY-PLAN-E2E-001 failed: Town runtime did not install within timeout. Last state: " + lastState);
        }

        private static CareerRouteRuntime FindRuntime()
        {
            var player = GameObject.Find("Player");
            return new CareerRouteRuntime(
                player,
                player.GetComponent<PlayerInteractionRouter>(),
                player.GetComponent<StudentLifeProgressComponent>(),
                player.GetComponent<DailyPlanProgressComponent>(),
                GameObject.Find("DailyPlanBoard").GetComponent<DailyPlanBoardInteractor>(),
                GameObject.Find("CareerPracticeBoard").GetComponent<CareerPracticeBoard>(),
                Object.FindFirstObjectByType<DailyPlanPanel>(FindObjectsInactive.Include),
                Object.FindFirstObjectByType<ObjectiveJournalPanel>(FindObjectsInactive.Include),
                Object.FindFirstObjectByType<ObjectiveJournalRuntimeProvider>(FindObjectsInactive.Include),
                Object.FindFirstObjectByType<TrackedObjectiveHud>(FindObjectsInactive.Include));
        }

        private static IEnumerator OpenDailyPlanBoard(CareerRouteRuntime runtime, Keyboard keyboard, string failure)
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

        private static CareerDefinition FindCareer(CareerPracticeBoard board, string id)
        {
            Assert.IsNotNull(board);
            for (int i = 0; i < board.Practices.Count; i++)
            {
                var practice = board.Practices[i];
                if (practice != null && practice.CareerHint != null && practice.CareerHint.Id == id) return practice.CareerHint;
            }

            Assert.Fail("Missing career on CareerPracticeBoard: " + id);
            return null;
        }

        private static void AssertRouteMissing(DailyPlanPanel panel, string label, string failure)
        {
            for (int i = 0; i < panel.RouteRowCountForTests; i++)
            {
                var button = panel.GetRouteButtonForTests(i);
                var text = button != null ? button.GetComponentInChildren<Text>(true) : null;
                if (text != null && text.text == label)
                {
                    Assert.Fail(failure + "\n" + DailyPlanE2EDriver.DescribeRouteRows(panel));
                }
            }
        }

        private void AssertSaveContainsExactlyOnce(string fileName, string expected, string failure)
        {
            string path = Path.Combine(_saveRoot, TestSlotId, fileName);
            Assert.IsTrue(File.Exists(path), failure + " Missing file: " + path);
            string text = File.ReadAllText(path);
            string expectedJsonValue = "\"" + expected + "\"";
            Assert.AreEqual(1, CountOccurrences(text, expectedJsonValue), failure + " File: " + fileName + " Expected JSON value: " + expectedJsonValue + " Text: " + text);
        }

        private static void AssertObjectiveRowsContainExactlyOnce(ObjectiveJournalPanel panel, string expected, string failure)
        {
            Assert.IsNotNull(panel, failure + " Objective Journal panel missing.");
            var buttons = panel.GetComponentsInChildren<Button>(true);
            int count = 0;
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null || !buttons[i].name.StartsWith("ObjectiveRow_", StringComparison.Ordinal)) continue;
                var text = buttons[i].GetComponentInChildren<Text>(true);
                if (text != null && text.text.Contains(expected)) count++;
            }

            Assert.AreEqual(1, count, failure + "\nVisible Journal Text:\n" + panel.VisibleText + "\n" + PlayerFlowE2EDriver.DescribeButtons(panel.transform));
        }

        private static int CountOccurrences(string text, string expected)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(expected)) return 0;
            int count = 0;
            int index = 0;
            while (index < text.Length)
            {
                int found = text.IndexOf(expected, index, StringComparison.Ordinal);
                if (found < 0) break;
                count++;
                index = found + expected.Length;
            }

            return count;
        }

        private static void AssertOneTrackedHud(string failure)
        {
            Assert.AreEqual(1, Object.FindObjectsByType<TrackedObjectiveHud>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, failure);
        }

        private static void CleanupResidue()
        {
            PlayerFlowE2EDriver.DestroyAllNamed("SaveSlotSelectPanel");
            PlayerFlowE2EDriver.DestroyAllNamed("SaveSlotCanvas");
            PlayerFlowE2EDriver.DestroyAllNamed("SaveSlotSelectRoot");
            PlayerFlowE2EDriver.DestroyAllNamed("EventSystem");
            PlayerFlowE2EDriver.DestroyAllNamed("Player");
        }

        private readonly struct CareerRouteRuntime
        {
            public readonly GameObject Player;
            public readonly PlayerInteractionRouter Router;
            public readonly StudentLifeProgressComponent Student;
            public readonly DailyPlanProgressComponent Daily;
            public readonly DailyPlanBoardInteractor DailyBoard;
            public readonly CareerPracticeBoard Board;
            public readonly DailyPlanPanel DailyPanel;
            public readonly ObjectiveJournalPanel Journal;
            public readonly ObjectiveJournalRuntimeProvider ObjectiveProvider;
            public readonly TrackedObjectiveHud TrackedHud;

            public CareerRouteRuntime(GameObject player, PlayerInteractionRouter router, StudentLifeProgressComponent student, DailyPlanProgressComponent daily, DailyPlanBoardInteractor dailyBoard, CareerPracticeBoard board, DailyPlanPanel dailyPanel, ObjectiveJournalPanel journal, ObjectiveJournalRuntimeProvider objectiveProvider, TrackedObjectiveHud trackedHud)
            {
                Player = player;
                Router = router;
                Student = student;
                Daily = daily;
                DailyBoard = dailyBoard;
                Board = board;
                DailyPanel = dailyPanel;
                Journal = journal;
                ObjectiveProvider = objectiveProvider;
                TrackedHud = trackedHud;
            }
        }
    }
}
