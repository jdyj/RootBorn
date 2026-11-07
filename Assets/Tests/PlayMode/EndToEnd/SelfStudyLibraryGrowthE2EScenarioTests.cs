using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.UI.MainMenu;
using Rootborn.UI.StudentLife;
using Rootborn.Tests.PlayMode.EndToEnd.Support;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    public sealed class SelfStudyLibraryGrowthE2EScenarioTests : Rootborn.Tests.PlayMode.PlayModeInputFixture
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

        // Free-path growth: the same trait grows outside school (library self-study in Town) and in school (class in School),
        // both reached by walking and pressing E, and both survive a reload once.
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator FREE_PATH_001_FocusGrowsFromLibrarySelfStudyAndFromSchoolClass()
        {
            const string FocusTraitId = "trait.focus";
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return PlayerFlowE2EDriver.EnterWorldFromSaveSlot(TestSlotId, "NewGameButton", "Town", "FREE-PATH-001 failed: New Game did not enter Town.");
            yield return PlayerFlowE2EDriver.DismissDailyEvent();
            var student = GameObject.Find("Player").GetComponent<StudentLifeProgressComponent>();
            int focusStart = student.Progress.GetTraitValueById(FocusTraitId);

            OutsideSchoolActivityInteractor library = null;
            foreach (var candidate in Object.FindObjectsByType<OutsideSchoolActivityInteractor>(FindObjectsSortMode.None))
                if (candidate.Activity != null && candidate.Activity.Id == "outside.self-study.library") library = candidate;
            Assert.IsNotNull(library, "FREE-PATH-001 failed: authored Town has no library self-study point.");
            yield return WalkAndInteract(keyboard, library.transform.position, "FREE-PATH-001 failed: library self-study");
            Assert.AreEqual(LifeActivityResultKind.Applied, library.LastResult.Kind, "FREE-PATH-001 failed: library self-study did not apply through E.");
            int focusAfterLibrary = student.Progress.GetTraitValueById(FocusTraitId);
            Assert.Greater(focusAfterLibrary, focusStart, "FREE-PATH-001 failed: studying outside school did not grow focus.");

            yield return PlayerFlowE2EDriver.WalkThroughDoor(keyboard, "School", "FREE-PATH-002 failed: could not enter School.");
            student = GameObject.Find("Player").GetComponent<StudentLifeProgressComponent>();
            Assert.AreEqual(focusAfterLibrary, student.Progress.GetTraitValueById(FocusTraitId), "FREE-PATH-002 failed: focus changed while walking to School.");
            var classroom = GameObject.Find("SchoolClassActivity");
            Assert.IsNotNull(classroom, "FREE-PATH-002 failed: School has no class activity.");
            yield return WalkAndInteract(keyboard, classroom.transform.position, "FREE-PATH-002 failed: school class");
            var panel = Object.FindFirstObjectByType<LocationIdentityPanel>(FindObjectsInactive.Include);
            Assert.IsTrue(panel != null && panel.IsOpen, "FREE-PATH-002 failed: E at the classroom did not open its activity panel.");
            yield return PlayerFlowE2EDriver.ClickButton(PlayerFlowE2EDriver.FindButton(panel.transform, "LocationActivityButton_0"));
            Assert.AreEqual(LifeActivityResultKind.Applied, panel.LastResult.Kind, "FREE-PATH-002 failed: the class did not apply (" + panel.LastResult.Kind + ").");
            int focusAfterClass = student.Progress.GetTraitValueById(FocusTraitId);
            Assert.Greater(focusAfterClass, focusAfterLibrary, "FREE-PATH-002 failed: attending class did not grow focus.");

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return PlayerFlowE2EDriver.EnterWorldFromSaveSlot(TestSlotId, "LoadButton", "School", "FREE-PATH-003 failed: Load did not return to School.");
            var reloaded = GameObject.Find("Player").GetComponent<StudentLifeProgressComponent>();
            Assert.AreEqual(focusAfterClass, reloaded.Progress.GetTraitValueById(FocusTraitId), "FREE-PATH-003 failed: focus from both routes did not survive reload exactly once.");
        }

        private static IEnumerator WalkAndInteract(Keyboard keyboard, Vector3 target, string failure)
        {
            var player = GameObject.Find("Player");
            yield return PlayerFlowE2EDriver.WalkPlayerWithKeyboardTo(player, keyboard, target, 0.9f, 900, failure + ": could not walk there.");
            var router = player.GetComponent<PlayerInteractionRouter>();
            router.RefreshPromptNow();
            Assert.IsTrue(router.PromptVisible, failure + ": no prompt there.");
            yield return PlayerFlowE2EDriver.PressInteractKey(keyboard);
            yield return null;
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
