using System;
using System.Collections.Generic;
using Rootborn.Core.Network;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;
using Rootborn.Core.Time;
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
    /// - 방의 시계(Core RoomClock)를 가진다. 들어오면 참가자로 넣고 나가면 뺀다(같은 신원이 연결만 바꾸는 것은 나간 것이 아니다).
    ///   클라이언트가 때를 끝냈다(FinishPart)·하루를 마감했다(DayEndReady)고 하면 Core 규칙으로 판정한다. 바뀔 때마다 모두에게 보내고
    ///   저장소의 그 슬롯(room-clock.json)에 쓴다. 자동 넘기기의 틱은 Update 한 번이 하나다.
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
        private readonly List<RoomParticipantState> _clockScratch = new List<RoomParticipantState>(8);
        private readonly List<PeerId> _peerScratch = new List<PeerId>(8);
        private ushort _nextNetId = 1;
        private double _now;
        private bool _disposed;

        /// <param name="store">방의 시계를 읽고 쓸 저장소. 없으면 저장하지 않는다.</param>
        /// <param name="calendar">하루의 때 수를 주는 달력(정의 데이터). 없으면 Core 기본 달력.</param>
        public SessionServer(ITransport transport, SessionServerSettings settings, ISaveStore store = null, string saveSlot = null, LifeCalendar calendar = null)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _settings = settings ?? new SessionServerSettings();
            _settings.Validate();
            _catalog = RootbornMessages.CreateCatalog(_settings.MaxMessageBytes);
            _store = store;
            _saveSlot = saveSlot;
            Clock = RoomClockSaveFiles.Load(store, saveSlot, calendar ?? new LifeCalendar(), _settings.Clock);
            _handlers = new MessageHandlers<PeerId>()
                .On<HeartbeatMessage>((p, m) => { })
                .On<PoseMessage>(OnPose)
                .On<LookMessage>(OnLook)
                .On<DayEndReadyMessage>(OnDayEndReady)
                .On<FinishPartMessage>(OnFinishPart);
        }

        public RoomClock Clock { get; }
        public DropCounters Drops { get; } = new DropCounters();

        /// <summary>
        /// 들어왔다(받아들임 인사·다른 사람·방의 시계를 보낸 뒤). 같은 신원이 연결만 바꿔도 부른다(그 연결에 다시 보낼 것이 있다).
        /// 방(RoomServer)이 방 설정·세이브·조각 상태를 보낸다.
        /// </summary>
        public event Action<ServerPlayer> PlayerJoined;

        /// <summary>나갔다(끊김, 시간 초과, 연결 바꿈).</summary>
        public event Action<ServerPlayer> PlayerLeft;

        /// <summary>들어온 사람이 보낸 그 종류의 메시지를 받는다(세션이 처리하지 않는 종류만). 종류마다 한 줄로 더한다.</summary>
        public SessionServer On<T>(Action<ServerPlayer, T> handler) where T : class
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _handlers.On<T>((peer, m) => handler(_connections[peer].Player, m));
            return this;
        }

        /// <summary>그 사람에게 신뢰 순서로 보낸다. 나간 사람이면 아무 일도 없다.</summary>
        public void Send(ServerPlayer player, object message)
        {
            if (player == null || !_players.TryGetValue(player.PlayerId, out var current) || current != player) return;
            Send(player.Peer, message, Delivery.ReliableOrdered);
        }

        /// <summary>들어와 있는 모두에게 신뢰 순서로 보낸다.</summary>
        public void Broadcast(object message) => Broadcast(message, Delivery.ReliableOrdered, except: null);
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

            if (Clock.Tick(1)) ClockChanged(saveIt: true);

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

            if (previous != null)
            {
                // 연결만 바뀐다. 방의 시계에서는 나간 것이 아니다(남은 사람만으로 때가 넘어가지 않게).
                Drop(previous.Peer, ReplacedReason, notify: false);
                RemovePlayer(previous, leaveClock: false);
            }

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

            bool joined = Clock.Join(player.PlayerId);
            ClockChanged(saveIt: joined);
            PlayerJoined?.Invoke(player);
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

        // 받아들이지 않은 요청(기다리는 동안 보낸 것 등)은 지금 시계를 그 사람에게 다시 보내 맞춘다.
        // 날이 적힌 요청(v2)은 그 날에만 받는다(지난 날의 마감이 새 날을 마감하지 않게). 날 0은 지금 날(v1).
        private void OnDayEndReady(PeerId peer, DayEndReadyMessage message)
        {
            bool sameDay = message.Day == 0 || message.Day == Clock.Day;
            if (sameDay && Clock.EndDay(_connections[peer].Player.PlayerId)) ClockChanged(saveIt: true);
            else SendClock(peer);
        }

        private void OnFinishPart(PeerId peer, FinishPartMessage message)
        {
            string id = _connections[peer].Player.PlayerId;
            bool changed = message.Day == 0 ? Clock.Spend(id, message.Points) : Clock.SpendFrom(id, message.Day, message.FromPart, message.Points);
            if (changed) ClockChanged(saveIt: true);
            else SendClock(peer);
        }

        private void RemovePlayer(ServerPlayer player, bool leaveClock = true)
        {
            if (!_players.TryGetValue(player.PlayerId, out var current) || current != player) return;
            _players.Remove(player.PlayerId);
            Broadcast(new PlayerLeftMessage { NetId = player.NetId }, Delivery.ReliableOrdered, except: null);
            PlayerLeft?.Invoke(player);
            if (!leaveClock) return;
            bool left = Clock.Leave(player.PlayerId);
            ClockChanged(saveIt: left);
        }

        /// <summary>연결을 끊고(이유를 실어), 들어와 있던 사람이면 지운다.</summary>
        private void Drop(PeerId peer, string reason, bool notify = true)
        {
            if (!_connections.Remove(peer, out var connection)) return;
            _catalog.TryEncode(new FarewellMessage { Reason = reason }, out var bytes);
            _transport.Disconnect(peer, bytes);
            if (connection.Player != null && notify) RemovePlayer(connection.Player);
        }

        private void ClockChanged(bool saveIt)
        {
            Broadcast(ClockMessage(), Delivery.ReliableOrdered, except: null);
            if (saveIt) RoomClockSaveFiles.Save(_store, _saveSlot, Clock);
        }

        private void SendClock(PeerId peer) => Send(peer, ClockMessage(), Delivery.ReliableOrdered);

        private RoomClockMessage ClockMessage()
        {
            Clock.CopyPresent(_clockScratch);
            var entries = new List<RoomClockEntry>(_clockScratch.Count);
            foreach (var state in _clockScratch)
            {
                if (!_players.TryGetValue(state.ParticipantId, out var player)) continue;
                entries.Add(new RoomClockEntry
                {
                    NetId = player.NetId, FinishedPart = state.FinishedPart, DayEnded = state.DayEnded,
                    CompletedParts = (byte)Math.Min(byte.MaxValue, Math.Max(0, state.CompletedParts)),
                });
            }

            return new RoomClockMessage
            {
                Day = Clock.Day,
                PartIndex = (byte)Math.Min(byte.MaxValue, Clock.PartIndex),
                PartsToday = (byte)Math.Min(byte.MaxValue, Clock.PartsToday),
                Entries = entries.ToArray(),
            };
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
