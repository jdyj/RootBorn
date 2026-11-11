using System;
using System.Collections.Generic;
using Rootborn.Core.Network;
using Rootborn.Core.Room;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;
using Rootborn.Core.Time;
using Rootborn.Net.Session;
using Rootborn.Net.Transport;

namespace Rootborn.Net.Room
{
    /// <summary>이미 있는 방의 설정(<see cref="RoomServer.TryReadSaved"/>): 같이 쓰는 조각 이름, 정원(적지 않은 옛 방이면 0).</summary>
    public sealed class SavedRoomSettings
    {
        public SavedRoomSettings(IReadOnlyList<string> sharedPieces, int maxPlayers)
        {
            SharedPieces = sharedPieces ?? Array.Empty<string>();
            MaxPlayers = maxPlayers;
        }

        public IReadOnlyList<string> SharedPieces { get; }
        public int MaxPlayers { get; }
    }

    /// <summary>방을 열 때 받는 값. 실행 인자(데디케이티드 서버) 또는 방 열기 화면·--host 인자(내 PC에서 열기)에서 넘긴다. 정원과 같이 쓰는 것은 방을 처음 만들 때만 쓰고 방 설정 파일에 남는다.</summary>
    public sealed class RoomServerSettings
    {
        /// <summary>정원의 상한(설계 10절 1번: 기본 4명, 설정으로 8명까지. 도착 자리 규칙 SpawnSpread의 한계).</summary>
        public const int MaxPlayersLimit = 8;

        /// <summary>날마다 남기는 슬롯 사본 수의 기본값(게임 한 판 GameSession.DefaultDayCopiesToKeep과 같다).</summary>
        public const int DefaultDayCopies = 3;

        public int MaxPlayers { get; set; } = 4;

        /// <summary>틱 간격(초). 받은 것을 처리하고 보낼 것을 보내는 주기. 위치를 보내는 클라이언트 횟수(초당 10번)보다 촘촘하게 둔다.</summary>
        public double TickSeconds { get; set; } = 1.0 / 30.0;

        /// <summary>연결한 뒤 이만큼 버전을 밝히지 않으면 끊는다.</summary>
        public double HandshakeTimeoutSeconds { get; set; } = 5.0;

        /// <summary>
        /// 같이 쓰는 조각 이름(Core Room RoomPieceCatalog: "wallet" 돈, "town" 마을 상태). 방을 처음 만들 때만 쓴다 — 이미 있는 방은 저장된 설정을 따른다.
        /// 비어 있으면 시간만 같이 쓴다.
        /// </summary>
        public IReadOnlyList<string> SharedPieces { get; set; } = Array.Empty<string>();

        /// <summary>날이 바뀔 때 남기는 슬롯 사본 수(방의 파일과 손님 세이브 모두). 0이면 남기지 않는다.</summary>
        public int DayCopiesToKeep { get; set; } = DefaultDayCopies;

        /// <summary>세션 쪽 설정(살아 있다는 표시, 시간 초과, 메시지 크기). MaxPlayers는 위 값으로 덮는다.</summary>
        public SessionServerSettings Session { get; set; } = new SessionServerSettings();

        public void Validate()
        {
            if (MaxPlayers < 1 || MaxPlayers > MaxPlayersLimit) throw new ArgumentException("정원은 1~" + MaxPlayersLimit + "명이다: " + MaxPlayers);
            if (TickSeconds <= 0 || HandshakeTimeoutSeconds <= 0) throw new ArgumentException("틱 간격과 접속 확인 시간 초과는 양수여야 한다");
            if (DayCopiesToKeep < 0) throw new ArgumentException("사본 수는 0 이상이다: " + DayCopiesToKeep);
            if (Session == null) throw new ArgumentException("세션 설정이 없다");
        }
    }

    /// <summary>
    /// 방이 손님(방에 들어온 플레이어)의 세이브를 갖는 곳(설계 3절·5절: 개인 진행은 각자 계산하고 저장은 방을 가진 쪽에 남는다).
    /// 내용을 검사하는 일(게임 데이터로 시작해 보기)은 구현(Rootborn.Server)이 한다 — Net은 게임 규칙을 모른다.
    /// </summary>
    public interface IGuestSaves
    {
        /// <summary>그 사람의 세이브 파일 전부(원문). 처음 온 사람이면 빈 묶음.</summary>
        SaveFileSet Read(string playerId);

