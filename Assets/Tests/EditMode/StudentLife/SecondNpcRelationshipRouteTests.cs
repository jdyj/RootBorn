using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Dialogue;
using Rootborn.Game.StudentLife;
using Rootborn.UI.Objectives;
using Rootborn.UI.StudentLife;
using UnityEditor;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class SecondNpcRelationshipRouteTests
    {
        private const string RegistryPath = "Assets/Data/Registry/GameDataRegistry.asset";
        private const string TeacherNpcId = "npc.teacher";
        private const string FirstGuideRelationshipId = "relationship.first-guide.trust";
        private const string TeacherRelationshipId = "relationship.teacher.support";
        private const string TeacherHelpActionId = "town.help.teacher-study-prep";
        private const string TeacherMilestoneId = "milestone.teacher-support";
        private const string TeacherMilestoneObjectiveId = "milestone.objective.teacher-support";
        private const string TeacherMilestoneRouteId = "milestone.route.teacher-support-help";
        private const string TeacherCommitmentMilestoneId = "milestone.teacher-commitment";
        private const string TeacherCommitmentObjectiveId = "milestone.objective.teacher-commitment";
        private const string TeacherCommitmentRouteId = "milestone.route.teacher-commitment-help";

        [Test]
        public void NPC_RELATIONSHIP_EDIT_002_SecondNpcRelationshipRouteDataIsRegistered()
        {
            var registry = LoadRegistry();
            var teacher = registry.Npcs.FirstOrDefault(candidate => candidate != null && candidate.Id == TeacherNpcId);
            var schedule = registry.NpcSchedules.FirstOrDefault(candidate => candidate != null && candidate.Npc == teacher);
            var relationship = registry.Relationships.FirstOrDefault(candidate => candidate != null && candidate.Id == TeacherRelationshipId);
            var firstGuideRelationship = registry.Relationships.FirstOrDefault(candidate => candidate != null && candidate.Id == FirstGuideRelationshipId);
            var action = registry.TownHelpActions.FirstOrDefault(candidate => candidate != null && candidate.Id == TeacherHelpActionId);
            var milestone = registry.Milestones.FirstOrDefault(candidate => candidate != null && candidate.Id == TeacherMilestoneId);
            var objective = registry.MilestoneObjectives.OfType<RelationshipMilestoneObjective>().FirstOrDefault(candidate => candidate != null && candidate.Id == TeacherMilestoneObjectiveId);
            var route = registry.MilestoneRoutes.FirstOrDefault(candidate => candidate != null && candidate.Id == TeacherMilestoneRouteId);

            Assert.IsNotNull(teacher, "Second NPC relationship route must use a registered Teacher NPC.");
            Assert.IsNotNull(schedule, "Teacher NPC must have a registered schedule so the player can reach the NPC in Town.");
            Assert.IsNotNull(relationship, "Teacher support relationship must be registered.");
            Assert.AreNotSame(firstGuideRelationship, relationship, "Second NPC route must not reuse the First Guide relationship asset.");
            Assert.AreEqual(TeacherNpcId, relationship.TargetId, "Teacher relationship must target the Teacher NPC.");
            Assert.IsNotNull(action, "Teacher help action must be registered.");
            Assert.AreEqual(TeacherNpcId, action.NpcId, "Teacher help action must target the Teacher NPC.");
            Assert.IsTrue(action.Outcomes.OfType<HelpActionRelationshipDeltaOutcome>().Any(outcome => outcome.Relationship == relationship && outcome.Delta > 0), "Teacher help action must increase Teacher support through a relationship outcome asset.");
            Assert.IsNotNull(milestone, "Teacher support milestone must be registered.");
            Assert.IsNotNull(objective, "Teacher support relationship objective must be registered.");
            Assert.AreSame(relationship, objective.Relationship, "Teacher relationship objective must evaluate the registered Teacher support relationship.");
            Assert.GreaterOrEqual(objective.RequiredValue, 2, "Teacher relationship objective should require the help action's support gain.");
            Assert.IsNotNull(route, "Teacher support milestone route must be registered.");
            Assert.IsFalse(string.IsNullOrEmpty(route.DisplayNameKey), "Teacher milestone route must expose an Objective Journal display key.");
            Assert.AreSame(objective, route.Objective, "Teacher milestone route must point at the relationship objective.");
            Assert.AreEqual(TeacherHelpActionId, route.RecommendedActionId, "Teacher milestone route must recommend the data-driven help action.");
            CollectionAssert.Contains(milestone.Objectives, objective, "Teacher milestone must own the relationship objective.");
            CollectionAssert.Contains(milestone.Routes, route, "Teacher milestone must own the relationship route.");
        }

        [Test]
        public void NPC_RELATIONSHIP_EDIT_004_ObjectiveJournalMilestoneSourceIncludesTeacherRouteHint()
        {
            var registry = LoadRegistry();
            var source = new MilestoneObjectiveSource();
            var items = source.BuildItems(new ObjectiveJournalContext(registry, null, null));
            var matching = items.Where(candidate => candidate.StableKey.Contains(TeacherMilestoneId)).ToArray();
            Assert.AreEqual(1, matching.Length, "Objective Journal milestone source must expose exactly one Teacher relationship route Hint.");
            StringAssert.Contains(TeacherHelpActionId, matching[0].NextHintText, "Teacher milestone Journal hint must point at the Teacher help action route.");
            Assert.AreEqual("Hints", matching[0].CategoryId, "Relationship route guidance must remain owned by ObjectiveJournalPanel Hints.");
        }

        [Test]
        public void NPC_RELATIONSHIP_EDIT_006_TeacherCommitmentRouteDataIsRegisteredBehindTeacherSupportMilestone()
        {
            var registry = LoadRegistry();
            var relationship = registry.Relationships.FirstOrDefault(candidate => candidate != null && candidate.Id == TeacherRelationshipId);
            var support = registry.Milestones.FirstOrDefault(candidate => candidate != null && candidate.Id == TeacherMilestoneId);
            var supportObjective = registry.MilestoneObjectives.OfType<RelationshipMilestoneObjective>().FirstOrDefault(candidate => candidate != null && candidate.Id == TeacherMilestoneObjectiveId);
            var commitment = registry.Milestones.FirstOrDefault(candidate => candidate != null && candidate.Id == TeacherCommitmentMilestoneId);
            var objective = registry.MilestoneObjectives.OfType<RelationshipMilestoneObjective>().FirstOrDefault(candidate => candidate != null && candidate.Id == TeacherCommitmentObjectiveId);
            var route = registry.MilestoneRoutes.FirstOrDefault(candidate => candidate != null && candidate.Id == TeacherCommitmentRouteId);

            Assert.IsNotNull(relationship, "Teacher relationship must remain registered.");
            Assert.IsNotNull(support, "Teacher support milestone must remain registered.");
            Assert.IsNotNull(supportObjective, "Teacher support objective must remain registered.");
            Assert.IsNotNull(commitment, "Teacher commitment milestone must be registered as the follow-up relationship guidance.");
            Assert.IsNotNull(objective, "Teacher commitment milestone must have a relationship objective.");
            Assert.AreSame(relationship, objective.Relationship, "Teacher commitment objective must evaluate the same Teacher relationship data.");
            Assert.Greater(objective.RequiredValue, supportObjective.RequiredValue, "Teacher commitment must require more relationship progress than the first Teacher support milestone.");
            Assert.IsNotNull(route, "Teacher commitment route must be registered.");
            Assert.IsFalse(string.IsNullOrEmpty(route.DisplayNameKey), "Teacher commitment route must expose Objective Journal text.");
            Assert.AreSame(objective, route.Objective, "Teacher commitment route must point at the commitment objective.");
            Assert.AreEqual(TeacherHelpActionId, route.RecommendedActionId, "Teacher commitment route must recommend the data-driven Teacher help action.");
            CollectionAssert.Contains(commitment.Objectives, objective, "Teacher commitment milestone must own the commitment objective.");
            CollectionAssert.Contains(commitment.Routes, route, "Teacher commitment milestone must own the commitment route.");
            CollectionAssert.Contains(ReadPrerequisiteMilestones(commitment), support, "Teacher commitment milestone must be gated behind Teacher support completion, not shown as an always-on hint.");
        }

        private static MilestoneDefinition[] ReadPrerequisiteMilestones(MilestoneDefinition milestone)
        {
            var property = typeof(MilestoneDefinition).GetProperty("PrerequisiteMilestones", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(property, "MilestoneDefinition must expose data-driven prerequisite milestones for follow-up relationship guidance.");
            return property.GetValue(milestone) as MilestoneDefinition[] ?? Array.Empty<MilestoneDefinition>();
        }

        private static GameDataRegistry LoadRegistry()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(RegistryPath);
            Assert.IsNotNull(registry, "GameDataRegistry missing at " + RegistryPath);
            return registry;
        }
    }
}
