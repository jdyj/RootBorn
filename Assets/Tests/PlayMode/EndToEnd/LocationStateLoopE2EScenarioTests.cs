using System.Collections;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.UI.MainMenu;
using Rootborn.UI.StudentLife;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    public sealed class LocationStateLoopE2EScenarioTests : InputTestFixture
    {
        private const string TestSlotId = "slot-0";
        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.timeScale = 1f;
            CleanupResidue();
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-location-state-e2e", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator LOCATION_STATE_E2E_001_011_SaveSlotMovementTimeShiftStateUiEffectAndReloadPersistence()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "NewGameButton"));
            yield return WaitForScene("Town", 10f, "LOCATION-STATE-E2E-001 failed: SaveSlot New Game did not enter Town.");
            StudentDayRuntimeInstaller.EnsureForActiveScene();
            LocationNpcRuntimeInstaller.EnsureForActiveScene();
            LocationStateRuntimeInstaller.EnsureForActiveScene();
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return null;
            yield return WaitForRuntime(10f);

            var runtime = FindRuntime();
            var library = RequireObject(LocationNpcRuntimeInstaller.LocationObjectName("location.library"), "LOCATION-STATE-E2E-002 failed: library object missing.");
            var identityPanel = Object.FindFirstObjectByType<LocationIdentityPanel>(FindObjectsInactive.Include);
            Assert.IsNotNull(identityPanel, "LOCATION-STATE-E2E-003 failed: location identity UI missing.");

            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, library.transform.position, 0.25f, "LOCATION-STATE-E2E-002 failed: keyboard movement could not reach library.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "LOCATION-STATE-E2E-003 failed: library prompt was not visible after movement.");
            yield return PressInteractKey(keyboard);
            yield return WaitForLocationStatePanel(2f, "LOCATION-STATE-E2E-003 failed: location state UI did not open from actual input.");
            var statePanel = Object.FindFirstObjectByType<LocationStatePanel>(FindObjectsInactive.Include);
            Assert.IsNotNull(statePanel, "LOCATION-STATE-E2E-003 failed: location state UI missing after interaction.");
            StringAssert.Contains("Library Morning", statePanel.GetTextForTests(), "LOCATION-STATE-E2E-003 failed: morning location state was not shown.");
            StringAssert.Contains("object.library-shelf", statePanel.GetTextForTests(), "LOCATION-STATE-E2E-006 failed: morning state object effect was not shown.");

            int beforeTime = runtime.Student.Progress.TimeMinutes;
            yield return ClickButton(FindButton(identityPanel.transform, "LocationActivityButton_0"));
            yield return null;
            Assert.Greater(runtime.Student.Progress.TimeMinutes, beforeTime, "LOCATION-STATE-E2E-005 failed: actual activity button did not advance time.");

            runtime.Router.RefreshPromptNow();
            yield return PressInteractKey(keyboard);
            yield return WaitForLocationStateText("Library Afternoon", 2f, "LOCATION-STATE-E2E-006 failed: revisiting same location after time shift did not show afternoon state.");
            StringAssert.Contains("object.reading-table", statePanel.GetTextForTests(), "LOCATION-STATE-E2E-006 failed: afternoon state object effect was not shown.");
            StringAssert.Contains("npc.librarian", statePanel.GetTextForTests(), "LOCATION-STATE-E2E-006 failed: state NPC effect was not shown.");
            StringAssert.Contains("clue.library-archive-rumor", statePanel.GetTextForTests(), "LOCATION-STATE-E2E-006 failed: state clue effect was not shown.");

            string locationStateFile = Path.Combine(_saveRoot, TestSlotId, "location-state-progress-local-player.json");
            if (!File.Exists(locationStateFile)) locationStateFile = Path.Combine(_saveRoot, TestSlotId, "location-state-progress.json");
            Assert.IsTrue(File.Exists(locationStateFile), "LOCATION-STATE-E2E-010 failed: location state progress save file missing.");
            string savedJson = File.ReadAllText(locationStateFile);
            StringAssert.Contains("location.state.library-morning", savedJson, "LOCATION-STATE-E2E-010 failed: morning discovery was not saved.");
            StringAssert.Contains("location.state.library-afternoon", savedJson, "LOCATION-STATE-E2E-010 failed: afternoon discovery was not saved.");

            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.DayEnd.transform.position, 0.25f, "LOCATION-STATE-E2E-009 failed: keyboard movement could not reach day-end board.");
            runtime.Router.RefreshPromptNow();
            yield return PressInteractKey(keyboard);
            yield return WaitForDayResultPanel(3f, "LOCATION-STATE-E2E-009 failed: day result UI did not open from actual day-end input.");
            var resultPanel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
            Assert.IsNotNull(resultPanel, "LOCATION-STATE-E2E-009 failed: day result panel missing.");
            Assert.GreaterOrEqual(resultPanel.GetLocationStateCardCountForTests(), 2, "LOCATION-STATE-E2E-009 failed: day result did not render visited location states.");
            StringAssert.Contains("Library Morning", resultPanel.GetLocationStateTextForTests(), "LOCATION-STATE-E2E-009 failed: day result missing morning state summary.");
            StringAssert.Contains("Library Afternoon", resultPanel.GetLocationStateTextForTests(), "LOCATION-STATE-E2E-009 failed: day result missing afternoon state summary.");

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return LoadScene("MainMenu");
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "LoadButton"));
            yield return WaitForScene("Town", 10f, "LOCATION-STATE-E2E-010 failed: SaveSlot Load did not re-enter Town.");
            StudentDayRuntimeInstaller.EnsureForActiveScene();
            LocationNpcRuntimeInstaller.EnsureForActiveScene();
            LocationStateRuntimeInstaller.EnsureForActiveScene();
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return null;
            yield return WaitForRuntime(10f);

            var reloaded = FindRuntime();
            var reloadedLibrary = RequireObject(LocationNpcRuntimeInstaller.LocationObjectName("location.library"), "LOCATION-STATE-E2E-010 failed: reloaded library object missing.");
            yield return WalkPlayerWithKeyboardTo(reloaded.Player, keyboard, reloadedLibrary.transform.position, 0.25f, "LOCATION-STATE-E2E-010 failed: keyboard movement could not reach library after reload.");
            reloaded.Router.RefreshPromptNow();
            yield return PressInteractKey(keyboard);
            yield return WaitForLocationStatePanel(2f, "LOCATION-STATE-E2E-010 failed: location state UI did not reopen after reload.");
            savedJson = File.ReadAllText(locationStateFile);
            StringAssert.Contains("location.state.library-morning", savedJson, "LOCATION-STATE-E2E-010 failed: saved discovery state was not stable after reload.");

            yield return WalkPlayerWithKeyboardTo(reloaded.Player, keyboard, reloaded.DayEnd.transform.position, 0.25f, "LOCATION-STATE-E2E-011 failed: keyboard movement could not reach day-end board after reload.");
            reloaded.Router.RefreshPromptNow();
            yield return PressInteractKey(keyboard);
            yield return WaitForDayResultPanel(3f, "LOCATION-STATE-E2E-011 failed: reloaded day result UI did not open from actual input.");
            resultPanel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
            StringAssert.Contains("Library Morning", resultPanel.GetLocationStateTextForTests(), "LOCATION-STATE-E2E-011 failed: reloaded day result missing persisted morning state summary.");
        }

        private static IEnumerator WaitForRuntime(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var player = GameObject.Find("Player");
                var library = GameObject.Find(LocationNpcRuntimeInstaller.LocationObjectName("location.library"));
                var dayEnd = GameObject.Find("StudentDayEndBoard");
                var identityPanel = Object.FindFirstObjectByType<LocationIdentityPanel>(FindObjectsInactive.Include);
                if (player != null && player.GetComponent<PlayerController>() != null && player.GetComponent<PlayerInteractionRouter>() != null && player.GetComponent<StudentLifeProgressComponent>() != null && library != null && library.GetComponent<LocationActivityInteractor>() != null && dayEnd != null && dayEnd.GetComponent<StudentDayEndInteractor>() != null && identityPanel != null && EventSystem.current != null) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.Fail("LOCATION-STATE-E2E runtime did not expose player, library, day-end, and identity UI within timeout.");
        }

        private static RuntimeState FindRuntime()
        {
            var player = GameObject.Find("Player");
            var dayEnd = GameObject.Find("StudentDayEndBoard");
            Assert.IsNotNull(player);
            Assert.IsNotNull(dayEnd);
            return new RuntimeState(player, player.GetComponent<PlayerInteractionRouter>(), player.GetComponent<StudentLifeProgressComponent>(), dayEnd.GetComponent<StudentDayEndInteractor>());
        }

        private static GameObject RequireObject(string name, string failure)
        {
            var go = GameObject.Find(name);
            Assert.IsNotNull(go, failure);
            return go;
        }

        private static IEnumerator WaitForLocationStatePanel(float timeoutSeconds, string failure)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var panel = Object.FindFirstObjectByType<LocationStatePanel>(FindObjectsInactive.Include);
                if (panel != null && panel.IsOpen) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.Fail(failure);
        }

        private static IEnumerator WaitForLocationStateText(string expected, float timeoutSeconds, string failure)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var panel = Object.FindFirstObjectByType<LocationStatePanel>(FindObjectsInactive.Include);
                if (panel != null && panel.IsOpen && panel.GetTextForTests().Contains(expected)) yield break;
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

        private IEnumerator WalkPlayerWithKeyboardTo(GameObject player, Keyboard keyboard, Vector3 targetPosition, float tolerance, string failure)
        {
            keyboard = EnsureKeyboard(keyboard);
            Key activeKey = Key.None;
            for (int i = 0; i < 480 && Vector3.Distance(player.transform.position, targetPosition) > tolerance; i++)
            {
                Vector3 delta = targetPosition - player.transform.position;
                Key key = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y) ? (delta.x >= 0f ? Key.D : Key.A) : (delta.y >= 0f ? Key.W : Key.S);
                if (activeKey != key)
                {
                    QueueKeyboardState(keyboard, key);
                    activeKey = key;
                }
                InputSystem.Update();
                yield return null;
                yield return new WaitForFixedUpdate();
            }
            QueueKeyboardState(keyboard, Key.None);
            InputSystem.Update();
            yield return new WaitForFixedUpdate();
            Assert.LessOrEqual(Vector3.Distance(player.transform.position, targetPosition), tolerance, failure);
        }

        private IEnumerator PressInteractKey(Keyboard keyboard)
        {
            keyboard = EnsureKeyboard(keyboard);
            QueueKeyboardState(keyboard, Key.E);
            InputSystem.Update();
            yield return null;
            yield return new WaitForFixedUpdate();
            QueueKeyboardState(keyboard, Key.None);
            InputSystem.Update();
            yield return null;
        }

        private static Keyboard EnsureKeyboard(Keyboard keyboard)
        {
            if (keyboard != null && keyboard.added) return keyboard;
            return Keyboard.current != null && Keyboard.current.added ? Keyboard.current : InputSystem.AddDevice<Keyboard>();
        }

        private static void QueueKeyboardState(Keyboard keyboard, Key key)
        {
            if (key == Key.None)
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                return;
            }

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
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
            Assert.IsNotNull(card, "Save slot UI card missing: " + cardName);
            return FindButton(card.transform, buttonName);
        }

        private static Button FindButton(Transform root, string name)
        {
            var child = FindChild(root, name);
            Assert.IsNotNull(child, "UI button missing: " + name);
            var button = child.GetComponent<Button>();
            Assert.IsNotNull(button, name + " does not have Button component.");
            return button;
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var match = FindChild(root.GetChild(i), name);
                if (match != null) return match;
            }
            return null;
        }

        private static void CleanupResidue()
        {
            DestroyAllNamed("SaveSlotSelectPanel");
            DestroyAllNamed("SaveSlotCanvas");
            DestroyAllNamed("SaveSlotSelectRoot");
            DestroyAllNamed("EventSystem");
            DestroyAllNamed("Player");
            DestroyAllNamed("LocationStatePanel");
            DestroyAllNamed("LocationStateCanvas");
            DestroyAllNamed("StudentDayResultPanel");
            DestroyAllNamed("StudentDayResultRoot");
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
            public readonly StudentDayEndInteractor DayEnd;

            public RuntimeState(GameObject player, PlayerInteractionRouter router, StudentLifeProgressComponent student, StudentDayEndInteractor dayEnd)
            {
                Player = player;
                Router = router;
                Student = student;
                DayEnd = dayEnd;
            }
        }
    }
}