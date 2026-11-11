using System;
using System.Collections.Generic;

namespace Rootborn.Core.Network
{
    /// <summary>
    /// Netcode client ids for transport connections. The server is 0; each new connection gets the next id, and ids are
    /// never reused, so a connection handle the transport hands out again is a new player, not the one who left.
    /// </summary>
    public sealed class PeerTable
    {
        private readonly Dictionary<uint, ulong> _byHandle = new Dictionary<uint, ulong>(8);
        private readonly Dictionary<ulong, uint> _byClient = new Dictionary<ulong, uint>(8);
        private ulong _nextClientId = 1;

        public int Count => _byHandle.Count;
        public IEnumerable<uint> Handles => _byHandle.Keys;

        public ulong Add(uint handle)
        {
            if (_byHandle.TryGetValue(handle, out ulong existing)) return existing;
            ulong id = _nextClientId++;
            _byHandle.Add(handle, id);
            _byClient.Add(id, handle);
            return id;
        }

        public bool TryGetHandle(ulong clientId, out uint handle) => _byClient.TryGetValue(clientId, out handle);

        public bool TryGetClientId(uint handle, out ulong clientId) => _byHandle.TryGetValue(handle, out clientId);

        public bool Remove(uint handle, out ulong clientId)
        {
            if (!_byHandle.TryGetValue(handle, out clientId)) return false;
            _byHandle.Remove(handle);
            _byClient.Remove(clientId);
            return true;
        }

        public void Clear()
        {
            _byHandle.Clear();
            _byClient.Clear();
        }
    }

    /// <summary>Steam starts the game with "+connect_lobby &lt;id&gt;" when a friend clicks Join Game while it is closed.</summary>
    public static class SteamLaunchArgs
    {
        public const string ConnectLobbyArgument = "+connect_lobby";

        public static string ConnectString(ulong lobbyId) => ConnectLobbyArgument + " " + lobbyId;

        public static bool TryGetLobbyId(IReadOnlyList<string> args, out ulong lobbyId)
        {
            lobbyId = 0;
            if (args == null) return false;
            for (int i = 0; i < args.Count - 1; i++)
            {
                if (!string.Equals(args[i], ConnectLobbyArgument, StringComparison.OrdinalIgnoreCase)) continue;
                return ulong.TryParse(args[i + 1], out lobbyId) && lobbyId != 0;
            }

            return false;
        }
    }

    /// <summary>What the Steam friends list says about one friend.</summary>
    public readonly struct FriendRoomCandidate
    {
        public FriendRoomCandidate(ulong friendId, string name, uint playingAppId, ulong lobbyId)
        {
            FriendId = friendId;
            Name = name ?? string.Empty;
            PlayingAppId = playingAppId;
            LobbyId = lobbyId;
        }

        public ulong FriendId { get; }
        public string Name { get; }
        public uint PlayingAppId { get; }
        public ulong LobbyId { get; }
    }

    public readonly struct FriendRoom
    {
        public FriendRoom(ulong friendId, string name, ulong lobbyId)
        {
            FriendId = friendId;
            Name = name;
            LobbyId = lobbyId;
        }

        public ulong FriendId { get; }
        public string Name { get; }
        public ulong LobbyId { get; }
    }

    public static class FriendRooms
    {
        /// <summary>Friends playing this game inside a lobby, one row per lobby, sorted by friend name.</summary>
        public static IReadOnlyList<FriendRoom> Joinable(IEnumerable<FriendRoomCandidate> candidates, uint appId)
        {
            var rooms = new List<FriendRoom>();
            if (candidates == null) return rooms;
            var sorted = new List<FriendRoomCandidate>();
            foreach (var candidate in candidates)
                if (candidate.PlayingAppId == appId && candidate.LobbyId != 0) sorted.Add(candidate);
            sorted.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            var seen = new HashSet<ulong>();
            foreach (var candidate in sorted)
                if (seen.Add(candidate.LobbyId)) rooms.Add(new FriendRoom(candidate.FriendId, candidate.Name, candidate.LobbyId));
            return rooms;
        }
    }
}
