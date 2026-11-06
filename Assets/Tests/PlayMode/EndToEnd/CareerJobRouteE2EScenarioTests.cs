using System;
using System.Collections;
using System.IO;
using System.Linq;
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
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    public sealed class CareerJobRouteE2EScenarioTests : Rootborn.Tests.PlayMode.PlayModeInputFixture
    {
        private const string TestSlotId = "slot-0";
        private const string WorkId = "work.shift.corner-store";
        private const string RouteId = "daily.workshop.shift";
        private const string RouteLabel = "daily.route.workshop.shift";
        private const string RouteObjectiveTitle = "objective.daily.workshop.title";
        private const string ScreenshotPath = "Builds/Logs/career-job-route-e2e.png";

        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.timeScale = 1f;
            CleanupResidue();
            PlayerFlowE2EDriver.EnsurePassiveEventSystem();
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-career-job-route-e2e", Guid.NewGuid().ToString("N"));
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

        [Ignore(LegacyPremise.AuthoredTown + "Last state work=False")]
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator CAREER_JOB_ROUTE_E2E_001_ProgressUnlockSelectReload()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            yield return StartNewTownFromSaveSlotUi();
            var runtime = FindRuntime();
            Assert.AreEqual(WorkId, runtime.Work.Work.Id, "CAREER-JOB-ROUTE-E2E-001 failed: runtime work point must use registered Corner Store work data.");

            yield return OpenDailyPlanBoard(runtime, keyboard, "CAREER-JOB-ROUTE-E2E-001 failed: Daily Plan board did not open before work progress.");
            AssertRouteMissing(runtime.DailyPanel, RouteLabel, "CAREER-JOB-ROUTE-E2E-001 failed: workshop route was visible before the part-time work requirement was satisfied.");
            runtime.DailyPanel.gameObject.SetActive(false);

            yield return PlayerFlowE2EDriver.WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.Work.transform.position, 0.25f, 720, "CAREER-JOB-ROUTE-E2E-001 failed: keyboard movement could not reach part-time work point.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "CAREER-JOB-ROUTE-E2E-001 failed: part-time work prompt was not visible.");
            StringAssert.Contains(Rootborn.Game.Localization.GameText.Get("work.shift.corner-store"), runtime.Router.PromptText, "CAREER-JOB-ROUTE-E2E-001 failed: prompt did not expose the expected player-facing work text.");
            yield return PlayerFlowE2EDriver.PressInteractKey(keyboard);
            yield return null;

            Assert.AreEqual(LifeActivityResultKind.Applied, runtime.Work.LastResult.Kind, "CAREER-JOB-ROUTE-E2E-001 failed: part-time work did not apply through player input.");
            CollectionAssert.Contains(runtime.Student.Progress.GetTodayActivityIds(), WorkId, "CAREER-JOB-ROUTE-E2E-001 failed: StudentLife progress did not record the work activity.");
            Assert.AreEqual(1, PartTimeWorkDayLog.FromResultLogs(runtime.Student.Progress.GetTodayResultLogIds()).WorkIds.Count(id => id == WorkId), "CAREER-JOB-ROUTE-E2E-001 failed: work result log should appear exactly once before reload.");

            yield return OpenDailyPlanBoard(runtime, keyboard, "CAREER-JOB-ROUTE-E2E-001 failed: Daily Plan board did not reopen after work progress.");
            var routeButton = DailyPlanE2EDriver.FindRouteButton(runtime.DailyPanel, RouteLabel, "CAREER-JOB-ROUTE-E2E-001 failed: workshop route was not visible after part-time work progress.");
            yield return PlayerFlowE2EDriver.ClickButton(routeButton);
            Assert.AreEqual(RouteId, runtime.Daily.Progress.SelectedRouteId, "CAREER-JOB-ROUTE-E2E-001 failed: clicked workshop route was not selected.");

            yield return OpenDailyPlanBoard(runtime, keyboard, "CAREER-JOB-ROUTE-E2E-001 failed: Daily Plan board did not reopen for repeated route selection.");
            var repeatedRouteButton = DailyPlanE2EDriver.FindRouteButton(runtime.DailyPanel, RouteLabel, "CAREER-JOB-ROUTE-E2E-001 failed: workshop route was not visible for repeated route selection.");
            yield return PlayerFlowE2EDriver.ClickButton(repeatedRouteButton);
            Assert.AreEqual(RouteId, runtime.Daily.Progress.SelectedRouteId, "CAREER-JOB-ROUTE-E2E-001 failed: repeated route click changed selected route unexpectedly.");

            runtime.ObjectiveProvider.RefreshNow();
            yield return PlayerFlowE2EDriver.PressTab(keyboard);
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(runtime.Journal.transform, "ObjectiveCategory_Goals"));
            AssertObjectiveRowsContainExactlyOnce(runtime.Journal, RouteObjectiveTitle, "CAREER-JOB-ROUTE-E2E-001 failed: Objective Journal Goals duplicated or omitted selected workshop route.");
            yield return PlayerFlowE2EDriver.ClickButton(ObjectiveJournalE2EDriver.FindRowButtonContaining(runtime.Journal, RouteObjectiveTitle, "CAREER-JOB-ROUTE-E2E-001 failed: Objective Journal Goals did not include selected workshop route."));
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(runtime.Journal.transform, "ObjectiveTrackButton"));
            AssertOneTrackedHud("CAREER-JOB-ROUTE-E2E-001 failed: tracking created more than one HUD instance.");
            Assert.IsTrue(runtime.TrackedHud.IsVisible, "CAREER-JOB-ROUTE-E2E-001 failed: tracked HUD did not show selected workshop route.");
            StringAssert.Contains(RouteObjectiveTitle, runtime.TrackedHud.VisibleText, "CAREER-JOB-ROUTE-E2E-001 failed: tracked HUD did not include selected workshop route title.");
            ScreenshotEvidence.Capture(ScreenshotPath);
            yield return ScreenshotEvidence.WaitForNonZeroFile(ScreenshotPath, 3f, "CAREER-JOB-ROUTE-E2E-001 failed: Game View screenshot evidence was not written.");

            AssertSaveContainsExactlyOnce(DailyPlanProgressPersistence.FileNameFor(PlayerIdentity.DefaultPlayerId), RouteId, "CAREER-JOB-ROUTE-E2E-001 failed: Daily Plan save must include selected workshop route exactly once.");
            AssertStudentLifeSaveContainsWorkStateExactlyOnce("CAREER-JOB-ROUTE-E2E-001 failed: StudentLife save must include part-time work progress exactly once.");

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return PlayerFlowE2EDriver.LoadScene("MainMenu");
            PlayerFlowE2EDriver.EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton("SaveSlotCard_" + TestSlotId, "LoadButton"));
            yield return PlayerFlowE2EDriver.WaitForScene("Town", 20f, "CAREER-JOB-ROUTE-E2E-001 failed: SaveSlot Load did not re-enter Town.");
            EnsureTownInstallers();
            yield return WaitForRuntime(15f);

            var reloaded = FindRuntime();
            Assert.AreEqual(RouteId, reloaded.Daily.Progress.SelectedRouteId, "CAREER-JOB-ROUTE-E2E-001 failed: selected workshop route did not survive reload.");
            Assert.AreEqual(1, PartTimeWorkDayLog.FromResultLogs(reloaded.Student.Progress.GetTodayResultLogIds()).WorkIds.Count(id => id == WorkId), "CAREER-JOB-ROUTE-E2E-001 failed: reload duplicated or removed the work result log.");
            AssertSaveContainsExactlyOnce(DailyPlanProgressPersistence.FileNameFor(PlayerIdentity.DefaultPlayerId), RouteId, "CAREER-JOB-ROUTE-E2E-001 failed: reload duplicated or removed the selected workshop route save state.");
            AssertStudentLifeSaveContainsWorkStateExactlyOnce("CAREER-JOB-ROUTE-E2E-001 failed: reload duplicated or removed the part-time work save state.");
            yield return OpenDailyPlanBoard(reloaded, keyboard, "CAREER-JOB-ROUTE-E2E-001 failed: Daily Plan board did not open after reload.");
            DailyPlanE2EDriver.FindRouteButton(reloaded.DailyPanel, RouteLabel, "CAREER-JOB-ROUTE-E2E-001 failed: workshop route availability did not restore after reload.");
            reloaded.DailyPanel.gameObject.SetActive(false);
            yield return PlayerFlowE2EDriver.PressTab(keyboard);
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(reloaded.Journal.transform, "ObjectiveCategory_Goals"));
            AssertObjectiveRowsContainExactlyOnce(reloaded.Journal, RouteObjectiveTitle, "CAREER-JOB-ROUTE-E2E-001 failed: reload duplicated or omitted selected workshop route Objective Journal row.");
            AssertOneTrackedHud("CAREER-JOB-ROUTE-E2E-001 failed: reload created duplicate tracked HUD instances.");
            Assert.IsTrue(reloaded.TrackedHud.IsVisible, "CAREER-JOB-ROUTE-E2E-001 failed: tracked workshop route HUD did not restore after reload.");
            StringAssert.Contains(RouteObjectiveTitle, reloaded.TrackedHud.VisibleText, "CAREER-JOB-ROUTE-E2E-001 failed: tracked HUD did not restore workshop route context after reload.");
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
            yield return PlayerFlowE2EDriver.WaitForScene("Town", 20f, "CAREER-JOB-ROUTE-E2E-001 failed: SaveSlot New Game did not enter Town.");
            EnsureTownInstallers();
            yield return WaitForRuntime(15f);
        }

        private static void EnsureTownInstallers()
        {
            StudentDayRuntimeInstaller.EnsureForActiveScene();
            PartTimeWorkRuntimeInstaller.EnsureForActiveScene();
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
                var work = GameObject.Find("PartTimeWorkPoint");
                var dailyBoard = GameObject.Find("DailyPlanBoard");
                var journal = Object.FindFirstObjectByType<ObjectiveJournalPanel>(FindObjectsInactive.Include);
                var provider = Object.FindFirstObjectByType<ObjectiveJournalRuntimeProvider>(FindObjectsInactive.Include);
                var hud = Object.FindFirstObjectByType<TrackedObjectiveHud>(FindObjectsInactive.Include);
                var dailyPanel = Object.FindFirstObjectByType<DailyPlanPanel>(FindObjectsInactive.Include);
                lastState = "player=" + (player != null)
                    + " router=" + (player != null && player.GetComponent<PlayerInteractionRouter>() != null)
                    + " student=" + (player != null && player.GetComponent<StudentLifeProgressComponent>() != null)
                    + " inventory=" + (player != null && player.GetComponent<PlayerInventory>() != null)
                    + " daily=" + (player != null && player.GetComponent<DailyPlanProgressComponent>() != null)
                    + " work=" + (work != null && work.GetComponent<PartTimeWorkInteractor>() != null)
                    + " dailyBoard=" + (dailyBoard != null && dailyBoard.GetComponent<DailyPlanBoardInteractor>() != null)
                    + " dailyPanel=" + (dailyPanel != null)
                    + " journal=" + (journal != null)
                    + " provider=" + (provider != null)
                    + " hud=" + (hud != null)
                    + " eventSystem=" + (EventSystem.current != null);

                if (player != null && player.GetComponent<PlayerInteractionRouter>() != null && player.GetComponent<StudentLifeProgressComponent>() != null && player.GetComponent<PlayerInventory>() != null && player.GetComponent<DailyPlanProgressComponent>() != null &&
                    work != null && work.GetComponent<PartTimeWorkInteractor>() != null && dailyBoard != null && dailyBoard.GetComponent<DailyPlanBoardInteractor>() != null && dailyPanel != null && journal != null && provider != null && hud != null && EventSystem.current != null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("CAREER-JOB-ROUTE-E2E-001 failed: Town runtime did not install within timeout. Last state: " + lastState);
        }

        private static CareerJobRuntime FindRuntime()
        {
            var player = GameObject.Find("Player");
            return new CareerJobRuntime(
                player,
                player.GetComponent<PlayerInteractionRouter>(),
                player.GetComponent<StudentLifeProgressComponent>(),
                player.GetComponent<DailyPlanProgressComponent>(),
                GameObject.Find("PartTimeWorkPoint").GetComponent<PartTimeWorkInteractor>(),
                GameObject.Find("DailyPlanBoard").GetComponent<DailyPlanBoardInteractor>(),
                Object.FindFirstObjectByType<DailyPlanPanel>(FindObjectsInactive.Include),
                Object.FindFirstObjectByType<ObjectiveJournalPanel>(FindObjectsInactive.Include),
                Object.FindFirstObjectByType<ObjectiveJournalRuntimeProvider>(FindObjectsInactive.Include),
                Object.FindFirstObjectByType<TrackedObjectiveHud>(FindObjectsInactive.Include));
        }

        private static IEnumerator OpenDailyPlanBoard(CareerJobRuntime runtime, Keyboard keyboard, string failure)
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

        private void AssertStudentLifeSaveContainsWorkStateExactlyOnce(string failure)
        {
            string path = Path.Combine(_saveRoot, TestSlotId, "student-life-progress.json");
            Assert.IsTrue(File.Exists(path), failure + " Missing file: " + path);
            string text = File.ReadAllText(path);
            Assert.AreEqual(1, CountOccurrences(ExtractJsonArrayBlock(text, "AppliedRequestIds"), "\"" + WorkId + ":0\""), failure + " AppliedRequestIds should contain the work request exactly once. Text: " + text);
            Assert.AreEqual(1, CountOccurrences(ExtractJsonArrayBlock(text, "ActivityLogIds"), "\"" + WorkId + "\""), failure + " ActivityLogIds should contain the work id exactly once. Text: " + text);
            Assert.AreEqual(1, CountOccurrences(ExtractJsonArrayBlock(text, "TodayActivityIds"), "\"" + WorkId + "\""), failure + " TodayActivityIds should contain the work id exactly once. Text: " + text);
        }

        private void AssertSaveContainsExactlyOnce(string fileName, string expected, string failure)
        {
            string path = Path.Combine(_saveRoot, TestSlotId, fileName);
            Assert.IsTrue(File.Exists(path), failure + " Missing file: " + path);
            string text = File.ReadAllText(path);
            string expectedJsonValue = "\"" + expected + "\"";
            Assert.AreEqual(1, CountOccurrences(text, expectedJsonValue), failure + " File: " + fileName + " Expected JSON value: " + expectedJsonValue + " Text: " + text);
        }

        private static string ExtractJsonArrayBlock(string text, string fieldName)
        {
            string marker = "\"" + fieldName + "\": [";
            int start = text.IndexOf(marker, StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, "Missing JSON array field: " + fieldName + " Text: " + text);
            int blockStart = start + marker.Length;
            int end = text.IndexOf("]", blockStart, StringComparison.Ordinal);
            Assert.Greater(end, blockStart, "JSON array field was not closed: " + fieldName + " Text: " + text);
            return text.Substring(blockStart, end - blockStart);
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

        private readonly struct CareerJobRuntime
        {
            public readonly GameObject Player;
            public readonly PlayerInteractionRouter Router;
            public readonly StudentLifeProgressComponent Student;
            public readonly DailyPlanProgressComponent Daily;
            public readonly PartTimeWorkInteractor Work;
            public readonly DailyPlanBoardInteractor DailyBoard;
            public readonly DailyPlanPanel DailyPanel;
            public readonly ObjectiveJournalPanel Journal;
            public readonly ObjectiveJournalRuntimeProvider ObjectiveProvider;
            public readonly TrackedObjectiveHud TrackedHud;

            public CareerJobRuntime(GameObject player, PlayerInteractionRouter router, StudentLifeProgressComponent student, DailyPlanProgressComponent daily, PartTimeWorkInteractor work, DailyPlanBoardInteractor dailyBoard, DailyPlanPanel dailyPanel, ObjectiveJournalPanel journal, ObjectiveJournalRuntimeProvider objectiveProvider, TrackedObjectiveHud trackedHud)
            {
                Player = player;
                Router = router;
                Student = student;
                Daily = daily;
                Work = work;
                DailyBoard = dailyBoard;
                DailyPanel = dailyPanel;
                Journal = journal;
                ObjectiveProvider = objectiveProvider;
                TrackedHud = trackedHud;
            }
        }
    }
}
