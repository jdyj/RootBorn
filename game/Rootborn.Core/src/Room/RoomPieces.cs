using System;
using System.Collections.Generic;
using Rootborn.Core.Save;

namespace Rootborn.Core.Room
{
    /// <summary>
    /// 방이 가진 공유 조각 하나(멀티플레이 설계 4절): 이름, 지금 상태, 요청을 판정해 반영하는 함수, 저장.
    /// 방(서버)만 이것을 돌린다. 같은 요청 id를 두 번 반영하지 않는 일은 <see cref="RoomPieceSet"/>이 장부로 한다.
    /// 반영은 전부 아니면 전무다: Refused·Invalid 답이면 상태가 그대로다. 되돌리는 흐름은 없다.
    /// </summary>
    public interface IRoomPiece
    {
        string Name { get; }

        /// <summary>상태가 바뀔 때마다 오른다(모두에게 다시 보낼지 정한다).</summary>
        int Revision { get; }

        /// <param name="day">방의 지금 날(기록용).</param>
        RoomAnswer Apply(string playerId, RoomRequestSaveData request, int day);

        RoomPieceState State();

        void Load(ISaveStore store, string saveSlot);
        void Save(ISaveStore store, string saveSlot);
    }

    /// <summary>
    /// 방에 들어간 플레이어 쪽의 조각 하나. 방이 보낸 상태와, 내가 보낸 요청의 답을 받는다. 요청은 <see cref="RoomOutbox"/>에 적는다.
    /// 게임 한 판(GameSession)은 이것을 "주는 쪽"으로 받는다(지갑이면 <see cref="Inventory.IWallet"/>).
    /// </summary>
    public interface IRoomPieceClient
    {
        string Name { get; }

        void Receive(RoomPieceState state);

        /// <param name="request">답을 받은 내 요청(이미 보낼 목록에서 지웠다).</param>
        void Receive(RoomRequestSaveData request, RoomAnswer answer);

        event Action Changed;
    }

    /// <summary>
    /// 공유 조각 종류 등록 표. 조각 하나는 클래스(방 쪽과 플레이어 쪽) 하나씩과 여기 한 줄이다. 종류를 enum·switch로 닫지 않는다.
    /// 이름은 저장 파일 이름에 들어가므로 글자·숫자·'-'·'_'만 쓴다. 한 번 쓴 이름은 뜻을 바꾸지 않는다.
    /// </summary>
    public sealed class RoomPieceCatalog
    {
        private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>(8, StringComparer.Ordinal);
        private readonly List<string> _names = new List<string>(8);
        private readonly Dictionary<string, Entry> _followers = new Dictionary<string, Entry>(4, StringComparer.Ordinal);
        private readonly List<KeyValuePair<string, string>> _followerOrder = new List<KeyValuePair<string, string>>(4);

        /// <summary>이 게임의 조각들: 돈(방 지갑), 집과 가구(같이 쓰는 집), 마을 상태(월드 상태 깃발). 집을 따라 상자와 꾸밈(벽지·바닥·띠)이 켜진다.</summary>
        /// <param name="houseRules">같이 쓰는 집의 배치 판정(방 쪽만 필요 — 가구 정의와 집 맵의 칸). 없으면 방은 가구 놓기·옮기기를 거절한다.</param>
        public static RoomPieceCatalog CreateDefault(RoomHouseRules houseRules = null)
        {
            return new RoomPieceCatalog()
                .Add(RoomWallet.PieceName, () => new RoomWallet(), outbox => new RoomWalletClient(outbox))
                .Add(RoomHouse.PieceName, () => new RoomHouse(houseRules), outbox => new RoomHouseClient(outbox))
                .Add(RoomWorldFlags.PieceName, () => new RoomWorldFlags(), outbox => new RoomWorldFlagsClient(outbox))
                .Follow(RoomChests.PieceName, RoomHouse.PieceName, () => new RoomChests(houseRules), outbox => new RoomChestsClient(outbox))
                .Follow(RoomHouseBuild.PieceName, RoomHouse.PieceName, () => new RoomHouseBuild(houseRules), outbox => new RoomHouseBuildClient(outbox));
        }

