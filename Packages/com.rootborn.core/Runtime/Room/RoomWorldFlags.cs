using System;
using System.Collections.Generic;
using Rootborn.Core.Save;

namespace Rootborn.Core.Room
{
    /// <summary>
    /// 방의 마을 상태(마을 상태 공유를 켠 방, 멀티플레이 설계 4.1절): 방 전체에 켜진 월드 상태 깃발.
    /// 요청은 <see cref="Activate"/>(Ids = 켠 깃발 id) 하나다. 깃발은 켜기만 있고(끄는 규칙이 없다) 켜기는 겹쳐도 같으므로 거절하지 않는다.
    /// 처음 켠 사람과 날을 남긴다. 어느 깃발이 마을 상태인지(범위 Shared·SaveSlot)는 플레이어 쪽이 깃발 정의로 정해 보낸다.
    /// </summary>
    public sealed class RoomWorldFlags : IRoomPiece
    {
        public const string PieceName = "town";
        public const string Activate = "activate";

        /// <summary>요청 하나에 실을 수 있는 깃발 수(깨진 요청이 방을 키우지 않게).</summary>
        public const int MaxIdsPerRequest = 256;

        private readonly SortedDictionary<string, RoomWorldFlagSaveData> _flags = new SortedDictionary<string, RoomWorldFlagSaveData>(StringComparer.Ordinal);

        public string Name => PieceName;
        public int Revision { get; private set; }
        public bool IsActive(string flagId) => !string.IsNullOrEmpty(flagId) && _flags.ContainsKey(flagId);
        public int Count => _flags.Count;

        public RoomAnswer Apply(string playerId, RoomRequestSaveData request, int day)
        {
            if (request == null || request.Kind != Activate || request.Ids == null || request.Ids.Length == 0 || request.Ids.Length > MaxIdsPerRequest)
                return new RoomAnswer(PieceName, string.Empty, RoomOutcomes.Invalid, 0);
            foreach (var id in request.Ids)
                if (string.IsNullOrEmpty(id)) return new RoomAnswer(PieceName, string.Empty, RoomOutcomes.Invalid, 0);

            bool added = false;
            foreach (var id in request.Ids)
            {
                if (_flags.ContainsKey(id)) continue;
                _flags[id] = new RoomWorldFlagSaveData { FlagId = id, PlayerId = playerId, Day = day };
                added = true;
            }

            if (added) Revision++;
            return new RoomAnswer(PieceName, string.Empty, RoomOutcomes.Applied, _flags.Count);
        }

        public RoomPieceState State()
        {
            var ids = new string[_flags.Count];
            _flags.Keys.CopyTo(ids, 0);
            return new RoomPieceState(PieceName, ids.Length, ids);
        }

        public void Load(ISaveStore store, string saveSlot)
        {
            _flags.Clear();
            if (store == null || !store.TryRead(saveSlot, SaveFileNames.RoomPiece(PieceName), out RoomWorldFlagsSaveData data) || data.Flags == null) return;
            foreach (var f in data.Flags)
                if (f != null && !string.IsNullOrEmpty(f.FlagId)) _flags[f.FlagId] = f;
        }

        public void Save(ISaveStore store, string saveSlot)
        {
            if (store == null) return;
            store.Write(saveSlot, SaveFileNames.RoomPiece(PieceName), new RoomWorldFlagsSaveData
            {
                Version = RoomWorldFlagsSaveData.KnownVersion,
                Flags = new List<RoomWorldFlagSaveData>(_flags.Values).ToArray(),
            });
        }
    }

    [Serializable]
    public sealed class RoomWorldFlagsSaveData : IVersionedSave
    {
        public const int KnownVersion = 1;

        public int Version;
        public RoomWorldFlagSaveData[] Flags = Array.Empty<RoomWorldFlagSaveData>();

        int IVersionedSave.Version { get => Version; set => Version = value; }
        int IVersionedSave.KnownVersion => KnownVersion;
    }

    [Serializable]
    public sealed class RoomWorldFlagSaveData
    {
        public string FlagId;
        public string PlayerId;
        public int Day;
    }

    /// <summary>
    /// 방의 마을 상태를 플레이어 쪽에서 비춘다. 방이 알린 깃발 목록을 갖고(<see cref="RoomFlags"/>), 내가 켠 마을 상태 깃발 중 방에 없는 것을 요청으로 적는다(<see cref="Publish"/>).
    /// 게임 한 판은 방의 깃발을 자기 월드 상태에 켜서 따른다(켜기만 있으므로 개인 쪽을 되돌릴 일이 없다).
    /// </summary>
    public sealed class RoomWorldFlagsClient : IRoomPieceClient
    {
        private readonly RoomOutbox _outbox;
        private readonly HashSet<string> _room = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> _scratch = new List<string>(8);

        public RoomWorldFlagsClient(RoomOutbox outbox)
        {
            _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
        }

        public string Name => RoomWorldFlags.PieceName;
        public IReadOnlyCollection<string> RoomFlags => _room;
        public bool Known { get; private set; }

        public event Action Changed;

        /// <summary>내가 켠 마을 상태 깃발들. 방에 없고 아직 보내지 않은 것만 요청 하나로 적는다. 적었으면 true.</summary>
        public bool Publish(IEnumerable<string> activeTownFlags)
        {
            if (activeTownFlags == null) return false;
            _scratch.Clear();
            foreach (var id in activeTownFlags)
                if (!string.IsNullOrEmpty(id) && !_room.Contains(id) && !Pending(id) && !_scratch.Contains(id)) _scratch.Add(id);
            if (_scratch.Count == 0) return false;
            for (int start = 0; start < _scratch.Count; start += RoomWorldFlags.MaxIdsPerRequest)
            {
                int count = Math.Min(RoomWorldFlags.MaxIdsPerRequest, _scratch.Count - start);
                _outbox.Add(new RoomRequestSaveData
                {
                    Piece = RoomWorldFlags.PieceName, RequestId = _outbox.NewId(), Kind = RoomWorldFlags.Activate, Ids = _scratch.GetRange(start, count).ToArray(),
                });
            }

            return true;
        }

        public void Receive(RoomPieceState state)
        {
            Known = true;
            bool changed = state.Ids.Length != _room.Count;
            if (!changed)
                foreach (var id in state.Ids)
                    if (!_room.Contains(id)) { changed = true; break; }
            if (!changed) return;
            _room.Clear();
            foreach (var id in state.Ids) if (!string.IsNullOrEmpty(id)) _room.Add(id);
            Changed?.Invoke();
        }

        public void Receive(RoomRequestSaveData request, RoomAnswer answer)
        {
        }

        private bool Pending(string id)
        {
            foreach (var r in _outbox.Requests)
                if (r.Piece == RoomWorldFlags.PieceName && r.Ids != null && Array.IndexOf(r.Ids, id) >= 0) return true;
            return false;
        }
    }
}
