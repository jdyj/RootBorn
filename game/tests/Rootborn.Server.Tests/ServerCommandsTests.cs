using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Save;
using Rootborn.Core.Time;
using Rootborn.Net.Room;
using Rootborn.Net.Session;
using Rootborn.Net.Transport;

namespace Rootborn.Server.Tests
{
    /// <summary>
    /// 서버 콘솔 명령(status, players, save, help, stop, 모르는 명령)과 주기 저장(틱 수로 잰다). 같은 프로세스 전송 위의 방, 손으로 돌리는 시계, 메모리 저장소.
    /// 자동 테스트다. 실제 프로세스의 콘솔은 <see cref="ServerOperationsProcessTests"/>.
    /// </summary>
    public sealed class ServerCommandsTests
    {
        private sealed class Rig : IDisposable
        {
            public readonly LoopbackNetwork Network = new LoopbackNetwork(1);
            public readonly ManualRoomClock Clock = new ManualRoomClock();
            public readonly List<string> Log = new List<string>();
            public readonly RoomServerSettings Settings;
            public readonly ObservedSaveStore Store;
            public readonly RoomServer Room;
            public readonly ServerRunner Runner;
            public DateTime Now = new DateTime(2026, 11, 12, 9, 30, 0);
            private readonly List<SessionClient> _clients = new List<SessionClient>();

            public Rig(double autosaveSeconds = 0, ServerCommands commands = null, params string[] shared)
            {
                Settings = new RoomServerSettings { MaxPlayers = 3, SharedPieces = shared };
                Store = new ObservedSaveStore(new MemoryStore(), () => Now);
                Room = new RoomServer(Network.Listen(), Settings, RoomRig.Versions, Store, "ops-room", Log.Add);
                Runner = new ServerRunner(Room, Clock, Settings.TickSeconds, "ops-room", Store, autosaveSeconds, Log.Add, commands);
            }

            public SessionClient Join(string id, string name = null)
            {
                var c = RoomClient.Create(Network.Connect(), RoomRig.Versions, id, "child|", new SessionClientSettings(), name);
                _clients.Add(c);
                return c;
            }

            public void Run(double seconds)
            {
                double end = Clock.Now + seconds;
                while (Clock.Now < end - 1e-9)
                {
                    Clock.Now += Settings.TickSeconds;
                    Runner.RunDue();
                    foreach (var c in _clients) c.Update(Clock.Now);
                }
            }

            public List<string> Command(string line)
            {
                int before = Log.Count;
                Runner.Enqueue(line);
                Runner.RunDue();
                return Log.Skip(before).ToList();
            }

            public void Dispose()
            {
                foreach (var c in _clients) c.Dispose();
                Room.Dispose();
            }
        }

        [Test]
        public void Status_ShowsDayTimePlayersSharingUptimeAndLastSave()
        {
            using var rig = new Rig(0, null, "wallet");
            var a = rig.Join("player-a", "하늘");
            var b = rig.Join("player-b");
            rig.Run(2.0);
            Assert.That(a.Status, Is.EqualTo(ClientStatus.Joined));
            Assert.That(b.Status, Is.EqualTo(ClientStatus.Joined));

            string text = string.Join("\n", rig.Command("status"));
            StringAssert.Contains("슬롯 ops-room", text);
            StringAssert.Contains(rig.Room.DayAndTime, text, "방의 날·시각");
            StringAssert.Contains("같이 쓰는 것 " + rig.Room.ShareSettings, text);
            StringAssert.Contains("들어와 있는 사람: 2명", text);
            StringAssert.Contains("하늘 [player-a]", text, "이름과 신원");
            StringAssert.Contains("  player-b (번호", text, "이름이 없으면 신원만");
            StringAssert.Contains("가동 시간: 00:00:02", text, "틱 수 × 틱 간격");
            StringAssert.Contains("마지막 저장: 2026-11-12 09:30:00", text, "들어올 때 방의 시계를 썼다(감싼 저장소가 본다)");
            StringAssert.Contains("주기 저장 꺼짐", text);
        }

        [Test]
        public void Players_ListsWhoIsIn_AndEmptyRoomSaysSo()
        {
            using var rig = new Rig();
            Assert.That(string.Join("\n", rig.Command("players")), Does.Contain("들어와 있는 사람: 0명 / 정원 3"));
            rig.Join("solo");
            rig.Run(1.0);
            var lines = rig.Command("PLAYERS"); // 대소문자 무관
            Assert.That(lines[0], Does.Contain("1명 / 정원 3"));
            Assert.That(lines.Count(l => l.Contains("solo")), Is.EqualTo(1));
        }

