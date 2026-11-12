using System;
using System.Collections.Generic;
using Rootborn.Core.Save;
using Rootborn.Core.Time;
using Rootborn.Net.Session;
using Rootborn.Net.Transport;

namespace Rootborn.Net.Tests
{
    /// <summary>같은 프로세스 전송 위에 서버 하나와 클라이언트 여럿을 두고 같은 시계로 돌린다.</summary>
    internal sealed class SessionRig : IDisposable
    {
        public const double Step = 1.0 / 60.0;

        private readonly List<SessionClient> _clients = new List<SessionClient>();
        private readonly Dictionary<SessionClient, LoopbackTransport> _transports = new Dictionary<SessionClient, LoopbackTransport>();

        public SessionRig(int maxPlayers = 4, int seed = 1, ISaveStore store = null, string saveSlot = "slot-0", SessionServerSettings serverSettings = null,
            TimeRules timeRules = null)
        {
            Network = new LoopbackNetwork(seed);
            ServerSettings = serverSettings ?? new SessionServerSettings { MaxPlayers = maxPlayers };
            Server = new SessionServer(Network.Listen(), ServerSettings, store, saveSlot, timeRules);
        }

        public LoopbackNetwork Network { get; }
        public SessionServerSettings ServerSettings { get; }
        public SessionServer Server { get; }
        public double Now { get; private set; }

        public SessionClient Join(string playerId, string look = "child|", SessionClientSettings settings = null, string name = null)
        {
            var transport = Network.Connect();
            var client = new SessionClient(transport, playerId, look, settings ?? new SessionClientSettings(), name);
            _clients.Add(client);
            _transports[client] = transport;
            return client;
        }

        public LoopbackTransport TransportOf(SessionClient client) => _transports[client];

        /// <summary>그 클라이언트를 닫는다(창을 끈 것과 같다).</summary>
        public void Quit(SessionClient client)
        {
            client.Dispose();
            _clients.Remove(client);
        }

        /// <summary>고장 난 클라이언트처럼 아무것도 하지 않게 한다(돌리지 않음).</summary>
        public void Freeze(SessionClient client) => _clients.Remove(client);

        /// <summary>얼린 클라이언트를 다시 돌린다(그동안 온 것은 전송에 쌓여 있다).</summary>
        public void Thaw(SessionClient client)
        {
            if (!_clients.Contains(client)) _clients.Add(client);
        }

        public void Run(double seconds, Action<double> eachStep = null)
        {
            double end = Now + seconds;
            while (Now < end - 1e-9)
            {
                Now += Step;
                eachStep?.Invoke(Now);
                Server.Update(Now);
                foreach (var client in _clients.ToArray()) client.Update(Now);
            }
        }

        public void Dispose()
        {
            foreach (var client in _clients) client.Dispose();
            Server.Dispose();
        }
    }
}
