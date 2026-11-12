using System;
using System.Collections.Generic;
using System.Globalization;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;
using Rootborn.Core.Save;

namespace Rootborn.Core.Room
{
    /// <summary>
    /// 같이 쓰는 집의 배치 판정에 필요한 것(방 쪽): 가구 정의와, 면 id → 그 방의 칸(집 맵에서 만든 것 — 플레이어 쪽 화면이 쓰는 것과 같은 칸).
    /// 방(서버)도 정의와 맵을 읽으므로 "같은 칸에 먼저 온 요청만"을 Core 배치 규칙(<see cref="HouseFurnitureLayout.CanPlace"/>)으로 판정한다.
    /// 칸을 모르는 면(맵을 읽지 못함)이면 놓기·옮기기를 거절한다.
    /// </summary>
    public sealed class RoomHouseRules
    {
        private readonly Dictionary<string, FurnitureDefinition> _furniture = new Dictionary<string, FurnitureDefinition>(StringComparer.Ordinal);
        private readonly List<FurnitureDefinition> _all = new List<FurnitureDefinition>();
        private readonly Func<string, IPlacementGrid> _gridFor;
        private readonly Dictionary<string, HousePlacementModel> _models = new Dictionary<string, HousePlacementModel>(StringComparer.Ordinal);

        /// <param name="gridFor">면 id → 방 칸. 모르는 면이면 null.</param>
        public RoomHouseRules(IEnumerable<FurnitureDefinition> furniture, Func<string, IPlacementGrid> gridFor)
        {
            _gridFor = gridFor ?? throw new ArgumentNullException(nameof(gridFor));
            if (furniture == null) return;
            foreach (var f in furniture)
            {
                if (f == null || _furniture.ContainsKey(f.StableId)) continue;
                _furniture.Add(f.StableId, f);
                _all.Add(f);
            }
        }

        public bool TryGetFurniture(string id, out FurnitureDefinition definition) => _furniture.TryGetValue(id ?? string.Empty, out definition);

        /// <summary>그 면의 배치를 records로 다시 채운 판(저장하지 않는다). 칸을 모르는 면이면 null.</summary>
        internal HouseFurnitureLayout Layout(string surfaceId, IReadOnlyList<FurniturePlacementSaveData> records)
        {
            if (!_models.TryGetValue(surfaceId ?? string.Empty, out var model))
            {
                var grid = _gridFor(surfaceId);
                if (grid == null) return null;
                model = new HousePlacementModel(grid);
                _models[surfaceId] = model;
            }

            var layout = new HouseFurnitureLayout(model, surfaceId);
            layout.Restore(records, _all);
            return layout;
        }
    }

    /// <summary>
    /// 같이 쓰는 집(집과 가구 공유를 켠 방, 멀티플레이 설계 4.1절). 방이 주인이고 놓인 가구 목록을 갖는다. 요청 종류(Ids의 뜻):
    /// - <see cref="Place"/>(Ids = 면, 가구 id, x, y, 방향): 놓는다. 같은 칸·그림 칸이 겹치거나 길을 막으면 거절 — 먼저 온 요청만 된다.
    ///   방이 새 인스턴스 id를 준다(놓은 사람도 남긴다).
    /// - <see cref="Move"/>(Ids = 인스턴스 id, x, y, 방향): 옮긴다. 판정은 그 가구를 뺀 나머지 위에 놓기와 같다.
    /// - <see cref="Remove"/>(Ids = 인스턴스 id): 치운다. 이미 없으면 거절(누가 먼저 치웠다).
    /// - <see cref="Clear"/>(Ids = 면): 그 면의 가구를 모두 치운다.
    /// 옮기기·치우기는 방에 있는 누구나 한다(같이 쓰는 집이다). 가구 해금·구매는 각자이고(설계 4.1절) 놓는 사람이 가진 것만 놓는다 — 그 확인은
    /// 놓는 사람 쪽 게임 한 판이 한다(해금은 그 사람의 진행이다). 해금은 놓아도 줄지 않으므로 치워도 돌려줄 물건이 없다(놓은 사람은 계속 그 가구를 가진다).
    /// 반영은 전부 아니면 전무다. 되돌리는 흐름은 없다.
    /// </summary>
    public sealed class RoomHouse : IRoomPiece
    {
        public const string PieceName = "house";
        public const string Place = "place";
        public const string Move = "move";
        public const string Remove = "remove";
        public const string Clear = "clear";

        /// <summary>상태 한 줄의 칸 구분 글자. id에 이 글자가 든 요청은 받지 않는다.</summary>
        public const char Separator = '|';

        private readonly RoomHouseRules _rules;
        private readonly List<RoomHousePlacementSaveData> _placed = new List<RoomHousePlacementSaveData>(16);
        private int _nextNumber = 1;

