using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Game.Player;
using Rootborn.Game.StudentLife;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode.TownConcept
{
    [SetUpFixture]
    internal sealed class InputSystemUiModuleGuardFixture
    {
        [OneTimeSetUp]
        public void OneTimeSetUp() => InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();

        [OneTimeTearDown]
        public void OneTimeTearDown() => InputSystemUiModulePlayModeTestGuard.UninstallForCurrentTest();
    }

    public sealed class ATownKeyboardInteractionPlayModeTests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator LIFE_STUDENT_PM_005_PlayerKeyboardWalksToSchoolAndEAppliesActivity()
        {
            yield return SceneManager.LoadSceneAsync("Town", LoadSceneMode.Single);
            yield return WaitForTownInteractionRuntime();

            var keyboard = InputSystem.AddDevice<Keyboard>();
            var player = GameObject.Find("Player");
            yield return RebindPlayerInputActions(player);

            var progress = player.GetComponent<StudentLifeProgressComponent>();
            var router = player.GetComponent<PlayerInteractionRouter>();
            var schoolEntry = GameObject.Find("SchoolEntryActivity").GetComponent<StudentLifeActivityInteractor>();
            Vector3 targetPosition = schoolEntry.transform.position;
            int initialTrait = progress.Progress.GetTraitValue(schoolEntry.PrimaryTrait);

            yield return WalkPlayerWithKeyboardTo(player, keyboard, targetPosition);
            schoolEntry = GameObject.Find("SchoolEntryActivity").GetComponent<StudentLifeActivityInteractor>();
            router.RefreshPromptNow();

            Assert.IsTrue(router.PromptVisible, "School prompt should be visible after walking into interaction range.");
            StringAssert.Contains("Attend School", router.PromptText);

            yield return PressInteractKey(keyboard);
            yield return null;

            Assert.AreEqual(LifeActivityResultKind.Applied, schoolEntry.LastResult.Kind);
            Assert.Greater(progress.Progress.GetTraitValue(schoolEntry.PrimaryTrait), initialTrait);
        }

        [UnityTest]
        public IEnumerator LIFE_STUDENT_PM_006_PlayerKeyboardWalksToStudyBasicsAndEAppliesActivity()
        {
            yield return SceneManager.LoadSceneAsync("Town", LoadSceneMode.Single);
            yield return WaitForTownInteractionRuntime();

            var keyboard = InputSystem.AddDevice<Keyboard>();
            var player = GameObject.Find("Player");
            yield return RebindPlayerInputActions(player);

            var progress = player.GetComponent<StudentLifeProgressComponent>();
            var router = player.GetComponent<PlayerInteractionRouter>();
            var studyDesk = GameObject.Find("StudyBasicsActivity").GetComponent<StudentLifeActivityInteractor>();
            Vector3 targetPosition = studyDesk.transform.position;
            int initialTrait = progress.Progress.GetTraitValue(studyDesk.PrimaryTrait);
            int initialSkill = progress.Progress.GetSkillValue(studyDesk.PrimarySkill);

            yield return WalkPlayerWithKeyboardTo(player, keyboard, targetPosition);
            studyDesk = GameObject.Find("StudyBasicsActivity").GetComponent<StudentLifeActivityInteractor>();
            router.RefreshPromptNow();

            Assert.IsTrue(router.PromptVisible, "Study prompt should be visible after walking into interaction range.");
            StringAssert.Contains("Study Basics", router.PromptText);

            yield return PressInteractKey(keyboard);
            yield return null;

            Assert.AreEqual(LifeActivityResultKind.Applied, studyDesk.LastResult.Kind);
            Assert.Greater(progress.Progress.GetTraitValue(studyDesk.PrimaryTrait), initialTrait);
            Assert.Greater(progress.Progress.GetSkillValue(studyDesk.PrimarySkill), initialSkill);
            Assert.IsTrue(progress.Progress.IsCareerHintUnlocked(studyDesk.PrimaryCareer));
        }

        [UnityTest]
        public IEnumerator LIFE_CAREER_PRACTICE_PM_008_PlayerKeyboardWalksToCareerBoardAndEProgressesAllRoutes()
        {
            yield return SceneManager.LoadSceneAsync("Town", LoadSceneMode.Single);
            yield return WaitForTownInteractionRuntime();

            var keyboard = InputSystem.AddDevice<Keyboard>();
            var player = GameObject.Find("Player");
            yield return RebindPlayerInputActions(player);

            var router = player.GetComponent<PlayerInteractionRouter>();
            var board = Object.FindFirstObjectByType<CareerPracticeBoard>();
            Vector3 targetPosition = board.transform.position;

            yield return WalkPlayerWithKeyboardTo(player, keyboard, targetPosition);
            board = Object.FindFirstObjectByType<CareerPracticeBoard>();
            Assert.IsNotNull(board, "Career practice board should remain in the Town scene while walking to it.");
            var expectedHints = new HashSet<string>();
            for (int i = 0; i < board.Practices.Count; i++)
            {
                expectedHints.Add(board.Practices[i].CareerHint.Id);
            }
            router.RefreshPromptNow();

            Assert.IsTrue(router.PromptVisible, "Career practice prompt should be visible after walking into interaction range.");
            StringAssert.Contains("Career Practice", router.PromptText);

            for (int i = 0; i < board.Practices.Count; i++)
            {
                yield return PressInteractKey(keyboard);
                yield return null;
                Assert.AreEqual(LifeActivityResultKind.Applied, board.LastResultKind);
                CollectionAssert.Contains(expectedHints, board.LastCareerHintId);
            }

            foreach (var expectedHint in expectedHints)
            {
                CollectionAssert.Contains(board.UnlockedCareerHintIds, expectedHint);
            }
        }

        private static IEnumerator WaitForTownInteractionRuntime()
        {
            for (int i = 0; i < 240; i++)
            {
                var player = GameObject.Find("Player");
                var schoolEntry = GameObject.Find("SchoolEntryActivity");
                var studyDesk = GameObject.Find("StudyBasicsActivity");
                var board = Object.FindFirstObjectByType<CareerPracticeBoard>();
                if (player != null &&
                    player.GetComponent<PlayerController>() != null &&
                    player.GetComponent<StudentLifeProgressComponent>() != null &&
                    player.GetComponent<GatherInteractor>() != null &&
                    player.GetComponent<PlayerInteractionRouter>() != null &&
                    schoolEntry != null && schoolEntry.GetComponent<StudentLifeActivityInteractor>() != null &&
                    studyDesk != null && studyDesk.GetComponent<StudentLifeActivityInteractor>() != null &&
                    board != null && board.Practices.Count == 4)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("Town interaction runtime did not install player, activities, and career board within timeout.");
        }

        private IEnumerator WalkPlayerWithKeyboardTo(GameObject player, Keyboard keyboard, Vector3 targetPosition)
        {
            var before = player.transform.position;
            yield return HoldKey(keyboard, Key.W, 24);
            yield return ReleaseKeyboard(keyboard, Key.W);

            Assert.Greater(
                Vector3.Distance(player.transform.position, before),
                0.05f,
                "Player must move through PlayerController keyboard input before interaction is attempted.");

            Key activeKey = Key.None;
            for (int i = 0; i < 300 && Vector3.Distance(player.transform.position, targetPosition) > 0.2f; i++)
            {
                Vector3 delta = targetPosition - player.transform.position;
                Key key = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
                    ? (delta.x >= 0f ? Key.D : Key.A)
                    : (delta.y >= 0f ? Key.W : Key.S);

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

            Assert.LessOrEqual(
                Vector3.Distance(player.transform.position, targetPosition),
                0.25f,
                "Player should be able to walk to the target interaction object using keyboard input.");
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

        private static IEnumerator RebindPlayerInputActions(GameObject player)
        {
            var controller = player.GetComponent<PlayerController>();
            var gather = player.GetComponent<GatherInteractor>();
            Assert.IsNotNull(controller);
            Assert.IsNotNull(gather);

            controller.enabled = false;
            gather.enabled = false;
            yield return null;
            controller.enabled = true;
            gather.enabled = true;
            yield return null;
        }

        private IEnumerator HoldKey(Keyboard keyboard, Key key, int frameCount)
        {
            var control = ControlFor(keyboard, key);
            Press(control);
            InputSystem.Update();
            for (int i = 0; i < frameCount; i++)
            {
                yield return null;
                yield return new WaitForFixedUpdate();
            }
        }

        private IEnumerator ReleaseKeyboard(Keyboard keyboard, Key key)
        {
            Release(ControlFor(keyboard, key));
            InputSystem.Update();
            yield return null;
            yield return new WaitForFixedUpdate();
        }

        private static KeyControl ControlFor(Keyboard keyboard, Key key)
        {
            if (key == Key.A) return keyboard.aKey;
            if (key == Key.D) return keyboard.dKey;
            if (key == Key.W) return keyboard.wKey;
            if (key == Key.S) return keyboard.sKey;
            if (key == Key.E) return keyboard.eKey;
            Assert.Fail("Unsupported keyboard key for town interaction test: " + key);
            return keyboard.eKey;
        }
    }
}
