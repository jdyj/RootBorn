using System.Collections.Generic;
using Rootborn.Game.Common;
using Rootborn.Game.Tools;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Editor.Tools
{
    /// <summary>
    /// ItemDefinition / ToolDefinition .asset 의 _icon 필드를 Pixelwood Items 16x16 sheet 의
    /// sub-sprite 로 자동 와이어링. ID → sub-sprite 이름 매핑 테이블은 코드 안에 데이터로 보관.
    /// 새 ID 추가 시 IconMap 만 갱신 → 메뉴 한 번 실행하면 모든 .asset 갱신.
    /// </summary>
    public static class ItemIconWiring
    {
        private const string ItemsSheetPath = "Assets/Pixelwood Valley/Pixelwood Valley Icon Pack 1.0/1.0/Items 16x16.png";

        // ID 별 추정 sub-sprite 이름. 실제 sheet 칸 위치는 PNG 시각 검증 후 보정.
        // SliceOne 의 row=0 = TOP, col=0 = LEFT 규칙. Items 16x16 = 21cols × 15rows.
        private static readonly Dictionary<string, string> IconMap = new()
        {
            // Resources
            { "Wood",     "Icon_r14_c0" }, // 마지막 줄 좌측 — 통나무 추정
            { "Stone",    "Icon_r14_c1" },
            // Tools
            { "BareHand", "Icon_r0_c19" }, // 추정 — 손 모양 cell
            { "StoneAxe", "Icon_r0_c2" },  // 추정 — 도끼 cell
            { "StoneHoe", "Icon_r0_c5" },  // 추정 — 곡괭이/괭이 cell
        };

        [MenuItem("Rootborn/Items/Wire Icons From Pixelwood")]
        public static void WireAll()
        {
            // 1) 모든 sub-sprite 로드 → 이름 lookup 가능하도록 dictionary 구성.
            var spritesByName = new Dictionary<string, Sprite>(512);
            var assets = AssetDatabase.LoadAllAssetsAtPath(ItemsSheetPath);
            int totalSubs = 0;
            foreach (var a in assets)
            {
                if (a is Sprite s)
                {
                    spritesByName[s.name] = s;
                    totalSubs++;
                }
            }
            if (totalSubs == 0)
            {
                Debug.LogError($"[ROOTBORN/IconWire] No sub-sprites found at {ItemsSheetPath}. Run 'Rootborn/Pixelwood/Slice Sprite Sheets' first.");
                return;
            }
            Debug.Log($"[ROOTBORN/IconWire] Items sheet has {totalSubs} sub-sprites.");

            int wiredItems = 0, wiredTools = 0, missing = 0;

            // 2) ItemDefinition 순회.
            var itemGuids = AssetDatabase.FindAssets("t:ItemDefinition");
            foreach (var guid in itemGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var def = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
                if (def == null) continue;
                if (string.IsNullOrEmpty(def.Id))
                {
                    Debug.LogWarning($"[ROOTBORN/IconWire] {path} has empty _id — skip.");
                    continue;
                }
                if (!IconMap.TryGetValue(def.Id, out var subName))
                {
                    Debug.LogWarning($"[ROOTBORN/IconWire] No icon mapping for ItemDefinition '{def.Id}' ({path}).");
                    missing++;
                    continue;
                }
                if (!spritesByName.TryGetValue(subName, out var sprite))
                {
                    Debug.LogWarning($"[ROOTBORN/IconWire] Sub-sprite '{subName}' not found in sheet for '{def.Id}'.");
                    missing++;
                    continue;
                }
                AssignIconField(def, sprite);
                wiredItems++;
            }

            // 3) ToolDefinition 순회 (도구는 Item 과 ID 공유).
            var toolGuids = AssetDatabase.FindAssets("t:ToolDefinition");
            foreach (var guid in toolGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var def = AssetDatabase.LoadAssetAtPath<ToolDefinition>(path);
                if (def == null) continue;
                if (string.IsNullOrEmpty(def.Id)) continue;
                if (!IconMap.TryGetValue(def.Id, out var subName)) continue;
                if (!spritesByName.TryGetValue(subName, out var sprite)) continue;
                AssignIconField(def, sprite);
                wiredTools++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ROOTBORN/IconWire] Wired {wiredItems} ItemDefinition + {wiredTools} ToolDefinition icons. {missing} missing mappings.");
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
    }
}
