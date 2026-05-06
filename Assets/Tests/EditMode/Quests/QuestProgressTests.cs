using NUnit.Framework;
using Rootborn.Game.Quests;

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
    }
}
