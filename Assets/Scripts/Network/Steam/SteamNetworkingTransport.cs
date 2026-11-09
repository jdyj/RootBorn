using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Rootborn.Core.Network;
using Steamworks;
using Unity.Netcode;
using UnityEngine;

namespace Rootborn.Network.Steam
{
    /// <summary>
    /// Netcode transport over Steam Networking Sockets (P2P through Steam's relay): players never see an IP or port,
    /// and router setup is not needed. The host listens; a client connects to the host's Steam ID.
    /// Netcode client ids come from <see cref="PeerTable"/>; the server is 0.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SteamNetworkingTransport : NetworkTransport
    {
        private const int VirtualPort = 0;

        private readonly struct PendingEvent
        {
            public PendingEvent(NetworkEvent type, ulong clientId)
            {
                Type = type;
                ClientId = clientId;
            }

            public NetworkEvent Type { get; }
            public ulong ClientId { get; }
        }

        private readonly PeerTable _peers = new PeerTable();
        private readonly Queue<PendingEvent> _events = new Queue<PendingEvent>(8);
        private readonly IntPtr[] _message = new IntPtr[1];
        private byte[] _buffer = new byte[2048];
        private Callback<SteamNetConnectionStatusChangedCallback_t> _statusChanged;
        private HSteamListenSocket _listenSocket = HSteamListenSocket.Invalid;
        private HSteamNetPollGroup _pollGroup = HSteamNetPollGroup.Invalid;
        private HSteamNetConnection _serverConnection = HSteamNetConnection.Invalid;
        private bool _isServer;

        /// <summary>The host to join, set before StartClient (the lobby owner's Steam ID).</summary>
        public ulong HostSteamId { get; set; }

        public override ulong ServerClientId => 0;
        public override bool IsSupported => SteamPlatform.IsReady;

        public override void Initialize(NetworkManager networkManager = null)
        {
            if (_statusChanged == null && SteamPlatform.IsReady)
                _statusChanged = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnStatusChanged);
        }

        public override bool StartServer()
        {
            if (!SteamPlatform.IsReady) return false;
            Initialize();
            _isServer = true;
            _listenSocket = SteamNetworkingSockets.CreateListenSocketP2P(VirtualPort, 0, null);
            _pollGroup = SteamNetworkingSockets.CreatePollGroup();
            return _listenSocket != HSteamListenSocket.Invalid && _pollGroup != HSteamNetPollGroup.Invalid;
        }

        public override bool StartClient()
        {
            if (!SteamPlatform.IsReady || HostSteamId == 0) return false;
            Initialize();
            _isServer = false;
            var host = new SteamNetworkingIdentity();
            host.SetSteamID(new CSteamID(HostSteamId));
            _serverConnection = SteamNetworkingSockets.ConnectP2P(ref host, VirtualPort, 0, null);
            return _serverConnection != HSteamNetConnection.Invalid;
        }

        private void OnStatusChanged(SteamNetConnectionStatusChangedCallback_t change)
        {
            var connection = change.m_hConn;
            var state = change.m_info.m_eState;
            bool closed = state == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer
                || state == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally;

            if (_isServer)
            {
                if (state == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting && change.m_info.m_hListenSocket == _listenSocket)
                {
                    if (SteamNetworkingSockets.AcceptConnection(connection) != EResult.k_EResultOK)
                        SteamNetworkingSockets.CloseConnection(connection, 0, "accept failed", false);
                    else
                        SteamNetworkingSockets.SetConnectionPollGroup(connection, _pollGroup);
                }
                else if (state == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected)
                {
                    _events.Enqueue(new PendingEvent(NetworkEvent.Connect, _peers.Add(connection.m_HSteamNetConnection)));
                }
                else if (closed)
                {
                    if (_peers.Remove(connection.m_HSteamNetConnection, out ulong clientId))
                        _events.Enqueue(new PendingEvent(NetworkEvent.Disconnect, clientId));
                    SteamNetworkingSockets.CloseConnection(connection, 0, null, false);
                }

                return;
            }

            if (connection != _serverConnection) return;
            if (state == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected)
            {
                _events.Enqueue(new PendingEvent(NetworkEvent.Connect, ServerClientId));
            }
            else if (closed)
            {
                _events.Enqueue(new PendingEvent(NetworkEvent.Disconnect, ServerClientId));
                SteamNetworkingSockets.CloseConnection(connection, 0, null, false);
                _serverConnection = HSteamNetConnection.Invalid;
            }
        }

