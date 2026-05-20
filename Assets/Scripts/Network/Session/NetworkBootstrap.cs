using System.Threading.Tasks;
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

        public static async Task StartSessionAsync(AppConfig config)
        {
            if (config == null || config.Mode == SessionMode.None) return;
            if (ActiveSession != null && ActiveSession.Mode == config.Mode && ActiveSession.IsServer == (config.Mode == SessionMode.Host || config.Mode == SessionMode.Server) && ActiveSession.IsClient == (config.Mode == SessionMode.Host || config.Mode == SessionMode.Client)) return;

            ActiveSession = NetworkSessionFactory.Create(config.Mode);
            await ActiveSession.StartAsync(config);
        }

        private async void HandleBootstrapped(AppConfig config)
        {
            await StartSessionAsync(config);
        }
    }
}
