using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Dialogue;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;

namespace Rootborn.Game.Tests
{
    /// <summary>화면이 직접 하던 대화 선택지 판정과 하루 이벤트 미루기·지나가기를 GameSession이 한다. 자동 테스트다.</summary>
    public sealed class DialogueAndDailyEventQueryTests
    {
        private const string LibraryEvent = "daily-event.library-hard-book";
        private const string SquareEvent = "daily-event.square-friend-request";

        [Test]
        public void DialogueChoices_GiveEachChoiceWithWhetherItCanBeChosen()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            Assert.That(game.DialogueChoices(), Is.Empty, "열린 대화가 없으면 빈 목록");

            game.TalkTo("npc.first-guide");
            var before = game.DialogueChoices();
            Assert.That(before.Count, Is.EqualTo(game.CurrentDialogue.Choices.Count), "정의 순서대로 모든 선택지");
            Assert.That(before[0].Index, Is.EqualTo(0));
            Assert.That(before[0].Choice, Is.SameAs(game.CurrentDialogue.Choices[0]));
            Assert.That(before[0].Evaluation.Visible && before[0].Evaluation.CanChoose, Is.True, "받기 전 퀘스트 받기");

            Assert.That(game.ChooseDialogueOption(0), Is.True);
            var after = game.DialogueChoices();
            Assert.That(after[0].Evaluation.CanChoose, Is.False);
            Assert.That(after[0].Evaluation.Block, Is.EqualTo(DialogueChoiceBlock.QuestAlreadyStarted));
            Assert.That(after[0].Evaluation.Visible, Is.False, "Unity 판처럼 받은 퀘스트의 받기는 숨긴다");

            game.CloseDialogue();
            Assert.That(game.DialogueChoices(), Is.Empty);
        }

        [Test]
        public void DeferDailyEvent_MovesItToALaterDay_AndIsSaved()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);

            Assert.That(game.DeferDailyEvent(LibraryEvent, out var record), Is.True);
            Assert.That(record.DueDay, Is.EqualTo(game.StudentDay + 1), "미루는 날 수 1(데이터)");

            var again = TestWorld.Start(saves.Store);
            Assert.That(again.DailyEvents.GetRecord(LibraryEvent).DueDay, Is.EqualTo(record.DueDay), "미룬 것이 저장된다");
            Assert.That(again.AvailableDailyEvents("location.library").Select(e => e.Id), Does.Not.Contain(LibraryEvent), "미룬 이벤트는 오늘 다시 나오지 않는다");
        }

        [Test]
        public void DeferDailyEvent_RefusesAMissingEvent_WithoutSaving()
        {
            // 지금 data/의 하루 이벤트는 둘 다 미룰 수 있는 종류다. 미룰 수 없는 종류의 거절은 Core DailyEventTests가 본다.
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            Assert.That(game.DeferDailyEvent("daily-event.missing", out var missing), Is.False);
            Assert.That(missing, Is.Null);
            Assert.That(saves.Store.TryRead<DailyEventProgressSaveData>(TestWorld.Slot, SaveFileNames.DailyEvent(TestWorld.Player), out _), Is.False,
                "적용되지 않은 명령은 저장하지 않는다");
        }

        [Test]
        public void DeclineDailyEvent_PassesItForToday_AndIsSaved()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            Assert.That(game.AvailableDailyEvents("location.town-square").Select(e => e.Id), Does.Contain(SquareEvent));

            Assert.That(game.DeclineDailyEvent(SquareEvent, out var record), Is.True);
            Assert.That(record.State, Is.EqualTo(DailyEventStates.Declined));
            Assert.That(game.Student.GetTraitValue("trait.empathy"), Is.EqualTo(0), "지나가기는 선택지 결과를 주지 않는다");

            var again = TestWorld.Start(saves.Store);
            Assert.That(again.DailyEvents.GetRecord(SquareEvent).State, Is.EqualTo(DailyEventStates.Declined));
            Assert.That(again.AvailableDailyEvents("location.town-square").Select(e => e.Id), Does.Not.Contain(SquareEvent));
            Assert.That(again.DeclineDailyEvent("daily-event.missing", out _), Is.False);
        }
    }
}
