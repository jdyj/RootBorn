using System.Collections;
using System.Collections.Generic;
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
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    public sealed class DailyEventLoopE2EScenarioTests : InputTestFixture
    {
        private const string TestSlotId = "slot-0";
        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.timeScale = 1f;
            CleanupResidue();
            EnsurePassiveEventSystem();
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-daily-event-e2e", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator DAILYEVENT_E2E_001_010_SaveSlotTownLocationChoiceDeferResultReloadAndDedupe()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "NewGameButton"));
            yield return WaitForScene("Town", 10f, "DAILYEVENT-E2E-001 failed: SaveSlot New Game did not enter Town.");
            StudentDayRuntimeInstaller.EnsureForActiveScene();
            LocationNpcRuntimeInstaller.EnsureForActiveScene();
            DailyEventRuntimeInstaller.EnsureForActiveScene();
            yield return WaitForDailyEventRuntime(10f);

            var runtime = FindRuntime();
            var library = RequireObject(LocationNpcRuntimeInstaller.LocationObjectName("location.library"), "DAILYEVENT-E2E-002 failed: library location object missing.");
            var square = RequireObject(LocationNpcRuntimeInstaller.LocationObjectName("location.town-square"), "DAILYEVENT-E2E-006 failed: square location object missing.");
            var eventPanel = Object.FindFirstObjectByType<DailyEventPanel>(FindObjectsInactive.Include);
            Assert.IsNotNull(eventPanel, "DAILYEVENT-E2E-003 failed: daily event panel missing.");

            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, library.transform.position, 0.25f, "DAILYEVENT-E2E-002 failed: actual keyboard movement could not reach library.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "DAILYEVENT-E2E-003 failed: location prompt was not visible after movement.");
            yield return PressInteractKey(keyboard);
            yield return WaitForDailyEventPanel(2f, "DAILYEVENT-E2E-003 failed: daily event UI did not open from actual location input.");
            AssertPanelContains(eventPanel.transform, "Library", "DAILYEVENT-E2E-003 failed: event UI did not show library event title.");

            int traitBefore = FirstTraitValue(runtime.Student.Progress);
            yield return ClickButton(FindButton(eventPanel.transform, "DailyEventChoiceButton_0"));
            yield return null;
            Assert.AreEqual(LifeActivityResultKind.Applied, eventPanel.LastResult.Kind, "DAILYEVENT-E2E-004 failed: choice button did not apply event result.");
            Assert.Greater(FirstTraitValue(runtime.Student.Progress), traitBefore, "DAILYEVENT-E2E-005 failed: daily event choice did not change student state.");
            AssertPanelContains(eventPanel.transform, "daily-event:", "DAILYEVENT-E2E-005 failed: immediate feedback UI omitted event result log.");
            string completedEventId = eventPanel.LastResult.ActivityId;

            yield return ClickButton(FindButton(eventPanel.transform, "DailyEventChoiceButton_0"));
            yield return null;
            Assert.AreEqual(LifeActivityResultKind.DuplicateRequest, eventPanel.LastResult.Kind, "DAILYEVENT-E2E-010 failed: repeated event choice did not report duplicate.");

            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, square.transform.position, 0.25f, "DAILYEVENT-E2E-006 failed: actual keyboard movement could not reach square.");
            runtime.Router.RefreshPromptNow();
            yield return PressInteractKey(keyboard);
            yield return WaitForDailyEventPanel(2f, "DAILYEVENT-E2E-006 failed: second daily event UI did not open.");
            yield return ClickButton(FindButton(eventPanel.transform, "DailyEventDeferButton"));
            yield return null;
            Assert.AreEqual(DailyEventStates.Deferred, eventPanel.LastRecord.State, "DAILYEVENT-E2E-006 failed: defer button did not store deferred state.");

            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.DayEnd.transform.position, 0.25f, "DAILYEVENT-E2E-008 failed: actual keyboard movement could not reach day-end point.");
            runtime.Router.RefreshPromptNow();
            yield return PressInteractKey(keyboard);
            yield return WaitForDayResultPanel(2f, "DAILYEVENT-E2E-008 failed: result UI did not open from actual day-end interaction.");
            var resultPanel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
            AssertPanelContains(resultPanel.transform, completedEventId, "DAILYEVENT-E2E-008 failed: day result UI omitted completed daily event.");
            AssertPanelContains(resultPanel.transform, "daily-event:", "DAILYEVENT-E2E-008 failed: day result UI omitted daily event result details.");

            string eventFile = Path.Combine(_saveRoot, TestSlotId, "daily-event-progress.json");
            Assert.IsTrue(File.Exists(eventFile), "DAILYEVENT-E2E-009 failed: daily event progress save file missing.");
            string savedJson = File.ReadAllText(eventFile);
            StringAssert.Contains(DailyEventStates.Completed, savedJson, "DAILYEVENT-E2E-009 failed: save file does not record completed event state.");
            StringAssert.Contains(DailyEventStates.Deferred, savedJson, "DAILYEVENT-E2E-009 failed: save file does not record deferred event state.");

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return LoadScene("MainMenu");
            EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "LoadButton"));
            yield return WaitForScene("Town", 10f, "DAILYEVENT-E2E-009 failed: SaveSlot Load did not re-enter Town.");
            StudentDayRuntimeInstaller.EnsureForActiveScene();
            LocationNpcRuntimeInstaller.EnsureForActiveScene();
            DailyEventRuntimeInstaller.EnsureForActiveScene();
            yield return WaitForDailyEventRuntime(10f);

            var reloaded = FindRuntime();
            var progress = reloaded.Player.GetComponent<DailyEventProgressComponent>();
            Assert.AreEqual(DailyEventStates.Completed, progress.Progress.GetRecord(completedEventId).State, "DAILYEVENT-E2E-009 failed: completed event state did not survive reload.");
        }

        private static IEnumerator WaitForDailyEventRuntime(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var player = GameObject.Find("Player");
                var library = GameObject.Find(LocationNpcRuntimeInstaller.LocationObjectName("location.library"));
                var square = GameObject.Find(LocationNpcRuntimeInstaller.LocationObjectName("location.town-square"));
                var dayEnd = GameObject.Find("StudentDayEndBoard");
                var panel = Object.FindFirstObjectByType<DailyEventPanel>(FindObjectsInactive.Include);
                if (player != null && player.GetComponent<PlayerController>() != null && player.GetComponent<PlayerInteractionRouter>() != null && player.GetComponent<StudentLifeProgressComponent>() != null && player.GetComponent<DailyEventProgressComponent>() != null &&
                    library != null && library.GetComponent<LocationActivityInteractor>() != null &&
                    square != null && square.GetComponent<LocationActivityInteractor>() != null &&
                    dayEnd != null && dayEnd.GetComponent<StudentDayEndInteractor>() != null &&
                    panel != null && EventSystem.current != null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("DAILYEVENT-E2E runtime did not expose player, locations, day-end point, daily event progress, and UI within timeout.");
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

        private static int FirstTraitValue(StudentLifeProgress progress)
        {
            string[] ids = progress.GetTraitIds();
            return ids.Length == 0 ? 0 : progress.GetTraitValueById(ids[0]);
        }

        private static IEnumerator WaitForDailyEventPanel(float timeoutSeconds, string failure)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var panel = Object.FindFirstObjectByType<DailyEventPanel>(FindObjectsInactive.Include);
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

        private static void AssertPanelContains(Transform panel, string expected, string message)
        {
            var texts = panel.GetComponentsInChildren<Text>(true);
            var all = new List<string>();
            for (int i = 0; i < texts.Length; i++)
            {
                all.Add(texts[i].text);
                if (!string.IsNullOrEmpty(texts[i].text) && texts[i].text.Contains(expected)) return;
            }
            Assert.Fail(message + " Expected '" + expected + "' in text: " + string.Join(" | ", all));
        }

        private static KeyControl ControlFor(Keyboard keyboard, Key key)
        {
            if (key == Key.A) return keyboard.aKey;
            if (key == Key.D) return keyboard.dKey;
            if (key == Key.W) return keyboard.wKey;
            if (key == Key.S) return keyboard.sKey;
            Assert.Fail("Unsupported key: " + key);
            return keyboard.eKey;
        }

        private static void EnsurePassiveEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            go.AddComponent<PassiveInputModule>();
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
