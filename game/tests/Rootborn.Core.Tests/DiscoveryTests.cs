using NUnit.Framework;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Tests
{
    // 옮겨 옴: Assets/Tests/EditMode/StudentLife/TownExplorationDiscoveryTests.cs (등록부·소스 문자열 검사는 옮기지 않음)
    public sealed class DiscoveryTests
    {
        private static Discovery Discovery(string id, DiscoveryScope scope = DiscoveryScope.Personal, IDiscoveryRequirement[] requirements = null, params IDiscoveryOutcome[] outcomes)
            => new Discovery(id, id, scope, requirements, outcomes);

        [Test]
        public void EXPLORE_EDIT_002_RequirementsAndOutcomesRunWithoutEntityIdBranching()
        {
            var requirement = new PreviousDiscoveryRequirement("discovery.old-well", DiscoveryScope.Personal);
            var progress = new StudentLifeProgress("slot-explore", "player-explore", 8, 8);

            Assert.IsFalse(requirement.IsSatisfied(progress));
            progress.MarkRequestApplied(DiscoveryRunner.RequestIdFor(Discovery("discovery.old-well")));
            Assert.IsTrue(requirement.IsSatisfied(progress));
        }

        [Test]
        public void EXPLORE_EDIT_003_DiscoveryOutcomeChangesGenericGrowthState()
        {
            var progress = new StudentLifeProgress("slot-explore", "player-explore", 8, 8);
            var discovery = Discovery("discovery.clocktower", outcomes: new DiscoveryTraitDeltaOutcome("trait.curiosity", 2));

            Assert.IsTrue(new DiscoveryRunner().TryDiscover(discovery, progress, out var result));
            string expected = "discovery:discovery.clocktower:+trait.curiosity=2";
            Assert.AreEqual(LifeActivityResultKind.Applied, result.Kind);
            Assert.AreEqual("discovery.clocktower", result.DiscoveryId);
            Assert.AreEqual(2, progress.GetTraitValue("trait.curiosity"));
            CollectionAssert.Contains(result.OutcomeLogIds, expected);
            CollectionAssert.Contains(progress.GetTodayActivityIds(), discovery.Id);
            CollectionAssert.Contains(progress.GetTodayResultLogIds(), expected);
        }

        [Test]
        public void EXPLORE_EDIT_004_SameDiscoveryDoesNotApplyTwiceAfterSaveLoad()
        {
            var runner = new DiscoveryRunner();
            var discovery = Discovery("discovery.clocktower", outcomes: new DiscoveryTraitDeltaOutcome("trait.curiosity", 3));
            var progress = new StudentLifeProgress("slot-explore", "player-explore", 8, 8);

            Assert.IsTrue(runner.TryDiscover(discovery, progress, out var first));
            var restored = StudentLifeProgress.FromSaveData(progress.ToSaveData());
            Assert.IsFalse(runner.TryDiscover(discovery, restored, out var duplicate));

            Assert.AreEqual(LifeActivityResultKind.Applied, first.Kind);
            Assert.AreEqual(LifeActivityResultKind.DuplicateRequest, duplicate.Kind);
            Assert.AreEqual(3, restored.GetTraitValue("trait.curiosity"));
            Assert.AreEqual(1, DiscoveryDayLog.FromResultLogs(restored.GetTodayResultLogIds()).DiscoveryIds.Length);
        }

        [Test]
        public void EXPLORE_EDIT_005_PersonalAndSharedDiscoveriesAreDataScoped()
        {
            Assert.AreEqual(DiscoveryScope.Personal, Discovery("discovery.personal").Scope);
            Assert.AreEqual(DiscoveryScope.SharedWorld, Discovery("discovery.shared", DiscoveryScope.SharedWorld).Scope);
            Assert.AreEqual("discovery:Personal:discovery.personal", DiscoveryRunner.RequestIdFor(Discovery("discovery.personal")));
            Assert.AreEqual("discovery:SharedWorld:discovery.shared", DiscoveryRunner.RequestIdFor(Discovery("discovery.shared", DiscoveryScope.SharedWorld)));
        }

        // 더한 경계

        [Test]
        public void UnmetRequirementAndInvalidRequestsChangeNothing()
        {
            var progress = new StudentLifeProgress("slot", "player", 8, 8);
            var runner = new DiscoveryRunner();
            var locked = Discovery("d", requirements: new IDiscoveryRequirement[] { new DiscoveryCareerHintRequirement("career.detective") }, outcomes: new DiscoverySkillDeltaOutcome("skill.s", 1));

            Assert.IsFalse(runner.TryDiscover(locked, progress, out var blocked));
            Assert.AreEqual(LifeActivityResultKind.RequirementFailed, blocked.Kind);
            Assert.IsFalse(runner.TryDiscover(locked, progress, "", out var empty));
            Assert.AreEqual(LifeActivityResultKind.InvalidRequest, empty.Kind);
            Assert.IsFalse(runner.TryDiscover(null, progress, out var none));
            Assert.AreEqual(string.Empty, none.RequestId);
            Assert.AreEqual(0, progress.GetSkillValue("skill.s"));
            Assert.IsEmpty(progress.GetActivityLogIds());
            Assert.IsFalse(progress.HasAppliedRequest(DiscoveryRunner.RequestIdFor(locked)));

            progress.UnlockCareerHint("career.detective");
            Assert.IsTrue(runner.TryDiscover(locked, progress, out _));
            Assert.AreEqual(1, progress.GetSkillValue("skill.s"));
        }

        [Test]
        public void OutcomesLogDeltasAndCareerHintLogsOnlyFirstUnlock()
        {
            var progress = new StudentLifeProgress("slot", "player", 8, 8);
            Assert.AreEqual("discovery:d:+career.c=1", new DiscoveryCareerHintOutcome("career.c").Apply(progress, "d"));
            Assert.AreEqual(string.Empty, new DiscoveryCareerHintOutcome("career.c").Apply(progress, "d"));
            Assert.AreEqual("discovery:d:+status.s=2", new DiscoveryStatusDeltaOutcome("status.s", 2).Apply(progress, "d"));
            Assert.AreEqual(string.Empty, new DiscoveryTraitDeltaOutcome("t", 0).Apply(progress, "d"));
            Assert.IsTrue(new DiscoveryTraitThresholdRequirement("t", -1).IsSatisfied(progress));
            Assert.IsFalse(new PreviousDiscoveryRequirement("", DiscoveryScope.Personal).IsSatisfied(progress));

            var summary = DiscoveryGrowthSummary.FromResultLogs(new[] { "discovery:d:+career.c=1", "discovery:d:status.s=-2", "town-help:x:+t=1", "discovery:Personal:d" });
            Assert.AreEqual(2, summary.Entries.Length);
            Assert.AreEqual(-2, summary.Entries[1].Delta);
        }
    }
}
