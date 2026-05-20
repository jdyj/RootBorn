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
    public sealed class ObjectiveJournalUiScalingE2EScenarioTests : InputTestFixture
    {
        private const string TestSlotId = "slot-0";
        private const string ScreenshotPath = "Builds/Logs/objective-ui-scaling-e2e.png";
        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.timeScale = 1f;
            CleanupResidue();
            EnsurePassiveEventSystem();
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-objective-ui-scaling-e2e", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator OBJECTIVE_UI_STRICT_E2E_001_SelectionTrackRefreshReloadScaling()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            yield return LoadScene("MainMenu");
            EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "NewGameButton"));
            yield return ClickButton(FindButton("SpumCharacterCreatorPanel", "ConfirmButton"));
            yield return WaitForScene("Town", 15f, "OBJECTIVE-UI-STRICT-E2E-001 failed: SaveSlot New Game did not enter Town.");
            EnsureTownInstallers();
            yield return WaitForObjectiveRuntime(15f);

            var runtime = FindRuntime();
            yield return PressTab(keyboard);
            Assert.IsTrue(runtime.Journal.IsVisible, "OBJECTIVE-UI-STRICT-E2E-001 failed: Tab did not open Objective Journal.");
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<TrackedObjectiveHud>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, "OBJECTIVE-UI-STRICT-E2E-001 failed: scene must own exactly one TrackedObjectiveHud surface.");

            yield return AssertCategoryReadable(runtime.Journal, "Goals");
            yield return AssertCategoryReadable(runtime.Journal, "Quests");
            var secondQuestRow = FindButton(runtime.Journal.transform, "ObjectiveRow_1");
            Assert.IsNotNull(secondQuestRow, "OBJECTIVE-UI-STRICT-E2E-001 failed: Quests must expose multiple rows for scaling and non-first-row evidence.");
            yield return ClickButton(secondQuestRow);

            string selectedCategory = runtime.Journal.SelectedCategoryIdForTests;
            string selectedStableKey = runtime.Journal.SelectedStableKeyForTests;
            int selectedIndex = runtime.Journal.SelectedItemIndexForTests;
            int visibleRowCount = runtime.Journal.VisibleRowCountForTests;
            string visibleSnapshot = runtime.Journal.VisibleText;
            Assert.AreEqual("Quests", selectedCategory, "OBJECTIVE-UI-STRICT-E2E-001 failed: selected category id was not inspectable after user click.");
            Assert.AreEqual(1, selectedIndex, "OBJECTIVE-UI-STRICT-E2E-001 failed: user click did not select the second Quest row.");
            Assert.GreaterOrEqual(visibleRowCount, 2, "OBJECTIVE-UI-STRICT-E2E-001 failed: visible row count did not reflect the multi-row Quest category.");
            Assert.IsFalse(string.IsNullOrEmpty(selectedStableKey), "OBJECTIVE-UI-STRICT-E2E-001 failed: selected stable key was blank after row click.");
            StringAssert.Contains("Quest", visibleSnapshot, "OBJECTIVE-UI-STRICT-E2E-001 failed: selected Quest row text was not readable in the detail snapshot.");

            yield return ClickButton(FindButton(runtime.Journal.transform, "ObjectiveTrackButton"));
            Assert.IsTrue(runtime.TrackedHud.IsVisible, "OBJECTIVE-UI-STRICT-E2E-001 failed: Track button did not update TrackedObjectiveHud.");
            Assert.IsTrue(ObjectiveJournalTrackingStore.TryLoad(ActiveSaveContext.Metadata, out var trackedCategory, out var trackedStableKey), "OBJECTIVE-UI-STRICT-E2E-001 failed: tracked stable key was not saved.");
            Assert.AreEqual(selectedCategory, trackedCategory);
            Assert.AreEqual(selectedStableKey, trackedStableKey, "OBJECTIVE-UI-STRICT-E2E-001 failed: tracked stable key differs from the clicked row stable key.");
            string trackedTitle = FirstVisibleLine(runtime.TrackedHud.VisibleText);
            Assert.IsFalse(string.IsNullOrEmpty(trackedTitle), "OBJECTIVE-UI-STRICT-E2E-001 failed: tracked HUD title was blank.");

            yield return AssertCategoryReadable(runtime.Journal, "Campaign");
            yield return AssertCategoryReadable(runtime.Journal, "Hints");
            yield return ClickButton(FindButton(runtime.Journal.transform, "ObjectiveCategory_Quests"));
            Assert.AreEqual("Quests", runtime.Journal.SelectedCategoryIdForTests, "OBJECTIVE-UI-STRICT-E2E-001 failed: category id changed unexpectedly after returning to Quests.");
            Assert.GreaterOrEqual(runtime.Journal.VisibleRowCountForTests, 2, "OBJECTIVE-UI-STRICT-E2E-001 failed: returning to Quests left stale or missing rows.");

            yield return PressTab(keyboard);
            Assert.IsFalse(runtime.Journal.IsVisible, "OBJECTIVE-UI-STRICT-E2E-001 failed: Tab did not close Objective Journal before movement.");

            int energyBefore = runtime.Student.Progress.Energy;
            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.Outside.transform.position, 0.25f, "OBJECTIVE-UI-STRICT-E2E-001 failed: keyboard movement could not reach outside-school activity.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "OBJECTIVE-UI-STRICT-E2E-001 failed: outside-school prompt was not visible after movement.");
            yield return PressInteractKey(keyboard);
            yield return null;
            Assert.AreEqual(LifeActivityResultKind.Applied, runtime.Outside.LastResult.Kind, "OBJECTIVE-UI-STRICT-E2E-001 failed: outside-school action did not apply through actual input.");
            Assert.Less(runtime.Student.Progress.Energy, energyBefore, "OBJECTIVE-UI-STRICT-E2E-001 failed: activity did not mutate progress for refresh evidence.");

            yield return PressTab(keyboard);
            Assert.IsTrue(runtime.Journal.IsVisible, "OBJECTIVE-UI-STRICT-E2E-001 failed: Tab did not reopen Objective Journal after activity refresh.");
            Assert.AreEqual("Quests", runtime.Journal.SelectedCategoryIdForTests, "OBJECTIVE-UI-STRICT-E2E-001 failed: selected category did not survive provider refresh.");
            Assert.AreEqual(selectedStableKey, runtime.Journal.SelectedStableKeyForTests, "OBJECTIVE-UI-STRICT-E2E-001 failed: selected stable key did not survive provider refresh.");
            Assert.GreaterOrEqual(runtime.Journal.VisibleRowCountForTests, 2, "OBJECTIVE-UI-STRICT-E2E-001 failed: refreshed Quest category lost rows.");
            Assert.IsTrue(ObjectiveJournalTrackingStore.TryLoad(ActiveSaveContext.Metadata, out var refreshedCategory, out var refreshedStableKey));
            Assert.AreEqual(trackedCategory, refreshedCategory);
            Assert.AreEqual(trackedStableKey, refreshedStableKey, "OBJECTIVE-UI-STRICT-E2E-001 failed: provider refresh changed tracked stable key.");
            StringAssert.Contains(trackedTitle, runtime.TrackedHud.VisibleText, "OBJECTIVE-UI-STRICT-E2E-001 failed: tracked HUD changed after provider refresh.");
            yield return CaptureScreenshot(ScreenshotPath);

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return LoadScene("MainMenu");
            EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "LoadButton"));
            yield return WaitForScene("Town", 15f, "OBJECTIVE-UI-STRICT-E2E-001 failed: SaveSlot Load did not re-enter Town.");
            EnsureTownInstallers();
            yield return WaitForObjectiveRuntime(15f);

            var reloaded = FindRuntime();
            yield return PressTab(keyboard);
            Assert.IsTrue(reloaded.Journal.IsVisible, "OBJECTIVE-UI-STRICT-E2E-001 failed: Tab did not open Objective Journal after reload.");
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<TrackedObjectiveHud>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, "OBJECTIVE-UI-STRICT-E2E-001 failed: reload created duplicate tracked HUDs.");
            Assert.IsTrue(reloaded.TrackedHud.IsVisible, "OBJECTIVE-UI-STRICT-E2E-001 failed: tracked HUD did not restore after reload.");
            StringAssert.Contains(trackedTitle, reloaded.TrackedHud.VisibleText, "OBJECTIVE-UI-STRICT-E2E-001 failed: reload restored a different tracked objective title.");
            Assert.IsTrue(ObjectiveJournalTrackingStore.TryLoad(ActiveSaveContext.Metadata, out var reloadedCategory, out var reloadedStableKey));
            Assert.AreEqual(trackedCategory, reloadedCategory);
            Assert.AreEqual(trackedStableKey, reloadedStableKey, "OBJECTIVE-UI-STRICT-E2E-001 failed: reload changed tracked stable key.");
            yield return AssertCategoryReadable(reloaded.Journal, "Quests");
        }

        private static void EnsureTownInstallers()
        {
            StudentDayRuntimeInstaller.EnsureForActiveScene();
            CampaignRuntimeInstaller.EnsureForActiveScene();
            DailyPlanRuntimeInstaller.EnsureForActiveScene();
        }

        private IEnumerator AssertCategoryReadable(ObjectiveJournalPanel journal, string categoryId)
        {
            yield return ClickButton(FindButton(journal.transform, "ObjectiveCategory_" + categoryId));
            Assert.AreEqual(categoryId, journal.SelectedCategoryIdForTests, "OBJECTIVE-UI-STRICT-E2E-001 failed: selected category id mismatch for " + categoryId + ".");
            Assert.IsFalse(string.IsNullOrEmpty(journal.VisibleText), "OBJECTIVE-UI-STRICT-E2E-001 failed: visible text snapshot was blank for " + categoryId + ".");
            if (journal.VisibleRowCountForTests == 0)
            {
                StringAssert.Contains("No objectives in this category", journal.VisibleText, "OBJECTIVE-UI-STRICT-E2E-001 failed: empty category lacked explicit empty text for " + categoryId + ".");
            }
            else
            {
                var row = FindButton(journal.transform, "ObjectiveRow_0");
                Assert.IsNotNull(row, "OBJECTIVE-UI-STRICT-E2E-001 failed: row count reported rows but ObjectiveRow_0 was missing for " + categoryId + ".");
                yield return ClickButton(row);
                Assert.IsFalse(string.IsNullOrEmpty(journal.SelectedStableKeyForTests), "OBJECTIVE-UI-STRICT-E2E-001 failed: row click produced a blank stable key for " + categoryId + ".");
            }
        }

        private static IEnumerator WaitForObjectiveRuntime(float timeoutSeconds)
        {
            float elapsed = 0f;
            string lastState = string.Empty;
            while (elapsed < timeoutSeconds)
            {
                var player = GameObject.Find("Player");
                var outside = GameObject.Find("OutsideSchoolActivityPoint");
                var board = GameObject.Find("DailyPlanBoard");
                var journal = UnityEngine.Object.FindFirstObjectByType<ObjectiveJournalPanel>(FindObjectsInactive.Include);
                var provider = UnityEngine.Object.FindFirstObjectByType<ObjectiveJournalRuntimeProvider>(FindObjectsInactive.Include);
                var hud = UnityEngine.Object.FindFirstObjectByType<TrackedObjectiveHud>(FindObjectsInactive.Include);
                lastState = "scene=" + SceneManager.GetActiveScene().name
                    + " player=" + (player != null)
                    + " controller=" + (player != null && player.GetComponent<PlayerController>() != null)
                    + " router=" + (player != null && player.GetComponent<PlayerInteractionRouter>() != null)
                    + " student=" + (player != null && player.GetComponent<StudentLifeProgressComponent>() != null)
                    + " outside=" + (outside != null)
                    + " dailyBoard=" + (board != null)
                    + " journal=" + (journal != null)
                    + " provider=" + (provider != null)
                    + " hud=" + (hud != null)
                    + " eventSystem=" + (EventSystem.current != null);

                if (player != null && player.GetComponent<PlayerController>() != null && player.GetComponent<PlayerInteractionRouter>() != null && player.GetComponent<StudentLifeProgressComponent>() != null &&
                    outside != null && outside.GetComponent<OutsideSchoolActivityInteractor>() != null && board != null && board.GetComponent<DailyPlanBoardInteractor>() != null &&
                    journal != null && provider != null && hud != null && EventSystem.current != null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("Objective Journal UI scaling E2E runtime did not install within timeout. Last state: " + lastState);
        }

        private static RuntimeState FindRuntime()
        {
            var player = GameObject.Find("Player");
            var outside = GameObject.Find("OutsideSchoolActivityPoint");
            Assert.IsNotNull(player);
            Assert.IsNotNull(outside);
            var journal = UnityEngine.Object.FindFirstObjectByType<ObjectiveJournalPanel>(FindObjectsInactive.Include);
            var hud = UnityEngine.Object.FindFirstObjectByType<TrackedObjectiveHud>(FindObjectsInactive.Include);
            Assert.IsNotNull(journal);
            Assert.IsNotNull(hud);
            return new RuntimeState(player, player.GetComponent<PlayerInteractionRouter>(), player.GetComponent<StudentLifeProgressComponent>(), outside.GetComponent<OutsideSchoolActivityInteractor>(), journal, hud);
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

        private static Button FindButton(string objectName, string buttonName)
        {
            var root = GameObject.Find(objectName);
            Assert.IsNotNull(root, "UI object missing: " + objectName);
            return FindButton(root.transform, buttonName);
        }

        private static Button FindButton(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name && root.TryGetComponent<Button>(out var button)) return button;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindButton(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        private static string FirstVisibleLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (!string.IsNullOrEmpty(line)) return line;
            }
            return string.Empty;
        }

        private static ButtonControl ControlFor(Keyboard keyboard, Key key)
        {
            return (ButtonControl)keyboard[key];
        }

        private static IEnumerator CaptureScreenshot(string relativePath)
        {
            var directory = Path.GetDirectoryName(relativePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            if (File.Exists(relativePath)) File.Delete(relativePath);
            ScreenCapture.CaptureScreenshot(relativePath);
            for (int i = 0; i < 5; i++) yield return null;
            Assert.IsTrue(File.Exists(relativePath), "Objective Journal UI scaling screenshot evidence was not written: " + relativePath);
            Assert.Greater(new FileInfo(relativePath).Length, 0L, "Objective Journal UI scaling screenshot evidence was empty: " + relativePath);
        }

        private static void DeleteScreenshot()
        {
            if (File.Exists(ScreenshotPath)) File.Delete(ScreenshotPath);
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
            var transforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = transforms.Length - 1; i >= 0; i--)
            {
                var transform = transforms[i];
                if (transform != null && transform.name == objectName) UnityEngine.Object.DestroyImmediate(transform.gameObject);
            }
        }

        private readonly struct RuntimeState
        {
            public readonly GameObject Player;
            public readonly PlayerInteractionRouter Router;
            public readonly StudentLifeProgressComponent Student;
            public readonly OutsideSchoolActivityInteractor Outside;
            public readonly ObjectiveJournalPanel Journal;
            public readonly TrackedObjectiveHud TrackedHud;

            public RuntimeState(GameObject player, PlayerInteractionRouter router, StudentLifeProgressComponent student, OutsideSchoolActivityInteractor outside, ObjectiveJournalPanel journal, TrackedObjectiveHud trackedHud)
            {
                Player = player;
                Router = router;
                Student = student;
                Outside = outside;
                Journal = journal;
                TrackedHud = trackedHud;
            }
        }
    }
}