        /// <summary>
        /// 따라 켜지는 조각: 방 설정에서 고르지 않고 leader를 같이 쓰면 늘 함께 켜진다(같이 쓰는 집의 상자). <see cref="Names"/>·<see cref="Knows"/>에는 없다.
        /// </summary>
        public RoomPieceCatalog Follow(string name, string leader, Func<IRoomPiece> room, Func<RoomOutbox, IRoomPieceClient> player)
        {
            if (string.IsNullOrEmpty(name) || SaveFileNames.Sanitize(name) != name) throw new ArgumentException("조각 이름은 글자·숫자·'-'·'_'만 쓴다: " + name, nameof(name));
            if (_entries.ContainsKey(name) || _followers.ContainsKey(name)) throw new ArgumentException("이미 등록한 조각: " + name, nameof(name));
            if (leader == null || !_entries.ContainsKey(leader)) throw new ArgumentException("먼저 등록한 고르는 조각이어야 한다: " + leader, nameof(leader));
            _followers.Add(name, new Entry(room ?? throw new ArgumentNullException(nameof(room)), player ?? throw new ArgumentNullException(nameof(player))));
            _followerOrder.Add(new KeyValuePair<string, string>(name, leader));
            return this;
        }

        /// <summary>방 설정이 고른 조각과, 그것을 따라 켜지는 조각(끝에).</summary>
        public IReadOnlyList<string> ActiveNames(RoomShareSettings settings)
        {
            var names = new List<string>(8);
            if (settings == null) return names;
            foreach (var name in settings.SharedPieces) names.Add(name);
            foreach (var pair in _followerOrder)
                if (settings.Shares(pair.Value)) names.Add(pair.Key);
            return names;
        }

        /// <summary>따라 켜지는 조각 이름이다.</summary>
        public bool Follows(string name) => !string.IsNullOrEmpty(name) && _followers.ContainsKey(name);

        public RoomPieceCatalog Add(string name, Func<IRoomPiece> room, Func<RoomOutbox, IRoomPieceClient> player)
        {
            if (string.IsNullOrEmpty(name) || SaveFileNames.Sanitize(name) != name) throw new ArgumentException("조각 이름은 글자·숫자·'-'·'_'만 쓴다: " + name, nameof(name));
            if (_entries.ContainsKey(name)) throw new ArgumentException("이미 등록한 조각: " + name, nameof(name));
            _entries.Add(name, new Entry(room ?? throw new ArgumentNullException(nameof(room)), player ?? throw new ArgumentNullException(nameof(player))));
            _names.Add(name);
            return this;
        }

        public IReadOnlyList<string> Names => _names;

        public bool Knows(string name) => !string.IsNullOrEmpty(name) && _entries.ContainsKey(name);

        public IRoomPiece CreateRoomPiece(string name) => Get(name).Room();

        public IRoomPieceClient CreatePlayerPiece(string name, RoomOutbox outbox) => Get(name).Player(outbox);

        private Entry Get(string name)
        {
            if (name == null || !(_entries.TryGetValue(name, out var entry) || _followers.TryGetValue(name, out entry))) throw new ArgumentException("모르는 조각: " + name, nameof(name));
            return entry;
        }

        private sealed class Entry
        {
            public Entry(Func<IRoomPiece> room, Func<RoomOutbox, IRoomPieceClient> player)
            {
                Room = room;
                Player = player;
            }

            public Func<IRoomPiece> Room { get; }
            public Func<RoomOutbox, IRoomPieceClient> Player { get; }
        }
    }

