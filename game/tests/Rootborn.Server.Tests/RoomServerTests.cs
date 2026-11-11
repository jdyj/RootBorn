using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Network;
using Rootborn.Core.World;
using Rootborn.Data.Save;
using Rootborn.Net.Room;
using Rootborn.Net.Session;
using Rootborn.Net.Transport;

namespace Rootborn.Server.Tests
{
    /// <summary>방(서버 쪽 논리)을 같은 프로세스 안 전송으로. 자동 테스트이고 실제 소켓·두 프로세스는 아니다.</summary>
    public sealed class RoomServerTests
    {
        private string _saveRoot;

        [SetUp]
        public void SetUp()
        {
            _saveRoot = Path.Combine(Path.GetTempPath(), "rootborn-room-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_saveRoot)) Directory.Delete(_saveRoot, true);
        }

        [Test]
        public void TwoJoin_AndSeeEachOthersPosition()
        {
            using var rig = new RoomRig(new FileSaveStore(_saveRoot));
            var a = rig.Join("a");
            var b = rig.Join("b");
            rig.Run(0.5);
            Assert.That(a.Status, Is.EqualTo(ClientStatus.Joined));
            Assert.That(b.Status, Is.EqualTo(ClientStatus.Joined));
            Assert.That(rig.Room.Players.Select(p => p.PlayerId), Is.EquivalentTo(new[] { "a", "b" }));

            a.SetPose("Town", 10f, 20f, Facing.Left, false);
            b.SetPose("Town", 3f, 4f, Facing.Up, true);
            rig.Run(1.0);
            var seenByB = b.PlayersOn("Town", rig.Clock.Now).Single();
            Assert.That(seenByB.PlayerId, Is.EqualTo("a"));
            Assert.That(seenByB.X, Is.EqualTo(10f));
            Assert.That(a.PlayersOn("Town", rig.Clock.Now).Single().PlayerId, Is.EqualTo("b"));
            Assert.That(rig.Log.Any(l => l.Contains("a") && l.Contains("들어옴")), Is.True, "들어온 것을 로그에 남긴다");
        }

        [Test]
        public void OverCapacity_IsRefusedWithReasonCode()
        {
            using var rig = new RoomRig(new FileSaveStore(_saveRoot), maxPlayers: 2);
            var a = rig.Join("a");
            rig.Join("b");
            rig.Run(0.5);
            var c = rig.Join("c");
            rig.Run(0.5);
            Assert.That(c.Status, Is.EqualTo(ClientStatus.Refused));
            Assert.That(c.FarewellReason, Is.EqualTo(RoomRefusal.Full));
            Assert.That(rig.Room.Players.Count, Is.EqualTo(2));
            Assert.That(a.Status, Is.EqualTo(ClientStatus.Joined));
        }

        [TestCase(1, "game-1", "data-1", RoomRefusal.ProtocolMismatch)]
        [TestCase(0, "game-2", "data-1", RoomRefusal.GameVersionMismatch)]
        [TestCase(0, "game-1", "data-2", RoomRefusal.DataVersionMismatch)]
        public void DifferentVersion_IsRefusedWithReasonCode(int protocolOffset, string game, string data, string expected)
        {
            using var rig = new RoomRig(new FileSaveStore(_saveRoot));
            var stranger = rig.Join("x", new RoomVersions(RoomProtocol.Version + protocolOffset, game, data));
            rig.Run(0.5);
            Assert.That(stranger.Status, Is.EqualTo(ClientStatus.Refused));
            Assert.That(stranger.FarewellReason, Is.EqualTo(expected));
            Assert.That(rig.Room.Players, Is.Empty, "거절된 사람은 방에 없다");
            Assert.That(rig.Log.Any(l => l.Contains(expected)), Is.True, "거절 이유를 로그에 남긴다");
        }

        [Test]
        public void ClientWithoutHandshake_IsRefusedAsProtocolMismatch()
        {
            using var rig = new RoomRig(new FileSaveStore(_saveRoot));
            var old = rig.JoinWithoutHandshake("old");
            rig.Run(0.5);
            Assert.That(old.Status, Is.EqualTo(ClientStatus.Refused));
            Assert.That(old.FarewellReason, Is.EqualTo(RoomRefusal.ProtocolMismatch));
            Assert.That(rig.Room.Players, Is.Empty);
        }

        [Test]
        public void ConnectionThatNeverIntroducesItself_IsDroppedAfterHandshakeTimeout()
        {
            using var rig = new RoomRig(new FileSaveStore(_saveRoot), settings: new RoomServerSettings { HandshakeTimeoutSeconds = 2 });
            var silent = rig.Network.Connect();
            rig.Run(3.0);
            silent.Update(rig.Clock.Now);
            bool disconnected = false;
            while (silent.TryReceive(out var e))
                if (e.Kind == TransportEventKind.Disconnected) disconnected = true;
            Assert.That(disconnected, Is.True);
            Assert.That(rig.Log.Any(l => l.Contains(RoomRefusal.HandshakeTimeout)), Is.True);
        }

        [Test]
        public void Leaving_NotifiesThoseWhoRemain()
        {
            using var rig = new RoomRig(new FileSaveStore(_saveRoot));
            var a = rig.Join("a");
            var b = rig.Join("b");
            a.SetPose("Town", 1f, 1f, Facing.Down, false);
            rig.Run(1.0);
            Assert.That(b.RemotePlayerIds, Is.EquivalentTo(new[] { "a" }));

            rig.Quit(a);
            rig.Run(0.5);
            Assert.That(b.RemotePlayerIds, Is.Empty, "남은 사람에게 나간 것을 알린다");
            Assert.That(b.PlayersOn("Town", rig.Clock.Now), Is.Empty);
            Assert.That(rig.Room.Players.Select(p => p.PlayerId), Is.EquivalentTo(new[] { "b" }));
            Assert.That(rig.Log.Any(l => l.Contains("a") && l.Contains("나감")), Is.True);
        }

        [Test]
        public void RestartWithSameSave_ContinuesTheRoom()
        {
            using (var rig = new RoomRig(new FileSaveStore(_saveRoot)))
            {
                Assert.That(rig.Room.IsNewRoom, Is.True);
                var a = rig.Join("a");
                var b = rig.Join("b");
                rig.Run(0.5);
                a.RequestDayEndReady();
                b.RequestDayEndReady();
                rig.Run(0.5);
                a.RequestAdvancePartOfDay();
                rig.Run(0.5);
                Assert.That(rig.Room.WorldTime.Day, Is.EqualTo(2));
                Assert.That(rig.Room.WorldTime.PartOfDay, Is.EqualTo(WorldPartOfDay.Afternoon));
                rig.Room.Shutdown();
                rig.Run(0.5);
                Assert.That(a.Status, Is.EqualTo(ClientStatus.Disconnected), "끄면 들어와 있던 사람은 끊긴다");
                Assert.That(a.FarewellReason, Is.EqualTo(RoomRefusal.Shutdown));
            }

            using (var again = new RoomRig(new FileSaveStore(_saveRoot)))
            {
                Assert.That(again.Room.IsNewRoom, Is.False);
                Assert.That(again.Room.WorldTime.Day, Is.EqualTo(2));
                Assert.That(again.Room.WorldTime.PartOfDay, Is.EqualTo(WorldPartOfDay.Afternoon));
                var a = again.Join("a");
                again.Run(0.5);
                Assert.That(a.WorldTime.Day, Is.EqualTo(2), "다시 들어온 사람도 이어진 날을 받는다");
            }
        }

        [Test]
        public void NewRoom_IsWrittenAtStart_SoASecondStartContinues()
        {
            using (var rig = new RoomRig(new FileSaveStore(_saveRoot)))
                Assert.That(rig.Room.IsNewRoom, Is.True);
            using (var again = new RoomRig(new FileSaveStore(_saveRoot)))
                Assert.That(again.Room.IsNewRoom, Is.False);
        }

        [Test]
        public void EmptyRoom_ClockDoesNotMove()
        {
            using var rig = new RoomRig(new FileSaveStore(_saveRoot));
            rig.Run(30.0);
            Assert.That(rig.Loop.Ticks, Is.GreaterThan(0), "틱은 돈다");
            Assert.That(rig.Room.WorldTime.Day, Is.EqualTo(1));
            Assert.That(rig.Room.WorldTime.PartOfDay, Is.EqualTo(WorldPartOfDay.Morning));

            var a = rig.Join("a");
            rig.Run(0.5);
            a.RequestDayEndReady();
            rig.Run(0.5);
            Assert.That(rig.Room.WorldTime.Day, Is.EqualTo(2));
            rig.Quit(a);
            rig.Run(30.0);
            Assert.That(rig.Room.WorldTime.Day, Is.EqualTo(2), "다 나간 뒤에도 시계는 그대로다");
            Assert.That(rig.Room.WorldTime.ReadyCount, Is.EqualTo(0));
        }

        [Test]
        public void Settings_RejectCapacityOutsideOneToEight()
        {
            Assert.Throws<ArgumentException>(() => new RoomServerSettings { MaxPlayers = 0 }.Validate());
            Assert.Throws<ArgumentException>(() => new RoomServerSettings { MaxPlayers = RoomServerSettings.MaxPlayersLimit + 1 }.Validate());
            Assert.DoesNotThrow(() => new RoomServerSettings { MaxPlayers = RoomServerSettings.MaxPlayersLimit }.Validate());
            Assert.That(new RoomServerSettings().MaxPlayers, Is.EqualTo(4));
            Assert.That(RoomServerSettings.MaxPlayersLimit, Is.EqualTo(8));
        }
    }
}
