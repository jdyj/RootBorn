using System;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Inventory;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Tests
{
    // 옮겨 옴: Assets/Tests/EditMode/StudentLife/OpenEndedMilestoneGrowthTests.cs
    // (등록부 검사 001과 등록부·목표 저널을 보는 SecondNpcRelationshipRouteTests는 옮기지 않음)
    public sealed class MilestoneTests
    {
        [Test]
        public void ObjectiveNameKey_ComesFromTheDefinition_OrFallsBackToTheId()
        {
            Assert.That(new TraitMilestoneObjective("objective.a", "trait.a", 1, "milestone.objective.a.name").DisplayNameKey, Is.EqualTo("milestone.objective.a.name"));
            Assert.That(new SkillMilestoneObjective("objective.b", "skill.b", 1).DisplayNameKey, Is.EqualTo("objective.b"));
            Assert.That(new OutsideSchoolLogMilestoneObjective("objective.c", null, 1, "").DisplayNameKey, Is.EqualTo("objective.c"));
        }

        private static Milestone Milestone(string id, MilestoneObjective[] objectives, IMilestoneReward[] rewards, MilestoneRoute[] routes, int requiredObjectiveCount, string[] prerequisites = null)
            => new Milestone(id, id, objectives, rewards, routes, prerequisites, requiredObjectiveCount);

        private static bool Complete(MilestoneObjective objective, MilestoneEvaluationContext context) => objective.Evaluate(in context).IsComplete;

        private static ItemInventory FullInventory()
        {
            var inventory = new ItemInventory();
            for (int i = 0; i < ItemInventory.MaxSlots; i++) inventory.Add(new ItemDefinition("filler." + i, 1), 1);
            return inventory;
        }

        [Test]
        public void MILESTONE_EDIT_002_ObjectivesEvaluateStudentLifeStateWithoutEntitySpecificBranches()
        {
            var progress = new StudentLifeProgress("slot-a", "player-a", 12, 10);
            progress.AddTrait("trait-kindness", 3);
            progress.AddSkill("skill-study", 4);
            progress.AddRelationship("rel-guide", 2);
            progress.AddStatus("status-rested", 1);
            progress.RecordActivityCompleted("library-study", new[] { OutsideSchoolLogCodec.EncodeDelta("library-study", "skill-study", 2) });
            var context = new MilestoneEvaluationContext(progress, null, Array.Empty<string>());

            Assert.That(Complete(new TraitMilestoneObjective("trait-goal", "trait-kindness", 3), context), Is.True);
            Assert.That(Complete(new SkillMilestoneObjective("skill-goal", "skill-study", 4), context), Is.True);
            Assert.That(Complete(new RelationshipMilestoneObjective("relationship-goal", "rel-guide", 2), context), Is.True);
            Assert.That(Complete(new StatusMilestoneObjective("status-goal", "status-rested", 1), context), Is.True);
            Assert.That(Complete(new OutsideSchoolLogMilestoneObjective("outside-goal", "skill-study", 2), context), Is.True);
        }

        [Test]
        public void MILESTONE_EDIT_003_SameObjectiveCanBeAdvancedByTwoOrMoreRouteDefinitions()
        {
            var objective = new TraitMilestoneObjective("kindness-five", "trait-kindness", 5);
            var milestone = Milestone("village-adaptation", new MilestoneObjective[] { objective }, null,
                new[] { new MilestoneRoute("school-class-route", objective, "classroom-practice"), new MilestoneRoute("town-help-route", objective, "town-help-action") }, 1);

            Assert.That(milestone.Routes.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(milestone.Routes.Select(r => r.Objective).Distinct().Single(), Is.SameAs(objective));
        }

        [Test]
        public void MILESTONE_EDIT_004_OutsideSchoolLogCanAdvanceMilestoneWithoutSchoolClass()
        {
            var objective = new OutsideSchoolLogMilestoneObjective("study-anywhere", "skill-study", 2);
            var milestone = Milestone("find-my-study", new MilestoneObjective[] { objective }, null, new[] { new MilestoneRoute("library-route", objective, "library-study") }, 1);
            var progress = new StudentLifeProgress("slot-a", "player-a", 12, 10);
            progress.RecordActivityCompleted("library-study", new[] { OutsideSchoolLogCodec.EncodeDelta("library-study", "skill-study", 2) });
            var milestoneProgress = new MilestoneProgress("slot-a", "player-a", new[] { milestone });

            var result = milestoneProgress.ApplyDayResult(new[] { milestone }, progress, null, "day-1");

            Assert.That(result.ProgressChanges.Any(c => c.MilestoneId == milestone.Id && c.Completed), Is.True);
            Assert.That(milestoneProgress.IsCompleted(milestone), Is.True);
        }

        [Test]
        public void MILESTONE_EDIT_005_CompletedRewardValidatesInventoryCapacityAndDuplicateClaimBeforeApplying()
        {
            var item = new ItemDefinition("item.reward", 99);
            var milestone = Milestone("rewarded-goal", null, new IMilestoneReward[] { new ItemMilestoneReward(item, 1) }, null, 0);
            var milestoneProgress = new MilestoneProgress("slot-a", "player-a", new[] { milestone });
            milestoneProgress.MarkCompletedForTests(milestone);
            var fullInventory = FullInventory();

            Assert.That(milestoneProgress.TryClaimReward(milestone, fullInventory), Is.False);
            Assert.That(fullInventory.CountOf(item), Is.Zero);

            var inventory = new ItemInventory();
            Assert.That(milestoneProgress.TryClaimReward(milestone, inventory), Is.True);
            Assert.That(inventory.CountOf(item), Is.EqualTo(1));
            Assert.That(milestoneProgress.TryClaimReward(milestone, inventory), Is.False);
            Assert.That(inventory.CountOf(item), Is.EqualTo(1));
        }

        [Test]
        public void MILESTONE_EDIT_006_SaveLoadRestoresProgressCompletionAndRewardClaimState()
        {
            var objective = new OutsideSchoolLogMilestoneObjective("study-anywhere", "skill-study", 2);
            var milestone = Milestone("find-my-study", new MilestoneObjective[] { objective }, null, null, 1);
            var original = new MilestoneProgress("slot-a", "player-a", new[] { milestone });
            original.SetObjectiveProgressForTests(milestone, objective, 2);
            original.MarkCompletedForTests(milestone);
            original.MarkRewardClaimedForTests(milestone);

            var restored = MilestoneProgress.FromSaveData(original.ToSaveData(), new[] { milestone });

            Assert.That(restored.GetObjectiveProgress(milestone, objective), Is.EqualTo(2));
            Assert.That(restored.IsCompleted(milestone), Is.True);
            Assert.That(restored.IsRewardClaimed(milestone), Is.True);
            Assert.That(restored.SaveSlot, Is.EqualTo("slot-a"));
        }

        [Test]
        public void MILESTONE_EDIT_007_ReapplyingSameDayResultDoesNotDuplicateProgressOrRewards()
        {
            var item = new ItemDefinition("item.reward", 99);
            var objective = new OutsideSchoolLogMilestoneObjective("study-anywhere", "skill-study", 2);
            var milestone = Milestone("find-my-study", new MilestoneObjective[] { objective }, new IMilestoneReward[] { new ItemMilestoneReward(item, 1) }, null, 1);
            var progress = new StudentLifeProgress("slot-a", "player-a", 12, 10);
            progress.RecordActivityCompleted("library-study", new[] { OutsideSchoolLogCodec.EncodeDelta("library-study", "skill-study", 2) });
            var milestoneProgress = new MilestoneProgress("slot-a", "player-a", new[] { milestone });
            var inventory = new ItemInventory();

            milestoneProgress.ApplyDayResult(new[] { milestone }, progress, inventory, "day-1");
            milestoneProgress.TryClaimReward(milestone, inventory);
            milestoneProgress.ApplyDayResult(new[] { milestone }, progress, inventory, "day-1");
            milestoneProgress.TryClaimReward(milestone, inventory);

            Assert.That(milestoneProgress.GetObjectiveProgress(milestone, objective), Is.EqualTo(2));
            Assert.That(inventory.CountOf(item), Is.EqualTo(1));
        }

        [Test]
        public void MILESTONE_EDIT_008_DaySummaryIncludesMilestoneProgressChangesAndFiltersCompletedRecommendations()
        {
            var objective = new OutsideSchoolLogMilestoneObjective("study-anywhere", "skill-study", 2);
            var milestone = Milestone("find-my-study", new MilestoneObjective[] { objective }, null, new[] { new MilestoneRoute("library-route", objective, "library-study") }, 1);
            var progress = new StudentLifeProgress("slot-a", "player-a", 12, 10);
            progress.RecordActivityCompleted("library-study", new[] { OutsideSchoolLogCodec.EncodeDelta("library-study", "skill-study", 2) });
            var milestoneProgress = new MilestoneProgress("slot-a", "player-a", new[] { milestone });

            var result = milestoneProgress.ApplyDayResult(new[] { milestone }, progress, null, "day-1");
            var summary = MilestoneDaySummary.FromApplyResult(result, new[] { milestone });

            Assert.That(summary.ProgressLines.Any(line => line.Contains("find-my-study") && line.Contains("complete")), Is.True);
            Assert.That(summary.RecommendedActionIds, Does.Not.Contain("library-study"));
        }

        [Test]
        public void MILESTONE_EDIT_012_CompletedMilestoneRoutesDoNotRemainRecommended()
        {
            var completedObjective = new OutsideSchoolLogMilestoneObjective("completed-objective", "completed-growth", 1);
            var completedMilestone = Milestone("completed-milestone", new MilestoneObjective[] { completedObjective }, null, new[] { new MilestoneRoute("completed-route", completedObjective, "completed-action") }, 1);
            var activeObjective = new OutsideSchoolLogMilestoneObjective("active-objective", "active-growth", 2);
            var activeMilestone = Milestone("active-milestone", new MilestoneObjective[] { activeObjective }, null, new[] { new MilestoneRoute("active-route", activeObjective, "active-action") }, 1);
            var milestones = new[] { completedMilestone, activeMilestone };
            var milestoneProgress = new MilestoneProgress("slot-a", "player-a", milestones);
            milestoneProgress.MarkCompletedForTests(completedMilestone);

            var result = milestoneProgress.ApplyDayResult(milestones, new StudentLifeProgress("slot-a", "player-a", 12, 10), null, "day-1");

            Assert.That(result.RecommendedActionIds, Does.Contain("active-action"));
            Assert.That(result.RecommendedActionIds, Does.Not.Contain("completed-action"));
        }

        // 더한 경계

        [Test]
        public void SeveralItemRewardsThatDoNotFitTogetherAreRejectedWithoutPartialGrant()
        {
            // Unity 판과 달라진 동작: 원본은 보상을 하나씩만 확인해 함께 넘치면 일부만 지급하고 받은 것으로 기록했다.
            var inventory = new ItemInventory();
            for (int i = 0; i < ItemInventory.MaxSlots - 1; i++) inventory.Add(new ItemDefinition("filler." + i, 1), 1);
            var a = new ItemDefinition("item.a", 1);
            var b = new ItemDefinition("item.b", 1);
            var milestone = Milestone("m", null, new IMilestoneReward[] { new ItemMilestoneReward(a, 1), new ItemMilestoneReward(b, 1) }, null, 0);
            var progress = new MilestoneProgress("slot", "player", new[] { milestone });
            progress.MarkCompletedForTests(milestone);

            Assert.That(progress.TryClaimReward(milestone, inventory), Is.False);
            Assert.That(inventory.CountOf(a) + inventory.CountOf(b), Is.Zero);
            Assert.That(progress.IsRewardClaimed(milestone), Is.False);
            Assert.That(progress.TryClaimReward(milestone, new ItemInventory()), Is.True);
        }

        [Test]
        public void IncompleteOrMissingInventoryRewardIsRejected()
        {
            var milestone = Milestone("m", null, new IMilestoneReward[] { new ItemMilestoneReward(new ItemDefinition("i"), 1) }, null, 0);
            var progress = new MilestoneProgress("slot", "player", new[] { milestone });

            Assert.That(progress.TryClaimReward(milestone, new ItemInventory()), Is.False);
            progress.MarkCompletedForTests(milestone);
            Assert.That(progress.TryClaimReward(milestone, null), Is.False);
            Assert.That(progress.TryClaimReward(null, new ItemInventory()), Is.False);
            Assert.That(progress.IsRewardClaimed(milestone), Is.False);
            Assert.That(Milestone("no-reward", null, null, null, 0).RequiredObjectiveCount, Is.EqualTo(0));
        }

        [Test]
        public void PrerequisitesGateProgressAndRecommendationsAndProgressNeverGoesDown()
        {
            var trait = new TraitMilestoneObjective("o.trait", "trait.kind", 2);
            var first = Milestone("first", new MilestoneObjective[] { trait }, null, new[] { new MilestoneRoute("r1", trait, "act.first") }, 1);
            var skill = new SkillMilestoneObjective("o.skill", "skill.s", 1);
            var second = Milestone("second", new MilestoneObjective[] { skill }, null, new[] { new MilestoneRoute("r2", skill, "act.second") }, 1, new[] { "first" });
            var milestones = new[] { first, second };
            var student = new StudentLifeProgress("slot", "player", 8, 8);
            student.AddTrait("trait.kind", 1);
            student.AddSkill("skill.s", 1);
            var progress = new MilestoneProgress("slot", "player", milestones);

            var day1 = progress.ApplyDayResult(milestones, student, null, "day-1");
            Assert.That(day1.RecommendedActionIds, Is.EqualTo(new[] { "act.first" }));
            Assert.That(progress.GetObjectiveProgress(second, skill), Is.Zero);
            Assert.That(progress.GetObjectiveProgress(first, trait), Is.EqualTo(1));

            student.AddTrait("trait.kind", 1);
            var day2 = progress.ApplyDayResult(milestones, student, null, "day-2");
            Assert.That(progress.IsCompleted(first), Is.True);
            Assert.That(progress.IsCompleted(second), Is.True, "앞 마일스톤이 같은 날 끝나면 뒤 마일스톤도 같은 날 평가한다(목록 순서대로).");
            Assert.That(day2.RecommendedActionIds, Is.Empty);

            var lower = new StudentLifeProgress("slot", "player", 8, 8);
            progress.ApplyDayResult(milestones, lower, null, "day-3");
            Assert.That(progress.GetObjectiveProgress(first, trait), Is.EqualTo(2));
            Assert.That(progress.ApplyDayResult(milestones, student, null, "").ProgressChanges, Is.Empty);
        }

        [Test]
        public void SaveFileNameAndFallbackMilestoneAndActiveSummary()
        {
            Assert.That(Save.SaveFileNames.Milestone("local-player"), Is.EqualTo("milestone-progress.json"));
            Assert.That(Save.SaveFileNames.Milestone("p 2"), Is.EqualTo("milestone-progress-p_2.json"));

            var fallback = MilestoneFallback.Create("outside.help");
            Assert.That(fallback.Id, Is.EqualTo("milestone.open-ended-town-growth"));
            Assert.That(fallback.Routes[0].RecommendedActionId, Is.EqualTo("outside.help"));
            Assert.That(MilestoneFallback.Create(null).Routes[0].RecommendedActionId, Is.EqualTo("outside-school"));

            var summary = MilestoneDaySummary.FromApplyResult(new MilestoneApplyResult(null, null), new[] { fallback });
            Assert.That(summary.ProgressLines, Is.EqualTo(new[] { "milestone.open-ended-town-growth active" }));
            var restored = MilestoneProgress.FromSaveData(null, null);
            Assert.That(restored.PlayerId, Is.EqualTo("player"));
        }

        [Test]
        public void ChangedBehavior_DayResultDoesNotFillATargetedObjectiveFromAnotherTargetsLog()
        {
            var objective = new OutsideSchoolLogMilestoneObjective("study-anywhere", "skill-study", 2);
            var anyObjective = new OutsideSchoolLogMilestoneObjective("anything-outside", string.Empty, 1);
            var milestone = Milestone("find-my-study", new MilestoneObjective[] { objective, anyObjective }, null, null, 2);
            var progress = new StudentLifeProgress("slot-a", "player-a", 12, 10);
            progress.RecordActivityCompleted("cafe-help", new[] { OutsideSchoolLogCodec.EncodeDelta("cafe-help", "skill-other", -1) });
            var milestoneProgress = new MilestoneProgress("slot-a", "player-a", new[] { milestone });

            milestoneProgress.ApplyDayResult(new[] { milestone }, progress, new ItemInventory(), "day-1");

            Assert.That(milestoneProgress.GetObjectiveProgress(milestone, objective), Is.EqualTo(0), "다른 대상의 기록은 이 목표를 채우지 않는다.");
            Assert.That(milestoneProgress.GetObjectiveProgress(milestone, anyObjective), Is.EqualTo(1), "대상이 없는 목표는 학교 밖 기록 하나로 채워진다.");
        }
    }
}
