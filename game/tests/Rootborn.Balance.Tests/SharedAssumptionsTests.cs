using NUnit.Framework;
using Rootborn.Game.Tests;

namespace Rootborn.Balance.Tests
{
    public sealed class SharedAssumptionsTests
    {
        [Test]
        public void AssumptionsComeFromTheDayBalanceFile()
        {
            var a = DayAssumptions.Current;
            Assert.That(a.TravelMinutes, Is.EqualTo(30));
            Assert.That(a.MinigameShare, Is.EqualTo(0.5f));
        }

        [Test]
        public void ANewGameStartsOnAMemoryStore()
        {
            var game = TestWorld.Start(new MemorySaveStore());
            Assert.That(game.Student.CurrentDay, Is.EqualTo(1));
            Assert.That(game.Inventory.Currency, Is.Not.Null, "통화는 game/economy.json에서 온다");
        }
    }
}