        /// <summary>
        /// 받은 저장을 검사하고 쓴다. 쓰면 null과 그 저장에 든 보낼 요청(Core Room RoomOutbox)을, 쓰지 않으면 이유 코드(<see cref="GuestSaveRefusal"/>)를 준다.
        /// 쓰지 않으면 그 사람의 옛 세이브는 그대로다.
        /// </summary>
        string Accept(string playerId, SaveFileSet changes, out IReadOnlyList<RoomRequestSaveData> requests);

        /// <summary>그 사람의 세이브에 든 보낼 요청(들어올 때 이미 답한 것을 다시 알려 준다).</summary>
        IReadOnlyList<RoomRequestSaveData> PendingRequests(string playerId);

        /// <summary>날이 바뀌었다: 손님 세이브마다 그 날의 사본을 남긴다(최근 keep개).</summary>
        void KeepDayCopies(int day, int keep);
    }

    /// <summary>
    /// 방 하나(서버 쪽 논리). 데디케이티드 서버(Rootborn.Server)와, 나중에 같은 프로세스 안의 호스트 모드가 같은 이 코드를 돌린다.
    /// - 시작: 세이브 슬롯의 방 상태(방의 시계, 방 설정, 공유 조각)를 읽는다. 없으면 새 방으로 만들고 바로 써 둔다. 방 설정은 이때만 정한다.
    /// - 접속: 접속 확인 관문(<see cref="HandshakeServerTransport"/>)이 버전을 맞춰 보고, 그 뒤 세션(<see cref="SessionServer"/>)이
    ///   정원·신원을 본 다음 위치·겉모습·공용 시간을 주인으로서 중계한다.
    /// - 들어온 사람에게: 방 설정 → 그 사람의 세이브(손님 저장을 받는 방이면) → 조각 상태 → 그 사람의 세이브에 든 요청의 답 순으로 보낸다.
    /// - 손님 저장: 저장 한 번이 묶음 하나로 온다. 검사(<see cref="IGuestSaves.Accept"/>)를 지나면 쓰고, 그 저장에 든 요청을 조각에 반영한다
    ///   (저장과 요청이 한 번에 — 장부가 같은 요청 id를 한 번만 반영). 답은 보낸 사람에게, 바뀐 조각 상태는 모두에게.
    /// - 틱: <see cref="Tick"/>을 고정 간격으로 부른다(<see cref="RoomLoop"/>). 시간은 넘긴 값만 쓴다. 날이 바뀌면 슬롯 사본을 남긴다.
    ///   공용 시간은 사람이 하루를 끝내야 넘어가므로 아무도 없으면 저절로 멈춘다.
    /// - 끝: <see cref="Shutdown"/>이 방 상태를 저장하고 모두를 이유 코드(<see cref="RoomRefusal.Shutdown"/>)와 함께 끊는다.
    /// </summary>
    public sealed class RoomServer : IDisposable
    {
        private readonly ISaveStore _store;
        private readonly string _saveSlot;
        private readonly Action<string> _log;
        private readonly SessionServer _session;
        private readonly RoomServerSettings _settings;
        private readonly IGuestSaves _guests;
        private readonly RoomPieceSet _pieces;
        private readonly HashSet<string> _known = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _seen = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> _gone = new List<string>(8);
        private readonly Dictionary<string, ChunkAssembler> _uploads = new Dictionary<string, ChunkAssembler>(StringComparer.Ordinal);
        private readonly List<RoomAnswer> _answers = new List<RoomAnswer>(8);
        private readonly List<IRoomPiece> _changed = new List<IRoomPiece>(4);
        private uint _nextDownloadId = 1;
        private bool _shutDown;

