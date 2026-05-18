using System.Collections;
using NUnit.Framework;
using Rootborn.Game.Bootstrap;
using Rootborn.Game.Player;
using Rootborn.Network.Player;
using Rootborn.Network.Session;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode.Network
{
    public sealed class NetworkSessionHostPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            ShutdownNetworkManagers();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            ShutdownNetworkManagers();
            yield return null;
        }

        [UnityTest]
        public IEnumerator MULTIPLAYER_PM_001_HostSessionStartsOwnedLocalPlayerAndStopsWithoutStaleNetworkObjects()
        {
            var networkRoot = new GameObject("[NetworkManager]", typeof(NetworkManager), typeof(UnityTransport));
            var manager = networkRoot.GetComponent<NetworkManager>();
            var transport = networkRoot.GetComponent<UnityTransport>();
            manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                EnableSceneManagement = false,
                ConnectionApproval = false,
            };

            var session = new HostSession();
            int connectedCount = 0;
            session.OnPlayerConnected += _ => connectedCount++;

            session.StartAsync(new AppConfig { Mode = SessionMode.Host, Port = 7791, SaveSlot = "playmode-host-slot" }).GetAwaiter().GetResult();
            yield return WaitForHost(manager, 3f);

            Assert.IsTrue(manager.IsServer, "Host session must start server authority.");
            Assert.IsTrue(manager.IsClient, "Host session must also start a local client.");
            Assert.IsTrue(manager.IsListening, "Host NetworkManager must be listening after StartHost.");
            Assert.AreEqual(1, manager.ConnectedClientsIds.Count, "Host-mode PlayMode gate should have exactly the local client connected.");
            Assert.GreaterOrEqual(connectedCount, 1, "HostSession must surface the local client connection callback.");

            var spawned = SpawnOwnedPlayer(manager.LocalClientId);
            yield return WaitForIdentity(spawned.GetComponent<PlayerIdentity>(), "client-0", 2f);

            var networkObject = spawned.GetComponent<NetworkObject>();
            Assert.IsTrue(networkObject.IsSpawned, "Spawned player must be a Netcode-spawned NetworkObject.");
            Assert.IsTrue(networkObject.IsOwner, "Host local player must own its spawned player object.");
            Assert.AreEqual(manager.LocalClientId, networkObject.OwnerClientId, "Spawned player owner id must match host local client id.");

            var identity = spawned.GetComponent<PlayerIdentity>();
            Assert.AreEqual(0UL, identity.ClientId, "Host local player should bind client id 0.");

            session.StopAsync().GetAwaiter().GetResult();
            yield return null;
            Assert.IsFalse(manager.IsListening, "Stopping HostSession must shut down NetworkManager listening state.");
            Assert.AreEqual(0, CountSpawnedNetworkObjects(), "Stopping host must not leave stale spawned NetworkObjects in the scene.");
        }

        private static NetworkPlayerIdentityBinder SpawnOwnedPlayer(ulong ownerClientId)
        {
            var go = new GameObject("HostOwnedNetworkPlayer", typeof(NetworkObject), typeof(PlayerIdentity), typeof(NetworkPlayerIdentityBinder));
            var networkObject = go.GetComponent<NetworkObject>();
            networkObject.SpawnAsPlayerObject(ownerClientId, true);
            var binder = go.GetComponent<NetworkPlayerIdentityBinder>();
            Assert.IsNotNull(binder);
            return binder;
        }

        private static IEnumerator WaitForIdentity(PlayerIdentity identity, string expectedPlayerId, float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                if (identity != null && identity.PlayerId == expectedPlayerId)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.AreEqual(expectedPlayerId, identity != null ? identity.PlayerId : null, "NetworkPlayerIdentityBinder must derive stable player id from owner client id.");
        }

        private static IEnumerator WaitForHost(NetworkManager manager, float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                if (manager != null && manager.IsListening && manager.IsServer && manager.IsClient && manager.ConnectedClientsIds.Count > 0)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("HostSession did not enter listening host state within timeout.");
        }

        private static int CountSpawnedNetworkObjects()
        {
            int count = 0;
            var networkObjects = Object.FindObjectsByType<NetworkObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < networkObjects.Length; i++)
            {
                if (networkObjects[i] != null && networkObjects[i].IsSpawned)
                {
                    count++;
                }
            }

            return count;
        }

        private static void ShutdownNetworkManagers()
        {
            var managers = Object.FindObjectsByType<NetworkManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = managers.Length - 1; i >= 0; i--)
            {
                var manager = managers[i];
                if (manager == null)
                {
                    continue;
                }

                if (manager.IsListening)
                {
                    manager.Shutdown();
                }

                Object.DestroyImmediate(manager.gameObject);
            }

            var networkObjects = Object.FindObjectsByType<NetworkObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = networkObjects.Length - 1; i >= 0; i--)
            {
                if (networkObjects[i] != null)
                {
                    Object.DestroyImmediate(networkObjects[i].gameObject);
                }
            }
        }
    }
}
