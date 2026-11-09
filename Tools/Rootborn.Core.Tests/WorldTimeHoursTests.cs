using NUnit.Framework;
using Rootborn.Core.Network;

namespace Rootborn.Core.Tests
{
    public sealed class WorldTimeHoursTests
    {
        [TestCase(0, 9f)]
        [TestCase(1, 14f)]
        [TestCase(2, 18.5f)]
        [TestCase(3, 22f)]
        public void EachPartOfDayHasARepresentativeHour(int partOfDay, float hour)
        {
            Assert.That(WorldTimeHours.Representative(partOfDay), Is.EqualTo(hour));
        }

        [Test]
        public void HoursRunForwardThroughTheDay()
        {
            for (int i = 1; i < 4; i++)
                Assert.That(WorldTimeHours.Representative(i), Is.GreaterThan(WorldTimeHours.Representative(i - 1)));
        }

        [TestCase(-1, 9f)]
        [TestCase(7, 22f)]
        public void OutOfRangeValuesClampToTheNearestPart(int partOfDay, float hour)
        {
            Assert.That(WorldTimeHours.Representative(partOfDay), Is.EqualTo(hour));
        }
    }
}
