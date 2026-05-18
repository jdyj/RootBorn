using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode.Portability
{
    public sealed class RegistryOnlyRuntimeProviderSourceAuditTests
    {
        private static readonly string[] RuntimeProviderPaths =
        {
            "Assets/Scripts/Game/StudentLife/GameDataRegistryExplorationExtensions.cs",
            "Assets/Scripts/Game/StudentLife/GameDataRegistryLocationStateExtensions.cs",
            "Assets/Scripts/Game/WorldState/GameDataRegistryWorldStateUsageExtensions.cs",
            "Assets/Scripts/Game/DiscoveryClues/GameDataRegistryDiscoveryClueExtensions.cs",
            "Assets/Scripts/Game/DiscoveryClues/GameDataRegistryClueInterpretationExtensions.cs"
        };

        [Test]
        public void PORTABILITY_REGISTRY_001_RuntimeProvidersDoNotUseEditorAssetDatabaseScans()
        {
            for (int i = 0; i < RuntimeProviderPaths.Length; i++)
            {
                string source = File.ReadAllText(RuntimeProviderPaths[i]);
                StringAssert.DoesNotContain("AssetDatabase.FindAssets", source, RuntimeProviderPaths[i]);
                StringAssert.DoesNotContain("AssetDatabase.LoadAllAssetsAtPath", source, RuntimeProviderPaths[i]);
                StringAssert.DoesNotContain("AssetDatabase.LoadAssetAtPath", source, RuntimeProviderPaths[i]);
            }
        }

        [Test]
        public void PORTABILITY_REGISTRY_002_RuntimeProvidersDoNotUseResourcesFallbacks()
        {
            for (int i = 0; i < RuntimeProviderPaths.Length; i++)
            {
                string source = File.ReadAllText(RuntimeProviderPaths[i]);
                StringAssert.DoesNotContain("Resources.Load", source, RuntimeProviderPaths[i]);
                StringAssert.DoesNotContain("Resources.LoadAll", source, RuntimeProviderPaths[i]);
            }
        }

        [Test]
        public void PORTABILITY_REGISTRY_003_RuntimeProvidersDoNotReturnEmptyForNonEditorBuilds()
        {
            for (int i = 0; i < RuntimeProviderPaths.Length; i++)
            {
                string source = File.ReadAllText(RuntimeProviderPaths[i]);
                StringAssert.DoesNotContain("#if UNITY_EDITOR", source, RuntimeProviderPaths[i]);
                StringAssert.DoesNotContain("Array.Empty<", source, RuntimeProviderPaths[i]);
            }
        }
    }
}
