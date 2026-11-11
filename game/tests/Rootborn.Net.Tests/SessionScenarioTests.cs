using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Network;
using Rootborn.Core.Save;
using Rootborn.Core.Time;
using Rootborn.Core.World;
using Rootborn.Net.Session;

namespace Rootborn.Net.Tests
{
    /// <summary>같은 프로세스 전송으로 서버 1 + 클라이언트 여럿. 자동 테스트다(화면·실제 네트워크 근거가 아니다).</summary>
    public sealed class SessionScenarioTests
    {
        [Test]
        public void Join_IsApprovedAndGetsSpawnSpread()
        {
            using var rig = new SessionRig(maxPlayers: 4);
            var a = rig.Join("steam-a");
            var b = rig.Join("steam-b");
            rig.Run(0.5);

            Assert.That(a.Status, Is.EqualTo(ClientStatus.Joined));
            Assert.That(b.Status, Is.EqualTo(ClientStatus.Joined));
            Assert.That(rig.Server.Players.Select(p => p.PlayerId), Is.EquivalentTo(new[] { "steam-a", "steam-b" }));
            Assert.That(a.JoinIndex, Is.EqualTo(0));
            Assert.That(b.JoinIndex, Is.EqualTo(1));
            Assert.That(b.SpawnOffset.X, Is.EqualTo(SpawnSpread.Offset(1).X));
            Assert.That(b.SpawnOffset.Y, Is.EqualTo(SpawnSpread.Offset(1).Y));
            Assert.That(a.NetId, Is.Not.EqualTo(b.NetId));
        }

        [Test]
        public void Join_OverCapacity_IsRefusedWithServerFull()
        {
            using var rig = new SessionRig(maxPlayers: 2);
            var a = rig.Join("a");
            var b = rig.Join("b");
            rig.Run(0.5);
            var c = rig.Join("c");
            rig.Run(0.5);

            Assert.That(c.Status, Is.EqualTo(ClientStatus.Refused));
            Assert.That(c.FarewellReason, Is.EqualTo(SessionCapacity.ServerFullReason));
            Assert.That(SessionCapacity.IsServerFull(c.FarewellReason), Is.True);
            Assert.That(rig.Server.Players.Count, Is.EqualTo(2));
            Assert.That(a.Status, Is.EqualTo(ClientStatus.Joined));
            Assert.That(b.PlayersOn("Town", rig.Now).Count, Is.EqualTo(0), "거절된 사람은 남에게 보이지 않는다");
        }

        [Test]
        public void EmptyIdentity_IsRefused()
        {
            using var rig = new SessionRig();
            var a = rig.Join("");
            rig.Run(0.5);
            Assert.That(a.Status, Is.EqualTo(ClientStatus.Refused));
            Assert.That(a.FarewellReason, Is.EqualTo(SessionServer.InvalidIdentityReason));
        }

        [Test]
        public void PlayersSeeEachOthersPoseAndLook()
        {
            using var rig = new SessionRig();
            var a = rig.Join("a", "child|shirt");
            var b = rig.Join("b", "child|");
            rig.Run(0.3);
            a.SetPose("Town", 100f, 200f, Facing.Left, walking: true);
            b.SetPose("Town", 5f, 6f, Facing.Up, walking: false);
            rig.Run(1.0);

            var seenByB = b.PlayersOn("Town", rig.Now).Single();
            Assert.That(seenByB.PlayerId, Is.EqualTo("a"));
            Assert.That(seenByB.X, Is.EqualTo(100f));
            Assert.That(seenByB.Y, Is.EqualTo(200f));
            Assert.That(seenByB.Facing, Is.EqualTo(Facing.Left));
            Assert.That(seenByB.Walking, Is.True);
            Assert.That(seenByB.Look, Is.EqualTo("child|shirt"));

            var seenByA = a.PlayersOn("Town", rig.Now).Single();
            Assert.That(seenByA.PlayerId, Is.EqualTo("b"));
            Assert.That(seenByA.X, Is.EqualTo(5f));
            Assert.That(a.PlayersOn("Town", rig.Now).Any(p => p.PlayerId == "a"), Is.False, "자기 자신은 남의 목록에 없다");
        }

