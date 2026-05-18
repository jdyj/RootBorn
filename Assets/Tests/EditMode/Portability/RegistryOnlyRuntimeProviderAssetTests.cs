using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.StudentLife;
using UnityEditor;

namespace Rootborn.Tests.EditMode.Portability
{
    public sealed class RegistryOnlyRuntimeProviderAssetTests
    {
        private const string RegistryPath = "Assets/Data/Registry/GameDataRegistry.asset";

        [Test]
        public void PORTABILITY_REGISTRY_009_ExplorationProviderDataIsSerializedOnGameDataRegistryAsset()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(RegistryPath);
            Assert.IsNotNull(registry);
            Assert.GreaterOrEqual(registry.ExplorationInteractions.Length, 1);
            Assert.GreaterOrEqual(registry.ExplorationChoices.Length, 1);
            Assert.GreaterOrEqual(registry.ExplorationRiskPolicies.Length, 1);

            Assert.AreSame(registry.ExplorationInteractions, registry.GetExplorationInteractions());
            Assert.AreSame(registry.ExplorationChoices, registry.GetExplorationChoices());
            Assert.AreSame(registry.ExplorationRiskPolicies, registry.GetExplorationRiskPolicies());
            StringAssert.StartsWith("Assets/Data/StudentLife/ExplorationInteractions/", AssetDatabase.GetAssetPath(registry.ExplorationInteractions[0]));
        }

        [Test]
        public void PORTABILITY_REGISTRY_010_LocationStateProviderDataIsSerializedOnGameDataRegistryAsset()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(RegistryPath);
            Assert.IsNotNull(registry);
            Assert.GreaterOrEqual(registry.LocationStates.Length, 1);
            Assert.GreaterOrEqual(registry.LocationStateConflictPolicies.Length, 1);

            Assert.AreSame(registry.LocationStates, GameDataRegistryLocationStateExtensions.GetLocationStates(registry));
            Assert.AreSame(registry.LocationStateConflictPolicies, GameDataRegistryLocationStateExtensions.GetLocationStateConflictPolicies(registry));
            StringAssert.StartsWith("Assets/Data/StudentLife/LocationStates/", AssetDatabase.GetAssetPath(registry.LocationStates[0]));
        }
    }
}
