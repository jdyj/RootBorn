using NUnit.Framework;
using Rootborn.Core.Time;

namespace Rootborn.Core.Tests
{
    // TIME-001~003 / STATUS-002: 일차와 하루 진행률은 지난 시간만으로 결정된다.
    public sealed class DayClockTests
    {
        [Test]
        public void TIME_001_DefaultDayIs600SecondsAndRollsToDay2()
        {
            var clock = new DayClock();
            Assert.That(clock.RealSecondsPerGameDay, Is.EqualTo(600f));

            Assert.That(clock.Tick(599f), Is.EqualTo(0));
            Assert.That(clock.Day, Is.EqualTo(1));
            Assert.That(clock.DayProgress01, Is.EqualTo(599f / 600f).Within(0.001f));

            Assert.That(clock.Tick(1f), Is.EqualTo(1));
            Assert.That(clock.Day, Is.EqualTo(2));
            Assert.That(clock.DayProgress01, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void TIME_003_OneTickAcrossSeveralDaysReportsEveryRolledDay()
        {
            var clock = new DayClock(100f, 3);
            Assert.That(clock.Day, Is.EqualTo(3));
            Assert.That(clock.Tick(250f), Is.EqualTo(2));
            Assert.That(clock.Day, Is.EqualTo(5));
            Assert.That(clock.DayProgress01, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(clock.ElapsedGameDays, Is.EqualTo(2.5f).Within(0.001f));
        }

        [Test]
        public void NegativeDelta_DoesNotMoveTimeBackwards()
        {
            var clock = new DayClock();
            clock.Tick(120f);
            Assert.That(clock.Tick(-5f), Is.EqualTo(0));
            Assert.That(clock.ElapsedRealSeconds, Is.EqualTo(120f));
        }

        [Test]
        public void HourAndMinute_FollowDayProgress()
        {
            var clock = new DayClock(2400f, 1); // 100초가 한 시간
            clock.Tick(950f); // 9.5시
            Assert.That(clock.CurrentHour, Is.EqualTo(9.5f).Within(0.001f));
            Assert.That(clock.Hour, Is.EqualTo(9));
            Assert.That(clock.Minute, Is.EqualTo(30));
        }

        [Test]
        public void SetCurrentHour_MovesWithinTheSameDay()
        {
            var clock = new DayClock(600f, 1);
            clock.Tick(700f); // 2일차
            clock.SetCurrentHour(12f);
            Assert.That(clock.Day, Is.EqualTo(2));
            Assert.That(clock.CurrentHour, Is.EqualTo(12f).Within(0.001f));

            clock.SetCurrentHour(-1f);
            Assert.That(clock.Day, Is.EqualTo(2));
            Assert.That(clock.CurrentHour, Is.EqualTo(23f).Within(0.001f));
        }

        [Test]
        public void Configure_ClampsToAtLeastOneSecondAndDayOne_AndKeepsElapsedTime()
        {
            var clock = new DayClock();
            clock.Tick(50f);
            clock.Configure(0f, 0);
            Assert.That(clock.RealSecondsPerGameDay, Is.EqualTo(1f));
            Assert.That(clock.ElapsedRealSeconds, Is.EqualTo(50f));
            Assert.That(clock.Day, Is.EqualTo(51)); // 시작 1일 + 1초짜리 하루 50번

            clock.Configure(100f, 4);
            Assert.That(clock.Day, Is.EqualTo(4));
            Assert.That(clock.DayProgress01, Is.EqualTo(0.5f).Within(0.001f));
        }
    }
}
