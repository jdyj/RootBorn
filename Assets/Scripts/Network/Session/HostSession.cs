using System;
using System.Threading.Tasks;
using Rootborn.Game.Bootstrap;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Rootborn.Network.Session
{
    public sealed class HostSession : INetworkSession
    {
        public SessionMode Mode => SessionMode.Host;
        public bool IsServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
        public bool IsClient => NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient;

        public event Action<ulong> OnPlayerConnected;
        public event Action<ulong> OnPlayerDisconnected;

        public Task StartAsync(AppConfig config)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null)
            {
                Debug.LogError("[ROOTBORN] NetworkManager.Singleton missing in scene.");
                return Task.CompletedTask;
            }

            var transport = nm.GetComponent<UnityTransport>();
            if (transport != null)
            {
                transport.SetConnectionData("0.0.0.0", config.Port);
            }

            nm.OnClientConnectedCallback += HandleConnected;
            nm.OnClientDisconnectCallback += HandleDisconnected;

            if (!nm.StartHost())
            {
                Debug.LogError($"[ROOTBORN] StartHost failed (port={config.Port}).");
            }
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null)
            {
                nm.OnClientConnectedCallback -= HandleConnected;
                nm.OnClientDisconnectCallback -= HandleDisconnected;
                nm.Shutdown();
            }
            return Task.CompletedTask;
        }

        private void HandleConnected(ulong id) => OnPlayerConnected?.Invoke(id);
        private void HandleDisconnected(ulong id) => OnPlayerDisconnected?.Invoke(id);
    }
}
