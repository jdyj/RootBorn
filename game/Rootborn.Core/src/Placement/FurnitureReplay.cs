using System;
using System.Collections.Generic;
using Rootborn.Core.Minigames;

namespace Rootborn.Core.Placement
{
    /// <summary>되살리지 못한 이유. 숫자는 Unity 판 AuthoredFurnitureReplayFailure 순서와 같다.</summary>
    public enum FurnitureReplayFailure { None, MissingDefinition, WrongSurface, Blocked, DuplicateFootprint, AmbiguousDefinition, InvalidDirection }

    public sealed class FurnitureReplayEntry
    {
        public FurnitureReplayEntry(int sourceIndex, FurniturePlacementSaveData record, FurnitureDefinition definition, FurnitureReplayFailure reason, IReadOnlyList<GridPoint> cells,
            bool moved = false)
        {
            SourceIndex = sourceIndex;
            Record = record?.Clone();
            Definition = definition;
            Reason = reason;
            Cells = cells ?? Array.Empty<GridPoint>();
            Moved = moved;
        }

        public int SourceIndex { get; }

        /// <summary>제자리에 못 놓아 가까운 자리로 옮긴 기록이다(<see cref="FurnitureReplay.PlanRelocating"/>). Record의 기준 칸이 새 자리다.</summary>
        public bool Moved { get; }

        /// <summary>원래 기록의 사본. 고쳐도 원래 기록은 바뀌지 않는다.</summary>
        public FurniturePlacementSaveData Record { get; }

        public FurnitureDefinition Definition { get; }
        public FurnitureReplayFailure Reason { get; }

        /// <summary>받은 기록이 차지하는 칸. 받지 못한 기록은 빈 목록일 수 있다.</summary>
        public IReadOnlyList<GridPoint> Cells { get; }
    }

    public sealed class FurnitureReplayPlan
    {
        internal FurnitureReplayPlan(List<FurnitureReplayEntry> accepted, List<FurnitureReplayEntry> unresolved)
        {
            Accepted = accepted.AsReadOnly();
            Unresolved = unresolved.AsReadOnly();
        }

        public IReadOnlyList<FurnitureReplayEntry> Accepted { get; }

        /// <summary>놓지 못한 기록. 버리지 않고 세이브에 그대로 다시 쓴다(정의가 다시 생기면 되살아난다).</summary>
        public IReadOnlyList<FurnitureReplayEntry> Unresolved { get; }
    }

    /// <summary>
    /// 저장된 배치 기록을 방에 다시 놓을 수 있는지 미리 본다(원본 AuthoredHouseFurnitureReplay). 아무것도 바꾸지 않는다.
    /// 기록 순서대로 본다: 앞에서 받은 가구와 겹치거나, 앞의 가구와 함께 길을 막으면 뒤의 것을 받지 않는다.
    /// </summary>
    public static class FurnitureReplay
    {
        /// <param name="acceptedSurfaceIds">이 면에서도 받는 다른 면의 가구(예: 일터가 집 가구를 받는다). 기록의 면은 여전히 surfaceId여야 한다.</param>
        public static FurnitureReplayPlan Plan(HousePlacementModel model, string surfaceId, IReadOnlyList<FurniturePlacementSaveData> records, IEnumerable<FurnitureDefinition> catalog,
            IReadOnlyList<string> acceptedSurfaceIds = null)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            var accepted = new List<FurnitureReplayEntry>();
            var unresolved = new List<FurnitureReplayEntry>();
            var occupied = new HashSet<GridPoint>();
            var blocking = new List<GridPoint>();
            var definitions = new Dictionary<string, FurnitureDefinition>(StringComparer.Ordinal);
            var ambiguous = new HashSet<string>(StringComparer.Ordinal);
            if (catalog != null)
            {
                foreach (var definition in catalog)
                {
                    if (definition == null) continue;
                    if (definitions.ContainsKey(definition.StableId)) ambiguous.Add(definition.StableId);
                    else definitions.Add(definition.StableId, definition);
                }
            }

            if (records == null) return new FurnitureReplayPlan(accepted, unresolved);
            for (int i = 0; i < records.Count; i++)
            {
                var record = records[i];
                if (record == null) continue;
                definitions.TryGetValue(record.FurnitureId ?? string.Empty, out var found);
                var reason = Check(model, surfaceId, acceptedSurfaceIds, record, found, ambiguous, occupied, out var cells);
                if (reason == FurnitureReplayFailure.None && found.BlocksMovement)
                {
                    var withThis = new List<GridPoint>(blocking);
                    withThis.AddRange(cells);
                    if (!model.PreservesAccess(withThis)) reason = FurnitureReplayFailure.Blocked;
                }

                var entry = new FurnitureReplayEntry(i, record, found, reason, cells);
                if (reason != FurnitureReplayFailure.None)
                {
                    unresolved.Add(entry);
                    continue;
                }

                accepted.Add(entry);
                for (int c = 0; c < cells.Count; c++)
                {
                    occupied.Add(cells[c]);
                    if (found.BlocksMovement) blocking.Add(cells[c]);
                }
            }

