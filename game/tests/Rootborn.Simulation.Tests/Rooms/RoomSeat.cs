using System;
using System.Collections.Generic;
using System.Globalization;
using Rootborn.Core.Room;
using Rootborn.Data.Save;
using Rootborn.Game;
using Rootborn.Net.Room;
using Rootborn.Net.Session;
using Rootborn.Net.Transport;
using Rootborn.Simulation.Headless;

namespace Rootborn.Simulation.Rooms
{
    /// <summary>
    /// 방의 자리 하나: 자동 플레이어 한 명의 창 없는 몸과 그 사람의 방 접속(Host RoomConnection + GuestProgress.RoomStoreFor + HostGame.StartSession과 같은 순서).
    /// - 접속: 같은 프로세스 안 전송으로 접속 확인 관문 + 세션 클라이언트 + 손님 저장 링크. 방이 받았다고 답하지 않은 저장(GuestUploads)은 접속보다 오래 산다
    ///   (끊겼다 같은 사람으로 다시 들어가면 이어서 보낸다 — Host가 신원마다 들고 있는 것과 같다).
    /// - 게임 한 판: 방이 내려 준 세이브의 메모리 저장소(저장하면 방으로 보낸다), 방의 시계(RemoteRoomTime — 바뀌면 SyncWithRoom), 공유 조각.
    /// - 프레임마다(몸이 부른다): 자기 자리·저장한 요청 보내기·받기(Host UpdateInRoom). 방의 틱과 다른 사람의 프레임은 <see cref="RoomPlay"/>가 돈다.
    /// 끊기면 몸은 다음 걸음 앞에 시작 화면으로 돌아가고(Host: 끊김 → 시작 화면), 다시 이으면 슬롯을 골라 다시 들어간다.
    /// </summary>
    public sealed class RoomSeat : IHeadlessSeat, ISavedGameSource
    {
        private readonly RoomPlay _play;
        private readonly GuestUploads _uploads = new GuestUploads();
        private LoopbackTransport _line;
        private SessionClient _client;
        private RoomGuestLink _link;
        private RemoteRoomTime _time;
        private int _requestCounter;

        public RoomSeat(RoomPlay play, int index, string playerId, IPlayPolicy policy)
        {
            _play = play;
            Index = index;
            PlayerId = playerId;
            Policy = policy;
        }

        public int Index { get; }
        public string PlayerId { get; }
        public IPlayPolicy Policy { get; }
        public HeadlessBody Body { get; internal set; }
        public PlayRunner Runner { get; internal set; }

        /// <summary>일부러 방을 떠나 있다(쉰 날 확인 — 다음 날 다시 들어온다).</summary>
        public bool Away { get; internal set; }

        /// <summary>세션 클라이언트가 방에 들어가 있다.</summary>
        public bool Joined => _client != null && _client.Status == ClientStatus.Joined;

        /// <summary>
        /// 지금 게임 한 판이 쓰는 접속이 살아 있다(몸이 본다 — 아니면 시작 화면으로). 끊겼다 다시 이었으면 새 접속이라도 그 판의 시계·조각은 옛 접속의 것이라 false.
        /// </summary>
        public bool Connected => Joined && _sessionLink == _link;

        /// <summary>방에 들어가 방 설정과 그 사람의 세이브를 받았다(게임 한 판을 시작할 수 있다).</summary>
        public bool Ready => Joined && _link != null && _link.Ready;

        private RoomGuestLink _sessionLink;

        public RoomGuestLink Link => _link;
        public GuestUploads Uploads => _uploads;
        public SessionClient Client => _client;

        /// <summary>지금 게임 한 판(시작 화면이면 null).</summary>
        public GameSession Session => Body?.Session;

        /// <summary>방에 들어가 걷고 있다(그 판의 접속이 살아 있고 방의 시계를 안다).</summary>
        public bool Playing => Session != null && Body.InWorld && Connected && Session.Room != null && Session.Room.Known;

        /// <summary>게임 한 판이 시작될 때(방의 불변식이 그 판의 답을 듣는다).</summary>
        public event Action<RoomSeat, GameSession> SessionStarted;