        /// <param name="transport">듣는 전송(UDP 또는 같은 프로세스 안). 이 방이 닫을 때 같이 닫는다.</param>
        /// <param name="versions">이 방의 버전. 들어오는 사람의 버전이 이것과 같아야 한다.</param>
        /// <param name="store">방 상태를 읽고 쓸 저장소.</param>
        /// <param name="saveSlot">세이브 슬롯 id.</param>
        /// <param name="log">사람이 읽는 운영 로그(현지화 대상 아님). 없으면 남기지 않는다.</param>
        /// <param name="calendar">하루의 때 수와 때의 대표 시각을 주는 달력(게임 데이터의 정의). 없으면 Core 기본 달력.</param>
        /// <param name="guests">손님 세이브를 갖는 곳. 없으면 손님 저장을 받지 않는다(조각은 있어도 요청이 오지 않는다).</param>
        /// <param name="pieces">공유 조각 종류 등록 표. 없으면 Core 기본(RoomPieceCatalog.CreateDefault).</param>
        /// <exception cref="ArgumentException">저장된 방 설정에 이 프로그램이 모르는 조각이 있다(더 새 버전이 만든 방).</exception>
        public RoomServer(ITransport transport, RoomServerSettings settings, RoomVersions versions, ISaveStore store, string saveSlot, Action<string> log,
            LifeCalendar calendar = null, IGuestSaves guests = null, RoomPieceCatalog pieces = null)
        {
            if (transport == null) throw new ArgumentNullException(nameof(transport));
            settings ??= new RoomServerSettings();
            settings.Validate();
            _settings = settings;
            _store = store ?? throw new ArgumentNullException(nameof(store));
            if (string.IsNullOrWhiteSpace(saveSlot)) throw new ArgumentException("세이브 슬롯이 비어 있다", nameof(saveSlot));
            _saveSlot = saveSlot;
            _log = log;
            _guests = guests;
            Versions = versions;

            IsNewRoom = IsNew(_store, saveSlot);

            var catalog = pieces ?? RoomPieceCatalog.CreateDefault();
            var requested = new RoomShareSettings(settings.SharedPieces);
            var unknown = new List<string>();
            requested.CopyUnknown(catalog, unknown);
            if (unknown.Count > 0) throw new ArgumentException("모르는 공유 조각: " + string.Join(", ", unknown) + " (아는 것: " + string.Join(", ", catalog.Names) + ")");
            ShareSettings = RoomSettingsSaveFiles.LoadOrCreate(_store, saveSlot, IsNewRoom, requested, settings.MaxPlayers, out bool created, out int maxPlayers);
            ShareSettings.CopyUnknown(catalog, unknown);
            if (unknown.Count > 0) throw new ArgumentException("저장된 방 설정에 모르는 공유 조각이 있다: " + string.Join(", ", unknown));
            if (!created && requested.SharedPieces.Count > 0 && !requested.SameAs(ShareSettings))
                Log("방 설정은 방을 만들 때만 정한다 — 저장된 설정을 쓴다: " + ShareSettings + " (받은 값 " + requested + "은 쓰지 않음)");
            MaxPlayers = Math.Max(1, Math.Min(RoomServerSettings.MaxPlayersLimit, maxPlayers));
            if (MaxPlayers != settings.MaxPlayers)
                Log("정원은 방 설정이다 — 저장된 정원을 쓴다: " + MaxPlayers + " (받은 값 " + settings.MaxPlayers + "은 쓰지 않음)");
            _pieces = new RoomPieceSet(ShareSettings, catalog);
            _pieces.Load(_store, saveSlot);

            var gate = new HandshakeServerTransport(transport, versions, settings.HandshakeTimeoutSeconds, settings.Session.MaxMessageBytes);
            gate.Refused += (peer, reason, theirs) =>
                Log(theirs.HasValue ? $"접속 거절 {peer}: {reason} (상대 {theirs.Value}, 방 {versions})" : $"접속 거절 {peer}: {reason}");
            settings.Session.MaxPlayers = MaxPlayers;
            _session = new SessionServer(gate, settings.Session, store, saveSlot, calendar);
            _session.PlayerJoined += OnJoined;
            _session.PlayerLeft += player => _uploads.Remove(player.PlayerId);
            _session.On<SaveChunkMessage>(OnSaveChunk);
            _session.On<RoomRequestsMessage>(OnRequests);

            if (IsNewRoom)
            {
                RoomClockSaveFiles.Save(_store, _saveSlot, _session.Clock);
                _pieces.Save(_store, _saveSlot);
            }

            Log((IsNewRoom ? "새 방을 만듦" : "방을 이어서 엶") + $": 슬롯 {saveSlot}, {DayText()}, 정원 {MaxPlayers}, 같이 쓰는 것 {ShareSettings}, {versions}");
        }

        public RoomVersions Versions { get; }

        /// <summary>이 슬롯에 방 상태가 없어서 새로 만들었다.</summary>
        public bool IsNewRoom { get; }

        /// <summary>이 방이 같이 쓰는 것(만들 때 정한 것).</summary>
        public RoomShareSettings ShareSettings { get; }

        /// <summary>이 방의 정원(방 설정에 적힌 것. 적지 않은 옛 방이면 열 때 받은 값).</summary>
        public int MaxPlayers { get; }

        /// <summary>방의 시계 파일도, 옛 공용 시간 파일도 없으면 새 방이다(옛 파일만 있으면 그 날에서 잇는다 — RoomClockSaveFiles.Load).</summary>
        public static bool IsNew(ISaveStore store, string saveSlot)
            => !store.TryRead(saveSlot, SaveFileNames.RoomClock, out RoomClockSaveData _)
               && !store.TryRead(SaveFileNames.WorldTimeSlot, SaveFileNames.WorldTime(saveSlot), out WorldTimeSaveData _);

