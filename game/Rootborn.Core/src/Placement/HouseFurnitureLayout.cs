using System;
using System.Collections.Generic;
using Rootborn.Core.Minigames;

namespace Rootborn.Core.Placement
{
    /// <summary>놓을 가구 하나(템플릿의 한 줄). 한꺼번에 바꾸기가 받는다.</summary>
    public readonly struct FurniturePlacementRequest
    {
        public FurniturePlacementRequest(FurnitureDefinition definition, GridPoint anchor, FurniturePlacementDirection direction)
        {
            Definition = definition;
            Anchor = anchor;
            Direction = direction;
        }

        public FurnitureDefinition Definition { get; }
        public GridPoint Anchor { get; }
        public FurniturePlacementDirection Direction { get; }
    }

    /// <summary>방에 놓인 가구 하나.</summary>
    public sealed class PlacedFurniture
    {
        internal PlacedFurniture(string instanceId, FurnitureDefinition definition, FurniturePlacementSaveData record)
        {
            InstanceId = instanceId;
            Definition = definition;
            Record = record;
            var anchor = new GridPoint(record.AnchorX, record.AnchorY);
            OccupiedCells = HouseFurnitureLayout.Cells(anchor, definition.GetFootprintCells(record.Direction));
            var parts = definition.GetTileParts(record.Direction);
            var tileCells = new GridPoint[parts.Count];
            for (int i = 0; i < tileCells.Length; i++) tileCells[i] = new GridPoint(anchor.X + parts[i].LocalCell.X, anchor.Y + parts[i].LocalCell.Y);
            TileCells = tileCells;
        }

        public string InstanceId { get; }
        public FurnitureDefinition Definition { get; }
        public GridPoint Anchor => new GridPoint(Record.AnchorX, Record.AnchorY);
        public FurniturePlacementDirection Direction => Record.Direction;

        /// <summary>가구가 차지하는 칸(다른 가구를 놓을 수 없다).</summary>
        public IReadOnlyList<GridPoint> OccupiedCells { get; }

        /// <summary>가구 그림이 그려지는 칸(다른 가구의 그림과 겹칠 수 없다).</summary>
        public IReadOnlyList<GridPoint> TileCells { get; }

        /// <summary>상자이고 물건이 들었다(치울 수 없다).</summary>
        public bool HoldsItems => FurnitureStorageSaveData.HoldsItems(Record.Storage);

        internal FurniturePlacementSaveData Record { get; }
    }

    /// <summary>
    /// 보관한 가구 하나: 정의가 있는데 칸이 막혀(집 맵이 바뀌어 벽이 된 칸 등) 놓지 못했고 가까운 자리도 없던 기록. 기록은 원래 기준 칸 그대로 저장된다.
    /// <see cref="HouseFurnitureLayout.TryPlaceStored"/>로 새 자리에 다시 놓는다.
    /// </summary>
    public sealed class StoredFurniture
    {
        internal StoredFurniture(int index, FurnitureDefinition definition, FurniturePlacementSaveData record)
        {
            Index = index;
            Definition = definition;
            Record = record;
        }

        /// <summary>읽은 기록 목록 안의 자리(<see cref="HouseFurnitureLayout.RestoreRelocating"/>에 넘긴 순서). 다시 읽을 때까지 바뀌지 않는다.</summary>
        public int Index { get; }

        public FurnitureDefinition Definition { get; }

        /// <summary>기록에 저장된 방향.</summary>
        public FurniturePlacementDirection Direction => Record.Direction;

        /// <summary>상자이고 물건이 들었다. 다시 놓으면 내용물째 간다.</summary>
        public bool HoldsItems => FurnitureStorageSaveData.HoldsItems(Record.Storage);

        internal FurniturePlacementSaveData Record { get; }
    }

    /// <summary>옮기며 다시 채운 결과: 가까운 자리로 옮긴 가구 수와 보관한 가구 수.</summary>
    public sealed class HouseFurnitureRelocation
    {
        internal HouseFurnitureRelocation(int moved, int stored)
        {
            Moved = moved;
            Stored = stored;
        }

