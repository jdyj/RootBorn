using System;
using System.Collections.Generic;
using System.Net.Sockets;
using Rootborn.Core.World;
using Rootborn.Net.Room;
using Rootborn.Net.Session;
using Rootborn.Net.Transport;
using Rootborn.Screens.Start;

namespace Rootborn.Host.Net
{
    /// <summary>
    /// 방 하나와의 접속(Host 쪽). 시작 화면의 들어가기 창이 <see cref="Begin"/>을 부르면 UDP로 접속 확인 관문 + 세션 클라이언트를 만들고,
    /// 방이 받아들이면 <see cref="Entered"/>(Host가 그 슬롯으로 게임을 시작한다)를 부른다. 들어간 뒤에는 Host가 프레임마다
    /// <see cref="Update"/>로 자기 몸·겉모습을 넘기고 같은 맵의 남을 받는다. 끊기면 <see cref="Lost"/>로 이유 코드를 알린다.
    /// 시간은 Host가 넘긴 값(프레임 dt를 더한 것)만 쓴다. 혼자 하기에서는 이 객체가 없다(프레임마다 도는 Net 코드 없음).
    /// </summary>
    public sealed class RoomConnection : IRoomJoin, IDisposable
    {
        private static readonly IReadOnlyList<RemotePlayerView> Nobody = Array.Empty<RemotePlayerView>();

        private readonly Func<RoomVersions> _versionsSource;
        private RoomVersions? _versions;
        private readonly SessionClientSettings _settings;
        private readonly Func<string, string> _identityForSlot;
        private readonly Func<StartEntry, bool> _enter;
        private SessionClient _client;
        private RoomGuestLink _link;
        private RemoteRoomTime _time;
        private string _slotId;

        // 신원마다 방이 아직 받았다고 답하지 않은 저장. 끊겼다 같은 인물로 다시 들어가면 이어서 보낸다(설계 3.1절).
        private readonly Dictionary<string, GuestUploads> _uploads = new Dictionary<string, GuestUploads>(StringComparer.Ordinal);

        /// <param name="versions">이 프로그램의 버전(서버와 맞춰 본다). 처음 들어갈 때 한 번 부른다(데이터 지문은 모든 data/ 파일을 읽는다).</param>
        /// <param name="identityForSlot">슬롯 id → 방에서의 신원(<see cref="GuestProgress.RoomIdentity"/>).</param>
        /// <param name="enter">방이 받아들였을 때 그 슬롯으로 게임을 시작한다. 시작하지 못하면 false(그러면 방에서 나간다).</param>
        public RoomConnection(Func<RoomVersions> versions, SessionClientSettings settings, Func<string, string> identityForSlot, Func<StartEntry, bool> enter)
        {
            _versionsSource = versions ?? throw new ArgumentNullException(nameof(versions));
            _settings = settings ?? new SessionClientSettings();
            _identityForSlot = identityForSlot ?? throw new ArgumentNullException(nameof(identityForSlot));
            _enter = enter ?? throw new ArgumentNullException(nameof(enter));
        }

        public RoomJoinPhase Phase { get; private set; } = RoomJoinPhase.Idle;
        public string FailureCode { get; private set; } = string.Empty;

        /// <summary>단계가 바뀌었다(화면 쌓기에 알린다).</summary>
        public event Action PhaseChanged;

        /// <summary>들어간 뒤 방과 끊겼다(이유 코드). Host가 시작 화면으로 돌아간다.</summary>
        public event Action<string> Lost;

        /// <summary>들어가 있다(남을 그리고 자기 몸을 보낸다).</summary>
        public bool InRoom => Phase == RoomJoinPhase.Joined && _client != null;

        /// <summary>방에서의 번호(들어간 뒤). 시험·진단용.</summary>
        public ushort NetId => _client?.NetId ?? 0;

        /// <summary>방에서의 신원(접속을 시작한 뒤).</summary>
        public string PlayerId { get; private set; }

