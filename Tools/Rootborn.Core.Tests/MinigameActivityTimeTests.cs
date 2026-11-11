using NUnit.Framework;
using Rootborn.Core.Careers;
using Rootborn.Core.Minigames;
using Rootborn.Core.StudentLife;
using Rootborn.Core.Time;

namespace Rootborn.Core.Tests
{
    /// <summary>
    /// 흐르는 시간 설계 3.2절: 미니게임이 있는 활동은 미니게임을 하는 동안 흐른 시간을 활동 분에서 뺀다.
    /// 시작할 때 시계를 건너뛰지 않고 끝나는 시각만 정한다(하는 중). 정산·포기 때 혼자면 남은 몫만 건너뛰고(0 아래로는 안 감),
    /// 같이 하면 끝나는 시각까지 하는 중으로 남는다. 그래서 걸리는 시간은 "활동 분과 미니게임 동안 흐른 분 중 큰 것"이다.
    /// </summary>
    public sealed class MinigameActivityTimeTests
    {
        private const int Start = 8 * 60, Cost = 120, WorkMinutes = 90;

        [Test]
        public void Begin_DoesNotSkipTheClock_ItOnlySetsWhenTheActivityEnds()
        {
            var f = new Fixture();
            Assert.That(f.BeginPractice("p").Applied, Is.True);
            Assert.That(f.Student.TimeMinutes, Is.EqualTo(Start), "미니게임을 하는 동안은 시계가 스스로 흐른다");
            Assert.That(f.Student.BusyUntilMinutes, Is.EqualTo(Start + Cost));
            Assert.That(f.Student.IsBusy, Is.True);
        }

        [Test]
        public void Settle_AfterAShortGame_SkipsOnlyTheRest()
        {
            var f = new Fixture();
            f.BeginPractice("p");
            f.Student.AdvanceClock(30); // 미니게임 동안 흐른 분
            Assert.That(f.Service.TrySettlePractice(f.Practice, f.Student, "p", true).Applied, Is.True);
            Assert.That(f.Student.TimeMinutes, Is.EqualTo(Start + Cost), "30분 흘렀으면 남은 90분만 건너뛴다(합 = 활동 분)");
            Assert.That(f.Student.IsBusy, Is.False);
        }

        [Test]
        public void Settle_AfterALongGame_DoesNotSkipBelowZero()
        {
            var f = new Fixture();
            f.BeginWork("w");
            f.Student.AdvanceClock(Cost + 25);
            Assert.That(f.Service.TrySettlePartTimeWork(f.Work, f.Student, f.Payroll, "w", true).Applied, Is.True);
            Assert.That(f.Student.TimeMinutes, Is.EqualTo(Start + Cost + 25), "흐른 시간이 활동 분보다 길면 더 건너뛰지 않는다");
        }

        [Test]
        public void Forfeit_AlsoSpendsTheRest_TheCostIsNotGivenBack()
        {
            var f = new Fixture();
            f.BeginPractice("p");
            f.Student.AdvanceClock(10);
            Assert.That(f.Service.TrySettlePractice(f.Practice, f.Student, "p", false).Applied, Is.True);
            Assert.That(f.Student.TimeMinutes, Is.EqualTo(Start + Cost));
        }

        [Test]
        public void Together_TheClockIsNotSkipped_ThePlayerStaysBusyUntilTheEnd()
        {
            var f = new Fixture();
            f.Student.ConfigureActivityClock(ActivityClockMode.StayBusy);
            f.BeginPractice("p");
            f.Student.AdvanceClock(30);
            f.Service.TrySettlePractice(f.Practice, f.Student, "p", true);
            Assert.That(f.Student.TimeMinutes, Is.EqualTo(Start + 30), "같이 하기는 시계를 건너뛰지 않는다");
            Assert.That(f.Student.IsBusy, Is.True, "끝나는 시각까지 하는 중");
            Assert.That(f.Student.BusyUntilMinutes, Is.EqualTo(Start + Cost));
            Assert.That(f.Student.CanStartActivity, Is.False);
            f.Student.AdvanceClock(Cost - 30);
            Assert.That(f.Student.CanStartActivity, Is.True);
        }

