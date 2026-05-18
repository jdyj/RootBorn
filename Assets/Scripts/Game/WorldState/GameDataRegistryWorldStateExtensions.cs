using System;
using System.Runtime.CompilerServices;
using Rootborn.Game.Common;

namespace Rootborn.Game.WorldState
{
    public static class GameDataRegistryWorldStateExtensions
    {
        private static readonly ConditionalWeakTable<GameDataRegistry, WorldStateRegistryData> Data = new ConditionalWeakTable<GameDataRegistry, WorldStateRegistryData>();

        public static void ConfigureWorldStateForTests(
            this GameDataRegistry registry,
            WorldStateScopeDefinition[] scopes,
            WorldStateFlagDefinition[] flags,
            WorldStateConditionBase[] conditions,
            WorldStateEffectBase[] effects)
        {
            if (registry == null) return;
            Data.Remove(registry);
            Data.Add(registry, new WorldStateRegistryData(scopes, flags, conditions, effects));
        }

        public static WorldStateScopeDefinition[] GetWorldStateScopes(GameDataRegistry registry)
        {
            if (registry == null) return Array.Empty<WorldStateScopeDefinition>();
            if (registry.WorldStateScopes != null && registry.WorldStateScopes.Length > 0) return registry.WorldStateScopes;
            return Data.TryGetValue(registry, out var data) ? data.Scopes : Array.Empty<WorldStateScopeDefinition>();
        }

        public static WorldStateFlagDefinition[] GetWorldStateFlags(GameDataRegistry registry)
        {
            if (registry == null) return Array.Empty<WorldStateFlagDefinition>();
            if (registry.WorldStateFlags != null && registry.WorldStateFlags.Length > 0) return registry.WorldStateFlags;
            return Data.TryGetValue(registry, out var data) ? data.Flags : Array.Empty<WorldStateFlagDefinition>();
        }

        public static WorldStateConditionBase[] GetWorldStateConditions(GameDataRegistry registry)
        {
            if (registry == null) return Array.Empty<WorldStateConditionBase>();
            if (registry.WorldStateConditions != null && registry.WorldStateConditions.Length > 0) return registry.WorldStateConditions;
            return Data.TryGetValue(registry, out var data) ? data.Conditions : Array.Empty<WorldStateConditionBase>();
        }

        public static WorldStateEffectBase[] GetWorldStateEffects(GameDataRegistry registry)
        {
            if (registry == null) return Array.Empty<WorldStateEffectBase>();
            if (registry.WorldStateEffects != null && registry.WorldStateEffects.Length > 0) return registry.WorldStateEffects;
            return Data.TryGetValue(registry, out var data) ? data.Effects : Array.Empty<WorldStateEffectBase>();
        }

        private sealed class WorldStateRegistryData
        {
            public readonly WorldStateScopeDefinition[] Scopes;
            public readonly WorldStateFlagDefinition[] Flags;
            public readonly WorldStateConditionBase[] Conditions;
            public readonly WorldStateEffectBase[] Effects;

            public WorldStateRegistryData(WorldStateScopeDefinition[] scopes, WorldStateFlagDefinition[] flags, WorldStateConditionBase[] conditions, WorldStateEffectBase[] effects)
            {
                Scopes = scopes ?? Array.Empty<WorldStateScopeDefinition>();
                Flags = flags ?? Array.Empty<WorldStateFlagDefinition>();
                Conditions = conditions ?? Array.Empty<WorldStateConditionBase>();
                Effects = effects ?? Array.Empty<WorldStateEffectBase>();
            }
        }
    }
}
