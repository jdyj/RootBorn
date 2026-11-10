using NUnit.Framework;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Tests
{
    // 옮겨 옴: Assets/Tests/EditMode/StudentLife/StudentDayProgressTests.cs (STUDENT_DAY_001~003, 005).
    // 004·006·007은 Unity 등록부·씬 설치 검사라 옮기지 않는다. 기본 규칙 값(체력 12, 집중 10, home-entry)은 Unity 기본 에셋의 값이다.
    public sealed class StudentDayProgressTests
    {
        private static DayEndRuleSet DefaultRules() => new DayEndRuleSet("day-end.default", "home-entry", new IDayEndRule[] { new EnergyRestoreRule(12), new FocusRestoreRule(10) });

        [Test]
        public void STUDENT_DAY_001_PerformedActivityRecordsTodayLogAndSaveFields()
        {
            var activity = new LifeActivity("activity.study-basics", LifeActivityCategory.School, null, 45, 1, 1, 0, null, new ILifeActivityEffect[] { new TraitDeltaEffect("trait.diligence", 1) }, null);
            var progress = new StudentLifeProgress("slot-day", "player-day", 8, 8);

            Assert.That(new LifeActivityRunner().TryPerform(activity, progress, "study-request-1", out var result), Is.True);

            Assert.That(result.Kind, Is.EqualTo(LifeActivityResultKind.Applied));
            Assert.That(progress.CurrentDay, Is.EqualTo(1));
            Assert.That(progress.DayState, Is.EqualTo(StudentDayState.InProgress));
            Assert.That(progress.GetTodayActivityIds(), Does.Contain("activity.study-basics"));
            Assert.That(progress.GetTodayResultLogIds(), Does.Contain("activity.study-basics:+trait.diligence=1"));
            var saveData = progress.ToSaveData();
            Assert.That(saveData.CurrentDay, Is.EqualTo(1));
            Assert.That(saveData.DayState, Is.EqualTo("InProgress"));
            Assert.That(saveData.TodayActivityIds, Does.Contain("activity.study-basics"));
            Assert.That(saveData.TodayResultLogIds, Does.Contain("activity.study-basics:+trait.diligence=1"));
        }

        [Test]
        public void STUDENT_DAY_002_EndDayAppliesRulesOnceAndPersistsResultReadyState()
        {
            var rules = DefaultRules();
            var progress = new StudentLifeProgress("slot-day", "player-day", 4, 3, 2, 17 * 60);
            progress.RecordActivityCompleted("activity.study-basics", new[] { "activity.study-basics:+skill.basic-study=3" });

            Assert.That(progress.TryEndDay(rules, out var summary), Is.True);
            Assert.That(progress.DayState, Is.EqualTo(StudentDayState.ResultReady));
            Assert.That(summary.DayNumber, Is.EqualTo(1));
            Assert.That(progress.NextDayEntryPointId, Is.EqualTo("home-entry"));
            Assert.That(progress.Energy, Is.EqualTo(12));
            Assert.That(progress.Focus, Is.EqualTo(10));
            Assert.That(summary.CompletedActivityIds, Does.Contain("activity.study-basics"));
            Assert.That(summary.ResultLogIds, Does.Contain("activity.study-basics:+skill.basic-study=3"));

            Assert.That(progress.TryEndDay(rules, out var duplicateSummary), Is.False);
            Assert.That(duplicateSummary.DayNumber, Is.EqualTo(1));
            Assert.That(progress.CurrentDay, Is.EqualTo(1));
            Assert.That(progress.DayState, Is.EqualTo(StudentDayState.ResultReady));
            Assert.That(progress.GetPreviousDayActivityIds().Length, Is.EqualTo(1));

            var saveData = progress.ToSaveData();
            Assert.That(saveData.DayState, Is.EqualTo("ResultReady"));
            Assert.That(saveData.LastSettledDay, Is.EqualTo(1));
            Assert.That(saveData.PreviousDayActivityIds, Does.Contain("activity.study-basics"));
            Assert.That(saveData.PreviousDayResultLogIds, Does.Contain("activity.study-basics:+skill.basic-study=3"));
        }

        [Test]
        public void STUDENT_DAY_003_NextDayAdvancesDayClearsTodayAndKeepsCumulativeGrowth()
        {
            var progress = new StudentLifeProgress("slot-day", "player-day", 4, 3);
            progress.ConfigureCalendar(new LifeCalendar());
            progress.AddTrait("trait.diligence", 2);
            progress.TrySpendTimePoints(2);
            progress.RecordActivityCompleted("activity.study-basics", new[] { "activity.study-basics:+trait.diligence=2" });
            Assert.That(progress.TryStartNextDay(), Is.False, "하루를 끝내기 전에는 다음 날로 못 간다");
            Assert.That(progress.TryEndDay(DefaultRules(), out _), Is.True);

            Assert.That(progress.TryStartNextDay(), Is.True);

            Assert.That(progress.CurrentDay, Is.EqualTo(2));
            Assert.That(progress.DayState, Is.EqualTo(StudentDayState.InProgress));
            Assert.That(progress.TimeMinutes, Is.EqualTo(8 * 60));
            Assert.That(progress.TimePointsSpentToday, Is.EqualTo(0));
            Assert.That(progress.GetTodayActivityIds(), Is.Empty);
            Assert.That(progress.GetTraitValue("trait.diligence"), Is.EqualTo(2));
            Assert.That(progress.GetPreviousDayActivityIds(), Does.Contain("activity.study-basics"));
            Assert.That(progress.TryStartNextDay(), Is.False, "이미 진행 중이면 다시 넘어가지 않는다");
        }

        [Test]
        public void STUDENT_DAY_005_RuleValuesAndEntryPointComeFromData()
        {
            var rules = new DayEndRuleSet("day-end.tuned", "dorm-entry", new IDayEndRule[] { new EnergyRestoreRule(7), new FocusRestoreRule(4) });
            var progress = new StudentLifeProgress("slot-day", "player-day", 1, 1);

            Assert.That(progress.TryEndDay(rules, out var summary), Is.True);

            Assert.That(progress.Energy, Is.EqualTo(7));
            Assert.That(progress.Focus, Is.EqualTo(4));
            Assert.That(summary.NextDayEntryPointId, Is.EqualTo("dorm-entry"));
        }

        [Test]
        public void EndDayWithoutRules_StillSettlesTheDay()
        {
            var progress = new StudentLifeProgress("slot-day", "player-day", 1, 1);
            Assert.That(progress.TryEndDay(null, out _), Is.True);
            Assert.That(progress.NextDayEntryPointId, Is.EqualTo(string.Empty));
            Assert.That(progress.Energy, Is.EqualTo(1));
        }

        [Test]
        public void TutorialStageAdvanceRule_MovesOnlyFromTheExpectedStage()
        {
            var target = new TutorialStage("stage.day2", "둘째 날 안내");
            var rules = new DayEndRuleSet("day-end.tutorial", "home-entry", new IDayEndRule[] { new TutorialStageAdvanceRule("stage.day1", target, "objective.go-school") });
            var elsewhere = new StudentLifeProgress("slot", "p", 1, 1);
            elsewhere.SetTutorialStageForTests("stage.other");
            Assert.That(elsewhere.TryEndDay(rules, out _), Is.True);
            Assert.That(elsewhere.TutorialStageId, Is.EqualTo("stage.other"));

            var onDay1 = new StudentLifeProgress("slot", "p", 1, 1);
            onDay1.SetTutorialStageForTests("stage.day1");
            Assert.That(onDay1.TryEndDay(rules, out var summary), Is.True);
            Assert.That(onDay1.TutorialStageId, Is.EqualTo("stage.day2"));
            Assert.That(summary.NextObjectiveId, Is.EqualTo("objective.go-school"));
            Assert.That(summary.NextGuideText, Is.EqualTo("둘째 날 안내"));
        }

        [Test]
        public void PreviousDayDeltas_AreFilteredFromResultLogs()
        {
            var progress = new StudentLifeProgress("slot", "p", 1, 1);
            progress.RecordActivityCompleted("a", new[] { "a:+relationship.mom=1", "a:+status.tired=2", "a:+trait.x=1" });
            progress.TryEndDay(null, out _);
            Assert.That(progress.GetPreviousDayRelationshipDeltas(), Is.EqualTo(new[] { "a:+relationship.mom=1" }));
            Assert.That(progress.GetPreviousDayStatusDeltas(), Is.EqualTo(new[] { "a:+status.tired=2" }));
        }

        [Test]
        public void RecordActivity_KeepsRepeatedResultsOnlyWhenAsked()
        {
            var progress = new StudentLifeProgress("slot", "p", 1, 1);
            progress.RecordActivityCompleted("a", new[] { "a:+x=1" });
            progress.RecordActivityCompleted("a", new[] { "a:+x=1" });
            Assert.That(progress.GetTodayResultLogIds().Length, Is.EqualTo(1));
            progress.RecordActivityCompleted("a", new[] { "a:+x=1" }, preserveRepeatedResults: true);
            Assert.That(progress.GetTodayResultLogIds().Length, Is.EqualTo(2));
            Assert.That(progress.GetTodayActivityIds().Length, Is.EqualTo(3));
        }
    }
}