        /// <summary>
        /// 손님 저장과 공유 조각(들어간 뒤). 이 방이 손님 저장을 받으면(<see cref="RoomGuestLink.TakesGuestSaves"/>) 게임 한 판은 이것의 세이브와
        /// 조각 묶음으로 시작한다(<see cref="GuestProgress.RoomStoreFor"/>).
        /// </summary>
        public RoomGuestLink Link => _link;

        /// <summary>
        /// 이 접속의 방의 시계를 게임 한 판에 끼울 것으로 만든다(시간 규칙은 게임 데이터의 정의 — 받은 시각 사이를 메운다). 접속이 끝나면 같이 끝난다. 들어가 있지 않으면 null.
        /// </summary>
        /// <param name="sessionPlayerId">시계를 끼울 게임 한 판의 플레이어 id(세이브의 id). 없으면 방에서의 신원.</param>
        public RemoteRoomTime CreateRoomTime(Rootborn.Core.Time.TimeRules rules, string sessionPlayerId = null)
        {
            if (_client == null) return null;
            _time?.Dispose();
            _time = new RemoteRoomTime(_client, sessionPlayerId ?? PlayerId, rules);
            return _time;
        }

        public void Begin(string address, string slotId)
        {
            Close();
            FailureCode = string.Empty;
            if (!RoomAddress.TryParse(address, out string host, out int port))
            {
                Fail(RoomJoinText.BadAddress);
                return;
            }

            UdpTransport transport;
            try
            {
                transport = UdpTransport.Connect(RoomAddress.LocalBindFor(host), host, port, RoomProtocol.ConnectionKey);
            }
            catch (Exception e) when (e is InvalidOperationException || e is SocketException || e is ArgumentException)
            {
                Console.Error.WriteLine("[방] 연결을 열지 못했다: " + e.Message);
                Fail(RoomJoinText.CannotConnect);
                return;
            }

            Start(transport, slotId, host + ":" + port);
        }

        /// <summary>
        /// 이미 연 전송으로 방에 들어간다(내 PC에서 연 방: 같은 프로세스 안 전송 <see cref="HostedRoom.ConnectLocal"/>). 그 뒤는 <see cref="Begin"/>과 같다.
        /// </summary>
        /// <param name="where">로그에 남길 곳(사람이 읽는 운영 로그).</param>
        public void BeginWith(ITransport transport, string slotId, string where)
        {
            if (transport == null) throw new ArgumentNullException(nameof(transport));
            Close();
            FailureCode = string.Empty;
            Start(transport, slotId, where);
        }

        private void Start(ITransport transport, string slotId, string where)
        {
            _slotId = slotId;
            PlayerId = _identityForSlot(slotId);
            _versions ??= _versionsSource();
            _client = RoomClient.Create(transport, _versions.Value, PlayerId, string.Empty, _settings);
            if (!_uploads.TryGetValue(PlayerId, out var uploads)) _uploads[PlayerId] = uploads = new GuestUploads();
            _link = new RoomGuestLink(_client, _settings.MaxMessageBytes, uploads);
            Console.WriteLine("[방] " + where + "에 접속하는 중(신원 " + PlayerId + ", " + _versions.Value + ")");
            SetPhase(RoomJoinPhase.Connecting);
        }

        /// <summary>이 프로그램의 버전(처음 부를 때 한 번 셈한다 — 데이터 지문은 모든 data/ 파일을 읽는다). 내 PC에서 여는 방도 같은 값을 쓴다.</summary>
        public RoomVersions Versions => _versions ??= _versionsSource();

        public void Cancel()
        {
            if (Phase != RoomJoinPhase.Connecting) return;
            Close();
            SetPhase(RoomJoinPhase.Idle);
        }

