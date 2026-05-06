using System.Collections.Generic;
using Rootborn.Game.Common;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Editor.DataValidators
{
    public static class GameDataRegistryValidator
    {
        [MenuItem("Rootborn/Data/Validate Registry")]
        public static void Validate()
        {
            var guids = AssetDatabase.FindAssets("t:GameDataRegistry");
            if (guids.Length == 0)
            {
                Debug.LogWarning("[ROOTBORN] No GameDataRegistry asset found.");
                return;
            }
            int issues = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var reg = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(path);
                issues += CheckUnique(reg.Crops, "Crop", path);
                issues += CheckUnique(reg.Tools, "Tool", path);
                issues += CheckUnique(reg.Resources, "Resource", path);
                issues += CheckUnique(reg.Knowledge, "Knowledge", path);
                issues += CheckUnique(reg.Traits, "Trait", path);
                issues += CheckUnique(reg.Statuses, "Status", path);
                issues += CheckUnique(reg.Quests, "Quest", path);
                issues += CheckUnique(reg.Npcs, "NPC", path);
                issues += CheckUnique(reg.StoryFlags, "StoryFlag", path);
                issues += CheckNonNull(reg.QuestObjectives, "QuestObjective", path);
                issues += CheckNonNull(reg.QuestRewards, "QuestReward", path);
                issues += CheckNonNull(reg.QuestCompletionEffects, "QuestCompletionEffect", path);
                issues += CheckNonNull(reg.QuestConditions, "QuestCondition", path);
                issues += CheckNonNull(reg.Dialogues, "Dialogue", path);
            }
            Debug.Log($"[ROOTBORN] Registry validation complete. issues={issues}");
        }

        private static int CheckUnique<T>(T[] items, string kind, string registryPath) where T : ScriptableObject
        {
            var seen = new HashSet<string>();
            int issues = 0;
            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                if (item == null)
                {
                    Debug.LogWarning($"[ROOTBORN] {kind} index {i} is null in {registryPath}");
                    issues++;
                    continue;
                }
                var idField = item.GetType().GetField("_id",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public);
                if (idField == null) continue;
                var id = idField.GetValue(item) as string;
                if (string.IsNullOrEmpty(id))
                {
                    Debug.LogWarning($"[ROOTBORN] {kind} '{item.name}' has empty _id");
                    issues++;
                    continue;
                }
                if (!seen.Add(id))
                {
                    Debug.LogWarning($"[ROOTBORN] {kind} duplicate _id '{id}' in {registryPath}");
                    issues++;
                }
            }
            return issues;
        }

        private static int CheckNonNull<T>(T[] items, string kind, string registryPath) where T : UnityEngine.Object
        {
            int issues = 0;
            if (items == null)
            {
                Debug.LogWarning($"[ROOTBORN] {kind} array is null in {registryPath}");
                return 1;
            }

            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] == null)
                {
                    Debug.LogWarning($"[ROOTBORN] {kind} index {i} is null in {registryPath}");
                    issues++;
                }
            }

            return issues;
        }
    }
}
