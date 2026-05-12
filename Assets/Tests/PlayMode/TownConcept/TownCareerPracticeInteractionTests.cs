using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Game.Player;
using Rootborn.Game.StudentLife;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode.TownConcept
{
    public sealed class TownCareerPracticeInteractionTests
    {
        [UnityTest]
        public IEnumerator LIFE_CAREER_PRACTICE_PM_001_TownSceneInstallsCareerPracticeBoardWithFourPractices()
        {
            yield return SceneManager.LoadSceneAsync("Town", LoadSceneMode.Single);
            yield return WaitForCareerPracticeBoard();
            var board = Object.FindFirstObjectByType<CareerPracticeBoard>();

            Assert.IsNotNull(GameObject.Find("CareerPracticeBoard"));
            Assert.AreEqual(4, board.Practices.Count);
            AssertPractice(board.Practices[0], "practice.chef");
            AssertPractice(board.Practices[1], "practice.interior");
            AssertPractice(board.Practices[2], "practice.soldier");
            AssertPractice(board.Practices[3], "practice.emergency-care");
        }

        [UnityTest]
        public IEnumerator LIFE_CAREER_PRACTICE_PM_002_TownChefPracticeChangesPlayerProgress()
        {
            yield return AssertTownPracticeChangesProgress(0, "practice.chef", "career.chef");
        }

        [UnityTest]
        public IEnumerator LIFE_CAREER_PRACTICE_PM_003_TownInteriorPracticeChangesPlayerProgress()
        {
            yield return AssertTownPracticeChangesProgress(1, "practice.interior", "career.interior");
        }

        [UnityTest]
        public IEnumerator LIFE_CAREER_PRACTICE_PM_004_TownSoldierPracticeChangesPlayerProgress()
        {
            yield return AssertTownPracticeChangesProgress(2, "practice.soldier", "career.soldier");
        }

        [UnityTest]
        public IEnumerator LIFE_CAREER_PRACTICE_PM_005_TownEmergencyPracticeChangesPlayerProgress()
        {
            yield return AssertTownPracticeChangesProgress(3, "practice.emergency-care", "career.emergency-care");
        }

        [UnityTest]
        public IEnumerator LIFE_CAREER_PRACTICE_PM_006_TownCareerPracticesUnlockDifferentHints()
        {
            yield return SceneManager.LoadSceneAsync("Town", LoadSceneMode.Single);
            yield return WaitForCareerPracticeBoard();
            var board = Object.FindFirstObjectByType<CareerPracticeBoard>();
            var progress = GameObject.Find("Player").GetComponent<StudentLifeProgressComponent>();
            var hintIds = new HashSet<string>();

            for (int i = 0; i < board.Practices.Count; i++)
            {
                Assert.IsTrue(board.RunPractice(progress, i, "town-practice-" + i));
                CollectionAssert.Contains(board.UnlockedCareerHintIds, board.Practices[i].CareerHint.Id);
                Assert.IsTrue(hintIds.Add(board.Practices[i].CareerHint.Id));
            }

            Assert.AreEqual(4, hintIds.Count);
        }

        [UnityTest]
        public IEnumerator LIFE_CAREER_PRACTICE_PM_007_TownCareerBoardPromptAndEInteractionProgressAllFourRoutes()
        {
            yield return SceneManager.LoadSceneAsync("Town", LoadSceneMode.Single);
            yield return WaitForCareerPracticeBoard();

            var player = GameObject.Find("Player");
            var gather = player.GetComponent<GatherInteractor>();
            var router = player.GetComponent<PlayerInteractionRouter>();
            var board = Object.FindFirstObjectByType<CareerPracticeBoard>();
            var expectedHints = new HashSet<string>();
            for (int i = 0; i < board.Practices.Count; i++)
            {
                expectedHints.Add(board.Practices[i].CareerHint.Id);
            }

            player.transform.position = board.transform.position + new Vector3(0.35f, 0f, 0f);
            yield return null;
            router.RefreshPromptNow();

            Assert.IsTrue(router.PromptVisible);
            StringAssert.Contains("Career", router.PromptText);

            for (int i = 0; i < board.Practices.Count; i++)
            {
                gather.TriggerInteract();
                yield return null;
                Assert.AreEqual(LifeActivityResultKind.Applied, board.LastResultKind);
                CollectionAssert.Contains(expectedHints, board.LastCareerHintId);
            }

            foreach (var expectedHint in expectedHints)
            {
                CollectionAssert.Contains(board.UnlockedCareerHintIds, expectedHint);
            }
        }

        private static IEnumerator AssertTownPracticeChangesProgress(int practiceIndex, string expectedPracticeId, string expectedCareerId)
        {
            yield return SceneManager.LoadSceneAsync("Town", LoadSceneMode.Single);
            yield return WaitForCareerPracticeBoard();
            var board = Object.FindFirstObjectByType<CareerPracticeBoard>();
            var progress = GameObject.Find("Player").GetComponent<StudentLifeProgressComponent>();
            int initialEnergy = progress.Progress.Energy;
            int initialFocus = progress.Progress.Focus;
            int initialTime = progress.Progress.TimeMinutes;

            Assert.IsTrue(board.RunPractice(progress, practiceIndex, expectedPracticeId + ":request"));
            yield return null;

            Assert.AreEqual(expectedPracticeId, board.LastPracticeId);
            Assert.AreEqual(expectedPracticeId + ":request", board.LastRequestId);
            Assert.AreEqual(LifeActivityResultKind.Applied, board.LastResultKind);
            Assert.Less(progress.Progress.Energy, initialEnergy);
            Assert.Less(progress.Progress.Focus, initialFocus);
            Assert.Greater(progress.Progress.TimeMinutes, initialTime);
            CollectionAssert.Contains(board.UnlockedCareerHintIds, expectedCareerId);
        }

        private static void AssertPractice(CareerPracticeDefinition practice, string expectedId)
        {
            Assert.IsNotNull(practice);
            Assert.AreEqual(expectedId, practice.Id);
            Assert.IsNotNull(practice.Activity);
            Assert.IsNotNull(practice.CareerHint);
            Assert.GreaterOrEqual(practice.Steps.Count, 3);
        }

        private static IEnumerator WaitForCareerPracticeBoard()
        {
            for (int i = 0; i < 240; i++)
            {
                var player = GameObject.Find("Player");
                var board = Object.FindFirstObjectByType<CareerPracticeBoard>();
                if (player != null &&
                    player.GetComponent<StudentLifeProgressComponent>() != null &&
                    player.GetComponent<GatherInteractor>() != null &&
                    player.GetComponent<PlayerInteractionRouter>() != null &&
                    board != null &&
                    board.Practices.Count == 4)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("Town career practice board did not install within timeout.");
        }
    }
}