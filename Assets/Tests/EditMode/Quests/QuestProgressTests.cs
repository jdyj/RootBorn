using System.Reflection;
using NUnit.Framework;
using Rootborn.Core.Quests;
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

        [Test]
        public void QUEST_STATE_001_QuestStateMatchesCoreQuestStageNamesAndOrder()
        {
            // QuestProgress casts between the game enum and Rootborn.Core's enum, and saves store both number and name.
            var states = (QuestState[])System.Enum.GetValues(typeof(QuestState));
            var stages = (QuestStage[])System.Enum.GetValues(typeof(QuestStage));
            Assert.AreEqual(stages.Length, states.Length);
            for (int i = 0; i < states.Length; i++)
            {
                Assert.AreEqual((int)stages[i], (int)states[i]);
                Assert.AreEqual(stages[i].ToString(), states[i].ToString());
            }
        }

        [Test]
        public void QUEST_SAVE_STATE_001_UndefinedSavedStateNameFallsBackToSavedNumber()
        {
            var progress = new QuestProgress(objectiveCount: 1);
            progress.LoadFromSaveData(new QuestProgressSaveData { State = QuestState.Active, StateName = "7", ObjectiveCounts = new[] { 0 } });

            Assert.AreEqual(QuestState.Active, progress.State);
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
