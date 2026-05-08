using System.Collections;
using NUnit.Framework;
using Rootborn.Game.StudentLife;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode.TownConcept
{
    public sealed class TownStudentLifeInteractionTests
    {
        [UnityTest]
        public IEnumerator LIFE_STUDENT_PM_001_TownSceneInstallsSchoolActivityInteractors()
        {
            yield return SceneManager.LoadSceneAsync("Town", LoadSceneMode.Single);
            yield return WaitForStudentLifeRuntime();

            var player = GameObject.Find("Player");
            var schoolEntry = GameObject.Find("SchoolEntryActivity");
            var studyDesk = GameObject.Find("StudyBasicsActivity");

            Assert.IsNotNull(player, "Town scene should expose Player.");
            Assert.IsNotNull(player.GetComponent<StudentLifeProgressComponent>(), "Town Player should receive StudentLifeProgressComponent.");
            Assert.IsNotNull(schoolEntry, "Town scene should expose a school entry activity object.");
            Assert.IsNotNull(studyDesk, "Town scene should expose a study basics activity object.");
            Assert.IsNotNull(schoolEntry.GetComponent<StudentLifeActivityInteractor>(), "School entry should be interactable.");
            Assert.IsNotNull(studyDesk.GetComponent<StudentLifeActivityInteractor>(), "Study desk should be interactable.");
            Assert.AreEqual("activity.attend-school", schoolEntry.GetComponent<StudentLifeActivityInteractor>().Activity.Id);
            Assert.AreEqual("activity.study-basics", studyDesk.GetComponent<StudentLifeActivityInteractor>().Activity.Id);
        }

        [UnityTest]
        public IEnumerator LIFE_STUDENT_PM_002_InteractingWithTownStudyActivityChangesStudentProgress()
        {
            yield return SceneManager.LoadSceneAsync("Town", LoadSceneMode.Single);
            yield return WaitForStudentLifeRuntime();

            var playerProgress = GameObject.Find("Player").GetComponent<StudentLifeProgressComponent>();
            var schoolEntry = GameObject.Find("SchoolEntryActivity").GetComponent<StudentLifeActivityInteractor>();
            var studyDesk = GameObject.Find("StudyBasicsActivity").GetComponent<StudentLifeActivityInteractor>();
            int initialEnergy = playerProgress.Progress.Energy;
            int initialFocus = playerProgress.Progress.Focus;
            int initialTime = playerProgress.Progress.TimeMinutes;

            Assert.IsTrue(schoolEntry.Interact(playerProgress));
            Assert.IsTrue(studyDesk.Interact(playerProgress));
            yield return null;

            Assert.Less(playerProgress.Progress.Energy, initialEnergy);
            Assert.Less(playerProgress.Progress.Focus, initialFocus);
            Assert.Greater(playerProgress.Progress.TimeMinutes, initialTime);
            Assert.Greater(playerProgress.Progress.GetTraitValue(studyDesk.PrimaryTrait), 0);
            Assert.Greater(playerProgress.Progress.GetSkillValue(studyDesk.PrimarySkill), 0);
            Assert.IsTrue(playerProgress.Progress.IsCareerHintUnlocked(studyDesk.PrimaryCareer));
            Assert.AreEqual(LifeActivityResultKind.Applied, studyDesk.LastResult.Kind);
        }

        [UnityTest]
        public IEnumerator LIFE_STUDENT_PM_003_RepeatedSameSceneInteractionRequestDoesNotDoubleApply()
        {
            yield return SceneManager.LoadSceneAsync("Town", LoadSceneMode.Single);
            yield return WaitForStudentLifeRuntime();

            var playerProgress = GameObject.Find("Player").GetComponent<StudentLifeProgressComponent>();
            var studyDesk = GameObject.Find("StudyBasicsActivity").GetComponent<StudentLifeActivityInteractor>();

            Assert.IsTrue(studyDesk.Interact(playerProgress, "fixed-study-request"));
            int energyAfterFirst = playerProgress.Progress.Energy;
            int traitAfterFirst = playerProgress.Progress.GetTraitValue(studyDesk.PrimaryTrait);

            Assert.IsFalse(studyDesk.Interact(playerProgress, "fixed-study-request"));
            yield return null;

            Assert.AreEqual(LifeActivityResultKind.DuplicateRequest, studyDesk.LastResult.Kind);
            Assert.AreEqual(energyAfterFirst, playerProgress.Progress.Energy);
            Assert.AreEqual(traitAfterFirst, playerProgress.Progress.GetTraitValue(studyDesk.PrimaryTrait));
        }

        private static IEnumerator WaitForStudentLifeRuntime()
        {
            for (int i = 0; i < 240; i++)
            {
                var player = GameObject.Find("Player");
                var schoolEntry = GameObject.Find("SchoolEntryActivity");
                var studyDesk = GameObject.Find("StudyBasicsActivity");
                if (player != null &&
                    player.GetComponent<StudentLifeProgressComponent>() != null &&
                    schoolEntry != null && schoolEntry.GetComponent<StudentLifeActivityInteractor>() != null &&
                    studyDesk != null && studyDesk.GetComponent<StudentLifeActivityInteractor>() != null)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("Town StudentLife runtime did not install player progress and activity interactors within timeout.");
        }
    }
}
