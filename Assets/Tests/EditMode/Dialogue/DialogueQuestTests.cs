using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Quests;
using Rootborn.Game.Quests.Rewards;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Dialogue
{
    public sealed class DialogueQuestTests
    {
        [Test]
        public void QUEST_001_DialogueSession_OpenAndClose()
        {
            var dialogue = ScriptableObject.CreateInstance<DialogueDefinition>();
            var session = new DialogueSession();

            session.Open(dialogue);
            Assert.IsTrue(session.IsOpen);
            Assert.AreSame(dialogue, session.Current);

            session.Close();
            Assert.IsFalse(session.IsOpen);
            Assert.IsNull(session.Current);
        }

        [Test]
        public void QUEST_002_DialogueChoice_AcceptsQuest()
        {
            var quest = ScriptableObject.CreateInstance<QuestDefinition>();
            SetField(quest, "_objectives", new QuestObjectiveBase[] { ScriptableObject.CreateInstance<AlwaysMatchObjective>() });
            var choice = ScriptableObject.CreateInstance<DialogueChoiceDefinition>();
            SetField(choice, "_questAction", DialogueQuestAction.AcceptQuest);
            SetField(choice, "_quest", quest);
            var log = new QuestLog(new[] { quest });

            Assert.IsTrue(choice.TryExecute(new DialogueChoiceContext(log, default)));

            Assert.AreEqual(QuestState.Active, log.GetState(quest));
        }

        [Test]
        public void QUEST_005_DialogueChoice_ClaimsRewardThroughQuestLogPreflight()
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            SetField(item, "_maxStack", 99);
            var reward = ScriptableObject.CreateInstance<ItemQuestReward>();
            SetField(reward, "_item", item);
            SetField(reward, "_count", 1);
            var objective = ScriptableObject.CreateInstance<AlwaysMatchObjective>();
            SetField(objective, "_requiredCount", 1);
            var quest = ScriptableObject.CreateInstance<QuestDefinition>();
            SetField(quest, "_objectives", new QuestObjectiveBase[] { objective });
            SetField(quest, "_rewards", new QuestRewardBase[] { reward });
            var log = new QuestLog(new[] { quest });
            var inventory = new Inventory();
            var context = new DialogueChoiceContext(log, new RewardRuntimeContext(log, inventory, null, null));
            var choice = ScriptableObject.CreateInstance<DialogueChoiceDefinition>();
            SetField(choice, "_questAction", DialogueQuestAction.ClaimReward);
            SetField(choice, "_quest", quest);

            log.Accept(quest);
            log.RecordEvent(new QuestEvent(QuestEventKind.Gather, "g1"));

            Assert.IsTrue(choice.TryExecute(in context));
            Assert.AreEqual(1, inventory.CountOf(item));
            Assert.AreEqual(QuestState.RewardClaimed, log.GetState(quest));
            Assert.IsFalse(choice.TryExecute(in context));
            Assert.AreEqual(1, inventory.CountOf(item));
        }

        private sealed class AlwaysMatchObjective : QuestObjectiveBase
        {
            public override bool Matches(in QuestEvent questEvent) => true;
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
