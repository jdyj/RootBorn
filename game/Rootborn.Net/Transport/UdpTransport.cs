using System;
using System.Collections.Generic;
using System.Net;
using LiteNetLib;

namespace Rootborn.Net.Transport
{
    /// <summary>
    /// UDP 전송(LiteNetLib 1.3.5). 데디케이티드 서버, Steam 없는 개발, 두 프로세스 검증용이다.
    /// 주소·포트·연결 열쇠는 부르는 쪽이 준다(코드에 적지 않는다). 포트 0이면 운영체제가 빈 포트를 준다(<see cref="LocalPort"/>).
    /// 같은 기계 검증은 루프백 주소로만 묶어 방화벽 창이 뜨지 않게 한다.
    /// LiteNetLib의 일은 Update 안의 PollEvents에서만 올라온다(다른 스레드에서 부르지 않는다).
    /// 끊김 시간 초과는 LiteNetLib의 DisconnectTimeout을 쓴다. 세션 쪽 시간 초과는 따로 있다.
    /// </summary>
    public sealed class UdpTransport : ITransport
    {
        private readonly NetManager _manager;
        private readonly EventBasedNetListener _listener = new EventBasedNetListener();
        private readonly Dictionary<PeerId, NetPeer> _peers = new Dictionary<PeerId, NetPeer>(8);
        private readonly Queue<TransportEvent> _events = new Queue<TransportEvent>(64);
        private readonly string _connectionKey;
        private readonly bool _isServer;

        private UdpTransport(string connectionKey, bool isServer, int disconnectTimeoutMs)
        {
            if (string.IsNullOrEmpty(connectionKey)) throw new ArgumentException("연결 열쇠가 비어 있다", nameof(connectionKey));
            _connectionKey = connectionKey;
            _isServer = isServer;
            _manager = new NetManager(_listener)
            {
                AutoRecycle = true,
                IPv6Enabled = false,
                DisconnectTimeout = disconnectTimeoutMs,
            };
            _listener.ConnectionRequestEvent += request =>
            {
                if (_isServer) request.AcceptIfKey(_connectionKey);
                else request.Reject();
            };
            _listener.PeerConnectedEvent += peer =>
            {
                var id = IdOf(peer);
                _peers[id] = peer;
                _events.Enqueue(new TransportEvent(TransportEventKind.Connected, id, null));
            };
            _listener.PeerDisconnectedEvent += (peer, info) =>
            {
                var id = IdOf(peer);
                _peers.Remove(id);
                if (info.Reason == DisconnectReason.DisconnectPeerCalled) return; // 이쪽이 끊은 것(인터페이스 약속: 이쪽에는 올리지 않는다)
                byte[] reason = info.AdditionalData != null && info.AdditionalData.AvailableBytes > 0 ? info.AdditionalData.GetRemainingBytes() : null;
                _events.Enqueue(new TransportEvent(TransportEventKind.Disconnected, id, reason));
            };
            _listener.NetworkReceiveEvent += (peer, reader, channel, method) =>
            {
                _events.Enqueue(new TransportEvent(TransportEventKind.Data, IdOf(peer), reader.GetRemainingBytes()));
            };
        }

        /// <summary>서버: bindAddress:port에서 듣는다.</summary>
        public static UdpTransport Listen(IPAddress bindAddress, int port, string connectionKey, int disconnectTimeoutMs = 5000)
        {
            if (bindAddress == null) throw new ArgumentNullException(nameof(bindAddress));
            var transport = new UdpTransport(connectionKey, isServer: true, disconnectTimeoutMs);
            if (!transport._manager.Start(bindAddress, IPAddress.IPv6Loopback, port))
            {
                transport.Dispose();
                throw new InvalidOperationException("UDP 소켓을 열 수 없다: " + bindAddress + ":" + port);
            }

            return transport;
        }

        /// <summary>클라이언트: 자기 쪽은 localBind의 빈 포트, 상대는 host:port.</summary>
        public static UdpTransport Connect(IPAddress localBind, string host, int port, string connectionKey, int disconnectTimeoutMs = 5000)
        {
            if (localBind == null) throw new ArgumentNullException(nameof(localBind));
            var transport = new UdpTransport(connectionKey, isServer: false, disconnectTimeoutMs);
            if (!transport._manager.Start(localBind, IPAddress.IPv6Loopback, 0))
            {
                transport.Dispose();
                throw new InvalidOperationException("UDP 소켓을 열 수 없다: " + localBind);
            }

            transport._manager.Connect(host, port, connectionKey);
            return transport;
        }

        public int LocalPort => _manager.LocalPort;

        public void Update(double now)
        {
            if (_manager.IsRunning) _manager.PollEvents();
        }

        public bool TryReceive(out TransportEvent transportEvent)
        {
            if (_events.Count > 0)
            {
                transportEvent = _events.Dequeue();
                return true;
            }

            transportEvent = default;
            return false;
        }

        public void Send(PeerId peer, byte[] data, Delivery delivery)
        {
            if (data == null || !_peers.TryGetValue(peer, out var netPeer)) return;
            netPeer.Send(data, delivery == Delivery.ReliableOrdered ? DeliveryMethod.ReliableOrdered : DeliveryMethod.Unreliable);
        }

        public void Disconnect(PeerId peer, byte[] reason)
        {
            if (!_peers.TryGetValue(peer, out var netPeer)) return;
            _peers.Remove(peer);
            if (reason != null && reason.Length > 0) _manager.DisconnectPeer(netPeer, reason);
            else _manager.DisconnectPeer(netPeer);
        }

        public void Dispose()
        {
            if (_manager.IsRunning) _manager.Stop(true);
            _peers.Clear();
        }

        private static PeerId IdOf(NetPeer peer) => new PeerId(peer.Id);
    }
}
