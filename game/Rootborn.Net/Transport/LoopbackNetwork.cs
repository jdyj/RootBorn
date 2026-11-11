using System;
using System.Collections.Generic;

namespace Rootborn.Net.Transport
{
    /// <summary>
    /// 같은 프로세스 안 전송(테스트용). 서버 하나와 클라이언트 여럿을 한 테스트에서 돌린다.
    /// 지연(LatencySeconds)과 흔들림(JitterSeconds)은 모든 바이트에, 손실(LossRate)은 비신뢰에만 넣는다.
    /// 흔들림 때문에 비신뢰는 순서가 바뀔 수 있고, 신뢰 순서는 연결마다 보낸 순서를 지킨다(앞의 것보다 먼저 닿지 않는다).
    /// 시간은 각 끝의 Update(now)로만 흐른다. 같은 씨앗이면 같은 손실·흔들림이 나온다.
    /// </summary>
    public sealed class LoopbackNetwork
    {
        private readonly Random _random;
        private readonly List<Packet> _inFlight = new List<Packet>(256);
        private LoopbackTransport _server;
        private long _nextPeer = 1;
        private long _nextOrder;
        private double _now;

        public LoopbackNetwork(int seed = 1) => _random = new Random(seed);

        public double LatencySeconds { get; set; }
        public double JitterSeconds { get; set; }

        /// <summary>비신뢰 바이트를 잃을 확률(0~1).</summary>
        public double LossRate { get; set; }

        /// <summary>서버 쪽 끝. 하나만 만들 수 있다.</summary>
        public LoopbackTransport Listen()
        {
            if (_server != null) throw new InvalidOperationException("서버 끝은 이미 있다");
            _server = new LoopbackTransport(this, isServer: true);
            return _server;
        }

        /// <summary>서버에 붙는 클라이언트 끝. 지연이 지나면 양쪽에 연결 일이 올라온다. 서버가 없으면 연결 실패(끊김 일)다.</summary>
        public LoopbackTransport Connect()
        {
            var client = new LoopbackTransport(this, isServer: false);
            if (_server == null || _server.Disposed)
            {
                _inFlight.Add(new Packet(new Link(0, client, null), client, new TransportEvent(TransportEventKind.Disconnected, Link.ClientSidePeer, null), _now + LatencySeconds, _nextOrder++));
                return client;
            }

            var link = new Link(_nextPeer++, client, _server);
            client.Link = link;
            _server.Links.Add(link.ServerSidePeer, link);
            double at = _now + LatencySeconds;
            _inFlight.Add(new Packet(link, _server, new TransportEvent(TransportEventKind.Connected, link.ServerSidePeer, null), at, _nextOrder++));
            _inFlight.Add(new Packet(link, client, new TransportEvent(TransportEventKind.Connected, Link.ClientSidePeer, null), at, _nextOrder++));
            return client;
        }

        /// <summary>선을 끊는다: 양쪽 모두 알림 없이 이후 바이트를 잃는다(시간 초과로만 알 수 있다).</summary>
        public void Sever(LoopbackTransport client)
        {
            if (client?.Link != null) client.Link.Severed = true;
        }

        internal void Advance(double now)
        {
            if (now > _now) _now = now;
            for (int i = _inFlight.Count - 1; i >= 0; i--)
            {
                var packet = _inFlight[i];
                if (packet.DeliverAt > _now) continue;
                _inFlight.RemoveAt(i);
                if (packet.Link.Severed || packet.Link.Closed && packet.Event.Kind == TransportEventKind.Data) continue;
                packet.To.Enqueue(packet.Event, packet.DeliverAt, packet.Order);
            }
        }

        internal void SendOver(Link link, LoopbackTransport from, TransportEvent transportEvent, Delivery delivery)
        {
            if (link.Severed || link.Closed) return;
            var to = from == link.Client ? (LoopbackTransport)link.Server : link.Client;
            bool reliable = delivery == Delivery.ReliableOrdered || transportEvent.Kind != TransportEventKind.Data;
            if (!reliable && LossRate > 0 && _random.NextDouble() < LossRate) return;
            double at = _now + LatencySeconds + (JitterSeconds > 0 ? _random.NextDouble() * JitterSeconds : 0);
            if (reliable)
            {
                ref double last = ref (from == link.Client ? ref link.LastReliableToServer : ref link.LastReliableToClient);
                if (at < last) at = last;
                last = at;
            }

            _inFlight.Add(new Packet(link, to, transportEvent, at, _nextOrder++));
        }

