using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using NUnit.Framework;
using Rootborn.Core.Save;
using Rootborn.Core.World;
using Rootborn.Net.Room;
using Rootborn.Net.Session;
using Rootborn.Net.Transport;

namespace Rootborn.Net.Tests
{
    /// <summary>
    /// 내 PC에서 여는 방(설계 6절 "내 PC에서 열기"): 같은 프로세스 안에 방을 띄우고, 방을 연 사람은 같은 프로세스 안 전송으로,
    /// 다른 사람은 UDP로 들어온다. 방은 넘긴 시계로 고정 간격 틱을 돈다. UDP는 같은 기계의 루프백 주소만(포트는 운영체제가 준다 —
    /// 같은 기계 안에서만 오가는 시험용 값이라 안전하다). 자동 테스트이고 두 기계·Steam 근거가 아니다.
    /// </summary>
    [NonParallelizable]
    public sealed class HostedRoomTests
    {
        private static readonly RoomVersions Versions = new RoomVersions(RoomProtocol.Version, "1.0.0+test", "data-1");

        /// <summary>테스트가 돌리는 시계(실제 시간). UDP 상대가 실제 시간으로 살아 있다는 표시를 보내므로 방도 실제 시간을 따른다.</summary>
        private sealed class TestClock : IRoomClock
        {
            private readonly Stopwatch _watch = Stopwatch.StartNew();
            public double Now => _watch.Elapsed.TotalSeconds;
        }

        private static HostedRoom Open(ISaveStore store, IRoomClock clock, int maxPlayers = 4, string slot = "slot-0", List<string> log = null) =>
            HostedRoom.Open(IPAddress.Loopback, 0, new RoomServerSettings { MaxPlayers = maxPlayers }, Versions, store, slot, clock, l => log?.Add(l));

        private static SessionClient Friend(HostedRoom room, string id, RoomVersions? versions = null) =>
            RoomClient.Create(UdpTransport.Connect(IPAddress.Loopback, IPAddress.Loopback.ToString(), room.Port, RoomProtocol.ConnectionKey),
                versions ?? Versions, id, "child|", new SessionClientSettings());

        private static SessionClient Me(HostedRoom room, string id = "me") =>
            RoomClient.Create(room.ConnectLocal(), Versions, id, "child|hat", new SessionClientSettings());

        /// <summary>방(그 시계의 틱)과 클라이언트들을 조건이 될 때까지 돌린다.</summary>
        private static bool Pump(HostedRoom room, IRoomClock clock, Func<bool> until, params SessionClient[] clients)
        {
            var watch = Stopwatch.StartNew();
            while (watch.Elapsed.TotalSeconds < 5)
            {
                room.RunDue();
                foreach (SessionClient c in clients) c.Update(clock.Now);
                if (until()) return true;
                Thread.Sleep(2);
            }

            return false;
        }

        [Test]
        public void HostOverTheInProcessTransport_AndAFriendOverUdp_JoinAndSeeEachOther()
        {
            var clock = new TestClock();
            var log = new List<string>();
            using HostedRoom room = Open(new MemorySaveStore(), clock, log: log);
            Assert.That(room.Port, Is.GreaterThan(0), "포트 0이면 운영체제가 빈 포트를 준다");
            Assert.That(room.ListenAddress, Is.EqualTo("127.0.0.1:" + room.Port));
            Assert.That(room.Server.IsNewRoom, Is.True);
            using SessionClient me = Me(room);
            using SessionClient friend = Friend(room, "friend");
            Assert.That(Pump(room, clock, () => me.Status == ClientStatus.Joined && friend.Status == ClientStatus.Joined, me, friend), Is.True);
            Assert.That(room.PlayerCount, Is.EqualTo(2));
            Assert.That(room.Server.Players.Select(p => p.PlayerId).OrderBy(x => x), Is.EqualTo(new[] { "friend", "me" }));

            me.SetPose("Town", 10f, 20f, Facing.Left, false);
            friend.SetPose("Town", 1f, 2f, Facing.Down, true);
            Assert.That(Pump(room, clock, () => friend.PlayersOn("Town", clock.Now).Any(p => p.PlayerId == "me" && p.X == 10f && p.Look == "child|hat"), me, friend), Is.True,
                "UDP 친구가 같은 프로세스 안의 나를 본다");
            Assert.That(Pump(room, clock, () => me.PlayersOn("Town", clock.Now).Any(p => p.PlayerId == "friend" && p.Walking), me, friend), Is.True,
                "나도 친구를 본다");
            Assert.That(log.Any(l => l.Contains("들어옴: me")) && log.Any(l => l.Contains("들어옴: friend")), Is.True, string.Join("\n", log));
        }

