using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class StudentLifeCareerPracticeTests
    {
        [Test]
        public void LIFE_CAREER_PRACTICE_001_FourCareerPracticeDefinitionsAreDataDrivenScriptableObjects()
        {
            var set = CareerPracticeTestSet.Create();

            Assert.AreEqual(4, set.Practices.Length);
            for (int i = 0; i < set.Practices.Length; i++)
            {
                Assert.IsInstanceOf<ScriptableObject>(set.Practices[i]);
                Assert.IsNotNull(set.Practices[i].Activity);
                Assert.GreaterOrEqual(set.Practices[i].Steps.Count, 3);
            }
        }

        [Test]
        public void LIFE_CAREER_PRACTICE_002_ChefPracticeRaisesCookingOrderingOrTimingSignals()
        {
            var set = CareerPracticeTestSet.Create();
            var progress = CreateProgress();

            AssertApplied(set.Chef, progress, "chef-request");

            Assert.Greater(progress.GetTraitValue(set.FocusTrait), 0);
            Assert.Greater(progress.GetSkillValue(set.CookingSkill), 0);
            Assert.Greater(progress.GetSkillValue(set.TimingSkill), 0);
        }

        [Test]
        public void LIFE_CAREER_PRACTICE_003_InteriorPracticeRaisesSpaceCreativeOrPlanningSignals()
        {
            var set = CareerPracticeTestSet.Create();
            var progress = CreateProgress();

            AssertApplied(set.Interior, progress, "interior-request");

            Assert.Greater(progress.GetTraitValue(set.CreativityTrait), 0);
            Assert.Greater(progress.GetSkillValue(set.SpaceLayoutSkill), 0);
            Assert.Greater(progress.GetSkillValue(set.BudgetSenseSkill), 0);
        }

        [Test]
        public void LIFE_CAREER_PRACTICE_004_SoldierPracticeRaisesFitnessDisciplineOrResponsibilitySignals()
        {
            var set = CareerPracticeTestSet.Create();
            var progress = CreateProgress();

            AssertApplied(set.Soldier, progress, "soldier-request");

            Assert.Greater(progress.GetTraitValue(set.FitnessTrait), 0);
            Assert.Greater(progress.GetTraitValue(set.DisciplineTrait), 0);
            Assert.Greater(progress.GetSkillValue(set.TeamworkSkill), 0);
        }

        [Test]
        public void LIFE_CAREER_PRACTICE_005_EmergencyPracticeRaisesCalmEmpathyOrObservationSignals()
        {
            var set = CareerPracticeTestSet.Create();
            var progress = CreateProgress();

            AssertApplied(set.Emergency, progress, "emergency-request");

            Assert.Greater(progress.GetTraitValue(set.CalmTrait), 0);
            Assert.Greater(progress.GetTraitValue(set.EmpathyTrait), 0);
            Assert.Greater(progress.GetSkillValue(set.FirstAidSkill), 0);
        }

        [Test]
        public void LIFE_CAREER_PRACTICE_006_EachPracticeUnlocksDifferentCareerHint()
        {
            var set = CareerPracticeTestSet.Create();
            var unlocked = new HashSet<string>();

            for (int i = 0; i < set.Practices.Length; i++)
            {
                var progress = CreateProgress();
                AssertApplied(set.Practices[i], progress, "practice-" + i);
                Assert.IsTrue(progress.IsCareerHintUnlocked(set.Practices[i].CareerHint));
                Assert.IsTrue(unlocked.Add(set.Practices[i].CareerHint.Id));
            }

            Assert.AreEqual(4, unlocked.Count);
        }

        [Test]
        public void LIFE_CAREER_PRACTICE_007_SameRequestIdDoesNotApplyCareerPracticeTwice()
        {
            var set = CareerPracticeTestSet.Create();
            var progress = CreateProgress();
            var runner = new CareerPracticeRunner();

            Assert.IsTrue(runner.TryPerform(set.Chef, progress, "repeat-request", out var first));
            int focusAfterFirst = progress.GetTraitValue(set.FocusTrait);
            int energyAfterFirst = progress.Energy;

            Assert.IsFalse(runner.TryPerform(set.Chef, progress, "repeat-request", out var second));

            Assert.AreEqual(LifeActivityResultKind.Applied, first.Kind);
            Assert.AreEqual(LifeActivityResultKind.DuplicateRequest, second.Kind);
            Assert.AreEqual(focusAfterFirst, progress.GetTraitValue(set.FocusTrait));
            Assert.AreEqual(energyAfterFirst, progress.Energy);
        }

        [Test]
        public void LIFE_CAREER_PRACTICE_008_BoardExecutionUsesPracticeDataWithoutCareerOrPracticeBranching()
        {
            var set = CareerPracticeTestSet.Create();
            var player = new GameObject("Player");
            var progressComponent = player.AddComponent<StudentLifeProgressComponent>();
            progressComponent.ConfigureForTests("slot-a", "player-1", 20, 20, 0, 480);
            var boardObject = new GameObject("CareerPracticeBoard");
            var board = boardObject.AddComponent<CareerPracticeBoard>();
            board.Bind(set.Practices);

            Assert.IsTrue(board.RunPractice(progressComponent, 0, "board-chef-request"));

            Assert.AreEqual("practice.chef", board.LastPracticeId);
            Assert.AreEqual("board-chef-request", board.LastRequestId);
            Assert.AreEqual(LifeActivityResultKind.Applied, board.LastResultKind);
            CollectionAssert.Contains(board.UnlockedCareerHintIds, "career.chef");

            Object.DestroyImmediate(player);
            Object.DestroyImmediate(boardObject);
        }

        private static StudentLifeProgress CreateProgress()
        {
            return new StudentLifeProgress("slot-a", "player-1", energy: 20, focus: 20, stress: 0, timeMinutes: 8 * 60);
        }

        private static void AssertApplied(CareerPracticeDefinition practice, StudentLifeProgress progress, string requestId)
        {
            var runner = new CareerPracticeRunner();
            Assert.IsTrue(runner.TryPerform(practice, progress, requestId, out var result));
            Assert.AreEqual(LifeActivityResultKind.Applied, result.Kind);
        }

        private sealed class CareerPracticeTestSet
        {
            public CareerPracticeDefinition[] Practices;
            public CareerPracticeDefinition Chef;
            public CareerPracticeDefinition Interior;
            public CareerPracticeDefinition Soldier;
            public CareerPracticeDefinition Emergency;
            public TraitDefinition FocusTrait;
            public TraitDefinition CreativityTrait;
            public TraitDefinition FitnessTrait;
            public TraitDefinition DisciplineTrait;
            public TraitDefinition CalmTrait;
            public TraitDefinition EmpathyTrait;
            public SkillDefinition CookingSkill;
            public SkillDefinition TimingSkill;
            public SkillDefinition SpaceLayoutSkill;
            public SkillDefinition BudgetSenseSkill;
            public SkillDefinition TeamworkSkill;
            public SkillDefinition FirstAidSkill;

            public static CareerPracticeTestSet Create()
            {
                var set = new CareerPracticeTestSet
                {
                    FocusTrait = CreateTrait("trait.focus"),
                    CreativityTrait = CreateTrait("trait.creativity"),
                    FitnessTrait = CreateTrait("trait.fitness"),
                    DisciplineTrait = CreateTrait("trait.discipline"),
                    CalmTrait = CreateTrait("trait.calm"),
                    EmpathyTrait = CreateTrait("trait.empathy"),
                    CookingSkill = CreateSkill("skill.cooking"),
                    TimingSkill = CreateSkill("skill.timing"),
                    SpaceLayoutSkill = CreateSkill("skill.space-layout"),
                    BudgetSenseSkill = CreateSkill("skill.budget-sense"),
                    TeamworkSkill = CreateSkill("skill.teamwork"),
                    FirstAidSkill = CreateSkill("skill.first-aid"),
                };

                set.Chef = CreatePractice("practice.chef", CreateCareer("career.chef"),
                    new LifeActivityEffectBase[] { Trait(set.FocusTrait, 2), Skill(set.CookingSkill, 3), Skill(set.TimingSkill, 2) });
                set.Interior = CreatePractice("practice.interior", CreateCareer("career.interior"),
                    new LifeActivityEffectBase[] { Trait(set.CreativityTrait, 2), Skill(set.SpaceLayoutSkill, 3), Skill(set.BudgetSenseSkill, 2) });
                set.Soldier = CreatePractice("practice.soldier", CreateCareer("career.soldier"),
                    new LifeActivityEffectBase[] { Trait(set.FitnessTrait, 2), Trait(set.DisciplineTrait, 2), Skill(set.TeamworkSkill, 2) });
                set.Emergency = CreatePractice("practice.emergency-care", CreateCareer("career.emergency-care"),
                    new LifeActivityEffectBase[] { Trait(set.CalmTrait, 2), Trait(set.EmpathyTrait, 2), Skill(set.FirstAidSkill, 3) });
                set.Practices = new[] { set.Chef, set.Interior, set.Soldier, set.Emergency };
                return set;
            }

            private static CareerPracticeDefinition CreatePractice(string id, CareerDefinition career, LifeActivityEffectBase[] effects)
            {
                var careerEffect = ScriptableObject.CreateInstance<CareerHintUnlockActivityEffect>();
                careerEffect.ConfigureForTests(career);
                var allEffects = new LifeActivityEffectBase[effects.Length + 1];
                effects.CopyTo(allEffects, 0);
                allEffects[allEffects.Length - 1] = careerEffect;

                var activity = ScriptableObject.CreateInstance<LifeActivityDefinition>();
                activity.ConfigureForTests(id, id, LifeActivityCategory.School, 45, 1, 1, 0, null, allEffects);
                var practice = ScriptableObject.CreateInstance<CareerPracticeDefinition>();
                practice.ConfigureForTests(id, id, activity, career, new[]
                {
                    CreateStep(id + ".step-1"),
                    CreateStep(id + ".step-2"),
                    CreateStep(id + ".step-3"),
                });
                return practice;
            }

            private static PracticeStepDefinition CreateStep(string id)
            {
                var step = ScriptableObject.CreateInstance<PracticeStepDefinition>();
                step.ConfigureForTests(id, id);
                return step;
            }

            private static TraitDeltaActivityEffect Trait(TraitDefinition trait, int delta)
            {
                var effect = ScriptableObject.CreateInstance<TraitDeltaActivityEffect>();
                effect.ConfigureForTests(trait, delta);
                return effect;
            }

            private static SkillProgressActivityEffect Skill(SkillDefinition skill, int delta)
            {
                var effect = ScriptableObject.CreateInstance<SkillProgressActivityEffect>();
                effect.ConfigureForTests(skill, delta);
                return effect;
            }

            private static TraitDefinition CreateTrait(string id)
            {
                var trait = ScriptableObject.CreateInstance<TraitDefinition>();
                trait.ConfigureForTests(id, id);
                return trait;
            }

            private static SkillDefinition CreateSkill(string id)
            {
                var skill = ScriptableObject.CreateInstance<SkillDefinition>();
                skill.ConfigureForTests(id, id);
                return skill;
            }

            private static CareerDefinition CreateCareer(string id)
            {
                var career = ScriptableObject.CreateInstance<CareerDefinition>();
                career.ConfigureForTests(id, id, null);
                return career;
            }
        }
    }
}
