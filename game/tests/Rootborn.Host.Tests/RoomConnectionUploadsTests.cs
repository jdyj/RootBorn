using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Save;
using Rootborn.Data.Save;
using Rootborn.Host.Net;
using Rootborn.Net.Room;
using Rootborn.Net.Session;
using Rootborn.Net.Transport;
using Rootborn.Screens.Start;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 방에 닿지 못한 저장(GuestUploads)은 그 방의 것이다(창 없이 RoomConnection만, 같은 프로세스 안 전송 위의 방 둘).
    /// 방 A에서 저장이 닿기 전에 끊기고 같은 프로세스·같은 슬롯으로 방 B에 들어가면, B가 내려 준 세이브에 A의 파일이 겹쳐지지 않고 B에 보내지지도 않는다.
    /// A로 돌아가면 그 저장을 겹쳐 시작하고 A에 다시 보낸다(끊긴 동안의 진행을 잃지 않는다).
    /// 방의 손님 저장 검사는 아무것도 하지 않는다(받은 파일을 그대로 쓰는지만 본다 — 게임 규칙은 Server SharedHouseBuildRoomTests).
    /// 자동 테스트(전송과 접속 객체만 — 화면·입력 근거 아님).
    /// </summary>
    public sealed class RoomConnectionUploadsTests
    {
        private const double Tick = 0.05;
        private string _root;
        private double _now;

        [SetUp]
        public void SetUp() => _root = Path.Combine(Path.GetTempPath(), "rootborn-room-uploads-" + Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        private sealed class Clock : IRoomClock { public double Now { get; set; } }

        private sealed class Room : IDisposable
        {
            public LoopbackNetwork Network;
            public RoomServer Server;
            public RoomLoop Loop;
            public Clock Clock;
            public FileSaveStore Guests;

            public void Dispose() => Server.Dispose();
        }

        private static readonly RoomVersions Versions = new RoomVersions(RoomProtocol.Version, "game-1", "data-1");

        private Room NewRoom(string name)
        {
            string saves = Path.Combine(_root, name);
            string guests = GuestSaveFolder.FolderFor(saves, "room");
            var room = new Room { Network = new LoopbackNetwork(1), Clock = new Clock { Now = _now } };
            room.Server = new RoomServer(room.Network.Listen(), new RoomServerSettings { MaxPlayers = 4 }, Versions, new FileSaveStore(saves), "room", null,
                guests: new GuestSaveFolder(guests, (store, slot, player) => { }));
            room.Loop = new RoomLoop(room.Server, room.Clock, Tick);
            room.Guests = new FileSaveStore(guests);
            return room;
        }

        private void Run(Room room, RoomConnection connection, double seconds)
        {
            double end = _now + seconds;
            while (_now < end - 1e-9)
            {
                _now += Tick;
                room.Clock.Now = _now;
                room.Loop.RunDue();
                if (connection.Phase == RoomJoinPhase.Connecting) connection.UpdateJoining(_now);
                else connection.UpdateInRoom(_now, "map", 0, 0, Rootborn.Core.World.Facing.Down, false, string.Empty);
            }
        }

        private LoopbackTransport Enter(Room room, RoomConnection connection, string where)
        {
            var line = room.Network.Connect();
            connection.BeginWith(line, "slot-0", where);
            Run(room, connection, 1.0);
            Assert.That(connection.InRoom, Is.True, where + ": 들어갔다");
            Assert.That(connection.Link.TakesGuestSaves, Is.True, "전제: 손님 저장을 받는 방");
            return line;
        }

        private static SaveFileSet Files(params (string Name, string Text)[] files)
            => SaveFileSet.From(files.Select(f => new KeyValuePair<string, string>(f.Name, f.Text)), null);

        private static string Text(SaveFileSet set, string name) => set.Writes.Where(p => p.Key == name).Select(p => p.Value).FirstOrDefault();

        [Test]
        public void AfterTheLineBreaks_ADifferentRoomGetsNoneOfTheFirstRoomsUnsentSaves_AndTheFirstRoomGetsThemBack()
        {
            string paid = SaveFileNames.RoomHouseBuildPaid("slot-0");
            var connection = new RoomConnection(() => Versions, new SessionClientSettings(), slot => slot, entry => true);
            using var a = NewRoom("a");
            using var b = NewRoom("b");

            var lineA = Enter(a, connection, "room-a");
            Assert.That(connection.Link.Upload(Files(("progress.json", "a-1"))), Is.True);
            Run(a, connection, 0.5);
            Assert.That(a.Guests.ReadSlotFiles("slot-0").Any(p => p.Key == "progress.json"), Is.True, "전제: 닿은 저장은 방 A가 받았다");

            a.Network.Sever(lineA); // 이 뒤의 저장은 A에 닿지 않는다
            Assert.That(connection.Link.Upload(Files(("progress.json", "a-2"), (paid, "a-paid"))), Is.True);
            Run(a, connection, 0.5);
            connection.Leave();
            Assert.That(a.Guests.ReadSlotFiles("slot-0").ToDictionary(p => p.Key, p => p.Value)["progress.json"], Is.EqualTo("a-1"), "전제: 끊긴 뒤의 저장은 A에 닿지 않았다");

            Enter(b, connection, "room-b");
            Assert.That(connection.Link.Files.Writes.Select(p => p.Key), Has.No.Member(paid), "B가 내려 준 세이브에 A의 돈 낸 표시가 겹쳐지지 않는다");
            Assert.That(connection.Link.Files.Writes.Select(p => p.Key), Has.No.Member("progress.json"), "A의 진행도 겹쳐지지 않는다");
            Assert.That(connection.Link.Uploads.Count, Is.Zero, "B에 보낼 것은 없다");
            Run(b, connection, 0.5);
            Assert.That(b.Guests.ReadSlotFiles("slot-0").Select(p => p.Key), Is.Empty, "B의 손님 세이브에 A의 파일이 가지 않는다");
            Assert.That(connection.Link.Upload(Files(("progress.json", "b-1"))), Is.True);
            Run(b, connection, 0.5);
            connection.Leave();

            Enter(a, connection, "room-a");
            Assert.That(Text(connection.Link.Files, "progress.json"), Is.EqualTo("a-2"), "A로 돌아오면 끊긴 동안의 저장을 겹쳐 시작한다(B의 저장이 아니라)");
            Assert.That(Text(connection.Link.Files, paid), Is.EqualTo("a-paid"), "돈 낸 표시도 A로 돌아온다");
            Run(a, connection, 0.5);
            var inA = a.Guests.ReadSlotFiles("slot-0").ToDictionary(p => p.Key, p => p.Value);
            Assert.That(inA["progress.json"], Is.EqualTo("a-2"), "A에 다시 보내 A가 받았다");
            Assert.That(inA[paid], Is.EqualTo("a-paid"));
            Assert.That(connection.Link.Uploads.Count, Is.Zero);
            Assert.That(b.Guests.ReadSlotFiles("slot-0").ToDictionary(p => p.Key, p => p.Value)["progress.json"], Is.EqualTo("b-1"), "B의 것은 B에 그대로");
        }
    }
}
