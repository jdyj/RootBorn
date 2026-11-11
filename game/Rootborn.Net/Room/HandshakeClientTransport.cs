using System;
using System.Collections.Generic;
using Rootborn.Net.Messages;
using Rootborn.Net.Session;
using Rootborn.Net.Transport;

namespace Rootborn.Net.Room
{
    /// <summary>
    /// 클라이언트 쪽 접속 확인 관문(<see cref="HandshakeServerTransport"/>의 짝). 연결되면 먼저 자기 버전을 보내고,
    /// 방이 받아들였다고 답할 때까지 위(<see cref="SessionClient"/>)가 보내는 것을 순서대로 들고 있다가 그 뒤에 보낸다.
    /// 방이 받지 않으면 이유 코드가 실린 끊김이 위로 올라가 <see cref="SessionClient.FarewellReason"/>에 뜬다.
    /// </summary>
    public sealed class HandshakeClientTransport : ITransport
    {
        private readonly ITransport _inner;
        private readonly MessageCatalog _handshake;
        private readonly byte[] _checkBytes;
        private readonly Queue<(PeerId Peer, byte[] Data, Delivery Delivery)> _held = new Queue<(PeerId, byte[], Delivery)>(8);
        private readonly Queue<TransportEvent> _up = new Queue<TransportEvent>(32);

        public HandshakeClientTransport(ITransport inner, RoomVersions versions, int maxMessageBytes)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _handshake = RoomHandshakeMessages.CreateCatalog(maxMessageBytes);
            if (!_handshake.TryEncode(new JoinCheckMessage { Protocol = versions.Protocol, Game = versions.Game, Data = versions.Data }, out _checkBytes))
                throw new ArgumentException("버전 글자가 너무 길다", nameof(versions));
        }

        /// <summary>방이 버전을 받아들였다.</summary>
        public bool Accepted { get; private set; }

        public void Update(double now)
        {
            _inner.Update(now);
            while (_inner.TryReceive(out var e))
            {
                switch (e.Kind)
                {
                    case TransportEventKind.Connected:
                        Accepted = false;
                        _held.Clear();
                        _inner.Send(e.Peer, _checkBytes, Delivery.ReliableOrdered);
                        _up.Enqueue(e);
                        break;
                    case TransportEventKind.Disconnected:
                        Accepted = false;
                        _held.Clear();
                        _up.Enqueue(e);
                        break;
                    case TransportEventKind.Data:
                        if (Accepted) _up.Enqueue(e);
                        else if (_handshake.Decode(e.Data, out var message) == DecodeOutcome.Ok && message is JoinCheckAcceptedMessage) Accept();
                        break;
                }
            }
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
            if (data == null) return;
            if (Accepted) _inner.Send(peer, data, delivery);
            else _held.Enqueue((peer, (byte[])data.Clone(), delivery));
        }

        public void Disconnect(PeerId peer, byte[] reason)
        {
            _held.Clear();
            Accepted = false;
            _inner.Disconnect(peer, reason);
        }

        public void Dispose() => _inner.Dispose();

        private void Accept()
        {
            Accepted = true;
            while (_held.Count > 0)
            {
                var item = _held.Dequeue();
                _inner.Send(item.Peer, item.Data, item.Delivery);
            }
        }
    }

    /// <summary>방에 들어가는 클라이언트를 만든다: 접속 확인 관문 위에 세션 클라이언트.</summary>
    public static class RoomClient
    {
        public static SessionClient Create(ITransport transport, RoomVersions versions, string playerId, string look, SessionClientSettings settings)
        {
            settings ??= new SessionClientSettings();
            return new SessionClient(new HandshakeClientTransport(transport, versions, settings.MaxMessageBytes), playerId, look, settings);
        }
    }
}