        [Test]
        public void Walking_IsSeenMovingBetweenSamples()
        {
            using var rig = new SessionRig();
            var a = rig.Join("a");
            var b = rig.Join("b");
            rig.Run(0.3);
            float x = 0f;
            rig.Run(2.0, now =>
            {
                x += 64f * (float)SessionRig.Step;
                a.SetPose("Town", x, 0f, Facing.Right, true);
            });
            float seen = b.PlayersOn("Town", rig.Now).Single().X;
            Assert.That(seen, Is.GreaterThan(x - 64f * 0.5f), "지연은 사이 메우기 지연 정도");
            Assert.That(seen, Is.LessThanOrEqualTo(x));
            a.SetPose("Town", x, 0f, Facing.Right, false);
            rig.Run(1.0);
            Assert.That(b.PlayersOn("Town", rig.Now).Single().X, Is.EqualTo(x), "멈추면 마지막 자리에 선다");
            Assert.That(b.PlayersOn("Town", rig.Now).Single().Walking, Is.False);
        }

        [Test]
        public void LookChange_ReachesOthers()
        {
            using var rig = new SessionRig();
            var a = rig.Join("a", "child|");
            var b = rig.Join("b");
            rig.Run(0.3);
            a.SetPose("Town", 1f, 1f, Facing.Down, false);
            a.SetLook("child|hat");
            rig.Run(0.5);
            Assert.That(b.PlayersOn("Town", rig.Now).Single().Look, Is.EqualTo("child|hat"));
            Assert.That(rig.Server.Players.Single(p => p.PlayerId == "a").Look, Is.EqualTo("child|hat"));
        }

        [Test]
        public void MovingToAnotherMap_DisappearsFromOldMapAndAppearsOnNew()
        {
            using var rig = new SessionRig();
            var a = rig.Join("a");
            var b = rig.Join("b");
            rig.Run(0.3);
            a.SetPose("Town", 10f, 10f, Facing.Down, false);
            b.SetPose("Town", 0f, 0f, Facing.Down, false);
            rig.Run(0.5);
            Assert.That(b.PlayersOn("Town", rig.Now).Count, Is.EqualTo(1));

            a.SetPose("House", 300f, 40f, Facing.Up, false);
            rig.Run(0.5);
            Assert.That(b.PlayersOn("Town", rig.Now).Count, Is.EqualTo(0));
            var inHouse = b.PlayersOn("House", rig.Now).Single();
            Assert.That(inHouse.PlayerId, Is.EqualTo("a"));
            Assert.That(inHouse.X, Is.EqualTo(300f));
            Assert.That(rig.Server.PlayersOnMap("House").Select(p => p.PlayerId), Is.EqualTo(new[] { "a" }));
            Assert.That(rig.Server.PlayersOnMap("Town").Select(p => p.PlayerId), Is.EqualTo(new[] { "b" }));
        }

        [Test]
        public void DayAdvances_OnlyWhenBothAreReady()
        {
            using var rig = new SessionRig();
            var a = rig.Join("a");
            var b = rig.Join("b");
            rig.Run(0.3);
            Assert.That(a.RoomClock.Known, Is.True);
            Assert.That(a.RoomClock.Day, Is.EqualTo(1));
            Assert.That(a.RoomClock.PlayerCount, Is.EqualTo(2));

            a.RequestDayEndReady();
            rig.Run(0.3);
            Assert.That(rig.Server.Clock.Day, Is.EqualTo(1));
            Assert.That(b.RoomClock.Day, Is.EqualTo(1));
            Assert.That(b.RoomClock.IsDayEnded(a.NetId), Is.True, "누가 마감했는지 보인다");
            Assert.That(b.RoomClock.DayEndedCount, Is.EqualTo(1));

            b.RequestDayEndReady();
            rig.Run(0.3);
            Assert.That(rig.Server.Clock.Day, Is.EqualTo(2));
            Assert.That(a.RoomClock.Day, Is.EqualTo(2));
            Assert.That(b.RoomClock.Day, Is.EqualTo(2));
            Assert.That(a.RoomClock.PartIndex, Is.EqualTo(0));
            Assert.That(a.RoomClock.DayEndedCount, Is.EqualTo(0));
        }