        [Test]
        public void FullRoom_AndOtherVersion_AreRefusedWithTheReason()
        {
            var clock = new TestClock();
            using HostedRoom room = Open(new MemorySaveStore(), clock, maxPlayers: 2);
            using SessionClient me = Me(room);
            using SessionClient friend = Friend(room, "friend");
            Assert.That(Pump(room, clock, () => me.Status == ClientStatus.Joined && friend.Status == ClientStatus.Joined, me, friend), Is.True);

            using SessionClient third = Friend(room, "third");
            Assert.That(Pump(room, clock, () => third.Status == ClientStatus.Refused, me, friend, third), Is.True, "정원 초과");
            Assert.That(third.FarewellReason, Is.EqualTo(RoomRefusal.Full));

            using SessionClient other = Friend(room, "other", new RoomVersions(Versions.Protocol, Versions.Game + ".other", Versions.Data));
            Assert.That(Pump(room, clock, () => other.Status == ClientStatus.Refused || other.Status == ClientStatus.Disconnected, me, friend, other), Is.True);
            Assert.That(other.FarewellReason, Is.EqualTo(RoomRefusal.GameVersionMismatch));
            Assert.That(room.PlayerCount, Is.EqualTo(2));
        }

        [Test]
        public void Shutdown_TellsEveryone_AndSavesTheRoom()
        {
            var clock = new TestClock();
            var store = new MemorySaveStore();
            HostedRoom room = Open(store, clock);
            using SessionClient me = Me(room);
            using SessionClient friend = Friend(room, "friend");
            Assert.That(Pump(room, clock, () => me.Status == ClientStatus.Joined && friend.Status == ClientStatus.Joined, me, friend), Is.True);
            store.Delete("slot-0", SaveFileNames.RoomClock);

            room.Shutdown();
            Assert.That(room.IsOpen, Is.False);
            Assert.That(room.RunDue(), Is.EqualTo(0), "닫은 방은 돌지 않는다");
            Assert.That(store.Contains("slot-0", SaveFileNames.RoomClock), Is.True, "닫을 때 방의 시계를 내 슬롯에 저장한다");
            var watch = Stopwatch.StartNew();
            while (watch.Elapsed.TotalSeconds < 5 && (friend.Status == ClientStatus.Joined || me.Status == ClientStatus.Joined))
            {
                me.Update(clock.Now);
                friend.Update(clock.Now);
                Thread.Sleep(2);
            }
            Assert.That(friend.FarewellReason, Is.EqualTo(RoomRefusal.Shutdown), "UDP 친구에게 닫혔다고 알린다");
            Assert.That(me.FarewellReason, Is.EqualTo(RoomRefusal.Shutdown), "같은 프로세스 안의 나에게도");
            room.Shutdown(); // 두 번 불러도 된다
            room.Dispose();
        }