        /// <param name="rules">배치 판정(가구 정의, 면의 칸). 없으면 놓기·옮기기를 판정할 수 없어 거절한다(치우기는 된다).</param>
        public RoomHouse(RoomHouseRules rules = null)
        {
            _rules = rules;
        }

        public string Name => PieceName;
        public int Revision { get; private set; }
        public IReadOnlyList<RoomHousePlacementSaveData> Placed => _placed;

        public RoomAnswer Apply(string playerId, RoomRequestSaveData request, int day)
        {
            if (request == null || request.Ids == null) return Answer(RoomOutcomes.Invalid);
            foreach (var id in request.Ids)
                if (string.IsNullOrEmpty(id) || id.IndexOf(Separator) >= 0) return Answer(RoomOutcomes.Invalid);

            switch (request.Kind)
            {
                case Place:
                {
                    if (request.Ids.Length != 5 || !TryCell(request.Ids, 2, out var anchor, out var direction)) return Answer(RoomOutcomes.Invalid);
                    string surface = request.Ids[0];
                    if (_rules == null) return Answer(RoomOutcomes.Refused);
                    if (!_rules.TryGetFurniture(request.Ids[1], out var furniture)) return Answer(RoomOutcomes.Invalid);
                    var layout = _rules.Layout(surface, Records(surface, null));
                    if (layout == null || !layout.CanPlace(furniture, anchor, direction)) return Answer(RoomOutcomes.Refused);
                    _placed.Add(new RoomHousePlacementSaveData
                    {
                        InstanceId = (_nextNumber++).ToString(CultureInfo.InvariantCulture), FurnitureId = furniture.StableId, SurfaceId = surface,
                        AnchorX = anchor.X, AnchorY = anchor.Y, Direction = direction, PlacedBy = playerId ?? string.Empty, Day = day,
                    });
                    Revision++;
                    return Answer(RoomOutcomes.Applied);
                }
                case Move:
                {
                    if (request.Ids.Length != 4 || !TryCell(request.Ids, 1, out var anchor, out var direction)) return Answer(RoomOutcomes.Invalid);
                    int index = IndexOf(request.Ids[0]);
                    if (index < 0) return Answer(RoomOutcomes.Refused);
                    var placement = _placed[index];
                    if (_rules == null || !_rules.TryGetFurniture(placement.FurnitureId, out var furniture)) return Answer(RoomOutcomes.Refused);
                    var layout = _rules.Layout(placement.SurfaceId, Records(placement.SurfaceId, placement));
                    if (layout == null || !layout.CanPlace(furniture, anchor, direction)) return Answer(RoomOutcomes.Refused);
                    placement.AnchorX = anchor.X;
                    placement.AnchorY = anchor.Y;
                    placement.Direction = direction;
                    Revision++;
                    return Answer(RoomOutcomes.Applied);
                }
                case Remove:
                {
                    if (request.Ids.Length != 1) return Answer(RoomOutcomes.Invalid);
                    int index = IndexOf(request.Ids[0]);
                    if (index < 0) return Answer(RoomOutcomes.Refused);
                    _placed.RemoveAt(index);
                    Revision++;
                    return Answer(RoomOutcomes.Applied);
                }
                case Clear:
                {
                    if (request.Ids.Length != 1) return Answer(RoomOutcomes.Invalid);
                    if (_placed.RemoveAll(p => p.SurfaceId == request.Ids[0]) > 0) Revision++;
                    return Answer(RoomOutcomes.Applied);
                }
                default:
                    return Answer(RoomOutcomes.Invalid);
            }
        }

        /// <summary>상태: 놓인 가구마다 한 줄(<see cref="RoomHousePlacement.Encode"/>), Amount는 그 수. 놓은 사람은 방의 세이브에만 둔다.</summary>
        public RoomPieceState State()
        {
            var ids = new string[_placed.Count];
            for (int i = 0; i < ids.Length; i++) ids[i] = RoomHousePlacement.From(_placed[i]).Encode();
            return new RoomPieceState(PieceName, ids.Length, ids);
        }

        public void Load(ISaveStore store, string saveSlot)
        {
            _placed.Clear();
            _nextNumber = 1;
            if (store == null || !store.TryRead(saveSlot, SaveFileNames.RoomPiece(PieceName), out RoomHouseSaveData data)) return;
            _nextNumber = Math.Max(1, data.NextNumber);
            if (data.Placed == null) return;
            foreach (var p in data.Placed)
                if (p != null && !string.IsNullOrEmpty(p.InstanceId) && IndexOf(p.InstanceId) < 0) _placed.Add(p);
        }

