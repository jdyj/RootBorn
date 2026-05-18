using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
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
    public sealed class TownHelpActionGrowthE2EScenarioTests : InputTestFixture
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
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-town-help-e2e", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator HELP_E2E_001_006_PlayTownHelpActionResultReloadAndDedupe()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "NewGameButton"));
            yield return WaitForScene("Town", 10f, "HELP-E2E-001 failed: SaveSlot New Game did not enter Town.");
            TownHelpActionRuntimeInstaller.EnsureForActiveScene();
            yield return null;
            yield return WaitForHelpRuntime(10f);

            var runtime = FindHelpRuntime();
            var outcome = runtime.Help.Action.Outcomes[0] as HelpActionTraitDeltaOutcome;
            Assert.IsNotNull(outcome, "HELP-E2E-003 failed: sample help outcome is not a trait delta strategy asset.");
            int initialTrait = runtime.Student.Progress.GetTraitValue(outcome.Trait);

            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.Help.transform.position, 0.25f, "HELP-E2E-002 failed: actual keyboard movement could not reach town help action point.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "HELP-E2E-002 failed: town help prompt was not visible after movement.");
            StringAssert.Contains("First Guide Errand", runtime.Router.PromptText, "HELP-E2E-003 failed: prompt did not target the sample town help action.");
            yield return PressInteractKey(keyboard);
            yield return null;

            int changedTrait = runtime.Student.Progress.GetTraitValue(outcome.Trait);
            Assert.AreEqual(LifeActivityResultKind.Applied, runtime.Help.LastResult.Kind, "HELP-E2E-003 failed: help action did not apply through input.");
            Assert.Greater(changedTrait, initialTrait, "HELP-E2E-004 failed: town help action did not update growth state.");
            CollectionAssert.Contains(runtime.Student.Progress.GetTodayActivityIds(), runtime.Help.Action.Id, "HELP-E2E-004 failed: today activity log did not record help action.");
            CollectionAssert.Contains(runtime.Student.Progress.GetTodayResultLogIds(), runtime.Help.LastResult.OutcomeLogIds[0], "HELP-E2E-004 failed: result log did not record help action outcome.");

            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.DayEnd.transform.position, 0.25f, "HELP-E2E-005 failed: actual keyboard movement could not reach day-end point.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "HELP-E2E-005 failed: day-end prompt was not visible after movement.");
            yield return PressInteractKey(keyboard);
            yield return WaitForDayResultPanel(2f, "HELP-E2E-005 failed: result UI did not open from actual day-end interaction.");

            var panel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
            AssertPanelContains(panel.transform, runtime.Help.Action.Id, "HELP-E2E-005 failed: result UI did not show help action id.");
            AssertPanelContains(panel.transform, outcome.Trait.Id, "HELP-E2E-005 failed: result UI did not show help growth target.");

            string studentFile = Path.Combine(_saveRoot, TestSlotId, "student-life-progress.json");
            Assert.IsTrue(File.Exists(studentFile), "HELP-E2E-006 failed: student progress save file missing after day end.");
            string resultReadyJson = File.ReadAllText(studentFile);
            StringAssert.Contains(runtime.Help.Action.Id, resultReadyJson, "HELP-E2E-006 failed: save file does not record help action log.");
            StringAssert.Contains("town-help:", resultReadyJson, "HELP-E2E-006 failed: save file does not record help action result log.");

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return LoadScene("MainMenu");
            EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "LoadButton"));
            yield return WaitForScene("Town", 10f, "HELP-E2E-006 failed: SaveSlot Load did not re-enter Town.");
            TownHelpActionRuntimeInstaller.EnsureForActiveScene();
            yield return null;
            yield return WaitForHelpRuntime(10f);

            var reloaded = FindHelpRuntime();
            Assert.AreEqual(StudentDayState.ResultReady, reloaded.Student.Progress.DayState, "HELP-E2E-006 failed: result-ready state did not restore.");
            Assert.AreEqual(changedTrait, reloaded.Student.Progress.GetTraitValue(outcome.Trait), "HELP-E2E-006 failed: help action growth state did not survive reload.");
            Assert.AreEqual(1, TownHelpActionDayLog.FromResultLogs(reloaded.Student.Progress.GetPreviousDayResultLogIds()).ActionIds.Length, "HELP-E2E-006 failed: previous help action log duplicated or disappeared after reload.");

            yield return WalkPlayerWithKeyboardTo(reloaded.Player, keyboard, reloaded.DayEnd.transform.position, 0.25f, "HELP-E2E-006 failed: movement could not return to day-end point after reload.");
            reloaded.Router.RefreshPromptNow();
            yield return PressInteractKey(keyboard);
            yield return WaitForDayResultPanel(2f, "HELP-E2E-006 failed: result UI did not reopen from saved result state.");
            Assert.AreEqual(1, TownHelpActionDayLog.FromResultLogs(reloaded.Student.Progress.GetPreviousDayResultLogIds()).ActionIds.Length, "HELP-E2E-006 failed: repeated result interaction duplicated help action log.");
            Assert.AreEqual(changedTrait, reloaded.Student.Progress.GetTraitValue(outcome.Trait), "HELP-E2E-006 failed: repeated result interaction applied help action growth again.");
        }

        private static IEnumerator WaitForHelpRuntime(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var player = GameObject.Find("Player");
                var help = GameObject.Find("TownHelpActionPoint");
                var dayEnd = GameObject.Find("StudentDayEndBoard");
                var panel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
                if (player != null && player.GetComponent<PlayerController>() != null && player.GetComponent<PlayerInteractionRouter>() != null && player.GetComponent<StudentLifeProgressComponent>() != null &&
                    help != null && help.GetComponent<TownHelpActionInteractor>() != null &&
                    dayEnd != null && dayEnd.GetComponent<StudentDayEndInteractor>() != null &&
                    panel != null && EventSystem.current != null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("Town help runtime did not expose player, help action point, day-end point, and result panel within timeout.");
        }

        private static HelpRuntime FindHelpRuntime()
        {
            var player = GameObject.Find("Player");
            var help = GameObject.Find("TownHelpActionPoint");
            var dayEnd = GameObject.Find("StudentDayEndBoard");
            Assert.IsNotNull(player);
            Assert.IsNotNull(help);
            Assert.IsNotNull(dayEnd);
            return new HelpRuntime(player, player.GetComponent<PlayerInteractionRouter>(), player.GetComponent<StudentLifeProgressComponent>(), help.GetComponent<TownHelpActionInteractor>(), dayEnd.GetComponent<StudentDayEndInteractor>());
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
            for (int i = 0; i < 360 && Vector3.Distance(player.transform.position, targetPosition) > tolerance; i++)
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
            Assert.IsFalse(string.IsNullOrEmpty(expected));
            var texts = panel.GetComponentsInChildren<Text>(true);
            var all = new List<string>();
            for (int i = 0; i < texts.Length; i++)
            {
                all.Add(texts[i].text);
                if (!string.IsNullOrEmpty(texts[i].text) && texts[i].text.Contains(expected)) return;
            }
            Assert.Fail(message + " Expected visible text to contain '" + expected + "' but saw: " + string.Join(" | ", all));
        }

        private static KeyControl ControlFor(Keyboard keyboard, Key key)
        {
            if (key == Key.A) return keyboard.aKey;
            if (key == Key.D) return keyboard.dKey;
            if (key == Key.W) return keyboard.wKey;
            if (key == Key.S) return keyboard.sKey;
            if (key == Key.E) return keyboard.eKey;
            Assert.Fail("Unsupported key: " + key);
            return keyboard.eKey;
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

        private readonly struct HelpRuntime
        {
            public readonly GameObject Player;
            public readonly PlayerInteractionRouter Router;
            public readonly StudentLifeProgressComponent Student;
            public readonly TownHelpActionInteractor Help;
            public readonly StudentDayEndInteractor DayEnd;

            public HelpRuntime(GameObject player, PlayerInteractionRouter router, StudentLifeProgressComponent student, TownHelpActionInteractor help, StudentDayEndInteractor dayEnd)
            {
                Player = player;
                Router = router;
                Student = student;
                Help = help;
                DayEnd = dayEnd;
            }
        }
    }
}
