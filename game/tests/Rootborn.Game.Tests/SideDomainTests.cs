using System.Linq;
using NUnit.Framework;
using Rootborn.Core.DiscoveryClues;
using Rootborn.Core.StudentLife;

namespace Rootborn.Game.Tests
{
    /// <summary>탐색·발견 단서·단서 해석·하루 이벤트 명령이 Core 실행기를 거쳐 저장되고, 같은 요청은 한 번만 적용된다. 자동 테스트다.</summary>
    public sealed class SideDomainTests
    {
        [Test]
        public void Exploration_GrantsItsOutcomesOnce_AndIsSaved()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            var wood = game.Item("Wood");

            var result = game.ChooseExploration("exploration.test.alley-crate", "choice.test.inspect");
            Assert.That(result.Kind, Is.EqualTo(ExplorationChoiceResultKind.Applied));
            Assert.That(game.Inventory.CountOf(wood), Is.EqualTo(1));
            Assert.That(game.Student.GetTraitValue("trait.focus"), Is.EqualTo(1));

            var again = TestWorld.Start(saves.Store);
            Assert.That(again.Inventory.CountOf(wood), Is.EqualTo(1));
            Assert.That(again.ChooseExploration("exploration.test.alley-crate", "choice.test.inspect").Kind, Is.Not.EqualTo(ExplorationChoiceResultKind.Applied),
                "되풀이할 수 없는 탐색은 한 번만");
            Assert.That(again.Inventory.CountOf(wood), Is.EqualTo(1));
        }

        [Test]
        public void ClueSource_RevealedOnce_AndSaved()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);

            var first = game.RevealClueSource("clue.discovery.play-loop", "source.discovery.play-npc");
            Assert.That(first.Kind, Is.EqualTo(DiscoveryClueResultKind.Revealed));
            Assert.That(TestWorld.Start(saves.Store).DiscoveryClues.GetRecord("clue.discovery.play-loop").Seen, Is.True);

            var reloaded = TestWorld.Start(saves.Store);
            string Record() => System.Text.Json.JsonSerializer.Serialize(reloaded.DiscoveryClues.ToSaveData(), new System.Text.Json.JsonSerializerOptions { IncludeFields = true });
            var once = Record();
            reloaded.RevealClueSource("clue.discovery.play-loop", "source.discovery.play-npc");
            Assert.That(Record(), Is.EqualTo(once), "같은 출처를 다시 봐도 기록은 늘지 않는다");
        }

        [Test]
        public void ClueInterpretation_GoesThroughTheCoreRunner()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            var before = game.InterpretClue("interpret.ask-librarian");
            Assert.That(before.Kind, Is.Not.EqualTo(ClueInterpretationResultKind.Completed), "단서를 보기 전에는 해석할 수 없다");

            game.RevealClueSource("clue.discovery.play-loop", "source.discovery.play-npc");
            var after = game.InterpretClue("interpret.ask-librarian");
            Assert.That(after.Kind, Is.EqualTo(ClueInterpretationResultKind.Completed), after.Reason);
            Assert.That(TestWorld.Start(saves.Store).InterpretClue("interpret.ask-librarian").Kind, Is.Not.EqualTo(ClueInterpretationResultKind.Completed));
        }

        [Test]
        public void DialogueChoice_AcceptsTheQuestOnce_AndIsSaved()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            var dialogue = game.TalkTo("npc.first-guide");
            Assert.That(dialogue.Id, Is.EqualTo("Dialogue_FirstNpc"));

            Assert.That(game.ChooseDialogueOption(0), Is.True, "Choice_AcceptGatherWood");
            Assert.That(game.QuestLog.GetState("quest.gatherWood"), Is.EqualTo(Rootborn.Core.Quests.QuestStage.Active));
            Assert.That(game.ChooseDialogueOption(0), Is.False, "이미 받은 퀘스트");

            var again = TestWorld.Start(saves.Store);
            Assert.That(again.QuestLog.GetState("quest.gatherWood"), Is.EqualTo(Rootborn.Core.Quests.QuestStage.Active));
        }

        [Test]
        public void DailyEvent_AtTheSquare_AppliesTheChoiceOnce()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            Assert.That(game.AvailableDailyEvents("location.town-square").Select(e => e.Id), Does.Contain("daily-event.square-friend-request"));

            var result = game.ChooseDailyEvent("daily-event.square-friend-request", "daily-event-choice.square-help-now", "event:square:1", "location.town-square");
            Assert.That(result.Kind, Is.EqualTo(LifeActivityResultKind.Applied));
            Assert.That(game.Student.GetTraitValue("trait.empathy"), Is.EqualTo(1));

            var again = TestWorld.Start(saves.Store);
            Assert.That(again.ChooseDailyEvent("daily-event.square-friend-request", "daily-event-choice.square-help-now", "event:square:1", "location.town-square").Kind,
                Is.EqualTo(LifeActivityResultKind.DuplicateRequest));
            Assert.That(again.Student.GetTraitValue("trait.empathy"), Is.EqualTo(1));
        }
    
        [Test]
        public void DailyEvent_ProgressIsSaved_SoTheSameEventStaysAnsweredAfterReading()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            game.ChooseDailyEvent("daily-event.square-friend-request", "daily-event-choice.square-help-now", "event:square:1", "location.town-square");
            var expected = game.AvailableDailyEvents("location.town-square").Select(e => e.Id).ToArray();
            Assert.That(expected, Does.Not.Contain("daily-event.square-friend-request"), "고른 이벤트는 오늘 다시 나오지 않는다");

            var again = TestWorld.Start(saves.Store);
            Assert.That(again.DailyEvents.GetRecord("daily-event.square-friend-request").SelectedChoiceId, Is.EqualTo("daily-event-choice.square-help-now"));
            Assert.That(again.AvailableDailyEvents("location.town-square").Select(e => e.Id), Is.EqualTo(expected));
            Assert.That(saves.Store.TryRead<DailyEventProgressSaveData>(TestWorld.Slot, Rootborn.Core.Save.SaveFileNames.DailyEvent(TestWorld.Player), out _), Is.True);
        }
    }
}
