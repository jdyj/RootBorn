using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Quests;
using Rootborn.Game.Quests.Rewards;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Quests
{
    public sealed class QuestRewardTests
    {
        [Test]
        public void QUEST_005_ItemReward_CanApplyThenAddsExactlyOnce()
        {
            var item = MakeItem(maxStack: 99);
            var reward = ScriptableObject.CreateInstance<ItemQuestReward>();
            SetField(reward, "_item", item);
            SetField(reward, "_count", 3);
            var inventory = new Inventory();
            var context = new RewardRuntimeContext(null, inventory, null, null);

            Assert.IsTrue(reward.CanApply(in context));
            reward.Apply(in context);

            Assert.AreEqual(3, inventory.CountOf(item));
        }

        [Test]
        public void QUEST_006_FullInventory_PreflightFailsAndMutatesNothing()
        {
            var fullItem = MakeItem(maxStack: 1);
            var rewardItem = MakeItem(maxStack: 1);
            var inventory = new Inventory();
            for (int i = 0; i < Inventory.MaxSlots; i++)
            {
                inventory.Add(fullItem, 1);
            }

            var reward = ScriptableObject.CreateInstance<ItemQuestReward>();
            SetField(reward, "_item", rewardItem);
            SetField(reward, "_count", 1);
            var context = new RewardRuntimeContext(null, inventory, null, null);

            Assert.IsFalse(reward.CanApply(in context));
            Assert.AreEqual(0, inventory.CountOf(rewardItem));
        }

        [Test]
        public void QUEST_006_QuestLogClaimReward_RepeatedCallDoesNotDuplicate()
        {
            var item = MakeItem(maxStack: 99);
            var reward = ScriptableObject.CreateInstance<ItemQuestReward>();
            SetField(reward, "_item", item);
            SetField(reward, "_count", 2);
            var objective = ScriptableObject.CreateInstance<AlwaysMatchObjective>();
            SetField(objective, "_requiredCount", 1);
            var quest = ScriptableObject.CreateInstance<QuestDefinition>();
            SetField(quest, "_objectives", new QuestObjectiveBase[] { objective });
            SetField(quest, "_rewards", new QuestRewardBase[] { reward });
            var log = new QuestLog(new[] { quest });
            var inventory = new Inventory();
            var context = new RewardRuntimeContext(log, inventory, null, null);

            log.Accept(quest);
            log.RecordEvent(new QuestEvent(QuestEventKind.Gather, "g1"));

            Assert.IsTrue(log.ClaimReward(quest, in context));
            Assert.IsFalse(log.ClaimReward(quest, in context));

            Assert.AreEqual(2, inventory.CountOf(item));
            Assert.AreEqual(QuestState.RewardClaimed, log.GetState(quest));
        }

        [Test]
        public void QUEST_005_QuestLogClaimReward_FullInventoryMutatesNothing()
        {
            var filler = MakeItem(maxStack: 1);
            var rewardItem = MakeItem(maxStack: 1);
            var inventory = new Inventory();
            for (int i = 0; i < Inventory.MaxSlots; i++)
            {
                inventory.Add(filler, 1);
            }

            var reward = ScriptableObject.CreateInstance<ItemQuestReward>();
            SetField(reward, "_item", rewardItem);
            SetField(reward, "_count", 1);
            var objective = ScriptableObject.CreateInstance<AlwaysMatchObjective>();
            SetField(objective, "_requiredCount", 1);
            var quest = ScriptableObject.CreateInstance<QuestDefinition>();
            SetField(quest, "_objectives", new QuestObjectiveBase[] { objective });
            SetField(quest, "_rewards", new QuestRewardBase[] { reward });
            var log = new QuestLog(new[] { quest });
            var context = new RewardRuntimeContext(log, inventory, null, null);

            log.Accept(quest);
            log.RecordEvent(new QuestEvent(QuestEventKind.Gather, "g1"));

            Assert.IsFalse(log.ClaimReward(quest, in context));

            Assert.AreEqual(0, inventory.CountOf(rewardItem));
            Assert.AreEqual(QuestState.Completed, log.GetState(quest));
        }

        private sealed class AlwaysMatchObjective : QuestObjectiveBase
        {
            public override bool Matches(in QuestEvent questEvent) => true;
        }

        private static ItemDefinition MakeItem(int maxStack)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            SetField(item, "_maxStack", maxStack);
            return item;
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
