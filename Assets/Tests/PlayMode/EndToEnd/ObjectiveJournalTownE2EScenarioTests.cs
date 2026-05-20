using System.Collections;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.UI.MainMenu;
using Rootborn.UI.Objectives;
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
    public sealed class ObjectiveJournalTownE2EScenarioTests : InputTestFixture
    {
        private const string TestSlotId = "slot-0";
        private const string ScreenshotPath = "Builds/Logs/objective-journal-town-e2e.png";
        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.timeScale = 1f;
            CleanupResidue();
            EnsurePassiveEventSystem();
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-objective-journal-town-e2e", System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveRoot);
            SaveService.SetRootDirectoryForTests(_saveRoot);
            SaveSlotSelectPanel.SetSaveRootForTests(_saveRoot);
            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
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
        public IEnumerator OBJECTIVE_JOURNAL_E2E_001_OpenTrackRefreshAndReload()
        {
            return OBJECTIVE_JOURNAL_E2E_001_TownFlowTracksStudentDayObjectiveRefreshesAfterActivityAndRestoresAfterReload();
        }

        [UnityTest]
        public IEnumerator OBJECTIVE_JOURNAL_E2E_001_TownFlowTracksStudentDayObjectiveRefreshesAfterActivityAndRestoresAfterReload()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            yield return LoadScene("MainMenu");
            EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "NewGameButton"));
            yield return null;
            yield return ClickButton(FindButton("SpumCharacterCreatorPanel", "ConfirmButton"));
            yield return WaitForScene("Town", 15f, "OBJECTIVE-JOURNAL-E2E-001 failed: SaveSlot New Game did not enter Town.");
            StudentDayRuntimeInstaller.EnsureForActiveScene();
            CampaignRuntimeInstaller.EnsureForActiveScene();
            yield return WaitForObjectiveRuntime(15f);

            var runtime = FindRuntime();
            int energyBefore = runtime.Student.Progress.Energy;

            yield return PressTab(keyboard);
            Assert.IsTrue(runtime.Journal.IsVisible, "OBJECTIVE-JOURNAL-E2E-001 failed: actual Tab input did not open Objective Journal.");
            StringAssert.Contains("Student Day Guide", runtime.Journal.VisibleText, "OBJECTIVE-JOURNAL-E2E-001 failed: Journal did not show the live Student Day Guide objective.");
            StringAssert.Contains("Energy " + energyBefore, runtime.Journal.VisibleText, "OBJECTIVE-JOURNAL-E2E-001 failed: Journal did not show current student energy.");
            Assert.IsNotNull(FindButton(runtime.Journal.transform, "ObjectiveTrackButton"), "OBJECTIVE-JOURNAL-E2E-001 failed: Journal needs a player-clickable Track button.");

            yield return ClickButton(FindButton(runtime.Journal.transform, "ObjectiveCategory_Quests"));
            StringAssert.Contains("Quest", runtime.Journal.VisibleText, "OBJECTIVE-JOURNAL-E2E-001 failed: Quest category did not display available quest objective entries.");
            AssertNoEmptyCategory(runtime.Journal, "Quests");
            yield return ClickButton(FindButton(runtime.Journal.transform, "ObjectiveRow_0"));
            StringAssert.Contains("Quest", runtime.Journal.VisibleText, "OBJECTIVE-JOURNAL-E2E-001 failed: Quest row click did not keep a quest detail selected.");

            yield return ClickButton(FindButton(runtime.Journal.transform, "ObjectiveCategory_Campaign"));
            StringAssert.Contains("Campaign", runtime.Journal.VisibleText, "OBJECTIVE-JOURNAL-E2E-001 failed: Campaign category did not display campaign objective entries.");
            AssertNoEmptyCategory(runtime.Journal, "Campaign");
            yield return ClickButton(FindButton(runtime.Journal.transform, "ObjectiveRow_0"));
            StringAssert.Contains("Campaign", runtime.Journal.VisibleText, "OBJECTIVE-JOURNAL-E2E-001 failed: Campaign row click did not keep a campaign detail selected.");

            yield return ClickButton(FindButton(runtime.Journal.transform, "ObjectiveCategory_Hints"));
            StringAssert.Contains("Milestone", runtime.Journal.VisibleText, "OBJECTIVE-JOURNAL-E2E-001 failed: Hints category did not display milestone objective entries.");
            AssertNoEmptyCategory(runtime.Journal, "Hints");
            yield return ClickButton(FindButton(runtime.Journal.transform, "ObjectiveRow_0"));
            StringAssert.Contains("Milestone", runtime.Journal.VisibleText, "OBJECTIVE-JOURNAL-E2E-001 failed: Milestone row click did not keep a milestone detail selected.");

            yield return ClickButton(FindButton(runtime.Journal.transform, "ObjectiveCategory_Goals"));
            yield return ClickButton(FindButton(runtime.Journal.transform, "ObjectiveTrackButton"));
            Assert.IsTrue(runtime.TrackedHud.IsVisible, "OBJECTIVE-JOURNAL-E2E-001 failed: Track button did not update TrackedObjectiveHud.");
            StringAssert.Contains("Student Day Guide", runtime.TrackedHud.VisibleText);
            Assert.IsTrue(ObjectiveJournalTrackingStore.TryLoad(ActiveSaveContext.Metadata, out var categoryId, out var stableKey), "OBJECTIVE-JOURNAL-E2E-001 failed: tracked stable key was not saved.");
            Assert.AreEqual("Goals", categoryId);
            Assert.AreEqual("student-day.current", stableKey);

            yield return PressTab(keyboard);
            Assert.IsFalse(runtime.Journal.IsVisible, "OBJECTIVE-JOURNAL-E2E-001 failed: second Tab should close Objective Journal before player movement.");

            var outside = GameObject.Find("OutsideSchoolActivityPoint").GetComponent<OutsideSchoolActivityInteractor>();
            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, outside.transform.position, 0.25f, "OBJECTIVE-JOURNAL-E2E-001 failed: actual keyboard movement could not reach outside-school activity.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "OBJECTIVE-JOURNAL-E2E-001 failed: outside-school prompt was not visible after movement.");
            yield return PressInteractKey(keyboard);
            yield return null;
            Assert.AreEqual(LifeActivityResultKind.Applied, outside.LastResult.Kind, "OBJECTIVE-JOURNAL-E2E-001 failed: outside-school action did not apply through actual input.");
            Assert.Less(runtime.Student.Progress.Energy, energyBefore, "OBJECTIVE-JOURNAL-E2E-001 failed: activity did not change student progress for Journal refresh.");

            yield return PressTab(keyboard);
            string updatedEnergyText = "Energy " + runtime.Student.Progress.Energy;
            StringAssert.Contains(updatedEnergyText, runtime.Journal.VisibleText, "OBJECTIVE-JOURNAL-E2E-001 failed: Journal did not refresh progress text after activity.");
            StringAssert.Contains(updatedEnergyText, runtime.TrackedHud.VisibleText, "OBJECTIVE-JOURNAL-E2E-001 failed: tracked HUD did not refresh from saved stable key after activity.");

            CaptureScreenshot();
            for (int i = 0; i < 5; i++) yield return null;
            Assert.IsTrue(File.Exists(ScreenshotPath), "OBJECTIVE-JOURNAL-E2E-001 failed: Game View screenshot evidence was not written.");

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return LoadScene("MainMenu");
            EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "LoadButton"));
            yield return WaitForScene("Town", 15f, "OBJECTIVE-JOURNAL-E2E-001 failed: SaveSlot Load did not re-enter Town.");
            StudentDayRuntimeInstaller.EnsureForActiveScene();
            CampaignRuntimeInstaller.EnsureForActiveScene();
            yield return WaitForObjectiveRuntime(15f);

            var reloaded = FindRuntime();
            yield return PressTab(keyboard);
            Assert.IsTrue(reloaded.TrackedHud.IsVisible, "OBJECTIVE-JOURNAL-E2E-001 failed: tracked objective was not restored after reload and Journal refresh.");
            StringAssert.Contains("Student Day Guide", reloaded.TrackedHud.VisibleText);
        }

        private static IEnumerator WaitForObjectiveRuntime(float timeoutSeconds)
        {
            float elapsed = 0f;
            string lastState = string.Empty;
            while (elapsed < timeoutSeconds)
            {
                var player = GameObject.Find("Player");
                var outside = GameObject.Find("OutsideSchoolActivityPoint");
                var journal = Object.FindFirstObjectByType<ObjectiveJournalPanel>(FindObjectsInactive.Include);
                var provider = Object.FindFirstObjectByType<ObjectiveJournalRuntimeProvider>(FindObjectsInactive.Include);
                var hud = Object.FindFirstObjectByType<TrackedObjectiveHud>(FindObjectsInactive.Include);
                lastState = "scene=" + SceneManager.GetActiveScene().name
                    + " player=" + (player != null)
                    + " controller=" + (player != null && player.GetComponent<PlayerController>() != null)
                    + " router=" + (player != null && player.GetComponent<PlayerInteractionRouter>() != null)
                    + " student=" + (player != null && player.GetComponent<StudentLifeProgressComponent>() != null)
                    + " outside=" + (outside != null)
                    + " journal=" + (journal != null)
                    + " provider=" + (provider != null)
                    + " hud=" + (hud != null)
                    + " eventSystem=" + (EventSystem.current != null);
                if (player != null &&
                    player.GetComponent<PlayerController>() != null &&
                    player.GetComponent<PlayerInteractionRouter>() != null &&
                    player.GetComponent<StudentLifeProgressComponent>() != null &&
                    outside != null && outside.GetComponent<OutsideSchoolActivityInteractor>() != null &&
                    journal != null && provider != null && hud != null && EventSystem.current != null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("Objective Journal Town E2E runtime did not install within timeout. Last state: " + lastState);
        }

        private static RuntimeState FindRuntime()
        {
            var player = GameObject.Find("Player");
            Assert.IsNotNull(player);
            var journal = Object.FindFirstObjectByType<ObjectiveJournalPanel>(FindObjectsInactive.Include);
            var hud = Object.FindFirstObjectByType<TrackedObjectiveHud>(FindObjectsInactive.Include);
            Assert.IsNotNull(journal);
            Assert.IsNotNull(hud);
            return new RuntimeState(player, player.GetComponent<PlayerInteractionRouter>(), player.GetComponent<StudentLifeProgressComponent>(), journal, hud);
        }

        private IEnumerator PressTab(Keyboard keyboard)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab));
            InputSystem.Update();
            yield return null;
            yield return new WaitForFixedUpdate();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            yield return null;
        }

        private IEnumerator WalkPlayerWithKeyboardTo(GameObject player, Keyboard keyboard, Vector3 targetPosition, float tolerance, string failure)
        {
            Key activeKey = Key.None;
            for (int i = 0; i < 480 && Vector3.Distance(player.transform.position, targetPosition) > tolerance; i++)
            {
                Vector3 delta = targetPosition - player.transform.position;
                Key key = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y) ? (delta.x >= 0f ? Key.D : Key.A) : (delta.y >= 0f ? Key.W : Key.S);
                if (activeKey != key)
                {
                    if (activeKey != Key.None) Release(ControlFor(keyboard, activeKey));
                    Press(ControlFor(keyboard, key));
                    activeKey = key;
                }
                InputSystem.Update();
                yield return null;
                yield return new WaitForFixedUpdate();
            }
            if (activeKey != Key.None) Release(ControlFor(keyboard, activeKey));
            InputSystem.Update();
            yield return new WaitForFixedUpdate();
            Assert.LessOrEqual(Vector3.Distance(player.transform.position, targetPosition), tolerance, failure);
        }

        private IEnumerator PressInteractKey(Keyboard keyboard)
        {
            Press(keyboard.eKey);
            InputSystem.Update();
            yield return null;
            yield return new WaitForFixedUpdate();
            Release(keyboard.eKey);
            InputSystem.Update();
            yield return null;
        }

        private static IEnumerator LoadScene(string sceneName)
        {
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            Assert.IsNotNull(op, sceneName + " should be present in BuildSettings.");
            while (!op.isDone) yield return null;
        }

        private static IEnumerator WaitForScene(string sceneName, float timeoutSeconds, string failure)
        {
            float elapsed = 0f;
            while (SceneManager.GetActiveScene().name != sceneName && elapsed < timeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.AreEqual(sceneName, SceneManager.GetActiveScene().name, failure);
        }

        private static IEnumerator ClickButton(Button button)
        {
            Assert.IsNotNull(button);
            Assert.IsTrue(button.gameObject.activeInHierarchy, button.name + " must be active for user click.");
            Assert.IsTrue(button.interactable, button.name + " must be interactable for user click.");
            ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
            yield return null;
        }

        private static Button FindButton(string cardName, string buttonName)
        {
            var card = GameObject.Find(cardName);
            Assert.IsNotNull(card, "UI object missing: " + cardName);
            return FindButton(card.transform, buttonName);
        }

        private static Button FindButton(Transform root, string name)
        {
            if (root.name == name && root.TryGetComponent<Button>(out var button)) return button;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindButton(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        private static void AssertNoEmptyCategory(ObjectiveJournalPanel journal, string categoryId)
        {
            Assert.IsFalse(journal.VisibleText.Contains("No objectives in this category"), "Objective Journal category should have live entries: " + categoryId);
        }

        private static ButtonControl ControlFor(Keyboard keyboard, Key key)
        {
            return (ButtonControl)keyboard[key];
        }

        private static void CaptureScreenshot()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScreenshotPath));
            if (File.Exists(ScreenshotPath)) File.Delete(ScreenshotPath);
            ScreenCapture.CaptureScreenshot(ScreenshotPath);
        }

        private static void EnsurePassiveEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            go.AddComponent<Rootborn.Game.Common.PassiveInputModule>();
        }

        private static void CleanupResidue()
        {
            DestroyAllNamed("SaveSlotSelectPanel");
            DestroyAllNamed("SaveSlotCanvas");
            DestroyAllNamed("SaveSlotSelectRoot");
            DestroyAllNamed("SpumCharacterCreatorPanel");
            DestroyAllNamed("EventSystem");
            DestroyAllNamed("Player");
        }

        private static void DestroyAllNamed(string objectName)
        {
            var transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = transforms.Length - 1; i >= 0; i--)
            {
                var transform = transforms[i];
                if (transform != null && transform.name == objectName) Object.DestroyImmediate(transform.gameObject);
            }
        }

        private readonly struct RuntimeState
        {
            public readonly GameObject Player;
            public readonly PlayerInteractionRouter Router;
            public readonly StudentLifeProgressComponent Student;
            public readonly ObjectiveJournalPanel Journal;
            public readonly TrackedObjectiveHud TrackedHud;

            public RuntimeState(GameObject player, PlayerInteractionRouter router, StudentLifeProgressComponent student, ObjectiveJournalPanel journal, TrackedObjectiveHud trackedHud)
            {
                Player = player;
                Router = router;
                Student = student;
                Journal = journal;
                TrackedHud = trackedHud;
            }
        }
    }
}
