using NUnit.Framework;
using Rootborn.Core.Careers;
using Rootborn.Core.Quests;
using Rootborn.Core.StudentLife;
using Rootborn.Core.WorldState;

namespace Rootborn.Core.Tests
{
    /// <summary>다른 영역이 둔 월드 상태 인터페이스를 WorldStateAccess가 잇는지 본다. 원래 테스트 없음.</summary>
    public sealed class WorldStateAccessTests
    {
        private static WorldStateFlag Flag(string id, WorldStateScopeKind scope = WorldStateScopeKind.Personal)
            => new WorldStateFlag(id, null, null, null, null, null, scope, WorldStateChangeKind.LocationUnlocked, null, null, null, 0, 2);

        [Test]
        public void WORLD_ACCESS_QuestActivationKeepsSourceAndScopeFromDefinition()
        {
            var progress = new WorldStateProgress("slot-a", "player-a");
            IQuestWorldState access = new WorldStateAccess(progress, new[] { Flag("world.park", WorldStateScopeKind.Shared) });

            Assert.IsTrue(access.TryActivate("world.park", new QuestWorldStateSource("chain.a", "step.b", "event.c", 3)));
            Assert.IsFalse(access.TryActivate("world.park", new QuestWorldStateSource("chain.a", "step.b", "event.c", 4)), "Already active.");
            var record = progress.GetRecord(Flag("world.park"));
            Assert.AreEqual(WorldStateScopeKind.Shared, record.Scope);
            Assert.AreEqual("chain.a", record.SourceQuestChainId);
            Assert.AreEqual("step.b", record.SourceQuestStepId);
            Assert.AreEqual("event.c", record.SourceEventId);
            Assert.AreEqual(3, record.ActivatedDay);
            Assert.IsTrue(access.IsActive("world.park"));
        }

        [Test]
        public void WORLD_ACCESS_UnknownOrEmptyFlagIdsChangeNothing()
        {
            var progress = new WorldStateProgress("slot-a", "player-a");
            var access = new WorldStateAccess(progress, new[] { Flag("world.park"), null });

            Assert.IsFalse(((IQuestWorldState)access).TryActivate("world.missing", default));
            Assert.IsFalse(((ICareerWorldState)access).TryActivate(null, "req", 1));
            Assert.IsFalse(access.MarkEffectVersionApplied("world.missing", 1));
            Assert.IsFalse(access.MarkEffectVersionApplied("world.park", 1), "Not active yet.");
            Assert.IsFalse(access.IsActive(null));
            Assert.AreEqual(0, progress.ActiveFlagCount);
        }

        [Test]
        public void WORLD_ACCESS_CareerActivationUsesRequestIdAsEventAndExposesIdentity()
        {
            var progress = new WorldStateProgress("slot-a", "player-a");
            ICareerWorldState access = new WorldStateAccess(progress, new[] { Flag("world.cafe") });

            Assert.AreEqual("slot-a", access.SaveSlot);
            Assert.AreEqual("player-a", access.PlayerId);
            Assert.IsTrue(access.TryActivate("world.cafe", "request-1", 7));
            var record = progress.GetRecord(Flag("world.cafe"));
            Assert.AreEqual(string.Empty, record.SourceQuestChainId);
            Assert.AreEqual("request-1", record.SourceEventId);
            Assert.AreEqual(7, record.ActivatedDay);
        }

        [Test]
        public void WORLD_ACCESS_EffectVersionAndReadOnlyViews()
        {
            var progress = new WorldStateProgress("slot-a", "player-a");
            var access = new WorldStateAccess(progress, new[] { Flag("world.park") });
            access.TryActivate("world.park", new WorldStateActivationSource(null, null, null, 1));

            Assert.IsTrue(access.MarkEffectVersionApplied("world.park", 2));
            Assert.IsFalse(access.MarkEffectVersionApplied("world.park", 2), "Same version is applied once.");
            Assert.IsTrue(((IDailyPlanWorldState)access).IsActive("world.park"));
            Assert.IsTrue(((ILocationStateWorldFlags)access).IsActive("world.park"));
            Assert.Throws<System.ArgumentNullException>(() => new WorldStateAccess(null, (WorldStateFlagLookupCache)null));
        }
    }
}
