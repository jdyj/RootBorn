using System.Collections.Generic;
using Rootborn.Game.Common;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace Rootborn.Editor.Tools
{
    public static class ModernFarmAddressablesSetup
    {
        private const string GroupSprites = "Sprites";

        private static readonly (string assetPath, string address)[] SheetEntries = new[]
        {
            ("Assets/Modern_Farm_v1.2/16x16/1_Terrains_16x16.png", ModernFarmSpriteAddresses.Terrain16),
            ("Assets/Modern_Farm_v1.2/16x16/2_Fences_16x16.png", ModernFarmSpriteAddresses.Fences16),
            ("Assets/Modern_Farm_v1.2/16x16/3_Props_and_Buildings_16x16.png", ModernFarmSpriteAddresses.Props16),
            ("Assets/Modern_Farm_v1.2/16x16/4_Crops_16x16.png", ModernFarmSpriteAddresses.Crops16),
            ("Assets/Modern_Farm_v1.2/16x16/5_Fruit_Trees.png", ModernFarmSpriteAddresses.FruitTrees16),
            ("Assets/Modern_Farm_v1.2/16x16/6_Trees_16x16.png", ModernFarmSpriteAddresses.Trees16),
            ("Assets/Modern_Farm_v1.2/16x16/7_Pickup_Items_16x16.png", ModernFarmSpriteAddresses.Pickups16),
        };

        public static IReadOnlyList<(string assetPath, string address)> GetSheetEntries()
        {
            return SheetEntries;
        }

        public static IReadOnlyList<string> FindMissingRegisteredSheetAddresses()
        {
            var missing = new List<string>();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                foreach (var (_, address) in SheetEntries)
                {
                    missing.Add(address);
                }

                return missing;
            }

            foreach (var (assetPath, address) in SheetEntries)
            {
                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                var entry = settings.FindAssetEntry(guid);
                if (entry == null || entry.address != address)
                {
                    missing.Add(address);
                }
            }

            return missing;
        }

        public static IReadOnlyList<string> FindRegisteredSheetAddressesMissingPreloadLabel()
        {
            var missing = new List<string>();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                return missing;
            }

            foreach (var (assetPath, address) in SheetEntries)
            {
                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                var entry = settings.FindAssetEntry(guid);
                if (entry != null && entry.address == address && !entry.labels.Contains(AddressablesSetup.LabelPreLoad))
                {
                    missing.Add(address);
                }
            }

            return missing;
        }

        [MenuItem("Rootborn/Modern Farm/Wire Addressables")]
        public static void WireSheets()
        {
            var settings = EnsureSettings();
            EnsureLabel(settings, AddressablesSetup.LabelPreLoad);
            var groupSprites = EnsureGroup(settings, GroupSprites);

            int ok = 0;
            int missing = 0;
            foreach (var (assetPath, address) in SheetEntries)
            {
                if (AssetDatabase.LoadAssetAtPath<Object>(assetPath) == null)
                {
                    Debug.LogWarning($"[ROOTBORN/ModernFarm] Missing sheet: {assetPath}");
                    missing++;
                    continue;
                }

                RegisterAsset(settings, groupSprites, assetPath, address, AddressablesSetup.LabelPreLoad);
                ok++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ROOTBORN/ModernFarm] Addressable sheets registered: {ok} ok, {missing} missing.");
        }

        private static AddressableAssetSettings EnsureSettings()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings != null)
            {
                return settings;
            }

            const string folder = "Assets/AddressableAssetsData";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder("Assets", "AddressableAssetsData");
            }

            settings = AddressableAssetSettings.Create(folder, "AddressableAssetSettings", true, true);
            AddressableAssetSettingsDefaultObject.Settings = settings;
            Debug.Log("[ROOTBORN/ModernFarm] Created new AddressableAssetSettings.");
            return settings;
        }

        private static void EnsureLabel(AddressableAssetSettings settings, string label)
        {
            if (!settings.GetLabels().Contains(label))
            {
                settings.AddLabel(label);
            }
        }

        private static AddressableAssetGroup EnsureGroup(AddressableAssetSettings settings, string groupName)
        {
            var existing = settings.FindGroup(groupName);
            if (existing != null)
            {
                return existing;
            }

            var group = settings.CreateGroup(groupName, false, false, false, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));

            var bundleSchema = group.GetSchema<BundledAssetGroupSchema>();
            if (bundleSchema != null)
            {
                bundleSchema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
                bundleSchema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
                bundleSchema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
                bundleSchema.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
            }

            return group;
        }

        private static void RegisterAsset(AddressableAssetSettings settings, AddressableAssetGroup group,
            string assetPath, string address, string addLabel)
        {
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
            {
                Debug.LogWarning($"[ROOTBORN/ModernFarm] Asset not found: {assetPath} (address {address})");
                return;
            }

            var entry = settings.CreateOrMoveEntry(guid, group);
            entry.address = address;
            if (!string.IsNullOrEmpty(addLabel))
            {
                entry.SetLabel(addLabel, true, true);
            }
        }
    }
}
