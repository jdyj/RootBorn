using System;
using System.Threading.Tasks;
using Rootborn.Game.Bootstrap;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Rootborn.Network.Session
{
    public sealed class ClientSession : INetworkSession
    {
        public SessionMode Mode => SessionMode.Client;
        public bool IsServer => false;
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
                transport.SetConnectionData(config.JoinIp, config.Port);
            }

            nm.OnClientConnectedCallback += HandleConnected;
            nm.OnClientDisconnectCallback += HandleDisconnected;

            if (!nm.StartClient())
            {
                Debug.LogError($"[ROOTBORN] StartClient failed (joinIp={config.JoinIp} port={config.Port}).");
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
