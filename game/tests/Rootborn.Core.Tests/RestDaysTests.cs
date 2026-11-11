using NUnit.Framework;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Tests
{
    /// <summary>
    /// 방에 없던 날은 쉰 날(멀티플레이 설계 10절 7번, 사용자 확인): 며칠 뒤 다시 들어온 사람은 방의 날짜로 맞춘다.
    /// 그 사이 날마다 하루 끝 규칙(체력·집중 회복)은 적용하고, 그날 못 한 활동에 대한 불이익은 없다.
    /// </summary>
    public sealed class RestDaysTests
    {
        private static readonly DayEndRuleSet Rules = new DayEndRuleSet("rules", "home", new IDayEndRule[] { new EnergyRestoreRule(10) });

        private static StudentLifeProgress Student()
        {
            var student = new StudentLifeProgress("slot", "player", 10, 10, 0, 480);
            student.ConfigureCalendar(new LifeCalendar());
            return student;
        }

        [Test]
        public void REST_001_MidDay_RestsThatDayAndTheDaysBetween_AndStartsTheRoomsDay()
        {
            var student = Student();
            student.Spend(0, 7, 0, 0);
            Assert.That(student.TrySpendTimePoints(1), Is.True);

            Assert.That(student.RestUntil(4, Rules), Is.EqualTo(3), "1·2·3일을 쉰 날로 넘긴다");
            Assert.That(student.CurrentDay, Is.EqualTo(4));
            Assert.That(student.DayState, Is.EqualTo(StudentDayState.InProgress));
            Assert.That(student.Energy, Is.EqualTo(10), "하루 끝 규칙의 체력 회복은 적용한다");
            Assert.That(student.TimePointsSpentToday, Is.EqualTo(0), "방의 날은 처음부터 한다");
            Assert.That(student.LastSettledDay, Is.EqualTo(3));
        }

        [Test]
        public void REST_002_AfterEndingTheDay_RestsOnlyTheDaysBetween()
        {
            var student = Student();
            Assert.That(student.TryEndDay(Rules, out _), Is.True);
            Assert.That(student.RestUntil(3, Rules), Is.EqualTo(2), "끝낸 1일 다음 날(2일)부터 방의 날(3일)까지");
            Assert.That(student.CurrentDay, Is.EqualTo(3));
            Assert.That(student.DayState, Is.EqualTo(StudentDayState.InProgress));
        }

        [Test]
        public void REST_003_SameOrEarlierDay_ChangesNothing()
        {
            var student = Student();
            Assert.That(student.RestUntil(1, Rules), Is.EqualTo(0));
            Assert.That(student.RestUntil(0, Rules), Is.EqualTo(0));
            Assert.That(student.CurrentDay, Is.EqualTo(1));
            Assert.That(student.TrySpendTimePoints(1), Is.True, "그날은 그대로 한다");
        }
    }
}
