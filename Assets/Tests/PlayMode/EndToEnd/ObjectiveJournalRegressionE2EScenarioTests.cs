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
    public sealed class ObjectiveJournalRegressionE2EScenarioTests : Rootborn.Tests.PlayMode.PlayModeInputFixture
    {
        private const string TestSlotId = "slot-0";
        private const string HintRouteTitle = "objective.daily.library.title";
        private const string ScreenshotPath = "Builds/Logs/objective-journal-source-collision-e2e.png";
        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.timeScale = 1f;
            CleanupResidue();
            PlayerFlowE2EDriver.EnsurePassiveEventSystem();
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-objective-journal-source-collision-e2e", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator OBJECTIVE_JOURNAL_SOURCE_COLLISION_PM_001_MultiSourceReloadNoDuplicateHud()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            yield return StartNewTownFromSaveSlotUi();
            var runtime = FindRuntime();
            runtime.ObjectiveProvider.RefreshNow();

            yield return PlayerFlowE2EDriver.PressTab(keyboard);
            Assert.IsTrue(runtime.Journal.IsVisible, "OBJECTIVE-JOURNAL-SOURCE-COLLISION-PM-001 failed: Tab did not open Objective Journal.");
            AssertNoLegacyObjectiveHudObjects("OBJECTIVE-JOURNAL-SOURCE-COLLISION-PM-001 before tracking");
            Assert.AreEqual(1, Object.FindObjectsByType<TrackedObjectiveHud>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, "OBJECTIVE-JOURNAL-SOURCE-COLLISION-PM-001 failed: Town created duplicate tracked HUD instances.");

            yield return SelectCategoryAndAssertRowContaining(runtime.Journal, "Goals", "Student Day Guide", "OBJECTIVE-JOURNAL-SOURCE-COLLISION-PM-001 failed: Goals source was missing.");
            string goalStableKey = runtime.Journal.SelectedStableKeyForTests;
            Assert.IsFalse(string.IsNullOrEmpty(goalStableKey), "OBJECTIVE-JOURNAL-SOURCE-COLLISION-PM-001 failed: Goals row had blank stable key.");
            int goalsRowsBefore = runtime.Journal.VisibleRowCountForTests;

            yield return SelectCategoryAndAssertRowContaining(runtime.Journal, "Hints", HintRouteTitle, "OBJECTIVE-JOURNAL-SOURCE-COLLISION-PM-001 failed: Hints route source was missing.");
            string hintStableKey = runtime.Journal.SelectedStableKeyForTests;
            Assert.IsFalse(string.IsNullOrEmpty(hintStableKey), "OBJECTIVE-JOURNAL-SOURCE-COLLISION-PM-001 failed: Hints row had blank stable key.");
            Assert.AreNotEqual(goalStableKey, hintStableKey, "OBJECTIVE-JOURNAL-SOURCE-COLLISION-PM-001 failed: Goals and Hints sources emitted colliding stable keys.");
            int hintsRowsBefore = runtime.Journal.VisibleRowCountForTests;

            yield return SelectCategoryAndAssertRowContaining(runtime.Journal, "Quests", "Quest", "OBJECTIVE-JOURNAL-SOURCE-COLLISION-PM-001 failed: Quest source was missing.");
            string questStableKey = runtime.Journal.SelectedStableKeyForTests;
            Assert.IsFalse(string.IsNullOrEmpty(questStableKey), "OBJECTIVE-JOURNAL-SOURCE-COLLISION-PM-001 failed: Quest row had blank stable key.");
            Assert.AreNotEqual(goalStableKey, questStableKey, "OBJECTIVE-JOURNAL-SOURCE-COLLISION-PM-001 failed: Goals and Quests sources emitted colliding stable keys.");
            Assert.AreNotEqual(hintStableKey, questStableKey, "OBJECTIVE-JOURNAL-SOURCE-COLLISION-PM-001 failed: Hints and Quests sources emitted colliding stable keys.");

            yield return SelectCategoryAndAssertRowContaining(runtime.Journal, "Hints", HintRouteTitle, "OBJECTIVE-JOURNAL-SOURCE-COLLISION-PM-001 failed: Hints source disappeared before tracking.");
            yield return PlayerFlowE2EDriver.ClickButton(ObjectiveJournalE2EDriver.FindTrackButton(runtime.Journal));
            Assert.IsTrue(runtime.TrackedHud.IsVisible, "OBJECTIVE-JOURNAL-SOURCE-COLLISION-PM-001 failed: Track button did not update TrackedObjectiveHud.");
            Assert.AreEqual(1, Object.FindObjectsByType<TrackedObjectiveHud>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, "OBJECTIVE-JOURNAL-SOURCE-COLLISION-PM-001 failed: tracking created duplicate tracked HUD instances.");
            Assert.IsTrue(ObjectiveJournalTrackingStore.TryLoad(ActiveSaveContext.Metadata, out var trackedCategory, out var trackedStableKey), "OBJECTIVE-JOURNAL-SOURCE-COLLISION-PM-001 failed: tracked stable key was not saved.");
            Assert.AreEqual("Hints", trackedCategory);
            Assert.AreEqual(hintStableKey, trackedStableKey);

            ScreenshotEvidence.Capture(ScreenshotPath);
            yield return ScreenshotEvidence.WaitForNonZeroFile(ScreenshotPath, 3f, "OBJECTIVE-JOURNAL-SOURCE-COLLISION-PM-001 failed: Game View screenshot evidence was not written.");

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return PlayerFlowE2EDriver.LoadScene("MainMenu");
            PlayerFlowE2EDriver.EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton("SaveSlotCard_" + TestSlotId, "LoadButton"));
            yield return PlayerFlowE2EDriver.WaitForScene("Town", 20f, "OBJECTIVE-JOURNAL-SOURCE-COLLISION-PM-001 failed: SaveSlot Load did not re-enter Town.");
            EnsureTownInstallers();
            yield return WaitForRuntime(15f);

            var reloaded = FindRuntime();
            reloaded.ObjectiveProvider.RefreshNow();
            yield return PlayerFlowE2EDriver.PressTab(keyboard);
            Assert.IsTrue(reloaded.Journal.IsVisible, "OBJECTIVE-JOURNAL-SOURCE-COLLISION-PM-001 failed: Tab did not open Objective Journal after reload.");
            AssertNoLegacyObjectiveHudObjects("OBJECTIVE-JOURNAL-SOURCE-COLLISION-PM-001 after reload");
            Assert.AreEqual(1, Object.FindObjectsByType<TrackedObjectiveHud>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, "OBJECTIVE-JOURNAL-SOURCE-COLLISION-PM-001 failed: reload created duplicate tracked HUD instances.");
            Assert.IsTrue(reloaded.TrackedHud.IsVisible, "OBJECTIVE-JOURNAL-SOURCE-COLLISION-PM-001 failed: tracked HUD did not restore after reload.");
            Assert.IsTrue(ObjectiveJournalTrackingStore.TryLoad(ActiveSaveContext.Metadata, out var reloadedCategory, out var reloadedStableKey), "OBJECTIVE-JOURNAL-SOURCE-COLLISION-PM-001 failed: tracked stable key was missing after reload.");
            Assert.AreEqual(trackedCategory, reloadedCategory);
            Assert.AreEqual(trackedStableKey, reloadedStableKey);

            yield return SelectCategoryAndAssertRowContaining(reloaded.Journal, "Goals", "Student Day Guide", "OBJECTIVE-JOURNAL-SOURCE-COLLISION-PM-001 failed: Goals row missing after reload.");
            Assert.AreEqual(goalsRowsBefore, reloaded.Journal.VisibleRowCountForTests, "OBJECTIVE-JOURNAL-SOURCE-COLLISION-PM-001 failed: reload duplicated or removed Goals rows.");
            yield return SelectCategoryAndAssertRowContaining(reloaded.Journal, "Hints", HintRouteTitle, "OBJECTIVE-JOURNAL-SOURCE-COLLISION-PM-001 failed: Hints row missing after reload.");
            Assert.AreEqual(hintsRowsBefore, reloaded.Journal.VisibleRowCountForTests, "OBJECTIVE-JOURNAL-SOURCE-COLLISION-PM-001 failed: reload duplicated or removed Hints rows.");
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
            yield return PlayerFlowE2EDriver.WaitForScene("Town", 20f, "OBJECTIVE-JOURNAL-SOURCE-COLLISION-PM-001 failed: SaveSlot New Game did not enter Town.");
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
                var journal = Object.FindFirstObjectByType<ObjectiveJournalPanel>(FindObjectsInactive.Include);
                var provider = Object.FindFirstObjectByType<ObjectiveJournalRuntimeProvider>(FindObjectsInactive.Include);
                var hud = Object.FindFirstObjectByType<TrackedObjectiveHud>(FindObjectsInactive.Include);
                lastState = "scene=" + SceneManager.GetActiveScene().name
                    + " player=" + (player != null)
                    + " router=" + (player != null && player.GetComponent<PlayerInteractionRouter>() != null)
                    + " student=" + (player != null && player.GetComponent<StudentLifeProgressComponent>() != null)
                    + " journal=" + (journal != null)
                    + " provider=" + (provider != null)
                    + " hud=" + (hud != null)
                    + " eventSystem=" + (EventSystem.current != null);

                if (player != null && player.GetComponent<PlayerInteractionRouter>() != null && player.GetComponent<StudentLifeProgressComponent>() != null && journal != null && provider != null && hud != null && EventSystem.current != null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("OBJECTIVE-JOURNAL-SOURCE-COLLISION-PM-001 failed: Town runtime did not install within timeout. Last state: " + lastState);
        }

        private static RuntimeState FindRuntime()
        {
            var player = GameObject.Find("Player");
            Assert.IsNotNull(player);
            var journal = Object.FindFirstObjectByType<ObjectiveJournalPanel>(FindObjectsInactive.Include);
            var provider = Object.FindFirstObjectByType<ObjectiveJournalRuntimeProvider>(FindObjectsInactive.Include);
            var hud = Object.FindFirstObjectByType<TrackedObjectiveHud>(FindObjectsInactive.Include);
            Assert.IsNotNull(journal);
            Assert.IsNotNull(provider);
            Assert.IsNotNull(hud);
            return new RuntimeState(player, journal, provider, hud);
        }

        private static IEnumerator SelectCategoryAndAssertRowContaining(ObjectiveJournalPanel journal, string categoryId, string expectedText, string failure)
        {
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(journal.transform, "ObjectiveCategory_" + categoryId));
            yield return PlayerFlowE2EDriver.ClickButton(ObjectiveJournalE2EDriver.FindRowButtonContaining(journal, expectedText, failure));
            StringAssert.Contains(expectedText, journal.VisibleText, failure);
            Assert.IsFalse(string.IsNullOrEmpty(journal.SelectedStableKeyForTests), failure + " Selected stable key was blank.");
        }

        private static void AssertNoLegacyObjectiveHudObjects(string phase)
        {
            string[] forbiddenNames =
            {
                "QuestChainHudWidget",
                "QuestChainHudRoot",
                "CareerInterestHudWidget",
                "CareerInterestHudRoot",
                "CampaignHudWidget",
                "CampaignHudRoot",
                "ObjectiveHudWidget",
                "ObjectiveHudRoot"
            };

            for (int i = 0; i < forbiddenNames.Length; i++)
            {
                Assert.IsNull(GameObject.Find(forbiddenNames[i]), phase + " found legacy objective HUD object: " + forbiddenNames[i]);
            }
        }

        private static void CleanupResidue()
        {
            PlayerFlowE2EDriver.DestroyAllNamed("SaveSlotSelectPanel");
            PlayerFlowE2EDriver.DestroyAllNamed("SaveSlotCanvas");
            PlayerFlowE2EDriver.DestroyAllNamed("SaveSlotSelectRoot");
            PlayerFlowE2EDriver.DestroyAllNamed("SpumCharacterCreatorPanel");
            PlayerFlowE2EDriver.DestroyAllNamed("SpumCharacterCreatorRoot");
            PlayerFlowE2EDriver.DestroyAllNamed("EventSystem");
            PlayerFlowE2EDriver.DestroyAllNamed("Player");
        }

        private readonly struct RuntimeState
        {
            public readonly GameObject Player;
            public readonly ObjectiveJournalPanel Journal;
            public readonly ObjectiveJournalRuntimeProvider ObjectiveProvider;
            public readonly TrackedObjectiveHud TrackedHud;

            public RuntimeState(GameObject player, ObjectiveJournalPanel journal, ObjectiveJournalRuntimeProvider objectiveProvider, TrackedObjectiveHud trackedHud)
            {
                Player = player;
                Journal = journal;
                ObjectiveProvider = objectiveProvider;
                TrackedHud = trackedHud;
            }
        }
    }
}
