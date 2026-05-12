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
    public sealed class SelfStudyLibraryGrowthE2EScenarioTests : InputTestFixture
    {
        private const string TestSlotId = "slot-0";
        private const string SelfStudyCategoryId = "outside.category.self-study";
        private const string LibraryStudyActivityId = "outside.self-study.library";
        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.timeScale = 1f;
            CleanupResidue();
            EnsurePassiveEventSystem();
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-self-study-e2e", System.Guid.NewGuid().ToString("N"));
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
        public IEnumerator STUDY_E2E_001_006_PlaySelfStudyEndDayResultReloadAndDedupe()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();

            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "NewGameButton"));
            yield return WaitForScene("Town", 10f, "STUDY-E2E-001 failed: SaveSlot New Game did not enter Town.");
            yield return WaitForSelfStudyRuntime(10f);
            LogAssert.ignoreFailingMessages = true;

            var runtime = FindSelfStudyRuntime();
            var targets = GrowthTargets.From(runtime.Study.Activity);
            Assert.IsTrue(targets.HasAny, "STUDY-E2E-003 failed: self-study activity does not expose trait, skill, relationship, or condition outcome strategies.");
            int startEnergy = runtime.Student.Progress.Energy;
            int startFocus = runtime.Student.Progress.Focus;
            int startStress = runtime.Student.Progress.Stress;
            int initialTrait = targets.Trait != null ? runtime.Student.Progress.GetTraitValue(targets.Trait) : 0;
            int initialSkill = targets.Skill != null ? runtime.Student.Progress.GetSkillValue(targets.Skill) : 0;

            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.Study.transform.position, 0.25f, "STUDY-E2E-002 failed: actual keyboard movement could not reach self-study point.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "STUDY-E2E-002 failed: self-study prompt was not visible after movement.");
            StringAssert.Contains("Library", runtime.Router.PromptText, "STUDY-E2E-003 failed: prompt did not target library self-study.");
            yield return PressInteractKey(keyboard);
            yield return null;

            Assert.AreEqual(LifeActivityResultKind.Applied, runtime.Study.LastResult.Kind, "STUDY-E2E-003 failed: self-study interaction did not apply through input.");
            Assert.AreEqual(startEnergy - runtime.Study.Activity.EnergyCost, runtime.Student.Progress.Energy, "STUDY-E2E-004 failed: energy cost was not applied.");
            Assert.AreEqual(startFocus - runtime.Study.Activity.FocusCost, runtime.Student.Progress.Focus, "STUDY-E2E-004 failed: focus cost was not applied.");
            Assert.AreEqual(startStress + runtime.Study.Activity.StressDelta, runtime.Student.Progress.Stress, "STUDY-E2E-004 failed: condition/stress cost was not applied.");
            if (targets.Trait != null) Assert.Greater(runtime.Student.Progress.GetTraitValue(targets.Trait), initialTrait, "STUDY-E2E-004 failed: trait growth did not apply.");
            if (targets.Skill != null) Assert.Greater(runtime.Student.Progress.GetSkillValue(targets.Skill), initialSkill, "STUDY-E2E-004 failed: skill growth did not apply.");
            CollectionAssert.Contains(runtime.Student.Progress.GetTodayActivityIds(), LibraryStudyActivityId, "STUDY-E2E-004 failed: today activity log did not record self-study.");
            CollectionAssert.Contains(runtime.Student.Progress.GetTodayResultLogIds(), runtime.Study.LastResult.OutcomeLogIds[0], "STUDY-E2E-004 failed: self-study outcome log was not recorded.");

            yield return WalkPlayerWithKeyboardTo(runtime.Player, keyboard, runtime.DayEnd.transform.position, 0.25f, "STUDY-E2E-005 failed: actual keyboard movement could not reach day-end point.");
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible, "STUDY-E2E-005 failed: day-end prompt was not visible after movement.");
            yield return PressInteractKey(keyboard);
            yield return WaitForDayResultPanel(2f, "STUDY-E2E-005 failed: result UI did not open from actual day-end interaction.");

            var panel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
            AssertPanelContains(panel.transform, "Outside School", "STUDY-E2E-005 failed: result UI did not show outside-school/self-study section.");
            AssertPanelContains(panel.transform, LibraryStudyActivityId, "STUDY-E2E-005 failed: result UI did not show self-study activity id.");
            AssertPanelContains(panel.transform, targets.FirstTargetId, "STUDY-E2E-005 failed: result UI did not show self-study growth target.");

            string studentFile = Path.Combine(_saveRoot, TestSlotId, "student-life-progress.json");
            Assert.IsTrue(File.Exists(studentFile), "STUDY-E2E-006 failed: student progress save file missing after day end.");
            string resultReadyJson = File.ReadAllText(studentFile);
            StringAssert.Contains(LibraryStudyActivityId, resultReadyJson, "STUDY-E2E-006 failed: save file does not record self-study activity.");
            StringAssert.Contains("outside-school:", resultReadyJson, "STUDY-E2E-006 failed: save file does not record self-study result log.");

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return LoadScene("MainMenu");
            EnsurePassiveEventSystem();
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            yield return ClickButton(FindButton("SaveSlotCard_" + TestSlotId, "LoadButton"));
            yield return WaitForScene("Town", 10f, "STUDY-E2E-006 failed: SaveSlot Load did not re-enter Town.");
            yield return WaitForSelfStudyRuntime(10f);

            var reloaded = FindSelfStudyRuntime();
            Assert.AreEqual(StudentDayState.ResultReady, reloaded.Student.Progress.DayState, "STUDY-E2E-006 failed: result-ready state did not restore.");
            Assert.AreEqual(1, OutsideSchoolDayLog.FromResultLogs(reloaded.Student.Progress.GetPreviousDayResultLogIds()).ActivityIds.Length, "STUDY-E2E-006 failed: previous self-study log duplicated or disappeared after reload.");
            if (targets.Trait != null) Assert.AreEqual(runtime.Student.Progress.GetTraitValue(targets.Trait), reloaded.Student.Progress.GetTraitValue(targets.Trait), "STUDY-E2E-006 failed: trait growth did not survive reload.");
            if (targets.Skill != null) Assert.AreEqual(runtime.Student.Progress.GetSkillValue(targets.Skill), reloaded.Student.Progress.GetSkillValue(targets.Skill), "STUDY-E2E-006 failed: skill growth did not survive reload.");

            yield return WalkPlayerWithKeyboardTo(reloaded.Player, keyboard, reloaded.DayEnd.transform.position, 0.25f, "STUDY-E2E-006 failed: movement could not return to day-end point after reload.");
            reloaded.Router.RefreshPromptNow();
            yield return PressInteractKey(keyboard);
            yield return WaitForDayResultPanel(2f, "STUDY-E2E-006 failed: result UI did not reopen from saved result state.");
            Assert.AreEqual(1, OutsideSchoolDayLog.FromResultLogs(reloaded.Student.Progress.GetPreviousDayResultLogIds()).ActivityIds.Length, "STUDY-E2E-006 failed: repeated result interaction duplicated self-study log.");

            panel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
            yield return ClickButton(FindButton(panel.transform, "NextDayButton"));
            Assert.AreEqual(StudentDayState.InProgress, reloaded.Student.Progress.DayState, "STUDY-E2E-006 failed: result UI button did not start the next day.");
            Assert.AreEqual(0, reloaded.Student.Progress.GetTodayActivityIds().Length, "STUDY-E2E-006 failed: next day did not clear today activity list.");
        }

        private static IEnumerator WaitForSelfStudyRuntime(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var runtime = TryFindSelfStudyRuntime();
                var panel = Object.FindFirstObjectByType<StudentDayResultPanel>(FindObjectsInactive.Include);
                if (runtime.HasValue && panel != null && EventSystem.current != null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("Self-study runtime did not expose player, library self-study point, day-end point, and result panel within timeout.");
        }

        private static StudyRuntime FindSelfStudyRuntime()
        {
            var runtime = TryFindSelfStudyRuntime();
            Assert.IsTrue(runtime.HasValue, "Self-study runtime must be available.");
            return runtime.Value;
        }

        private static StudyRuntime? TryFindSelfStudyRuntime()
        {
            var player = GameObject.Find("Player");
            var dayEnd = GameObject.Find("StudentDayEndBoard");
            var study = FindSelfStudyInteractor();
            if (player == null || dayEnd == null || study == null)
            {
                return null;
            }

            var router = player.GetComponent<PlayerInteractionRouter>();
            var student = player.GetComponent<StudentLifeProgressComponent>();
            var dayEndInteractor = dayEnd.GetComponent<StudentDayEndInteractor>();
            if (router == null || student == null || dayEndInteractor == null)
            {
                return null;
            }

            return new StudyRuntime(player, router, student, study, dayEndInteractor);
        }

        private static OutsideSchoolActivityInteractor FindSelfStudyInteractor()
        {
            var interactors = Object.FindObjectsByType<OutsideSchoolActivityInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < interactors.Length; i++)
            {
                var activity = interactors[i].Activity;
                if (activity != null && activity.Id == LibraryStudyActivityId && activity.CategoryId == SelfStudyCategoryId)
                {
                    return interactors[i];
                }
            }

            return null;
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

        private readonly struct StudyRuntime
        {
            public readonly GameObject Player;
            public readonly PlayerInteractionRouter Router;
            public readonly StudentLifeProgressComponent Student;
            public readonly OutsideSchoolActivityInteractor Study;
            public readonly StudentDayEndInteractor DayEnd;

            public StudyRuntime(GameObject player, PlayerInteractionRouter router, StudentLifeProgressComponent student, OutsideSchoolActivityInteractor study, StudentDayEndInteractor dayEnd)
            {
                Player = player;
                Router = router;
                Student = student;
                Study = study;
                DayEnd = dayEnd;
            }
        }

        private readonly struct GrowthTargets
        {
            public readonly TraitDefinition Trait;
            public readonly SkillDefinition Skill;
            public readonly string FirstTargetId;
            public bool HasAny => Trait != null || Skill != null || !string.IsNullOrEmpty(FirstTargetId);

            private GrowthTargets(TraitDefinition trait, SkillDefinition skill, string firstTargetId)
            {
                Trait = trait;
                Skill = skill;
                FirstTargetId = string.IsNullOrEmpty(firstTargetId) ? string.Empty : firstTargetId;
            }

            public static GrowthTargets From(OutsideSchoolActivityDefinition activity)
            {
                TraitDefinition trait = null;
                SkillDefinition skill = null;
                string first = string.Empty;
                var outcomes = activity != null ? activity.Outcomes : null;
                if (outcomes != null)
                {
                    for (int i = 0; i < outcomes.Count; i++)
                    {
                        if (outcomes[i] is OutsideSchoolTraitDeltaOutcome traitOutcome)
                        {
                            trait = trait ?? traitOutcome.Trait;
                            first = string.IsNullOrEmpty(first) && traitOutcome.Trait != null ? traitOutcome.Trait.Id : first;
                        }
                        else if (outcomes[i] is OutsideSchoolSkillDeltaOutcome skillOutcome)
                        {
                            skill = skill ?? skillOutcome.Skill;
                            first = string.IsNullOrEmpty(first) && skillOutcome.Skill != null ? skillOutcome.Skill.Id : first;
                        }
                    }
                }

                return new GrowthTargets(trait, skill, first);
            }
        }
    }
}
