using System;
using System.Collections.Generic;
using Rootborn.Core.Network;
using Rootborn.Core.Time;
using Rootborn.Core.World;
using Rootborn.Net.Messages;
using Rootborn.Net.Transport;

namespace Rootborn.Net.Session
{
    public enum ClientStatus
    {
        Connecting,
        Joined,
        /// <summary>서버가 받지 않았다(이유는 FarewellReason, 정원 초과는 Core SessionCapacity.ServerFullReason).</summary>
        Refused,
        Disconnected,
    }

    /// <summary>지금 그릴 남 한 명.</summary>
    public sealed class RemotePlayerView
    {
        public string PlayerId { get; internal set; }
        public ushort NetId { get; internal set; }
        public string Look { get; internal set; }
        public string MapId { get; internal set; }
        public float X { get; internal set; }
        public float Y { get; internal set; }
        public Facing Facing { get; internal set; }
        public bool Walking { get; internal set; }
    }

    /// <summary>
    /// 서버에서 받은 방의 시계(Core RoomClock의 상태). Known이 false면 아직 받지 않았다. 사람은 NetId로 가리킨다(자기 것은 SessionClient.NetId).
    /// 대표 시각은 부르는 쪽이 자기 달력으로 정한다(<see cref="RepresentativeMinutes"/>).
    /// </summary>
    public sealed class RoomClockView
    {
        private static readonly RoomClockEntry[] None = Array.Empty<RoomClockEntry>();
        private readonly RoomClockEntry[] _entries;

        public static RoomClockView Unknown { get; } = new RoomClockView();

        private RoomClockView() { _entries = None; }

        internal RoomClockView(RoomClockMessage m)
        {
            Known = true;
            Day = m.Day;
            PartIndex = m.PartIndex;
            PartsToday = m.PartsToday;
            _entries = m.Entries ?? None;
        }

        public bool Known { get; }
        public int Day { get; }
        public int PartIndex { get; }
        public int PartsToday { get; }
        public bool IsDayOver => Known && PartIndex >= PartsToday;
        public int PlayerCount => _entries.Length;
        public IReadOnlyList<RoomClockEntry> Entries => _entries;
        public int WeekdayIndex => Day < 1 ? 0 : (Day - 1) % 7;

        public int FinishedCount => Count(e => e.FinishedPart);
        public int DayEndedCount => Count(e => e.DayEnded);

        public bool HasFinished(ushort netId) => Find(netId, out var e) && e.FinishedPart;
        public int CompletedParts(ushort netId) => Find(netId, out var e) ? e.CompletedParts : 0;
        public bool TryGetEntry(ushort netId, out RoomClockEntry entry) => Find(netId, out entry);
        public bool IsDayEnded(ushort netId) => Find(netId, out var e) && e.DayEnded;

        public int RepresentativeMinutes(PartOfDayTimes times) => (times ?? PartOfDayTimes.Default).MinutesFor(PartIndex, PartsToday);

        private bool Find(ushort netId, out RoomClockEntry entry)
        {
            foreach (var e in _entries)
                if (e.NetId == netId) { entry = e; return true; }
            entry = default;
            return false;
        }

        private int Count(Func<RoomClockEntry, bool> test)
        {
            int count = 0;
            foreach (var e in _entries) if (test(e)) count++;
            return count;
        }
    }

    /// <summary>
    /// 접속 세션의 클라이언트 쪽. 연결되면 신원과 겉모습을 보내고, 받아들여지면 자기 위치를 초당 PoseSendsPerSecond번 보낸다
    /// (맵이 바뀌면 그때 신뢰로 한 번). 남의 위치는 Core PoseBuffer로 사이를 메워 <see cref="PlayersOn"/>에서 준다.
    /// 위치 단위는 부르는 쪽(Host)의 맵 좌표다. 도착 자리 차이(<see cref="SpawnOffset"/>)는 칸 단위라 칸 크기를 곱해 쓴다.
    /// 서버가 TimeoutSeconds 동안 조용하면 끊긴 것으로 본다. 시간은 Update에 넘긴 값만 쓴다.
    /// </summary>
    public sealed class SessionClient : IDisposable
    {
        public const string TimeoutReason = "timeout";
        public const string ClosedReason = "connection closed";

