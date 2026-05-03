using System;
using System.Threading.Tasks;
using Rootborn.Game.Bootstrap;

namespace Rootborn.Network.Session
{
    public interface INetworkSession
    {
        SessionMode Mode { get; }
        bool IsServer { get; }
        bool IsClient { get; }

        event Action<ulong> OnPlayerConnected;
        event Action<ulong> OnPlayerDisconnected;

        Task StartAsync(AppConfig config);
        Task StopAsync();
    }
}
