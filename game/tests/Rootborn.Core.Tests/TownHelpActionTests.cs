using NUnit.Framework;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Tests
{
    // 옮겨 옴: Assets/Tests/EditMode/StudentLife/TownHelpActionGrowthTests.cs
    // (등록부·에셋 와이어링·소스 문자열 검사와 씬 상호작용을 보는 TownHelpRequestIdentityTests는 옮기지 않음)
    public sealed class TownHelpActionTests
    {
        private static TownHelpAction Action(string id, int time, int energy, IHelpActionRequirement[] requirements, params IHelpActionOutcome[] outcomes)
            => new TownHelpAction(id, id, "npc.neighbor", "location.square", null, time, energy, 0, 0, requirements, outcomes);

        [Test]
        public void HELP_EDIT_003_RequirementsAndOutcomesRunWithoutEntityIdBranching()
        {
            var requirement = new HelpActionRelationshipThresholdRequirement("relationship.neighbor", 2);
            var progress = new StudentLifeProgress("slot-help", "player-help", 8, 8);

            Assert.IsFalse(requirement.IsSatisfied(progress));
            progress.AddRelationship("relationship.neighbor", 2);
            Assert.IsTrue(requirement.IsSatisfied(progress));
        }

        [Test]
        public void HELP_EDIT_004_HelpActionOutcomeChangesGenericGrowthState()
        {
            var progress = new StudentLifeProgress("slot-help", "player-help", 8, 8);
            var action = Action("town.help.neighbor", 20, 1, null, new HelpActionTraitDeltaOutcome("trait.helpfulness", 3));

            Assert.IsTrue(new TownHelpActionRunner().TryPerform(action, progress, "help-request-1", out var result));
            string expected = "town-help:town.help.neighbor:+trait.helpfulness=3";
            Assert.AreEqual(LifeActivityResultKind.Applied, result.Kind);
            Assert.AreEqual("town.help.neighbor", result.ActionId);
            Assert.AreEqual("npc.neighbor", result.NpcId);
            Assert.AreEqual("location.square", result.LocationId);
            Assert.AreEqual(3, progress.GetTraitValue("trait.helpfulness"));
            CollectionAssert.Contains(result.OutcomeLogIds, expected);
            CollectionAssert.Contains(progress.GetTodayResultLogIds(), expected);
        }

        [Test]
        public void HELP_EDIT_005_RelationshipOutcomeChangesGenericRelationshipState()
        {
            var progress = new StudentLifeProgress("slot-help", "player-help", 8, 8);
            var action = Action("town.help.neighbor", 20, 0, null, new HelpActionRelationshipDeltaOutcome("relationship.neighbor.trust", 2));

            Assert.IsTrue(new TownHelpActionRunner().TryPerform(action, progress, "relationship-help-1", out var result));
            string expected = "town-help:town.help.neighbor:+relationship.neighbor.trust=2";
            Assert.AreEqual(LifeActivityResultKind.Applied, result.Kind);
            Assert.AreEqual(2, progress.GetRelationshipValue("relationship.neighbor.trust"));
            CollectionAssert.Contains(result.OutcomeLogIds, expected);
            CollectionAssert.Contains(progress.GetTodayResultLogIds(), expected);
        }

        [Test]
        public void HELP_EDIT_006_DuplicateRequestIdDoesNotApplyTwice()
        {
            var progress = new StudentLifeProgress("slot-help", "player-help", 8, 8);
            var action = Action("town.help.neighbor", 20, 0, null, new HelpActionTraitDeltaOutcome("trait.helpfulness", 2));
            var runner = new TownHelpActionRunner();

            Assert.IsTrue(runner.TryPerform(action, progress, "same-help-request", out var first));
            Assert.IsFalse(runner.TryPerform(action, progress, "same-help-request", out var duplicate));
            Assert.AreEqual(LifeActivityResultKind.Applied, first.Kind);
            Assert.AreEqual(LifeActivityResultKind.DuplicateRequest, duplicate.Kind);
            Assert.AreEqual(2, progress.GetTraitValue("trait.helpfulness"));
            Assert.AreEqual(1, TownHelpActionDayLog.FromResultLogs(progress.GetTodayResultLogIds()).ActionIds.Length);
        }

        // 더한 경계

        [Test]
        public void RejectionsChangeNothingAndDuplicateSurvivesSave()
        {
            var progress = new StudentLifeProgress("slot", "player", 2, 2, 0, 480);
            var runner = new TownHelpActionRunner();
            var tiring = Action("a", 10, 5, null, new HelpActionSkillDeltaOutcome("skill.s", 1));
            var locked = Action("b", 10, 0, new IHelpActionRequirement[] { new HelpActionStatusThresholdRequirement("status.calm", 1) }, new HelpActionSkillDeltaOutcome("skill.s", 1));

            Assert.IsFalse(runner.TryPerform(tiring, progress, "r", out var poor));
            Assert.AreEqual(LifeActivityResultKind.InsufficientResources, poor.Kind);
            Assert.IsFalse(runner.TryPerform(locked, progress, "r", out var blocked));
            Assert.AreEqual(LifeActivityResultKind.RequirementFailed, blocked.Kind);
            Assert.IsFalse(runner.TryPerform(null, progress, "r", out var none));
            Assert.AreEqual(LifeActivityResultKind.InvalidRequest, none.Kind);
            Assert.IsFalse(runner.TryPerform(tiring, progress, "", out _));
            Assert.AreEqual(2, progress.Energy);
            Assert.AreEqual(480, progress.TimeMinutes);
            Assert.AreEqual(0, progress.GetSkillValue("skill.s"));
            Assert.IsFalse(progress.HasAppliedRequest("r"));

            progress.AddStatus("status.calm", 1);
            Assert.IsTrue(runner.TryPerform(locked, progress, "r", out _));
            var restored = StudentLifeProgress.FromSaveData(progress.ToSaveData());
            Assert.IsFalse(runner.TryPerform(locked, restored, "r", out var duplicate));
            Assert.AreEqual(LifeActivityResultKind.DuplicateRequest, duplicate.Kind);
            Assert.AreEqual(1, restored.GetSkillValue("skill.s"));
        }

        [Test]
        public void CareerHintLogsFirstUnlockAndSummaryParsesOnlyTownHelpLogs()
        {
            var progress = new StudentLifeProgress("slot", "player", 8, 8);
            Assert.AreEqual("town-help:a:+career.nurse=1", new HelpActionCareerHintOutcome("career.nurse").Apply(progress, "a"));
            Assert.AreEqual(string.Empty, new HelpActionCareerHintOutcome("career.nurse").Apply(progress, "a"));
            Assert.AreEqual("town-help:a:status.tired=-1", new HelpActionStatusDeltaOutcome("status.tired", -1).Apply(progress, "a"));
            Assert.IsTrue(new HelpActionTraitThresholdRequirement("t", 0).IsSatisfied(progress));
            Assert.IsTrue(new HelpActionSkillThresholdRequirement("s", -3).IsSatisfied(progress));
            Assert.IsFalse(new HelpActionSkillThresholdRequirement("", 0).IsSatisfied(progress));

            var summary = TownHelpActionGrowthSummary.FromResultLogs(new[] { "town-help:a:+t=2", "town-help:a:+t=1", "outside-school:a:+t=1", "town-help:x" });
            Assert.AreEqual(2, summary.Entries.Length);
            Assert.AreEqual(2, summary.Entries[0].Delta);
            CollectionAssert.AreEqual(new[] { "a" }, TownHelpActionDayLog.FromResultLogs(new[] { "town-help:a:+t=2", "town-help:a:+t=1" }).ActionIds);
        }
    }
}
