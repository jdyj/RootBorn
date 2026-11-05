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
    public sealed class ObjectiveJournalUiQualityE2EScenarioTests : Rootborn.Tests.PlayMode.PlayModeInputFixture
    {
        private const string TestSlotId = "slot-0";
        private const string ScreenshotPath = "Builds/Logs/objective-journal-ui-quality-e2e.png";
        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.timeScale = 1f;
            CleanupResidue();
            EnsurePassiveEventSystem();
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-objective-journal-ui-quality-e2e", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator OBJECTIVE_JOURNAL_E2E_002_NonFirstQuestSelectionSurvivesRefreshAndReload()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            yield return LoadScene("MainMenu");
            EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "NewGameButton"));
            yield return null;
            yield return Rootborn.Tests.PlayMode.PlayModeScenarioStart.WaitForGameData();
            yield return ClickButton(FindButton("SpumCharacterCreatorPanel", "ConfirmButton"));
            yield return WaitForScene("Town", 20f, "OBJECTIVE-JOURNAL-E2E-002 failed: SaveSlot New Game did not enter Town.");
            StudentDayRuntimeInstaller.EnsureForActiveScene();
            CampaignRuntimeInstaller.EnsureForActiveScene();
            yield return WaitForObjectiveRuntime(15f);

            var runtime = FindRuntime();
            int energyBefore = runtime.Student.Progress.Energy;

            yield return PressTab(keyboard);
            Assert.IsTrue(runtime.Journal.IsVisible, "OBJECTIVE-JOURNAL-E2E-002 failed: actual Tab input did not open Objective Journal.");
            yield return ClickButton(FindButton(runtime.Journal.transform, "ObjectiveCategory_Quests"));
            var secondQuestRow = FindButton(runtime.Journal.transform, "ObjectiveRow_1");
            Assert.IsNotNull(secondQuestRow, "OBJECTIVE-JOURNAL-E2E-002 failed: Quests category must expose multiple rows for non-first row selection coverage.");
            yield return ClickButton(secondQuestRow);
            StringAssert.Contains("Quest", runtime.Journal.VisibleText, "OBJECTIVE-JOURNAL-E2E-002 failed: clicking a non-first Quest row did not show quest detail text.");
            yield return ClickButton(FindButton(runtime.Journal.transform, "ObjectiveTrackButton"));
            Assert.IsTrue(runtime.TrackedHud.IsVisible, "OBJECTIVE-JOURNAL-E2E-002 failed: Track click did not update TrackedObjectiveHud.");
            Assert.IsTrue(ObjectiveJournalTrackingStore.TryLoad(ActiveSaveContext.Metadata, out var trackedCategory, out var trackedStableKey), "OBJECTIVE-JOURNAL-E2E-002 failed: selected non-first row stable key was not saved.");
            Assert.AreEqual("Quests", trackedCategory);
            Assert.IsFalse(string.IsNullOrEmpty(trackedStableKey));
            string trackedTitle = FirstVisibleLine(runtime.TrackedHud.VisibleText);
            Assert.IsFalse(string.IsNullOrEmpty(trackedTitle), "OBJECTIVE-JOURNAL-E2E-002 failed: tracked HUD did not expose visible text for the selected row.");

            yield return PressTab(keyboard);
            Assert.IsFalse(runtime.Journal.IsVisible, "OBJECTIVE-JOURNAL-E2E-002 failed: second Tab should close Objective Journal before player movement.");

            var outside = GameObject.Find("OutsideSchoolActivityPoint").GetComponent<OutsideSchoolActivityInteractor>();
            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, outside.transform.position, 0.25f, "OBJECTIVE-JOURNAL-E2E-002 failed: actual keyboard movement could not reach outside-school activity.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "OBJECTIVE-JOURNAL-E2E-002 failed: outside-school prompt was not visible after movement.");
            yield return PressInteractKey(keyboard);
            yield return null;
            Assert.AreEqual(LifeActivityResultKind.Applied, outside.LastResult.Kind, "OBJECTIVE-JOURNAL-E2E-002 failed: outside-school action did not apply through actual input.");
            Assert.Less(runtime.Student.Progress.Energy, energyBefore, "OBJECTIVE-JOURNAL-E2E-002 failed: activity did not change student progress for Journal refresh.");

            yield return PressTab(keyboard);
            Assert.IsTrue(runtime.Journal.IsVisible, "OBJECTIVE-JOURNAL-E2E-002 failed: Tab did not reopen Objective Journal after activity refresh.");
            StringAssert.Contains(trackedTitle, runtime.TrackedHud.VisibleText, "OBJECTIVE-JOURNAL-E2E-002 failed: tracked HUD jumped away from the selected non-first row after refresh.");
            Assert.IsTrue(ObjectiveJournalTrackingStore.TryLoad(ActiveSaveContext.Metadata, out var refreshedCategory, out var refreshedStableKey));
            Assert.AreEqual("Quests", refreshedCategory);
            Assert.AreEqual(trackedStableKey, refreshedStableKey, "OBJECTIVE-JOURNAL-E2E-002 failed: refresh changed the tracked stable key.");
            StringAssert.Contains(trackedTitle, runtime.Journal.VisibleText, "OBJECTIVE-JOURNAL-E2E-002 failed: Objective Journal detail did not keep the selected non-first row visible after refresh.");
            yield return CaptureScreenshot(ScreenshotPath);

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return LoadScene("MainMenu");
            EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "LoadButton"));
            yield return WaitForScene("Town", 20f, "OBJECTIVE-JOURNAL-E2E-002 failed: SaveSlot Load did not re-enter Town.");
            StudentDayRuntimeInstaller.EnsureForActiveScene();
            CampaignRuntimeInstaller.EnsureForActiveScene();
            yield return WaitForObjectiveRuntime(15f);

            var reloaded = FindRuntime();
            yield return PressTab(keyboard);
            Assert.IsTrue(reloaded.TrackedHud.IsVisible, "OBJECTIVE-JOURNAL-E2E-002 failed: tracked objective was not restored after reload and Journal refresh.");
            StringAssert.Contains(trackedTitle, reloaded.TrackedHud.VisibleText, "OBJECTIVE-JOURNAL-E2E-002 failed: reload restored a different tracked objective than the selected non-first row.");
            Assert.IsTrue(ObjectiveJournalTrackingStore.TryLoad(ActiveSaveContext.Metadata, out var reloadedCategory, out var reloadedStableKey));
            Assert.AreEqual("Quests", reloadedCategory);
            Assert.AreEqual(trackedStableKey, reloadedStableKey, "OBJECTIVE-JOURNAL-E2E-002 failed: reload changed the tracked stable key.");
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

            Assert.Fail("Objective Journal UI quality E2E runtime did not install within timeout. Last state: " + lastState);
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
            Directory.CreateDirectory(Path.GetDirectoryName(relativePath));
            if (File.Exists(relativePath)) File.Delete(relativePath);
            ScreenCapture.CaptureScreenshot(relativePath);
            for (int i = 0; i < 5; i++) yield return null;
            Assert.IsTrue(File.Exists(relativePath), "Objective Journal UI quality screenshot evidence was not written: " + relativePath);
            Assert.Greater(new FileInfo(relativePath).Length, 0L, "Objective Journal UI quality screenshot evidence was empty: " + relativePath);
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
