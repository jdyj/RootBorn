using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;
using Rootborn.Data.Save;
using Rootborn.Net.Room;
using Rootborn.Screens.Start;

namespace Rootborn.Host.Net
{
    /// <summary>
    /// 내 PC에서 방을 연다(설계 6절 "내 PC에서 열기"). 시작 화면의 방 열기 창이나 --host가 <see cref="Begin"/>을 부르면
    /// 같은 프로세스 안에 방(<see cref="HostedRoom"/> — 데디케이티드 서버와 같은 RoomServer)을 열고, 내 게임은 들어가기와 같은
    /// <see cref="RoomConnection"/>으로 같은 프로세스 안 전송을 타고 거기에 들어간다. 다른 사람은 UDP로 들어온다.
    /// 방은 Host의 게임 루프가 프레임마다 부르는 <see cref="Update"/>에서 고정 간격 틱으로 돈다(시계는 Host가 넘긴다). 방을 열지 않으면
    /// 이 객체는 프레임마다 아무것도 하지 않는다.
    /// 방의 상태(시계)는 고른 슬롯 폴더에 저장된다(방의 세이브는 내 슬롯). 내 접속이 끝나면(닫기, 끊김, 여는 중 취소) 방을 닫고
    /// 들어와 있던 사람에게 닫혔다고 알린다(<see cref="RoomRefusal.Shutdown"/>).
    /// </summary>
    public sealed class RoomHosting : IRoomHost, IDisposable
    {
        private readonly RoomConnection _connection;
        private readonly Func<ISaveStore> _store;
        private readonly IRoomClock _clock;
        private readonly Func<Rootborn.Core.Time.TimeRules> _timeRules;
        private readonly Action _closed;
        private readonly Func<string, IGuestSaves> _guestsFor;
        private readonly Func<Rootborn.Core.Room.RoomPieceCatalog> _pieces;
        private HostedRoom _room;
        private RoomJoinPhase _ownPhase = RoomJoinPhase.Idle;
        private string _ownFailure = string.Empty;
        private int _shownPlayers = -1;
        private readonly string[] _shareable;
        private IReadOnlyList<string> _defaultShares = Array.Empty<string>();

        /// <param name="connection">내 게임이 방에 들어가는 쪽(들어가기와 같은 것). 들어가면 그것이 그 슬롯으로 게임을 시작한다.</param>
        /// <param name="store">방 상태를 쓸 저장소(내 세이브 폴더).</param>
        /// <param name="clock">방의 틱을 세는 시계(Host의 네트워크 시계).</param>
        /// <param name="timeRules">방의 시계가 쓸 시간 규칙(게임 데이터의 정의 — 데디케이티드 서버와 같은 것).</param>
        /// <param name="closed">내가 방을 닫았다(<see cref="Close"/>): Host가 시작 화면으로 돌아간다.</param>
        /// <param name="guestsFor">슬롯 id → 그 방의 손님 세이브(내 슬롯 폴더 안 guests/ — 데디케이티드 서버와 같은 자리·검사). 없으면 손님 진행은 손님 PC에 남는다.</param>
        /// <param name="pieces">공유 조각 등록 표(같이 쓰는 집의 판정 칸을 넣은 것). 없으면 Core 기본.</param>
        public RoomHosting(RoomConnection connection, Func<ISaveStore> store, IRoomClock clock, Func<Rootborn.Core.Time.TimeRules> timeRules, Action closed,
            Func<string, IGuestSaves> guestsFor = null, Func<Rootborn.Core.Room.RoomPieceCatalog> pieces = null)
        {
            _guestsFor = guestsFor;
            _pieces = pieces;
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _timeRules = timeRules;
            _closed = closed;
            var names = Rootborn.Core.Room.RoomPieceCatalog.CreateDefault().Names;
            _shareable = new string[names.Count];
            for (int i = 0; i < names.Count; i++) _shareable[i] = names[i];
        }

        /// <summary>무엇이 바뀌었다(단계, 들어온 사람 수). Host가 화면 쌓기에 RoomHostText.Topic으로 넘긴다.</summary>
        public event Action Changed;

        /// <summary>열린 방(열려 있을 때만). 시험·진단용.</summary>
        public HostedRoom Room => _room;

        public RoomJoinPhase Phase => _room != null ? _connection.Phase : _ownPhase;

        public string FailureCode => _room != null ? _connection.FailureCode : _ownFailure;

        public int DefaultMaxPlayers => new RoomServerSettings().MaxPlayers;

        public int MaxPlayersLimit => RoomServerSettings.MaxPlayersLimit;

        public bool IsOpen => _room != null && _room.IsOpen && _connection.InRoom;

        public string ListenAddress => _room?.ListenAddress ?? string.Empty;

        public int PlayerCount => _room?.PlayerCount ?? 0;

        public int MaxPlayers => _room?.MaxPlayers ?? 0;

        public IReadOnlyList<string> ShareablePieces => _shareable;

        /// <summary>창을 열 때 켜 둘 것(--share). Host가 실행 인자에서 정한다.</summary>
        public IReadOnlyList<string> DefaultSharedPieces
        {
            get => _defaultShares;
            set => _defaultShares = value ?? Array.Empty<string>();
        }

        public IReadOnlyList<string> SharedPieces => _room != null ? _room.Server.ShareSettings.SharedPieces : (IReadOnlyList<string>)Array.Empty<string>();

