using System;

namespace Rootborn.Net.Transport
{
    /// <summary>전송이 연결 하나에 붙인 번호. 같은 전송 안에서만 뜻이 있고, 끊긴 뒤 다시 쓰일 수 있다.</summary>
    public readonly struct PeerId : IEquatable<PeerId>
    {
        public PeerId(long value) => Value = value;

        public long Value { get; }

        public bool Equals(PeerId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is PeerId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => "peer-" + Value;
    }

    /// <summary>보내는 방식. 신뢰 순서는 잃지 않고 보낸 순서대로 닿는다. 비신뢰는 잃거나 순서가 바뀔 수 있다.</summary>
    public enum Delivery
    {
        ReliableOrdered,
        Unreliable,
    }

    public enum TransportEventKind
    {
        Connected,
        Disconnected,
        Data,
    }

    /// <summary>
    /// 전송에서 올라온 일 하나. Data는 받은 바이트, Disconnected는 끊은 쪽이 붙인 바이트(없으면 빈 배열).
    /// 시간 초과나 연결 실패처럼 붙인 바이트가 없는 끊김은 빈 배열이다.
    /// </summary>
    public readonly struct TransportEvent
    {
        public TransportEvent(TransportEventKind kind, PeerId peer, byte[] data)
        {
            Kind = kind;
            Peer = peer;
            Data = data ?? Array.Empty<byte>();
        }

        public TransportEventKind Kind { get; }
        public PeerId Peer { get; }
        public byte[] Data { get; }
    }

    /// <summary>
    /// 연결·끊김·보내기·받기만 하는 전송. 서버 쪽은 여러 연결을, 클라이언트 쪽은 서버 연결 하나를 가진다.
    /// 시간은 밖에서 넘긴다(<see cref="Update"/>). 일은 Update 뒤 <see cref="TryReceive"/>로 꺼낸다.
    /// 바이트의 뜻(메시지 종류)은 모른다.
    /// </summary>
    public interface ITransport : IDisposable
    {
        /// <summary>지금 시각(초)을 넘겨 보낼 것을 보내고 받은 것을 모은다.</summary>
        void Update(double now);

        bool TryReceive(out TransportEvent transportEvent);

        /// <summary>모르는 연결이나 끊긴 연결에 보내면 아무 일도 없다.</summary>
        void Send(PeerId peer, byte[] data, Delivery delivery);

        /// <summary>연결을 끊는다. reason 바이트는 상대의 Disconnected 일에 실린다(닿는다는 보장은 없다). 이쪽에는 끊김 일이 올라오지 않는다.</summary>
        void Disconnect(PeerId peer, byte[] reason);
    }
}
