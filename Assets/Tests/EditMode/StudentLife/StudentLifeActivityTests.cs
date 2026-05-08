using NUnit.Framework;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class StudentLifeActivityTests
    {
        [Test]
        public void LIFE_STUDENT_002_AttendSchoolActivitySpendsTimeEnergyAndFocus()
        {
            var activity = ScriptableObject.CreateInstance<LifeActivityDefinition>();
            SetActivity(activity, "activity.attend-school", LifeActivityCategory.School, timeCostMinutes: 60, energyCost: 2, focusCost: 1, stressDelta: 1);
            var progress = new StudentLifeProgress("slot-a", "player-1", energy: 10, focus: 5, stress: 0, timeMinutes: 8 * 60);
            var runner = new LifeActivityRunner();

            Assert.IsTrue(runner.TryPerform(activity, progress, "request-1", out var result));

            Assert.AreEqual(LifeActivityResultKind.Applied, result.Kind);
            Assert.AreEqual("slot-a", result.SaveSlot);
            Assert.AreEqual("player-1", result.PlayerId);
            Assert.AreEqual("activity.attend-school", result.ActivityId);
            Assert.AreEqual(9 * 60, progress.TimeMinutes);
            Assert.AreEqual(8, progress.Energy);
            Assert.AreEqual(4, progress.Focus);
            Assert.AreEqual(1, progress.Stress);
        }

        [Test]
        public void LIFE_STUDENT_003_StudyActivityAppliesTraitSkillAndCareerHintEffects()
        {
            var diligence = CreateTrait("trait.diligence");
            var studySkill = CreateSkill("skill.basic-study");
            var career = CreateCareer("career.study-path");
            var traitEffect = ScriptableObject.CreateInstance<TraitDeltaActivityEffect>();
            traitEffect.ConfigureForTests(diligence, 3);
            var skillEffect = ScriptableObject.CreateInstance<SkillProgressActivityEffect>();
            skillEffect.ConfigureForTests(studySkill, 5);
            var careerEffect = ScriptableObject.CreateInstance<CareerHintUnlockActivityEffect>();
            careerEffect.ConfigureForTests(career);
            var activity = ScriptableObject.CreateInstance<LifeActivityDefinition>();
            SetActivity(activity, "activity.study-basics", LifeActivityCategory.School, effects: new LifeActivityEffectBase[] { traitEffect, skillEffect, careerEffect });
            var progress = new StudentLifeProgress("slot-a", "player-1", energy: 10, focus: 10);
            var runner = new LifeActivityRunner();

            Assert.IsTrue(runner.TryPerform(activity, progress, "request-1", out var result));

            Assert.AreEqual(LifeActivityResultKind.Applied, result.Kind);
            Assert.AreEqual(3, progress.GetTraitValue(diligence));
            Assert.AreEqual(5, progress.GetSkillValue(studySkill));
            Assert.IsTrue(progress.IsCareerHintUnlocked(career));
        }

        [Test]
        public void LIFE_STUDENT_004_ActivityUsesRequirementAndEffectStrategiesWithoutKindBranching()
        {
            var curiosity = CreateTrait("trait.curiosity");
            var traitRequirement = ScriptableObject.CreateInstance<TraitThresholdActivityRequirement>();
            traitRequirement.ConfigureForTests(curiosity, 2);
            var activity = ScriptableObject.CreateInstance<LifeActivityDefinition>();
            SetActivity(activity, "activity.self-study", LifeActivityCategory.SelfStudy, requirements: new LifeActivityRequirementBase[] { traitRequirement });
            var progress = new StudentLifeProgress("slot-a", "player-1", energy: 10, focus: 10);
            var runner = new LifeActivityRunner();

            Assert.IsFalse(runner.TryPerform(activity, progress, "request-1", out var rejected));
            Assert.AreEqual(LifeActivityResultKind.RequirementFailed, rejected.Kind);

            progress.AddTrait(curiosity, 2);
            Assert.IsTrue(runner.TryPerform(activity, progress, "request-2", out var applied));
            Assert.AreEqual(LifeActivityResultKind.Applied, applied.Kind);
        }

        [Test]
        public void LIFE_STUDENT_005_CareerUnlocksFromTraitAndSkillRequirements()
        {
            var diligence = CreateTrait("trait.diligence");
            var studySkill = CreateSkill("skill.basic-study");
            var traitRequirement = ScriptableObject.CreateInstance<TraitThresholdCareerRequirement>();
            traitRequirement.ConfigureForTests(diligence, 3);
            var skillRequirement = ScriptableObject.CreateInstance<SkillThresholdCareerRequirement>();
            skillRequirement.ConfigureForTests(studySkill, 5);
            var career = CreateCareer("career.study-path", traitRequirement, skillRequirement);
            var progress = new StudentLifeProgress("slot-a", "player-1", energy: 10, focus: 10);

            Assert.IsFalse(career.IsUnlocked(progress));
            progress.AddTrait(diligence, 3);
            progress.AddSkill(studySkill, 5);

            Assert.IsTrue(career.IsUnlocked(progress));
        }

        [Test]
        public void LIFE_STUDENT_006_SchoolAndNonSchoolActivitiesShareRunner()
        {
            var school = ScriptableObject.CreateInstance<LifeActivityDefinition>();
            SetActivity(school, "activity.attend-school", LifeActivityCategory.School, energyCost: 1);
            var work = ScriptableObject.CreateInstance<LifeActivityDefinition>();
            SetActivity(work, "activity.part-time-job", LifeActivityCategory.Work, energyCost: 1);
            var progress = new StudentLifeProgress("slot-a", "player-1", energy: 3, focus: 10);
            var runner = new LifeActivityRunner();

            Assert.IsTrue(runner.TryPerform(school, progress, "request-school", out _));
            Assert.IsTrue(runner.TryPerform(work, progress, "request-work", out _));

            Assert.AreEqual(1, progress.Energy);
        }

        [Test]
        public void LIFE_STUDENT_007_DuplicateActivityRequestDoesNotApplyTwice()
        {
            var diligence = CreateTrait("trait.diligence");
            var traitEffect = ScriptableObject.CreateInstance<TraitDeltaActivityEffect>();
            traitEffect.ConfigureForTests(diligence, 2);
            var activity = ScriptableObject.CreateInstance<LifeActivityDefinition>();
            SetActivity(activity, "activity.study-basics", LifeActivityCategory.School, energyCost: 1, effects: new LifeActivityEffectBase[] { traitEffect });
            var progress = new StudentLifeProgress("slot-a", "player-1", energy: 10, focus: 10);
            var runner = new LifeActivityRunner();

            Assert.IsTrue(runner.TryPerform(activity, progress, "same-request", out var first));
            Assert.IsFalse(runner.TryPerform(activity, progress, "same-request", out var second));

            Assert.AreEqual(LifeActivityResultKind.Applied, first.Kind);
            Assert.AreEqual(LifeActivityResultKind.DuplicateRequest, second.Kind);
            Assert.AreEqual(9, progress.Energy);
            Assert.AreEqual(2, progress.GetTraitValue(diligence));
        }

        [Test]
        public void LIFE_STUDENT_008_SaveLoadRestoresProgressForSameSlotAndPlayer()
        {
            var diligence = CreateTrait("trait.diligence");
            var studySkill = CreateSkill("skill.basic-study");
            var career = CreateCareer("career.study-path");
            var progress = new StudentLifeProgress("slot-a", "player-1", energy: 7, focus: 6, stress: 2, timeMinutes: 600);
            progress.AddTrait(diligence, 3);
            progress.AddSkill(studySkill, 5);
            progress.UnlockCareerHint(career);
            progress.MarkRequestApplied("request-1");

            var save = progress.ToSaveData();
            var loaded = StudentLifeProgress.FromSaveData(save, new[] { diligence }, new[] { studySkill }, new[] { career });
            var otherSlot = new StudentLifeProgress("slot-b", "player-1", energy: 7, focus: 6);

            Assert.AreEqual("slot-a", loaded.SaveSlot);
            Assert.AreEqual("player-1", loaded.PlayerId);
            Assert.AreEqual(7, loaded.Energy);
            Assert.AreEqual(6, loaded.Focus);
            Assert.AreEqual(2, loaded.Stress);
            Assert.AreEqual(600, loaded.TimeMinutes);
            Assert.AreEqual(3, loaded.GetTraitValue(diligence));
            Assert.AreEqual(5, loaded.GetSkillValue(studySkill));
            Assert.IsTrue(loaded.IsCareerHintUnlocked(career));
            Assert.IsTrue(loaded.HasAppliedRequest("request-1"));
            Assert.AreEqual(0, otherSlot.GetTraitValue(diligence));
            Assert.IsFalse(otherSlot.IsCareerHintUnlocked(career));
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

        private static CareerDefinition CreateCareer(string id, params CareerUnlockRequirementBase[] requirements)
        {
            var career = ScriptableObject.CreateInstance<CareerDefinition>();
            career.ConfigureForTests(id, id, requirements);
            return career;
        }

        private static void SetActivity(
            LifeActivityDefinition activity,
            string id,
            LifeActivityCategory category,
            int timeCostMinutes = 0,
            int energyCost = 0,
            int focusCost = 0,
            int stressDelta = 0,
            LifeActivityRequirementBase[] requirements = null,
            LifeActivityEffectBase[] effects = null)
        {
            activity.ConfigureForTests(
                id,
                id,
                category,
                timeCostMinutes,
                energyCost,
                focusCost,
                stressDelta,
                requirements,
                effects);
        }
    }
}