        /// <summary>
        /// 그 슬롯에 이미 방이 있으면 true와 그 방의 설정(같이 쓰는 것, 정원 — 적지 않았으면 0). 방 설정 파일보다 먼저 만든 방은 아무것도 같이 쓰지 않는다.
        /// 방을 다시 열기 전에 화면이 보이려고 읽는다. 읽을 수 없는 파일이면 저장소가 던진 예외가 그대로 나온다.
        /// </summary>
        public static bool TryReadSaved(ISaveStore store, string saveSlot, out SavedRoomSettings saved)
        {
            saved = null;
            if (store == null || string.IsNullOrWhiteSpace(saveSlot)) return false;
            if (RoomSettingsSaveFiles.TryReadSaved(store, saveSlot, out var shares, out int maxPlayers))
            {
                saved = new SavedRoomSettings(shares.SharedPieces, maxPlayers);
                return true;
            }

            if (IsNew(store, saveSlot)) return false;
            saved = new SavedRoomSettings(Array.Empty<string>(), 0);
            return true;
        }

        /// <summary>이 방의 공유 조각과 요청 장부.</summary>
        public RoomPieceSet Pieces => _pieces;

        /// <summary>방의 시계(날짜, 때, 참가자별 끝냄).</summary>
        public RoomClock Clock => _session.Clock;
        public IReadOnlyCollection<ServerPlayer> Players => _session.Players;
        public IEnumerable<ServerPlayer> PlayersOnMap(string mapId) => _session.PlayersOnMap(mapId);
        public bool IsShutDown => _shutDown;

        /// <summary>날마다 사본을 남기지 못한 마지막 오류(디스크가 참 등). 방은 계속 돈다. 없으면 null.</summary>
        public Exception DayCopyError { get; private set; }

        /// <summary>한 틱: 받은 것을 처리하고 보낼 것을 보낸다. 들어오고 나간 사람을 로그에 남긴다. 날이 바뀌면 슬롯 사본을 남긴다.</summary>
        public void Tick(double now)
        {
            if (_shutDown) return;
            int day = _session.Clock.Day;
            _session.Update(now);
            NoteArrivalsAndDepartures();
            if (_session.Clock.Day == day) return;
            Log("날이 바뀜: " + DayText());
            KeepDayCopies(_session.Clock.Day);
        }

        /// <summary>방 상태를 저장하고 모두를 끊고 전송을 닫는다. 두 번 불러도 된다.</summary>
        public void Shutdown()
        {
            if (_shutDown) return;
            RoomClockSaveFiles.Save(_store, _saveSlot, _session.Clock);
            _pieces.Save(_store, _saveSlot);
            int count = _session.Players.Count;
            _session.Dispose();
            _shutDown = true;
            Log($"저장하고 닫음: 슬롯 {_saveSlot}, {DayText()}, 끊은 사람 {count}명");
        }

        public void Dispose() => Shutdown();

        // ---------- 들어온 사람 ----------

        private void OnJoined(ServerPlayer player)
        {
            _uploads.Remove(player.PlayerId); // 연결을 바꿨으면 덜 받은 묶음은 버린다(다시 보낸다)
            _session.Send(player, new RoomSettingsMessage { SharedPieces = ToArray(ShareSettings.SharedPieces), GuestSaves = _guests != null });
            if (_guests != null)
            {
                var files = _guests.Read(player.PlayerId) ?? new SaveFileSet();
                if (!RoomTransfer.TryEncode(files, out var bytes))
                {
                    Log($"세이브를 내려 줄 수 없다(크기 한도): {player.PlayerId}");
                    RoomTransfer.TryEncode(new SaveFileSet(), out bytes);
                }

                foreach (var chunk in RoomTransfer.Split(_nextDownloadId++, bytes, _settings.Session.MaxMessageBytes)) _session.Send(player, chunk);
            }

            foreach (var piece in _pieces.Pieces) _session.Send(player, StateMessage(piece));
            if (_guests == null) return;
            foreach (var request in _guests.PendingRequests(player.PlayerId))
                if (request != null && _pieces.TryAnswer(player.PlayerId, request.RequestId, out var answer)) _session.Send(player, AnswerMessage(answer));
        }