        [Test]
        public void FacilityWork_Reserve_ThenSettle_SpendsMaxOfWorkMinutesAndGameMinutes()
        {
            var f = new AdultCareerTests.Fixture(timeCostMinutes: WorkMinutes); f.Transition("t");
            int start = f.Student.TimeMinutes;
            Assert.That(f.Reserve("a").Applied, Is.True);
            Assert.That(f.Student.TimeMinutes, Is.EqualTo(start));
            f.Student.AdvanceClock(40);
            Assert.That(f.Settle("a", 100).Applied, Is.True);
            Assert.That(f.Student.TimeMinutes, Is.EqualTo(start + WorkMinutes));
        }

        [Test]
        public void FacilityWork_Forfeit_SpendsTheRestToo()
        {
            var f = new AdultCareerTests.Fixture(timeCostMinutes: WorkMinutes); f.Transition("t");
            int start = f.Student.TimeMinutes;
            f.Reserve("a");
            Assert.That(f.Service.TryForfeitReservedWork(f.Student, f.Progress, "a").Applied, Is.True);
            Assert.That(f.Student.TimeMinutes, Is.EqualTo(start + WorkMinutes));
        }

        [Test]
        public void SavedAndRead_TheEndOfTheActivityIsKept()
        {
            var f = new Fixture();
            f.Student.ConfigureTimeRules(new TimeRules(600f, 7 * 60, 22 * 60, 1, 2));
            f.BeginPractice("p");
            f.Student.AdvanceClock(20);
            var again = StudentLifeProgress.FromSaveData(f.Student.ToSaveData());
            again.ConfigureTimeRules(f.Student.TimeRules);
            again.RestoreClock(f.Student.ToClockSaveData());
            Assert.That(again.BusyUntilMinutes, Is.EqualTo(Start + Cost));
            Assert.That(f.Service.TrySettlePractice(f.Practice, again, "p", true).Applied, Is.True);
            Assert.That(again.TimeMinutes, Is.EqualTo(Start + Cost));
        }

        private sealed class Fixture
        {
            public readonly StudentLifeProgress Student = new StudentLifeProgress("slot", "player", 12, 10, 0, Start);
            public readonly AdultCareerProgress Adult = new AdultCareerProgress("slot", "player");
            public readonly CareerPractice Practice;
            public readonly FakePartTimeWork Work;
            public readonly StudentMinigameServiceTests.FakePayroll Payroll = new StudentMinigameServiceTests.FakePayroll();
            public readonly StudentMinigameService Service;

            public Fixture()
            {
                Student.ConfigureCalendar(new LifeCalendar(3, 3));
                var policy = new ActivityTimePolicy("practice-time", 1, 127, false);
                var game = new Minigame("minigame.fixture", null, new IChallenge[] { new ChoiceChallenge("c", new[] { 0 }, new[] { "prompt" }, new[] { "a", "b" }) }, null);
                var activity = new LifeActivity("activity.fixture", LifeActivityCategory.School, policy, Cost, 1, 0, 0, null, new ILifeActivityEffect[] { new TraitDeltaEffect("trait.fixture", 2) }, null);
                Practice = new CareerPractice("practice.fixture", null, activity, null, null, game);
                Work = new FakePartTimeWork("job.fixture", game, policy, Cost, 1, 0, 0);
                Service = new StudentMinigameService(new[] { Practice }, new IMinigamePartTimeWork[] { Work }, null, null);
            }

            public AdultCareerResult BeginPractice(string request) => Service.TryBeginPractice(Practice, Student, Adult, request, 1);
            public AdultCareerResult BeginWork(string request) => Service.TryBeginPartTimeWork(Work, Student, Adult, Payroll, request, 1);
        }
    }
}