        public void Save(ISaveStore store, string saveSlot)
        {
            if (store == null) return;
            store.Write(saveSlot, SaveFileNames.RoomPiece(PieceName), new RoomHouseSaveData
            {
                Version = RoomHouseSaveData.KnownVersion,
                NextNumber = _nextNumber,
                Placed = _placed.ToArray(),
            });
        }

        // 그 면의 배치 기록(skip은 뺀다 — 옮길 때 자기 자리를 비운다).
        private List<FurniturePlacementSaveData> Records(string surface, RoomHousePlacementSaveData skip)
        {
            var records = new List<FurniturePlacementSaveData>(_placed.Count);
            foreach (var p in _placed)
                if (p != skip && p.SurfaceId == surface) records.Add(p.ToRecord());
            return records;
        }

        private int IndexOf(string instanceId)
        {
            for (int i = 0; i < _placed.Count; i++)
                if (string.Equals(_placed[i].InstanceId, instanceId, StringComparison.Ordinal)) return i;
            return -1;
        }

        private static bool TryCell(string[] ids, int start, out GridPoint anchor, out FurniturePlacementDirection direction)
        {
            anchor = default;
            direction = FurniturePlacementDirection.North;
            if (!int.TryParse(ids[start], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int x)
                || !int.TryParse(ids[start + 1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int y)
                || !int.TryParse(ids[start + 2], NumberStyles.None, CultureInfo.InvariantCulture, out int d)
                || !Enum.IsDefined(typeof(FurniturePlacementDirection), d)) return false;
            anchor = new GridPoint(x, y);
            direction = (FurniturePlacementDirection)d;
            return true;
        }

        private static RoomAnswer Answer(string outcome) => new RoomAnswer(PieceName, string.Empty, outcome, 0);
    }

    [Serializable]
    public sealed class RoomHouseSaveData : IVersionedSave
    {
        public const int KnownVersion = 1;

        public int Version;
        public int NextNumber = 1;
        public RoomHousePlacementSaveData[] Placed = Array.Empty<RoomHousePlacementSaveData>();

        int IVersionedSave.Version { get => Version; set => Version = value; }
        int IVersionedSave.KnownVersion => KnownVersion;
    }

    /// <summary>같이 쓰는 집에 놓인 가구 하나: 방이 준 인스턴스 id, 가구 id, 면, 자리, 방향, 놓은 사람, 놓은 날(방의 날).</summary>
    [Serializable]
    public sealed class RoomHousePlacementSaveData
    {
        public string InstanceId = string.Empty;
        public string FurnitureId = string.Empty;
        public string SurfaceId = string.Empty;
        public int AnchorX;
        public int AnchorY;
        public FurniturePlacementDirection Direction;
        public string PlacedBy = string.Empty;
        public int Day;

        internal FurniturePlacementSaveData ToRecord() => new FurniturePlacementSaveData
        {
            FurnitureId = FurnitureId, SurfaceId = SurfaceId, AnchorX = AnchorX, AnchorY = AnchorY, Direction = Direction,
        };
    }

    /// <summary>방이 알린 같이 쓰는 집의 가구 하나(플레이어 쪽). 상태 한 줄 "인스턴스|가구|면|x|y|방향"으로 오간다.</summary>
    public readonly struct RoomHousePlacement
    {
        public RoomHousePlacement(string instanceId, string furnitureId, string surfaceId, GridPoint anchor, FurniturePlacementDirection direction)
        {
            InstanceId = instanceId ?? string.Empty;
            FurnitureId = furnitureId ?? string.Empty;
            SurfaceId = surfaceId ?? string.Empty;
            Anchor = anchor;
            Direction = direction;
        }

        public string InstanceId { get; }
        public string FurnitureId { get; }
        public string SurfaceId { get; }
        public GridPoint Anchor { get; }
        public FurniturePlacementDirection Direction { get; }

        internal static RoomHousePlacement From(RoomHousePlacementSaveData p)
            => new RoomHousePlacement(p.InstanceId, p.FurnitureId, p.SurfaceId, new GridPoint(p.AnchorX, p.AnchorY), p.Direction);

        public FurniturePlacementSaveData ToRecord() => new FurniturePlacementSaveData
        {
            FurnitureId = FurnitureId, SurfaceId = SurfaceId, AnchorX = Anchor.X, AnchorY = Anchor.Y, Direction = Direction,
        };

        public string Encode()
            => string.Join(RoomHouse.Separator.ToString(), InstanceId, FurnitureId, SurfaceId,
                Anchor.X.ToString(CultureInfo.InvariantCulture), Anchor.Y.ToString(CultureInfo.InvariantCulture), ((int)Direction).ToString(CultureInfo.InvariantCulture));

        public static bool TryDecode(string line, out RoomHousePlacement placement)
        {
            placement = default;
            if (string.IsNullOrEmpty(line)) return false;
            var parts = line.Split(RoomHouse.Separator);
            if (parts.Length != 6 || parts[0].Length == 0 || parts[1].Length == 0) return false;
            if (!int.TryParse(parts[3], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int x)
                || !int.TryParse(parts[4], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int y)
                || !int.TryParse(parts[5], NumberStyles.None, CultureInfo.InvariantCulture, out int d)
                || !Enum.IsDefined(typeof(FurniturePlacementDirection), d)) return false;
            placement = new RoomHousePlacement(parts[0], parts[1], parts[2], new GridPoint(x, y), (FurniturePlacementDirection)d);
            return true;
        }
    }

