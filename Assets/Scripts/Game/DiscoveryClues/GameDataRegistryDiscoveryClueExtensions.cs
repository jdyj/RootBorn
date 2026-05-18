using Rootborn.Game.Common;

namespace Rootborn.Game.DiscoveryClues
{
    public static class GameDataRegistryDiscoveryClueExtensions
    {
        public static DiscoveryClueDefinition[] GetDiscoveryClues(GameDataRegistry registry)
        {
            return registry != null && registry.DiscoveryClues != null ? registry.DiscoveryClues : new DiscoveryClueDefinition[0];
        }

        public static DiscoveryClueSourceDefinition[] GetDiscoveryClueSources(GameDataRegistry registry)
        {
            return registry != null && registry.DiscoveryClueSources != null ? registry.DiscoveryClueSources : new DiscoveryClueSourceDefinition[0];
        }

        public static DiscoveryClueConditionBase[] GetDiscoveryClueConditions(GameDataRegistry registry)
        {
            return registry != null && registry.DiscoveryClueConditions != null ? registry.DiscoveryClueConditions : new DiscoveryClueConditionBase[0];
        }

        public static DiscoveryClueCompletionBase[] GetDiscoveryClueCompletions(GameDataRegistry registry)
        {
            return registry != null && registry.DiscoveryClueCompletions != null ? registry.DiscoveryClueCompletions : new DiscoveryClueCompletionBase[0];
        }

        public static DiscoveryClueOutcomeBase[] GetDiscoveryClueOutcomes(GameDataRegistry registry)
        {
            return registry != null && registry.DiscoveryClueOutcomes != null ? registry.DiscoveryClueOutcomes : new DiscoveryClueOutcomeBase[0];
        }
    }
}
