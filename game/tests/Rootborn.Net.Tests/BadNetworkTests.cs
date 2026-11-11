using System.Linq;
using NUnit.Framework;
using Rootborn.Core.World;
using Rootborn.Net.Messages;
using Rootborn.Net.Session;
using Rootborn.Net.Transport;

namespace Rootborn.Net.Tests
{
    /// <summary>나쁜 네트워크와 나쁜 바이트. 끝 상태가 맞고, 세션이 죽지 않는다.</summary>
    public sealed class BadNetworkTests
    {
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void LossLatencyAndReordering_EndStateIsRight(int seed)
        {
            using var rig = new SessionRig(seed: seed);
            rig.Network.LatencySeconds = 0.08;
            rig.Network.JitterSeconds = 0.12;  // 비신뢰는 순서가 바뀐다
            rig.Network.LossRate = 0.3;
            var a = rig.Join("a", "child|");
            var b = rig.Join("b");
            rig.Run(1.0);
            Assert.That(a.Status, Is.EqualTo(ClientStatus.Joined));
            Assert.That(b.Status, Is.EqualTo(ClientStatus.Joined));

            float x = 0f;
            rig.Run(3.0, now =>
            {
                x += 1f;
                a.SetPose("Town", x, 5f, Facing.Right, true);
            });
            a.SetPose("House", 300f, 40f, Facing.Up, false);
            a.SetLook("child|hat");
            a.RequestDayEndReady();
            b.RequestDayEndReady();
            rig.Run(2.0);

            Assert.That(b.PlayersOn("Town", rig.Now), Is.Empty);
            var seen = b.PlayersOn("House", rig.Now).Single();
            Assert.That(seen.X, Is.EqualTo(300f));
            Assert.That(seen.Y, Is.EqualTo(40f));
            Assert.That(seen.Facing, Is.EqualTo(Facing.Up));
            Assert.That(seen.Look, Is.EqualTo("child|hat"));
            Assert.That(a.RoomClock.Day, Is.EqualTo(2));
            Assert.That(b.RoomClock.Day, Is.EqualTo(2));
        }

        [Test]
        public void ReorderedPoses_NeverMoveSomeoneBackwards()
        {
            using var rig = new SessionRig(seed: 7);
            rig.Network.LatencySeconds = 0.05;
            rig.Network.JitterSeconds = 0.2;
            var a = rig.Join("a");
            var b = rig.Join("b");
            rig.Run(0.5);
            float x = 0f;
            float lastSeen = float.MinValue;
            bool wentBack = false;
            rig.Run(3.0, now =>
            {
                x += 1f;
                a.SetPose("Town", x, 0f, Facing.Right, true);
                var view = b.PlayersOn("Town", now).FirstOrDefault();
                if (view == null) return;
                if (view.X < lastSeen) wentBack = true;
                lastSeen = view.X;
            });
            Assert.That(wentBack, Is.False);
            Assert.That(lastSeen, Is.GreaterThan(0f));
        }

        [Test]
        public void GarbageUnknownTooNewAndTooLargeMessages_AreDroppedAndCounted()
        {
            using var rig = new SessionRig();
            var a = rig.Join("a");
            var b = rig.Join("b");
            rig.Run(0.3);
            a.SetPose("Town", 1f, 2f, Facing.Down, false);
            rig.Run(0.3);

            var raw = rig.TransportOf(b);
            var server = new PeerId(0);
            raw.Send(server, new byte[0], Delivery.ReliableOrdered);                          // 머리 없음
            raw.Send(server, new byte[] { 1 }, Delivery.ReliableOrdered);                     // 잘린 머리
            raw.Send(server, new byte[] { 250, 250, 1, 9, 9 }, Delivery.ReliableOrdered);     // 모르는 종류
            raw.Send(server, new byte[] { 6, 0, 99, 0, 0 }, Delivery.ReliableOrdered);        // 더 높은 버전의 위치
            raw.Send(server, new byte[] { 6, 0, 1, 0, 0, 1 }, Delivery.ReliableOrdered);      // 잘린 위치 몸통
            raw.Send(server, new byte[rig.ServerSettings.MaxMessageBytes + 1], Delivery.ReliableOrdered); // 너무 큼
            raw.Send(server, new byte[] { 1, 0, 1, 255, 255 }, Delivery.ReliableOrdered);     // 길이가 거짓말인 글자
            rig.Run(0.5);

            var drops = rig.Server.Drops;
            Assert.That(drops.Malformed, Is.EqualTo(4));
            Assert.That(drops.UnknownKind, Is.EqualTo(1));
            Assert.That(drops.TooNewVersion, Is.EqualTo(1));
            Assert.That(drops.TooLarge, Is.EqualTo(1));

            // 세션은 계속 돈다.
            Assert.That(b.Status, Is.EqualTo(ClientStatus.Joined));
            Assert.That(rig.Server.Players.Count, Is.EqualTo(2));
            a.SetPose("Town", 9f, 2f, Facing.Down, false);
            rig.Run(0.5);
            Assert.That(b.PlayersOn("Town", rig.Now).Single().X, Is.EqualTo(9f));
        }

