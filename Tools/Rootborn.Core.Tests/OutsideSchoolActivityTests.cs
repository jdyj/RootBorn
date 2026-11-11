using System.Linq;
using NUnit.Framework;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Tests
{
    // 옮겨 옴: Assets/Tests/EditMode/StudentLife/OutsideSchoolGrowthFoundationTests.cs, OutsideSchoolInteractorRequestPersistenceTests.cs의 규칙 부분
    // (등록부·소스 문자열 검사, 씬 상호작용·결과 화면 검사, 에셋을 쓰는 SelfStudyLibraryGrowthTests는 옮기지 않음)
    public sealed class OutsideSchoolActivityTests
    {
        private static OutsideSchoolActivity Activity(string id, string categoryId, int time, int energy, IOutsideSchoolRequirement[] requirements, params IOutsideSchoolOutcome[] outcomes)
            => new OutsideSchoolActivity(id, id, categoryId, null, time, energy, 0, 0, requirements, outcomes);

        [Test]
        public void OUTSIDE_FOUNDATION_EDIT_002_RequirementsEvaluateWithoutEntityIdBranching()
        {
            var requirement = new OutsideSchoolTraitThresholdRequirement("trait.helpfulness", 2);
            var progress = new StudentLifeProgress("slot-outside", "player-outside", 8, 8);

            Assert.IsFalse(requirement.IsSatisfied(progress));
            progress.AddTrait("trait.helpfulness", 2);
            Assert.IsTrue(requirement.IsSatisfied(progress));
        }

        [Test]
        public void OUTSIDE_FOUNDATION_EDIT_003_OutcomeRecordsGenericOutsideSchoolResultLog()
        {
            var activity = Activity("outside.help-neighbor", "outside.category.help", 30, 1, null, new OutsideSchoolTraitDeltaOutcome("trait.helpfulness", 3));
            var progress = new StudentLifeProgress("slot-outside", "player-outside", 8, 8);

            Assert.IsTrue(new OutsideSchoolActivityRunner().TryPerform(activity, progress, "outside-request-1", out var result));

            string expected = "outside-school:outside.help-neighbor:+trait.helpfulness=3";
            Assert.AreEqual(LifeActivityResultKind.Applied, result.Kind);
            Assert.AreEqual("outside.help-neighbor", result.ActivityId);
            Assert.AreEqual("outside.category.help", result.CategoryId);
            Assert.AreEqual(3, progress.GetTraitValue("trait.helpfulness"));
            CollectionAssert.Contains(result.OutcomeLogIds, expected);
            CollectionAssert.Contains(OutsideSchoolDayLog.FromResultLogs(progress.GetTodayResultLogIds()).ResultLogIds, expected);
            CollectionAssert.Contains(progress.GetTodayResultLogIds(), expected);
        }

        [Test]
        public void OUTSIDE_FOUNDATION_EDIT_004_DuplicateRequestIdDoesNotApplyTwice()
        {
            var activity = Activity("outside.help-neighbor", "outside.category.help", 30, 0, null, new OutsideSchoolTraitDeltaOutcome("trait.helpfulness", 2));
            var progress = new StudentLifeProgress("slot-outside", "player-outside", 8, 8);
            var runner = new OutsideSchoolActivityRunner();

            Assert.IsTrue(runner.TryPerform(activity, progress, "same-outside-request", out var first));
            Assert.IsFalse(runner.TryPerform(activity, progress, "same-outside-request", out var duplicate));

            Assert.AreEqual(LifeActivityResultKind.Applied, first.Kind);
            Assert.AreEqual(LifeActivityResultKind.DuplicateRequest, duplicate.Kind);
            Assert.AreEqual(2, progress.GetTraitValue("trait.helpfulness"));
            Assert.AreEqual(1, OutsideSchoolDayLog.FromResultLogs(progress.GetTodayResultLogIds()).ActivityIds.Length);
        }

        [Test]
        public void OUTSIDE_FOUNDATION_EDIT_005_TodayAndPreviousOutsideSchoolLogsPersistInSaveData()
        {
            var progress = new StudentLifeProgress("slot-outside", "player-outside", 8, 8);
            progress.RecordActivityCompleted("outside.help-neighbor", new[] { "outside-school:outside.help-neighbor:+trait.helpfulness=1" });

            var saveData = progress.ToSaveData();
            var restored = StudentLifeProgress.FromSaveData(saveData);

            CollectionAssert.Contains(saveData.TodayActivityIds, "outside.help-neighbor");
            CollectionAssert.Contains(saveData.TodayResultLogIds, "outside-school:outside.help-neighbor:+trait.helpfulness=1");
            CollectionAssert.Contains(OutsideSchoolDayLog.FromResultLogs(restored.GetTodayResultLogIds()).ActivityIds, "outside.help-neighbor");
            Assert.IsTrue(restored.TryEndDay(null, out var summary));
            CollectionAssert.Contains(OutsideSchoolDayLog.FromResultLogs(restored.GetPreviousDayResultLogIds()).ActivityIds, "outside.help-neighbor");
            CollectionAssert.Contains(summary.ResultLogIds, "outside-school:outside.help-neighbor:+trait.helpfulness=1");
        }

        [Test]
        public void OUTSIDE_FOUNDATION_EDIT_006_SummaryDtoUsesCommonEntriesWithoutRouteSpecificFields()
        {
            var log = new OutsideSchoolDayLog(new[] { "outside.help-neighbor" },
                new[] { "outside-school:outside.help-neighbor:+trait.helpfulness=1", "outside-school:outside.help-neighbor:+skill.community=2" }, new[] { "request-1" });

            var summary = OutsideSchoolGrowthSummary.FromDayLog(log);

            Assert.AreEqual(2, summary.Entries.Length);
            Assert.AreEqual("outside.help-neighbor", summary.Entries[0].ActivityId);
            Assert.AreEqual("trait.helpfulness", summary.Entries[0].TargetId);
            Assert.AreEqual(1, summary.Entries[0].Delta);
            Assert.AreEqual("skill.community", summary.Entries[1].TargetId);
            Assert.AreEqual(2, summary.Entries[1].Delta);
        }

        [Test]
        public void OUTSIDE_FOUNDATION_EDIT_007_FollowUpRoutesCanReuseSameModel()
        {
            string[] categories = { "outside.category.help", "outside.category.self-study", "outside.category.work", "outside.category.exploration" };
            for (int i = 0; i < categories.Length; i++) Assert.AreEqual(categories[i], Activity("outside.sample." + i, categories[i], 5, 0, null).CategoryId);
        }

        [Test]
        public void RepeatedFreshActionsKeepBothGrowthDeltasThroughSaveAndDayResult()
        {
            var activity = Activity("fixture.outside.action", null, 10, 0, null, new OutsideSchoolTraitDeltaOutcome("trait.fixture.outside", 1));
            var progress = new StudentLifeProgress("slot", "player", 12, 10, 0, 480);
            var runner = new OutsideSchoolActivityRunner();
            Assert.IsTrue(runner.TryPerform(activity, progress, "r1", out _));
            Assert.IsTrue(runner.TryPerform(activity, progress, "r2", out _));
            int Total(string[] logs) => OutsideSchoolGrowthSummary.FromResultLogs(logs).Entries.Where(e => e.TargetId == "trait.fixture.outside").Sum(e => e.Delta);

            Assert.AreEqual(2, progress.GetTraitValue("trait.fixture.outside"));
            Assert.AreEqual(2, Total(progress.GetTodayResultLogIds()));
            var restored = StudentLifeProgress.FromSaveData(progress.ToSaveData());
            Assert.AreEqual(2, Total(restored.GetTodayResultLogIds()));
            Assert.IsTrue(progress.TryEndDay(null, out var day));
            Assert.AreEqual(2, Total(day.ResultLogIds));
        }

        [Test]
        public void ExplicitRepeatedRequestRejectsDuplicateWithoutCostOrGrowth()
        {
            var activity = Activity("fixture.outside.action", null, 10, 0, null, new OutsideSchoolTraitDeltaOutcome("trait.fixture.outside", 1));
            var progress = new StudentLifeProgress("slot", "player", 12, 10, 0, 480);
            var runner = new OutsideSchoolActivityRunner();
            Assert.IsTrue(runner.TryPerform(activity, progress, "explicit-request", out _));
            int minutes = progress.TimeMinutes;

            Assert.IsFalse(runner.TryPerform(activity, progress, "explicit-request", out var duplicate));
            Assert.AreEqual(LifeActivityResultKind.DuplicateRequest, duplicate.Kind);
            Assert.AreEqual(minutes, progress.TimeMinutes);
            Assert.AreEqual(1, progress.GetTraitValue("trait.fixture.outside"));
        }

        [Test]
        public void GrowthSummaryMergesRepeatedTargetsAndRetainsFirstSeenOrder()
        {
            var logs = new[]
            {
                OutsideSchoolLogCodec.EncodeDelta("activity.fixture", "trait.fixture", 1), "other:untouched",
                OutsideSchoolLogCodec.EncodeDelta("activity.fixture", "skill.fixture", 3), OutsideSchoolLogCodec.EncodeDelta("activity.fixture", "trait.fixture", 1),
            };
            var summary = OutsideSchoolGrowthSummary.FromResultLogs(logs);

            Assert.AreEqual(2, summary.Entries.Length);
            Assert.AreEqual("trait.fixture", summary.Entries[0].TargetId);
            Assert.AreEqual(2, summary.Entries[0].Delta);
            Assert.AreEqual("skill.fixture", summary.Entries[1].TargetId);
            Assert.AreEqual(3, summary.Entries[1].Delta);
        }

        // 더한 경계

        [Test]
        public void RejectionsChangeNothing()
        {
            var progress = new StudentLifeProgress("slot", "player", 2, 2, 0, 480);
            var runner = new OutsideSchoolActivityRunner();
            var costly = Activity("a", "c", 10, 5, null, new OutsideSchoolSkillDeltaOutcome("skill.s", 1));
            var locked = Activity("b", "c", 10, 0, new IOutsideSchoolRequirement[] { new OutsideSchoolSkillThresholdRequirement("skill.s", 4) }, new OutsideSchoolSkillDeltaOutcome("skill.s", 1));

            Assert.IsFalse(runner.TryPerform(costly, progress, "r", out var poor));
            Assert.AreEqual(LifeActivityResultKind.InsufficientResources, poor.Kind);
            Assert.IsFalse(runner.TryPerform(locked, progress, "r", out var blocked));
            Assert.AreEqual(LifeActivityResultKind.RequirementFailed, blocked.Kind);
            Assert.IsFalse(runner.TryPerform(costly, progress, null, out var empty));
            Assert.AreEqual(LifeActivityResultKind.InvalidRequest, empty.Kind);
            Assert.AreEqual(2, progress.Energy);
            Assert.AreEqual(480, progress.TimeMinutes);
            Assert.AreEqual(0, progress.GetSkillValue("skill.s"));
            Assert.IsFalse(progress.HasAppliedRequest("r"));
        }

        [Test]
        public void DuplicateRequestStaysRejectedAfterSaveRoundTrip()
        {
            var activity = Activity("a", "c", 0, 0, null, new OutsideSchoolRelationshipDeltaOutcome("relationship.r", 1), new OutsideSchoolStatusDeltaOutcome("status.s", 2));
            var progress = new StudentLifeProgress("slot", "player", 8, 8);
            Assert.IsTrue(new OutsideSchoolActivityRunner().TryPerform(activity, progress, "r", out var applied));
            CollectionAssert.AreEqual(new[] { "outside-school:a:+relationship.r=1", "outside-school:a:+status.s=2" }, applied.OutcomeLogIds);

            var restored = StudentLifeProgress.FromSaveData(progress.ToSaveData());
            Assert.IsFalse(new OutsideSchoolActivityRunner().TryPerform(activity, restored, "r", out var duplicate));
            Assert.AreEqual(LifeActivityResultKind.DuplicateRequest, duplicate.Kind);
            Assert.AreEqual(1, restored.GetRelationshipValue("relationship.r"));
        }

        [Test]
        public void ZeroOrEmptyOutcomesDoNotLogAndAggregateKeepsOtherLogs()
        {
            var progress = new StudentLifeProgress("slot", "player", 8, 8);
            Assert.AreEqual(string.Empty, new OutsideSchoolTraitDeltaOutcome("t", 0).Apply(progress, "a"));
            Assert.AreEqual(string.Empty, new OutsideSchoolSkillDeltaOutcome("", 1).Apply(progress, "a"));
            Assert.AreEqual(string.Empty, new OutsideSchoolTraitDeltaOutcome("t", 1).Apply(null, "a"));

            var aggregated = OutsideSchoolLogCodec.AggregateDeltas(new[] { "x", "outside-school:a:+t=1", "outside-school:a:t=-1" });
            CollectionAssert.AreEqual(new[] { "x", string.Empty }, aggregated);
            CollectionAssert.IsEmpty(OutsideSchoolLogCodec.AggregateDeltas(null));
        }
    }
}
