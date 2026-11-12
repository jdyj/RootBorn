using System.Net;
using NUnit.Framework;
using Rootborn.Net.Room;
using Rootborn.Net.Session;

namespace Rootborn.Net.Tests
{
    /// <summary>
    /// 게임 루프가 오래 멈춘 뒤(창을 끌거나 디버거·느린 디스크로 프레임이 한참 안 옴) 내 PC에서 연 방의 방장 접속이 시간 초과로 끊기지 않는다.
    /// Host와 같은 순서로 돈다: 한 프레임에 방장의 클라이언트 갱신 → 방의 틱(RunDue). 시계는 손으로 돌린다 — 벽시계를 기다리지 않는다.
    /// 자동 테스트다(같은 프로세스 안 전송, 실제 창이 아니다).
    /// </summary>
    [NonParallelizable]
    public sealed class LongStallTests
    {
        private const double Frame = 1.0 / 60.0;
        private static readonly RoomVersions Versions = new RoomVersions(RoomProtocol.Version, "1.0.0+test", "data-1");

        private sealed class HandClock : IRoomClock
        {
            public double Now { get; set; } = 100.0;
        }

        private static void Frames(HandClock clock, HostedRoom room, SessionClient me, double seconds)
        {
            for (double t = 0; t < seconds; t += Frame)
            {
                clock.Now += Frame;
                me.Update(clock.Now);
                room.RunDue();
            }
        }

        [TestCase(15.0)]
        [TestCase(60.0)]
        public void HostsOwnConnection_SurvivesALongFrameStall(double stallSeconds)
        {
            var clock = new HandClock();
            using HostedRoom room = HostedRoom.Open(IPAddress.Loopback, 0, new RoomServerSettings(), Versions, new MemorySaveStore(), "slot-0", clock, null);
            using SessionClient me = RoomClient.Create(room.ConnectLocal(), Versions, "me", "child|", new SessionClientSettings());
            Frames(clock, room, me, 2.0);
            Assert.That(me.Status, Is.EqualTo(ClientStatus.Joined));
            Assert.That(stallSeconds, Is.GreaterThan(new SessionClientSettings().TimeoutSeconds), "시간 초과보다 오래 멈춘다");

            clock.Now += stallSeconds; // 이 사이 프레임이 하나도 오지 않았다
            Frames(clock, room, me, 3.0);

            Assert.That(me.Status, Is.EqualTo(ClientStatus.Joined), "방장 자신의 접속이 끊기지 않는다: " + me.FarewellReason);
            Assert.That(room.PlayerCount, Is.EqualTo(1), "방도 방장을 지우지 않았다");
        }

        [Test]
        public void ASilentConnection_IsStillDropped_WhenTheLoopKeepsRunning()
        {
            // 루프가 멈춘 것이 아니라 상대가 조용한 것이면 지금처럼 끊긴다(멈춤으로 보는 것은 갱신 사이가 길 때뿐이다).
            var clock = new HandClock();
            using HostedRoom room = HostedRoom.Open(IPAddress.Loopback, 0, new RoomServerSettings(), Versions, new MemorySaveStore(), "slot-0", clock, null);
            using SessionClient me = RoomClient.Create(room.ConnectLocal(), Versions, "me", "child|", new SessionClientSettings());
            Frames(clock, room, me, 2.0);
            Assert.That(me.Status, Is.EqualTo(ClientStatus.Joined));

            // 클라이언트는 얼고(갱신 없음) 방만 돈다.
            for (double t = 0; t < 12.0; t += Frame)
            {
                clock.Now += Frame;
                room.RunDue();
            }

            Assert.That(room.PlayerCount, Is.Zero, "조용한 연결은 시간 초과로 지운다");
        }
    }
}
