using System;
using System.Collections.Generic;
using Rootborn.Core.Minigames;

namespace Rootborn.Core.Placement
{
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

        internal FurniturePlacementSaveData Record { get; }
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
        private int _nextInstanceNumber;

        /// <param name="commit">바뀐 뒤의 기록 전체를 저장한다. false면 바꾸지 않는다. null이면 늘 성공으로 본다.</param>
        public HouseFurnitureLayout(HousePlacementModel model, string surfaceId, Func<FurniturePlacementSaveData[], bool> commit = null)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            SurfaceId = surfaceId ?? string.Empty;
            _commit = commit;
        }

        /// <summary>가구를 새로 놓은 뒤 한 번. 퀘스트의 가구 배치 이벤트를 여기서 낸다.</summary>
        public event Action<FurnitureDefinition> PlacementCommitted;

        public string SurfaceId { get; }
        public IReadOnlyList<PlacedFurniture> Placed => _placed;
        public int UnresolvedCount => _unresolved.Count;

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
        public void Restore(IReadOnlyList<FurniturePlacementSaveData> records, IEnumerable<FurnitureDefinition> catalog)
        {
            var plan = FurnitureReplay.Plan(_model, SurfaceId, records, catalog);
            _placed.Clear();
            _unresolved.Clear();
            for (int i = 0; i < plan.Unresolved.Count; i++) _unresolved.Add(plan.Unresolved[i].Record);
            for (int i = 0; i < plan.Accepted.Count; i++)
                _placed.Add(new PlacedFurniture(NextInstanceId(plan.Accepted[i].Definition.StableId), plan.Accepted[i].Definition, plan.Accepted[i].Record));
        }

        public bool CanPlace(FurnitureDefinition definition, GridPoint anchor, FurniturePlacementDirection direction)
        {
            if (definition == null || !Enum.IsDefined(typeof(FurniturePlacementDirection), direction) || !definition.AllowsSurface(SurfaceId)) return false;
            var cells = Cells(anchor, definition.GetFootprintCells(direction));
            if (!_model.CanOccupy(cells)) return false;
            var occupied = new HashSet<GridPoint>();
            var tiles = new HashSet<GridPoint>();
            var blocking = new List<GridPoint>();
            for (int i = 0; i < _placed.Count; i++)
            {
                occupied.UnionWith(_placed[i].OccupiedCells);
                tiles.UnionWith(_placed[i].TileCells);
                if (_placed[i].Definition.BlocksMovement) blocking.AddRange(_placed[i].OccupiedCells);
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

            var plan = FurnitureReplay.Plan(_model, SurfaceId, placedRecords, definitions);
            if (plan.Accepted.Count != placedRecords.Count) return false;
            if (TileOverlaps(original.Definition, anchor, direction, otherTiles)) return false;
            if (!Commit(Records(original, moved))) return false;
            _placed[index] = new PlacedFurniture(original.InstanceId, original.Definition, moved);
            return true;
        }

        public bool Delete(string instanceId)
        {
            int index = IndexOf(instanceId);
            if (index < 0) return false;
            if (!Commit(Records(_placed[index], null))) return false;
            _placed.RemoveAt(index);
            return true;
        }

        /// <summary>놓인 가구를 모두 치운다. 되살리지 못한 기록은 남긴다.</summary>
        public bool ClearUserFurniture()
        {
            if (!Commit(new List<FurniturePlacementSaveData>(_unresolved))) return false;
            _placed.Clear();
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
