using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Crafting;
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

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    public sealed class TownResourceRecipeActivityE2EScenarioTests : Rootborn.Tests.PlayMode.PlayModeInputFixture
    {
        private const string TestSlotId = "slot-0";
        private const string ActivityId = "location.activity.square-help-request";
        private const string RecipeId = "recipe.town.field-notes";
        // Players see the localized recipe name, not its key.
        private static string RecipeTitle => Rootborn.Game.Localization.GameText.Get("recipe.town.field-notes.name");
        private const string ScreenshotPath = "Builds/Logs/town-resource-recipe-activity-e2e.png";
        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.timeScale = 1f;
            CleanupResidue();
            PlayerFlowE2EDriver.EnsurePassiveEventSystem();
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-town-resource-recipe-e2e", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator TOWN_RESOURCE_RECIPE_E2E_001_ActivityRewardHintReloadNoDuplicate()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            string recipeUnlockLog = LocationActivityLogCodec.EncodeUnlock(ActivityId, RecipeId);

            yield return StartNewTownFromSaveSlotUi();
            var runtime = FindRuntime();
            var square = RequireObject(LocationNpcRuntimeInstaller.LocationObjectName("location.town-square"), "TOWN-RESOURCE-RECIPE-E2E-001 failed: town square location object missing.");

            yield return WalkToTownSquareAndOpenActivityPanel(runtime, keyboard, square, "TOWN-RESOURCE-RECIPE-E2E-001 failed: could not open Town Square activity panel.");
            Assert.IsTrue(runtime.LocationPanel.IsOpen, "TOWN-RESOURCE-RECIPE-E2E-001 failed: Town Square activity panel was not visible after player interaction.");
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(runtime.LocationPanel.transform, "LocationActivityButton_0"));
            Assert.AreEqual(LifeActivityResultKind.Applied, runtime.LocationPanel.LastResult.Kind, "TOWN-RESOURCE-RECIPE-E2E-001 failed: Town Square activity did not apply through visible UI.");
            CollectionAssert.Contains(runtime.LocationPanel.LastResult.OutcomeLogIds, recipeUnlockLog, "TOWN-RESOURCE-RECIPE-E2E-001 failed: activity result did not include resource/recipe unlock log.");

            var progress = LoadProgress(runtime.Player);
            Assert.AreEqual(1, progress.RevealedRecipeIds.Count(id => id == RecipeId), "TOWN-RESOURCE-RECIPE-E2E-001 failed: recipe progress was not revealed exactly once.");
            Assert.AreEqual(1, progress.SeenResourceIds.Length, "TOWN-RESOURCE-RECIPE-E2E-001 failed: resource sighting was not recorded.");

            runtime.ObjectiveProvider.RefreshNow();
            // The location panel holds gameplay shortcuts while open; the player closes it before pressing Tab.
            if (runtime.LocationPanel.IsOpen) yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(runtime.LocationPanel.transform, "CloseLocation"));
            yield return PlayerFlowE2EDriver.DismissDailyEvent();
            yield return PlayerFlowE2EDriver.PressTab(keyboard);
            Assert.IsTrue(runtime.Journal.IsVisible, "TOWN-RESOURCE-RECIPE-E2E-001 failed: Tab did not open Objective Journal. " + PlayerFlowE2EDriver.InputLockOwners());
            yield return SelectResourceRecipeHint(runtime.Journal, "TOWN-RESOURCE-RECIPE-E2E-001 failed: Objective Journal did not show resource/recipe hint after activity.");
            Assert.AreEqual(1, CountVisibleRecipeRows(runtime.Journal), "TOWN-RESOURCE-RECIPE-E2E-001 failed: Objective Journal showed duplicate resource/recipe rows after first reveal.");

            ScreenshotEvidence.Capture(ScreenshotPath);
            yield return ScreenshotEvidence.WaitForNonZeroFile(ScreenshotPath, 3f, "TOWN-RESOURCE-RECIPE-E2E-001 failed: Game View screenshot evidence was not written.");

            string progressFile = Path.Combine(_saveRoot, TestSlotId, "resource-recipe-progress-local-player.json");
            Assert.IsTrue(File.Exists(progressFile), "TOWN-RESOURCE-RECIPE-E2E-001 failed: resource/recipe progress save file missing after activity.");
            StringAssert.Contains(RecipeId, File.ReadAllText(progressFile), "TOWN-RESOURCE-RECIPE-E2E-001 failed: saved resource/recipe progress did not contain recipe id.");

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return PlayerFlowE2EDriver.LoadScene("MainMenu");
            PlayerFlowE2EDriver.EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton("SaveSlotCard_" + TestSlotId, "LoadButton"));
            yield return PlayerFlowE2EDriver.WaitForScene("Town", 20f, "TOWN-RESOURCE-RECIPE-E2E-001 failed: SaveSlot Load did not re-enter Town.");
            EnsureTownInstallers();
            yield return WaitForRuntime(15f);

            var reloaded = FindRuntime();
            var reloadedProgress = LoadProgress(reloaded.Player);
            Assert.AreEqual(1, reloadedProgress.RevealedRecipeIds.Count(id => id == RecipeId), "TOWN-RESOURCE-RECIPE-E2E-001 failed: recipe reveal did not restore exactly once after reload.");
            reloaded.ObjectiveProvider.RefreshNow();
            // The location panel holds gameplay shortcuts while open; the player closes it before pressing Tab.
            if (reloaded.LocationPanel.IsOpen) yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(reloaded.LocationPanel.transform, "CloseLocation"));
            yield return PlayerFlowE2EDriver.DismissDailyEvent();
            yield return PlayerFlowE2EDriver.PressTab(keyboard);
            Assert.IsTrue(reloaded.Journal.IsVisible, "TOWN-RESOURCE-RECIPE-E2E-001 failed: Tab did not open Objective Journal after reload.");
            yield return SelectResourceRecipeHint(reloaded.Journal, "TOWN-RESOURCE-RECIPE-E2E-001 failed: reloaded Objective Journal did not show resource/recipe hint.");
            Assert.AreEqual(1, CountVisibleRecipeRows(reloaded.Journal), "TOWN-RESOURCE-RECIPE-E2E-001 failed: reload duplicated resource/recipe Journal rows.");

            // The location panel holds gameplay shortcuts while open; the player closes it before pressing Tab.
            if (reloaded.LocationPanel.IsOpen) yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(reloaded.LocationPanel.transform, "CloseLocation"));
            yield return PlayerFlowE2EDriver.DismissDailyEvent();
            yield return PlayerFlowE2EDriver.PressTab(keyboard);
            Assert.IsFalse(reloaded.Journal.IsVisible, "TOWN-RESOURCE-RECIPE-E2E-001 failed: second Tab did not close Objective Journal before repeated interaction.");
            var reloadedSquare = RequireObject(LocationNpcRuntimeInstaller.LocationObjectName("location.town-square"), "TOWN-RESOURCE-RECIPE-E2E-001 failed: reloaded town square location object missing.");
            yield return WalkToTownSquareAndOpenActivityPanel(reloaded, keyboard, reloadedSquare, "TOWN-RESOURCE-RECIPE-E2E-001 failed: could not reopen Town Square activity panel after reload.");
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(reloaded.LocationPanel.transform, "LocationActivityButton_0"));

            var repeatedProgress = LoadProgress(reloaded.Player);
            Assert.AreEqual(1, repeatedProgress.RevealedRecipeIds.Count(id => id == RecipeId), "TOWN-RESOURCE-RECIPE-E2E-001 failed: repeated activity duplicated recipe reveal state.");
            Assert.IsFalse(reloaded.LocationPanel.LastResult.OutcomeLogIds.Contains(recipeUnlockLog), "TOWN-RESOURCE-RECIPE-E2E-001 failed: repeated activity emitted duplicate recipe unlock log.");
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
            yield return PlayerFlowE2EDriver.WaitForScene("Town", 20f, "TOWN-RESOURCE-RECIPE-E2E-001 failed: SaveSlot New Game did not enter Town.");
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
                var square = GameObject.Find(LocationNpcRuntimeInstaller.LocationObjectName("location.town-square"));
                var panel = Object.FindFirstObjectByType<LocationIdentityPanel>(FindObjectsInactive.Include);
                var journal = Object.FindFirstObjectByType<ObjectiveJournalPanel>(FindObjectsInactive.Include);
                var provider = Object.FindFirstObjectByType<ObjectiveJournalRuntimeProvider>(FindObjectsInactive.Include);
                lastState = "scene=" + SceneManager.GetActiveScene().name
                    + " player=" + (player != null)
                    + " router=" + (player != null && player.GetComponent<PlayerInteractionRouter>() != null)
                    + " student=" + (player != null && player.GetComponent<StudentLifeProgressComponent>() != null)
                    + " square=" + (square != null)
                    + " panel=" + (panel != null)
                    + " journal=" + (journal != null)
                    + " provider=" + (provider != null)
                    + " eventSystem=" + (EventSystem.current != null);

                if (player != null && player.GetComponent<PlayerInteractionRouter>() != null && player.GetComponent<StudentLifeProgressComponent>() != null && square != null && square.GetComponent<LocationActivityInteractor>() != null && panel != null && journal != null && provider != null && EventSystem.current != null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("TOWN-RESOURCE-RECIPE-E2E-001 failed: Town runtime did not install within timeout. Last state: " + lastState);
        }

        private static RuntimeState FindRuntime()
        {
            var player = GameObject.Find("Player");
            var panel = Object.FindFirstObjectByType<LocationIdentityPanel>(FindObjectsInactive.Include);
            var journal = Object.FindFirstObjectByType<ObjectiveJournalPanel>(FindObjectsInactive.Include);
            var provider = Object.FindFirstObjectByType<ObjectiveJournalRuntimeProvider>(FindObjectsInactive.Include);
            Assert.IsNotNull(player);
            Assert.IsNotNull(panel);
            Assert.IsNotNull(journal);
            Assert.IsNotNull(provider);
            return new RuntimeState(player, player.GetComponent<PlayerInteractionRouter>(), player.GetComponent<StudentLifeProgressComponent>(), panel, journal, provider);
        }

        private static GameObject RequireObject(string name, string failure)
        {
            var go = GameObject.Find(name);
            Assert.IsNotNull(go, failure);
            return go;
        }

        private static IEnumerator WalkToTownSquareAndOpenActivityPanel(RuntimeState runtime, Keyboard keyboard, GameObject square, string failure)
        {
            yield return PlayerFlowE2EDriver.WalkPlayerWithKeyboardTo(runtime.Player, keyboard, square.transform.position + new Vector3(-1.45f, 0f, 0f), 0.06f, 480, failure);
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, failure + " Prompt was not visible.");
            StringAssert.Contains(Rootborn.Game.Localization.GameText.Format("life.location.explore", "").Replace("", string.Empty).Trim(), runtime.Router.PromptText, failure + " Prompt did not select the location activity interaction.");
            yield return PlayerFlowE2EDriver.PressInteractKey(keyboard);
            yield return null;
        }

        private static ResourceRecipeProgress LoadProgress(GameObject player)
        {
            var student = player.GetComponent<StudentLifeProgressComponent>().EnsureProgress();
            var identity = player.GetComponent<PlayerIdentity>();
            string playerId = identity != null && !string.IsNullOrEmpty(identity.PlayerId) ? identity.PlayerId : student.PlayerId;
            return ResourceRecipeProgressPersistence.LoadOrCreate(student.SaveSlot, playerId);
        }

        private static IEnumerator SelectResourceRecipeHint(ObjectiveJournalPanel journal, string failure)
        {
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(journal.transform, "ObjectiveCategory_Hints"));
            yield return PlayerFlowE2EDriver.ClickButton(ObjectiveJournalE2EDriver.FindRowButtonContaining(journal, RecipeTitle, failure));
            StringAssert.Contains(RecipeTitle, journal.VisibleText, failure);
            Assert.AreEqual("resource-recipe:" + RecipeId, journal.SelectedStableKeyForTests, failure + " Selected stable key did not match recipe hint.");
        }

        private static int CountVisibleRecipeRows(ObjectiveJournalPanel journal)
        {
            return journal.GetComponentsInChildren<Button>(true).Count(button =>
            {
                if (button == null || !button.name.StartsWith("ObjectiveRow_")) return false;
                var text = button.GetComponentInChildren<Text>(true);
                return text != null && !string.IsNullOrEmpty(text.text) && text.text.Contains(RecipeTitle);
            });
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

            public RuntimeState(GameObject player, PlayerInteractionRouter router, StudentLifeProgressComponent student, LocationIdentityPanel locationPanel, ObjectiveJournalPanel journal, ObjectiveJournalRuntimeProvider objectiveProvider)
            {
                Player = player;
                Router = router;
                Student = student;
                LocationPanel = locationPanel;
                Journal = journal;
                ObjectiveProvider = objectiveProvider;
            }
        }
    }
}