        [Test]
        public void FinishingAPart_IsSharedAndSaved()
        {
            var store = new MemorySaveStore();
            using (var rig = new SessionRig(store: store, saveSlot: "slot-0"))
            {
                var a = rig.Join("a");
                var b = rig.Join("b");
                rig.Run(0.3);
                a.RequestFinishPart(1);
                b.RequestSkipPart();
                rig.Run(0.3);
                Assert.That(b.RoomClock.PartIndex, Is.EqualTo(1));
                Assert.That(a.RoomClock.PartIndex, Is.EqualTo(1));
            }

            Assert.That(store.Contains("slot-0", RoomClockSaveFiles.FileName), Is.True);
            using var again = new SessionRig(store: store, saveSlot: "slot-0");
            Assert.That(again.Server.Clock.PartIndex, Is.EqualTo(1), "서버를 다시 켜면 이어진다");
        }

        [Test]
        public void LateJoiner_ReceivesCurrentPlayersMapsPosesLooksAndTime()
        {
            using var rig = new SessionRig();
            var a = rig.Join("a", "child|shirt");
            var b = rig.Join("b", "teen|");
            rig.Run(0.3);
            a.SetPose("Town", 11f, 12f, Facing.Left, false);
            b.SetPose("House", 21f, 22f, Facing.Up, false);
            a.RequestFinishPart(1);
            b.RequestFinishPart(1);
            rig.Run(0.5);

            var c = rig.Join("c");
            rig.Run(0.5);
            Assert.That(c.Status, Is.EqualTo(ClientStatus.Joined));
            var inTown = c.PlayersOn("Town", rig.Now).Single();
            Assert.That(inTown.PlayerId, Is.EqualTo("a"));
            Assert.That(inTown.X, Is.EqualTo(11f));
            Assert.That(inTown.Look, Is.EqualTo("child|shirt"));
            var inHouse = c.PlayersOn("House", rig.Now).Single();
            Assert.That(inHouse.PlayerId, Is.EqualTo("b"));
            Assert.That(inHouse.Facing, Is.EqualTo(Facing.Up));
            Assert.That(inHouse.Look, Is.EqualTo("teen|"));
            Assert.That(c.RoomClock.PartIndex, Is.EqualTo(1));
            Assert.That(c.RoomClock.PlayerCount, Is.EqualTo(3));
            Assert.That(c.RoomClock.HasFinished(c.NetId), Is.False, "도중에 들어오면 지금 때부터");
            Assert.That(c.JoinIndex, Is.EqualTo(2));
            Assert.That(a.RemotePlayerIds, Does.Contain("c"), "먼저 있던 사람도 새 사람을 안다");
        }

        [Test]
        public void Quitting_DisappearsAndRejoiningIsTheSamePlayer()
        {
            using var rig = new SessionRig();
            var a = rig.Join("a");
            var b = rig.Join("b");
            rig.Run(0.3);
            a.SetPose("Town", 1f, 1f, Facing.Down, false);
            rig.Run(0.3);
            ushort firstNetId = a.NetId;
            int firstIndex = a.JoinIndex;

            rig.Quit(a);
            rig.Run(0.3);
            Assert.That(b.RemotePlayerIds, Is.Empty);
            Assert.That(b.PlayersOn("Town", rig.Now), Is.Empty);
            Assert.That(rig.Server.Players.Select(p => p.PlayerId), Is.EqualTo(new[] { "b" }));
            Assert.That(b.RoomClock.PlayerCount, Is.EqualTo(1));

            var again = rig.Join("a");
            rig.Run(0.3);
            again.SetPose("Town", 50f, 1f, Facing.Down, false);
            rig.Run(0.3);
            Assert.That(again.NetId, Is.EqualTo(firstNetId), "같은 신원은 같은 번호");
            Assert.That(again.JoinIndex, Is.EqualTo(firstIndex), "같은 도착 자리");
            Assert.That(b.PlayersOn("Town", rig.Now).Single().X, Is.EqualTo(50f), "번호가 처음부터 다시 시작해도 보인다");
        }

