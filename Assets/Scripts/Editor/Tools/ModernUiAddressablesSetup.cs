using System.Collections.Generic;
using Rootborn.Game.Common;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace Rootborn.Editor.Tools
{
    public static class ModernUiAddressablesSetup
    {
        private const string GroupSprites = "Sprites";

        private static readonly (string assetPath, string address)[] SheetEntries = new[]
        {
            ("Assets/modernuserinterface-win/16x16/Modern_UI_Style_1.png", ModernUISpriteAddresses.Style16),
            ("Assets/modernuserinterface-win/16x16/Modern_UI_Style_2.png", ModernUISpriteAddresses.Style16Alt),
            ("Assets/modernuserinterface-win/16x16/Modern_UI_Gamepad.png", ModernUISpriteAddresses.Gamepad16),
            ("Assets/modernuserinterface-win/32x32/Modern_UI_Style_1_32x32.png", ModernUISpriteAddresses.Style32),
            ("Assets/modernuserinterface-win/32x32/Modern_UI_Style_2_32x32.png", ModernUISpriteAddresses.Style32Alt),
            ("Assets/modernuserinterface-win/32x32/Modern_UI_Gamepad_32x32.png", ModernUISpriteAddresses.Gamepad32),
            ("Assets/modernuserinterface-win/48x48/Modern_UI_Style_1_48x48.png", ModernUISpriteAddresses.Style48),
            ("Assets/modernuserinterface-win/48x48/Modern_UI_Style_2_48x48.png", ModernUISpriteAddresses.Style48Alt),
            ("Assets/modernuserinterface-win/48x48/Modern_UI_Gamepad_48x48.png", ModernUISpriteAddresses.Gamepad48),
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

        [MenuItem("Rootborn/Modern UI/Wire Addressables")]
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
                    Debug.LogWarning($"[ROOTBORN/ModernUI] Missing sheet: {assetPath}");
                    missing++;
                    continue;
                }

                RegisterAsset(settings, groupSprites, assetPath, address, AddressablesSetup.LabelPreLoad);
                ok++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ROOTBORN/ModernUI] Addressable sheets registered: {ok} ok, {missing} missing.");
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
            Debug.Log("[ROOTBORN/ModernUI] Created new AddressableAssetSettings.");
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
                Debug.LogWarning($"[ROOTBORN/ModernUI] Asset not found: {assetPath} (address {address})");
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
