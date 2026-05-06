using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Quests;
using Rootborn.Game.Quests.Objectives;
using Rootborn.Game.Resources;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Quests
{
    public sealed class QuestObjectiveTests
    {
        [Test]
        public void QUEST_004_AllObjectivesRequiredBeforeCompletion()
        {
            var progress = new QuestProgress(objectiveCount: 2);
            progress.TryAccept();

            Assert.IsTrue(progress.TryAddObjectiveCount(0, 1, requiredCount: 1, eventKey: "event-a"));
            Assert.AreEqual(QuestState.Active, progress.State);

            Assert.IsTrue(progress.TryAddObjectiveCount(1, 1, requiredCount: 1, eventKey: "event-b"));
            Assert.AreEqual(QuestState.Completed, progress.State);
        }

        [Test]
        public void QUEST_007_DuplicateEventKey_DoesNotCountTwice()
        {
            var progress = new QuestProgress(objectiveCount: 1);
            progress.TryAccept();

            Assert.IsTrue(progress.TryAddObjectiveCount(0, 1, requiredCount: 2, eventKey: "gather-1"));
            Assert.IsFalse(progress.TryAddObjectiveCount(0, 1, requiredCount: 2, eventKey: "gather-1"));

            Assert.AreEqual(1, progress.GetObjectiveCount(0));
            Assert.AreEqual(QuestState.Active, progress.State);
        }

        [Test]
        public void QUEST_007_GatherObjective_MatchesResourceReference()
        {
            var resource = ScriptableObject.CreateInstance<ResourceNodeDefinition>();
            var other = ScriptableObject.CreateInstance<ResourceNodeDefinition>();
            var objective = ScriptableObject.CreateInstance<GatherQuestObjective>();
            SetField(objective, "_targetResource", resource);

            Assert.IsTrue(objective.Matches(new QuestEvent(QuestEventKind.Gather, "g1", resource: resource)));
            Assert.IsFalse(objective.Matches(new QuestEvent(QuestEventKind.Gather, "g2", resource: other)));
        }

        [Test]
        public void QUEST_008_DefeatObjective_MatchesTargetReference()
        {
            var target = ScriptableObject.CreateInstance<ScriptableObject>();
            var other = ScriptableObject.CreateInstance<ScriptableObject>();
            var objective = ScriptableObject.CreateInstance<DefeatQuestObjective>();
            SetField(objective, "_target", target);

            Assert.IsTrue(objective.Matches(new QuestEvent(QuestEventKind.Defeat, "d1", defeatTarget: target)));
            Assert.IsFalse(objective.Matches(new QuestEvent(QuestEventKind.Defeat, "d2", defeatTarget: other)));
        }

        [Test]
        public void QUEST_009_CollectObjective_MatchesItemReference()
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            var other = ScriptableObject.CreateInstance<ItemDefinition>();
            var objective = ScriptableObject.CreateInstance<CollectQuestObjective>();
            SetField(objective, "_targetItem", item);

            Assert.IsTrue(objective.Matches(new QuestEvent(QuestEventKind.Collect, "c1", item: item)));
            Assert.IsFalse(objective.Matches(new QuestEvent(QuestEventKind.Collect, "c2", item: other)));
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            field.SetValue(target, value);
        }
    }
}