        public int Moved { get; }
        public int Stored { get; }
    }

    /// <summary>
    /// 집 한 단계의 가구 배치(원본 AuthoredHouseFurnitureSession에서 타일맵을 뺀 것).
    /// 바꾸기는 먼저 바뀐 뒤의 기록 전체를 commit에 넘기고, commit이 성공해야만 상태를 바꾼다.
    /// 되살리지 못한 기록은 버리지 않고 함께 저장한다. 화면은 <see cref="Placed"/>와 <see cref="BlockingCells"/>로 그린다.
    /// </summary>
    public sealed class HouseFurnitureLayout
    {
        private readonly HousePlacementModel _model;
        private readonly Func<FurniturePlacementSaveData[], bool> _commit;
        private readonly List<PlacedFurniture> _placed = new List<PlacedFurniture>();
        private readonly List<FurniturePlacementSaveData> _unresolved = new List<FurniturePlacementSaveData>();
        private readonly List<StoredFurniture> _stored = new List<StoredFurniture>();
        private readonly string[] _acceptedSurfaceIds;
        private int _nextInstanceNumber;

        /// <param name="commit">바뀐 뒤의 기록 전체를 저장한다. false면 바꾸지 않는다. null이면 늘 성공으로 본다.</param>
        /// <param name="acceptedSurfaceIds">
        /// 이 면에서도 받는 다른 면의 가구(예: 일터가 "house" 가구를 받는다). 가구가 <see cref="SurfaceId"/>나 이 중 하나를 허용하면 놓는다.
        /// 기록의 면은 늘 <see cref="SurfaceId"/>다.
        /// </param>
        public HouseFurnitureLayout(HousePlacementModel model, string surfaceId, Func<FurniturePlacementSaveData[], bool> commit = null, IReadOnlyList<string> acceptedSurfaceIds = null)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            SurfaceId = surfaceId ?? string.Empty;
            _commit = commit;
            _acceptedSurfaceIds = Copy(acceptedSurfaceIds);
        }

        /// <summary>가구를 새로 놓은 뒤 한 번. 퀘스트의 가구 배치 이벤트를 여기서 낸다.</summary>
        public event Action<FurnitureDefinition> PlacementCommitted;

        public string SurfaceId { get; }
        public IReadOnlyList<PlacedFurniture> Placed => _placed;
        public int UnresolvedCount => _unresolved.Count;

        /// <summary>
        /// 못 놓은 기록 중 정의가 있고 칸이 막혀(<see cref="FurnitureReplayFailure.Blocked"/>·<see cref="FurnitureReplayFailure.DuplicateFootprint"/>) 못 놓은 것(기록 순서).
        /// 데이터에 없는 가구, 다른 면, 방향이 잘못된 기록은 들지 않는다(그것들도 버리지 않고 함께 저장한다).
        /// </summary>
        public IReadOnlyList<StoredFurniture> Stored => _stored;

        /// <summary>이동을 막는 가구가 차지한 칸. 화면의 충돌 칸이 된다.</summary>
        public IReadOnlyCollection<GridPoint> BlockingCells
        {
            get
            {
                var cells = new HashSet<GridPoint>();
                for (int i = 0; i < _placed.Count; i++)
                    if (_placed[i].Definition.BlocksMovement) cells.UnionWith(_placed[i].OccupiedCells);
                return cells;
            }
        }

        /// <summary>저장된 기록으로 방을 다시 채운다. 놓을 수 없는 기록은 남겨 둔다. 저장하지 않는다.</summary>
        /// <param name="instanceIds">기록마다(같은 순서) 쓸 인스턴스 id(같이 쓰는 집: 방이 준 id). 없으면 여기서 만든다.</param>
        public void Restore(IReadOnlyList<FurniturePlacementSaveData> records, IEnumerable<FurnitureDefinition> catalog, IReadOnlyList<string> instanceIds = null)
        {
            CheckIds(records, instanceIds);
            Apply(FurnitureReplay.Plan(_model, SurfaceId, records, catalog, _acceptedSurfaceIds), instanceIds);
        }

