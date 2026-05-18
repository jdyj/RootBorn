using System;
using System.Threading.Tasks;
using Rootborn.Game.Bootstrap;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.Network.Session
{
    public sealed class HostSession : INetworkSession
    {
        private const string TownSceneName = "Town";

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
                return Task.CompletedTask;
            }

            if (nm.SceneManager != null)
            {
                nm.SceneManager.OnSceneEvent += HandleSceneEvent;
            }

            Debug.Log($"[ROOTBORN] Host started - port={config.Port} saveSlot={config.SaveSlot}");
            if (nm.NetworkConfig != null && nm.NetworkConfig.EnableSceneManagement && nm.SceneManager != null)
            {
                var status = nm.SceneManager.LoadScene(TownSceneName, LoadSceneMode.Single);
                Debug.Log($"[ROOTBORN] Host requested network scene load scene={TownSceneName} status={status}");
            }
            else
            {
                Debug.Log("[ROOTBORN] Host skipped network scene load because Netcode scene management is disabled.");
            }
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
            Debug.Log($"[ROOTBORN] Host client connected id={id}");
            OnPlayerConnected?.Invoke(id);
        }

        private void HandleDisconnected(ulong id)
        {
            Debug.Log($"[ROOTBORN] Host client disconnected id={id}");
            OnPlayerDisconnected?.Invoke(id);
        }

        private void HandleSceneEvent(SceneEvent sceneEvent)
        {
            Debug.Log($"[ROOTBORN] Host scene event type={sceneEvent.SceneEventType} scene={sceneEvent.SceneName} client={sceneEvent.ClientId}");
        }
    }
}
