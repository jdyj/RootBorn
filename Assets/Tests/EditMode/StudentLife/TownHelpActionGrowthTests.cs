using System.IO;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.StudentLife;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class TownHelpActionGrowthTests
    {
        private const string RegistryPath = "Assets/Data/Registry/GameDataRegistry.asset";

        [Test]
        public void HELP_EDIT_001_HelpActionDefinitionsLoadFromRegistryDataPath()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(RegistryPath);

            Assert.IsNotNull(registry, RegistryPath + " must exist.");
            Assert.IsNotNull(registry.TownHelpActions, "Town help actions must be exposed by GameDataRegistry.");
            Assert.GreaterOrEqual(registry.TownHelpActions.Length, 1, "At least one town help action must be registered.");
            StringAssert.StartsWith("Assets/Data/StudentLife/TownHelpActions/", AssetDatabase.GetAssetPath(registry.TownHelpActions[0]));
        }

        [Test]
        public void HELP_EDIT_002_RequirementsAndOutcomesRunWithoutEntityIdBranching()
        {
            var relationship = ScriptableObject.CreateInstance<RelationshipDefinition>();
            var requirement = ScriptableObject.CreateInstance<HelpActionRelationshipThresholdRequirement>();
            var progress = new StudentLifeProgress("slot-help", "player-help", 8, 8);
            try
            {
                relationship.ConfigureForTests("relationship.neighbor", "relationship.neighbor");
                requirement.ConfigureForTests(relationship, 2);

                Assert.IsFalse(requirement.IsSatisfied(progress));
                progress.AddRelationship(relationship, 2, "setup");
                Assert.IsTrue(requirement.IsSatisfied(progress));
            }
            finally
            {
                Object.DestroyImmediate(requirement);
                Object.DestroyImmediate(relationship);
            }
        }

        [Test]
        public void HELP_EDIT_003_HelpActionOutcomeChangesGenericGrowthState()
        {
            var trait = ScriptableObject.CreateInstance<TraitDefinition>();
            var outcome = ScriptableObject.CreateInstance<HelpActionTraitDeltaOutcome>();
            var action = ScriptableObject.CreateInstance<TownHelpActionDefinition>();
            var progress = new StudentLifeProgress("slot-help", "player-help", 8, 8);
            var runner = new TownHelpActionRunner();
            try
            {
                trait.ConfigureForTests("trait.helpfulness", "trait.helpfulness");
                outcome.ConfigureForTests(trait, 3);
                action.ConfigureForTests("town.help.neighbor", "town.help.neighbor", "npc.neighbor", "location.square", 20, 1, 0, 0, null, new HelpActionOutcomeBase[] { outcome });

                Assert.IsTrue(runner.TryPerform(action, progress, "help-request-1", out var result));

                string expected = "town-help:town.help.neighbor:+trait.helpfulness=3";
                Assert.AreEqual(LifeActivityResultKind.Applied, result.Kind);
                Assert.AreEqual("town.help.neighbor", result.ActionId);
                Assert.AreEqual("npc.neighbor", result.NpcId);
                Assert.AreEqual(3, progress.GetTraitValue(trait));
                CollectionAssert.Contains(result.OutcomeLogIds, expected);
                CollectionAssert.Contains(progress.GetTodayResultLogIds(), expected);
            }
            finally
            {
                Object.DestroyImmediate(action);
                Object.DestroyImmediate(outcome);
                Object.DestroyImmediate(trait);
            }
        }

        [Test]
        public void HELP_EDIT_004_DuplicateRequestIdDoesNotApplyTwice()
        {
            var trait = ScriptableObject.CreateInstance<TraitDefinition>();
            var outcome = ScriptableObject.CreateInstance<HelpActionTraitDeltaOutcome>();
            var action = ScriptableObject.CreateInstance<TownHelpActionDefinition>();
            var progress = new StudentLifeProgress("slot-help", "player-help", 8, 8);
            var runner = new TownHelpActionRunner();
            try
            {
                trait.ConfigureForTests("trait.helpfulness", "trait.helpfulness");
                outcome.ConfigureForTests(trait, 2);
                action.ConfigureForTests("town.help.neighbor", "town.help.neighbor", "npc.neighbor", "location.square", 20, 0, 0, 0, null, new HelpActionOutcomeBase[] { outcome });

                Assert.IsTrue(runner.TryPerform(action, progress, "same-help-request", out var first));
                Assert.IsFalse(runner.TryPerform(action, progress, "same-help-request", out var duplicate));

                Assert.AreEqual(LifeActivityResultKind.Applied, first.Kind);
                Assert.AreEqual(LifeActivityResultKind.DuplicateRequest, duplicate.Kind);
                Assert.AreEqual(2, progress.GetTraitValue(trait));
                Assert.AreEqual(1, TownHelpActionDayLog.FromResultLogs(progress.GetTodayResultLogIds()).ActionIds.Length);
            }
            finally
            {
                Object.DestroyImmediate(action);
                Object.DestroyImmediate(outcome);
                Object.DestroyImmediate(trait);
            }
        }

        [Test]
        public void HELP_EDIT_005_SourceAvoidsEntityIdBranchingPatterns()
        {
            string[] files =
            {
                "Assets/Scripts/Game/StudentLife/TownHelpActionDefinition.cs",
                "Assets/Scripts/Game/StudentLife/TownHelpActionRunner.cs",
                "Assets/Scripts/Game/StudentLife/TownHelpActionOutcomes.cs",
                "Assets/Scripts/Game/StudentLife/TownHelpActionRequirements.cs",
                "Assets/Scripts/Game/StudentLife/TownHelpActionInteractor.cs"
            };

            for (int i = 0; i < files.Length; i++)
            {
                Assert.IsTrue(File.Exists(files[i]), files[i] + " must exist.");
                string source = File.ReadAllText(files[i]);
                StringAssert.DoesNotContain("helpActionId ==", source);
                StringAssert.DoesNotContain("npcId ==", source);
                StringAssert.DoesNotContain("relationshipId ==", source);
                StringAssert.DoesNotContain("switch (helpActionId", source);
                StringAssert.DoesNotContain("switch (npcId", source);
                StringAssert.DoesNotContain("switch (relationshipId", source);
            }
        }
    }
}