        private void OnSaveChunk(ServerPlayer player, SaveChunkMessage chunk)
        {
            if (!_uploads.TryGetValue(player.PlayerId, out var assembler)) _uploads[player.PlayerId] = assembler = new ChunkAssembler();
            uint? failedBefore = assembler.Failed;
            if (!assembler.Add(chunk, out uint id, out var payload))
            {
                if (assembler.Failed != failedBefore && assembler.Failed == id) Refuse(player, id, GuestSaveRefusal.Malformed);
                return;
            }

            if (_guests == null)
            {
                Refuse(player, id, GuestSaveRefusal.NotAccepted);
                return;
            }

            if (!RoomTransfer.TryDecode(payload, out var changes))
            {
                Refuse(player, id, GuestSaveRefusal.Malformed);
                return;
            }

            string refusal = _guests.Accept(player.PlayerId, changes, out var requests);
            if (refusal != null)
            {
                Refuse(player, id, refusal);
                return;
            }

            _answers.Clear();
            _changed.Clear();
            _pieces.Apply(player.PlayerId, requests, _session.Clock.Day, _answers, _changed);
            if (_answers.Count > 0) _pieces.Save(_store, _saveSlot);
            _session.Send(player, new SaveTransferAnswerMessage { TransferId = id, Accepted = true });
            foreach (var answer in _answers) _session.Send(player, AnswerMessage(answer));
            foreach (var piece in _changed) _session.Broadcast(StateMessage(piece));
        }

        /// <summary>
        /// 손님 저장을 받지 않는 방: 요청을 바로 반영하고 답한다(받는 방이면 요청은 저장과 함께 와야 하므로 버린다 — 개인 진행과 한 번에 바뀌게).
        /// </summary>
        private void OnRequests(ServerPlayer player, RoomRequestsMessage message)
        {
            if (_guests != null || message?.Requests == null || message.Requests.Length == 0) return;
            ApplyAndAnswer(player, message.Requests);
        }

        private void ApplyAndAnswer(ServerPlayer player, IReadOnlyList<RoomRequestSaveData> requests)
        {
            _answers.Clear();
            _changed.Clear();
            _pieces.Apply(player.PlayerId, requests, _session.Clock.Day, _answers, _changed);
            if (_answers.Count > 0) _pieces.Save(_store, _saveSlot);
            foreach (var answer in _answers) _session.Send(player, AnswerMessage(answer));
            foreach (var piece in _changed) _session.Broadcast(StateMessage(piece));
        }

        private void Refuse(ServerPlayer player, uint id, string reason)
        {
            Log($"저장을 받지 않음: {player.PlayerId} 묶음 {id} ({reason}) — 옛 세이브를 지킨다");
            _session.Send(player, new SaveTransferAnswerMessage { TransferId = id, Accepted = false, Reason = reason });
        }

        private void KeepDayCopies(int day)
        {
            if (_settings.DayCopiesToKeep <= 0) return;
            DayCopyError = null;
            try
            {
                if (_store is ISaveSlotDayCopies copies) copies.KeepDayCopy(_saveSlot, day, _settings.DayCopiesToKeep);
                _guests?.KeepDayCopies(day, _settings.DayCopiesToKeep);
            }
            catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException)
            {
                DayCopyError = e;
                Log("날마다 남기는 사본을 쓰지 못했다(방은 계속 돈다): " + e.Message);
            }
        }

        private static RoomPieceStateMessage StateMessage(IRoomPiece piece)
        {
            var state = piece.State();
            return new RoomPieceStateMessage { Piece = state.Piece, Amount = state.Amount, Ids = state.Ids };
        }

        private static RoomAnswerMessage AnswerMessage(RoomAnswer answer)
            => new RoomAnswerMessage { Piece = answer.Piece, RequestId = answer.RequestId, Outcome = answer.Outcome, Amount = answer.Amount };

        private static string[] ToArray(IReadOnlyList<string> list)
        {
            var array = new string[list.Count];
            for (int i = 0; i < array.Length; i++) array[i] = list[i];
            return array;
        }

        private void NoteArrivalsAndDepartures()
        {
            _seen.Clear();
            foreach (var player in _session.Players)
            {
                _seen.Add(player.PlayerId);
                if (_known.Add(player.PlayerId)) Log($"들어옴: {player.PlayerId} (지금 {_session.Players.Count}명)");
            }

            if (_known.Count == _seen.Count) return;
            _gone.Clear();
            foreach (var id in _known)
                if (!_seen.Contains(id)) _gone.Add(id);
            foreach (var id in _gone)
            {
                _known.Remove(id);
                Log($"나감: {id} (지금 {_session.Players.Count}명)");
            }
        }

        private string DayText() => $"{_session.Clock.Day}일째 때 {_session.Clock.PartIndex + 1}/{_session.Clock.PartsToday}";

        private void Log(string line) => _log?.Invoke(line);
    }
}
