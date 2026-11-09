using System;
using System.Threading.Tasks;
using Rootborn.Game.Bootstrap;
using Rootborn.Network.Session;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Rootborn.Network.Steam
{
    /// <summary>
    /// Player hosting and joining through Steam: the session uses the Steam transport and a friends-only lobby.
    /// Without Steam (development, tests, dedicated server) sessions keep Unity Transport and the command-line address.
    /// </summary>
    public static class SteamSessions
    {
        public static bool Available => SteamPlatform.IsReady && NetworkManager.Singleton != null;

        /// <summary>Starts the host session and opens the friends lobby. Returns false when the host did not start.</summary>
        public static async Task<bool> HostAsync(AppConfig config)
        {
            var nm = NetworkManager.Singleton;
            if (!Available || config == null) return false;
            UseSteamTransport(nm);
            config.Mode = SessionMode.Host;
            await NetworkBootstrap.StartSessionAsync(config);
            if (!nm.IsServer) return false;
            nm.OnServerStopped -= LeaveLobbyWhenStopped;
            nm.OnServerStopped += LeaveLobbyWhenStopped;
            SteamLobby.CreateFriendsLobby(config.MaxPlayers, null);
            return true;
        }

        /// <summary>
        /// Enters a friend's lobby and starts the client toward its host. <paramref name="failed"/> runs when the lobby
        /// could not be entered or the client could not start; connection results arrive through Netcode callbacks.
        /// </summary>
        public static void Join(ulong lobbyId, AppConfig config, Action failed)
        {
            var nm = NetworkManager.Singleton;
            if (!Available || config == null)
            {
                failed?.Invoke();
                return;
            }

            SteamLobby.Join(lobbyId, host =>
            {
                if (host == 0 || nm == null)
                {
                    failed?.Invoke();
                    return;
                }

                UseSteamTransport(nm).HostSteamId = host;
                config.Mode = SessionMode.Client;
                _ = StartClientAsync(nm, config, failed);
            });
        }

        private static async Task StartClientAsync(NetworkManager nm, AppConfig config, Action failed)
        {
            await NetworkBootstrap.StartSessionAsync(config);
            if (nm == null || !nm.IsClient)
            {
                SteamLobby.Leave();
                failed?.Invoke();
                return;
            }

            nm.OnClientStopped -= LeaveLobbyWhenClientStopped;
            nm.OnClientStopped += LeaveLobbyWhenClientStopped;
        }

        public static SteamNetworkingTransport UseSteamTransport(NetworkManager nm)
        {
            var steam = nm.GetComponent<SteamNetworkingTransport>();
            if (steam == null) steam = nm.gameObject.AddComponent<SteamNetworkingTransport>();
            nm.NetworkConfig.NetworkTransport = steam;
            return steam;
        }

        /// <summary>Back to Unity Transport (address from the command line), for sessions started without Steam.</summary>
        public static void UseDefaultTransport(NetworkManager nm)
        {
            var unity = nm != null ? nm.GetComponent<UnityTransport>() : null;
            if (unity != null) nm.NetworkConfig.NetworkTransport = unity;
        }

        private static void LeaveLobbyWhenStopped(bool wasHost)
        {
            if (NetworkManager.Singleton != null) NetworkManager.Singleton.OnServerStopped -= LeaveLobbyWhenStopped;
            SteamLobby.Leave();
        }

        private static void LeaveLobbyWhenClientStopped(bool wasHost)
        {
            if (NetworkManager.Singleton != null) NetworkManager.Singleton.OnClientStopped -= LeaveLobbyWhenClientStopped;
            if (!wasHost) SteamLobby.Leave();
        }
    }
}
