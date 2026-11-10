using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Tests
{
    // 옮겨 옴: Assets/Tests/EditMode/StudentLife/StudentLifeActivityTests.cs (LIFE_STUDENT_005),
    //          Assets/Tests/EditMode/StudentLife/StudentLifeCareerPracticeTests.cs (LIFE_CAREER_PRACTICE_001~007)
    public sealed class CareerPracticeTests
    {
        [Test]
        public void LIFE_STUDENT_005_CareerUnlocksFromTraitAndSkillRequirements()
        {
            var career = new Career("career.study-path", null, new ICareerUnlockRequirement[]
            {
                new TraitThresholdCareerRequirement("trait.diligence", 3), new SkillThresholdCareerRequirement("skill.basic-study", 5),
            });
            var progress = new StudentLifeProgress("slot-a", "player-1", energy: 10, focus: 10);

            Assert.IsFalse(career.IsUnlocked(progress));
            progress.AddTrait("trait.diligence", 3);
            progress.AddSkill("skill.basic-study", 5);

            Assert.IsTrue(career.IsUnlocked(progress));
        }

        [Test]
        public void Career_WithoutProgressOrWithNegativeThresholdBehavesLikeOriginal()
        {
            Assert.IsFalse(new Career("career.any", null, null).IsUnlocked(null));
            Assert.IsTrue(new Career("career.any", null, null).IsUnlocked(new StudentLifeProgress("s", "p", 0, 0)));
            Assert.IsTrue(new TraitThresholdCareerRequirement("trait.none", -5).IsSatisfied(new StudentLifeProgress("s", "p", 0, 0)));
            Assert.That(new Career("career.any", "", null).DisplayNameKey, Is.EqualTo("career.any"));
        }

        [Test]
        public void LIFE_CAREER_PRACTICE_001_FourCareerPracticeDefinitionsAreDataDriven()
        {
            var set = PracticeSet.Create();
            Assert.AreEqual(4, set.Practices.Length);
            foreach (var practice in set.Practices)
            {
                Assert.IsNotNull(practice.Activity);
                Assert.GreaterOrEqual(practice.Steps.Count, 3);
            }
        }

        [Test]
        public void LIFE_CAREER_PRACTICE_002_ChefPracticeRaisesCookingOrderingOrTimingSignals()
        {
            var progress = CreateProgress();
            AssertApplied(PracticeSet.Create().Chef, progress, "chef-request");
            Assert.Greater(progress.GetTraitValue("trait.focus"), 0);
            Assert.Greater(progress.GetSkillValue("skill.cooking"), 0);
            Assert.Greater(progress.GetSkillValue("skill.timing"), 0);
        }

        [Test]
        public void LIFE_CAREER_PRACTICE_003_InteriorPracticeRaisesSpaceCreativeOrPlanningSignals()
        {
            var progress = CreateProgress();
            AssertApplied(PracticeSet.Create().Interior, progress, "interior-request");
            Assert.Greater(progress.GetTraitValue("trait.creativity"), 0);
            Assert.Greater(progress.GetSkillValue("skill.space-layout"), 0);
            Assert.Greater(progress.GetSkillValue("skill.budget-sense"), 0);
        }

        [Test]
        public void LIFE_CAREER_PRACTICE_004_SoldierPracticeRaisesFitnessDisciplineOrResponsibilitySignals()
        {
            var progress = CreateProgress();
            AssertApplied(PracticeSet.Create().Soldier, progress, "soldier-request");
            Assert.Greater(progress.GetTraitValue("trait.fitness"), 0);
            Assert.Greater(progress.GetTraitValue("trait.discipline"), 0);
            Assert.Greater(progress.GetSkillValue("skill.teamwork"), 0);
        }

        [Test]
        public void LIFE_CAREER_PRACTICE_005_EmergencyPracticeRaisesCalmEmpathyOrObservationSignals()
        {
            var progress = CreateProgress();
            AssertApplied(PracticeSet.Create().Emergency, progress, "emergency-request");
            Assert.Greater(progress.GetTraitValue("trait.calm"), 0);
            Assert.Greater(progress.GetTraitValue("trait.empathy"), 0);
            Assert.Greater(progress.GetSkillValue("skill.first-aid"), 0);
        }

        [Test]
        public void LIFE_CAREER_PRACTICE_006_EachPracticeUnlocksDifferentCareerHint()
        {
            var set = PracticeSet.Create();
            var unlocked = new HashSet<string>();
            for (int i = 0; i < set.Practices.Length; i++)
            {
                var progress = CreateProgress();
                AssertApplied(set.Practices[i], progress, "practice-" + i);
                Assert.IsTrue(progress.IsCareerHintUnlocked(set.Practices[i].CareerHintId));
                Assert.IsTrue(unlocked.Add(set.Practices[i].CareerHintId));
            }
            Assert.AreEqual(4, unlocked.Count);
        }

        [Test]
        public void LIFE_CAREER_PRACTICE_007_SameRequestIdDoesNotApplyCareerPracticeTwice()
        {
            var chef = PracticeSet.Create().Chef;
            var progress = CreateProgress();
            var runner = new CareerPracticeRunner();

            Assert.IsTrue(runner.TryPerform(chef, progress, "repeat-request", out var first));
            int focusAfterFirst = progress.GetTraitValue("trait.focus");
            int energyAfterFirst = progress.Energy;
            Assert.IsFalse(runner.TryPerform(chef, progress, "repeat-request", out var second));

            Assert.AreEqual(LifeActivityResultKind.Applied, first.Kind);
            Assert.AreEqual(LifeActivityResultKind.DuplicateRequest, second.Kind);
            Assert.AreEqual(focusAfterFirst, progress.GetTraitValue("trait.focus"));
            Assert.AreEqual(energyAfterFirst, progress.Energy);
        }

        [Test]
        public void PracticeWithMinigameOrMissingPractice_IsRejectedWithoutChange()
        {
            var plain = PracticeSet.Create().Chef;
            var game = new Rootborn.Core.Minigames.Minigame("game", "game", null, null);
            var withGame = new CareerPractice("practice.game", null, plain.Activity, "career.chef", null, game);
            var progress = CreateProgress();
            var runner = new CareerPracticeRunner();

            Assert.IsFalse(runner.TryPerform(withGame, progress, "r1", out var gameResult));
            Assert.IsFalse(runner.TryPerform(null, progress, "r2", out var nullResult));

            Assert.AreEqual(LifeActivityResultKind.InvalidRequest, gameResult.Kind);
            Assert.AreEqual(string.Empty, gameResult.ActivityId);
            Assert.AreEqual(LifeActivityResultKind.InvalidRequest, nullResult.Kind);
            Assert.AreEqual(10, progress.Energy);
            Assert.IsFalse(progress.HasAppliedRequest("r1"));
        }

        private static StudentLifeProgress CreateProgress() => new StudentLifeProgress("slot-a", "player-a", energy: 10, focus: 10, stress: 0, timeMinutes: 8 * 60);

        private static void AssertApplied(CareerPractice practice, StudentLifeProgress progress, string requestId)
        {
            Assert.IsTrue(new CareerPracticeRunner().TryPerform(practice, progress, requestId, out var result));
            Assert.AreEqual(LifeActivityResultKind.Applied, result.Kind);
        }

        private sealed class PracticeSet
        {
            public CareerPractice[] Practices;
            public CareerPractice Chef, Interior, Soldier, Emergency;

            public static PracticeSet Create()
            {
                var set = new PracticeSet
                {
                    Chef = Practice("practice.chef", "career.chef", new TraitDeltaEffect("trait.focus", 2), new SkillProgressEffect("skill.cooking", 3), new SkillProgressEffect("skill.timing", 2)),
                    Interior = Practice("practice.interior", "career.interior", new TraitDeltaEffect("trait.creativity", 2), new SkillProgressEffect("skill.space-layout", 3), new SkillProgressEffect("skill.budget-sense", 2)),
                    Soldier = Practice("practice.soldier", "career.soldier", new TraitDeltaEffect("trait.fitness", 2), new TraitDeltaEffect("trait.discipline", 2), new SkillProgressEffect("skill.teamwork", 2)),
                    Emergency = Practice("practice.emergency-care", "career.emergency-care", new TraitDeltaEffect("trait.calm", 2), new TraitDeltaEffect("trait.empathy", 2), new SkillProgressEffect("skill.first-aid", 3)),
                };
                set.Practices = new[] { set.Chef, set.Interior, set.Soldier, set.Emergency };
                return set;
            }

            private static CareerPractice Practice(string id, string careerId, params ILifeActivityEffect[] effects)
            {
                var all = new List<ILifeActivityEffect>(effects) { new CareerHintUnlockEffect(careerId) };
                var activity = new LifeActivity(id, LifeActivityCategory.School, null, 45, 1, 1, 0, null, all.ToArray(), null);
                return new CareerPractice(id, id, activity, careerId, new[] { new PracticeStep(id + ".step-1"), new PracticeStep(id + ".step-2"), new PracticeStep(id + ".step-3") });
            }
        }
    }
}
