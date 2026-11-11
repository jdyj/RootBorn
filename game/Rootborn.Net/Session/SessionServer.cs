using System;
using System.Collections.Generic;
using Rootborn.Core.Network;
using Rootborn.Core.Save;
using Rootborn.Net.Messages;
using Rootborn.Net.Transport;

namespace Rootborn.Net.Session
{
    /// <summary>서버가 아는 플레이어 한 명.</summary>
    public sealed class ServerPlayer
    {
        internal ServerPlayer(string playerId, ushort netId, int joinIndex)
        {
            PlayerId = playerId;
            NetId = netId;
            JoinIndex = joinIndex;
        }

        public string PlayerId { get; }
        public ushort NetId { get; }
        public int JoinIndex { get; }
        public string Look { get; internal set; } = string.Empty;
        public bool HasPose { get; internal set; }
        public PoseSample LastPose { get; internal set; }
        internal PeerId Peer { get; set; }
    }

    /// <summary>
    /// 접속 세션의 서버 쪽(조사 문서 3.3의 A 모델: 각자 자기 판, 서버는 위치·겉모습·공용 시간·누가 어느 맵에 있는지만 전한다).
    /// - 접속 승인: 들어온 사람 수 &lt; 정원(Core SessionCapacity). 거절 이유는 끊김에 실린다.
    /// - 신원: 클라이언트가 Hello로 보낸 안정된 id. 같은 신원은 같은 번호(NetId)와 같은 도착 자리(Core SpawnSpread 순번)를 받는다.
    ///   같은 신원이 다시 붙으면 옛 연결을 끊고 새 연결로 바꾼다(다른 사람에게는 나갔다 들어온 것으로 보인다).
    /// - 위치는 주인 것만 받는다(보낸 연결로 누구인지 정한다). 번호가 더 새 것만 쓰고 다른 사람에게 전한다.
    /// - 공용 시간(Core SharedWorldTime)을 가지고, 바뀔 때마다 모두에게 보내고 저장소에 쓴다.
    /// - 끊김과 시간 초과: 끊기거나 TimeoutSeconds 동안 아무것도 받지 못하면 그 사람을 지우고 다른 사람에게 알린다.
    /// 깨진·모르는·너무 큰 메시지는 버리고 <see cref="Drops"/>에 센다. 시간은 Update에 넘긴 값만 쓴다.
    /// </summary>
    public sealed class SessionServer : IDisposable
    {
        public const string InvalidIdentityReason = "invalid identity";
        public const string ReplacedReason = "replaced by new connection";
        public const string TimeoutReason = "timeout";
        public const string ShutdownReason = "server shutdown";

        private readonly ITransport _transport;
        private readonly SessionServerSettings _settings;
        private readonly MessageCatalog _catalog;
        private readonly MessageHandlers<PeerId> _handlers;
        private readonly ISaveStore _store;
        private readonly string _saveSlot;
        private readonly Dictionary<PeerId, Connection> _connections = new Dictionary<PeerId, Connection>(8);
        private readonly Dictionary<string, ServerPlayer> _players = new Dictionary<string, ServerPlayer>(StringComparer.Ordinal);
        private readonly Dictionary<string, (ushort NetId, int JoinIndex)> _known = new Dictionary<string, (ushort, int)>(StringComparer.Ordinal);
        private readonly List<string> _present = new List<string>(8);
        private readonly List<PeerId> _peerScratch = new List<PeerId>(8);
        private ushort _nextNetId = 1;
        private double _now;
        private bool _disposed;

        /// <param name="store">공용 시간을 읽고 쓸 저장소. 없으면 저장하지 않는다.</param>
        public SessionServer(ITransport transport, SessionServerSettings settings, ISaveStore store = null, string saveSlot = null)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _settings = settings ?? new SessionServerSettings();
            _settings.Validate();
            _catalog = RootbornMessages.CreateCatalog(_settings.MaxMessageBytes);
            _store = store;
            _saveSlot = saveSlot;
            WorldTime = SharedWorldTime.Load(store, saveSlot);
            _handlers = new MessageHandlers<PeerId>()
                .On<HeartbeatMessage>((p, m) => { })
                .On<PoseMessage>(OnPose)
                .On<LookMessage>(OnLook)
                .On<DayEndReadyMessage>(OnDayEndReady)
                .On<AdvancePartOfDayMessage>(OnAdvancePartOfDay);
        }