        [Test]
        public void SameIdentityConnectingAgain_ReplacesTheOldConnection()
        {
            using var rig = new SessionRig(maxPlayers: 2);
            var a = rig.Join("a");
            var b = rig.Join("b");
            rig.Run(0.3);
            a.SetPose("Town", 1f, 1f, Facing.Down, false);
            rig.Run(0.3);
            rig.Freeze(a); // 옛 연결은 아직 끊긴 줄 모른다

            var again = rig.Join("a");
            rig.Run(0.3);
            again.SetPose("Town", 77f, 1f, Facing.Down, false);
            rig.Run(0.3);
            Assert.That(again.Status, Is.EqualTo(ClientStatus.Joined), "정원 2가 차 있어도 같은 사람은 들어온다");
            Assert.That(rig.Server.Players.Count, Is.EqualTo(2));
            Assert.That(b.PlayersOn("Town", rig.Now).Single().X, Is.EqualTo(77f));
        }

        [Test]
        public void SilentClient_TimesOutOnServerAndOthers()
        {
            using var rig = new SessionRig(serverSettings: new SessionServerSettings { TimeoutSeconds = 2.0 });
            var a = rig.Join("a");
            var b = rig.Join("b");
            rig.Run(0.3);
            a.SetPose("Town", 1f, 1f, Facing.Down, false);
            rig.Run(0.3);
            rig.Network.Sever(rig.TransportOf(a));
            rig.Run(1.0);
            Assert.That(b.RemotePlayerIds, Does.Contain("a"), "시간 초과 전에는 남아 있다");
            rig.Run(2.0);
            Assert.That(b.RemotePlayerIds, Is.Empty);
            Assert.That(rig.Server.Players.Select(p => p.PlayerId), Is.EqualTo(new[] { "b" }));
        }

        [Test]
        public void ClientNoticesServerSilence()
        {
            using var rig = new SessionRig();
            var a = rig.Join("a", settings: new SessionClientSettings { TimeoutSeconds = 2.0 });
            rig.Run(0.3);
            rig.Network.Sever(rig.TransportOf(a));
            rig.Run(3.0);
            Assert.That(a.Status, Is.EqualTo(ClientStatus.Disconnected));
            Assert.That(a.FarewellReason, Is.EqualTo(SessionClient.TimeoutReason));
        }

        [Test]
        public void ReadyPlayerLeaving_DoesNotSkipTheDayForTheOther()
        {
            using var rig = new SessionRig();
            var a = rig.Join("a");
            var b = rig.Join("b");
            rig.Run(0.3);
            a.RequestDayEndReady();
            rig.Run(0.3);
            rig.Quit(a);
            rig.Run(0.3);
            Assert.That(b.RoomClock.Day, Is.EqualTo(1));
            b.RequestDayEndReady();
            rig.Run(0.3);
            Assert.That(b.RoomClock.Day, Is.EqualTo(2));
        }

        [Test]
        public void ServerShuttingDown_TellsClients()
        {
            var rig = new SessionRig();
            var a = rig.Join("a");
            rig.Run(0.3);
            rig.Server.Dispose();
            rig.Run(0.3);
            Assert.That(a.Status, Is.EqualTo(ClientStatus.Disconnected));
            Assert.That(a.FarewellReason, Is.EqualTo(SessionServer.ShutdownReason));
        }
    }
}
