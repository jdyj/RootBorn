using NUnit.Framework;
using Rootborn.Core.Network;

namespace Rootborn.Core.Tests
{
    public sealed class SessionCapacityTests
    {
        [TestCase(0, 4, true)]
        [TestCase(3, 4, true)]
        [TestCase(4, 4, false)]
        [TestCase(7, 4, false)]
        [TestCase(0, 1, true)]
        [TestCase(1, 1, false)]
        public void Decide_AcceptsOnlyWhileBelowMaxPlayers(int connected, int maxPlayers, bool expected)
        {
            var decision = SessionCapacity.Decide(connected, maxPlayers);
            Assert.That(decision.Approved, Is.EqualTo(expected));
            Assert.That(decision.Reason, expected ? Is.Empty : Is.EqualTo(SessionCapacity.ServerFullReason));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Decide_RejectsWhenMaxPlayersIsNotPositive(int maxPlayers)
        {
            var decision = SessionCapacity.Decide(0, maxPlayers);
            Assert.That(decision.Approved, Is.False);
            Assert.That(decision.Reason, Is.EqualTo(SessionCapacity.InvalidLimitReason));
        }
    }
}
