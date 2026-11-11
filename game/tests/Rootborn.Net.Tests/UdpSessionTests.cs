using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using NUnit.Framework;
using Rootborn.Core.Network;
using Rootborn.Core.World;
using Rootborn.Net.Session;
using Rootborn.Net.Transport;

namespace Rootborn.Net.Tests
{
    /// <summary>
    /// 같은 기계의 UDP 소켓(루프백 주소만, 포트는 운영체제가 준다)으로 서버 1 + 클라이언트 2. 실제 소켓을 지나는 자동 테스트다.
    /// 두 프로세스·두 기계·Steam은 아니다.
    /// </summary>
    [NonParallelizable]
    public sealed class UdpSessionTests
    {
        private const string Key = "rootborn-test";

        [Test]
        public void TwoClients_JoinSeeEachOtherChangeMapAndShareTheDay()
        {
            using var serverTransport = UdpTransport.Listen(IPAddress.Loopback, 0, Key);
            int port = serverTransport.LocalPort;
            Assert.That(port, Is.GreaterThan(0));
            using var server = new SessionServer(serverTransport, new SessionServerSettings { MaxPlayers = 2 });
            using var a = new SessionClient(UdpTransport.Connect(IPAddress.Loopback, IPAddress.Loopback.ToString(), port, Key), "a", "child|", new SessionClientSettings());
            using var b = new SessionClient(UdpTransport.Connect(IPAddress.Loopback, IPAddress.Loopback.ToString(), port, Key), "b", "child|hat", new SessionClientSettings());
            var clock = Stopwatch.StartNew();
            double Now() => clock.Elapsed.TotalSeconds;

            bool Pump(Func<bool> until, double seconds = 5.0)
            {
                double end = Now() + seconds;
                while (Now() < end)
                {
                    double now = Now();
                    server.Update(now);
                    a.Update(now);
                    b.Update(now);
                    if (until()) return true;
                    Thread.Sleep(5);
                }

                return false;
            }

            Assert.That(Pump(() => a.Status == ClientStatus.Joined && b.Status == ClientStatus.Joined), Is.True, "둘 다 들어온다");

            a.SetPose("Town", 10f, 20f, Facing.Left, false);
            b.SetPose("Town", 1f, 2f, Facing.Down, false);
            Assert.That(Pump(() => b.PlayersOn("Town", Now()).Any(p => p.PlayerId == "a" && p.X == 10f)), Is.True, "b가 a를 본다");
            Assert.That(Pump(() => a.PlayersOn("Town", Now()).Any(p => p.PlayerId == "b" && p.Look == "child|hat")), Is.True, "a가 b의 겉모습을 본다");

            a.SetPose("House", 300f, 40f, Facing.Up, false);
            Assert.That(Pump(() => b.PlayersOn("Town", Now()).Count == 0 && b.PlayersOn("House", Now()).Count == 1), Is.True, "맵을 옮기면 따라간다");

            a.RequestDayEndReady();
            Assert.That(Pump(() => b.WorldTime.ReadyCount == 1), Is.True);
            Assert.That(server.WorldTime.Day, Is.EqualTo(1), "한쪽만 준비하면 안 넘어간다");
            b.RequestDayEndReady();
            Assert.That(Pump(() => a.WorldTime.Day == 2 && b.WorldTime.Day == 2), Is.True, "둘 다 준비하면 넘어간다");

            using var c = new SessionClient(UdpTransport.Connect(IPAddress.Loopback, IPAddress.Loopback.ToString(), port, Key), "c", "child|", new SessionClientSettings());
            Assert.That(Pump(() =>
            {
                c.Update(Now());
                return c.Status == ClientStatus.Refused;
            }), Is.True, "정원 초과");
            Assert.That(c.FarewellReason, Is.EqualTo(SessionCapacity.ServerFullReason));
        }

        [Test]
        public void WrongConnectionKey_IsRejected()
        {
            using var serverTransport = UdpTransport.Listen(IPAddress.Loopback, 0, Key);
            using var server = new SessionServer(serverTransport, new SessionServerSettings());
            using var a = new SessionClient(UdpTransport.Connect(IPAddress.Loopback, IPAddress.Loopback.ToString(), serverTransport.LocalPort, "other-key"), "a", "child|", new SessionClientSettings());
            var clock = Stopwatch.StartNew();
            while (clock.Elapsed.TotalSeconds < 5 && a.Status == ClientStatus.Connecting)
            {
                server.Update(clock.Elapsed.TotalSeconds);
                a.Update(clock.Elapsed.TotalSeconds);
                Thread.Sleep(5);
            }

            Assert.That(a.Status, Is.EqualTo(ClientStatus.Disconnected));
            Assert.That(server.Players, Is.Empty);
        }
    }
}