        private static readonly PeerId Server = new PeerId(0);

        private readonly ITransport _transport;
        private readonly SessionClientSettings _settings;
        private readonly MessageCatalog _catalog;
        private readonly MessageHandlers<double> _handlers;
        private readonly Dictionary<ushort, Remote> _remotes = new Dictionary<ushort, Remote>(8);
        private readonly List<RemotePlayerView> _viewBuffer = new List<RemotePlayerView>(8);
        private readonly string _playerId;
        private PeerId _server = Server;
        private string _look;
        private bool _connected;
        private bool _hasPose;
        private bool _poseMapChanged;
        private string _mapId;
        private float _x, _y;
        private Facing _facing;
        private bool _walking;
        private string _sentMapId;
        private uint _sequence;
        private double _nextPoseAt;
        private double _lastHeard;
        private double _lastSent;
        private bool _disposed;

        public SessionClient(ITransport transport, string playerId, string look, SessionClientSettings settings)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _settings = settings ?? new SessionClientSettings();
            _settings.Validate();
            _catalog = RootbornMessages.CreateCatalog(_settings.MaxMessageBytes);
            _playerId = playerId ?? string.Empty;
            _look = look ?? string.Empty;
            _handlers = new MessageHandlers<double>()
                .On<HeartbeatMessage>((now, m) => { })
                .On<WelcomeMessage>(OnWelcome)
                .On<PlayerJoinedMessage>(OnJoined)
                .On<PlayerLeftMessage>((now, m) => _remotes.Remove(m.NetId))
                .On<PoseMessage>(OnPose)
                .On<LookMessage>(OnLook)
                .On<RoomClockMessage>(OnRoomClock);
        }

        public ClientStatus Status { get; private set; } = ClientStatus.Connecting;

        /// <summary>끊기거나 거절된 이유. 서버가 실은 이유가 없으면 TimeoutReason 또는 ClosedReason.</summary>
        public string FarewellReason { get; private set; } = string.Empty;

        public ushort NetId { get; private set; }
        public int JoinIndex { get; private set; } = -1;
        public SpawnOffset SpawnOffset { get; private set; }
        public RoomClockView RoomClock { get; private set; } = RoomClockView.Unknown;

        /// <summary>서버에서 방의 시계를 받을 때마다(Update 안에서).</summary>
        public event Action RoomClockChanged;