        public SharedWorldTime WorldTime { get; }
        public DropCounters Drops { get; } = new DropCounters();
        public IReadOnlyCollection<ServerPlayer> Players => _players.Values;

        public IEnumerable<ServerPlayer> PlayersOnMap(string mapId)
        {
            foreach (var player in _players.Values)
                if (player.HasPose && player.LastPose.MapId == mapId) yield return player;
        }

        public void Update(double now)
        {
            if (_disposed) return;
            _now = now;
            _transport.Update(now);
            while (_transport.TryReceive(out var e))
            {
                switch (e.Kind)
                {
                    case TransportEventKind.Connected:
                        _connections[e.Peer] = new Connection { LastHeard = now, LastSent = now };
                        break;
                    case TransportEventKind.Disconnected:
                        if (_connections.Remove(e.Peer, out var gone) && gone.Player != null) RemovePlayer(gone.Player);
                        break;
                    case TransportEventKind.Data:
                        Receive(e.Peer, e.Data);
                        break;
                }
            }

            _peerScratch.Clear();
            _peerScratch.AddRange(_connections.Keys);
            foreach (var peer in _peerScratch)
            {
                if (!_connections.TryGetValue(peer, out var connection)) continue;
                if (now - connection.LastHeard > _settings.TimeoutSeconds)
                {
                    Drop(peer, TimeoutReason);
                    continue;
                }

                if (connection.Player != null && now - connection.LastSent >= _settings.HeartbeatSeconds)
                    Send(peer, new HeartbeatMessage(), Delivery.Unreliable);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            foreach (var peer in new List<PeerId>(_connections.Keys)) Drop(peer, ShutdownReason, notify: false);
            _transport.Dispose();
            _disposed = true;
        }

        private void Receive(PeerId peer, byte[] data)
        {
            if (!_connections.TryGetValue(peer, out var connection)) return;
            connection.LastHeard = _now;
            var outcome = _catalog.Decode(data, out var message);
            if (outcome != DecodeOutcome.Ok)
            {
                Drops.Count(outcome);
                return;
            }

            if (message is HelloMessage hello)
            {
                if (connection.Player != null) Drops.CountUnexpected();
                else OnHello(peer, hello);
                return;
            }

            if (connection.Player == null || !_handlers.Dispatch(peer, message)) Drops.CountUnexpected();
        }

        private void OnHello(PeerId peer, HelloMessage hello)
        {
            if (string.IsNullOrEmpty(hello.PlayerId))
            {
                Drop(peer, InvalidIdentityReason);
                return;
            }

            _players.TryGetValue(hello.PlayerId, out var previous);
            int others = _players.Count - (previous != null ? 1 : 0);
            var decision = SessionCapacity.Decide(others, _settings.MaxPlayers);
            if (!decision.Approved)
            {
                Drop(peer, decision.Reason);
                return;
            }

            if (previous != null) Drop(previous.Peer, ReplacedReason);

            if (!_known.TryGetValue(hello.PlayerId, out var known))
            {
                known = (_nextNetId++, -1);
            }

            int joinIndex = known.JoinIndex >= 0 && !IndexTaken(known.JoinIndex) ? known.JoinIndex : FreeIndex();
            _known[hello.PlayerId] = (known.NetId, joinIndex);
            var player = new ServerPlayer(hello.PlayerId, known.NetId, joinIndex) { Look = hello.Look ?? string.Empty, Peer = peer };
            _connections[peer].Player = player;
            _players[player.PlayerId] = player;

            var offset = SpawnSpread.Offset(joinIndex);
            Send(peer, new WelcomeMessage { NetId = player.NetId, JoinIndex = joinIndex, SpawnOffsetX = offset.X, SpawnOffsetY = offset.Y }, Delivery.ReliableOrdered);
            foreach (var other in _players.Values)
            {
                if (other == player) continue;
                Send(peer, Joined(other), Delivery.ReliableOrdered);
                Send(other.Peer, Joined(player), Delivery.ReliableOrdered);
            }

            WorldTime.PlayersChanged(Present());
            TimeChanged(saveIt: false);
        }

        private void OnPose(PeerId peer, PoseMessage pose)
        {
            var player = _connections[peer].Player;
            if (player.HasPose && pose.Sequence <= player.LastPose.Sequence)
            {
                Drops.CountUnexpected();
                return;
            }

            bool mapChanged = !player.HasPose || player.LastPose.MapId != pose.MapId;
            pose.NetId = player.NetId;
            player.LastPose = pose.ToSample();
            player.HasPose = true;
            Broadcast(pose, mapChanged ? Delivery.ReliableOrdered : Delivery.Unreliable, except: player);
        }

        private void OnLook(PeerId peer, LookMessage look)
        {
            var player = _connections[peer].Player;
            player.Look = look.Look ?? string.Empty;
            look.NetId = player.NetId;
            Broadcast(look, Delivery.ReliableOrdered, except: player);
        }

        private void OnDayEndReady(PeerId peer, DayEndReadyMessage message)
        {
            WorldTime.MarkReady(_connections[peer].Player.PlayerId, Present());
            TimeChanged(saveIt: true);
        }

        private void OnAdvancePartOfDay(PeerId peer, AdvancePartOfDayMessage message)
        {
            WorldTime.AdvancePartOfDay();
            TimeChanged(saveIt: true);
        }

        private void RemovePlayer(ServerPlayer player)
        {
            if (!_players.TryGetValue(player.PlayerId, out var current) || current != player) return;
            _players.Remove(player.PlayerId);
            Broadcast(new PlayerLeftMessage { NetId = player.NetId }, Delivery.ReliableOrdered, except: null);
            bool advanced = WorldTime.PlayersChanged(Present());
            TimeChanged(saveIt: advanced);
        }

        /// <summary>연결을 끊고(이유를 실어), 들어와 있던 사람이면 지운다.</summary>
        private void Drop(PeerId peer, string reason, bool notify = true)
        {
            if (!_connections.Remove(peer, out var connection)) return;
            _catalog.TryEncode(new FarewellMessage { Reason = reason }, out var bytes);
            _transport.Disconnect(peer, bytes);
            if (connection.Player != null && notify) RemovePlayer(connection.Player);
        }

        private void TimeChanged(bool saveIt)
        {
            var message = new WorldTimeMessage
            {
                Day = WorldTime.Day,
                PartOfDay = (byte)WorldTime.PartOfDay,
                Phase = (byte)WorldTime.Phase,
                ReadyCount = (byte)Math.Min(byte.MaxValue, WorldTime.ReadyCount),
                PlayerCount = (byte)Math.Min(byte.MaxValue, _players.Count),
            };
            Broadcast(message, Delivery.ReliableOrdered, except: null);
            if (saveIt) SharedWorldTime.Save(_store, _saveSlot, WorldTime);
        }

        private PlayerJoinedMessage Joined(ServerPlayer player)
        {
            var message = new PlayerJoinedMessage { NetId = player.NetId, PlayerId = player.PlayerId, Look = player.Look, HasPose = player.HasPose };
            if (player.HasPose)
            {
                var p = player.LastPose;
                message.Pose = new PoseMessage
                {
                    NetId = player.NetId, Sequence = p.Sequence, SenderTime = p.SenderTime, MapId = p.MapId,
                    X = p.X, Y = p.Y, Facing = (byte)p.Facing, Walking = p.Walking,
                };
            }

            return message;
        }

        private IReadOnlyCollection<string> Present()
        {
            _present.Clear();
            _present.AddRange(_players.Keys);
            return _present;
        }

        private bool IndexTaken(int index)
        {
            foreach (var player in _players.Values)
                if (player.JoinIndex == index) return true;
            return false;
        }

        private int FreeIndex()
        {
            int index = 0;
            while (IndexTaken(index)) index++;
            return index;
        }

        private void Broadcast(object message, Delivery delivery, ServerPlayer except)
        {
            if (!_catalog.TryEncode(message, out var bytes)) return;
            foreach (var player in _players.Values)
            {
                if (player == except) continue;
                SendBytes(player.Peer, bytes, delivery);
            }
        }

        private void Send(PeerId peer, object message, Delivery delivery)
        {
            if (_catalog.TryEncode(message, out var bytes)) SendBytes(peer, bytes, delivery);
        }

        private void SendBytes(PeerId peer, byte[] bytes, Delivery delivery)
        {
            _transport.Send(peer, bytes, delivery);
            if (_connections.TryGetValue(peer, out var connection)) connection.LastSent = _now;
        }

        private sealed class Connection
        {
            public double LastHeard;
            public double LastSent;
            public ServerPlayer Player;
        }
    }
}
