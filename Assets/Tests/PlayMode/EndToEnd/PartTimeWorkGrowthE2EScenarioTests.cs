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
    public sealed class PartTimeWorkGrowthE2EScenarioTests : InputTestFixture
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
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-work-e2e", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator WORK_E2E_001_006_PlayPartTimeWorkResultReloadAndDedupe()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "NewGameButton"));
            yield return WaitForScene("Town", 10f, "WORK-E2E-001 failed: SaveSlot New Game did not enter Town.");
            PartTimeWorkRuntimeInstaller.EnsureForActiveScene();
            yield return null;
            yield return WaitForWorkRuntime(10f);

            var runtime = FindWorkRuntime();
            var traitOutcome = runtime.Work.Work.Outcomes[0] as WorkTraitDeltaOutcome;
            Assert.IsNotNull(traitOutcome, "WORK-E2E-003 failed: sample work outcome is not a trait delta strategy asset.");
            var reward = runtime.Work.Work.Rewards[0];
            Assert.IsNotNull(reward.Item, "WORK-E2E-003 failed: sample work reward item missing.");
            int initialTrait = runtime.Student.Progress.GetTraitValue(traitOutcome.Trait);
            int initialRewardCount = runtime.Inventory.Inventory.CountOf(reward.Item);

            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.Work.transform.position, 0.25f, "WORK-E2E-002 failed: actual keyboard movement could not reach part-time work point.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "WORK-E2E-002 failed: part-time work prompt was not visible after movement.");
            StringAssert.Contains("Corner Store", runtime.Router.PromptText, "WORK-E2E-003 failed: prompt did not target the sample part-time work.");
            yield return PressInteractKey(keyboard);
            yield return null;

            int changedTrait = runtime.Student.Progress.GetTraitValue(traitOutcome.Trait);
            int changedRewardCount = runtime.Inventory.Inventory.CountOf(reward.Item);
            Assert.AreEqual(LifeActivityResultKind.Applied, runtime.Work.LastResult.Kind, "WORK-E2E-003 failed: part-time work did not apply through input.");
            Assert.Greater(changedTrait, initialTrait, "WORK-E2E-004 failed: part-time work did not update growth state.");
            Assert.AreEqual(initialRewardCount + reward.Count, changedRewardCount, "WORK-E2E-004 failed: part-time work item reward did not update inventory.");
            CollectionAssert.Contains(runtime.Student.Progress.GetTodayActivityIds(), runtime.Work.Work.Id, "WORK-E2E-004 failed: today activity log did not record part-time work.");
            CollectionAssert.Contains(runtime.Student.Progress.GetTodayResultLogIds(), runtime.Work.LastResult.OutcomeLogIds[0], "WORK-E2E-004 failed: result log did not record part-time work reward.");

            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.DayEnd.transform.position, 0.25f, "WORK-E2E-005 failed: actual keyboard movement could not reach day-end point.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "WORK-E2E-005 failed: day-end prompt was not visible after movement.");
            yield return PressInteractKey(keyboard);
            yield return WaitForDayResultPanel(2f, "WORK-E2E-005 failed: result UI did not open from actual day-end interaction.");

            var panel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
            AssertPanelContains(panel.transform, runtime.Work.Work.Id, "WORK-E2E-005 failed: result UI did not show work id.");
            AssertPanelContains(panel.transform, reward.Item.Id, "WORK-E2E-005 failed: result UI did not show work reward item.");
            AssertPanelContains(panel.transform, traitOutcome.Trait.Id, "WORK-E2E-005 failed: result UI did not show work growth target.");

            string studentFile = Path.Combine(_saveRoot, TestSlotId, "student-life-progress.json");
            Assert.IsTrue(File.Exists(studentFile), "WORK-E2E-006 failed: student progress save file missing after work.");
            StringAssert.Contains("part-time-work:", File.ReadAllText(studentFile), "WORK-E2E-006 failed: save file does not record part-time work result log.");

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return LoadScene("MainMenu");
            EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "LoadButton"));
            yield return WaitForScene("Town", 10f, "WORK-E2E-006 failed: SaveSlot Load did not re-enter Town.");
            PartTimeWorkRuntimeInstaller.EnsureForActiveScene();
            yield return null;
            yield return WaitForWorkRuntime(10f);

            var reloaded = FindWorkRuntime();
            Assert.AreEqual(StudentDayState.ResultReady, reloaded.Student.Progress.DayState, "WORK-E2E-006 failed: result-ready state did not restore.");
            Assert.AreEqual(changedTrait, reloaded.Student.Progress.GetTraitValue(traitOutcome.Trait), "WORK-E2E-006 failed: work growth state did not survive reload.");
            Assert.AreEqual(changedRewardCount, reloaded.Inventory.Inventory.CountOf(reward.Item), "WORK-E2E-006 failed: work reward inventory did not survive reload.");
            Assert.AreEqual(1, PartTimeWorkDayLog.FromResultLogs(reloaded.Student.Progress.GetPreviousDayResultLogIds()).WorkIds.Length, "WORK-E2E-006 failed: previous work log duplicated or disappeared after reload.");

            yield return WalkPlayerWithKeyboardTo(reloaded.Player, keyboard, reloaded.DayEnd.transform.position, 0.25f, "WORK-E2E-006 failed: movement could not return to day-end point after reload.");
            reloaded.Router.RefreshPromptNow();
            yield return PressInteractKey(keyboard);
            yield return WaitForDayResultPanel(2f, "WORK-E2E-006 failed: result UI did not reopen from saved result state.");
            Assert.AreEqual(changedRewardCount, reloaded.Inventory.Inventory.CountOf(reward.Item), "WORK-E2E-006 failed: repeated result interaction paid work reward again.");
        }

        private static IEnumerator WaitForWorkRuntime(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var player = GameObject.Find("Player");
                var work = GameObject.Find("PartTimeWorkPoint");
                var dayEnd = GameObject.Find("StudentDayEndBoard");
                var panel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
                if (player != null && player.GetComponent<PlayerController>() != null && player.GetComponent<PlayerInteractionRouter>() != null && player.GetComponent<StudentLifeProgressComponent>() != null && player.GetComponent<PlayerInventory>() != null &&
                    work != null && work.GetComponent<PartTimeWorkInteractor>() != null &&
                    dayEnd != null && dayEnd.GetComponent<StudentDayEndInteractor>() != null &&
                    panel != null && EventSystem.current != null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("Part-time work runtime did not expose player, work point, day-end point, and result panel within timeout.");
        }

        private static WorkRuntime FindWorkRuntime()
        {
            var player = GameObject.Find("Player");
            var work = GameObject.Find("PartTimeWorkPoint");
            var dayEnd = GameObject.Find("StudentDayEndBoard");
            Assert.IsNotNull(player);
            Assert.IsNotNull(work);
            Assert.IsNotNull(dayEnd);
            return new WorkRuntime(player, player.GetComponent<PlayerInteractionRouter>(), player.GetComponent<PlayerInventory>(), player.GetComponent<StudentLifeProgressComponent>(), work.GetComponent<PartTimeWorkInteractor>(), dayEnd.GetComponent<StudentDayEndInteractor>());
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

        private readonly struct WorkRuntime
        {
            public readonly GameObject Player;
            public readonly PlayerInteractionRouter Router;
            public readonly PlayerInventory Inventory;
            public readonly StudentLifeProgressComponent Student;
            public readonly PartTimeWorkInteractor Work;
            public readonly StudentDayEndInteractor DayEnd;

            public WorkRuntime(GameObject player, PlayerInteractionRouter router, PlayerInventory inventory, StudentLifeProgressComponent student, PartTimeWorkInteractor work, StudentDayEndInteractor dayEnd)
            {
                Player = player;
                Router = router;
                Inventory = inventory;
                Student = student;
                Work = work;
                DayEnd = dayEnd;
            }
        }
    }
}