        [Test]
        public void GarbageFromServer_IsDroppedByClient()
        {
            var network = new LoopbackNetwork();
            var serverEnd = network.Listen();
            var client = new SessionClient(network.Connect(), "a", "child|", new SessionClientSettings());
            double now = 0;
            PeerId peer = default;
            for (int i = 0; i < 10; i++)
            {
                now += 0.02;
                serverEnd.Update(now);
                while (serverEnd.TryReceive(out var e)) if (e.Kind == TransportEventKind.Connected) peer = e.Peer;
                client.Update(now);
            }

            serverEnd.Send(peer, new byte[] { 3 }, Delivery.ReliableOrdered);
            serverEnd.Send(peer, new byte[] { 200, 0, 1 }, Delivery.Unreliable);
            for (int i = 0; i < 5; i++)
            {
                now += 0.02;
                serverEnd.Update(now);
                client.Update(now);
            }

            Assert.That(client.Drops.Malformed, Is.EqualTo(1));
            Assert.That(client.Drops.UnknownKind, Is.EqualTo(1));
            Assert.That(client.Status, Is.EqualTo(ClientStatus.Connecting));
        }

        [Test]
        public void MessagesBeforeHello_AreIgnored()
        {
            using var rig = new SessionRig();
            var catalog = RootbornMessages.CreateCatalog(rig.ServerSettings.MaxMessageBytes);
            var raw = rig.Network.Connect();
            rig.Run(0.2, now => raw.Update(now));
            catalog.TryEncode(new PoseMessage { MapId = "Town", Sequence = 1 }, out var pose);
            catalog.TryEncode(new DayEndReadyMessage(), out var ready);
            raw.Send(new PeerId(0), pose, Delivery.ReliableOrdered);
            raw.Send(new PeerId(0), ready, Delivery.ReliableOrdered);
            rig.Run(0.2, now => raw.Update(now));
            Assert.That(rig.Server.Players, Is.Empty);
            Assert.That(rig.Server.Drops.Unexpected, Is.EqualTo(2));
            Assert.That(rig.Server.Clock.PresentCount, Is.EqualTo(0));
            Assert.That(rig.Server.Clock.Day, Is.EqualTo(1));
        }

        [Test]
        public void ClientCannotMoveSomeoneElse()
        {
            using var rig = new SessionRig();
            var a = rig.Join("a");
            var b = rig.Join("b");
            rig.Run(0.3);
            a.SetPose("Town", 1f, 1f, Facing.Down, false);
            rig.Run(0.3);
            var catalog = RootbornMessages.CreateCatalog(rig.ServerSettings.MaxMessageBytes);
            // b가 a의 번호를 적어 보내도 서버는 b의 위치로 본다.
            catalog.TryEncode(new PoseMessage { NetId = a.NetId, Sequence = 999999, SenderTime = rig.Now, MapId = "Town", X = -40f }, out var fake);
            rig.TransportOf(b).Send(new PeerId(0), fake, Delivery.ReliableOrdered);
            rig.Run(0.3);
            Assert.That(rig.Server.Players.Single(p => p.PlayerId == "a").LastPose.X, Is.EqualTo(1f));
            Assert.That(rig.Server.Players.Single(p => p.PlayerId == "b").LastPose.X, Is.EqualTo(-40f));
        }
    }
}