        /// <summary>
        /// <see cref="Restore"/>와 같되, 칸이 막혀 못 놓은 기록은 가까운 자리로 옮겨 놓는다(<see cref="FurnitureReplay.PlanRelocating"/>, 집 맵을 바꾼 뒤의 옛 가구).
        /// 그래도 못 놓은 것은 <see cref="Stored"/>에 든다. 저장하지 않는다 — 옮긴 자리는 다음에 무엇을 바꿀 때 함께 저장되고, 그 전에는 읽을 때마다 같은 자리가 나온다.
        /// </summary>
        public HouseFurnitureRelocation RestoreRelocating(IReadOnlyList<FurniturePlacementSaveData> records, IEnumerable<FurnitureDefinition> catalog, IReadOnlyList<string> instanceIds = null)
        {
            CheckIds(records, instanceIds);
            var plan = FurnitureReplay.PlanRelocating(_model, SurfaceId, records, catalog, _acceptedSurfaceIds);
            Apply(plan, instanceIds);
            int moved = 0;
            for (int i = 0; i < plan.Accepted.Count; i++) if (plan.Accepted[i].Moved) moved++;
            return new HouseFurnitureRelocation(moved, _stored.Count);
        }

        /// <summary>보관한 가구(<see cref="StoredFurniture.Index"/>)를 그 자리·방향에 놓을 수 있는가(놓인 가구 위에 하나 더 놓기와 같은 판정).</summary>
        public bool CanPlaceStored(int index, GridPoint anchor, FurniturePlacementDirection direction)
        {
            var stored = FindStored(index);
            return stored != null && Fits(stored.Definition, anchor, direction, _placed);
        }

        /// <summary>
        /// 보관한 가구를 새 자리·방향에 놓는다: 그 기록(상태·그림 이름·상자 내용물 그대로)을 못 놓은 목록에서 빼 놓인 가구 끝에 넣고 전체를 한 번 저장한다.
        /// 맞지 않거나 저장이 실패하면 아무것도 바꾸지 않는다. 이미 놓은 index면 false다. <see cref="PlacementCommitted"/>는 내지 않는다(새 가구가 아니다).
        /// </summary>
        public bool TryPlaceStored(int index, GridPoint anchor, FurniturePlacementDirection direction)
        {
            var stored = FindStored(index);
            if (stored == null || !Fits(stored.Definition, anchor, direction, _placed)) return false;
            var record = stored.Record.Clone();
            record.AnchorX = anchor.X;
            record.AnchorY = anchor.Y;
            record.Direction = direction;

            var next = new List<FurniturePlacementSaveData>(_placed.Count + _unresolved.Count);
            for (int i = 0; i < _placed.Count; i++) next.Add(_placed[i].Record);
            next.Add(record);
            for (int i = 0; i < _unresolved.Count; i++) if (!ReferenceEquals(_unresolved[i], stored.Record)) next.Add(_unresolved[i]);
            if (!Commit(next)) return false;

            _unresolved.Remove(stored.Record);
            _stored.Remove(stored);
            _placed.Add(new PlacedFurniture(NextInstanceId(stored.Definition.StableId), stored.Definition, record));
            return true;
        }

        private StoredFurniture FindStored(int index)
        {
            for (int i = 0; i < _stored.Count; i++) if (_stored[i].Index == index) return _stored[i];
            return null;
        }

        private static void CheckIds(IReadOnlyList<FurniturePlacementSaveData> records, IReadOnlyList<string> instanceIds)
        {
            if (instanceIds != null && (records == null || instanceIds.Count != records.Count)) throw new ArgumentException("인스턴스 id는 기록마다 하나다", nameof(instanceIds));
        }