            return new FurnitureReplayPlan(accepted, unresolved);
        }

        /// <summary>
        /// <see cref="Plan"/>과 같이 제자리 기록을 받은 뒤, 칸이 막혀(<see cref="FurnitureReplayFailure.Blocked"/>) 또는 앞의 가구와 겹쳐
        /// (<see cref="FurnitureReplayFailure.DuplicateFootprint"/>) 못 놓은 기록만 가까운 자리로 옮겨 본다(집 맵을 바꾼 뒤 옛 가구, 설계 7.3절).
        /// 기록 순서대로, 방의 바닥 칸(<see cref="IPlacementGrid.FloorCells"/>)을 원래 기준 칸에서 맨해튼 거리가 가까운 것부터 —
        /// 같으면 가구 칸 Y가 큰 것(화면 위), 그것도 같으면 X가 작은 것(왼쪽) — 같은 방향으로 놓아 보고 처음 들어가는 칸에 놓는다.
        /// 들어간다 = 놓을 수 있는 칸이고(<see cref="HousePlacementModel.CanOccupy"/>) 앞에서 받은 가구와 차지 칸·그림 칸이 겹치지 않고,
        /// 이동을 막는 가구면 길을 지킨다(<see cref="HousePlacementModel.PreservesAccess"/>). 거리 상한은 없다(바닥 칸 수가 상한이다).
        /// 옮긴 기록은 사본이고 기준 칸만 바뀐다(방향·상태·그림 이름·상자 내용물 그대로). 다른 이유(정의 없음·다른 면·정의 중복·방향 잘못)는 옮기지 않는다.
        /// 아무것도 바꾸지 않는다. 같은 입력이면 같은 결과다.
        /// </summary>
        public static FurnitureReplayPlan PlanRelocating(HousePlacementModel model, string surfaceId, IReadOnlyList<FurniturePlacementSaveData> records,
            IEnumerable<FurnitureDefinition> catalog, IReadOnlyList<string> acceptedSurfaceIds = null)
        {
            var plan = Plan(model, surfaceId, records, catalog, acceptedSurfaceIds);
            bool any = false;
            for (int i = 0; i < plan.Unresolved.Count && !any; i++) any = Movable(plan.Unresolved[i].Reason);
            if (!any) return plan;

            var occupied = new HashSet<GridPoint>();
            var tiles = new HashSet<GridPoint>();
            var blocking = new List<GridPoint>();
            for (int i = 0; i < plan.Accepted.Count; i++) Take(plan.Accepted[i].Definition, plan.Accepted[i].Record, plan.Accepted[i].Cells, occupied, tiles, blocking);

            var floor = new List<GridPoint>(model.FloorCells);
            var moved = new List<FurnitureReplayEntry>();
            var unresolved = new List<FurnitureReplayEntry>(plan.Unresolved.Count);
            for (int i = 0; i < plan.Unresolved.Count; i++)
            {
                var entry = plan.Unresolved[i];
                if (!Movable(entry.Reason) || entry.Definition == null || !TryFindNearest(model, entry, floor, occupied, tiles, blocking, out var anchor, out var cells))
                {
                    unresolved.Add(entry);
                    continue;
                }

                var record = entry.Record.Clone();
                record.AnchorX = anchor.X;
                record.AnchorY = anchor.Y;
                var placed = new FurnitureReplayEntry(entry.SourceIndex, record, entry.Definition, FurnitureReplayFailure.None, cells, moved: true);
                Take(placed.Definition, placed.Record, cells, occupied, tiles, blocking);
                moved.Add(placed);
            }

            if (moved.Count == 0) return plan;
            var accepted = new List<FurnitureReplayEntry>(plan.Accepted.Count + moved.Count);
            int a = 0, m = 0;
            while (a < plan.Accepted.Count || m < moved.Count)
            {
                if (m >= moved.Count || (a < plan.Accepted.Count && plan.Accepted[a].SourceIndex < moved[m].SourceIndex)) accepted.Add(plan.Accepted[a++]);
                else accepted.Add(moved[m++]);
            }

            return new FurnitureReplayPlan(accepted, unresolved);
        }

        private static bool Movable(FurnitureReplayFailure reason) => reason == FurnitureReplayFailure.Blocked || reason == FurnitureReplayFailure.DuplicateFootprint;

        private static void Take(FurnitureDefinition definition, FurniturePlacementSaveData record, IReadOnlyList<GridPoint> cells,
            HashSet<GridPoint> occupied, HashSet<GridPoint> tiles, List<GridPoint> blocking)
        {
            for (int c = 0; c < cells.Count; c++)
            {
                occupied.Add(cells[c]);
                if (definition.BlocksMovement) blocking.Add(cells[c]);
            }

            var parts = definition.GetTileParts(record.Direction);
            for (int p = 0; p < parts.Count; p++) tiles.Add(new GridPoint(record.AnchorX + parts[p].LocalCell.X, record.AnchorY + parts[p].LocalCell.Y));
        }

