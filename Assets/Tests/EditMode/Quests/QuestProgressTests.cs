using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Quests;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Quests
{
    public sealed class QuestProgressTests
    {
        [Test]
        public void QUEST_002_AcceptedQuest_BecomesActive()
        {
            var progress = new QuestProgress(objectiveCount: 1);

            Assert.AreEqual(QuestState.NotStarted, progress.State);
            Assert.IsTrue(progress.TryAccept());

            Assert.AreEqual(QuestState.Active, progress.State);
        }

        [Test]
        public void QUEST_002_QuestLogAccept_ActivatesDefinition()
        {
            var quest = ScriptableObject.CreateInstance<QuestDefinition>();
            SetField(quest, "_objectives", new QuestObjectiveBase[] { ScriptableObject.CreateInstance<AlwaysMatchObjective>() });
            var log = new QuestLog(new[] { quest });

            Assert.IsTrue(log.Accept(quest));

            Assert.AreEqual(QuestState.Active, log.GetState(quest));
        }

        [Test]
        public void QUEST_004_QuestLogRecordEvent_CompletesWhenObjectiveSatisfied()
        {
            var objective = ScriptableObject.CreateInstance<AlwaysMatchObjective>();
            SetField(objective, "_requiredCount", 1);
            var quest = ScriptableObject.CreateInstance<QuestDefinition>();
            SetField(quest, "_objectives", new QuestObjectiveBase[] { objective });
            var log = new QuestLog(new[] { quest });
            log.Accept(quest);

            log.RecordEvent(new QuestEvent(QuestEventKind.Gather, "event-1"));

            Assert.AreEqual(QuestState.Completed, log.GetState(quest));
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