        [Test]
        public void PlayerLabel_UsesTheIdentity_WhenNoNameIsKnown()
        {
            using var rig = new Rig();
            rig.Join("only-id");
            rig.Run(1.0);
            var player = rig.Room.Players.Single();
            Assert.That(ServerCommands.PlayerLabel(player), Does.Contain("only-id"));
        }

        [Test]
        public void Save_WritesTheRoomNow_AndLogsIt()
        {
            using var rig = new Rig();
            rig.Run(0.5);
            rig.Now = new DateTime(2026, 11, 12, 10, 0, 0);
            var lines = rig.Command("save");
            Assert.That(lines.Single(), Does.StartWith("저장함(save 명령): 슬롯 ops-room"));
            Assert.That(rig.Store.LastWrite, Is.EqualTo(rig.Now));
            Assert.That(rig.Store.TryRead("ops-room", SaveFileNames.RoomClock, out RoomClockSaveData _), Is.True);
        }

        [Test]
        public void Help_ListsEveryRegisteredCommand_AndUnknownPointsToHelp()
        {
            using var rig = new Rig();
            string help = string.Join("\n", rig.Command("help"));
            foreach (var name in new[] { "status", "players", "save", "help", "stop" }) StringAssert.Contains("  " + name, help);

            var unknown = rig.Command("teleport everyone");
            Assert.That(unknown.Single(), Is.EqualTo("모르는 명령: teleport — help 로 명령 목록을 본다"));
            Assert.That(rig.Command("   "), Is.Empty, "빈 줄은 무시");
        }

        [Test]
        public void Stop_AsksTheLoopToEnd_AndNoMoreTicksRun()
        {
            using var rig = new Rig();
            rig.Run(0.2);
            long ticks = rig.Runner.Ticks;
            rig.Command("stop");
            Assert.That(rig.Runner.StopRequested, Is.True);
            rig.Clock.Now += 1.0;
            Assert.That(rig.Runner.RunDue(), Is.EqualTo(0));
            Assert.That(rig.Runner.Ticks, Is.EqualTo(ticks));
        }

        [Test]
        public void NewCommand_IsOneRegistrationLine()
        {
            var commands = ServerCommands.CreateDefault().Add("ping", "살아 있는지", (s, rest) => s.Log("pong " + rest));
            using var rig = new Rig(0, commands);
            Assert.That(rig.Command("ping hello").Single(), Is.EqualTo("pong hello"));
            StringAssert.Contains("  ping", string.Join("\n", rig.Command("help")));
            Assert.Throws<ArgumentException>(() => commands.Add("PING", "겹침", (s, _) => { }));
        }

        [Test]
        public void Autosave_RunsEveryNTicks_CountedByTheLoopNotTheWallClock()
        {
            using var rig = new Rig(autosaveSeconds: 1.0);
            rig.Run(0.9);
            Assert.That(rig.Log.Count(l => l.StartsWith("저장함(주기 저장)")), Is.EqualTo(0));
            rig.Run(0.2);
            Assert.That(rig.Log.Count(l => l.StartsWith("저장함(주기 저장)")), Is.EqualTo(1), "30틱(1초)마다");
            rig.Run(3.0);
            Assert.That(rig.Log.Count(l => l.StartsWith("저장함(주기 저장)")), Is.EqualTo(4));

            // 시계가 서 있으면(틱이 없으면) 저장도 없다.
            int before = rig.Log.Count;
            for (int i = 0; i < 100; i++) rig.Runner.RunDue();
            Assert.That(rig.Log.Count, Is.EqualTo(before));
        }

        [Test]
        public void Autosave_Zero_NeverSaves_AndSaveCommandResetsTheCount()
        {
            using (var off = new Rig(autosaveSeconds: 0))
            {
                off.Run(5.0);
                Assert.That(off.Log.Any(l => l.Contains("주기 저장")), Is.False);
            }

            using var rig = new Rig(autosaveSeconds: 1.0);
            rig.Run(0.8);
            rig.Command("save");
            rig.Run(0.8);
            Assert.That(rig.Log.Count(l => l.StartsWith("저장함(주기 저장)")), Is.EqualTo(0), "방금 저장했으면 다음 주기 저장은 그때부터 센다");
            rig.Run(0.3);
            Assert.That(rig.Log.Count(l => l.StartsWith("저장함(주기 저장)")), Is.EqualTo(1));
        }

        [Test]
        public void Duration_IsReadable()
        {
            Assert.That(ServerCommands.Duration(0), Is.EqualTo("00:00:00"));
            Assert.That(ServerCommands.Duration(3725.9), Is.EqualTo("01:02:05"));
            Assert.That(ServerCommands.Duration(90061), Is.EqualTo("1일 01:01:01"));
        }
    }
}
