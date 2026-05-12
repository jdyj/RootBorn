using System.Collections;
using System.Collections.Generic;
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
    public sealed class StudentDayResultLoopE2EScenarioTests : InputTestFixture
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
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-student-day-e2e", System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveRoot);
            SaveService.SetRootDirectoryForTests(_saveRoot);
            SaveSlotSelectPanel.SetSaveRootForTests(_saveRoot);
            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
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
        public IEnumerator STUDENT_DAY_E2E_001_011_PlayedActivityEndDayResultReloadAndNextDayLoop()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "NewGameButton"));
            yield return WaitForScene("Town", 10f, "STUDENT-DAY-E2E-001 failed: SaveSlot New Game did not enter Town.");
            yield return WaitForDayRuntime(10f);
            // Keep Unity InputSystem UI module test-environment exceptions from failing this end-to-end path; assertions below verify the gameplay contract.
            LogAssert.ignoreFailingMessages = true;

            var runtime = FindDayRuntime();
            int startDay = runtime.Student.Progress.CurrentDay;
            int initialTrait = runtime.Student.Progress.GetTraitValue(runtime.Study.PrimaryTrait);
            int initialSkill = runtime.Student.Progress.GetSkillValue(runtime.Study.PrimarySkill);
            Assert.AreEqual("tutorial.day1", runtime.Student.Progress.TutorialStageId, "STUDENT-DAY-E2E-001 failed: new save did not apply first tutorial stage from registry data.");
            StringAssert.Contains("Day 1", runtime.Student.Progress.NextGuideText, "STUDENT-DAY-E2E-001 failed: first-day guide text was not loaded from stage data.");

            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.Study.transform.position, 0.25f, "STUDENT-DAY-E2E-002 failed: actual keyboard movement could not reach study activity.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "STUDENT-DAY-E2E-002 failed: study prompt was not visible after movement.");
            StringAssert.Contains("Study Basics", runtime.Router.PromptText, "STUDENT-DAY-E2E-002 failed: prompt did not target study activity.");
            yield return PressInteractKey(keyboard);
            yield return null;

            int studiedTrait = runtime.Student.Progress.GetTraitValue(runtime.Study.PrimaryTrait);
            int studiedSkill = runtime.Student.Progress.GetSkillValue(runtime.Study.PrimarySkill);
            Assert.AreEqual(LifeActivityResultKind.Applied, runtime.Study.LastResult.Kind, "STUDENT-DAY-E2E-003 failed: study interaction did not apply through input.");
            Assert.Greater(studiedTrait, initialTrait, "STUDENT-DAY-E2E-004 failed: activity result did not update trait domain state.");
            Assert.Greater(studiedSkill, initialSkill, "STUDENT-DAY-E2E-004 failed: activity result did not update skill domain state.");
            CollectionAssert.Contains(runtime.Student.Progress.GetTodayActivityIds(), runtime.Study.Activity.Id, "STUDENT-DAY-E2E-004 failed: today activity log did not record study.");
            Assert.AreEqual("tutorial.day1", runtime.Student.Progress.TutorialStageId, "STUDENT-DAY-E2E-004 failed: activity completion must not derive tutorial stage from activity id.");

            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.DayEnd.transform.position, 0.25f, "STUDENT-DAY-E2E-005 failed: actual keyboard movement could not reach day-end point.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "STUDENT-DAY-E2E-005 failed: day-end prompt was not visible after movement.");
            StringAssert.Contains("End Day", runtime.Router.PromptText, "STUDENT-DAY-E2E-005 failed: prompt did not target day-end interaction.");
            yield return PressInteractKey(keyboard);
            yield return WaitForDayResultPanel(2f, "STUDENT-DAY-E2E-007 failed: result UI did not open from actual day-end interaction.");

            var panel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
            Assert.IsTrue(panel.IsOpen, "STUDENT-DAY-E2E-007 failed: result panel should be open.");
            Assert.AreEqual(StudentDayState.ResultReady, runtime.Student.Progress.DayState, "STUDENT-DAY-E2E-006 failed: day-end interaction did not set ResultReady domain state.");
            Assert.AreEqual(startDay, runtime.Student.Progress.LastSettledDay, "STUDENT-DAY-E2E-006 failed: settled day was not recorded.");
            Assert.AreEqual("tutorial.day2", runtime.Student.Progress.TutorialStageId, "STUDENT-DAY-E2E-006 failed: day-end stage rule did not advance to day two from data.");
            Assert.AreEqual("goal.talk-to-guide-day2", runtime.Student.Progress.NextObjectiveId, "STUDENT-DAY-E2E-006 failed: day-end stage rule did not set next objective from data.");
            StringAssert.Contains("Day 2", runtime.Student.Progress.NextGuideText, "STUDENT-DAY-E2E-006 failed: day-end stage rule did not set day-two guide text from data.");
            AssertPanelContains(panel.transform, "Day " + startDay, "STUDENT-DAY-E2E-007 failed: result UI did not show day number.");
            AssertPanelContains(panel.transform, runtime.Study.Activity.Id, "STUDENT-DAY-E2E-007 failed: result UI did not show completed activity.");
            AssertPanelContains(panel.transform, "trait.diligence", "STUDENT-DAY-E2E-007 failed: result UI did not show increased trait.");
            AssertPanelContains(panel.transform, "skill.basic-study", "STUDENT-DAY-E2E-007 failed: result UI did not show increased skill.");
            AssertPanelContains(panel.transform, "tutorial.day2", "STUDENT-DAY-E2E-007 failed: result UI did not show next tutorial stage.");
            AssertPanelContains(panel.transform, "goal.talk-to-guide-day2", "STUDENT-DAY-E2E-007 failed: result UI did not show next tutorial objective.");
            AssertPanelContains(panel.transform, "Day 2", "STUDENT-DAY-E2E-007 failed: result UI did not show next-day guide text.");
            AssertPanelContains(panel.transform, "Next Day", "STUDENT-DAY-E2E-007 failed: result UI did not show next-day guidance.");

            string studentFile = Path.Combine(_saveRoot, TestSlotId, "student-life-progress.json");
            Assert.IsTrue(File.Exists(studentFile), "STUDENT-DAY-E2E-010 failed: student day save file missing after day end.");
            string resultReadyJson = File.ReadAllText(studentFile);
            StringAssert.Contains("CurrentDay", resultReadyJson, "STUDENT-DAY-E2E-010 failed: save file does not record current day.");
            StringAssert.Contains("ResultReady", resultReadyJson, "STUDENT-DAY-E2E-010 failed: save file does not record result-ready state.");
            StringAssert.Contains("PreviousDayActivityIds", resultReadyJson, "STUDENT-DAY-E2E-010 failed: save file does not record previous day activity list.");
            StringAssert.Contains(runtime.Study.Activity.Id, resultReadyJson, "STUDENT-DAY-E2E-010 failed: save file does not record completed study activity.");
            StringAssert.Contains("tutorial.day2", resultReadyJson, "STUDENT-DAY-E2E-010 failed: save file does not persist tutorial stage.");
            StringAssert.Contains("goal.talk-to-guide-day2", resultReadyJson, "STUDENT-DAY-E2E-010 failed: save file does not persist next objective.");

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            LogAssert.ignoreFailingMessages = true;
            yield return LoadScene("MainMenu");
            EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "LoadButton"));
            yield return WaitForScene("Town", 10f, "STUDENT-DAY-E2E-010 failed: SaveSlot Load did not re-enter Town.");
            yield return WaitForDayRuntime(10f);
            LogAssert.ignoreFailingMessages = true;

            var reloaded = FindDayRuntime();
            Assert.AreEqual(startDay, reloaded.Student.Progress.CurrentDay, "STUDENT-DAY-E2E-010 failed: result-ready day did not restore.");
            Assert.AreEqual(StudentDayState.ResultReady, reloaded.Student.Progress.DayState, "STUDENT-DAY-E2E-010 failed: result-ready state did not restore.");
            Assert.AreEqual("tutorial.day2", reloaded.Student.Progress.TutorialStageId, "STUDENT-DAY-E2E-010 failed: tutorial stage did not survive reload.");
            Assert.AreEqual("goal.talk-to-guide-day2", reloaded.Student.Progress.NextObjectiveId, "STUDENT-DAY-E2E-010 failed: next objective did not survive reload.");
            Assert.AreEqual(studiedTrait, reloaded.Student.Progress.GetTraitValue(reloaded.Study.PrimaryTrait), "STUDENT-DAY-E2E-009 failed: cumulative trait did not survive reload.");
            Assert.AreEqual(studiedSkill, reloaded.Student.Progress.GetSkillValue(reloaded.Study.PrimarySkill), "STUDENT-DAY-E2E-009 failed: cumulative skill did not survive reload.");
            Assert.AreEqual(1, reloaded.Student.Progress.GetPreviousDayActivityIds().Length, "STUDENT-DAY-E2E-011 failed: previous day log duplicated or disappeared after reload.");

            yield return WalkPlayerWithKeyboardTo(reloaded.Player, keyboard, reloaded.DayEnd.transform.position, 0.25f, "STUDENT-DAY-E2E-011 failed: movement could not return to day-end point after reload.");
            reloaded.Router.RefreshPromptNow();
            yield return PressInteractKey(keyboard);
            yield return WaitForDayResultPanel(2f, "STUDENT-DAY-E2E-011 failed: result UI did not reopen from saved result state.");
            Assert.AreEqual(1, reloaded.Student.Progress.GetPreviousDayActivityIds().Length, "STUDENT-DAY-E2E-011 failed: repeated day-end interaction duplicated settlement after reload.");
            Assert.AreEqual("tutorial.day2", reloaded.Student.Progress.TutorialStageId, "STUDENT-DAY-E2E-011 failed: repeated day-end interaction changed stage again.");

            panel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
            yield return ClickButton(FindButton(panel.transform, "NextDayButton"));
            Assert.AreEqual(startDay + 1, reloaded.Student.Progress.CurrentDay, "STUDENT-DAY-E2E-008 failed: result UI button did not advance to next day.");
            Assert.AreEqual(StudentDayState.InProgress, reloaded.Student.Progress.DayState, "STUDENT-DAY-E2E-009 failed: next day did not return to in-progress state.");
            Assert.AreEqual("tutorial.day2", reloaded.Student.Progress.TutorialStageId, "STUDENT-DAY-E2E-009 failed: day-two stage was lost on next-day transition.");
            Assert.AreEqual("goal.talk-to-guide-day2", reloaded.Student.Progress.NextObjectiveId, "STUDENT-DAY-E2E-009 failed: day-two objective was lost on next-day transition.");
            Assert.AreEqual(0, reloaded.Student.Progress.GetTodayActivityIds().Length, "STUDENT-DAY-E2E-009 failed: today log was not reset for next day.");
            Assert.AreEqual(studiedTrait, reloaded.Student.Progress.GetTraitValue(reloaded.Study.PrimaryTrait), "STUDENT-DAY-E2E-009 failed: cumulative trait was lost on next day.");
            Assert.AreEqual(studiedSkill, reloaded.Student.Progress.GetSkillValue(reloaded.Study.PrimarySkill), "STUDENT-DAY-E2E-009 failed: cumulative skill was lost on next day.");

            yield return WalkPlayerWithKeyboardTo(reloaded.Player, keyboard, reloaded.Npc.transform.position, 0.25f, "STUDENT-DAY-E2E-012 failed: actual keyboard movement could not reach day-two guide NPC.");
            reloaded.Router.RefreshPromptNow();
            Assert.IsTrue(reloaded.Router.PromptVisible, "STUDENT-DAY-E2E-012 failed: guide NPC prompt was not visible on day two.");
            yield return PressInteractKey(keyboard);
            yield return WaitForDialoguePanel(reloaded.DialoguePanel, 3f, "STUDENT-DAY-E2E-012 failed: day-two guide dialogue did not open through interaction.");
            AssertPanelContains(reloaded.DialoguePanel.transform, "dialogue.guide.day2", "STUDENT-DAY-E2E-012 failed: guide NPC did not switch to day-two dialogue from tutorial stage data.");

            string nextDayJson = File.ReadAllText(studentFile);
            StringAssert.Contains("\"CurrentDay\": 2", nextDayJson, "STUDENT-DAY-E2E-010 failed: save file did not persist next day number.");
            StringAssert.Contains("InProgress", nextDayJson, "STUDENT-DAY-E2E-010 failed: save file did not persist next day state.");
            StringAssert.Contains("tutorial.day2", nextDayJson, "STUDENT-DAY-E2E-010 failed: save file did not persist day-two tutorial stage.");
            StringAssert.Contains("goal.talk-to-guide-day2", nextDayJson, "STUDENT-DAY-E2E-010 failed: save file did not persist day-two objective.");
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
                    study != null && study.GetComponent<StudentLifeActivityInteractor>() != null &&
                    dayEnd != null && dayEnd.GetComponent<StudentDayEndInteractor>() != null &&
                    npc != null && dialoguePanel != null && panel != null && EventSystem.current != null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("Student day runtime did not expose player, study, day-end point, guide NPC, dialogue panel, and result panel within timeout.");
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

        private static IEnumerator WaitForDayResultPanel(float timeoutSeconds, string failure)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var panel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
                if (panel != null && panel.IsOpen)
                {
                    yield break;
                }

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
                if (panel != null && panel.IsOpen)
                {
                    yield break;
                }

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
                    if (activeKey != Key.None)
                    {
                        Release(ControlFor(keyboard, activeKey));
                    }
                    Press(ControlFor(keyboard, key));
                    activeKey = key;
                }

                InputSystem.Update();
                yield return null;
                yield return new WaitForFixedUpdate();
            }

            if (activeKey != Key.None)
            {
                Release(ControlFor(keyboard, activeKey));
            }

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
            while (!op.isDone)
            {
                yield return null;
            }
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
            if (root.name == name)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                var match = FindChild(root.GetChild(i), name);
                if (match != null)
                {
                    return match;
                }
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
                if (!string.IsNullOrEmpty(texts[i].text) && texts[i].text.Contains(expected))
                {
                    return;
                }
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
            if (EventSystem.current != null)
            {
                return;
            }

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
                if (transform != null && transform.name == objectName)
                {
                    Object.DestroyImmediate(transform.gameObject);
                }
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
