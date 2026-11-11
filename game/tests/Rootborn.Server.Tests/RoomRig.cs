using System;
using System.Collections.Generic;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;
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

        public RoomRig(ISaveStore store, string slot = "room-a", int maxPlayers = 4, RoomServerSettings settings = null, LifeCalendar calendar = null)
        {
            Network = new LoopbackNetwork(1);
            Clock = new ManualRoomClock();
            Settings = settings ?? new RoomServerSettings { MaxPlayers = maxPlayers };
            Log = new List<string>();
            Room = new RoomServer(Network.Listen(), Settings, Versions, store, slot, Log.Add, calendar);
            Loop = new RoomLoop(Room, Clock, Settings.TickSeconds);
        }

        public LoopbackNetwork Network { get; }
        public ManualRoomClock Clock { get; }
        public RoomServerSettings Settings { get; }
        public RoomServer Room { get; }
        public RoomLoop Loop { get; }
        public List<string> Log { get; }

        public SessionClient Join(string playerId, RoomVersions? versions = null, string look = "child|")
        {
            var client = RoomClient.Create(Network.Connect(), versions ?? Versions, playerId, look, new SessionClientSettings());
            _clients.Add(client);
            return client;
        }

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

        public void Run(double seconds)
        {
            double end = Clock.Now + seconds;
            while (Clock.Now < end - 1e-9)
            {
                Clock.Now += Settings.TickSeconds;
                Loop.RunDue();
                foreach (var client in _clients.ToArray()) client.Update(Clock.Now);
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
