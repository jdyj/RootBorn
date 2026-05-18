using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.DiscoveryClues;
using UnityEditor;

namespace Rootborn.Tests.EditMode.DiscoveryClues
{
    public sealed class ClueInterpretationRegistryTests
    {
        [Test]
        public void CLUE_INTERPRET_EDIT_001_002_RegistryAssetOwnsInterpretationDefinitionsAndStrategies()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            Assert.IsNotNull(registry);

            Assert.GreaterOrEqual(registry.ClueInterpretations.Length, 3, "A test clue must expose at least three interpretation paths through SO data only.");
            Assert.GreaterOrEqual(registry.ClueInterpretationSources.Length, 3);
            Assert.GreaterOrEqual(registry.ClueInterpretationConditions.Length, 1);
            Assert.GreaterOrEqual(registry.ClueInterpretationOutcomes.Length, 2);
            Assert.GreaterOrEqual(registry.ClueInterpretationPolicies.Length, 2, "At least two policy kinds must be represented as data.");

            var interpretations = GameDataRegistryClueInterpretationExtensions.GetClueInterpretations(registry);
            var sources = GameDataRegistryClueInterpretationExtensions.GetClueInterpretationSources(registry);
            var conditions = GameDataRegistryClueInterpretationExtensions.GetClueInterpretationConditions(registry);
            var outcomes = GameDataRegistryClueInterpretationExtensions.GetClueInterpretationOutcomes(registry);
            var policies = GameDataRegistryClueInterpretationExtensions.GetClueInterpretationPolicies(registry);

            Assert.AreSame(registry.ClueInterpretations, interpretations);
            Assert.AreSame(registry.ClueInterpretationSources, sources);
            Assert.AreSame(registry.ClueInterpretationConditions, conditions);
            Assert.AreSame(registry.ClueInterpretationOutcomes, outcomes);
            Assert.AreSame(registry.ClueInterpretationPolicies, policies);
            Assert.IsTrue(new ClueInterpretationLookupCache(interpretations).GetByClue("clue.discovery.play-loop").Length >= 3);
            StringAssert.StartsWith("Assets/Data/DiscoveryClues/Interpretations/", AssetDatabase.GetAssetPath(interpretations[0]));
        }
    }
}
