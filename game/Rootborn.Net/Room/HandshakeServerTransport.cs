using System;
using System.Collections.Generic;
using Rootborn.Net.Messages;
using Rootborn.Net.Session;
using Rootborn.Net.Transport;

namespace Rootborn.Net.Room
{
    /// <summary>
    /// 방 쪽 접속 확인 관문. 실제 전송을 감싸 그 위의 <see cref="SessionServer"/>에게는 버전이 맞는 연결만 보인다.
    /// - 새 연결은 <see cref="JoinCheckMessage"/>를 보낼 때까지 기다린다(위로 올리지 않는다).
    /// - 버전이 맞으면 <see cref="JoinCheckAcceptedMessage"/>를 보내고 그때 위로 연결 일을 올린다. 이 뒤의 바이트는 그대로 지나간다.
    /// - 버전이 다르거나, 확인 메시지가 아니거나(관문 없는 옛 클라이언트), 시간 안에 오지 않으면 이유 코드(<see cref="RoomRefusal"/>)를 실어 끊는다.
    ///   이유는 세션의 작별 메시지로 실으므로 클라이언트의 <see cref="SessionClient.FarewellReason"/>에 그대로 뜬다.
    /// 시간은 <see cref="Update"/>에 넘긴 값만 쓴다.
    /// </summary>
    public sealed class HandshakeServerTransport : ITransport
    {
        private readonly ITransport _inner;
        private readonly RoomVersions _versions;
        private readonly double _timeoutSeconds;
        private readonly MessageCatalog _handshake;
        private readonly MessageCatalog _session;
        private readonly Dictionary<PeerId, double> _pending = new Dictionary<PeerId, double>(8);
        private readonly HashSet<PeerId> _accepted = new HashSet<PeerId>();
        private readonly Queue<TransportEvent> _up = new Queue<TransportEvent>(32);
        private readonly List<PeerId> _scratch = new List<PeerId>(8);
        private readonly byte[] _acceptedBytes;

        public HandshakeServerTransport(ITransport inner, RoomVersions versions, double timeoutSeconds, int maxMessageBytes)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            if (timeoutSeconds <= 0) throw new ArgumentException("접속 확인 시간 초과는 양수여야 한다", nameof(timeoutSeconds));
            _versions = versions;
            _timeoutSeconds = timeoutSeconds;
            _handshake = RoomHandshakeMessages.CreateCatalog(maxMessageBytes);
            _session = RootbornMessages.CreateCatalog(maxMessageBytes);
            _handshake.TryEncode(new JoinCheckAcceptedMessage(), out _acceptedBytes);
        }

        /// <summary>버전이 맞아 받아들인 연결.</summary>
        public event Action<PeerId, RoomVersions> Accepted;

        /// <summary>받지 않은 연결과 이유 코드, 상대가 밝힌 버전(밝히지 않았으면 null).</summary>
        public event Action<PeerId, string, RoomVersions?> Refused;

        public void Update(double now)
        {
            _inner.Update(now);
            while (_inner.TryReceive(out var e))
            {
                switch (e.Kind)
                {
                    case TransportEventKind.Connected:
                        _pending[e.Peer] = now;
                        break;
                    case TransportEventKind.Disconnected:
                        if (_pending.Remove(e.Peer)) break; // 위에서는 모르는 연결
                        if (_accepted.Remove(e.Peer)) _up.Enqueue(e);
                        break;
                    case TransportEventKind.Data:
                        if (_accepted.Contains(e.Peer)) _up.Enqueue(e);
                        else if (_pending.ContainsKey(e.Peer)) Check(e.Peer, e.Data);
                        break;
                }
            }

            _scratch.Clear();
            foreach (var pair in _pending)
                if (now - pair.Value > _timeoutSeconds) _scratch.Add(pair.Key);
            foreach (var peer in _scratch) Refuse(peer, RoomRefusal.HandshakeTimeout, null);
        }

        public bool TryReceive(out TransportEvent transportEvent)
        {
            if (_up.Count > 0)
            {
                transportEvent = _up.Dequeue();
                return true;
            }

            transportEvent = default;
            return false;
        }

        public void Send(PeerId peer, byte[] data, Delivery delivery)
        {
            if (_accepted.Contains(peer)) _inner.Send(peer, data, delivery);
        }

        public void Disconnect(PeerId peer, byte[] reason)
        {
            _accepted.Remove(peer);
            _pending.Remove(peer);
            _inner.Disconnect(peer, reason);
        }

        public void Dispose()
        {
            foreach (var peer in new List<PeerId>(_pending.Keys)) Refuse(peer, RoomRefusal.Shutdown, null);
            _accepted.Clear();
            _inner.Dispose();
        }

        private void Check(PeerId peer, byte[] data)
        {
            if (_handshake.Decode(data, out var message) != DecodeOutcome.Ok || !(message is JoinCheckMessage check))
            {
                Refuse(peer, RoomRefusal.ProtocolMismatch, null);
                return;
            }

            var theirs = new RoomVersions(check.Protocol, check.Game, check.Data);
            string reason = RoomRefusal.Check(_versions, theirs);
            if (reason != null)
            {
                Refuse(peer, reason, theirs);
                return;
            }

            _pending.Remove(peer);
            _accepted.Add(peer);
            _inner.Send(peer, _acceptedBytes, Delivery.ReliableOrdered);
            _up.Enqueue(new TransportEvent(TransportEventKind.Connected, peer, null));
            Accepted?.Invoke(peer, theirs);
        }

        private void Refuse(PeerId peer, string reason, RoomVersions? theirs)
        {
            _pending.Remove(peer);
            _session.TryEncode(new FarewellMessage { Reason = reason }, out var bytes);
            _inner.Disconnect(peer, bytes);
            Refused?.Invoke(peer, reason, theirs);
        }
    }
}
