using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Quests;
using Rootborn.Game.Quests.Rewards;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Quests
{
    public sealed class QuestSaveTests
    {
        [Test]
        public void QUEST_012_ActiveProgress_RoundTripsThroughSaveData()
        {
            var objective = ScriptableObject.CreateInstance<AlwaysMatchObjective>();
            SetField(objective, "_requiredCount", 3);
            var quest = MakeQuest("quest.active", objective, null);
            var log = new QuestLog(new[] { quest });
            log.Accept(quest);
            log.RecordEvent(new QuestEvent(QuestEventKind.Gather, "g1"));

            var save = log.ToSaveData();
            var loaded = new QuestLog(new[] { quest });
            loaded.LoadFromSaveData(save);

            Assert.AreEqual(QuestState.Active, loaded.GetState(quest));
            Assert.AreEqual(1, loaded.GetObjectiveCount(quest, 0));

            loaded.RecordEvent(new QuestEvent(QuestEventKind.Gather, "g1"));
            Assert.AreEqual(1, loaded.GetObjectiveCount(quest, 0));

            loaded.RecordEvent(new QuestEvent(QuestEventKind.Gather, "g2"));
            loaded.RecordEvent(new QuestEvent(QuestEventKind.Gather, "g3"));
            Assert.AreEqual(QuestState.Completed, loaded.GetState(quest));
            Assert.AreEqual(3, loaded.GetObjectiveCount(quest, 0));
        }

        [Test]
        public void QUEST_013_RewardClaimed_AfterLoadCannotPayAgain()
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            SetField(item, "_maxStack", 99);
            var reward = ScriptableObject.CreateInstance<ItemQuestReward>();
            SetField(reward, "_item", item);
            SetField(reward, "_count", 1);
            var objective = ScriptableObject.CreateInstance<AlwaysMatchObjective>();
            SetField(objective, "_requiredCount", 1);
            var quest = MakeQuest("quest.claimed", objective, reward);
            var log = new QuestLog(new[] { quest });
            var inventory = new Inventory();
            var context = new RewardRuntimeContext(log, inventory, null, null);
            log.Accept(quest);
            log.RecordEvent(new QuestEvent(QuestEventKind.Gather, "g1"));
            Assert.IsTrue(log.ClaimReward(quest, in context));

            var loaded = new QuestLog(new[] { quest });
            loaded.LoadFromSaveData(log.ToSaveData());
            var loadedContext = new RewardRuntimeContext(loaded, inventory, null, null);

            Assert.IsFalse(loaded.ClaimReward(quest, in loadedContext));
            Assert.AreEqual(1, inventory.CountOf(item));
        }

        private static QuestDefinition MakeQuest(string id, QuestObjectiveBase objective, QuestRewardBase reward)
        {
            var quest = ScriptableObject.CreateInstance<QuestDefinition>();
            SetField(quest, "_id", id);
            SetField(quest, "_objectives", new[] { objective });
            if (reward != null)
            {
                SetField(quest, "_rewards", new[] { reward });
            }
            return quest;
        }

        private sealed class AlwaysMatchObjective : QuestObjectiveBase
        {
            public override bool Matches(in QuestEvent questEvent) => true;
        }

        private static void SetField(object target, string name, object value)
        {
            var type = target.GetType();
            while (type != null)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }

                type = type.BaseType;
            }

            Assert.Fail(name);
        }
    }
}
