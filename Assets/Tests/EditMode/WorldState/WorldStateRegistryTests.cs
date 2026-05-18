using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.WorldState;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode.WorldState
{
    public sealed class WorldStateRegistryTests
    {
        private const string RegistryPath = "Assets/Data/Registry/GameDataRegistry.asset";

        [Test]
        public void WORLD_STATE_EDIT_001_002_RegistryExtensionAndLookupCacheLoadWorldStateDefinitions()
        {
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();
            var scope = ScriptableObject.CreateInstance<WorldStateScopeDefinition>();
            scope.ConfigureForTests("scope.shared", WorldStateScopeKind.Shared, "Shared");
            var flag = ScriptableObject.CreateInstance<WorldStateFlagDefinition>();
            flag.ConfigureForTests(
                "world.test.registry-only",
                "Registry Test",
                "Loaded without runtime code edits",
                null,
                null,
                null,
                WorldStateScopeKind.Shared,
                WorldStateChangeKind.ObjectRevealed,
                "Open the changed place",
                new[] { WorldStateSummarySurface.DayResult, WorldStateSummarySurface.WorldLog },
                new[] { WorldStateBadgeKind.New, WorldStateBadgeKind.Shared },
                5,
                1);

            registry.ConfigureWorldStateForTests(new[] { scope }, new[] { flag }, new WorldStateConditionBase[0], new WorldStateEffectBase[0]);
            var cache = new GameDataLookupCache(registry);

            Assert.AreEqual(1, GameDataRegistryWorldStateExtensions.GetWorldStateScopes(registry).Length);
            Assert.AreEqual(1, GameDataRegistryWorldStateExtensions.GetWorldStateFlags(registry).Length);
            Assert.IsTrue(cache.TryGetWorldStateFlag("world.test.registry-only", out var loadedFlag));
            Assert.AreSame(flag, loadedFlag);
            Assert.AreEqual(1, cache.BuildCount);
        }

        [Test]
        public void WORLD_STATE_EDIT_001_002_ProjectRegistryLoadsWorldStateAssetsFromDataPath()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(RegistryPath);

            Assert.IsNotNull(registry, RegistryPath + " must exist.");
            Assert.GreaterOrEqual(registry.WorldStateScopes.Length, 1, "WORLD-STATE-EDIT-001 failed: registry must contain at least one world-state scope.");
            Assert.GreaterOrEqual(registry.WorldStateFlags.Length, 1, "WORLD-STATE-EDIT-002 failed: registry must contain at least one data-authored world-state flag.");

            var cache = new GameDataLookupCache(registry);
            for (int i = 0; i < registry.WorldStateFlags.Length; i++)
            {
                var flag = registry.WorldStateFlags[i];
                Assert.IsNotNull(flag, "WORLD-STATE-EDIT-001 failed: registry contains a null world-state flag.");
                StringAssert.StartsWith("Assets/Data/WorldState/Flags/", AssetDatabase.GetAssetPath(flag));
                Assert.IsTrue(cache.TryGetWorldStateFlag(flag.Id, out var cachedFlag), flag.Id + " must resolve from GameDataLookupCache.");
                Assert.AreSame(flag, cachedFlag);
            }
        }
    }
}
