using Rootborn.Game.Bootstrap;
using UnityEngine;

namespace Rootborn.Network.Session
{
    public sealed class NetworkBootstrap : MonoBehaviour
    {
        public static INetworkSession ActiveSession { get; private set; }

        private void Awake()
        {
            GameBootstrap.OnBootstrapped += HandleBootstrapped;
        }

        private void OnDestroy()
        {
            GameBootstrap.OnBootstrapped -= HandleBootstrapped;
        }

        private async void HandleBootstrapped(AppConfig config)
        {
            if (config.Mode == SessionMode.None) return;
            ActiveSession = NetworkSessionFactory.Create(config.Mode);
            await ActiveSession.StartAsync(config);
        }
    }
}
