using NUnit.Framework;
using Rootborn.Core.Quests;

namespace Rootborn.Core.Tests
{
    // 옮겨 옴: Assets/Tests/EditMode/Quests/QuestObjectiveTests.cs, Assets/Tests/EditMode/StudentLife/QuestGameplayObjectiveTests.cs
    // Unity에서는 같은 id를 가진 사본(씬·번들)이 같은 대상이었다. Core는 처음부터 id로만 가리킨다.
    public sealed class QuestObjectiveTests
    {
        private static QuestEvent Event(QuestEventKind kind, string subjectId, string surfaceId = null, int count = 1)
            => new QuestEvent(kind, "fixture-event", subjectId, count, surfaceId);

        [Test]
        public void QUEST_007_GatherObjective_MatchesResourceById()
        {
            var objective = new GatherQuestObjective("gather", 1, "resource.test-tree");
            Assert.That(objective.Matches(Event(QuestEventKind.Gather, "resource.test-tree")), Is.True);
            Assert.That(objective.Matches(Event(QuestEventKind.Gather, "resource.test-rock")), Is.False);
            Assert.That(objective.Matches(Event(QuestEventKind.Collect, "resource.test-tree")), Is.False, "wrong kind");
        }

        [Test]
        public void QUEST_009_CollectObjective_MatchesItemById()
        {
            var objective = new CollectQuestObjective("collect", 1, "item.test-wood");
            Assert.That(objective.Matches(Event(QuestEventKind.Collect, "item.test-wood")), Is.True);
            Assert.That(objective.Matches(Event(QuestEventKind.Collect, "item.test-stone")), Is.False);
        }

        [Test]
        public void QUEST_ID_001_TalkObjective_MatchesNpcById()
        {
            var objective = new TalkQuestObjective("talk", 1, "npc.test-mom");
            Assert.That(objective.Matches(Event(QuestEventKind.Talk, "npc.test-mom")), Is.True);
            Assert.That(objective.Matches(Event(QuestEventKind.Talk, "npc.test-teacher")), Is.False);
        }

        [Test]
        public void QUEST_ID_002_LocationVisitObjective_MatchesLocationByIdAndKind_AndNeedsATarget()
        {
            var objective = new LocationVisitQuestObjective("visit", 1, "location.test-library");
            Assert.That(objective.Matches(Event(QuestEventKind.LocationVisit, "location.test-library")), Is.True);
            Assert.That(objective.Matches(Event(QuestEventKind.LocationVisit, "location.test-park")), Is.False);
            Assert.That(objective.Matches(new QuestEvent(QuestEventKind.Talk, "wrong-kind")), Is.False);
            Assert.That(new LocationVisitQuestObjective("visit", 1, null).Matches(Event(QuestEventKind.LocationVisit, "location.test-library")), Is.False);
        }

        [Test]
        public void QUEST_ID_003_LocationActivityObjective_MatchesByIdAndAnyActivityWithoutTarget()
        {
            var objective = new LocationActivityQuestObjective("test", 1, "activity.test-read");
            Assert.That(objective.Matches(Event(QuestEventKind.LocationActivity, "activity.test-read")), Is.True);
            Assert.That(objective.Matches(Event(QuestEventKind.LocationActivity, "activity.test-run")), Is.False);

            var any = new LocationActivityQuestObjective("test", 1, null);
            Assert.That(any.Matches(Event(QuestEventKind.LocationActivity, "activity.test-run")), Is.True, "no target accepts any activity");
            Assert.That(any.Matches(Event(QuestEventKind.LocationActivity, null)), Is.True, "even one without a subject");
        }

        [Test]
        public void QUEST_ID_004_TilePlacedObjective_MatchesByIdAndAnyTileWithoutTarget()
        {
            var objective = new TilePlacedQuestObjective("test", 1, "tile.test-wood");
            Assert.That(objective.Matches(Event(QuestEventKind.TilePlaced, "tile.test-wood")), Is.True);
            Assert.That(objective.Matches(Event(QuestEventKind.TilePlaced, "tile.test-stone")), Is.False);
            Assert.That(new TilePlacedQuestObjective("test", 1, null).Matches(Event(QuestEventKind.TilePlaced, "tile.test-stone")), Is.True, "no target accepts any tile");
        }

