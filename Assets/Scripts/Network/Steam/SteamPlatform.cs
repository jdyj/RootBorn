using System;
using Rootborn.Core.Network;
using Steamworks;
using UnityEngine;

namespace Rootborn.Network.Steam
{
    /// <summary>
    /// Starts the Steam API for players (not for a dedicated server, batch runs or -nosteam). When Steam is not running
    /// the game keeps working and Steam features report why they are unavailable.
    /// </summary>
    public static class SteamPlatform
    {
        // Valve's public test app (Spacewar) until the store app id exists. A build or run can override it: -steamAppId <id>.
        public const uint DevelopmentAppId = 480;

        private static Callback<GameLobbyJoinRequested_t> s_joinRequested;

        public static bool IsReady { get; private set; }
        public static uint AppId { get; private set; } = DevelopmentAppId;
        public static string FailureReason { get; private set; } = string.Empty;

        /// <summary>A lobby the player asked to join from Steam (friends list, invite, or "+connect_lobby" at launch).</summary>
        public static ulong PendingLobbyId { get; private set; }

        public static event Action<ulong> LobbyJoinRequested;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_joinRequested = null;
            LobbyJoinRequested = null;
            PendingLobbyId = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartForPlayers()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (ShouldSkip(args, Application.isBatchMode)) return;
            Initialize(args);
        }

        public static bool ShouldSkip(string[] args, bool batchMode)
        {
            if (batchMode) return true;
            if (args == null) return false;
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "-nosteam", StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(args[i], "-mode", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length
                    && string.Equals(args[i + 1], "server", StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        public static bool Initialize(string[] args)
        {
            if (IsReady) return true;
            AppId = ReadAppId(args);
            // Steam reads the app id from steam_appid.txt or these variables; the variables keep builds free of a loose file.
            Environment.SetEnvironmentVariable("SteamAppId", AppId.ToString());
            Environment.SetEnvironmentVariable("SteamGameId", AppId.ToString());
            try
            {
                var result = SteamAPI.InitEx(out string message);
                if (result != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
                {
                    FailureReason = result + " " + message;
                    Debug.Log($"[ROOTBORN] Steam unavailable: {FailureReason}");
                    return false;
                }
            }
            catch (Exception exception) when (exception is DllNotFoundException || exception is EntryPointNotFoundException || exception is BadImageFormatException)
            {
                FailureReason = exception.GetType().Name;
                Debug.Log($"[ROOTBORN] Steam unavailable: {FailureReason}");
                return false;
            }

            IsReady = true;
            FailureReason = string.Empty;
            s_joinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
            var runner = new GameObject("[Steam]", typeof(SteamCallbackRunner));
            UnityEngine.Object.DontDestroyOnLoad(runner);
            if (SteamLaunchArgs.TryGetLobbyId(args, out ulong launchLobby)) RequestJoin(launchLobby);
            Debug.Log($"[ROOTBORN] Steam ready - appId={AppId} user={SteamUser.GetSteamID().m_SteamID}");
            return true;
        }

        /// <summary>The menu takes the request once it can act on it.</summary>
        public static bool TryTakePendingLobby(out ulong lobbyId)
        {
            lobbyId = PendingLobbyId;
            PendingLobbyId = 0;
            return lobbyId != 0;
        }

        internal static void Shutdown()
        {
            if (!IsReady) return;
            IsReady = false;
            s_joinRequested?.Dispose();
            s_joinRequested = null;
            SteamAPI.Shutdown();
        }

        private static void OnJoinRequested(GameLobbyJoinRequested_t request) => RequestJoin(request.m_steamIDLobby.m_SteamID);

        private static void RequestJoin(ulong lobbyId)
        {
            if (lobbyId == 0) return;
            PendingLobbyId = lobbyId;
            LobbyJoinRequested?.Invoke(lobbyId);
        }

        private static uint ReadAppId(string[] args)
        {
            if (args != null)
                for (int i = 0; i < args.Length - 1; i++)
                    if (string.Equals(args[i], "-steamAppId", StringComparison.OrdinalIgnoreCase) && uint.TryParse(args[i + 1], out uint id) && id != 0)
                        return id;
            return DevelopmentAppId;
        }
    }
}