        // 바닥 칸을 가까운 순서로 훑어 처음 들어가는 기준 칸. 순서: 맨해튼 거리, 같으면 Y 큰 것(위), 같으면 X 작은 것(왼쪽) — 칸마다 다르므로 늘 하나로 정해진다.
        private static bool TryFindNearest(HousePlacementModel model, FurnitureReplayEntry entry, List<GridPoint> floor, HashSet<GridPoint> occupied,
            HashSet<GridPoint> tiles, List<GridPoint> blocking, out GridPoint anchor, out IReadOnlyList<GridPoint> cells)
        {
            var origin = new GridPoint(entry.Record.AnchorX, entry.Record.AnchorY);
            var direction = entry.Record.Direction;
            var definition = entry.Definition;
            floor.Sort((p, q) =>
            {
                int byDistance = Distance(p, origin).CompareTo(Distance(q, origin));
                if (byDistance != 0) return byDistance;
                int byY = q.Y.CompareTo(p.Y);
                return byY != 0 ? byY : p.X.CompareTo(q.X);
            });

            var footprint = definition.GetFootprintCells(direction);
            var parts = definition.GetTileParts(direction);
            for (int i = 0; i < floor.Count; i++)
            {
                var candidate = floor[i];
                var candidateCells = HouseFurnitureLayout.Cells(candidate, footprint);
                if (!model.CanOccupy(candidateCells) || Overlaps(candidateCells, occupied) || TilesOverlap(candidate, parts, tiles)) continue;
                if (definition.BlocksMovement)
                {
                    var withThis = new List<GridPoint>(blocking.Count + candidateCells.Count);
                    withThis.AddRange(blocking);
                    withThis.AddRange(candidateCells);
                    if (!model.PreservesAccess(withThis)) continue;
                }

                anchor = candidate;
                cells = candidateCells;
                return true;
            }

            anchor = default;
            cells = null;
            return false;
        }

        private static long Distance(GridPoint p, GridPoint origin) => Math.Abs((long)p.X - origin.X) + Math.Abs((long)p.Y - origin.Y);

        private static bool Overlaps(IReadOnlyList<GridPoint> cells, HashSet<GridPoint> occupied)
        {
            for (int i = 0; i < cells.Count; i++) if (occupied.Contains(cells[i])) return true;
            return false;
        }

        private static bool TilesOverlap(GridPoint anchor, IReadOnlyList<FurnitureTilePart> parts, HashSet<GridPoint> tiles)
        {
            for (int i = 0; i < parts.Count; i++)
                if (tiles.Contains(new GridPoint(anchor.X + parts[i].LocalCell.X, anchor.Y + parts[i].LocalCell.Y))) return true;
            return false;
        }

        /// <summary>가구가 이 면이나 받는 면 중 하나를 허용하는가.</summary>
        internal static bool Allows(FurnitureDefinition definition, string surfaceId, IReadOnlyList<string> acceptedSurfaceIds)
        {
            if (definition.AllowsSurface(surfaceId)) return true;
            if (acceptedSurfaceIds == null) return false;
            for (int i = 0; i < acceptedSurfaceIds.Count; i++)
                if (!string.IsNullOrEmpty(acceptedSurfaceIds[i]) && definition.AllowsSurface(acceptedSurfaceIds[i])) return true;
            return false;
        }

        private static FurnitureReplayFailure Check(HousePlacementModel model, string surfaceId, IReadOnlyList<string> acceptedSurfaceIds, FurniturePlacementSaveData record,
            FurnitureDefinition definition, HashSet<string> ambiguous, HashSet<GridPoint> occupied, out IReadOnlyList<GridPoint> cells)
        {
            cells = Array.Empty<GridPoint>();
            if (!string.Equals(record.SurfaceId, surfaceId, StringComparison.Ordinal)) return FurnitureReplayFailure.WrongSurface;
            if (definition == null) return FurnitureReplayFailure.MissingDefinition;
            if (ambiguous.Contains(definition.StableId)) return FurnitureReplayFailure.AmbiguousDefinition;
            if (!Enum.IsDefined(typeof(FurniturePlacementDirection), record.Direction)) return FurnitureReplayFailure.InvalidDirection;
            if (!Allows(definition, surfaceId, acceptedSurfaceIds)) return FurnitureReplayFailure.WrongSurface;
            cells = HouseFurnitureLayout.Cells(new GridPoint(record.AnchorX, record.AnchorY), definition.GetFootprintCells(record.Direction));
            if (!model.CanOccupy(cells)) return FurnitureReplayFailure.Blocked;
            for (int i = 0; i < cells.Count; i++) if (occupied.Contains(cells[i])) return FurnitureReplayFailure.DuplicateFootprint;
            return FurnitureReplayFailure.None;
        }
    }
}
