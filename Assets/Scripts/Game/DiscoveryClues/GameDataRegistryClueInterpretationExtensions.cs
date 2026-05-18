using Rootborn.Game.Common;

namespace Rootborn.Game.DiscoveryClues
{
    public static class GameDataRegistryClueInterpretationExtensions
    {
        public static ClueInterpretationDefinition[] GetClueInterpretations(GameDataRegistry registry)
        {
            return registry != null && registry.ClueInterpretations != null ? registry.ClueInterpretations : new ClueInterpretationDefinition[0];
        }

        public static ClueInterpretationSourceDefinition[] GetClueInterpretationSources(GameDataRegistry registry)
        {
            return registry != null && registry.ClueInterpretationSources != null ? registry.ClueInterpretationSources : new ClueInterpretationSourceDefinition[0];
        }

        public static ClueInterpretationConditionBase[] GetClueInterpretationConditions(GameDataRegistry registry)
        {
            return registry != null && registry.ClueInterpretationConditions != null ? registry.ClueInterpretationConditions : new ClueInterpretationConditionBase[0];
        }

        public static ClueInterpretationOutcomeBase[] GetClueInterpretationOutcomes(GameDataRegistry registry)
        {
            return registry != null && registry.ClueInterpretationOutcomes != null ? registry.ClueInterpretationOutcomes : new ClueInterpretationOutcomeBase[0];
        }

        public static ClueInterpretationPolicyDefinition[] GetClueInterpretationPolicies(GameDataRegistry registry)
        {
            return registry != null && registry.ClueInterpretationPolicies != null ? registry.ClueInterpretationPolicies : new ClueInterpretationPolicyDefinition[0];
        }
    }
}
