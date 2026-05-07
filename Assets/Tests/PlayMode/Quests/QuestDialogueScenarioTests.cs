using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Quests;
using Rootborn.Game.Quests.Effects;
using Rootborn.Game.Quests.Objectives;
using Rootborn.Game.Quests.Rewards;
using Rootborn.Game.Resources;
using Rootborn.Game.Story;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode.Quests
{
    public sealed class QuestDialogueScenarioTests
    {
        [UnityTest]
        public IEnumerator QUEST_001_NpcInteraction_OpensAndClosesDialogue()
        {
            var npc = new GameObject("NPC");
            var interactor = npc.AddComponent<NpcInteractor>();
            var dialogue = ScriptableObject.CreateInstance<DialogueDefinition>();
            var npcDef = ScriptableObject.CreateInstance<NpcDefinition>();
            SetField(npcDef, "_defaultDialogue", dialogue);
            interactor.Bind(npcDef);

            interactor.Interact();
            yield return null;

            Assert.IsTrue(interactor.Session.IsOpen);
            Assert.AreSame(dialogue, interactor.Session.Current);

            interactor.Close();
            yield return null;

            Assert.IsFalse(interactor.Session.IsOpen);
            Object.Destroy(npc);
        }

        [UnityTest]
        public IEnumerator QUEST_015_NpcQuest_FullFlow_ClaimReward_SetsStoryFlag()
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            SetField(item, "_maxStack", 99);
            var resource = ScriptableObject.CreateInstance<ResourceNodeDefinition>();
            var objective = ScriptableObject.CreateInstance<GatherQuestObjective>();
            SetField(objective, "_targetResource", resource);
            SetField(objective, "_requiredCount", 1);
            var reward = ScriptableObject.CreateInstance<ItemQuestReward>();
            SetField(reward, "_item", item);
            SetField(reward, "_count", 1);
            var flag = ScriptableObject.CreateInstance<StoryFlagDefinition>();
            var effect = ScriptableObject.CreateInstance<SetStoryFlagCompletionEffect>();
            SetField(effect, "_flag", flag);
            var quest = ScriptableObject.CreateInstance<QuestDefinition>();
            SetField(quest, "_objectives", new QuestObjectiveBase[] { objective });
            SetField(quest, "_rewards", new QuestRewardBase[] { reward });
            SetField(quest, "_completionEffects", new QuestCompletionEffectBase[] { effect });

            var log = new QuestLog(new[] { quest });
            var inventory = new Inventory();
            var flags = new StoryFlagSet();
            var rewardContext = new RewardRuntimeContext(log, inventory, null, flags);

            Assert.IsTrue(log.Accept(quest));
            log.RecordEvent(new QuestEvent(QuestEventKind.Gather, "g1", resource: resource));
            Assert.AreEqual(QuestState.Completed, log.GetState(quest));
            Assert.IsTrue(log.ClaimReward(quest, in rewardContext));

            yield return null;

            Assert.AreEqual(1, inventory.CountOf(item));
            Assert.IsTrue(flags.IsSet(flag));
            Assert.AreEqual(QuestState.RewardClaimed, log.GetState(quest));
            Assert.IsFalse(log.ClaimReward(quest, in rewardContext));
            Assert.AreEqual(1, inventory.CountOf(item));
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var type = target.GetType();
            while (type != null)
            {
                var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }

                type = type.BaseType;
            }

            Assert.Fail(fieldName);
        }
    }
}
