using NUnit.Framework;
using Rootborn.Game.Quests;

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
    }
}
