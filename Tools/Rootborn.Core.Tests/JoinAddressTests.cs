using NUnit.Framework;
using Rootborn.Core.Network;

namespace Rootborn.Core.Tests
{
    public sealed class JoinAddressTests
    {
        [TestCase("127.0.0.1", "7777", "127.0.0.1", 7777)]
        [TestCase(" 192.168.0.10 ", " 7861 ", "192.168.0.10", 7861)]
        [TestCase("0.0.0.0", "1", "0.0.0.0", 1)]
        [TestCase("255.255.255.255", "65535", "255.255.255.255", 65535)]
        public void TryParse_AcceptsIpv4AndPort(string host, string port, string expectedHost, int expectedPort)
        {
            Assert.That(JoinAddress.TryParse(host, port, out var address), Is.EqualTo(JoinAddressError.None));
            Assert.That(address.Host, Is.EqualTo(expectedHost));
            Assert.That(address.Port, Is.EqualTo((ushort)expectedPort));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void TryParse_ReportsMissingHost(string host)
        {
            Assert.That(JoinAddress.TryParse(host, "7777", out _), Is.EqualTo(JoinAddressError.MissingHost));
        }

        // Unity Transport connects to an IP endpoint; a host name or a malformed address would fail only after a timeout.
        [TestCase("localhost")]
        [TestCase("192.168.0")]
        [TestCase("192.168.0.1.5")]
        [TestCase("256.1.1.1")]
        [TestCase("1.2.3.-4")]
        [TestCase("1.2..4")]
        [TestCase("1.2.3.4:7777")]
        [TestCase("１.2.3.4")]
        public void TryParse_ReportsInvalidHost(string host)
        {
            Assert.That(JoinAddress.TryParse(host, "7777", out _), Is.EqualTo(JoinAddressError.InvalidHost));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("0")]
        [TestCase("65536")]
        [TestCase("-1")]
        [TestCase("77a7")]
        [TestCase("99999999999")]
        public void TryParse_ReportsInvalidPort(string port)
        {
            Assert.That(JoinAddress.TryParse("127.0.0.1", port, out _), Is.EqualTo(JoinAddressError.InvalidPort));
        }

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
