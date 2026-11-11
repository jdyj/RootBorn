using NUnit.Framework;
using Rootborn.Core.Quests;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Tests
{
    // Unity 판과 일부러 다르게 고친 동작. 각 테스트가 무엇이 바뀌었는지 이름으로 말한다.
    public sealed class QuestChangedBehaviorTests
    {
        private static QuestChainDefinition TwoStepChain()
            => new QuestChainDefinition("chain.changed", "n", "d", "", new[]
            {
                new QuestStepDefinition("first", "first", "d", new QuestObjective[] { new AnyEventObjective(2) }),
                new QuestStepDefinition("second", "second", "d", new QuestObjective[] { new AnyEventObjective(1) }),
            });

        [Test]
        public void EventsWithoutAPositiveCount_CreditNothing_AndDoNotUseUpTheirKey()
        {
            // Unity: GetDelta returned Max(1, count), so a 0 or negative count still added 1.
            var quest = new QuestDefinition("quest.count", null, null, new QuestObjective[] { new GatherQuestObjective("g", 3, "resource.tree") });
            var log = new QuestLog(new[] { quest });
            log.Accept(quest);
            log.RecordEvent(new QuestEvent(QuestEventKind.Gather, "k1", "resource.tree", 0));
            log.RecordEvent(new QuestEvent(QuestEventKind.Gather, "k2", "resource.tree", -5));
            Assert.That(log.GetObjectiveCount(quest, 0), Is.Zero);
            log.RecordEvent(new QuestEvent(QuestEventKind.Gather, "k1", "resource.tree", 2));
            Assert.That(log.GetObjectiveCount(quest, 0), Is.EqualTo(2), "the key of a refused event is still free");

            var chain = TwoStepChain();
            var chains = new QuestChainLog(new[] { chain });
            chains.Accept(chain);
            chains.RecordEvent(new QuestEvent(QuestEventKind.Talk, "c1", "npc.a", 0));
            Assert.That(chains.GetObjectiveProgress(chain, 0), Is.Zero);
            chains.RecordEvent(new QuestEvent(QuestEventKind.Talk, "c1", "npc.a", 1));
            Assert.That(chains.GetObjectiveProgress(chain, 0), Is.EqualTo(1), "chains also keep the key free");
        }

        [Test]
        public void QuestLog_IgnoresReplayedEvents_WhileChainsStillCatchUpOnThem()
        {
            // Unity: only PlayerGlobalState filtered replays; QuestLog itself counted them.
            var quest = new QuestDefinition("quest.replay", null, null, new QuestObjective[] { new AnyEventObjective(1) });
            var log = new QuestLog(new[] { quest });
            log.Accept(quest);
            log.RecordEvent(new QuestEvent(QuestEventKind.FurniturePlaced, "f1", "furniture.desk", isReplay: true));
            Assert.That(log.GetState(quest), Is.EqualTo(QuestStage.Active));

            var chain = TwoStepChain();
            var chains = new QuestChainLog(new[] { chain });
            chains.Accept(chain);
            chains.RecordEvent(new QuestEvent(QuestEventKind.FurniturePlaced, "f1", "furniture.desk", isReplay: true));
            Assert.That(chains.GetObjectiveProgress(chain, 0), Is.EqualTo(1));
        }

        [Test]
        public void ChainSaveState_AcceptsOnlyStateNames()
        {
            // Unity: Enum.TryParse also took numbers, including ones that are no state ("9").
            var chain = TwoStepChain();
            Assert.That(QuestChainProgress.FromSaveData(new QuestChainProgressSaveData { ChainState = "9" }, chain).State, Is.EqualTo(QuestChainState.Available));
            Assert.That(QuestChainProgress.FromSaveData(new QuestChainProgressSaveData { ChainState = "1" }, chain).State, Is.EqualTo(QuestChainState.Available));
            Assert.That(QuestChainProgress.FromSaveData(new QuestChainProgressSaveData { ChainState = "Tracked" }, chain).State, Is.EqualTo(QuestChainState.Tracked), "saved names still load");
            Assert.That(QuestChainProgress.FromSaveData(new QuestChainProgressSaveData { ChainState = "tracked" }, chain).State, Is.EqualTo(QuestChainState.Available), "names are exact, as the game wrote them");
        }

        [Test]
        public void Store_ReturnsAHeldLogEvenWithoutAChainList()
        {
            // Unity: GetOrCreate returned null for a missing registry even when the owner had bound a log.
            var store = new QuestChainProgressStore(null);
            var student = new StudentLifeProgress("slot", "player-a", 10, 10);
            var log = new QuestChainLog(new[] { TwoStepChain() });
            store.BindLog(student, log);
            Assert.That(store.GetOrCreate(null, student), Is.SameAs(log));
            Assert.That(store.GetOrCreate(null, new StudentLifeProgress("slot", "player-b", 10, 10)), Is.Null, "nothing to create from");
        }
    }
}
