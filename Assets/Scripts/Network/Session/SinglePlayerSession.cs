using System;
using System.Threading.Tasks;
using Rootborn.Game.Bootstrap;

namespace Rootborn.Network.Session
{
    public sealed class SinglePlayerSession : INetworkSession
    {
        public SessionMode Mode => SessionMode.Single;
        public bool IsServer => true;
        public bool IsClient => true;

        public event Action<ulong> OnPlayerConnected;
        public event Action<ulong> OnPlayerDisconnected;

        public Task StartAsync(AppConfig config)
        {
            OnPlayerConnected?.Invoke(0UL);
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            OnPlayerDisconnected?.Invoke(0UL);
            return Task.CompletedTask;
        }
    }
}
