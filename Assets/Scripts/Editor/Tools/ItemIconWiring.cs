using System.Collections.Generic;
using Rootborn.Game.Common;
using Rootborn.Game.Tools;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Editor.Tools
{
    public static class ItemIconWiring
    {
        private const string ModernFarmRoot = "Assets/Modern_Farm_v1.2";

        private static readonly Dictionary<string, string> IconMap = new()
        {
            { "Wood", ModernFarmRoot + "/16x16/Single_Files_16x16/Pickup_Items_16x16/Pickup_Fishing_Branch_16x16.png" },
            { "Stone", ModernFarmRoot + "/16x16/Single_Files_16x16/Pickup_Items_16x16/Pickup_Resource_1_16x16.png" },
            { "BareHand", ModernFarmRoot + "/Icons/Icons_16x16/Icons_16x16_Singles/Icons_16x16_Tools_Bag.png" },
            { "StoneAxe", ModernFarmRoot + "/Icons/Icons_16x16/Icons_16x16_Singles/Icons_16x16_Tools_Axe.png" },
            { "StoneHoe", ModernFarmRoot + "/Icons/Icons_16x16/Icons_16x16_Singles/Icons_16x16_Tools_Shovel.png" },
            { "StonePickaxe", ModernFarmRoot + "/Icons/Icons_16x16/Icons_16x16_Singles/Icons_16x16_Tools_Shovel.png" },
        };

        [MenuItem("Rootborn/Items/Wire Icons From Modern Farm")]
        public static void WireAll()
        {
            int wiredItems = 0;
            int wiredTools = 0;
            int missing = 0;

            var itemGuids = AssetDatabase.FindAssets("t:ItemDefinition");
            foreach (var guid in itemGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var def = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
                if (def == null || string.IsNullOrEmpty(def.Id))
                {
                    continue;
                }

                if (!TryGetIcon(def.Id, out var sprite))
                {
                    missing++;
                    continue;
                }

                AssignIconField(def, sprite);
                wiredItems++;
            }

            var toolGuids = AssetDatabase.FindAssets("t:ToolDefinition");
            foreach (var guid in toolGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var def = AssetDatabase.LoadAssetAtPath<ToolDefinition>(path);
                if (def == null || string.IsNullOrEmpty(def.Id))
                {
                    continue;
                }

                if (!TryGetIcon(def.Id, out var sprite))
                {
                    continue;
                }

                AssignIconField(def, sprite);
                wiredTools++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ROOTBORN/IconWire] Wired {wiredItems} ItemDefinition + {wiredTools} ToolDefinition Modern Farm icons. {missing} missing mappings.");
        }

        private static bool TryGetIcon(string id, out Sprite sprite)
        {
            sprite = null;
            if (!IconMap.TryGetValue(id, out var assetPath))
            {
                Debug.LogWarning($"[ROOTBORN/IconWire] No Modern Farm icon mapping for '{id}'.");
                return false;
            }

            EnsureSingleSpriteImporter(assetPath);
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
            {
                Debug.LogWarning($"[ROOTBORN/IconWire] Modern Farm icon missing for '{id}': {assetPath}");
                return false;
            }

            return true;
        }

        private static void AssignIconField(Object asset, Sprite sprite)
        {
            var so = new SerializedObject(asset);
            var prop = so.FindProperty("_icon");
            if (prop == null)
            {
                Debug.LogError($"[ROOTBORN/IconWire] {asset.name}: _icon property not found.");
                return;
            }
            prop.objectReferenceValue = sprite;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        private static void EnsureSingleSpriteImporter(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 16;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
    }
}