        public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)
        {
            receiveTime = Time.realtimeSinceStartup;
            payload = default;
            if (_events.Count > 0)
            {
                var pending = _events.Dequeue();
                clientId = pending.ClientId;
                return pending.Type;
            }

            clientId = 0;
            while (true)
            {
                int received = _isServer
                    ? (_pollGroup != HSteamNetPollGroup.Invalid ? SteamNetworkingSockets.ReceiveMessagesOnPollGroup(_pollGroup, _message, 1) : 0)
                    : (_serverConnection != HSteamNetConnection.Invalid ? SteamNetworkingSockets.ReceiveMessagesOnConnection(_serverConnection, _message, 1) : 0);
                if (received <= 0) return NetworkEvent.Nothing;

                var message = SteamNetworkingMessage_t.FromIntPtr(_message[0]);
                try
                {
                    ulong sender = ServerClientId;
                    // A message from a connection that already left is dropped; Netcode no longer knows that client.
                    if (_isServer && !_peers.TryGetClientId(message.m_conn.m_HSteamNetConnection, out sender)) continue;
                    if (_buffer.Length < message.m_cbSize) _buffer = new byte[Math.Max(message.m_cbSize, _buffer.Length * 2)];
                    Marshal.Copy(message.m_pData, _buffer, 0, message.m_cbSize);
                    clientId = sender;
                    payload = new ArraySegment<byte>(_buffer, 0, message.m_cbSize);
                    return NetworkEvent.Data;
                }
                finally
                {
                    SteamNetworkingMessage_t.Release(_message[0]);
                }
            }
        }

        public override void Send(ulong clientId, ArraySegment<byte> payload, NetworkDelivery networkDelivery)
        {
            HSteamNetConnection connection;
            if (_isServer)
            {
                if (!_peers.TryGetHandle(clientId, out uint handle)) return;
                connection = new HSteamNetConnection(handle);
            }
            else
            {
                connection = _serverConnection;
            }

            if (connection == HSteamNetConnection.Invalid || payload.Array == null) return;
            int flags = networkDelivery == NetworkDelivery.Unreliable || networkDelivery == NetworkDelivery.UnreliableSequenced
                ? Constants.k_nSteamNetworkingSend_UnreliableNoNagle
                : Constants.k_nSteamNetworkingSend_ReliableNoNagle;
            var pinned = GCHandle.Alloc(payload.Array, GCHandleType.Pinned);
            try
            {
                IntPtr data = IntPtr.Add(pinned.AddrOfPinnedObject(), payload.Offset);
                SteamNetworkingSockets.SendMessageToConnection(connection, data, (uint)payload.Count, flags, out _);
            }
            finally
            {
                pinned.Free();
            }
        }

        public override void DisconnectRemoteClient(ulong clientId)
        {
            if (!_peers.TryGetHandle(clientId, out uint handle)) return;
            _peers.Remove(handle, out _);
            SteamNetworkingSockets.CloseConnection(new HSteamNetConnection(handle), 0, "disconnected by host", true);
        }

        public override void DisconnectLocalClient()
        {
            if (_serverConnection == HSteamNetConnection.Invalid) return;
            SteamNetworkingSockets.CloseConnection(_serverConnection, 0, "left", true);
            _serverConnection = HSteamNetConnection.Invalid;
        }

        public override ulong GetCurrentRtt(ulong clientId)
        {
            HSteamNetConnection connection = _serverConnection;
            if (_isServer)
            {
                if (!_peers.TryGetHandle(clientId, out uint handle)) return 0;
                connection = new HSteamNetConnection(handle);
            }

            if (connection == HSteamNetConnection.Invalid) return 0;
            var status = new SteamNetConnectionRealTimeStatus_t();
            var lanes = new SteamNetConnectionRealTimeLaneStatus_t();
            return SteamNetworkingSockets.GetConnectionRealTimeStatus(connection, ref status, 0, ref lanes) == EResult.k_EResultOK && status.m_nPing > 0
                ? (ulong)status.m_nPing
                : 0;
        }

        public override void Shutdown()
        {
            if (SteamPlatform.IsReady)
            {
                foreach (uint handle in _peers.Handles)
                    SteamNetworkingSockets.CloseConnection(new HSteamNetConnection(handle), 0, "host closed", true);
                if (_serverConnection != HSteamNetConnection.Invalid) SteamNetworkingSockets.CloseConnection(_serverConnection, 0, "left", true);
                if (_listenSocket != HSteamListenSocket.Invalid) SteamNetworkingSockets.CloseListenSocket(_listenSocket);
                if (_pollGroup != HSteamNetPollGroup.Invalid) SteamNetworkingSockets.DestroyPollGroup(_pollGroup);
            }

            _peers.Clear();
            _events.Clear();
            _serverConnection = HSteamNetConnection.Invalid;
            _listenSocket = HSteamListenSocket.Invalid;
            _pollGroup = HSteamNetPollGroup.Invalid;
            _isServer = false;
        }

        private void OnDestroy()
        {
            _statusChanged?.Dispose();
            _statusChanged = null;
        }
    }
}
