using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Net.Transport;

namespace Rootborn.Net.Tests
{
    public sealed class LoopbackTransportTests
    {
        private static List<TransportEvent> Drain(ITransport t, double now)
        {
            t.Update(now);
            var list = new List<TransportEvent>();
            while (t.TryReceive(out var e)) list.Add(e);
            return list;
        }

        [Test]
        public void Connect_DeliversConnectedToBothSidesAfterLatency()
        {
            var network = new LoopbackNetwork { LatencySeconds = 0.1 };
            var server = network.Listen();
            var client = network.Connect();
            Assert.That(Drain(server, 0.05), Is.Empty);
            Assert.That(Drain(server, 0.1)[0].Kind, Is.EqualTo(TransportEventKind.Connected));
            Assert.That(Drain(client, 0.1)[0].Kind, Is.EqualTo(TransportEventKind.Connected));
        }

        [Test]
        public void Reliable_KeepsOrderUnderJitter_AndIsNeverLost()
        {
            var network = new LoopbackNetwork(seed: 3) { LatencySeconds = 0.02, JitterSeconds = 0.3, LossRate = 0.9 };
            var server = network.Listen();
            var client = network.Connect();
            var serverEvents = Drain(server, 0.05);
            Drain(client, 0.05);
            var peer = serverEvents[0].Peer;
            double now = 0.05;
            for (byte i = 0; i < 50; i++)
            {
                now += 0.01;
                client.Update(now);
                client.Send(new PeerId(0), new[] { i }, Delivery.ReliableOrdered);
            }

            var got = new List<byte>();
            for (int i = 0; i < 100; i++)
            {
                now += 0.01;
                foreach (var e in Drain(server, now))
                    if (e.Kind == TransportEventKind.Data && e.Peer.Equals(peer)) got.Add(e.Data[0]);
            }

            Assert.That(got.Count, Is.EqualTo(50));
            for (int i = 0; i < 50; i++) Assert.That(got[i], Is.EqualTo(i));
        }

        [Test]
        public void Unreliable_CanBeLostAndReordered()
        {
            var network = new LoopbackNetwork(seed: 5) { LatencySeconds = 0.02, JitterSeconds = 0.3, LossRate = 0.3 };
            var server = network.Listen();
            var client = network.Connect();
            Drain(server, 0.05);
            Drain(client, 0.05);
            double now = 0.05;
            for (byte i = 0; i < 100; i++)
            {
                now += 0.01;
                client.Update(now);
                client.Send(new PeerId(0), new[] { i }, Delivery.Unreliable);
            }

            var got = new List<byte>();
            for (int i = 0; i < 100; i++)
            {
                now += 0.01;
                foreach (var e in Drain(server, now)) if (e.Kind == TransportEventKind.Data) got.Add(e.Data[0]);
            }

            Assert.That(got.Count, Is.LessThan(100).And.GreaterThan(40));
            bool reordered = false;
            for (int i = 1; i < got.Count; i++) if (got[i] < got[i - 1]) reordered = true;
            Assert.That(reordered, Is.True);
        }

        [Test]
        public void Disconnect_CarriesReasonToTheOtherSideOnly()
        {
            var network = new LoopbackNetwork();
            var server = network.Listen();
            var client = network.Connect();
            var peer = Drain(server, 0.01)[0].Peer;
            Drain(client, 0.01);
            server.Disconnect(peer, new byte[] { 4, 2 });
            Assert.That(Drain(server, 0.02), Is.Empty);
            var events = Drain(client, 0.02);
            Assert.That(events[0].Kind, Is.EqualTo(TransportEventKind.Disconnected));
            Assert.That(events[0].Data, Is.EqualTo(new byte[] { 4, 2 }));
            client.Send(new PeerId(0), new byte[] { 1 }, Delivery.ReliableOrdered);
            Assert.That(Drain(server, 0.05), Is.Empty, "끊긴 뒤 보낸 것은 닿지 않는다");
        }

        [Test]
        public void Sever_LosesEverythingSilently()
        {
            var network = new LoopbackNetwork();
            var server = network.Listen();
            var client = network.Connect();
            Drain(server, 0.01);
            Drain(client, 0.01);
            network.Sever(client);
            client.Send(new PeerId(0), new byte[] { 1 }, Delivery.ReliableOrdered);
            client.Dispose();
            Assert.That(Drain(server, 1.0), Is.Empty);
        }

        [Test]
        public void ConnectWithoutServer_Fails()
        {
            var network = new LoopbackNetwork();
            var client = network.Connect();
            Assert.That(Drain(client, 0.01)[0].Kind, Is.EqualTo(TransportEventKind.Disconnected));
        }
    }
}
