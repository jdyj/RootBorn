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
            SessionApproval.EnableForClient(nm);

            if (!nm.StartClient())
            {
                Debug.LogError($"[ROOTBORN] StartClient failed (joinIp={config.JoinIp} port={config.Port}).");
                return Task.CompletedTask;
            }

            if (nm.SceneManager != null)
            {
                nm.SceneManager.OnSceneEvent += HandleSceneEvent;
            }

            Debug.Log($"[ROOTBORN] Client started - joinIp={config.JoinIp} port={config.Port} saveSlot={config.SaveSlot}");
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null)
            {
                if (nm.SceneManager != null)
                {
                    nm.SceneManager.OnSceneEvent -= HandleSceneEvent;
                }

                nm.OnClientConnectedCallback -= HandleConnected;
                nm.OnClientDisconnectCallback -= HandleDisconnected;
                nm.Shutdown();
            }
            return Task.CompletedTask;
        }

        private void HandleConnected(ulong id)
        {
            Debug.Log($"[ROOTBORN] Client connected callback id={id} localClientId={NetworkManager.Singleton?.LocalClientId}");
            OnPlayerConnected?.Invoke(id);
        }

        private void HandleDisconnected(ulong id)
        {
            // The server's reason (e.g. "server full" from SessionCapacity) is the only clue a refused player gets.
            Debug.Log($"[ROOTBORN] Client disconnected callback id={id} localClientId={NetworkManager.Singleton?.LocalClientId} reason={NetworkManager.Singleton?.DisconnectReason}");
            OnPlayerDisconnected?.Invoke(id);
        }

        private void HandleSceneEvent(SceneEvent sceneEvent)
        {
            Debug.Log($"[ROOTBORN] Client scene event type={sceneEvent.SceneEventType} scene={sceneEvent.SceneName} client={sceneEvent.ClientId}");
        }
    }
}