        internal double Now => _now;

        internal sealed class Link
        {
            public static readonly PeerId ClientSidePeer = new PeerId(0);

            public Link(long id, LoopbackTransport client, LoopbackTransport server)
            {
                ServerSidePeer = new PeerId(id);
                Client = client;
                Server = server;
            }

            public PeerId ServerSidePeer { get; }
            public LoopbackTransport Client { get; }
            public LoopbackTransport Server { get; }
            public bool Severed;
            public bool Closed;
            public double LastReliableToServer;
            public double LastReliableToClient;
        }

        private readonly struct Packet
        {
            public Packet(Link link, LoopbackTransport to, TransportEvent transportEvent, double deliverAt, long order)
            {
                Link = link;
                To = to;
                Event = transportEvent;
                DeliverAt = deliverAt;
                Order = order;
            }

            public Link Link { get; }
            public LoopbackTransport To { get; }
            public TransportEvent Event { get; }
            public double DeliverAt { get; }
            public long Order { get; }
        }
    }

    /// <summary><see cref="LoopbackNetwork"/>의 한 끝.</summary>
    public sealed class LoopbackTransport : ITransport
    {
        private readonly LoopbackNetwork _network;
        private readonly List<(double At, long Order, TransportEvent Event)> _arrived = new List<(double, long, TransportEvent)>(64);
        private readonly Queue<TransportEvent> _ready = new Queue<TransportEvent>(64);

        internal LoopbackTransport(LoopbackNetwork network, bool isServer)
        {
            _network = network;
            IsServer = isServer;
        }

        internal bool IsServer { get; }
        internal bool Disposed { get; private set; }
        internal LoopbackNetwork.Link Link { get; set; }
        internal Dictionary<PeerId, LoopbackNetwork.Link> Links { get; } = new Dictionary<PeerId, LoopbackNetwork.Link>();

        public void Update(double now)
        {
            if (Disposed) return;
            _network.Advance(now);
            // 같은 Update 안에서 닿은 것은 닿은 시각, 보낸 순서로 꺼낸다.
            _arrived.Sort((a, b) => a.At != b.At ? a.At.CompareTo(b.At) : a.Order.CompareTo(b.Order));
            foreach (var item in _arrived) _ready.Enqueue(item.Event);
            _arrived.Clear();
        }

        public bool TryReceive(out TransportEvent transportEvent)
        {
            if (_ready.Count > 0)
            {
                transportEvent = _ready.Dequeue();
                if (transportEvent.Kind == TransportEventKind.Disconnected)
                {
                    if (IsServer) Links.Remove(transportEvent.Peer);
                    else Link = null;
                }
                return true;
            }

            transportEvent = default;
            return false;
        }

        public void Send(PeerId peer, byte[] data, Delivery delivery)
        {
            var link = LinkFor(peer);
            if (link == null || data == null) return;
            _network.SendOver(link, this, new TransportEvent(TransportEventKind.Data, OtherSidePeer(link), (byte[])data.Clone()), delivery);
        }

        public void Disconnect(PeerId peer, byte[] reason)
        {
            var link = LinkFor(peer);
            if (link == null || link.Closed) return;
            _network.SendOver(link, this, new TransportEvent(TransportEventKind.Disconnected, OtherSidePeer(link), reason), Delivery.ReliableOrdered);
            link.Closed = true;
            if (IsServer) Links.Remove(peer);
            else Link = null;
        }

        public void Dispose()
        {
            if (Disposed) return;
            if (IsServer)
                foreach (var peer in new List<PeerId>(Links.Keys)) Disconnect(peer, null);
            else if (Link != null) Disconnect(LoopbackNetwork.Link.ClientSidePeer, null);
            Disposed = true;
        }

        internal void Enqueue(TransportEvent transportEvent, double at, long order)
        {
            if (Disposed) return;
            _arrived.Add((at, order, transportEvent));
        }

        private LoopbackNetwork.Link LinkFor(PeerId peer)
        {
            if (Disposed) return null;
            if (IsServer) return Links.TryGetValue(peer, out var link) ? link : null;
            return Link != null && peer.Equals(LoopbackNetwork.Link.ClientSidePeer) ? Link : null;
        }

        private PeerId OtherSidePeer(LoopbackNetwork.Link link) => IsServer ? LoopbackNetwork.Link.ClientSidePeer : link.ServerSidePeer;
    }
}
