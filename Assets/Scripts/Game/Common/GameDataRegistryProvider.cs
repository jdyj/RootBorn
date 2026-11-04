using System;

namespace Rootborn.Game.Common
{
    public static class GameDataRegistryProvider
    {
        private static IGameDataRegistryProvider s_provider = new DefaultGameDataRegistryProvider();
        private static GameDataRegistry s_overrideRegistry;
        private static GameDataRegistry s_cachedRegistry;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlayMode()
        {
            // Domain reload may be disabled: no editor/test registry is valid for a new runtime session.
            s_overrideRegistry = null;
            s_cachedRegistry = null;
            s_provider = new DefaultGameDataRegistryProvider();
        }

        public static bool TryGet(out GameDataRegistry registry)
        {
            if (s_overrideRegistry != null)
            {
                registry = s_overrideRegistry;
                s_cachedRegistry = registry;
                return true;
            }

            if (s_cachedRegistry != null)
            {
                registry = s_cachedRegistry;
                return true;
            }

            if (s_provider != null && s_provider.TryGet(out registry))
            {
                s_cachedRegistry = registry;
                return true;
            }

            registry = null;
            return false;
        }

        public static GameDataRegistry Require()
        {
            if (TryGet(out var registry)) return registry;
            throw new InvalidOperationException("GameDataRegistry is not available through GameDataRegistryProvider.");
        }

        public static void SetProviderForTests(IGameDataRegistryProvider provider)
        {
            s_provider = provider ?? new DefaultGameDataRegistryProvider();
            s_cachedRegistry = null;
        }

        public static void SetOverrideForTests(GameDataRegistry registry)
        {
            s_overrideRegistry = registry;
            s_cachedRegistry = registry;
        }

        public static void ClearOverrideForTests()
        {
            s_overrideRegistry = null;
        }

        public static void ResetCacheForTests()
        {
            s_cachedRegistry = null;
            s_provider = new DefaultGameDataRegistryProvider();
        }

        private sealed class DefaultGameDataRegistryProvider : IGameDataRegistryProvider
        {
            public bool TryGet(out GameDataRegistry registry)
            {
                registry = Rootborn.Game.Managers.Managers.Data != null ? Rootborn.Game.Managers.Managers.Data.Registry : null;
                return registry != null;
            }

            public GameDataRegistry Require()
            {
                if (TryGet(out var registry)) return registry;
                throw new InvalidOperationException("Managers.Data.Registry is not initialized.");
            }
        }
    }
}
