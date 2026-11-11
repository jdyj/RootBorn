using NUnit.Framework;
using Rootborn.Core.Time;

namespace Rootborn.Core.Tests
{
    public sealed class DayPartsTests
    {
        [TestCase(4.99f, false)]
        [TestCase(5f, true)]
        [TestCase(10.99f, true)]
        [TestCase(11f, false)]
        [TestCase(29f, true)] // 29시는 다음 날 5시
        public void Morning_IsFiveToEleven(float hour, bool expected) => Assert.That(DayParts.IsMorningHour(hour), Is.EqualTo(expected));

        [TestCase(10.99f, false)]
        [TestCase(11f, true)]
        [TestCase(16.99f, true)]
        [TestCase(17f, false)]
        public void Afternoon_IsElevenToSeventeen(float hour, bool expected) => Assert.That(DayParts.IsAfternoonHour(hour), Is.EqualTo(expected));

        [TestCase(16.99f, false)]
        [TestCase(17f, true)]
        [TestCase(19.99f, true)]
        [TestCase(20f, false)]
        public void Evening_IsSeventeenToTwenty(float hour, bool expected) => Assert.That(DayParts.IsEveningHour(hour), Is.EqualTo(expected));

        [TestCase(19.99f, false)]
        [TestCase(20f, true)]
        [TestCase(4.99f, true)]
        [TestCase(5f, false)]
        [TestCase(-1f, true)] // 23시
        [TestCase(24f, true)] // 0시
        public void Night_IsTwentyToFive(float hour, bool expected) => Assert.That(DayParts.IsNightHour(hour), Is.EqualTo(expected));

        [Test]
        public void EveryHourBelongsToExactlyOnePart()
        {
            for (float hour = 0f; hour < 24f; hour += 0.25f)
            {
                int parts = (DayParts.IsMorningHour(hour) ? 1 : 0) + (DayParts.IsAfternoonHour(hour) ? 1 : 0)
                          + (DayParts.IsEveningHour(hour) ? 1 : 0) + (DayParts.IsNightHour(hour) ? 1 : 0);
                Assert.That(parts, Is.EqualTo(1), "hour " + hour);
            }
        }
    }
}
