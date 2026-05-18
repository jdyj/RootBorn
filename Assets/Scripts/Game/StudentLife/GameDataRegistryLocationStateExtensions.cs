using Rootborn.Game.Common;

namespace Rootborn.Game.StudentLife
{
    public static class GameDataRegistryLocationStateExtensions
    {
        public static LocationStateDefinition[] GetLocationStates(GameDataRegistry registry)
        {
            return registry != null && registry.LocationStates != null ? registry.LocationStates : new LocationStateDefinition[0];
        }

        public static LocationStateConflictPolicyDefinition[] GetLocationStateConflictPolicies(GameDataRegistry registry)
        {
            return registry != null && registry.LocationStateConflictPolicies != null ? registry.LocationStateConflictPolicies : new LocationStateConflictPolicyDefinition[0];
        }
    }
}