        /// <summary>들어온 뒤 서버가 보낸 그 종류의 메시지를 받는다(세션이 처리하지 않는 종류만, Update 안에서). 종류마다 한 줄로 더한다.</summary>
        public SessionClient On<T>(Action<T> handler) where T : class
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _handlers.On<T>((now, m) => handler(m));
            return this;
        }

        /// <summary>들어와 있으면 서버에 신뢰 순서로 보낸다. 보냈으면 true(등록하지 않은 메시지이거나 너무 크면 false).</summary>
        public bool Send(object message)
        {
            if (Status != ClientStatus.Joined || message == null || !_catalog.TryEncode(message, out var bytes)) return false;
            _transport.Send(_server, bytes, Delivery.ReliableOrdered);
            return true;
        }
        public DropCounters Drops { get; } = new DropCounters();

        public IEnumerable<string> RemotePlayerIds
        {
            get
            {
                foreach (var remote in _remotes.Values) yield return remote.PlayerId;
            }
        }

        /// <summary>자기 몸. 매 프레임 불러도 된다(보내는 횟수는 설정이 정한다).</summary>
        public void SetPose(string mapId, float x, float y, Facing facing, bool walking)
        {
            if (string.IsNullOrEmpty(mapId)) throw new ArgumentException("맵 id가 비어 있다", nameof(mapId));
            if (mapId != _mapId) _poseMapChanged = true;
            _mapId = mapId;
            _x = x;
            _y = y;
            _facing = facing;
            _walking = walking;
            _hasPose = true;
        }

        /// <summary>겉모습 키(Core WearableLookCode.Encode). 바뀌면 보낸다.</summary>
        public void SetLook(string look)
        {
            look ??= string.Empty;
            if (look == _look) return;
            _look = look;
            if (Status == ClientStatus.Joined) Send(new LookMessage { Look = look }, Delivery.ReliableOrdered);
        }

        /// <summary>그 날(day)의 내 하루를 끝냈다. 0이면 서버의 지금 날. 서버는 다른 날의 마감은 받지 않는다.</summary>
        public void RequestDayEndReady(int day = 0)
        {
            if (Status == ClientStatus.Joined && day >= 0) Send(new DayEndReadyMessage { Day = day }, Delivery.ReliableOrdered);
        }

        /// <summary>시간 포인트를 points만큼 써서 그만큼의 때를 끝냈다(서버의 지금 날·지금 때). 판정은 서버가 한다(기다리는 중이면 받지 않고 시계를 다시 보낸다).</summary>
        public void RequestFinishPart(int points) => RequestFinishPart(0, 0, points);

        /// <summary>(day, fromPart)에서 시간 포인트를 points만큼 써서 그만큼의 때를 끝냈다. 늦게 와도 서버가 두 번 세지 않는다.</summary>
        public void RequestFinishPart(int day, int fromPart, int points)
        {
            if (Status != ClientStatus.Joined || points < 1 || day < 0 || fromPart < 0) return;
            Send(new FinishPartMessage
            {
                Day = day, FromPart = (byte)Math.Min(byte.MaxValue, fromPart), Points = (byte)Math.Min(byte.MaxValue, points),
            }, Delivery.ReliableOrdered);
        }

        /// <summary>그 번호의 사람의 신원(자기 포함). 모르면 false.</summary>
        public bool TryGetPlayerId(ushort netId, out string playerId)
        {
            if (Status == ClientStatus.Joined && netId == NetId) { playerId = _playerId; return true; }
            if (_remotes.TryGetValue(netId, out var remote)) { playerId = remote.PlayerId; return true; }
            playerId = null;
            return false;
        }

        /// <summary>"넘기기": 시간 포인트를 쓰지 않고 지금 때를 끝낸다.</summary>
        public void RequestSkipPart() => RequestFinishPart(1);

        /// <summary>그 맵에 있는 남(자기 제외)을 지금 시각으로 메워서. 돌려준 목록은 다음 부를 때 다시 쓴다.</summary>
        public IReadOnlyList<RemotePlayerView> PlayersOn(string mapId, double now)
        {
            _viewBuffer.Clear();
            foreach (var remote in _remotes.Values)
            {
                var pose = remote.Poses.Sample(now);
                if (!pose.HasPose || pose.MapId != mapId) continue;
                var view = remote.View;
                view.Look = remote.Look;
                view.MapId = pose.MapId;
                view.X = pose.X;
                view.Y = pose.Y;
                view.Facing = pose.Facing;
                view.Walking = pose.Walking;
                _viewBuffer.Add(view);
            }

            return _viewBuffer;
        }

        public void Update(double now)
        {
            if (_disposed) return;
            _transport.Update(now);
            while (_transport.TryReceive(out var e))
            {
                switch (e.Kind)
                {
                    case TransportEventKind.Connected:
                        _connected = true;
                        _server = e.Peer;
                        _lastHeard = now;
                        Send(new HelloMessage { PlayerId = _playerId, Look = _look }, Delivery.ReliableOrdered, now);
                        break;
                    case TransportEventKind.Disconnected:
                        OnDisconnected(e.Data);
                        break;
                    case TransportEventKind.Data:
                        Receive(e.Data, now);
                        break;
                }
            }

            if (!_connected || Status == ClientStatus.Refused || Status == ClientStatus.Disconnected) return;

            if (now - _lastHeard > _settings.TimeoutSeconds)
            {
                _transport.Disconnect(_server, null);
                Close(ClientStatus.Disconnected, TimeoutReason);
                return;
            }

            if (Status != ClientStatus.Joined) return;
            if (_hasPose && (_poseMapChanged || now >= _nextPoseAt))
            {
                var delivery = _poseMapChanged || _sentMapId == null ? Delivery.ReliableOrdered : Delivery.Unreliable;
                Send(new PoseMessage
                {
                    Sequence = ++_sequence, SenderTime = now, MapId = _mapId, X = _x, Y = _y, Facing = (byte)_facing, Walking = _walking,
                }, delivery, now);
                _sentMapId = _mapId;
                _poseMapChanged = false;
                _nextPoseAt = now + 1.0 / _settings.PoseSendsPerSecond;
            }

            if (now - _lastSent >= _settings.HeartbeatSeconds) Send(new HeartbeatMessage(), Delivery.Unreliable, now);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _transport.Dispose();
            _remotes.Clear();
            _disposed = true;
        }

        private void Receive(byte[] data, double now)
        {
            _lastHeard = now;
            var outcome = _catalog.Decode(data, out var message);
            if (outcome != DecodeOutcome.Ok)
            {
                Drops.Count(outcome);
                return;
            }

            // 들어오기 전에는 Welcome만, 들어온 뒤에는 Welcome 말고 다른 것만 받는다.
            bool welcome = message is WelcomeMessage;
            bool expected = Status == ClientStatus.Connecting ? welcome : Status == ClientStatus.Joined && !welcome;
            if (!expected || !_handlers.Dispatch(now, message)) Drops.CountUnexpected();
        }

        private void OnWelcome(double now, WelcomeMessage m)
        {
            Status = ClientStatus.Joined;
            NetId = m.NetId;
            JoinIndex = m.JoinIndex;
            SpawnOffset = new SpawnOffset(m.SpawnOffsetX, m.SpawnOffsetY);
            _sequence = 0;
            _sentMapId = null;
            _poseMapChanged = true;
        }

        private void OnRoomClock(double now, RoomClockMessage m)
        {
            RoomClock = new RoomClockView(m);
            RoomClockChanged?.Invoke();
        }

        private void OnJoined(double now, PlayerJoinedMessage m)
        {
            if (m.NetId == NetId) return;
            var remote = new Remote(m.PlayerId, m.NetId, m.Look, _settings.InterpolationDelaySeconds);
            _remotes[m.NetId] = remote;
            if (m.HasPose) remote.Poses.Add(m.Pose.ToSample(), now);
        }

        private void OnPose(double now, PoseMessage m)
        {
            if (!_remotes.TryGetValue(m.NetId, out var remote))
            {
                Drops.CountUnexpected();
                return;
            }

            remote.Poses.Add(m.ToSample(), now);
        }

        private void OnLook(double now, LookMessage m)
        {
            if (_remotes.TryGetValue(m.NetId, out var remote)) remote.Look = m.Look ?? string.Empty;
            else Drops.CountUnexpected();
        }

        private void OnDisconnected(byte[] reasonBytes)
        {
            string reason = ClosedReason;
            if (reasonBytes != null && reasonBytes.Length > 0 && _catalog.Decode(reasonBytes, out var message) == DecodeOutcome.Ok && message is FarewellMessage farewell)
                reason = farewell.Reason;
            Close(Status == ClientStatus.Joined ? ClientStatus.Disconnected : _connected && reason != ClosedReason ? ClientStatus.Refused : ClientStatus.Disconnected, reason);
        }

        private void Close(ClientStatus status, string reason)
        {
            Status = status;
            FarewellReason = reason ?? string.Empty;
            _connected = false;
            _remotes.Clear();
        }

        private void Send(object message, Delivery delivery, double now = double.NaN)
        {
            if (!_catalog.TryEncode(message, out var bytes)) return;
            _transport.Send(_server, bytes, delivery);
            if (!double.IsNaN(now)) _lastSent = now;
        }

        private sealed class Remote
        {
            public Remote(string playerId, ushort netId, string look, double delay)
            {
                PlayerId = playerId;
                Look = look ?? string.Empty;
                Poses = new PoseBuffer(delay);
                View = new RemotePlayerView { PlayerId = playerId, NetId = netId };
            }

            public string PlayerId { get; }
            public string Look { get; set; }
            public PoseBuffer Poses { get; }
            public RemotePlayerView View { get; }
        }
    }
}
