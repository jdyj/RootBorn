using Rootborn.Game.Common;

namespace Rootborn.Game.WorldState
{
    public static class GameDataRegistryWorldStateUsageExtensions
    {
        public static WorldStateUsageDefinition[] GetWorldStateUsages(GameDataRegistry registry)
        {
            return registry != null && registry.WorldStateUsages != null ? registry.WorldStateUsages : new WorldStateUsageDefinition[0];
        }

        public static WorldStateUsageConditionBase[] GetWorldStateUsageConditions(GameDataRegistry registry)
        {
            return registry != null && registry.WorldStateUsageConditions != null ? registry.WorldStateUsageConditions : new WorldStateUsageConditionBase[0];
        }

        public static WorldStateUsageOutcomeBase[] GetWorldStateUsageOutcomes(GameDataRegistry registry)
        {
            return registry != null && registry.WorldStateUsageOutcomes != null ? registry.WorldStateUsageOutcomes : new WorldStateUsageOutcomeBase[0];
        }

        public static WorldStateUsageRepeatPolicyDefinition[] GetWorldStateUsageRepeatPolicies(GameDataRegistry registry)
        {
            return registry != null && registry.WorldStateUsageRepeatPolicies != null ? registry.WorldStateUsageRepeatPolicies : new WorldStateUsageRepeatPolicyDefinition[0];
        }
    }
}