        [Test]
        public void ReopeningTheSameSlot_ContinuesTheRoom()
        {
            var clock = new TestClock();
            var store = new MemorySaveStore();
            using (HostedRoom room = Open(store, clock))
            {
                using SessionClient me = Me(room);
                using SessionClient friend = Friend(room, "friend");
                Assert.That(Pump(room, clock, () => me.Status == ClientStatus.Joined && friend.Status == ClientStatus.Joined, me, friend), Is.True);
                me.RequestDayEndReady();
                friend.RequestDayEndReady();
                Assert.That(Pump(room, clock, () => room.Server.Clock.Day == 2, me, friend), Is.True, "둘 다 하루를 마감하면 다음 날");
            }

            using HostedRoom again = Open(store, clock);
            Assert.That(again.Server.IsNewRoom, Is.False);
            Assert.That(again.Server.Clock.Day, Is.EqualTo(2), "같은 슬롯으로 다시 열면 그 날에서 잇는다");
            using HostedRoom other = Open(store, clock, slot: "slot-1");
            Assert.That(other.Server.IsNewRoom, Is.True, "다른 슬롯은 새 방");
        }

        [Test]
        public void PortInUse_ThrowsAndLeavesNothingOpen()
        {
            var clock = new TestClock();
            using HostedRoom first = Open(new MemorySaveStore(), clock);
            Assert.That(() => HostedRoom.Open(IPAddress.Loopback, first.Port, new RoomServerSettings(), Versions, new MemorySaveStore(), "slot-0", clock, null),
                Throws.InstanceOf<Exception>(), "같은 포트는 들을 수 없다");
        }

        [Test]
        public void MergedTransport_KeepsPeersApart_AndRoutesSendsAndDisconnects()
        {
            var a = new LoopbackNetwork(1);
            var b = new LoopbackNetwork(2);
            using var merged = new MergedServerTransport(a.Listen(), b.Listen());
            LoopbackTransport ca = a.Connect(), cb = b.Connect();
            merged.Update(0);
            ca.Update(0);
            cb.Update(0);
            var connected = new List<PeerId>();
            while (merged.TryReceive(out TransportEvent e))
                if (e.Kind == TransportEventKind.Connected) connected.Add(e.Peer);
            Assert.That(connected, Has.Count.EqualTo(2));
            Assert.That(connected[0], Is.Not.EqualTo(connected[1]), "두 전송의 1번 연결이 서로 다른 번호가 된다");
            Assert.That(connected.Select(MergedServerTransport.TransportIndexOf), Is.EquivalentTo(new[] { 0, 1 }));

            PeerId toB = connected.First(p => MergedServerTransport.TransportIndexOf(p) == 1);
            merged.Send(toB, new byte[] { 7 }, Delivery.ReliableOrdered);
            merged.Update(0);
            ca.Update(0);
            cb.Update(0);
            while (ca.TryReceive(out TransportEvent e)) Assert.That(e.Kind, Is.Not.EqualTo(TransportEventKind.Data), "a 쪽에는 가지 않는다");
            Assert.That(cb.TryReceive(out TransportEvent got) && got.Kind == TransportEventKind.Connected, Is.True);
            Assert.That(cb.TryReceive(out got) && got.Kind == TransportEventKind.Data && got.Data[0] == 7, Is.True, "b 쪽에 간다");

            cb.Send(new PeerId(0), new byte[] { 9 }, Delivery.ReliableOrdered);
            cb.Update(0);
            merged.Update(0);
            Assert.That(merged.TryReceive(out TransportEvent up) && up.Kind == TransportEventKind.Data && up.Peer.Equals(toB), Is.True, "받은 것은 바꿔 붙인 번호로 올라온다");

            merged.Disconnect(toB, null);
            merged.Update(0);
            cb.Update(0);
            Assert.That(cb.TryReceive(out TransportEvent gone) && gone.Kind == TransportEventKind.Disconnected, Is.True);
            merged.Send(new PeerId(-3), new byte[] { 1 }, Delivery.Unreliable); // 모르는 번호는 아무 일 없다
            merged.Send(new PeerId(5 * MergedServerTransport.MaxTransports + 6), new byte[] { 1 }, Delivery.Unreliable);
        }
    }
}
