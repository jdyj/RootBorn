using System;
using System.Collections.Generic;
using Rootborn.Core.Network;
using Steamworks;
using UnityEngine;

namespace Rootborn.Network.Steam
{
    /// <summary>
    /// Friends-only Steam lobbies: the host opens one, friends see it on the Steam friends list ("Join Game") and in the
    /// game's friend room list, and joining a lobby tells the client which Steam ID to connect to.
    /// </summary>
    public static class SteamLobby
    {
        private const string HostKey = "rootborn_host";
        private const string ConnectPresenceKey = "connect";

        private static CallResult<LobbyCreated_t> s_created;
        private static CallResult<LobbyEnter_t> s_entered;

        public static ulong CurrentLobbyId { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_created = null;
            s_entered = null;
            CurrentLobbyId = 0;
        }

        public static void CreateFriendsLobby(int maxPlayers, Action<bool> done)
        {
            if (!SteamPlatform.IsReady)
            {
                done?.Invoke(false);
                return;
            }

            if (s_created == null) s_created = CallResult<LobbyCreated_t>.Create();
            var call = SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, Mathf.Max(1, maxPlayers));
            s_created.Set(call, (result, ioFailure) =>
            {
                bool ok = !ioFailure && result.m_eResult == EResult.k_EResultOK && result.m_ulSteamIDLobby != 0;
                if (ok)
                {
                    CurrentLobbyId = result.m_ulSteamIDLobby;
                    var lobby = new CSteamID(CurrentLobbyId);
                    SteamMatchmaking.SetLobbyData(lobby, HostKey, SteamUser.GetSteamID().m_SteamID.ToString());
                    // Lets "Join Game" on the friends list start or reach this game with the lobby to enter.
                    SteamFriends.SetRichPresence(ConnectPresenceKey, SteamLaunchArgs.ConnectString(CurrentLobbyId));
                }

                Debug.Log($"[ROOTBORN] Steam lobby created ok={ok} lobby={result.m_ulSteamIDLobby} result={result.m_eResult} ioFailure={ioFailure}");
                done?.Invoke(ok);
            });
        }

        /// <summary>Enters the lobby and reports the host's Steam ID to connect to (0 when entering failed).</summary>
        public static void Join(ulong lobbyId, Action<ulong> done)
        {
            if (!SteamPlatform.IsReady || lobbyId == 0)
            {
                done?.Invoke(0);
                return;
            }

            if (s_entered == null) s_entered = CallResult<LobbyEnter_t>.Create();
            var call = SteamMatchmaking.JoinLobby(new CSteamID(lobbyId));
            s_entered.Set(call, (result, ioFailure) =>
            {
                bool ok = !ioFailure && result.m_EChatRoomEnterResponse == (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess;
                ulong host = 0;
                if (ok)
                {
                    CurrentLobbyId = result.m_ulSteamIDLobby;
                    var lobby = new CSteamID(CurrentLobbyId);
                    if (!ulong.TryParse(SteamMatchmaking.GetLobbyData(lobby, HostKey), out host) || host == 0)
                        host = SteamMatchmaking.GetLobbyOwner(lobby).m_SteamID;
                }

                Debug.Log($"[ROOTBORN] Steam lobby entered ok={ok} lobby={lobbyId} host={host} response={result.m_EChatRoomEnterResponse} ioFailure={ioFailure}");
                done?.Invoke(host);
            });
        }

        public static void Leave()
        {
            if (!SteamPlatform.IsReady) return;
            if (CurrentLobbyId != 0) SteamMatchmaking.LeaveLobby(new CSteamID(CurrentLobbyId));
            SteamFriends.SetRichPresence(ConnectPresenceKey, string.Empty);
            CurrentLobbyId = 0;
        }

        /// <summary>Friends currently in a lobby of this game, one row per lobby.</summary>
        public static IReadOnlyList<FriendRoom> FriendRoomsNow()
        {
            if (!SteamPlatform.IsReady) return Array.Empty<FriendRoom>();
            int count = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
            var candidates = new List<FriendRoomCandidate>(count);
            for (int i = 0; i < count; i++)
            {
                var friend = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
                if (!SteamFriends.GetFriendGamePlayed(friend, out FriendGameInfo_t game)) continue;
                uint appId = game.m_gameID.IsValid() ? game.m_gameID.AppID().m_AppId : 0;
                candidates.Add(new FriendRoomCandidate(friend.m_SteamID, SteamFriends.GetFriendPersonaName(friend), appId, game.m_steamIDLobby.m_SteamID));
            }

            return FriendRooms.Joinable(candidates, SteamPlatform.AppId);
        }
    }
}