        /// <summary>접속 중일 때의 한 프레임(시작 화면). 들어가면 Entered, 실패하면 Failed.</summary>
        public void UpdateJoining(double now)
        {
            if (_client == null || Phase != RoomJoinPhase.Connecting) return;
            _client.Update(now);
            switch (_client.Status)
            {
                case ClientStatus.Joined:
                    if (!_link.Ready) break; // 방 설정과(손님 저장을 받는 방이면) 그 사람의 세이브를 기다린다
                    Console.WriteLine("[방] 들어감: 번호 " + _client.NetId + (_link.TakesGuestSaves ? ", 진행은 방에 저장" : ", 진행은 이 PC에 저장"));
                    Phase = RoomJoinPhase.Joined; // Entered가 화면을 바꾸기 전에 단계를 맞춘다
                    if (!_enter(new StartEntry(_slotId, false)))
                    {
                        Close();
                        Fail(RoomJoinText.EntryFailed);
                        return;
                    }
                    PhaseChanged?.Invoke();
                    break;
                case ClientStatus.Refused:
                    Fail(_client.FarewellReason);
                    Close();
                    break;
                case ClientStatus.Disconnected:
                    // 들어가기 전에 이유 없이 닫혔으면 서버에 닿지 못한 것이다(포트가 닫혔거나 서버가 없다).
                    Fail(_client.FarewellReason == SessionClient.ClosedReason ? RoomJoinText.Unreachable : _client.FarewellReason);
                    Close();
                    break;
            }
        }

        /// <summary>
        /// 들어가 있을 때의 한 프레임: 자기 몸과 겉모습을 넘기고(보내는 횟수는 Net 설정) 받은 것을 처리한다.
        /// 끊겼으면 Lost를 부르고 false. 같은 맵의 남은 <see cref="PlayersOn"/>.
        /// </summary>
        public bool UpdateInRoom(double now, string mapId, float x, float y, Facing facing, bool walking, string look)
        {
            if (!InRoom) return false;
            _client.SetPose(mapId, x, y, facing, walking);
            _client.SetLook(look);
            _link?.FlushRequests(); // 손님 저장을 받지 않는 방: 게임 한 판이 저장까지 끝낸 공유 조각 요청을 보낸다(받는 방이면 아무것도 하지 않는다)
            _client.Update(now);
            if (_client.Status == ClientStatus.Joined) return true;
            string reason = string.IsNullOrEmpty(_client.FarewellReason) ? SessionClient.ClosedReason : _client.FarewellReason;
            Console.WriteLine("[방] 끊김: " + reason);
            Close();
            FailureCode = reason;
            Phase = RoomJoinPhase.Failed;
            Lost?.Invoke(reason);
            return false;
        }

        /// <summary>그 맵의 남(자기 제외)을 지금 시각으로 메운 것. 돌려준 목록은 다음 부를 때 다시 쓴다(새로 만들지 않는다).</summary>
        public IReadOnlyList<RemotePlayerView> PlayersOn(string mapId, double now) => InRoom ? _client.PlayersOn(mapId, now) : Nobody;

        /// <summary>방에서 나간다(끊는다고 알린다). 게임을 끌 때 부른다.</summary>
        public void Leave()
        {
            if (_client == null) return;
            Console.WriteLine("[방] 나감");
            Close();
            if (Phase != RoomJoinPhase.Failed) SetPhase(RoomJoinPhase.Idle);
        }

        public void Dispose() => Leave();

        private void Fail(string code)
        {
            FailureCode = string.IsNullOrEmpty(code) ? string.Empty : code;
            Console.WriteLine("[방] 들어가지 못함: " + FailureCode);
            SetPhase(RoomJoinPhase.Failed);
        }

        private void SetPhase(RoomJoinPhase phase)
        {
            Phase = phase;
            PhaseChanged?.Invoke();
        }

        /// <summary>전송을 닫는다. UDP 전송은 닫을 때 상대에게 끊는다고 보낸다(LiteNetLib Stop(true)) — 서버가 시간 초과를 기다리지 않는다.</summary>
        private void Close()
        {
            _time?.Dispose();
            _time = null;
            _link = null;
            _client?.Dispose();
            _client = null;
        }
    }
}
