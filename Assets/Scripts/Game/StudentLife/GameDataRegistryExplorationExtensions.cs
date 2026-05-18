using Rootborn.Game.Common;

namespace Rootborn.Game.StudentLife
{
    public static class GameDataRegistryExplorationExtensions
    {
        public static ExplorationInteractionDefinition[] GetExplorationInteractions(this GameDataRegistry registry)
        {
            return registry != null && registry.ExplorationInteractions != null ? registry.ExplorationInteractions : new ExplorationInteractionDefinition[0];
        }

        public static ExplorationChoiceDefinition[] GetExplorationChoices(this GameDataRegistry registry)
        {
            return registry != null && registry.ExplorationChoices != null ? registry.ExplorationChoices : new ExplorationChoiceDefinition[0];
        }

        public static ExplorationConditionBase[] GetExplorationConditions(this GameDataRegistry registry)
        {
            return registry != null && registry.ExplorationConditions != null ? registry.ExplorationConditions : new ExplorationConditionBase[0];
        }

        public static ExplorationOutcomeBase[] GetExplorationOutcomes(this GameDataRegistry registry)
        {
            return registry != null && registry.ExplorationOutcomes != null ? registry.ExplorationOutcomes : new ExplorationOutcomeBase[0];
        }

        public static ExplorationRiskPolicyDefinition[] GetExplorationRiskPolicies(this GameDataRegistry registry)
        {
            return registry != null && registry.ExplorationRiskPolicies != null ? registry.ExplorationRiskPolicies : new ExplorationRiskPolicyDefinition[0];
        }
    }
}
