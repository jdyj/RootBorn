using System;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Quests;
using Rootborn.Game.Quests.Effects;
using Rootborn.Game.Quests.Rewards;
using Rootborn.Game.StudentLife;
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

        [Test]
        public void QUEST_CHAIN_EDIT_008_CompletionRewardPreflightAndDuplicateClaimAreAtomic()
        {
            var item = MakeItem(maxStack: 99);
            var reward = ScriptableObject.CreateInstance<ItemQuestReward>();
            SetField(reward, "_item", item);
            SetField(reward, "_count", 2);
            var chain = MakeCompletedChainWithReward(reward);
            var log = new QuestChainLog(new ScriptableObject[] { chain });
            var inventory = new Inventory();
            var context = new RewardRuntimeContext(null, inventory, null, null);

            log.Accept(chain);
            log.RecordEvent(new QuestEvent(QuestEventKind.Gather, "chain-reward-progress"));

            Assert.IsTrue(log.CanClaimCompletionRewards(chain, in context));
            Assert.IsTrue(log.ClaimCompletionRewards(chain, in context));
            Assert.IsFalse(log.ClaimCompletionRewards(chain, in context), "QUEST_CHAIN_EDIT_008 failed: duplicate reward claim should be rejected.");
            Assert.AreEqual(2, inventory.CountOf(item));
            Assert.IsTrue(log.IsRewardClaimed(chain, "completion"));
        }

        [Test]
        public void QUEST_CHAIN_EDIT_008_FullInventoryCompletionRewardMutatesNothing()
        {
            var filler = MakeItem(maxStack: 1);
            var rewardItem = MakeItem(maxStack: 1);
            var inventory = new Inventory();
            for (int i = 0; i < Inventory.MaxSlots; i++) inventory.Add(filler, 1);
            var reward = ScriptableObject.CreateInstance<ItemQuestReward>();
            SetField(reward, "_item", rewardItem);
            SetField(reward, "_count", 1);
            var chain = MakeCompletedChainWithReward(reward);
            var log = new QuestChainLog(new ScriptableObject[] { chain });
            var context = new RewardRuntimeContext(null, inventory, null, null);

            log.Accept(chain);
            log.RecordEvent(new QuestEvent(QuestEventKind.Gather, "chain-full-inventory-progress"));

            Assert.IsFalse(log.CanClaimCompletionRewards(chain, in context));
            Assert.IsFalse(log.ClaimCompletionRewards(chain, in context));
            Assert.AreEqual(0, inventory.CountOf(rewardItem));
            Assert.IsFalse(log.IsRewardClaimed(chain, "completion"));
        }

        [Test]
        public void QUEST_STUDENT_001_QuestCompletionEffectRaisesStudentLifeTraitAtomically()
        {
            var trait = ScriptableObject.CreateInstance<TraitDefinition>();
            trait.ConfigureForTests("trait.service-sense", "trait.service-sense");
            var effect = ScriptableObject.CreateInstance<TraitDeltaCompletionEffect>();
            SetField(effect, "_trait", trait);
            SetField(effect, "_delta", 2);
            var objective = ScriptableObject.CreateInstance<AlwaysMatchObjective>();
            SetField(objective, "_requiredCount", 1);
            var quest = ScriptableObject.CreateInstance<QuestDefinition>();
            SetField(quest, "_objectives", new QuestObjectiveBase[] { objective });
            SetField(quest, "_completionEffects", new QuestCompletionEffectBase[] { effect });
            var log = new QuestLog(new[] { quest });
            var progress = new StudentLifeProgress("slot-a", "player-1", 10, 10);
            var context = new RewardRuntimeContext(log, null, null, null, progress);

            log.Accept(quest);
            log.RecordEvent(new QuestEvent(QuestEventKind.Talk, "career-mentor"));

            Assert.IsTrue(log.ClaimReward(quest, in context));
            Assert.AreEqual(2, progress.GetTraitValue(trait));
            Assert.IsFalse(log.ClaimReward(quest, in context));
            Assert.AreEqual(2, progress.GetTraitValue(trait));
        }

        private sealed class AlwaysMatchObjective : QuestObjectiveBase
        {
            public override bool Matches(in QuestEvent questEvent) => true;
        }

        private static QuestChainDefinition MakeCompletedChainWithReward(QuestRewardBase reward)
        {
            var objective = ScriptableObject.CreateInstance<AlwaysMatchObjective>();
            objective.ConfigureForRuntime("objective.chain.reward", 1);
            var step = ScriptableObject.CreateInstance<QuestStepDefinition>();
            step.ConfigureForTests("step.chain.reward", "step.chain.reward", "step.chain.reward.desc", new QuestObjectiveBase[] { objective }, Array.Empty<QuestConditionBase>(), Array.Empty<QuestConditionBase>(), Array.Empty<QuestRewardBase>(), Array.Empty<ScriptableObject>());
            var chain = ScriptableObject.CreateInstance<QuestChainDefinition>();
            chain.ConfigureForTests("chain.reward", "chain.reward", "chain.reward.desc", "interest.learning", new ScriptableObject[] { step }, Array.Empty<QuestConditionBase>(), Array.Empty<QuestConditionBase>(), Array.Empty<QuestConditionBase>(), new QuestRewardBase[] { reward }, 0);
            return chain;
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