        private void Apply(FurnitureReplayPlan plan, IReadOnlyList<string> instanceIds)
        {
            _placed.Clear();
            _unresolved.Clear();
            _stored.Clear();
            for (int i = 0; i < plan.Unresolved.Count; i++)
            {
                var entry = plan.Unresolved[i];
                _unresolved.Add(entry.Record);
                if (entry.Definition != null && (entry.Reason == FurnitureReplayFailure.Blocked || entry.Reason == FurnitureReplayFailure.DuplicateFootprint))
                    _stored.Add(new StoredFurniture(entry.SourceIndex, entry.Definition, entry.Record));
            }

            for (int i = 0; i < plan.Accepted.Count; i++)
            {
                var entry = plan.Accepted[i];
                string id = instanceIds != null ? instanceIds[entry.SourceIndex] : NextInstanceId(entry.Definition.StableId);
                _placed.Add(new PlacedFurniture(id, entry.Definition, entry.Record));
            }
        }

        public bool CanPlace(FurnitureDefinition definition, GridPoint anchor, FurniturePlacementDirection direction)
            => Fits(definition, anchor, direction, _placed);

        /// <summary>
        /// 놓인 가구를 items로 모두 바꿀 수 있는지. 물건이 든 상자만 남긴 방에 차례로 놓아 본다(되살리지 못한 기록은 칸을 차지하지 않는다).
        /// 아무것도 바꾸지 않고 저장하지 않는다.
        /// </summary>
        public bool CanReplaceUserFurniture(IReadOnlyList<FurniturePlacementRequest> items) => PlanReplacement(items) != null;

        /// <summary>
        /// 놓인 가구를 items로 모두 바꾼다. 물건이 든 상자와 되살리지 못한 기록은 남긴다. 전부 놓일 때만 한 번 저장하고,
        /// 하나라도 안 되거나 저장이 실패하면 아무것도 바꾸지 않는다. <see cref="PlacementCommitted"/>는 내지 않는다.
        /// </summary>
        public bool TryReplaceUserFurniture(IReadOnlyList<FurniturePlacementRequest> items)
        {
            var next = PlanReplacement(items);
            if (next == null) return false;
            var records = new List<FurniturePlacementSaveData>(next.Count + _unresolved.Count);
            for (int i = 0; i < next.Count; i++) records.Add(next[i].Record);
            records.AddRange(_unresolved);
            if (!Commit(records)) return false;

            _placed.Clear();
            for (int i = 0; i < next.Count; i++)
                _placed.Add(next[i].InstanceId != null ? next[i] : new PlacedFurniture(NextInstanceId(next[i].Definition.StableId), next[i].Definition, next[i].Record));
            return true;
        }

        /// <summary>바꾼 뒤의 가구 목록(남는 상자 + 새 가구, 새 가구는 인스턴스 id가 아직 없다). 안 되면 null.</summary>
        private List<PlacedFurniture> PlanReplacement(IReadOnlyList<FurniturePlacementRequest> items)
        {
            if (items == null) return null;
            var next = new List<PlacedFurniture>(items.Count + 2);
            for (int i = 0; i < _placed.Count; i++) if (_placed[i].HoldsItems) next.Add(_placed[i]);
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (!Fits(item.Definition, item.Anchor, item.Direction, next)) return null;
                var record = new FurniturePlacementSaveData
                {
                    FurnitureId = item.Definition.StableId,
                    SurfaceId = SurfaceId,
                    AnchorX = item.Anchor.X,
                    AnchorY = item.Anchor.Y,
                    Direction = item.Direction,
                };
                next.Add(new PlacedFurniture(null, item.Definition, record));
            }

