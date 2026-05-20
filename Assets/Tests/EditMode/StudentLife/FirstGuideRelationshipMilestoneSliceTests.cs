using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Dialogue;
using Rootborn.Game.StudentLife;
using UnityEditor;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class FirstGuideRelationshipMilestoneSliceTests
    {
        private const string RegistryPath = "Assets/Data/Registry/GameDataRegistry.asset";
        private const string FirstGuideNpcId = "npc.first-guide";
        private const string FirstGuideRelationshipId = "relationship.first-guide.trust";
        private const string FirstGuideHelpActionId = "town.help.first-guide-errand";
        private const string FirstGuideMilestoneId = "milestone.first-guide-trust";
        private const string FirstGuideMilestoneObjectiveId = "milestone.objective.first-guide-trust";
        private const string FirstGuideMilestoneRouteId = "milestone.route.first-guide-trust-help";

        [Test]
        public void NPC_RELATIONSHIP_EDIT_001_FirstGuideRelationshipMilestoneDataIsRegistered()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(RegistryPath);
            Assert.IsNotNull(registry, "GameDataRegistry must exist for First Guide relationship milestone data wiring.");

            var npc = registry.Npcs.FirstOrDefault(candidate => candidate != null && candidate.Id == FirstGuideNpcId);
            var schedule = registry.NpcSchedules.FirstOrDefault(candidate => candidate != null && candidate.Npc == npc);
            var relationship = registry.Relationships.FirstOrDefault(candidate => candidate != null && candidate.Id == FirstGuideRelationshipId);
            var action = registry.TownHelpActions.FirstOrDefault(candidate => candidate != null && candidate.Id == FirstGuideHelpActionId);
            var milestone = registry.Milestones.FirstOrDefault(candidate => candidate != null && candidate.Id == FirstGuideMilestoneId);
            var objective = registry.MilestoneObjectives.OfType<RelationshipMilestoneObjective>().FirstOrDefault(candidate => candidate != null && candidate.Id == FirstGuideMilestoneObjectiveId);
            var route = registry.MilestoneRoutes.FirstOrDefault(candidate => candidate != null && candidate.Id == FirstGuideMilestoneRouteId);

            Assert.IsNotNull(npc, "First Guide NPC must be registered.");
            Assert.IsNotNull(schedule, "First Guide NPC must have a registered schedule so the player can reach the NPC in Town.");
            Assert.IsNotNull(relationship, "First Guide trust relationship must be registered.");
            Assert.IsNotNull(action, "First Guide help action must be registered.");
            Assert.AreEqual(FirstGuideNpcId, action.NpcId, "First Guide help action must remain tied to the scheduled First Guide NPC.");
            Assert.IsTrue(action.Outcomes.OfType<HelpActionRelationshipDeltaOutcome>().Any(outcome => outcome.Relationship == relationship && outcome.Delta > 0), "First Guide help action must increase First Guide trust through a relationship outcome asset.");

            Assert.IsNotNull(milestone, "First Guide relationship milestone must be registered.");
            Assert.IsNotNull(objective, "First Guide relationship milestone objective must be registered.");
            Assert.AreSame(relationship, objective.Relationship, "First Guide relationship objective must evaluate the registered First Guide trust relationship.");
            Assert.GreaterOrEqual(objective.RequiredValue, 2, "First Guide relationship objective should require the existing help action's trust gain.");
            Assert.IsNotNull(route, "First Guide relationship milestone route must be registered.");
            Assert.AreSame(objective, route.Objective, "First Guide milestone route must point at the relationship objective.");
            Assert.AreEqual(FirstGuideHelpActionId, route.RecommendedActionId, "First Guide milestone route must recommend the data-driven help action.");
            CollectionAssert.Contains(milestone.Objectives, objective, "First Guide milestone must own the relationship objective.");
            CollectionAssert.Contains(milestone.Routes, route, "First Guide milestone must own the relationship route.");
        }
    }
}