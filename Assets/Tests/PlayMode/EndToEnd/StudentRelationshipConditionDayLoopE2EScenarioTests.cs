using System.Collections;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.UI.MainMenu;
using Rootborn.UI.Quests;
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
    public sealed class StudentRelationshipConditionDayLoopE2EScenarioTests : InputTestFixture
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
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-relcond-e2e", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator RELCOND_E2E_001_011_PlayedRelationshipConditionDayLoopPersistsAndDoesNotDuplicate()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var registry = Rootborn.Game.Managers.Managers.Data.Registry;
            var relationship = registry.Relationships[0];
            var status = registry.StudentConditionStatuses[0];

            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "NewGameButton"));
            yield return WaitForScene("Town", 10f, "RELCOND-E2E-001 failed: ?Ä???¨Î°Ø UI ??Í≤åÏûÑ??Town??ÏßÑÏûÖ?òÏ? Î™ªÌñà??");
            yield return WaitForDayRuntime(10f);
            LogAssert.ignoreFailingMessages = true;

            var runtime = FindDayRuntime();
            AssertPanelNotOpen();
            int initialRelationship = runtime.Student.Progress.GetRelationshipValue(relationship);
            int initialStatus = runtime.Student.Progress.GetStatusValue(status);

            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.Study.transform.position, 0.25f, "RELCOND-E2E-002 failed: NPC/?ÑÏ? ?âÎèô ÏßÄ?êÍπåÏßÄ ?§Ï†ú ?§Î≥¥???¥Îèô ?§Ìå®");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "RELCOND-E2E-002 failed: ?ÅÌò∏?ëÏö© ?ÑÎ°¨?ÑÌä∏ ?ÜÏùå");
            yield return PressInteractKey(keyboard);
            yield return null;

            int changedRelationship = runtime.Student.Progress.GetRelationshipValue(relationship);
            int changedStatus = runtime.Student.Progress.GetStatusValue(status);
            Assert.Greater(changedRelationship, initialRelationship, "RELCOND-E2E-004 failed: relationship value did not increase.");
            Assert.Greater(changedStatus, initialStatus, "RELCOND-E2E-004 failed: status value did not increase.");
            CollectionAssert.Contains(runtime.Student.Progress.GetTodayResultLogIds(), runtime.Study.Activity.Id + ":+" + relationship.Id + "=2", "RELCOND-E2E-004 failed: ?§Îäò Í¥ÄÍ≥?Î≥Ä??Î°úÍ∑∏ ?ÑÎùΩ");
            CollectionAssert.Contains(runtime.Student.Progress.GetTodayResultLogIds(), runtime.Study.Activity.Id + ":+" + status.Id + "=3", "RELCOND-E2E-004 failed: ?§Îäò Ïª®Îîî??Î≥Ä??Î°úÍ∑∏ ?ÑÎùΩ");

            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.DayEnd.transform.position, 0.25f, "RELCOND-E2E-005 failed: ?òÎ£® Ï¢ÖÎ£å ÏßÄ?êÍπåÏßÄ ?§Ï†ú ?§Î≥¥???¥Îèô ?§Ìå®");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "RELCOND-E2E-005 failed: ?òÎ£® Ï¢ÖÎ£å ?ÑÎ°¨?ÑÌä∏ ?ÜÏùå");
            yield return PressInteractKey(keyboard);
            yield return WaitForDayResultPanel(2f, "RELCOND-E2E-007 failed: ?§Ï†ú ?òÎ£® Ï¢ÖÎ£å ?ÖÎ†• ??Í≤∞Í≥º UIÍ∞Ä ?¥Î¶¨ÏßÄ ?äÏùå");

            var panel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
            AssertPanelContains(panel.transform, relationship.DisplayNameKey, "RELCOND-E2E-007 failed: ?òÎ£® Í≤∞Í≥º Í¥ÄÍ≥??îÏïΩ ?ÑÎùΩ");
            AssertPanelContains(panel.transform, initialRelationship + " -> " + changedRelationship, "RELCOND-E2E-007 failed: ?òÎ£® Í≤∞Í≥º Í¥ÄÍ≥??¥Ï†Ñ/?¥ÌõÑ Í∞??ÑÎùΩ");
            AssertPanelContains(panel.transform, status.DisplayNameKey, "RELCOND-E2E-007 failed: day result status summary missing.");
            AssertPanelContains(panel.transform, initialStatus + " -> " + changedStatus, "RELCOND-E2E-007 failed: ?òÎ£® Í≤∞Í≥º Ïª®Îîî???¥Ï†Ñ/?¥ÌõÑ Í∞??ÑÎùΩ");
            AssertPanelContains(panel.transform, "tomorrow", "RELCOND-E2E-007 failed: ?§Ïùå ???ÅÌñ• ?úÏãú ?ÑÎùΩ");

            string studentFile = Path.Combine(_saveRoot, TestSlotId, "student-life-progress.json");
            Assert.IsTrue(File.Exists(studentFile), "RELCOND-E2E-010 failed: ?Ä???åÏùº ?ÜÏùå");
            string resultReadyJson = File.ReadAllText(studentFile);
            StringAssert.Contains(relationship.Id, resultReadyJson, "RELCOND-E2E-010 failed: ?Ä????Í¥ÄÍ≥?Î≥µÏõê ?∞Ïù¥???ÑÎùΩ");
            StringAssert.Contains(status.Id, resultReadyJson, "RELCOND-E2E-010 failed: ?Ä????Ïª®Îîî??Î≥µÏõê ?∞Ïù¥???ÑÎùΩ");

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return LoadScene("MainMenu");
            EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "LoadButton"));
            yield return WaitForScene("Town", 10f, "RELCOND-E2E-010 failed: ?Ä???¨Î°Ø UI Î°úÎìú ??Town ?¨ÏßÑ???§Ìå®");
            yield return WaitForDayRuntime(10f);

            var reloaded = FindDayRuntime();
            Assert.AreEqual(changedRelationship, reloaded.Student.Progress.GetRelationshipValue(relationship), "RELCOND-E2E-010 failed: ?Ä????Í¥ÄÍ≥?Î≥µÏõê ?§Ìå®");
            Assert.AreEqual(changedStatus, reloaded.Student.Progress.GetStatusValue(status), "RELCOND-E2E-010 failed: ?Ä????Ïª®Îîî??Î≥µÏõê ?§Ìå®");
            Assert.AreEqual(1, reloaded.Student.Progress.GetPreviousDayRelationshipDeltas().Length, "RELCOND-E2E-011 failed: Ï§ëÎ≥µ Í¥ÄÍ≥??ïÏÇ∞ Î∞úÏÉù");
            Assert.AreEqual(1, reloaded.Student.Progress.GetPreviousDayStatusDeltas().Length, "RELCOND-E2E-011 failed: Ï§ëÎ≥µ Ïª®Îîî???ïÏÇ∞ Î∞úÏÉù");

            yield return WalkPlayerWithKeyboardTo(reloaded.Player, keyboard, reloaded.DayEnd.transform.position, 0.25f, "RELCOND-E2E-011 failed: ?Ä??Î°úÎìú ???òÎ£® Í≤∞Í≥º ?¨Ìôï??ÏßÄ???¥Îèô ?§Ìå®");
            reloaded.Router.RefreshPromptNow();
            yield return PressInteractKey(keyboard);
            yield return WaitForDayResultPanel(2f, "RELCOND-E2E-011 failed: ?Ä??Î°úÎìú ??Í≤∞Í≥º UI ?¨Ìôï???§Ìå®");
            Assert.AreEqual(changedRelationship, reloaded.Student.Progress.GetRelationshipValue(relationship), "RELCOND-E2E-011 failed: Í≤∞Í≥º ?¨Ìôï?∏ÏúºÎ°?Í¥ÄÍ≥?Ï§ëÎ≥µ ?ÅÏö©");
            Assert.AreEqual(changedStatus, reloaded.Student.Progress.GetStatusValue(status), "RELCOND-E2E-011 failed: Í≤∞Í≥º ?¨Ìôï?∏ÏúºÎ°?Ïª®Îîî??Ï§ëÎ≥µ ?ÅÏö©");

            panel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
            yield return ClickButton(FindButton(panel.transform, "NextDayButton"));
            yield return WalkPlayerWithKeyboardTo(reloaded.Player, keyboard, reloaded.Npc.transform.position, 0.25f, "RELCOND-E2E-009 failed: 2?ºÏ∞® NPCÍπåÏ? ?§Ï†ú ?§Î≥¥???¥Îèô ?§Ìå®");
            reloaded.Router.RefreshPromptNow();
            yield return PressInteractKey(keyboard);
            yield return WaitForDialoguePanel(reloaded.DialoguePanel, 3f, "RELCOND-E2E-009 failed: 2?ºÏ∞® NPC ?Ä??UI ?¥Î¶º ?§Ìå®");
            AssertPanelContains(reloaded.DialoguePanel.transform, "dialogue.guide.day2", "RELCOND-E2E-009 failed: 2?ºÏ∞® ?Ä??Î≥Ä???ÜÏùå");
        }

        private static IEnumerator WaitForDayRuntime(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var player = GameObject.Find("Player");
                var study = GameObject.Find("StudyBasicsActivity");
                var dayEnd = GameObject.Find("StudentDayEndBoard");
                var panel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
                var npc = Object.FindFirstObjectByType<NpcInteractor>(FindObjectsInactive.Include);
                var dialoguePanel = Object.FindFirstObjectByType<DialoguePanel>(FindObjectsInactive.Include);
                if (player != null && player.GetComponent<PlayerController>() != null && player.GetComponent<PlayerInteractionRouter>() != null && player.GetComponent<StudentLifeProgressComponent>() != null &&
                    study != null && study.GetComponent<StudentLifeActivityInteractor>() != null && dayEnd != null && dayEnd.GetComponent<StudentDayEndInteractor>() != null && npc != null && dialoguePanel != null && panel != null && EventSystem.current != null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("RELCOND-E2E setup failed: runtime objects not ready.");
        }

        private static DayRuntime FindDayRuntime()
        {
            var player = GameObject.Find("Player");
            var study = GameObject.Find("StudyBasicsActivity");
            var dayEnd = GameObject.Find("StudentDayEndBoard");
            var npc = Object.FindFirstObjectByType<NpcInteractor>(FindObjectsInactive.Include);
            var dialoguePanel = Object.FindFirstObjectByType<DialoguePanel>(FindObjectsInactive.Include);
            Assert.IsNotNull(player);
            Assert.IsNotNull(study);
            Assert.IsNotNull(dayEnd);
            Assert.IsNotNull(npc);
            Assert.IsNotNull(dialoguePanel);
            return new DayRuntime(player, player.GetComponent<PlayerInteractionRouter>(), player.GetComponent<StudentLifeProgressComponent>(), study.GetComponent<StudentLifeActivityInteractor>(), dayEnd.GetComponent<StudentDayEndInteractor>(), npc, dialoguePanel);
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

        private static IEnumerator WaitForDialoguePanel(DialoguePanel panel, float timeoutSeconds, string failure)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                if (panel != null && panel.IsOpen) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail(failure);
        }

        private static IEnumerator ClickButton(Button button)
        {
            Assert.IsNotNull(button);
            Assert.IsTrue(button.gameObject.activeInHierarchy, button.name + " must be active for user click.");
            Assert.IsTrue(button.interactable, button.name + " must be interactable for user click.");
            var eventData = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(button.gameObject, eventData, ExecuteEvents.pointerClickHandler);
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
            var all = new System.Collections.Generic.List<string>();
            for (int i = 0; i < texts.Length; i++)
            {
                all.Add(texts[i].text);
                if (!string.IsNullOrEmpty(texts[i].text) && texts[i].text.Contains(expected)) return;
            }

            Assert.Fail(message + " Expected visible text to contain '" + expected + "' but saw: " + string.Join(" | ", all));
        }

        private static void AssertPanelNotOpen()
        {
            var panel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
            Assert.IsTrue(panel == null || !panel.IsOpen);
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

        private readonly struct DayRuntime
        {
            public readonly GameObject Player;
            public readonly PlayerInteractionRouter Router;
            public readonly StudentLifeProgressComponent Student;
            public readonly StudentLifeActivityInteractor Study;
            public readonly StudentDayEndInteractor DayEnd;
            public readonly NpcInteractor Npc;
            public readonly DialoguePanel DialoguePanel;

            public DayRuntime(GameObject player, PlayerInteractionRouter router, StudentLifeProgressComponent student, StudentLifeActivityInteractor study, StudentDayEndInteractor dayEnd, NpcInteractor npc, DialoguePanel dialoguePanel)
            {
                Player = player;
                Router = router;
                Student = student;
                Study = study;
                DayEnd = dayEnd;
                Npc = npc;
                DialoguePanel = dialoguePanel;
            }
        }
    }
}
