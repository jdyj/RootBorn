using System.Collections.Generic;
using Rootborn.Game.Common;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace Rootborn.Editor.Tools
{
    public static class AddressablesSetup
    {
        public const string LabelPreLoad = "PreLoad";

        private const string GroupData = "Data";
        private const string GroupPrefabs = "Prefabs";
        private const string GroupTiles = "Tiles";
        private const string AddrRegistry = "data/registry";
        private const string AddrPlayerPrefab = "prefabs/player";
        private const string AddrGroundTile = "tiles/ground";

        public static IReadOnlyList<string> GetUiSpriteAddresses()
        {
            var list = new List<string>();
            foreach (var (sheetAddress, _) in ModernUISpriteAddresses.AllSheets)
            {
                list.Add(sheetAddress);
            }

            return list;
        }

        public static IReadOnlyList<(string assetPath, string address)> GetUiSpriteEntries()
        {
            return ModernUiAddressablesSetup.GetSheetEntries();
        }

        [MenuItem("Rootborn/Addressables/Wire All")]
        public static void WireAll()
        {
            ModernUiAddressablesSetup.WireSheets();
            ModernFarmAddressablesSetup.WireSheets();
            ModernInteriorsAddressablesSetup.WireSheets();
            WireCoreAssets();
        }

        public static AddressableAssetSettings EnsureSettings()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings != null) return settings;

            const string folder = "Assets/AddressableAssetsData";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder("Assets", "AddressableAssetsData");
            }
            settings = AddressableAssetSettings.Create(folder, "AddressableAssetSettings", true, true);
            AddressableAssetSettingsDefaultObject.Settings = settings;
            Debug.Log("[ROOTBORN/Addressables] Created new AddressableAssetSettings.");
            return settings;
        }

        private static void WireCoreAssets()
        {
            var settings = EnsureSettings();
            EnsureLabel(settings, LabelPreLoad);
            var groupData = EnsureGroup(settings, GroupData);
            var groupPrefabs = EnsureGroup(settings, GroupPrefabs);
            var groupTiles = EnsureGroup(settings, GroupTiles);

            RegisterAsset(settings, groupData, FindAssetPath("GameDataRegistry t:GameDataRegistry") ?? "Assets/Resources/GameDataRegistry.asset", AddrRegistry, LabelPreLoad);
            RegisterAsset(settings, groupPrefabs, "Assets/Prefabs/Player.prefab", AddrPlayerPrefab);
            RegisterAsset(settings, groupTiles, "Assets/Data/Tiles/GroundTile.asset", AddrGroundTile);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ROOTBORN/Addressables] Modern groups wired with core assets: {GroupData}, {GroupPrefabs}, {GroupTiles}.");
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
            if (existing != null) return existing;

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
            string assetPath, string address, string addLabel = null)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.LogWarning($"[ROOTBORN/Addressables] Skip register: empty path for address '{address}'.");
                return;
            }

            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
            {
                Debug.LogWarning($"[ROOTBORN/Addressables] Asset not found: {assetPath} (address {address})");
                return;
            }

            var entry = settings.CreateOrMoveEntry(guid, group);
            entry.address = address;
            if (!string.IsNullOrEmpty(addLabel))
            {
                entry.SetLabel(addLabel, true, true);
            }
        }

        private static string FindAssetPath(string filter)
        {
            var guids = AssetDatabase.FindAssets(filter);
            if (guids.Length == 0) return null;
            return AssetDatabase.GUIDToAssetPath(guids[0]);
        }
    }
}
