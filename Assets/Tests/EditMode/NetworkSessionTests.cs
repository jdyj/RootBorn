using NUnit.Framework;
using Rootborn.Game.Bootstrap;
using Rootborn.Network.Session;

namespace Rootborn.Tests.EditMode
{
    public sealed class NetworkSessionTests
    {
        [Test]
        public void NET_SESSION_001_FactoryCreatesHostClientServerSessionsForExplicitModes()
        {
            Assert.IsInstanceOf<HostSession>(NetworkSessionFactory.Create(SessionMode.Host));
            Assert.IsInstanceOf<ClientSession>(NetworkSessionFactory.Create(SessionMode.Client));
            Assert.IsInstanceOf<DedicatedServerSession>(NetworkSessionFactory.Create(SessionMode.Server));
        }

        [Test]
        public void NET_SESSION_002_FactoryFallsBackToSinglePlayerForNoneAndSingleModes()
        {
            Assert.IsInstanceOf<SinglePlayerSession>(NetworkSessionFactory.Create(SessionMode.None));
            Assert.IsInstanceOf<SinglePlayerSession>(NetworkSessionFactory.Create(SessionMode.Single));
        }

        [Test]
        public void NET_SESSION_003_SinglePlayerSessionEmitsLocalConnectAndDisconnectEvents()
        {
            var session = new SinglePlayerSession();
            ulong connected = ulong.MaxValue;
            ulong disconnected = ulong.MaxValue;
            session.OnPlayerConnected += id => connected = id;
            session.OnPlayerDisconnected += id => disconnected = id;

            session.StartAsync(new AppConfig()).GetAwaiter().GetResult();
            session.StopAsync().GetAwaiter().GetResult();

            Assert.AreEqual(SessionMode.Single, session.Mode);
            Assert.IsTrue(session.IsServer);
            Assert.IsTrue(session.IsClient);
            Assert.AreEqual(0UL, connected);
            Assert.AreEqual(0UL, disconnected);
        }
    }
}
