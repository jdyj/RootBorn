using System;
using NUnit.Framework;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Tests
{
    // 옮겨 옴: Assets/Tests/EditMode/StudentLife/StudentLifeActivityTests.cs (LIFE_STUDENT_002~009, 005는 진로 묶음에서 옮긴다)
    public sealed class StudentLifeActivityTests
    {
        private static LifeActivity Activity(string id, LifeActivityCategory category, int timeCostMinutes = 0, int energyCost = 0, int focusCost = 0, int stressDelta = 0,
            ILifeActivityRequirement[] requirements = null, ILifeActivityEffect[] effects = null, LifeChoice[] choices = null, ActivityTimePolicy timePolicy = null)
            => new LifeActivity(id, category, timePolicy, timeCostMinutes, energyCost, focusCost, stressDelta, requirements, effects, choices);

        [Test]
        public void LIFE_STUDENT_002_AttendSchoolActivitySpendsTimeEnergyAndFocus()
        {
            var activity = Activity("activity.attend-school", LifeActivityCategory.School, timeCostMinutes: 60, energyCost: 2, focusCost: 1, stressDelta: 1);
            var progress = new StudentLifeProgress("slot-a", "player-1", energy: 10, focus: 5, stress: 0, timeMinutes: 8 * 60);
            var runner = new LifeActivityRunner();

            Assert.That(runner.TryPerform(activity, progress, "request-1", out var result), Is.True);

            Assert.That(result.Kind, Is.EqualTo(LifeActivityResultKind.Applied));
            Assert.That(result.SaveSlot, Is.EqualTo("slot-a"));
            Assert.That(result.PlayerId, Is.EqualTo("player-1"));
            Assert.That(result.ActivityId, Is.EqualTo("activity.attend-school"));
            Assert.That(progress.TimeMinutes, Is.EqualTo(9 * 60));
            Assert.That(progress.Energy, Is.EqualTo(8));
            Assert.That(progress.Focus, Is.EqualTo(4));
            Assert.That(progress.Stress, Is.EqualTo(1));
        }

        [Test]
        public void LIFE_STUDENT_003_StudyActivityAppliesTraitSkillAndCareerHintEffects()
        {
            var activity = Activity("activity.study-basics", LifeActivityCategory.School, effects: new ILifeActivityEffect[]
            {
                new TraitDeltaEffect("trait.diligence", 3), new SkillProgressEffect("skill.basic-study", 5), new CareerHintUnlockEffect("career.study-path"),
            });
            var progress = new StudentLifeProgress("slot-a", "player-1", energy: 10, focus: 10);

            Assert.That(new LifeActivityRunner().TryPerform(activity, progress, "request-1", out var result), Is.True);

            Assert.That(result.Kind, Is.EqualTo(LifeActivityResultKind.Applied));
            Assert.That(progress.GetTraitValue("trait.diligence"), Is.EqualTo(3));
            Assert.That(progress.GetSkillValue("skill.basic-study"), Is.EqualTo(5));
            Assert.That(progress.IsCareerHintUnlocked("career.study-path"), Is.True);
        }

        [Test]
        public void LIFE_STUDENT_004_ActivityUsesRequirementAndEffectStrategiesWithoutKindBranching()
        {
            var activity = Activity("activity.self-study", LifeActivityCategory.SelfStudy, requirements: new ILifeActivityRequirement[] { new TraitThresholdRequirement("trait.curiosity", 2) });
            var progress = new StudentLifeProgress("slot-a", "player-1", energy: 10, focus: 10);
            var runner = new LifeActivityRunner();

            Assert.That(runner.TryPerform(activity, progress, "request-1", out var rejected), Is.False);
            Assert.That(rejected.Kind, Is.EqualTo(LifeActivityResultKind.RequirementFailed));

            progress.AddTrait("trait.curiosity", 2);
            Assert.That(runner.TryPerform(activity, progress, "request-2", out var applied), Is.True);
            Assert.That(applied.Kind, Is.EqualTo(LifeActivityResultKind.Applied));
        }

        [Test]
        public void LIFE_STUDENT_006_SchoolAndNonSchoolActivitiesShareRunner()
        {
            var school = Activity("activity.attend-school", LifeActivityCategory.School, energyCost: 1);
            var work = Activity("activity.part-time-job", LifeActivityCategory.Work, energyCost: 1);
            var progress = new StudentLifeProgress("slot-a", "player-1", energy: 3, focus: 10);
            var runner = new LifeActivityRunner();

            Assert.That(runner.TryPerform(school, progress, "request-school", out _), Is.True);
            Assert.That(runner.TryPerform(work, progress, "request-work", out _), Is.True);
            Assert.That(progress.Energy, Is.EqualTo(1));
        }

        [Test]
        public void LIFE_STUDENT_007_DuplicateActivityRequestDoesNotApplyTwice()
        {
            var activity = Activity("activity.study-basics", LifeActivityCategory.School, energyCost: 1, effects: new ILifeActivityEffect[] { new TraitDeltaEffect("trait.diligence", 2) });
            var progress = new StudentLifeProgress("slot-a", "player-1", energy: 10, focus: 10);
            var runner = new LifeActivityRunner();

            Assert.That(runner.TryPerform(activity, progress, "same-request", out var first), Is.True);
            Assert.That(runner.TryPerform(activity, progress, "same-request", out var second), Is.False);

            Assert.That(first.Kind, Is.EqualTo(LifeActivityResultKind.Applied));
            Assert.That(second.Kind, Is.EqualTo(LifeActivityResultKind.DuplicateRequest));
            Assert.That(progress.Energy, Is.EqualTo(9));
            Assert.That(progress.GetTraitValue("trait.diligence"), Is.EqualTo(2));
        }

        [Test]
        public void LIFE_STUDENT_008_SaveLoadRestoresProgressForSameSlotAndPlayer()
        {
            var progress = new StudentLifeProgress("slot-a", "player-1", energy: 7, focus: 6, stress: 2, timeMinutes: 600);
            progress.AddTrait("trait.diligence", 3);
            progress.AddSkill("skill.basic-study", 5);
            progress.UnlockCareerHint("career.study-path");
            progress.MarkRequestApplied("request-1");

            var loaded = StudentLifeProgress.FromSaveData(progress.ToSaveData());
            var otherSlot = new StudentLifeProgress("slot-b", "player-1", energy: 7, focus: 6);

            Assert.That(loaded.SaveSlot, Is.EqualTo("slot-a"));
            Assert.That(loaded.PlayerId, Is.EqualTo("player-1"));
            Assert.That(loaded.Energy, Is.EqualTo(7));
            Assert.That(loaded.Focus, Is.EqualTo(6));
            Assert.That(loaded.Stress, Is.EqualTo(2));
            Assert.That(loaded.TimeMinutes, Is.EqualTo(600));
            Assert.That(loaded.GetTraitValue("trait.diligence"), Is.EqualTo(3));
            Assert.That(loaded.GetSkillValue("skill.basic-study"), Is.EqualTo(5));
            Assert.That(loaded.IsCareerHintUnlocked("career.study-path"), Is.True);
            Assert.That(loaded.HasAppliedRequest("request-1"), Is.True);
            Assert.That(otherSlot.GetTraitValue("trait.diligence"), Is.EqualTo(0));
            Assert.That(otherSlot.IsCareerHintUnlocked("career.study-path"), Is.False);
        }

        [Test]
        public void LIFE_STUDENT_009_SaveLoadRecordsLastActivityAndProgressLogWithoutCodeBuiltStage()
        {
            var school = Activity("activity.attend-school", LifeActivityCategory.School);
            var study = Activity("activity.study-basics", LifeActivityCategory.SelfStudy);
            var progress = new StudentLifeProgress("slot-a", "player-1", energy: 10, focus: 10);
            var runner = new LifeActivityRunner();

            Assert.That(runner.TryPerform(school, progress, "request-school", out _), Is.True);
            Assert.That(runner.TryPerform(study, progress, "request-study", out _), Is.True);

            var save = progress.ToSaveData();
            Assert.That(save.LastActivityId, Is.EqualTo("activity.study-basics"));
            Assert.That(save.TutorialStageId, Is.EqualTo(string.Empty));
            Assert.That(save.ActivityLogIds, Is.EqualTo(new[] { "activity.attend-school", "activity.study-basics" }));

            var loaded = StudentLifeProgress.FromSaveData(save);
            Assert.That(loaded.LastActivityId, Is.EqualTo("activity.study-basics"));
            Assert.That(loaded.GetActivityLogIds(), Is.EqualTo(new[] { "activity.attend-school", "activity.study-basics" }));
        }

        // --- 원래 테스트에 없던 경우 ---

        [Test]
        public void InsufficientEnergy_ChangesNothing()
        {
            var activity = Activity("activity.heavy", LifeActivityCategory.Work, timeCostMinutes: 30, energyCost: 5, effects: new ILifeActivityEffect[] { new TraitDeltaEffect("trait.grit", 1) });
            var progress = new StudentLifeProgress("slot-a", "player-1", energy: 4, focus: 10, stress: 0, timeMinutes: 100);

            Assert.That(new LifeActivityRunner().TryPerform(activity, progress, "request-1", out var result), Is.False);

            Assert.That(result.Kind, Is.EqualTo(LifeActivityResultKind.InsufficientResources));
            Assert.That(progress.Energy, Is.EqualTo(4));
            Assert.That(progress.TimeMinutes, Is.EqualTo(100));
            Assert.That(progress.GetTraitValue("trait.grit"), Is.EqualTo(0));
            Assert.That(progress.HasAppliedRequest("request-1"), Is.False, "거절된 요청은 다시 시도할 수 있어야 한다");
            Assert.That(progress.GetActivityLogIds(), Is.Empty);
        }

        [Test]
        public void MissingActivityProgressOrRequestId_IsInvalidRequest()
        {
            var activity = Activity("activity.any", LifeActivityCategory.Rest);
            var progress = new StudentLifeProgress("slot-a", "player-1", 1, 1);
            var runner = new LifeActivityRunner();

            Assert.That(runner.TryPerform(null, progress, "r", out var noActivity), Is.False);
            Assert.That(noActivity.Kind, Is.EqualTo(LifeActivityResultKind.InvalidRequest));
            Assert.That(runner.TryPerform(activity, null, "r", out var noProgress), Is.False);
            Assert.That(noProgress.SaveSlot, Is.EqualTo("default"));
            Assert.That(noProgress.PlayerId, Is.EqualTo("player"));
            Assert.That(runner.TryPerform(activity, progress, "", out var noRequest), Is.False);
            Assert.That(noRequest.Kind, Is.EqualTo(LifeActivityResultKind.InvalidRequest));
        }

        [Test]
        public void Choice_AppliesActivityThenChoiceEffects_AndReportsChangedTraits()
        {
            var choice = new LifeChoice("choice.help", new ILifeActivityEffect[] { new TraitDeltaEffect("trait.kindness", 2), new RelationshipDeltaEffect("relationship.neighbor", 1) });
            var activity = Activity("activity.errand", LifeActivityCategory.Errand, effects: new ILifeActivityEffect[] { new StatusDeltaEffect("status.tired", 1) }, choices: new[] { choice });
            var progress = new StudentLifeProgress("slot-a", "player-1", 5, 5);

            Assert.That(new LifeActivityRunner().TryPerformChoice(activity, activity.GetChoice(0), progress, "request-1", out var result), Is.True);

            Assert.That(result.ChoiceId, Is.EqualTo("choice.help"));
            Assert.That(result.ChangedTraitIds, Is.EqualTo(new[] { "trait.kindness" }));
            Assert.That(progress.GetRelationshipValue("relationship.neighbor"), Is.EqualTo(1));
            Assert.That(progress.GetStatusValue("status.tired"), Is.EqualTo(1));
            Assert.That(progress.GetTodayResultLogIds(), Is.EqualTo(new[]
            {
                "activity.errand:+trait.kindness=2", "activity.errand:+relationship.neighbor=1", "activity.errand:+status.tired=1",
            }));
            Assert.That(activity.GetChoice(5), Is.Null);
        }

        [Test]
        public void TimePolicy_SpendsDailyTimePoints_AndOncePerDayBlocksTheSecondUse()
        {
            var policy = new ActivityTimePolicy("policy.library", timePointCost: 1, allowedDaysMask: 127, oncePerDay: true);
            var activity = Activity("activity.library", LifeActivityCategory.SelfStudy, timePolicy: policy);
            var progress = new StudentLifeProgress("slot-a", "player-1", 5, 5);
            progress.ConfigureCalendar(new LifeCalendar()); // 평일 2, 주말 3
            var runner = new LifeActivityRunner();

            Assert.That(progress.TimePointsRemaining, Is.EqualTo(2));
            Assert.That(runner.TryPerform(activity, progress, "r1", out _), Is.True);
            Assert.That(progress.TimePointsRemaining, Is.EqualTo(1));
            Assert.That(runner.TryPerform(activity, progress, "r2", out var second), Is.False);
            Assert.That(second.Kind, Is.EqualTo(LifeActivityResultKind.InsufficientResources));
        }

        [Test]
        public void TimePolicy_WithoutCalendar_IsNotABudget()
        {
            // 흐르는 시간(2026-11-12): 시간 포인트는 한도가 아니다. 달력이 없어도 활동은 된다(막는 것은 체력·지침·시각).
            var activity = Activity("activity.library", LifeActivityCategory.SelfStudy, timePolicy: new ActivityTimePolicy("policy.library", 1));
            var progress = new StudentLifeProgress("slot-a", "player-1", 5, 5);
            Assert.That(new LifeActivityRunner().TryPerform(activity, progress, "r1", out var result), Is.True);
            Assert.That(result.Kind, Is.EqualTo(LifeActivityResultKind.Applied));
        }

        [Test]
        public void Calendar_WeekendsAreDaySixAndSevenOfEachWeek()
        {
            var calendar = new LifeCalendar();
            Assert.That(calendar.GetBudget(1), Is.EqualTo(2));
            Assert.That(calendar.GetBudget(5), Is.EqualTo(2));
            Assert.That(calendar.GetBudget(6), Is.EqualTo(3));
            Assert.That(calendar.GetBudget(7), Is.EqualTo(3));
            Assert.That(calendar.GetBudget(8), Is.EqualTo(2));
            Assert.That(calendar.GetBudget(0), Is.EqualTo(2), "1보다 작은 날은 1일로 본다");
            var weekdaysOnly = new ActivityTimePolicy("policy.school", 1, allowedDaysMask: 31);
            Assert.That(weekdaysOnly.IsAllowedDay(5), Is.True);
            Assert.That(weekdaysOnly.IsAllowedDay(6), Is.False);
        }

        [Test]
        public void StatValues_NeverGoBelowZero_AndEmptyIdsAreIgnored()
        {
            var progress = new StudentLifeProgress("slot-a", "player-1", 5, 5);
            progress.AddTrait("trait.a", 2);
            progress.AddTrait("trait.a", -5);
            progress.AddTrait("", 3);
            progress.AddTrait(null, 3);
            Assert.That(progress.GetTraitValue("trait.a"), Is.EqualTo(0));
            Assert.That(progress.GetTraitIds(), Is.EqualTo(new[] { "trait.a" }));
            progress.UnlockCareerHint("");
            Assert.That(progress.GetCareerHintIds(), Is.Empty);
        }

        [Test]
        public void Constructor_UsesDefaultsForEmptyIdentity_AndClampsNegatives()
        {
            var progress = new StudentLifeProgress("", null, -1, -2, -3, -4);
            Assert.That(progress.SaveSlot, Is.EqualTo("default"));
            Assert.That(progress.PlayerId, Is.EqualTo("player"));
            Assert.That(new[] { progress.Energy, progress.Focus, progress.Stress, progress.TimeMinutes }, Is.EqualTo(new[] { 0, 0, 0, 0 }));
            Assert.That(progress.CurrentDay, Is.EqualTo(1));
            Assert.That(StudentLifeProgress.FromSaveData(null).SaveSlot, Is.EqualTo("default"));
        }
    }
}
