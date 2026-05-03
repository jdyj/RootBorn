using System.Collections.Generic;
using System.IO;
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
        private const string GroupSprites = "Sprites";
        private const string GroupPrefabs = "Prefabs";
        private const string GroupTiles = "Tiles";

        private const string AddrRegistry = "data/registry";
        private const string AddrGroundSprite = "sprites/ground";
        private const string AddrPlayerIdle = "sprites/player/idle_down";
        private const string AddrPlayerPrefab = "prefabs/player";
        private const string AddrGroundTile = "tiles/ground";

        [MenuItem("Rootborn/Addressables/Wire All")]
        public static void WireAll()
        {
            var settings = EnsureSettings();
            EnsureLabel(settings, LabelPreLoad);

            var groupData = EnsureGroup(settings, GroupData);
            var groupSprites = EnsureGroup(settings, GroupSprites);
            var groupPrefabs = EnsureGroup(settings, GroupPrefabs);
            var groupTiles = EnsureGroup(settings, GroupTiles);

            // Registry SO (Resources/ 또는 Data/Registry/ 어디든 검색)
            string registryPath = FindAssetPath<UnityEngine.Object>("GameDataRegistry t:GameDataRegistry");
            if (string.IsNullOrEmpty(registryPath))
            {
                registryPath = "Assets/Resources/GameDataRegistry.asset";
            }
            RegisterAsset(settings, groupData, registryPath, AddrRegistry, addLabel: LabelPreLoad);

            // Ground sprite — Tile sheet sub-sprite (Tile_r2_c4 우선)
            const string TileSheetPath = "Assets/Pixelwood Valley/Pixelwood Valley 1.1.2/Tiles/Tile.png";
            var groundSubSprite = FindSubSprite(TileSheetPath, "Tile_r2_c4")
                                  ?? FindSubSprite(TileSheetPath, "Tile_r3_c4");
            if (groundSubSprite != null)
            {
                RegisterSubSprite(settings, groupSprites, TileSheetPath, groundSubSprite.name, AddrGroundSprite, LabelPreLoad);
            }

            // Player idle down (sliced sub-sprite 첫 프레임)
            const string IdleDownPath = "Assets/Pixelwood Valley/Pixelwood Valley 1.1.2/Player Character/Idle/Down.png";
            var idleSub = FindSubSprite(IdleDownPath, "Idle_Down_0");
            if (idleSub != null)
            {
                RegisterSubSprite(settings, groupSprites, IdleDownPath, idleSub.name, AddrPlayerIdle, LabelPreLoad);
            }

            // Player prefab
            RegisterAsset(settings, groupPrefabs, "Assets/Prefabs/Player.prefab", AddrPlayerPrefab);

            // Ground tile
            RegisterAsset(settings, groupTiles, "Assets/Data/Tiles/GroundTile.asset", AddrGroundTile);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ROOTBORN/Addressables] Groups wired: {GroupData}, {GroupSprites}, {GroupPrefabs}, {GroupTiles}. " +
                      $"Open Window → Asset Management → Addressables → Groups to inspect.");
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

            var template = settings.DefaultGroup != null
                ? settings.DefaultGroup.Schemas
                : null;

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
                Debug.LogWarning($"[ROOTBORN/Addressables] Skip register — empty path for address '{address}'.");
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

        private static void RegisterSubSprite(AddressableAssetSettings settings, AddressableAssetGroup group,
            string sheetPath, string subSpriteName, string address, string addLabel)
        {
            // Addressables는 sub-asset도 entry로 가질 수 있음. 단순화를 위해 sheet 자체를 등록하고 address만 부여.
            // 런타임에서 LoadAssetAsync<Sprite>(address)는 sheet의 main sprite를 반환 (sliced multiple이라 main이 없을 수도 있음)
            // 정확히 sub-sprite를 가리키려면 별도 entry가 필요한데, 여기서는 sheet 자체에 PreLoad 라벨만 붙이고
            // ResourceManager가 sub-sprite를 이름으로 찾도록 한다.
            var guid = AssetDatabase.AssetPathToGUID(sheetPath);
            if (string.IsNullOrEmpty(guid))
            {
                Debug.LogWarning($"[ROOTBORN/Addressables] Sheet not found: {sheetPath}");
                return;
            }
            var entry = settings.CreateOrMoveEntry(guid, group);
            entry.address = $"sheet/{Path.GetFileNameWithoutExtension(sheetPath).Replace(' ', '_')}";
            if (!string.IsNullOrEmpty(addLabel))
            {
                entry.SetLabel(addLabel, true, true);
            }
            // Sub-sprite 별칭은 ResourceManager가 sheet에서 이름으로 찾으므로 여기서 alias entry는 생략.
        }

        private static string FindAssetPath<T>(string filter) where T : UnityEngine.Object
        {
            var guids = AssetDatabase.FindAssets(filter);
            if (guids.Length == 0) return null;
            return AssetDatabase.GUIDToAssetPath(guids[0]);
        }

        private static Sprite FindSubSprite(string sheetPath, string subName)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(sheetPath);
            foreach (var a in assets)
            {
                if (a is Sprite s && s.name == subName) return s;
            }
            return null;
        }
    }
}