    /// <summary>
    /// 방이 가진 조각들(방 설정이 고른 것)과 요청 장부. 요청을 받으면 장부를 먼저 보고(이미 답한 id면 같은 답), 처음 온 요청만 조각이 판정한다.
    /// 요청 목록은 앞에서부터 하나씩 반영한다. 먼저 도착한 요청이 이긴다(둘이 동시에 쓰면 먼저 온 쪽만 된다).
    /// </summary>
    public sealed class RoomPieceSet
    {
        private readonly List<IRoomPiece> _pieces = new List<IRoomPiece>(4);
        private readonly Dictionary<string, IRoomPiece> _byName = new Dictionary<string, IRoomPiece>(4, StringComparer.Ordinal);
        private RoomRequestLedger _ledger = new RoomRequestLedger();

        /// <exception cref="ArgumentException">방 설정에 모르는 조각이 있다.</exception>
        public RoomPieceSet(RoomShareSettings settings, RoomPieceCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            Settings = settings ?? RoomShareSettings.Nothing;
            foreach (var name in catalog.ActiveNames(Settings))
            {
                var piece = catalog.CreateRoomPiece(name);
                _pieces.Add(piece);
                _byName[name] = piece;
            }

            foreach (var piece in _pieces)
                if (piece is IRoomPieceLinks links) links.Link(n => _byName.TryGetValue(n ?? string.Empty, out var p) ? p : null);
        }

        public RoomShareSettings Settings { get; }
        public IReadOnlyList<IRoomPiece> Pieces => _pieces;
        public RoomRequestLedger Ledger => _ledger;

        public bool TryGet(string name, out IRoomPiece piece) => _byName.TryGetValue(name ?? string.Empty, out piece);

        /// <summary>요청들을 차례로 반영한다. 답은 요청마다 하나(answers에 더한다). 상태가 바뀐 조각은 changed에 한 번씩 더한다.</summary>
        public void Apply(string playerId, IReadOnlyList<RoomRequestSaveData> requests, int day, List<RoomAnswer> answers, List<IRoomPiece> changed)
        {
            if (requests == null) return;
            foreach (var request in requests)
            {
                if (request == null || string.IsNullOrEmpty(request.RequestId)) continue;
                if (_ledger.TryGet(playerId, request.RequestId, out var known))
                {
                    answers?.Add(known);
                    continue;
                }

                RoomAnswer answer;
                if (!_byName.TryGetValue(request.Piece ?? string.Empty, out var piece))
                    answer = new RoomAnswer(request.Piece, request.RequestId, RoomOutcomes.Invalid, 0);
                else
                {
                    int before = piece.Revision;
                    answer = piece.Apply(playerId, request, day);
                    answer = new RoomAnswer(piece.Name, request.RequestId, answer.Outcome, answer.Amount);
                    if (piece.Revision != before && changed != null && !changed.Contains(piece)) changed.Add(piece);
                }

                _ledger.Record(playerId, answer);
                answers?.Add(answer);
            }
        }

        /// <summary>이미 답한 요청이면 그 답(다시 들어온 사람에게 다시 준다).</summary>
        public bool TryAnswer(string playerId, string requestId, out RoomAnswer answer) => _ledger.TryGet(playerId, requestId, out answer);

        public void Load(ISaveStore store, string saveSlot)
        {
            if (store == null) return;
            _ledger = store.TryRead(saveSlot, SaveFileNames.RoomRequestLedger, out RoomRequestLedgerSaveData ledger)
                ? RoomRequestLedger.FromSaveData(ledger)
                : new RoomRequestLedger();
            foreach (var piece in _pieces) piece.Load(store, saveSlot);
        }

        /// <summary>조각들과 장부를 한 묶음으로 쓴다(묶음을 지원하는 저장소면 도중에 꺼져도 전부 옛것 또는 전부 새것).</summary>
        public void Save(ISaveStore store, string saveSlot)
        {
            if (store == null) return;
            using (var batch = SaveBatches.Begin(store, saveSlot))
            {
                foreach (var piece in _pieces) piece.Save(batch, saveSlot);
                batch.Write(saveSlot, SaveFileNames.RoomRequestLedger, _ledger.ToSaveData());
                batch.Commit();
            }
        }
    }