        /// <summary>그 슬롯에 이미 있는 방의 설정(없거나 읽을 수 없으면 null — 읽을 수 없는 방은 열 때 RoomSaveUnreadable로 실패한다).</summary>
        public SavedRoom SavedRoomIn(string slotId)
        {
            if (string.IsNullOrEmpty(slotId)) return null;
            try
            {
                return HostedRoom.TryReadSaved(_store(), slotId, out SavedRoomSettings saved) ? new SavedRoom(saved.SharedPieces, saved.MaxPlayers) : null;
            }
            catch (Exception e) when (e is SaveFileCorruptException || e is SaveFileTooNewException || e is System.IO.IOException)
            {
                return null;
            }
        }

        public void Begin(string listenAddress, int maxPlayers, string slotId, IReadOnlyList<string> sharedPieces)
        {
            ShutDownRoom();
            if (!RoomAddress.TryParseListen(listenAddress, out IPAddress bind, out int port))
            {
                Fail(RoomJoinText.BadAddress);
                return;
            }

            var settings = new RoomServerSettings
            {
                MaxPlayers = Math.Max(1, Math.Min(MaxPlayersLimit, maxPlayers)),
                SharedPieces = sharedPieces ?? Array.Empty<string>(), // 방을 처음 만들 때만 쓴다(이미 있는 방은 저장된 설정)
            };
            try
            {
                // 손님의 진행은 내 슬롯 폴더 안 guests/<그 사람>/에, 내 진행은 그 슬롯 그 자체다(방을 연 사람 = 내 신원).
                _room = HostedRoom.Open(bind, port, settings, _connection.Versions, _store(), slotId, _clock, line => Console.WriteLine("[내 방] " + line), _timeRules?.Invoke(),
                    _guestsFor?.Invoke(slotId), _pieces?.Invoke(), _connection.IdentityFor(slotId));
            }
            catch (Exception e) when (e is InvalidOperationException || e is SocketException || e is ArgumentException)
            {
                Console.Error.WriteLine("[내 방] 듣지 못했다: " + e.Message);
                Fail(RoomHostText.CannotListen);
                return;
            }
            catch (Exception e) when (e is SaveFileCorruptException || e is SaveFileTooNewException)
            {
                Console.Error.WriteLine("[내 방] 방 상태를 읽을 수 없다(덮어쓰지 않는다): " + e.Message);
                Fail(RoomHostText.RoomSaveUnreadable);
                return;
            }

            _ownPhase = RoomJoinPhase.Idle;
            _ownFailure = string.Empty;
            _shownPlayers = -1;
            _connection.BeginWith(_room.ConnectLocal(), slotId, "내 PC의 방(" + _room.ListenAddress + ")");
        }

        public void Cancel()
        {
            if (Phase != RoomJoinPhase.Connecting) return;
            _connection.Cancel();
            ShutDownRoom();
            _ownPhase = RoomJoinPhase.Idle;
            Changed?.Invoke();
        }

        /// <summary>
        /// 방을 닫는다: 내 접속을 끊고, 들어와 있던 사람에게 알리고 저장한 뒤, Host가 시작 화면으로 돌아간다.
        /// 화면의 단추가 부르므로 그 자리에서 화면 쌓기를 갈아 끼우지 않게 다음 <see cref="Update"/>에서 한다.
        /// </summary>
        public void Close()
        {
            if (_room != null) _closeRequested = true;
        }

        private bool _closeRequested;

        private void CloseNow()
        {
            _closeRequested = false;
            if (_room == null) return;
            _connection.Leave();
            ShutDownRoom();
            _ownPhase = RoomJoinPhase.Idle;
            Changed?.Invoke();
            _closed?.Invoke();
        }

        /// <summary>
        /// 한 프레임(Host가 내 접속을 돌린 뒤에 부른다): 방의 틱을 돌리고, 들어온 사람 수가 바뀌었으면 알린다.
        /// 내 접속이 끝났으면(끊김, 들어가기 실패) 방을 닫는다 — 방을 연 사람이 없는 방을 남겨 두지 않는다.
        /// </summary>
        public void Update()
        {
            if (_room == null) return;
            if (_closeRequested)
            {
                CloseNow();
                return;
            }

            RoomJoinPhase mine = _connection.Phase;
            if (mine != RoomJoinPhase.Connecting && mine != RoomJoinPhase.Joined)
            {
                _ownFailure = _connection.FailureCode;
                _ownPhase = mine;
                ShutDownRoom();
                Changed?.Invoke();
                return;
            }

            _room.RunDue();
            int players = _room.PlayerCount;
            if (players == _shownPlayers) return;
            _shownPlayers = players;
            Changed?.Invoke();
        }

        /// <summary>방을 닫고 저장한다(들어와 있던 사람에게 알린다). 열려 있지 않으면 아무것도 하지 않는다. 게임을 끌 때도 부른다.</summary>
        public void ShutDownRoom()
        {
            if (_room == null) return;
            HostedRoom room = _room;
            _room = null;
            _closeRequested = false;
            room.Shutdown();
        }

        public void Dispose() => ShutDownRoom();

        private void Fail(string code)
        {
            _ownFailure = code ?? string.Empty;
            _ownPhase = RoomJoinPhase.Failed;
            Console.WriteLine("[내 방] 열지 못함: " + _ownFailure);
            Changed?.Invoke();
        }
    }
}
