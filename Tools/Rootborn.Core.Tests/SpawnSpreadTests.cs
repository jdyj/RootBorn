using NUnit.Framework;
using Rootborn.Core.Network;

namespace Rootborn.Core.Tests
{
    public sealed class SpawnSpreadTests
    {
        [Test]
        public void FirstPlayerStandsOnTheSpawnPoint()
        {
            var offset = SpawnSpread.Offset(0);
            Assert.That(offset.X, Is.EqualTo(0f));
            Assert.That(offset.Y, Is.EqualTo(0f));
        }

        [Test]
        public void EachOfTheFirstEightPlayersGetsTheirOwnSpot()
        {
            var seen = new System.Collections.Generic.HashSet<(float, float)>();
            for (int i = 0; i < 8; i++)
            {
                var offset = SpawnSpread.Offset(i);
                Assert.That(seen.Add((offset.X, offset.Y)), Is.True, "player " + i + " would stand on someone");
                Assert.That(System.Math.Abs(offset.X), Is.LessThanOrEqualTo(2f), "spots stay near the spawn point");
                Assert.That(System.Math.Abs(offset.Y), Is.LessThanOrEqualTo(1f));
            }
        }

        [Test]
        public void SpotsAreAtLeastOneBodyApart()
        {
            for (int a = 0; a < 8; a++)
                for (int b = a + 1; b < 8; b++)
                {
                    var p = SpawnSpread.Offset(a);
                    var q = SpawnSpread.Offset(b);
                    double distance = System.Math.Sqrt((p.X - q.X) * (p.X - q.X) + (p.Y - q.Y) * (p.Y - q.Y));
                    Assert.That(distance, Is.GreaterThanOrEqualTo(1.0), a + " and " + b + " overlap");
                }
        }

        [Test]
        public void NegativeIndexIsTreatedAsTheFirstPlayer()
        {
            var offset = SpawnSpread.Offset(-3);
            Assert.That(offset.X, Is.EqualTo(0f));
            Assert.That(offset.Y, Is.EqualTo(0f));
        }
    }
}