    /// <summary>
    /// 플레이어 쪽의 방 조각 묶음: 방 설정, 보낼 요청 목록, 같이 쓰는 조각마다 플레이어 쪽 조각. 방 접속(Net)이 받은 상태와 답을 넘긴다.
    /// GameSession은 <see cref="Find{T}"/>로 "주는 쪽"을 찾는다(없으면 그 조각은 각자 것이다).
    /// 보낼 목록을 읽기 전에 온 답(들어오자마자 오는 것)은 갖고 있다가 목록을 읽을 때 맞춘다.
    /// </summary>
    public sealed class RoomShares
    {
        private readonly List<IRoomPieceClient> _pieces = new List<IRoomPieceClient>(4);
        private readonly Dictionary<string, IRoomPieceClient> _byName = new Dictionary<string, IRoomPieceClient>(4, StringComparer.Ordinal);
        private readonly Dictionary<string, RoomAnswer> _early = new Dictionary<string, RoomAnswer>(StringComparer.Ordinal);

        public RoomShares(RoomShareSettings settings, RoomPieceCatalog catalog, Func<string> newRequestId = null)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            Settings = settings ?? RoomShareSettings.Nothing;
            Outbox = new RoomOutbox(newRequestId);
            foreach (var name in catalog.ActiveNames(Settings))
            {
                if (!catalog.Knows(name) && !catalog.Follows(name)) continue; // 모르는 조각은 각자(방은 들어올 때 이미 같은 버전인지 봤다)
                var piece = catalog.CreatePlayerPiece(name, Outbox);
                _pieces.Add(piece);
                _byName[name] = piece;
                piece.Changed += () => Changed?.Invoke(piece.Name);
            }
        }

        public RoomShareSettings Settings { get; }
        public RoomOutbox Outbox { get; }

        /// <summary>
        /// 방이 아는 이 사람의 신원(방이 요청·잡기에 붙이는 주인 id — 접속의 신원). 방 접속이 적는다. 게임 한 판의 플레이어 id와 다를 수 있다
        /// (내 PC에서 방을 연 사람은 자기 세이브의 기본 id로 돌고 방에서는 접속의 신원이다). 모르면 null.
        /// </summary>
        public string RoomPlayerId { get; set; }
        public IReadOnlyList<IRoomPieceClient> Pieces => _pieces;

        /// <summary>조각이 바뀌었다(조각 이름). 받은 상태·답을 넘긴 뒤 부른다.</summary>
        public event Action<string> Changed;

        public T Find<T>() where T : class
        {
            foreach (var piece in _pieces)
                if (piece is T found) return found;
            return null;
        }

        public void Receive(RoomPieceState state)
        {
            if (_byName.TryGetValue(state.Piece, out var piece)) piece.Receive(state);
        }

        public void Receive(RoomAnswer answer)
        {
            var request = Take(answer.RequestId);
            if (request == null)
            {
                if (!string.IsNullOrEmpty(answer.RequestId)) _early[answer.RequestId] = answer;
                return;
            }

            if (_byName.TryGetValue(request.Piece ?? string.Empty, out var piece)) piece.Receive(request, answer);
        }

        /// <summary>세이브에서 보낼 목록을 읽는다(게임 한 판이 시작할 때). 먼저 와 있던 답을 맞춘다.</summary>
        public void LoadOutbox(RoomOutboxSaveData data)
        {
            Outbox.LoadFromSaveData(data);
            if (_early.Count == 0) return;
            var early = new List<RoomAnswer>(_early.Values);
            _early.Clear();
            foreach (var answer in early) Receive(answer);
        }

        private RoomRequestSaveData Take(string requestId)
        {
            foreach (var r in Outbox.Requests)
            {
                if (!string.Equals(r.RequestId, requestId, StringComparison.Ordinal)) continue;
                Outbox.Answered(requestId);
                return r;
            }

            return null;
        }
    }
}