        internal void Connect(LoopbackNetwork network)
        {
            Drop();
            _line = network.Connect();
            var settings = new SessionClientSettings();
            _client = RoomClient.Create(_line, RoomPlay.Versions, PlayerId, "child|", settings);
            _link = new RoomGuestLink(_client, settings.MaxMessageBytes, _uploads, null,
                () => PlayerId + "-req-" + (++_requestCounter).ToString(CultureInfo.InvariantCulture));
            _link.UploadRefused += reason => UploadRefused?.Invoke(this, reason);
        }

        /// <summary>방이 이 사람의 저장을 거절했다(이유 코드). 그 저장의 진행은 방에 남지 않는다.</summary>
        public event Action<RoomSeat, string> UploadRefused;

        /// <summary>선을 끊는다(이후 바이트를 양쪽 모두 알림 없이 잃는다)와 함께 이쪽 접속을 버린다 — 방은 시간 초과나 같은 사람의 새 접속으로 안다.</summary>
        internal void Sever(LoopbackNetwork network)
        {
            if (_line != null) network.Sever(_line);
            Drop();
        }

        /// <summary>방에서 나간다(끊는다고 알린다).</summary>
        internal void Leave() => Drop();

        private void Drop()
        {
            _time?.Dispose();
            _time = null;
            _link = null;
            _client?.Dispose();
            _client = null;
            _line = null;
        }

        /// <summary>마지막으로 시작한 판이 세이브에서 읽은 가방(아이템 id → 수).</summary>
        public Dictionary<string, int> BagAtLoad { get; private set; }

        public GameSession StartSession(string slotId, Func<string, string> newEventKey)
        {
            if (!Ready) return null;
            var definitions = _play.Atlas.Definitions;
            // 세이브에 든 가방(판이 시작되며 붙기 전에 온 상자 답을 반영하기 전) — 방의 불변식이 시작 도중의 가방 변화를 그 답의 몫으로 센다.
            var loaded = GameSession.Start(definitions, GameStartSettings.FromDefinitions(definitions), new MemorySaveSlot(RoomGuestLink.Slot, _link.Files.Writes),
                RoomGuestLink.Slot, PlayerId);
            BagAtLoad = RoomChecks.Bag(loaded);
            var store = new MemorySaveSlot(RoomGuestLink.Slot, _link.Files.Writes);
            var link = _link;
            store.Committed += c => link.Upload(SaveFileSet.From(c.Writes, c.Deletes));
            _time = new RemoteRoomTime(_client, PlayerId, definitions.TimeRules);
            var session = GameSession.Start(definitions, GameStartSettings.FromDefinitions(definitions), store, RoomGuestLink.Slot, PlayerId, newEventKey,
                room: _time, shares: _link.Shares);
            _time.Changed += () => session.SyncWithRoom();
            _sessionLink = _link;
            SessionStarted?.Invoke(this, session);
            return session;
        }

        public void AfterFrame(HeadlessBody body) => _play.AfterFrame(body);

        /// <summary>한 프레임의 접속 일(Host UpdateInRoom): 자기 자리와 저장한 요청을 보내고 받은 것을 처리한다.</summary>
        internal void Pump(double now)
        {
            if (_client == null) return;
            if (Body != null && Body.InWorld && Body.Session != null && _client.Status == ClientStatus.Joined)
            {
                var walker = Body.Walk.Walker;
                _client.SetPose(Body.Walk.Map.Id, walker.X, walker.Y, walker.Facing, walker.Moving);
                _link?.FlushRequests();
            }
            _client.Update(now);
        }

        /// <summary>방이 가진 이 사람의 세이브(+ 아직 방이 받았다고 답하지 않은 저장)로 새 게임 한 판 — 다시 들어오면 보일 진행. 방의 시계·조각 없이.</summary>
        public GameSession ReloadSaved()
        {
            SaveFileSet files = _play.Guests.Read(PlayerId);
            files.Overlay(_uploads.Merged());
            var definitions = _play.Atlas.Definitions;
            var store = new MemorySaveSlot(RoomGuestLink.Slot, files.Writes);
            return GameSession.Start(definitions, GameStartSettings.FromDefinitions(definitions), store, RoomGuestLink.Slot, PlayerId);
        }

        public override string ToString() => PlayerId + "(" + Policy.Name + ")";
    }
}