    /// <summary>
    /// 같이 쓰는 집을 플레이어 쪽에서 비춘다. 방이 알린 가구 목록(<see cref="Placements"/>)을 갖고, 놓기·옮기기·치우기를 요청으로 적는다.
    /// 게임 한 판은 요청만 적고 자기 배치를 바꾸지 않는다 — 방이 허락하면 바뀐 상태가 오고, 그것으로 집을 다시 채운다(되돌리는 흐름 없음).
    /// 답은 <see cref="Answered"/>(요청, 반영됐는지)로 알린다.
    /// </summary>
    public sealed class RoomHouseClient : IRoomPieceClient
    {
        private readonly RoomOutbox _outbox;
        private readonly List<RoomHousePlacement> _placements = new List<RoomHousePlacement>(16);
        private readonly List<string> _lines = new List<string>(16);

        public RoomHouseClient(RoomOutbox outbox)
        {
            _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
        }

        public string Name => RoomHouse.PieceName;

        /// <summary>방이 집 상태를 알렸다(들어온 뒤 첫 상태를 받았다).</summary>
        public bool Known { get; private set; }

        public IReadOnlyList<RoomHousePlacement> Placements => _placements;

        public event Action Changed;

        /// <summary>내 요청의 답이 왔다(그 요청, 반영됐는지). 반영된 배치는 뒤따르는 상태로 온다.</summary>
        public event Action<RoomRequestSaveData, bool> Answered;

        /// <summary>답을 기다리는 집 요청이 있다.</summary>
        public bool HasPending
        {
            get
            {
                foreach (var r in _outbox.Requests) if (r.Piece == RoomHouse.PieceName) return true;
                return false;
            }
        }

        /// <summary>그 면의 기록과 각 기록의 인스턴스 id(같은 순서).</summary>
        public void CopySurface(string surfaceId, List<FurniturePlacementSaveData> records, List<string> instanceIds)
        {
            records.Clear();
            instanceIds.Clear();
            foreach (var p in _placements)
            {
                if (p.SurfaceId != surfaceId) continue;
                records.Add(p.ToRecord());
                instanceIds.Add(p.InstanceId);
            }
        }

        public string RequestPlace(string surfaceId, string furnitureId, GridPoint anchor, FurniturePlacementDirection direction)
            => Add(RoomHouse.Place, surfaceId, furnitureId, Num(anchor.X), Num(anchor.Y), Num((int)direction));

        public string RequestMove(string instanceId, GridPoint anchor, FurniturePlacementDirection direction)
            => Add(RoomHouse.Move, instanceId, Num(anchor.X), Num(anchor.Y), Num((int)direction));

        public string RequestRemove(string instanceId) => Add(RoomHouse.Remove, instanceId);

        public string RequestClear(string surfaceId) => Add(RoomHouse.Clear, surfaceId);

        public void Receive(RoomPieceState state)
        {
            bool first = !Known;
            Known = true;
            bool same = state.Ids.Length == _lines.Count;
            for (int i = 0; same && i < state.Ids.Length; i++) same = string.Equals(state.Ids[i], _lines[i], StringComparison.Ordinal);
            if (same && !first) return;
            _lines.Clear();
            _placements.Clear();
            foreach (var line in state.Ids)
            {
                if (!RoomHousePlacement.TryDecode(line, out var placement)) continue;
                _lines.Add(line);
                _placements.Add(placement);
            }

            Changed?.Invoke();
        }

        public void Receive(RoomRequestSaveData request, RoomAnswer answer) => Answered?.Invoke(request, answer.Outcome == RoomOutcomes.Applied);

        private string Add(string kind, params string[] ids)
        {
            foreach (var id in ids)
                if (string.IsNullOrEmpty(id) || id.IndexOf(RoomHouse.Separator) >= 0) throw new ArgumentException("집 요청의 id가 비었거나 구분 글자가 들었다: " + id);
            string requestId = _outbox.NewId();
            _outbox.Add(new RoomRequestSaveData { Piece = RoomHouse.PieceName, RequestId = requestId, Kind = kind, Ids = ids });
            return requestId;
        }

        private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
