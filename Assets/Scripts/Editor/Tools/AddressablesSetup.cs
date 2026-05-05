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

        // Fantasy Book UI V2 — UI sprite addresses (모두 PreLoad 라벨로 부팅 시 일괄 로드).
        private const string FantasyBookRoot = "Assets/Pixelwood Valley/Fantasy Book UI V2/1.0/Sprites";
        private static readonly (string assetPath, string address)[] UiSpriteEntries = new[]
        {
            // Book pages — Page1~9 는 페이지 넘김 9프레임 애니메이션. Page1=정지, 1→9 책장 넘김.
            ($"{FantasyBookRoot}/Page + Animation/Page1.png",           "sprites/ui/book/page-1"),
            ($"{FantasyBookRoot}/Page + Animation/Page2.png",           "sprites/ui/book/page-2"),
            ($"{FantasyBookRoot}/Page + Animation/Page3.png",           "sprites/ui/book/page-3"),
            ($"{FantasyBookRoot}/Page + Animation/Page4.png",           "sprites/ui/book/page-4"),
            ($"{FantasyBookRoot}/Page + Animation/Page5.png",           "sprites/ui/book/page-5"),
            ($"{FantasyBookRoot}/Page + Animation/Page6.png",           "sprites/ui/book/page-6"),
            ($"{FantasyBookRoot}/Page + Animation/Page7.png",           "sprites/ui/book/page-7"),
            ($"{FantasyBookRoot}/Page + Animation/Page8.png",           "sprites/ui/book/page-8"),
            ($"{FantasyBookRoot}/Page + Animation/Page9.png",           "sprites/ui/book/page-9"),
            ($"{FantasyBookRoot}/Unique/DarkerPage.png",                "sprites/ui/book/spine"),

            // Sizeable boxes (HUD/단축키 패널/PREV·NEXT 버튼)
            ($"{FantasyBookRoot}/Sizeable Boxes/3.png",                 "sprites/ui/panel/hud"),
            ($"{FantasyBookRoot}/Sizeable Boxes/2.png",                 "sprites/ui/panel/hint"),
            ($"{FantasyBookRoot}/Sizeable Boxes/10.png",                "sprites/ui/button/small"),

            // Slots
            ($"{FantasyBookRoot}/Unique/Icon Container/1.png",          "sprites/ui/slot/item"),
            ($"{FantasyBookRoot}/Unique/Icon Container/4.png",          "sprites/ui/slot/equipment"),

            // Titles & Decoration
            ($"{FantasyBookRoot}/Titles/5.png",                         "sprites/ui/ribbon/items"),
            ($"{FantasyBookRoot}/Titles/10.png",                        "sprites/ui/ribbon/description"),
            ($"{FantasyBookRoot}/Titles/15.png",                        "sprites/ui/ribbon/equipment"),
            ($"{FantasyBookRoot}/Unique/Cutter1.1.png",                 "sprites/ui/decor/cutter-short"),
            ($"{FantasyBookRoot}/Unique/Cutter1.2.png",                 "sprites/ui/decor/cutter-long"),
            ($"{FantasyBookRoot}/Fancy Inscriptions/Plus.PNG",          "sprites/ui/decor/inscription-plus"),

            // Bookmark sheet (sliced 5색)
            ($"{FantasyBookRoot}/Bookmarks/1 22x20.png",                "sprites/ui/sheet/bookmark"),

            // Character silhouette
            ($"{FantasyBookRoot}/Unique/Character.png",                 "sprites/ui/character"),
        };

        // 도구 장착 캐릭터 sheet — Axe/Hoe/Pickaxe/Pickup × Down/Side/Up. 12개 sheet (각 sheet 안에 다수 sub-sprite frame).
        private const string ToolCharRoot = "Assets/Pixelwood Valley/Pixelwood Valley 1.1.2/Player Character";
        private static readonly (string assetPath, string address)[] PlayerToolSheetEntries = new[]
        {
            ($"{ToolCharRoot}/Axe/Down.png",      "sprites/player/tool/axe-down"),
            ($"{ToolCharRoot}/Axe/Side.png",      "sprites/player/tool/axe-side"),
            ($"{ToolCharRoot}/Axe/Up.png",        "sprites/player/tool/axe-up"),
            ($"{ToolCharRoot}/Hoe/Down.png",      "sprites/player/tool/hoe-down"),
            ($"{ToolCharRoot}/Hoe/Side.png",      "sprites/player/tool/hoe-side"),
            ($"{ToolCharRoot}/Hoe/Up.png",        "sprites/player/tool/hoe-up"),
            ($"{ToolCharRoot}/pickaxe/Down.png",  "sprites/player/tool/pickaxe-down"),
            ($"{ToolCharRoot}/pickaxe/Side.png",  "sprites/player/tool/pickaxe-side"),
            ($"{ToolCharRoot}/pickaxe/Up.png",    "sprites/player/tool/pickaxe-up"),
            ($"{ToolCharRoot}/Pickup/Down.png",   "sprites/player/tool/pickup-down"),
            ($"{ToolCharRoot}/Pickup/Side.png",   "sprites/player/tool/pickup-side"),
            ($"{ToolCharRoot}/Pickup/Up.png",     "sprites/player/tool/pickup-up"),
        };

        /// <summary>
        /// 테스트용: 등록된 모든 UI sprite address 반환. UISpriteAddresses 상수와 일관성 검증에 사용.
        /// </summary>
        public static System.Collections.Generic.IReadOnlyList<string> GetUiSpriteAddresses()
        {
            var list = new System.Collections.Generic.List<string>(UiSpriteEntries.Length);
            foreach (var (_, addr) in UiSpriteEntries) list.Add(addr);
            return list;
        }

        /// <summary>
        /// 테스트용: (assetPath, address) 쌍 반환. Asset 파일 존재 여부 검증에 사용.
        /// </summary>
        public static System.Collections.Generic.IReadOnlyList<(string assetPath, string address)> GetUiSpriteEntries()
        {
            return UiSpriteEntries;
        }

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

            // Fantasy Book UI sprite 일괄 등록 — 모두 Sprites 그룹 + PreLoad 라벨.
            int uiOk = 0, uiMiss = 0;
            foreach (var (path, addr) in UiSpriteEntries)
            {
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) == null)
                {
                    Debug.LogWarning($"[ROOTBORN/Addressables] UI sprite missing: {path}");
                    uiMiss++;
                    continue;
                }
                RegisterAsset(settings, groupSprites, path, addr, addLabel: LabelPreLoad);
                uiOk++;
            }
            Debug.Log($"[ROOTBORN/Addressables] UI sprites registered: {uiOk} ok, {uiMiss} missing.");

            // 도구 장착 캐릭터 sheet — Axe/Hoe/Pickaxe/Pickup × Down/Side/Up. PreLoad 로 부팅 시 일괄 사전 로드.
            int toolOk = 0, toolMiss = 0;
            foreach (var (path, addr) in PlayerToolSheetEntries)
            {
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) == null)
                {
                    Debug.LogWarning($"[ROOTBORN/Addressables] Player tool sheet missing: {path}");
                    toolMiss++;
                    continue;
                }
                RegisterAsset(settings, groupSprites, path, addr, addLabel: LabelPreLoad);
                toolOk++;
            }
            Debug.Log($"[ROOTBORN/Addressables] Player tool sheets registered: {toolOk} ok, {toolMiss} missing.");

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
