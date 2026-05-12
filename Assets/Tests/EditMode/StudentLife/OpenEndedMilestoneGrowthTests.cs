using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Quests;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class OpenEndedMilestoneGrowthTests
    {
        [Test]
        public void MILESTONE_EDIT_001_GameDataRegistryExposesMilestoneDataArrays()
        {
            var milestone = ScriptableObject.CreateInstance<MilestoneDefinition>();
            var objective = ScriptableObject.CreateInstance<TraitMilestoneObjective>();
            var route = ScriptableObject.CreateInstance<MilestoneRouteDefinition>();
            var reward = ScriptableObject.CreateInstance<ItemMilestoneReward>();
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();

            SetPrivateField(registry, "_milestones", new[] { milestone });
            SetPrivateField(registry, "_milestoneObjectives", new MilestoneObjectiveBase[] { objective });
            SetPrivateField(registry, "_milestoneRoutes", new[] { route });
            SetPrivateField(registry, "_milestoneRewards", new MilestoneRewardBase[] { reward });

            CollectionAssert.Contains(registry.Milestones, milestone);
            CollectionAssert.Contains(registry.MilestoneObjectives, objective);
            CollectionAssert.Contains(registry.MilestoneRoutes, route);
            CollectionAssert.Contains(registry.MilestoneRewards, reward);
        }

        [Test]
        public void MILESTONE_EDIT_002_ObjectivesEvaluateStudentLifeStateWithoutEntitySpecificBranches()
        {
            var trait = Definition<TraitDefinition>("trait-kindness");
            var skill = Definition<SkillDefinition>("skill-study");
            var relationship = Definition<RelationshipDefinition>("rel-guide");
            var status = Definition<StatusDefinition>("status-rested");
            var progress = new StudentLifeProgress("slot-a", "player-a", 12, 10);
            progress.AddTrait(trait, 3);
            progress.AddSkill(skill, 4);
            progress.AddRelationshipForTests(relationship, 2);
            progress.AddStatusForTests(status, 1);
            progress.RecordActivityCompleted("library-study", new[] { OutsideSchoolLogCodec.EncodeDelta("library-study", "skill-study", 2) });
            var context = new MilestoneEvaluationContext(progress, null, Array.Empty<string>());

            Assert.That(Complete(TraitObjective("trait-goal", trait, 3), context), Is.True, "Trait objective should evaluate configured SO state.");
            Assert.That(Complete(SkillObjective("skill-goal", skill, 4), context), Is.True, "Skill objective should evaluate configured SO state.");
            Assert.That(Complete(RelationshipObjective("relationship-goal", relationship, 2), context), Is.True, "Relationship objective should evaluate configured SO state.");
            Assert.That(Complete(StatusObjective("status-goal", status, 1), context), Is.True, "Status objective should evaluate configured SO state.");
            Assert.That(Complete(OutsideSchoolLogObjective("outside-goal", "skill-study", 2), context), Is.True, "Outside-school result logs should progress objectives without class-per-activity branching.");
        }

        [Test]
        public void MILESTONE_EDIT_003_SameObjectiveCanBeAdvancedByTwoOrMoreRouteDefinitions()
        {
            var trait = Definition<TraitDefinition>("trait-kindness");
            var objective = TraitObjective("kindness-five", trait, 5);
            var schoolRoute = Route("school-class-route", objective, "classroom-practice");
            var outsideRoute = Route("town-help-route", objective, "town-help-action");
            var milestone = Milestone("village-adaptation", new[] { objective }, Array.Empty<MilestoneRewardBase>(), new[] { schoolRoute, outsideRoute }, 1);

            Assert.That(milestone.Routes.Length, Is.GreaterThanOrEqualTo(2));
            Assert.That(milestone.Routes.Select(r => r.Objective).Distinct().Single(), Is.SameAs(objective));
        }

        [Test]
        public void MILESTONE_EDIT_004_OutsideSchoolLogCanAdvanceMilestoneWithoutSchoolClass()
        {
            var objective = OutsideSchoolLogObjective("study-anywhere", "skill-study", 2);
            var route = Route("library-route", objective, "library-study");
            var milestone = Milestone("find-my-study", new[] { objective }, Array.Empty<MilestoneRewardBase>(), new[] { route }, 1);
            var progress = new StudentLifeProgress("slot-a", "player-a", 12, 10);
            progress.RecordActivityCompleted("library-study", new[] { OutsideSchoolLogCodec.EncodeDelta("library-study", "skill-study", 2) });
            var milestoneProgress = new MilestoneProgress("slot-a", "player-a", new[] { milestone });

            var result = milestoneProgress.ApplyDayResult(new[] { milestone }, progress, null, "day-1");

            Assert.That(result.ProgressChanges.Any(c => c.MilestoneId == milestone.Id && c.Completed), Is.True, "Outside-school path progression was not reflected in milestone state.");
            Assert.That(milestoneProgress.IsCompleted(milestone), Is.True);
        }

        [Test]
        public void MILESTONE_EDIT_005_CompletedRewardValidatesInventoryCapacityAndDuplicateClaimBeforeApplying()
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            var reward = ScriptableObject.CreateInstance<ItemMilestoneReward>();
            reward.ConfigureForTests(item, 1);
            var milestone = Milestone("rewarded-goal", Array.Empty<MilestoneObjectiveBase>(), new MilestoneRewardBase[] { reward }, Array.Empty<MilestoneRouteDefinition>(), 0);
            var milestoneProgress = new MilestoneProgress("slot-a", "player-a", new[] { milestone });
            milestoneProgress.MarkCompletedForTests(milestone);
            var fullInventory = new Inventory();
            for (int i = 0; i < Inventory.MaxSlots; i++) fullInventory.Add(ScriptableObject.CreateInstance<ItemDefinition>(), 1);

            Assert.That(milestoneProgress.TryClaimReward(milestone, fullInventory), Is.False, "Reward claim must fail atomically when inventory has no capacity.");
            Assert.That(fullInventory.CountOf(item), Is.Zero);

            var inventory = new Inventory();
            Assert.That(milestoneProgress.TryClaimReward(milestone, inventory), Is.True);
            Assert.That(inventory.CountOf(item), Is.EqualTo(1));
            Assert.That(milestoneProgress.TryClaimReward(milestone, inventory), Is.False, "Duplicate reward claim should be rejected.");
            Assert.That(inventory.CountOf(item), Is.EqualTo(1));
        }

        [Test]
        public void MILESTONE_EDIT_006_SaveLoadRestoresProgressCompletionAndRewardClaimState()
        {
            var objective = OutsideSchoolLogObjective("study-anywhere", "skill-study", 2);
            var milestone = Milestone("find-my-study", new[] { objective }, Array.Empty<MilestoneRewardBase>(), Array.Empty<MilestoneRouteDefinition>(), 1);
            var original = new MilestoneProgress("slot-a", "player-a", new[] { milestone });
            original.SetObjectiveProgressForTests(milestone, objective, 2);
            original.MarkCompletedForTests(milestone);
            original.MarkRewardClaimedForTests(milestone);

            var restored = MilestoneProgress.FromSaveData(original.ToSaveData(), new[] { milestone });

            Assert.That(restored.GetObjectiveProgress(milestone, objective), Is.EqualTo(2));
            Assert.That(restored.IsCompleted(milestone), Is.True);
            Assert.That(restored.IsRewardClaimed(milestone), Is.True);
        }

        [Test]
        public void MILESTONE_EDIT_007_ReapplyingSameDayResultDoesNotDuplicateProgressOrRewards()
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            var reward = ScriptableObject.CreateInstance<ItemMilestoneReward>();
            reward.ConfigureForTests(item, 1);
            var objective = OutsideSchoolLogObjective("study-anywhere", "skill-study", 2);
            var milestone = Milestone("find-my-study", new[] { objective }, new MilestoneRewardBase[] { reward }, Array.Empty<MilestoneRouteDefinition>(), 1);
            var progress = new StudentLifeProgress("slot-a", "player-a", 12, 10);
            progress.RecordActivityCompleted("library-study", new[] { OutsideSchoolLogCodec.EncodeDelta("library-study", "skill-study", 2) });
            var milestoneProgress = new MilestoneProgress("slot-a", "player-a", new[] { milestone });
            var inventory = new Inventory();

            milestoneProgress.ApplyDayResult(new[] { milestone }, progress, inventory, "day-1");
            milestoneProgress.TryClaimReward(milestone, inventory);
            milestoneProgress.ApplyDayResult(new[] { milestone }, progress, inventory, "day-1");
            milestoneProgress.TryClaimReward(milestone, inventory);

            Assert.That(milestoneProgress.GetObjectiveProgress(milestone, objective), Is.EqualTo(2));
            Assert.That(inventory.CountOf(item), Is.EqualTo(1));
        }

        [Test]
        public void MILESTONE_EDIT_008_DaySummaryIncludesMilestoneProgressChangesAndRecommendations()
        {
            var objective = OutsideSchoolLogObjective("study-anywhere", "skill-study", 2);
            var route = Route("library-route", objective, "library-study");
            var milestone = Milestone("find-my-study", new[] { objective }, Array.Empty<MilestoneRewardBase>(), new[] { route }, 1);
            var progress = new StudentLifeProgress("slot-a", "player-a", 12, 10);
            progress.RecordActivityCompleted("library-study", new[] { OutsideSchoolLogCodec.EncodeDelta("library-study", "skill-study", 2) });
            var milestoneProgress = new MilestoneProgress("slot-a", "player-a", new[] { milestone });

            var result = milestoneProgress.ApplyDayResult(new[] { milestone }, progress, null, "day-1");
            var summary = MilestoneDaySummary.FromApplyResult(result, new[] { milestone });

            Assert.That(summary.ProgressLines.Any(line => line.Contains("find-my-study") && line.Contains("complete")), Is.True, "Day result milestone summary missing progress/completion line.");
            Assert.That(summary.RecommendedActionIds, Does.Contain("library-study"));
        }

        private static bool Complete(MilestoneObjectiveBase objective, MilestoneEvaluationContext context)
        {
            return objective.Evaluate(in context).IsComplete;
        }

        private static T Definition<T>(string id) where T : StudentLifeDefinitionBase
        {
            var asset = ScriptableObject.CreateInstance<T>();
            asset.ConfigureForTests(id, id);
            return asset;
        }

        private static TraitMilestoneObjective TraitObjective(string id, TraitDefinition trait, int required)
        {
            var objective = ScriptableObject.CreateInstance<TraitMilestoneObjective>();
            objective.ConfigureForTests(id, trait, required);
            return objective;
        }

        private static SkillMilestoneObjective SkillObjective(string id, SkillDefinition skill, int required)
        {
            var objective = ScriptableObject.CreateInstance<SkillMilestoneObjective>();
            objective.ConfigureForTests(id, skill, required);
            return objective;
        }

        private static RelationshipMilestoneObjective RelationshipObjective(string id, RelationshipDefinition relationship, int required)
        {
            var objective = ScriptableObject.CreateInstance<RelationshipMilestoneObjective>();
            objective.ConfigureForTests(id, relationship, required);
            return objective;
        }

        private static StatusMilestoneObjective StatusObjective(string id, StatusDefinition status, int required)
        {
            var objective = ScriptableObject.CreateInstance<StatusMilestoneObjective>();
            objective.ConfigureForTests(id, status, required);
            return objective;
        }

        private static OutsideSchoolLogMilestoneObjective OutsideSchoolLogObjective(string id, string targetId, int requiredDelta)
        {
            var objective = ScriptableObject.CreateInstance<OutsideSchoolLogMilestoneObjective>();
            objective.ConfigureForTests(id, targetId, requiredDelta);
            return objective;
        }

        private static MilestoneRouteDefinition Route(string id, MilestoneObjectiveBase objective, string recommendedActionId)
        {
            var route = ScriptableObject.CreateInstance<MilestoneRouteDefinition>();
            route.ConfigureForTests(id, objective, recommendedActionId);
            return route;
        }

        private static MilestoneDefinition Milestone(string id, MilestoneObjectiveBase[] objectives, MilestoneRewardBase[] rewards, MilestoneRouteDefinition[] routes, int requiredObjectiveCount)
        {
            var milestone = ScriptableObject.CreateInstance<MilestoneDefinition>();
            milestone.ConfigureForTests(id, id, objectives, rewards, routes, requiredObjectiveCount);
            return milestone;
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Missing backing field " + name + " on " + target.GetType().Name);
            field.SetValue(target, value);
        }
    }
}