        [Test]
        public void QUEST_ID_005_PartTimeWorkObjective_AcceptsEitherConfiguredWork_AndNeverJustItsLocation()
        {
            var objective = new PartTimeWorkQuestObjective("work", 1, new[] { "work.test-cafe", "work.test-store" });
            Assert.That(objective.Matches(Event(QuestEventKind.PartTimeWork, "work.test-cafe")), Is.True);
            Assert.That(objective.Matches(Event(QuestEventKind.PartTimeWork, "work.test-store")), Is.True);
            Assert.That(objective.Matches(Event(QuestEventKind.PartTimeWork, "work.test-other")), Is.False);
            Assert.That(objective.Matches(Event(QuestEventKind.LocationVisit, "work.test-cafe")), Is.False);
            Assert.That(new PartTimeWorkQuestObjective("work", 1, null).Matches(Event(QuestEventKind.PartTimeWork, "work.test-cafe")), Is.False, "an empty list accepts nothing");
        }

        [Test]
        public void QUEST_ID_006_FurniturePlacedObjective_MatchesByStableIdAndRequiredSurface()
        {
            var objective = new FurniturePlacedQuestObjective("furniture", 1, new[] { "furniture.test-desk" }, "surface.test-room");
            Assert.That(objective.Matches(Event(QuestEventKind.FurniturePlaced, "furniture.test-desk", "surface.test-room")), Is.True);
            Assert.That(objective.Matches(Event(QuestEventKind.FurniturePlaced, "furniture.test-desk", "surface.test-yard")), Is.False);
            Assert.That(objective.Matches(Event(QuestEventKind.FurniturePlaced, "furniture.test-bed", "surface.test-room")), Is.False);
            Assert.That(objective.Matches(Event(QuestEventKind.TilePlaced, "furniture.test-desk", "surface.test-room")), Is.False);

            var anySurface = new FurniturePlacedQuestObjective("furniture", 1, new[] { "furniture.test-desk" }, "");
            Assert.That(anySurface.Matches(Event(QuestEventKind.FurniturePlaced, "furniture.test-desk", "surface.test-yard")), Is.True);
        }

        [Test]
        public void QUEST_ID_007_CareerInterestObjective_EmptyListAcceptsAnyDreamOtherwiseById()
        {
            var any = new CareerInterestSelectedQuestObjective("dream", 1, null);
            Assert.That(any.Matches(Event(QuestEventKind.CareerInterestSelected, "career.test-nurse")), Is.True, "empty list accepts any dream");
            Assert.That(any.Matches(new QuestEvent(QuestEventKind.CareerInterestSelected, "c1")), Is.False, "but a dream must be chosen");

            var listed = new CareerInterestSelectedQuestObjective("dream", 1, new[] { "career.test-chef" });
            Assert.That(listed.Matches(Event(QuestEventKind.CareerInterestSelected, "career.test-chef")), Is.True);
            Assert.That(listed.Matches(Event(QuestEventKind.CareerInterestSelected, "career.test-nurse")), Is.False);
        }

        [Test]
        public void QUEST_008_DefeatObjective_MatchesTargetById()
        {
            var objective = new DefeatQuestObjective("defeat", 1, "enemy.test-slime");
            Assert.That(objective.Matches(Event(QuestEventKind.Defeat, "enemy.test-slime")), Is.True);
            Assert.That(objective.Matches(Event(QuestEventKind.Defeat, "enemy.test-bat")), Is.False);
        }

        [Test]
        public void DailyPlanObjective_RequiresSelectionEventWithARoute()
        {
            var objective = new DailyPlanSelectedQuestObjective("plan", 1);
            Assert.That(objective.Matches(Event(QuestEventKind.DailyPlanSelected, "route.test-library")), Is.True);
            Assert.That(objective.Matches(Event(QuestEventKind.DailyPlanSelected, null)), Is.False);
            Assert.That(objective.Matches(new QuestEvent(QuestEventKind.Talk, "talk")), Is.False);
        }

        [Test]
        public void AnyOfObjective_UsesEitherChildWithoutMultiplyingProgress_WhenBothMatch()
        {
            var first = new LocationActivityQuestObjective("first", 1, "activity.test-read");
            var second = new LocationActivityQuestObjective("second", 1, "activity.test-read");
            var objective = new AnyOfQuestObjective("any", 1, new QuestObjective[] { first, null, second });
            var matching = Event(QuestEventKind.LocationActivity, "activity.test-read");
            Assert.That(objective.Matches(matching), Is.True);
            Assert.That(objective.GetDelta(matching), Is.EqualTo(1));
            Assert.That(objective.Matches(new QuestEvent(QuestEventKind.Talk, "talk")), Is.False);
            Assert.That(new AnyOfQuestObjective("any", 1, null).Matches(matching), Is.False);
            Assert.That(objective.Alternatives.Count, Is.EqualTo(3));
        }

