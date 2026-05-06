using System;
using System.IO;
using System.Reflection;
using Rootborn.Game.Common;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Quests;
using Rootborn.Game.Quests.Effects;
using Rootborn.Game.Quests.Objectives;
using Rootborn.Game.Quests.Rewards;
using Rootborn.Game.Resources;
using Rootborn.Game.Story;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Editor.Tools
{
    public static class GenerateDefaultQuestData
    {
        private const string DataRoot = "Assets/Data";
        private const string RegistryPath = "Assets/Resources/GameDataRegistry.asset";

        [MenuItem("Rootborn/Data/Generate Default Quest Data")]
        public static void Generate()
        {
            EnsureFolder($"{DataRoot}/Quests");
            EnsureFolder($"{DataRoot}/Quests/Objectives");
            EnsureFolder($"{DataRoot}/Quests/Rewards");
            EnsureFolder($"{DataRoot}/Quests/Effects");
            EnsureFolder($"{DataRoot}/Dialogue");
            EnsureFolder($"{DataRoot}/NPCs");
            EnsureFolder($"{DataRoot}/Story");
            EnsureFolder("Assets/Resources");

            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(RegistryPath);
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<GameDataRegistry>();
                AssetDatabase.CreateAsset(registry, RegistryPath);
            }

            var tree = FindById(registry.Resources, "Tree")
                ?? AssetDatabase.LoadAssetAtPath<ResourceNodeDefinition>($"{DataRoot}/Resources/Resource_Tree.asset");
            var stone = FindById(registry.Items, "Stone")
                ?? AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{DataRoot}/Items/Item_Stone.asset");

            var flag = CreateOrLoad<StoryFlagDefinition>($"{DataRoot}/Story/StoryFlag_FirstQuestComplete.asset", asset =>
            {
                SetField(asset, "_id", "story.firstQuestComplete");
                SetField(asset, "_displayKey", "story.firstQuestComplete");
            });

            var objective = CreateOrLoad<GatherQuestObjective>($"{DataRoot}/Quests/Objectives/Objective_GatherWood.asset", asset =>
            {
                SetField(asset, "_targetResource", tree);
                SetField(asset, "_requiredCount", 1);
            });

            var reward = CreateOrLoad<ItemQuestReward>($"{DataRoot}/Quests/Rewards/Reward_WoodQuestStone.asset", asset =>
            {
                SetField(asset, "_item", stone);
                SetField(asset, "_count", 1);
            });

            var effect = CreateOrLoad<SetStoryFlagCompletionEffect>($"{DataRoot}/Quests/Effects/Effect_SetFirstQuestComplete.asset", asset =>
            {
                SetField(asset, "_flag", flag);
            });

            var quest = CreateOrLoad<QuestDefinition>($"{DataRoot}/Quests/Quest_GatherWood.asset", asset =>
            {
                SetField(asset, "_id", "quest.gatherWood");
                SetField(asset, "_displayNameKey", "quest.gatherWood.name");
                SetField(asset, "_descriptionKey", "quest.gatherWood.desc");
                SetField(asset, "_objectives", new QuestObjectiveBase[] { objective });
                SetField(asset, "_rewards", new QuestRewardBase[] { reward });
                SetField(asset, "_completionEffects", new QuestCompletionEffectBase[] { effect });
            });

            var acceptChoice = CreateOrLoad<DialogueChoiceDefinition>($"{DataRoot}/Dialogue/Choice_AcceptGatherWood.asset", asset =>
            {
                SetField(asset, "_labelKey", "dialogue.choice.acceptGatherWood");
                SetField(asset, "_questAction", DialogueQuestAction.AcceptQuest);
                SetField(asset, "_quest", quest);
            });

            var claimChoice = CreateOrLoad<DialogueChoiceDefinition>($"{DataRoot}/Dialogue/Choice_ClaimGatherWood.asset", asset =>
            {
                SetField(asset, "_labelKey", "dialogue.choice.claimGatherWood");
                SetField(asset, "_questAction", DialogueQuestAction.ClaimReward);
                SetField(asset, "_quest", quest);
            });

            var dialogue = CreateOrLoad<DialogueDefinition>($"{DataRoot}/Dialogue/Dialogue_FirstNpc.asset", asset =>
            {
                SetField(asset, "_lineKeys", new[] { "dialogue.firstNpc.greeting" });
                SetField(asset, "_choices", new[] { acceptChoice, claimChoice });
            });

            var npc = CreateOrLoad<NpcDefinition>($"{DataRoot}/NPCs/Npc_FirstGuide.asset", asset =>
            {
                SetField(asset, "_id", "npc.firstGuide");
                SetField(asset, "_displayNameKey", "npc.firstGuide.name");
                SetField(asset, "_defaultDialogue", dialogue);
                SetField(asset, "_quests", new[] { quest });
            });

            SetField(registry, "_quests", new[] { quest });
            SetField(registry, "_questObjectives", new QuestObjectiveBase[] { objective });
            SetField(registry, "_questRewards", new QuestRewardBase[] { reward });
            SetField(registry, "_questCompletionEffects", new QuestCompletionEffectBase[] { effect });
            SetField(registry, "_questConditions", Array.Empty<QuestConditionBase>());
            SetField(registry, "_npcs", new[] { npc });
            SetField(registry, "_dialogues", new[] { dialogue });
            SetField(registry, "_storyFlags", new[] { flag });
            EditorUtility.SetDirty(registry);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ROOTBORN/QuestData] Default quest data generated and registered.");
        }

        private static T CreateOrLoad<T>(string path, Action<T> configure) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }

            configure(asset);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static T FindById<T>(T[] items, string id) where T : ScriptableObject
        {
            if (items == null) return null;
            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                if (item == null) continue;
                var prop = item.GetType().GetProperty("Id", BindingFlags.Instance | BindingFlags.Public);
                if ((prop?.GetValue(item) as string) == id) return item;
            }
            return null;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var type = target.GetType();
            while (type != null)
            {
                var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }
                type = type.BaseType;
            }
            throw new MissingFieldException(target.GetType().FullName, fieldName);
        }
    }
}