            return next;
        }

        /// <summary>others가 놓인 방에 가구 하나를 놓을 수 있는가.</summary>
        private bool Fits(FurnitureDefinition definition, GridPoint anchor, FurniturePlacementDirection direction, IReadOnlyList<PlacedFurniture> others)
        {
            if (definition == null || !Enum.IsDefined(typeof(FurniturePlacementDirection), direction) || !FurnitureReplay.Allows(definition, SurfaceId, _acceptedSurfaceIds)) return false;
            var cells = Cells(anchor, definition.GetFootprintCells(direction));
            if (!_model.CanOccupy(cells)) return false;
            var occupied = new HashSet<GridPoint>();
            var tiles = new HashSet<GridPoint>();
            var blocking = new List<GridPoint>();
            for (int i = 0; i < others.Count; i++)
            {
                occupied.UnionWith(others[i].OccupiedCells);
                tiles.UnionWith(others[i].TileCells);
                if (others[i].Definition.BlocksMovement) blocking.AddRange(others[i].OccupiedCells);
            }

            for (int i = 0; i < cells.Count; i++) if (occupied.Contains(cells[i])) return false;
            if (TileOverlaps(definition, anchor, direction, tiles)) return false;
            if (!definition.BlocksMovement) return true;
            blocking.AddRange(cells);
            return _model.PreservesAccess(blocking);
        }

        public bool TryPlace(FurnitureDefinition definition, GridPoint anchor, FurniturePlacementDirection direction)
        {
            if (!CanPlace(definition, anchor, direction)) return false;
            var record = new FurniturePlacementSaveData
            {
                FurnitureId = definition.StableId,
                SurfaceId = SurfaceId,
                AnchorX = anchor.X,
                AnchorY = anchor.Y,
                Direction = direction,
            };
            var next = Records(null, null);
            next.Insert(_placed.Count, record);
            if (!Commit(next)) return false;
            _placed.Add(new PlacedFurniture(NextInstanceId(definition.StableId), definition, record));
            PlacementCommitted?.Invoke(definition);
            return true;
        }

        /// <summary>놓인 가구를 옮긴다. 자기 옛 칸으로 겹쳐 옮기는 것은 된다. 기록의 다른 값(StateJson 등)은 그대로다.</summary>
        public bool TryMove(string instanceId, GridPoint anchor, FurniturePlacementDirection direction)
        {
            int index = IndexOf(instanceId);
            if (index < 0) return false;
            var original = _placed[index];
            var moved = original.Record.Clone();
            moved.AnchorX = anchor.X;
            moved.AnchorY = anchor.Y;
            moved.Direction = direction;

            var placedRecords = new List<FurniturePlacementSaveData>(_placed.Count);
            var definitions = new List<FurnitureDefinition>(_placed.Count);
            var otherTiles = new HashSet<GridPoint>();
            for (int i = 0; i < _placed.Count; i++)
            {
                placedRecords.Add(i == index ? moved : _placed[i].Record);
                if (!definitions.Contains(_placed[i].Definition)) definitions.Add(_placed[i].Definition);
                if (i != index) otherTiles.UnionWith(_placed[i].TileCells);
            }

            var plan = FurnitureReplay.Plan(_model, SurfaceId, placedRecords, definitions, _acceptedSurfaceIds);
            if (plan.Accepted.Count != placedRecords.Count) return false;
            if (TileOverlaps(original.Definition, anchor, direction, otherTiles)) return false;
            if (!Commit(Records(original, moved))) return false;
            _placed[index] = new PlacedFurniture(original.InstanceId, original.Definition, moved);
            return true;
        }

        /// <summary>치운다. 물건이 든 상자는 치우지 않는다(비우면 치울 수 있다).</summary>
        public bool Delete(string instanceId)
        {
            int index = IndexOf(instanceId);
            if (index < 0 || _placed[index].HoldsItems) return false;
            if (!Commit(Records(_placed[index], null))) return false;
            _placed.RemoveAt(index);
            return true;
        }

        /// <summary>놓인 가구를 모두 치운다. 물건이 든 상자와 되살리지 못한 기록은 남긴다.</summary>
        public bool ClearUserFurniture()
        {
            var kept = new List<PlacedFurniture>();
            var records = new List<FurniturePlacementSaveData>(_unresolved.Count + 4);
            for (int i = 0; i < _placed.Count; i++)
            {
                if (!_placed[i].HoldsItems) continue;
                kept.Add(_placed[i]);
                records.Add(_placed[i].Record);
            }

            records.AddRange(_unresolved);
            if (!Commit(records)) return false;
            _placed.Clear();
            _placed.AddRange(kept);
            return true;
        }

        /// <summary>그 가구의 상자 내용물(사본). 없거나 비었으면 빈 내용물.</summary>
        public FurnitureStorageSaveData StorageOf(string instanceId)
        {
            int index = IndexOf(instanceId);
            return index >= 0 && _placed[index].Record.Storage != null ? _placed[index].Record.Storage.Clone() : new FurnitureStorageSaveData();
        }

        /// <summary>
        /// 상자 내용물을 바꾼다. 메모리만 바꾸고 저장하지 않는다 — 가방과 함께 바뀌므로 부르는 쪽이 가방과 같은 묶음으로
        /// <see cref="BuildSaveRecords"/>를 저장한다(가방 교환 안에서 부르므로 던지지 않는다). 없는 가구면 false.
        /// </summary>
        public bool SetStorage(string instanceId, FurnitureStorageSaveData storage)
        {
            int index = IndexOf(instanceId);
            if (index < 0) return false;
            _placed[index].Record.Storage = storage?.Clone();
            return true;
        }

        public bool Save() => Commit(Records(null, null));

        public bool TryFindAt(GridPoint cell, out PlacedFurniture placed)
        {
            for (int i = 0; i < _placed.Count; i++)
            {
                var cells = _placed[i].OccupiedCells;
                for (int c = 0; c < cells.Count; c++)
                {
                    if (!cells[c].Equals(cell)) continue;
                    placed = _placed[i];
                    return true;
                }
            }

            placed = null;
            return false;
        }

        /// <summary>놓인 가구(놓인 순서) 다음에 되살리지 못한 기록. 모두 사본이다.</summary>
        public FurniturePlacementSaveData[] BuildSaveRecords()
        {
            var records = Records(null, null);
            for (int i = 0; i < records.Count; i++) records[i] = records[i].Clone();
            return records.ToArray();
        }

        internal static IReadOnlyList<GridPoint> Cells(GridPoint anchor, IReadOnlyList<GridPoint> local)
        {
            var cells = new GridPoint[local.Count];
            for (int i = 0; i < cells.Length; i++) cells[i] = new GridPoint(anchor.X + local[i].X, anchor.Y + local[i].Y);
            return cells;
        }

        private static string[] Copy(IReadOnlyList<string> ids)
        {
            if (ids == null || ids.Count == 0) return Array.Empty<string>();
            var copy = new string[ids.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = ids[i];
            return copy;
        }

        private static bool TileOverlaps(FurnitureDefinition definition, GridPoint anchor, FurniturePlacementDirection direction, HashSet<GridPoint> tiles)
        {
            var parts = definition.GetTileParts(direction);
            for (int i = 0; i < parts.Count; i++)
                if (tiles.Contains(new GridPoint(anchor.X + parts[i].LocalCell.X, anchor.Y + parts[i].LocalCell.Y))) return true;
            return false;
        }

        /// <summary>지금 기록 목록. skip은 빼고, replace가 있으면 skip 자리에 넣는다.</summary>
        private List<FurniturePlacementSaveData> Records(PlacedFurniture skip, FurniturePlacementSaveData replace)
        {
            var records = new List<FurniturePlacementSaveData>(_placed.Count + _unresolved.Count + 1);
            for (int i = 0; i < _placed.Count; i++)
            {
                if (_placed[i] != skip) records.Add(_placed[i].Record);
                else if (replace != null) records.Add(replace);
            }

            records.AddRange(_unresolved);
            return records;
        }

        private bool Commit(List<FurniturePlacementSaveData> records)
        {
            if (_commit == null) return true;
            var copy = new FurniturePlacementSaveData[records.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = records[i].Clone();
            return _commit(copy);
        }

        private int IndexOf(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId)) return -1;
            for (int i = 0; i < _placed.Count; i++) if (_placed[i].InstanceId == instanceId) return i;
            return -1;
        }

        private string NextInstanceId(string furnitureId)
        {
            _nextInstanceNumber++;
            return furnitureId + "#" + _nextInstanceNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
