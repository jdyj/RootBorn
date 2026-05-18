using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.WorldState;
using UnityEditor;

namespace Rootborn.Tests.EditMode.WorldState
{
    public sealed class WorldStateUsageRegistryAssetTests
    {
        [Test]
        public void WORLD_USAGE_EDIT_001_002_RegistryLoadsUsageDefinitionsAndStrategies()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            Assert.IsNotNull(registry);
            Assert.GreaterOrEqual(registry.WorldStateUsages.Length, 1);
            Assert.GreaterOrEqual(registry.WorldStateUsageConditions.Length, 1);
            Assert.GreaterOrEqual(registry.WorldStateUsageOutcomes.Length, 2);
            Assert.GreaterOrEqual(registry.WorldStateUsageRepeatPolicies.Length, 1);

            var usages = GameDataRegistryWorldStateUsageExtensions.GetWorldStateUsages(registry);
            var conditions = GameDataRegistryWorldStateUsageExtensions.GetWorldStateUsageConditions(registry);
            var policies = GameDataRegistryWorldStateUsageExtensions.GetWorldStateUsageRepeatPolicies(registry);

            Assert.AreSame(registry.WorldStateUsages, usages);
            Assert.AreSame(registry.WorldStateUsageConditions, conditions);
            Assert.AreSame(registry.WorldStateUsageRepeatPolicies, policies);
            StringAssert.StartsWith("Assets/Data/WorldState/Usage/", AssetDatabase.GetAssetPath(usages[0]));

            var cache = new WorldStateUsageLookupCache(usages);
            Assert.IsTrue(cache.TryGetById("usage.library.archive-table", out var usage));
            Assert.IsNotNull(usage.SourceFlag);
        }
    }
}
