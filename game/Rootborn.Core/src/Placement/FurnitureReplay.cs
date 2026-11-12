using System;
using System.Collections.Generic;
using Rootborn.Core.Minigames;

namespace Rootborn.Core.Placement
{
    /// <summary>되살리지 못한 이유. 숫자는 Unity 판 AuthoredFurnitureReplayFailure 순서와 같다.</summary>
    public enum FurnitureReplayFailure { None, MissingDefinition, WrongSurface, Blocked, DuplicateFootprint, AmbiguousDefinition, InvalidDirection }

    public sealed class FurnitureReplayEntry
    {
        public FurnitureReplayEntry(int sourceIndex, FurniturePlacementSaveData record, FurnitureDefinition definition, FurnitureReplayFailure reason, IReadOnlyList<GridPoint> cells)
        {
            SourceIndex = sourceIndex;
            Record = record?.Clone();
            Definition = definition;
            Reason = reason;
            Cells = cells ?? Array.Empty<GridPoint>();
        }

        public int SourceIndex { get; }

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
