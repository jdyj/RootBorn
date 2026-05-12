using System.Collections.Generic;
using Rootborn.Game.Common;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace Rootborn.Editor.Tools
{
    public static class ModernInteriorsAddressablesSetup
    {
        private const string GroupSprites = "Sprites";

        private static readonly (string assetPath, string address)[] InstalledSheetEntries = new[]
        {
            ("Assets/moderninteriors-win/1_Interiors/16x16/Room_Builder_subfiles/Room_Builder_Floors_16x16.png", ModernInteriorsSpriteAddresses.Floors16),
            ("Assets/moderninteriors-win/1_Interiors/16x16/Room_Builder_subfiles/Room_Builder_Walls_16x16.png", ModernInteriorsSpriteAddresses.Walls16),
            ("Assets/moderninteriors-win/4_User_Interface_Elements/UI_16x16.png", ModernInteriorsSpriteAddresses.Ui16),
        };

        public static IReadOnlyList<(string assetPath, string address)> GetSheetEntries()
        {
            return ModernInteriorsSliceSetup.IsPackInstalled()
                ? InstalledSheetEntries
                : System.Array.Empty<(string assetPath, string address)>();
        }

        public static IReadOnlyList<string> FindMissingRegisteredSheetAddresses()
        {
            var missing = new List<string>();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var sheetEntries = GetSheetEntries();
            if (settings == null)
            {
                foreach (var (_, address) in sheetEntries)
                {
                    missing.Add(address);
                }

                return missing;
            }

            foreach (var (assetPath, address) in sheetEntries)
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

            foreach (var (assetPath, address) in GetSheetEntries())
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

        [MenuItem("Rootborn/Modern Interiors/Wire Addressables")]
        public static void WireSheets()
        {
            var settings = EnsureSettings();
            EnsureLabel(settings, AddressablesSetup.LabelPreLoad);
            var groupSprites = EnsureGroup(settings, GroupSprites);

            int ok = 0;
            int missing = 0;
            foreach (var (assetPath, address) in GetSheetEntries())
            {
                if (AssetDatabase.LoadAssetAtPath<Object>(assetPath) == null)
                {
                    Debug.LogWarning($"[ROOTBORN/ModernInteriors] Missing sheet: {assetPath}");
                    missing++;
                    continue;
                }

                RegisterAsset(settings, groupSprites, assetPath, address, AddressablesSetup.LabelPreLoad);
                ok++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ROOTBORN/ModernInteriors] Addressable sheets registered: {ok} ok, {missing} missing.");
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
            Debug.Log("[ROOTBORN/ModernInteriors] Created new AddressableAssetSettings.");
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
                Debug.LogWarning($"[ROOTBORN/ModernInteriors] Asset not found: {assetPath} (address {address})");
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