using System.Linq;
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
            Assert.AreSame(objective, route.Objective, "Teacher milestone route must point at the relationship objective.");
            Assert.AreEqual(TeacherHelpActionId, route.RecommendedActionId, "Teacher milestone route must recommend the data-driven help action.");
            CollectionAssert.Contains(milestone.Objectives, objective, "Teacher milestone must own the relationship objective.");
            CollectionAssert.Contains(milestone.Routes, route, "Teacher milestone must own the relationship route.");
        }

        [Test]
        public void NPC_RELATIONSHIP_EDIT_003_TownHelpRuntimeExposesMultipleDataDrivenActions()
        {
            var registry = LoadRegistry();
            var actions = TownHelpActionRuntimeInstaller.ResolveActionsForTests(registry);
            Assert.GreaterOrEqual(actions.Length, 2, "Town Help runtime must expose more than the First Guide sample action.");
            Assert.IsTrue(actions.Any(action => action != null && action.Id == TeacherHelpActionId), "Town Help runtime action resolution must include the Teacher relationship action.");
            Assert.IsTrue(actions.Any(action => action != null && action.Id == "town.help.first-guide-errand"), "Town Help runtime must keep the First Guide action available while adding the second NPC route.");
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

        private static GameDataRegistry LoadRegistry()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(RegistryPath);
            Assert.IsNotNull(registry, "GameDataRegistry missing at " + RegistryPath);
            return registry;
        }
    }
}
