using NUnit.Framework;
using Rootborn.Host.World;

namespace Rootborn.Host.Tests
{
    /// <summary>게임 한 판 없이 띄운 판(--world)의 NPC 시각: 시계의 날은 00:00에 넘어가도 NPC의 하루는 하루 시작에 넘어간다. 창 없이 계산만 본다(자동 테스트).</summary>
    [TestFixture]
    public sealed class NpcTimeWithoutGameTests
    {
        private const int DayStart = 6 * 60;

        [Test]
        public void BeforeMidnightAndAfter_AreTheSameDayAndContinuousMinutes()
        {
            WorldScreen.NpcTimeWithoutGame(3, 23f + 59f / 60f, DayStart, out int dayBefore, out double before);
            WorldScreen.NpcTimeWithoutGame(4, 1f / 60f, DayStart, out int dayAfter, out double after);
            Assert.That(dayAfter, Is.EqualTo(dayBefore), "00:01은 전날 밤의 이어짐이다");
            Assert.That(after - before, Is.EqualTo(2.0).Within(1e-3), "분이 이어진다");
        }

        [Test]
        public void AfterTheDayStart_UsesTheClockDay_AndTheDayIsNeverBelowOne()
        {
            WorldScreen.NpcTimeWithoutGame(4, 7f, DayStart, out int day, out double minutes);
            Assert.That(day, Is.EqualTo(4));
            Assert.That(minutes, Is.EqualTo(7 * 60).Within(1e-3));
            WorldScreen.NpcTimeWithoutGame(1, 1f, DayStart, out day, out _);
            Assert.That(day, Is.EqualTo(1), "1일차 새벽은 0일로 내려가지 않는다");
        }
    }
}
