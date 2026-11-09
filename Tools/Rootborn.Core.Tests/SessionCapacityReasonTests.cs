using NUnit.Framework;
using Rootborn.Core.Network;

namespace Rootborn.Core.Tests
{
    public sealed class SessionCapacityReasonTests
    {
        [Test]
        public void IsServerFull_MatchesOnlyTheCapacityReason()
        {
            Assert.That(SessionCapacity.IsServerFull(SessionCapacity.ServerFullReason), Is.True);
            Assert.That(SessionCapacity.IsServerFull(string.Empty), Is.False);
            Assert.That(SessionCapacity.IsServerFull(null), Is.False);
            Assert.That(SessionCapacity.IsServerFull(SessionCapacity.InvalidLimitReason), Is.False);
        }
    }
}
