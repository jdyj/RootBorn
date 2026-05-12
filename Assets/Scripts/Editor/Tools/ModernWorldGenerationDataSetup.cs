using Rootborn.Game.Common;
using Rootborn.Game.WorldGeneration;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Editor.Tools
{
    public static class ModernWorldGenerationDataSetup
    {
        private const string RuntimeRegistryPath = "Assets/Resources/GameDataRegistry.asset";
        private const string DataRegistryPath = "Assets/Data/Registry/GameDataRegistry.asset";
        private const string DataRegistryFolder = "Assets/Data/Registry";
        private const string FarmTerrainPath = "Assets/Data/WorldGeneration/Terrain_Farm_Default.asset";
        private const string DefaultTerrainPath = "Assets/Data/WorldGeneration/DefaultFarmTerrainGeneration.asset";

        [MenuItem("Rootborn/Modern Farm/Wire World Generation Data")]
        public static void WireRegistry()
        {
            EnsureFolder(DataRegistryFolder);
            EnsureDataRegistryCopy();

            var terrain = AssetDatabase.LoadAssetAtPath<TerrainGenerationDefinition>(FarmTerrainPath);
            if (terrain == null)
            {
                terrain = AssetDatabase.LoadAssetAtPath<TerrainGenerationDefinition>(DefaultTerrainPath);
            }

            if (terrain == null)
            {
                Debug.LogWarning($"[ROOTBORN/ModernWorldGen] Missing TerrainGenerationDefinition at {FarmTerrainPath} or {DefaultTerrainPath}.");
                return;
            }

            int wired = 0;
            wired += WireOne(RuntimeRegistryPath, terrain) ? 1 : 0;
            wired += WireOne(DataRegistryPath, terrain) ? 1 : 0;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ROOTBORN/ModernWorldGen] Wired default farm terrain generation on {wired} registry asset(s).");
        }

        private static void EnsureDataRegistryCopy()
        {
            if (AssetDatabase.LoadAssetAtPath<GameDataRegistry>(DataRegistryPath) != null)
            {
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<GameDataRegistry>(RuntimeRegistryPath) == null)
            {
                return;
            }

            AssetDatabase.CopyAsset(RuntimeRegistryPath, DataRegistryPath);
            AssetDatabase.ImportAsset(DataRegistryPath, ImportAssetOptions.ForceUpdate);
        }

        private static bool WireOne(string registryPath, TerrainGenerationDefinition terrain)
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(registryPath);
            if (registry == null)
            {
                return false;
            }

            var serialized = new SerializedObject(registry);
            var property = serialized.FindProperty("_defaultFarmTerrainGeneration");
            if (property == null)
            {
                Debug.LogWarning($"[ROOTBORN/ModernWorldGen] Registry missing _defaultFarmTerrainGeneration: {registryPath}");
                return false;
            }

            property.objectReferenceValue = terrain;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(registry);
            return true;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            var name = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
