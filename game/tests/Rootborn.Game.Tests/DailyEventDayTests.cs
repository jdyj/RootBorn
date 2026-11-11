// 원래 흐름: Unity E2E들이 마을에 들어갈 때마다 지나가는 하루 이벤트(PlayerFlowE2EDriver.DismissDailyEvent).
//            선택 결과는 SideDomainTests가 본다. 여기서는 날이 바뀔 때의 흐름을 본다.
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.StudentLife;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 광장의 하루 이벤트(되풀이 안 되는 종류): 답한 뒤에는 그날도 다음 날도 목록에 나오지 않고, 다시 읽어도 그렇다. 거절은 성장을 주지 않는다.
    /// 답한 이벤트를 새 요청으로 다시 고르는 경우는 Core가 막지 않아 여기서 보지 않는다(SCENARIOS.md "필요한 변경"). 자동 테스트다.
    /// </summary>
    public sealed class DailyEventDayTests
    {
        private const string Square = "location.town-square";
        private const string Event = "daily-event.square-friend-request";

        [Test]
        public void DailyEvent_AnsweredOnce_IsNotOfferedTheNextDayEither()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            Assert.That(game.ChooseDailyEvent(Event, "daily-event-choice.square-help-now", "event:square:day1", Square).Kind, Is.EqualTo(LifeActivityResultKind.Applied));
            int empathy = game.Student.GetTraitValue("trait.empathy");

            game = TestWorld.Start(saves.Store);
            Assert.That(game.EndDay().Ended, Is.True);
            Assert.That(game.StartNextDay(), Is.True);
            game = TestWorld.Start(saves.Store);
            Assert.That(game.Student.CurrentDay, Is.EqualTo(2));
            Assert.That(game.AvailableDailyEvents(Square).Select(e => e.Id), Does.Not.Contain(Event), "되풀이 안 되는 이벤트는 다음 날에도 나오지 않는다");
            Assert.That(game.Student.GetTraitValue("trait.empathy"), Is.EqualTo(empathy));
        }

        [Test]
        public void DailyEvent_Declining_GrowsNoEmpathy_AndTheEventIsNoLongerOffered()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            int empathy = game.Student.GetTraitValue("trait.empathy");
            Assert.That(game.ChooseDailyEvent(Event, "daily-event-choice.square-decline", "event:square:decline", Square).Kind, Is.EqualTo(LifeActivityResultKind.Applied));

            game = TestWorld.Start(saves.Store);
            Assert.That(game.Student.GetTraitValue("trait.empathy"), Is.EqualTo(empathy));
            Assert.That(game.AvailableDailyEvents(Square).Select(e => e.Id), Does.Not.Contain(Event));
            Assert.That(game.DailyEvents.GetRecord(Event).SelectedChoiceId, Is.EqualTo("daily-event-choice.square-decline"));
        }
    }
}
