using System;
using System.Threading.Tasks;
using Rootborn.Game.Bootstrap;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Rootborn.Network.Session
{
    public sealed class DedicatedServerSession : INetworkSession
    {
        public SessionMode Mode => SessionMode.Server;
        public bool IsServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
        public bool IsClient => false;

        public event Action<ulong> OnPlayerConnected;
        public event Action<ulong> OnPlayerDisconnected;

        public string SaveSlot { get; private set; } = "default";
        public int MaxPlayers { get; private set; } = 4;

        public Task StartAsync(AppConfig config)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null)
            {
                Debug.LogError("[ROOTBORN] NetworkManager.Singleton missing in scene.");
                return Task.CompletedTask;
            }

            SaveSlot = config.SaveSlot;
            MaxPlayers = config.MaxPlayers;

            var transport = nm.GetComponent<UnityTransport>();
            if (transport != null)
            {
                transport.SetConnectionData("0.0.0.0", config.Port);
            }

            nm.OnClientConnectedCallback += HandleConnected;
            nm.OnClientDisconnectCallback += HandleDisconnected;
            nm.ConnectionApprovalCallback = HandleApproval;

            Application.targetFrameRate = 30;

            if (!nm.StartServer())
            {
                Debug.LogError($"[ROOTBORN] StartServer failed (port={config.Port}).");
            }
            else
            {
                Debug.Log($"[ROOTBORN] Dedicated server started — port={config.Port} maxPlayers={MaxPlayers} saveSlot={SaveSlot}");
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
                nm.ConnectionApprovalCallback = null;
                nm.Shutdown();
            }
            return Task.CompletedTask;
        }

        private void HandleConnected(ulong id) => OnPlayerConnected?.Invoke(id);
        private void HandleDisconnected(ulong id) => OnPlayerDisconnected?.Invoke(id);

        private void HandleApproval(NetworkManager.ConnectionApprovalRequest req, NetworkManager.ConnectionApprovalResponse res)
        {
            var nm = NetworkManager.Singleton;
            int currentPlayers = nm == null ? 0 : nm.ConnectedClientsIds.Count;
            res.Approved = currentPlayers < MaxPlayers;
            res.CreatePlayerObject = res.Approved;
            res.Reason = res.Approved ? string.Empty : "server full";
        }
    }
}