        [Test]
        public void EmptyIdsNeverMatch()
        {
            // Unity: a definition without an id only matched itself. Core has no instances to fall back to, so an empty id matches nothing.
            Assert.That(new GatherQuestObjective("gather", 1, "").Matches(Event(QuestEventKind.Gather, "")), Is.False);
            Assert.That(new GatherQuestObjective("gather", 1, null).Matches(Event(QuestEventKind.Gather, null)), Is.False);
            Assert.That(new TalkQuestObjective("talk", 1, "npc.a").Matches(Event(QuestEventKind.Talk, "")), Is.False);
            Assert.That(new LocationActivityQuestObjective("a", 1, "").Matches(Event(QuestEventKind.LocationActivity, "activity.read")), Is.False, "an empty id is not 'no target'");
            Assert.That(new TilePlacedQuestObjective("t", 1, "").Matches(Event(QuestEventKind.TilePlaced, "tile.wood")), Is.False);
        }

        [Test]
        public void RequiredCountIsAtLeastOne_AndDeltaIsTheEventCountWhenMatched()
        {
            var objective = new GatherQuestObjective("gather", 0, "resource.tree");
            Assert.That(objective.RequiredCount, Is.EqualTo(1));
            Assert.That(objective.DisplayKey, Is.EqualTo("gather"));
            Assert.That(objective.GetDelta(Event(QuestEventKind.Gather, "resource.tree", count: 3)), Is.EqualTo(3));
            Assert.That(objective.GetDelta(Event(QuestEventKind.Gather, "resource.tree", count: 0)), Is.Zero, "changed: the game counted 0 as 1");
            Assert.That(objective.GetDelta(Event(QuestEventKind.Gather, "resource.rock", count: 3)), Is.Zero);
        }

        [Test]
        public void QuestEvent_ReducesToFactsAndDefaultsSurfaceToEmpty()
        {
            var questEvent = new QuestEvent(QuestEventKind.FurniturePlaced, "key", "furniture.desk", 2, null, isReplay: true);
            Assert.That(questEvent.SurfaceId, Is.EqualTo(string.Empty));
            Assert.That(questEvent.IsReplay, Is.True);
            var facts = questEvent.ToFacts();
            Assert.That(facts.Kind, Is.EqualTo((int)QuestEventKind.FurniturePlaced));
            Assert.That(facts.SubjectId, Is.EqualTo("furniture.desk"));
            Assert.That(facts.Count, Is.EqualTo(2));
        }

        [Test]
        public void QuestEventKind_KeepsTheGameOrder()
        {
            // Saved and network values are numbers; the order must stay as in the game's enum.
            Assert.That((int)QuestEventKind.Gather, Is.EqualTo(0));
            Assert.That((int)QuestEventKind.Harvest, Is.EqualTo(3));
            Assert.That((int)QuestEventKind.FurniturePlaced, Is.EqualTo(9));
            Assert.That((int)QuestEventKind.DayStarted, Is.EqualTo(12));
        }

        [Test]
        public void GameplayEventBridge_PublishesOnlyKeyedPositiveEventsWithAPlayer()
        {
            var bridge = new QuestGameplayEventBridge();
            int published = 0;
            bridge.OnRecorded += (questEvent, progress) => published++;
            var student = new Rootborn.Core.StudentLife.StudentLifeProgress("slot", "player", 10, 10);
            bridge.Record(new QuestEvent(QuestEventKind.Talk, "k1", "npc.a"), student);
            bridge.Record(new QuestEvent(QuestEventKind.Talk, "", "npc.a"), student);
            bridge.Record(new QuestEvent(QuestEventKind.Talk, "k2", "npc.a", 0), student);
            bridge.Record(new QuestEvent(QuestEventKind.Talk, "k3", "npc.a"), null);
            Assert.That(published, Is.EqualTo(1));
            string key = QuestGameplayEventBridge.CreateRequestKey("talk");
            Assert.That(key, Does.StartWith("talk:"));
            Assert.That(key, Is.Not.EqualTo(QuestGameplayEventBridge.CreateRequestKey("talk")));
        }
    }
}
