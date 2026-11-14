using System;
using System.Collections.Generic;
using Rootborn.Core.Save;
using Rootborn.Core.Time;
using Rootborn.Net.Room;
using Rootborn.Net.Session;
using Rootborn.Net.Transport;

namespace Rootborn.Server.Tests
{
    /// <summary>같은 프로세스 전송 위에 방 하나와 클라이언트 여럿을 두고 손으로 돌리는 시계로 돈다.</summary>
    internal sealed class RoomRig : IDisposable
    {
        public static readonly RoomVersions Versions = new RoomVersions(RoomProtocol.Version, "game-1", "data-1");

        private readonly List<SessionClient> _clients = new List<SessionClient>();
        private readonly Dictionary<SessionClient, LoopbackTransport> _lines = new Dictionary<SessionClient, LoopbackTransport>();

        public RoomRig(ISaveStore store, string slot = "room-a", int maxPlayers = 4, RoomServerSettings settings = null, TimeRules timeRules = null,
            IGuestSaves guests = null, Rootborn.Core.Room.RoomPieceCatalog pieces = null, string owner = null, RoomVersions? versions = null)
        {
            ServerVersions = versions ?? Versions;
            Network = new LoopbackNetwork(1);
            Clock = new ManualRoomClock();
            Settings = settings ?? new RoomServerSettings { MaxPlayers = maxPlayers };
            Log = new List<string>();
            Room = new RoomServer(Network.Listen(), Settings, ServerVersions, store, slot, Log.Add, timeRules, guests, pieces, owner);
            Loop = new RoomLoop(Room, Clock, Settings.TickSeconds);
        }

        /// <summary>이 방이 받아들이는 버전(기본은 <see cref="Versions"/>).</summary>
        public RoomVersions ServerVersions { get; }
        public LoopbackNetwork Network { get; }
        public ManualRoomClock Clock { get; }
        public RoomServerSettings Settings { get; }
        public RoomServer Room { get; }
        public RoomLoop Loop { get; }
        public List<string> Log { get; }

        public SessionClient Join(string playerId, RoomVersions? versions = null, string look = "child|")
        {
            var line = Network.Connect();
            var client = RoomClient.Create(line, versions ?? ServerVersions, playerId, look, new SessionClientSettings());
            _clients.Add(client);
            _lines[client] = line;
            return client;
        }

        /// <summary>그 클라이언트의 선을 끊는다: 이후 바이트를 양쪽 모두 알림 없이 잃는다(보낸 직후 끊김).</summary>
        public void Sever(SessionClient client) => Network.Sever(_lines[client]);

        /// <summary>관문 없이 들어오는 옛 클라이언트(프로토콜이 다르다).</summary>
        public SessionClient JoinWithoutHandshake(string playerId)
        {
            var client = new SessionClient(Network.Connect(), playerId, "child|", new SessionClientSettings());
            _clients.Add(client);
            return client;
        }

        public void Quit(SessionClient client)
        {
            client.Dispose();
            _clients.Remove(client);
        }

        /// <param name="afterTick">틱마다 클라이언트를 돌린 뒤 부른다(인자: 그 틱의 초). 게임 한 판의 AdvanceTime을 Host처럼 부를 때.</param>
        public void Run(double seconds, Action<float> afterTick = null)
        {
            double end = Clock.Now + seconds;
            while (Clock.Now < end - 1e-9)
            {
                Clock.Now += Settings.TickSeconds;
                Loop.RunDue();
                foreach (var client in _clients.ToArray()) client.Update(Clock.Now);
                afterTick?.Invoke((float)Settings.TickSeconds);
            }
        }

        public void Dispose()
        {
            foreach (var client in _clients) client.Dispose();
            Room.Dispose();
        }
    }

    internal sealed class ManualRoomClock : IRoomClock
    {
        public double Now { get; set; }
    }
}
