using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Rootborn.Game.Placement
{
    public enum FurniturePlacementDirection
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3
    }

    public enum FurniturePlacementPreference
    {
        Any = 0,
        AvoidBlockedCells = 1,
        NearWall = 2,
        NearWindow = 3
    }

    public enum FurniturePlacementFailureReason
    {
        None = 0,
        MissingSurface = 1,
        MissingFurniture = 2,
        SurfaceNotAllowed = 3,
        OutOfBounds = 4,
        MissingGround = 5,
        Occupied = 6,
        MissingTilemap = 7
    }

    [Flags]
    public enum TilePlacementRuleFlags
    {
        None = 0,
        RequireGround = 1 << 0,
        RejectOccupiedCells = 1 << 1,
        StayInsideBounds = 1 << 2
    }

    [Serializable]
    public readonly struct FurnitureTilePart
    {
        public FurnitureTilePart(Vector2Int localCell, TileBase tile)
        {
            LocalCell = localCell;
            Tile = tile;
        }

        public Vector2Int LocalCell { get; }
        public TileBase Tile { get; }
    }

    public readonly struct FurniturePlacementResult
    {
        public FurniturePlacementResult(
            bool success,
            FurniturePlacementFailureReason failureReason,
            string furnitureId,
            string surfaceId,
            Vector3Int anchorCell,
            FurniturePlacementDirection direction,
            IReadOnlyList<Vector3Int> occupiedCells)
        {
            Success = success;
            FailureReason = failureReason;
            FurnitureId = furnitureId ?? string.Empty;
            SurfaceId = surfaceId ?? string.Empty;
            AnchorCell = anchorCell;
            Direction = direction;
            OccupiedCells = occupiedCells ?? Array.Empty<Vector3Int>();
        }

        public bool Success { get; }
        public FurniturePlacementFailureReason FailureReason { get; }
        public string FurnitureId { get; }
        public string SurfaceId { get; }
        public Vector3Int AnchorCell { get; }
        public FurniturePlacementDirection Direction { get; }
        public IReadOnlyList<Vector3Int> OccupiedCells { get; }
    }
}
