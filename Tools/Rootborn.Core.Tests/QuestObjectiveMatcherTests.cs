using NUnit.Framework;
using Rootborn.Core.Quests;

namespace Rootborn.Core.Tests
{
    public sealed class QuestObjectiveMatcherTests
    {
        private const int Gather = 1;
        private const int Talk = 2;

        private static QuestEventFacts Event(int kind, string subjectId, int count = 1, string surfaceId = null)
            => new QuestEventFacts(kind, subjectId, count, surfaceId);

        [Test]
        public void Listed_MatchesOnlyTheSameKindAndAnAcceptedId()
        {
            var spec = QuestObjectiveSpec.Listed(Gather, "resource.tree");
            Assert.That(QuestObjectiveMatcher.Matches(spec, Event(Gather, "resource.tree")), Is.True);
            Assert.That(QuestObjectiveMatcher.Matches(spec, Event(Gather, "resource.rock")), Is.False);
            Assert.That(QuestObjectiveMatcher.Matches(spec, Event(Talk, "resource.tree")), Is.False);
        }

        [Test]
        public void Listed_WithNoAcceptedIdsNeverMatches()
        {
            var spec = QuestObjectiveSpec.Listed(Gather);
            Assert.That(QuestObjectiveMatcher.Matches(spec, Event(Gather, "resource.tree")), Is.False);
        }

        [Test]
        public void EmptyIdsNeverCountAsTheSameEntry()
        {
            var spec = QuestObjectiveSpec.Listed(Gather, "", null);
            Assert.That(QuestObjectiveMatcher.Matches(spec, Event(Gather, "")), Is.False);
            Assert.That(QuestObjectiveMatcher.Matches(spec, Event(Gather, null)), Is.False);
        }

        [Test]
        public void AnySubject_NeedsAnEventSubject()
        {
            var spec = QuestObjectiveSpec.AnySubject(Talk);
            Assert.That(QuestObjectiveMatcher.Matches(spec, Event(Talk, "npc.mom")), Is.True);
            Assert.That(QuestObjectiveMatcher.Matches(spec, Event(Talk, null)), Is.False);
        }

        [Test]
        public void AnyEvent_MatchesTheKindEvenWithoutASubject()
        {
            var spec = QuestObjectiveSpec.AnyEvent(Talk);
            Assert.That(QuestObjectiveMatcher.Matches(spec, Event(Talk, null)), Is.True);
            Assert.That(QuestObjectiveMatcher.Matches(spec, Event(Gather, null)), Is.False);
        }

        [Test]
        public void RequiredSurface_MustMatchExactly()
        {
            var spec = QuestObjectiveSpec.Listed(Gather, "furniture.desk").WithRequiredSurface("surface.room");
            Assert.That(QuestObjectiveMatcher.Matches(spec, Event(Gather, "furniture.desk", surfaceId: "surface.room")), Is.True);
            Assert.That(QuestObjectiveMatcher.Matches(spec, Event(Gather, "furniture.desk", surfaceId: "surface.yard")), Is.False);
            Assert.That(QuestObjectiveMatcher.Matches(spec, Event(Gather, "furniture.desk")), Is.False);
        }

        [Test]
        public void Delta_IsTheEventCountWithAFloorOfOneWhenMatched()
        {
            var spec = QuestObjectiveSpec.Listed(Gather, "resource.tree");
            Assert.That(QuestObjectiveMatcher.Delta(spec, Event(Gather, "resource.tree", count: 3)), Is.EqualTo(3));
            Assert.That(QuestObjectiveMatcher.Delta(spec, Event(Gather, "resource.tree", count: 0)), Is.EqualTo(1));
            Assert.That(QuestObjectiveMatcher.Delta(spec, Event(Gather, "resource.rock", count: 3)), Is.EqualTo(0));
        }

        [Test]
        public void NullSpecNeverMatches()
        {
            Assert.That(QuestObjectiveMatcher.Matches(null, Event(Gather, "resource.tree")), Is.False);
        }
    }
}
