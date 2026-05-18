using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.DiscoveryClues;
using UnityEditor;

namespace Rootborn.Tests.EditMode.DiscoveryClues
{
    public sealed class DiscoveryClueRegistryTests
    {
        [Test]
        public void DISCOVERY_CLUE_EDIT_001_002_RegistryLoadsDefinitionsAndStrategiesFromRegisteredData()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            Assert.IsNotNull(registry);

            Assert.GreaterOrEqual(registry.DiscoveryClues.Length, 1, "Discovery clues must be serialized on GameDataRegistry, not only discovered by editor folder scan.");
            Assert.GreaterOrEqual(registry.DiscoveryClueSources.Length, 2, "A clue must be exposable through multiple registered SO source definitions.");
            Assert.GreaterOrEqual(registry.DiscoveryClueConditions.Length, 1);
            Assert.GreaterOrEqual(registry.DiscoveryClueCompletions.Length, 1);
            Assert.GreaterOrEqual(registry.DiscoveryClueOutcomes.Length, 1);

            var clues = GameDataRegistryDiscoveryClueExtensions.GetDiscoveryClues(registry);
            var sources = GameDataRegistryDiscoveryClueExtensions.GetDiscoveryClueSources(registry);
            var conditions = GameDataRegistryDiscoveryClueExtensions.GetDiscoveryClueConditions(registry);
            var completions = GameDataRegistryDiscoveryClueExtensions.GetDiscoveryClueCompletions(registry);
            var outcomes = GameDataRegistryDiscoveryClueExtensions.GetDiscoveryClueOutcomes(registry);

            Assert.AreSame(registry.DiscoveryClues, clues);
            Assert.AreSame(registry.DiscoveryClueSources, sources);
            Assert.AreSame(registry.DiscoveryClueConditions, conditions);
            Assert.AreSame(registry.DiscoveryClueCompletions, completions);
            Assert.AreSame(registry.DiscoveryClueOutcomes, outcomes);
            StringAssert.StartsWith("Assets/Data/DiscoveryClues/", AssetDatabase.GetAssetPath(clues[0]));
            Assert.IsTrue(new DiscoveryClueLookupCache(clues).TryGetById(clues[0].Id, out _));
        }

        [Test]
        public void DISCOVERY_CLUE_EDIT_005_RegisteredPlayClueExposesNpcBoardMapEncyclopediaAndLocationSources()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            var cache = new DiscoveryClueLookupCache(GameDataRegistryDiscoveryClueExtensions.GetDiscoveryClues(registry));
            Assert.IsTrue(cache.TryGetById("clue.discovery.play-loop", out var clue));
            var kinds = new HashSet<DiscoveryClueSourceKind>();
            for (int i = 0; i < clue.Sources.Count; i++)
            {
                if (clue.Sources[i] != null) kinds.Add(clue.Sources[i].Kind);
            }

            Assert.IsTrue(kinds.Contains(DiscoveryClueSourceKind.NpcDialogue));
            Assert.IsTrue(kinds.Contains(DiscoveryClueSourceKind.BoardPost));
            Assert.IsTrue(kinds.Contains(DiscoveryClueSourceKind.MapHint));
            Assert.IsTrue(kinds.Contains(DiscoveryClueSourceKind.EncyclopediaUnknown));
            Assert.IsTrue(kinds.Contains(DiscoveryClueSourceKind.LocationTrace));
        }
    }
}
