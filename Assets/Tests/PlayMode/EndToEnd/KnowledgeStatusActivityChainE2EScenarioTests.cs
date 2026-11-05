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
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    public sealed class KnowledgeStatusActivityChainE2EScenarioTests : Rootborn.Tests.PlayMode.PlayModeInputFixture
    {
        private const string TestSlotId = "slot-0";
        private const string ActivityId = "location.activity.workshop-shift";
        private const string StatusId = "status.fatigue";
        private const string StatusTitle = "Fatigue";
        private const string ScreenshotPath = "Builds/Logs/knowledge-status-activity-chain-e2e.png";
        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.timeScale = 1f;
            CleanupResidue();
            PlayerFlowE2EDriver.EnsurePassiveEventSystem();
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-knowledge-status-e2e", Guid.NewGuid().ToString("N"));
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
            Time.timeScale = 1f;
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
        public IEnumerator KNOWLEDGE_STATUS_E2E_001_ActivityChainHintReloadNoDuplicate()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            string statusLog = LocationActivityLogCodec.EncodeDelta(ActivityId, StatusId, 1);

            yield return StartNewTownFromSaveSlotUi();
            var runtime = FindRuntime();
            var workshop = RequireObject(LocationNpcRuntimeInstaller.LocationObjectName("location.workshop"), "KNOWLEDGE-STATUS-E2E-001 failed: workshop location object missing.");
            var status = RequireStatus(StatusId);
            Assert.AreEqual(0, runtime.Student.Progress.GetStatusValue(status), "KNOWLEDGE-STATUS-E2E-001 failed: status should start unchanged.");

            yield return WalkToLocationAndOpenActivityPanel(runtime, keyboard, workshop, "KNOWLEDGE-STATUS-E2E-001 failed: could not open Workshop activity panel.");
            Assert.IsTrue(runtime.LocationPanel.IsOpen, "KNOWLEDGE-STATUS-E2E-001 failed: Workshop activity panel was not visible after player interaction.");
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(runtime.LocationPanel.transform, "LocationActivityButton_0"));
            Assert.AreEqual(LifeActivityResultKind.Applied, runtime.LocationPanel.LastResult.Kind, "KNOWLEDGE-STATUS-E2E-001 failed: Workshop activity did not apply through visible UI.");
            CollectionAssert.Contains(runtime.LocationPanel.LastResult.OutcomeLogIds, statusLog, "KNOWLEDGE-STATUS-E2E-001 failed: activity result did not include status outcome log.");
            Assert.AreEqual(1, runtime.Student.Progress.GetStatusValue(status), "KNOWLEDGE-STATUS-E2E-001 failed: Workshop activity did not change status value.");
            StringAssert.Contains(StatusId, runtime.LocationPanel.LastResult.OutcomeLogIds[0], "KNOWLEDGE-STATUS-E2E-001 failed: transient activity result did not expose status feedback.");

            runtime.ObjectiveProvider.RefreshNow();
            yield return PlayerFlowE2EDriver.PressTab(keyboard);
            Assert.IsTrue(runtime.Journal.IsVisible, "KNOWLEDGE-STATUS-E2E-001 failed: Tab did not open Objective Journal.");
            yield return SelectStatusHintAndTrack(runtime, "KNOWLEDGE-STATUS-E2E-001 failed: Objective Journal did not show status activity hint after Workshop activity.");
            Assert.AreEqual(1, CountVisibleStatusRows(runtime.Journal), "KNOWLEDGE-STATUS-E2E-001 failed: Objective Journal showed duplicate status rows after first activity.");
            AssertOneTrackedHud("KNOWLEDGE-STATUS-E2E-001 failed: tracking created duplicate HUD instances.");
            Assert.IsTrue(runtime.TrackedHud.IsVisible, "KNOWLEDGE-STATUS-E2E-001 failed: tracked HUD did not become visible.");
            StringAssert.Contains(StatusTitle, runtime.TrackedHud.VisibleText, "KNOWLEDGE-STATUS-E2E-001 failed: tracked HUD did not include status hint title.");

            ScreenshotEvidence.Capture(ScreenshotPath);
            yield return ScreenshotEvidence.WaitForNonZeroFile(ScreenshotPath, 3f, "KNOWLEDGE-STATUS-E2E-001 failed: Game View screenshot evidence was not written.");

            string studentFile = Path.Combine(_saveRoot, TestSlotId, "student-life-progress.json");
            Assert.IsTrue(File.Exists(studentFile), "KNOWLEDGE-STATUS-E2E-001 failed: student progress save file missing after activity.");
            string saved = File.ReadAllText(studentFile);
            var saveData = JsonUtility.FromJson<StudentLifeProgressSaveData>(saved);
            Assert.AreEqual(1, saveData.StudentConditionStatuses.Count(entry => string.Equals(entry.Id, StatusId, StringComparison.Ordinal) && entry.Value == 1), "KNOWLEDGE-STATUS-E2E-001 failed: status value should be saved exactly once before reload.");
            Assert.AreEqual(1, saveData.TodayResultLogIds.Count(id => string.Equals(id, statusLog, StringComparison.Ordinal)), "KNOWLEDGE-STATUS-E2E-001 failed: status result log should be saved exactly once before reload.");
            Assert.AreEqual(1, runtime.Student.Progress.GetTodayResultLogIds().Count(id => string.Equals(id, statusLog, StringComparison.Ordinal)), "KNOWLEDGE-STATUS-E2E-001 failed: status result log should exist exactly once before reload.");

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return PlayerFlowE2EDriver.LoadScene("MainMenu");
            PlayerFlowE2EDriver.EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton("SaveSlotCard_" + TestSlotId, "LoadButton"));
            yield return PlayerFlowE2EDriver.WaitForScene("Town", 20f, "KNOWLEDGE-STATUS-E2E-001 failed: SaveSlot Load did not re-enter Town.");
            EnsureTownInstallers();
            yield return WaitForRuntime(15f);

            var reloaded = FindRuntime();
            var reloadedStatus = RequireStatus(StatusId);
            Assert.AreEqual(1, reloaded.Student.Progress.GetStatusValue(reloadedStatus), "KNOWLEDGE-STATUS-E2E-001 failed: status value did not restore exactly once after reload.");
            Assert.AreEqual(1, reloaded.Student.Progress.GetTodayResultLogIds().Count(id => string.Equals(id, statusLog, StringComparison.Ordinal)), "KNOWLEDGE-STATUS-E2E-001 failed: reload duplicated or removed the status result log.");
            reloaded.ObjectiveProvider.RefreshNow();
            yield return PlayerFlowE2EDriver.PressTab(keyboard);
            Assert.IsTrue(reloaded.Journal.IsVisible, "KNOWLEDGE-STATUS-E2E-001 failed: Tab did not open Objective Journal after reload.");
            yield return SelectStatusHintAndTrack(reloaded, "KNOWLEDGE-STATUS-E2E-001 failed: reloaded Objective Journal did not show status activity hint.");
            Assert.AreEqual(1, CountVisibleStatusRows(reloaded.Journal), "KNOWLEDGE-STATUS-E2E-001 failed: reload duplicated status Journal rows.");
            AssertOneTrackedHud("KNOWLEDGE-STATUS-E2E-001 failed: reload created duplicate tracked HUD instances.");

            yield return PlayerFlowE2EDriver.PressTab(keyboard);
            Assert.IsFalse(reloaded.Journal.IsVisible, "KNOWLEDGE-STATUS-E2E-001 failed: second Tab did not close Objective Journal before repeated activity.");
            var reloadedWorkshop = RequireObject(LocationNpcRuntimeInstaller.LocationObjectName("location.workshop"), "KNOWLEDGE-STATUS-E2E-001 failed: reloaded workshop location object missing.");
            yield return WalkToLocationAndOpenActivityPanel(reloaded, keyboard, reloadedWorkshop, "KNOWLEDGE-STATUS-E2E-001 failed: could not reopen Workshop activity panel after reload.");
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(reloaded.LocationPanel.transform, "LocationActivityButton_0"));
            Assert.AreEqual(2, reloaded.Student.Progress.GetStatusValue(reloadedStatus), "KNOWLEDGE-STATUS-E2E-001 failed: repeated player activity should apply a new request exactly once.");
            reloaded.ObjectiveProvider.RefreshNow();
            yield return PlayerFlowE2EDriver.PressTab(keyboard);
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(reloaded.Journal.transform, "ObjectiveCategory_Hints"));
            Assert.AreEqual(1, CountVisibleStatusRows(reloaded.Journal), "KNOWLEDGE-STATUS-E2E-001 failed: repeated activity duplicated status Journal rows.");
        }

        private static IEnumerator StartNewTownFromSaveSlotUi()
        {
            yield return PlayerFlowE2EDriver.LoadScene("MainMenu");
            PlayerFlowE2EDriver.EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton("SaveSlotCard_" + TestSlotId, "NewGameButton"));
            var creator = GameObject.Find("SpumCharacterCreatorRoot") ?? GameObject.Find("SpumCharacterCreatorPanel");
            if (creator != null) yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(creator.transform, "ConfirmButton"));
            yield return PlayerFlowE2EDriver.WaitForScene("Town", 20f, "KNOWLEDGE-STATUS-E2E-001 failed: SaveSlot New Game did not enter Town.");
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
                var workshop = GameObject.Find(LocationNpcRuntimeInstaller.LocationObjectName("location.workshop"));
                var panel = Object.FindFirstObjectByType<LocationIdentityPanel>(FindObjectsInactive.Include);
                var journal = Object.FindFirstObjectByType<ObjectiveJournalPanel>(FindObjectsInactive.Include);
                var provider = Object.FindFirstObjectByType<ObjectiveJournalRuntimeProvider>(FindObjectsInactive.Include);
                var hud = Object.FindFirstObjectByType<TrackedObjectiveHud>(FindObjectsInactive.Include);
                lastState = "scene=" + SceneManager.GetActiveScene().name
                    + " player=" + (player != null)
                    + " router=" + (player != null && player.GetComponent<PlayerInteractionRouter>() != null)
                    + " student=" + (player != null && player.GetComponent<StudentLifeProgressComponent>() != null)
                    + " workshop=" + (workshop != null)
                    + " panel=" + (panel != null)
                    + " journal=" + (journal != null)
                    + " provider=" + (provider != null)
                    + " hud=" + (hud != null)
                    + " eventSystem=" + (EventSystem.current != null);

                if (player != null && player.GetComponent<PlayerInteractionRouter>() != null && player.GetComponent<StudentLifeProgressComponent>() != null && workshop != null && workshop.GetComponent<LocationActivityInteractor>() != null && panel != null && journal != null && provider != null && hud != null && EventSystem.current != null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("KNOWLEDGE-STATUS-E2E-001 failed: Town runtime did not install within timeout. Last state: " + lastState);
        }

        private static RuntimeState FindRuntime()
        {
            var player = GameObject.Find("Player");
            Assert.IsNotNull(player);
            return new RuntimeState(
                player,
                player.GetComponent<PlayerInteractionRouter>(),
                player.GetComponent<StudentLifeProgressComponent>(),
                Object.FindFirstObjectByType<LocationIdentityPanel>(FindObjectsInactive.Include),
                Object.FindFirstObjectByType<ObjectiveJournalPanel>(FindObjectsInactive.Include),
                Object.FindFirstObjectByType<ObjectiveJournalRuntimeProvider>(FindObjectsInactive.Include),
                Object.FindFirstObjectByType<TrackedObjectiveHud>(FindObjectsInactive.Include));
        }

        private static GameObject RequireObject(string name, string failure)
        {
            var go = GameObject.Find(name);
            Assert.IsNotNull(go, failure);
            return go;
        }

        private static StatusDefinition RequireStatus(string statusId)
        {
            var registry = Rootborn.Game.Managers.Managers.Data.Registry;
            var status = registry.StudentConditionStatuses.FirstOrDefault(definition => definition != null && definition.Id == statusId);
            Assert.IsNotNull(status, "KNOWLEDGE-STATUS-E2E-001 failed: required status was not registered: " + statusId);
            return status;
        }

        private static IEnumerator WalkToLocationAndOpenActivityPanel(RuntimeState runtime, Keyboard keyboard, GameObject location, string failure)
        {
            yield return PlayerFlowE2EDriver.WalkPlayerWithKeyboardTo(runtime.Player, keyboard, location.transform.position + new Vector3(0.7f, 0f, 0f), 0.06f, 720, failure);
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, failure + " Prompt was not visible.");
            StringAssert.Contains("Explore", runtime.Router.PromptText, failure + " Prompt did not select the location activity interaction.");
            StringAssert.Contains("Workshop", runtime.Router.PromptText, failure + " Prompt did not target Workshop.");
            yield return PlayerFlowE2EDriver.PressInteractKey(keyboard);
            yield return null;
        }

        private static IEnumerator SelectStatusHintAndTrack(RuntimeState runtime, string failure)
        {
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(runtime.Journal.transform, "ObjectiveCategory_Hints"));
            yield return PlayerFlowE2EDriver.ClickButton(ObjectiveJournalE2EDriver.FindRowButtonContaining(runtime.Journal, StatusTitle, failure));
            StringAssert.Contains(StatusTitle, runtime.Journal.VisibleText, failure);
            Assert.AreEqual("status-activity:" + StatusId, runtime.Journal.SelectedStableKeyForTests, failure + " Selected stable key did not match status hint.");
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(runtime.Journal.transform, "ObjectiveTrackButton"));
        }

        private static int CountVisibleStatusRows(ObjectiveJournalPanel journal)
        {
            return journal.GetComponentsInChildren<Button>(true).Count(button =>
            {
                if (button == null || !button.name.StartsWith("ObjectiveRow_")) return false;
                var text = button.GetComponentInChildren<Text>(true);
                return text != null && !string.IsNullOrEmpty(text.text) && text.text.Contains(StatusTitle);
            });
        }

        private static void AssertOneTrackedHud(string failure)
        {
            var huds = Object.FindObjectsByType<TrackedObjectiveHud>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.AreEqual(1, huds.Length, failure);
        }

        private static void CleanupResidue()
        {
            PlayerFlowE2EDriver.DestroyAllNamed("SaveSlotSelectPanel");
            PlayerFlowE2EDriver.DestroyAllNamed("SaveSlotCanvas");
            PlayerFlowE2EDriver.DestroyAllNamed("SaveSlotSelectRoot");
            PlayerFlowE2EDriver.DestroyAllNamed("SpumCharacterCreatorPanel");
            PlayerFlowE2EDriver.DestroyAllNamed("SpumCharacterCreatorRoot");
            PlayerFlowE2EDriver.DestroyAllNamed("LocationIdentityPanel");
            PlayerFlowE2EDriver.DestroyAllNamed("LocationNpcCanvas");
            PlayerFlowE2EDriver.DestroyAllNamed("EventSystem");
            PlayerFlowE2EDriver.DestroyAllNamed("Player");
        }

        private readonly struct RuntimeState
        {
            public readonly GameObject Player;
            public readonly PlayerInteractionRouter Router;
            public readonly StudentLifeProgressComponent Student;
            public readonly LocationIdentityPanel LocationPanel;
            public readonly ObjectiveJournalPanel Journal;
            public readonly ObjectiveJournalRuntimeProvider ObjectiveProvider;
            public readonly TrackedObjectiveHud TrackedHud;

            public RuntimeState(GameObject player, PlayerInteractionRouter router, StudentLifeProgressComponent student, LocationIdentityPanel locationPanel, ObjectiveJournalPanel journal, ObjectiveJournalRuntimeProvider objectiveProvider, TrackedObjectiveHud trackedHud)
            {
                Player = player;
                Router = router;
                Student = student;
                LocationPanel = locationPanel;
                Journal = journal;
                ObjectiveProvider = objectiveProvider;
                TrackedHud = trackedHud;
            }
        }
    }
}
