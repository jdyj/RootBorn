using System.IO;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.StudentLife;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class TownExplorationDiscoveryTests
    {
        private const string RegistryPath = "Assets/Data/Registry/GameDataRegistry.asset";

        [Test]
        public void EXPLORE_EDIT_001_DiscoveryAndLocationDefinitionsLoadFromRegistryDataPath()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(RegistryPath);

            Assert.IsNotNull(registry, RegistryPath + " must exist.");
            Assert.IsNotNull(registry.Discoveries, "Discovery definitions must be exposed by GameDataRegistry.");
            Assert.IsNotNull(registry.Locations, "Location definitions must be exposed by GameDataRegistry.");
            Assert.GreaterOrEqual(registry.Discoveries.Length, 1, "At least one discovery must be registered.");
            Assert.GreaterOrEqual(registry.Locations.Length, 1, "At least one location must be registered.");
            StringAssert.StartsWith("Assets/Data/StudentLife/Discoveries/", AssetDatabase.GetAssetPath(registry.Discoveries[0]));
            StringAssert.StartsWith("Assets/Data/StudentLife/Locations/", AssetDatabase.GetAssetPath(registry.Locations[0]));
        }

        [Test]
        public void EXPLORE_EDIT_002_RequirementsAndOutcomesRunWithoutEntityIdBranching()
        {
            var requiredDiscovery = ScriptableObject.CreateInstance<DiscoveryDefinition>();
            var requirement = ScriptableObject.CreateInstance<PreviousDiscoveryRequirement>();
            var progress = new StudentLifeProgress("slot-explore", "player-explore", 8, 8);
            try
            {
                requiredDiscovery.ConfigureForTests("discovery.old-well", "discovery.old-well", DiscoveryScope.Personal, Vector2.zero, 1f, null, null);
                requirement.ConfigureForTests(requiredDiscovery);

                Assert.IsFalse(requirement.IsSatisfied(progress));
                progress.MarkRequestApplied(DiscoveryRunner.RequestIdFor(requiredDiscovery));
                Assert.IsTrue(requirement.IsSatisfied(progress));
            }
            finally
            {
                Object.DestroyImmediate(requirement);
                Object.DestroyImmediate(requiredDiscovery);
            }
        }

        [Test]
        public void EXPLORE_EDIT_003_DiscoveryOutcomeChangesGenericGrowthState()
        {
            var trait = ScriptableObject.CreateInstance<TraitDefinition>();
            var outcome = ScriptableObject.CreateInstance<DiscoveryTraitDeltaOutcome>();
            var discovery = ScriptableObject.CreateInstance<DiscoveryDefinition>();
            var progress = new StudentLifeProgress("slot-explore", "player-explore", 8, 8);
            var runner = new DiscoveryRunner();
            try
            {
                trait.ConfigureForTests("trait.curiosity", "trait.curiosity");
                outcome.ConfigureForTests(trait, 2);
                discovery.ConfigureForTests("discovery.clocktower", "discovery.clocktower", DiscoveryScope.Personal, new Vector2(2f, -1f), 1.25f, null, new DiscoveryOutcomeBase[] { outcome });

                Assert.IsTrue(runner.TryDiscover(discovery, progress, out var result));

                string expected = "discovery:discovery.clocktower:+trait.curiosity=2";
                Assert.AreEqual(LifeActivityResultKind.Applied, result.Kind);
                Assert.AreEqual("discovery.clocktower", result.DiscoveryId);
                Assert.AreEqual(2, progress.GetTraitValue(trait));
                CollectionAssert.Contains(result.OutcomeLogIds, expected);
                CollectionAssert.Contains(progress.GetTodayActivityIds(), discovery.Id);
                CollectionAssert.Contains(progress.GetTodayResultLogIds(), expected);
            }
            finally
            {
                Object.DestroyImmediate(discovery);
                Object.DestroyImmediate(outcome);
                Object.DestroyImmediate(trait);
            }
        }

        [Test]
        public void EXPLORE_EDIT_004_SameDiscoveryDoesNotApplyTwiceAfterSaveLoad()
        {
            var trait = ScriptableObject.CreateInstance<TraitDefinition>();
            var outcome = ScriptableObject.CreateInstance<DiscoveryTraitDeltaOutcome>();
            var discovery = ScriptableObject.CreateInstance<DiscoveryDefinition>();
            var runner = new DiscoveryRunner();
            try
            {
                trait.ConfigureForTests("trait.curiosity", "trait.curiosity");
                outcome.ConfigureForTests(trait, 3);
                discovery.ConfigureForTests("discovery.clocktower", "discovery.clocktower", DiscoveryScope.Personal, Vector2.zero, 1f, null, new DiscoveryOutcomeBase[] { outcome });
                var progress = new StudentLifeProgress("slot-explore", "player-explore", 8, 8);

                Assert.IsTrue(runner.TryDiscover(discovery, progress, out var first));
                var restored = StudentLifeProgress.FromSaveData(progress.ToSaveData(), new[] { trait }, null, null);
                Assert.IsFalse(runner.TryDiscover(discovery, restored, out var duplicate));

                Assert.AreEqual(LifeActivityResultKind.Applied, first.Kind);
                Assert.AreEqual(LifeActivityResultKind.DuplicateRequest, duplicate.Kind);
                Assert.AreEqual(3, restored.GetTraitValue(trait));
                Assert.AreEqual(1, DiscoveryDayLog.FromResultLogs(restored.GetTodayResultLogIds()).DiscoveryIds.Length);
            }
            finally
            {
                Object.DestroyImmediate(discovery);
                Object.DestroyImmediate(outcome);
                Object.DestroyImmediate(trait);
            }
        }

        [Test]
        public void EXPLORE_EDIT_005_PersonalAndSharedDiscoveriesAreDataScoped()
        {
            var personal = ScriptableObject.CreateInstance<DiscoveryDefinition>();
            var shared = ScriptableObject.CreateInstance<DiscoveryDefinition>();
            try
            {
                personal.ConfigureForTests("discovery.personal", "discovery.personal", DiscoveryScope.Personal, Vector2.zero, 1f, null, null);
                shared.ConfigureForTests("discovery.shared", "discovery.shared", DiscoveryScope.SharedWorld, Vector2.one, 1f, null, null);

                Assert.AreEqual(DiscoveryScope.Personal, personal.Scope);
                Assert.AreEqual(DiscoveryScope.SharedWorld, shared.Scope);
            }
            finally
            {
                Object.DestroyImmediate(personal);
                Object.DestroyImmediate(shared);
            }
        }

        [Test]
        public void EXPLORE_EDIT_006_SourceAvoidsEntityIdBranchingPatterns()
        {
            string[] files =
            {
                "Assets/Scripts/Game/StudentLife/DiscoveryDefinition.cs",
                "Assets/Scripts/Game/StudentLife/DiscoveryRunner.cs",
                "Assets/Scripts/Game/StudentLife/DiscoveryOutcomes.cs",
                "Assets/Scripts/Game/StudentLife/DiscoveryRequirements.cs",
                "Assets/Scripts/Game/StudentLife/DiscoveryPointInteractor.cs"
            };

            for (int i = 0; i < files.Length; i++)
            {
                Assert.IsTrue(File.Exists(files[i]), files[i] + " must exist.");
                string source = File.ReadAllText(files[i]);
                StringAssert.DoesNotContain("discoveryId ==", source);
                StringAssert.DoesNotContain("locationId ==", source);
                StringAssert.DoesNotContain("knowledgeId ==", source);
                StringAssert.DoesNotContain("questId ==", source);
                StringAssert.DoesNotContain("careerId ==", source);
                StringAssert.DoesNotContain("switch (discoveryId", source);
                StringAssert.DoesNotContain("switch (locationId", source);
            }
        }
    }
}
