using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Rootborn.Game.Placement
{
    public static class FurniturePlacementService
    {
        private static Tile _occupancyTile;

        public static bool TryPlace(
            TilePlacementSurface surface,
            FurnitureDefinition furniture,
            Vector3Int anchorCell,
            FurniturePlacementDirection direction,
            out FurniturePlacementResult result)
        {
            var validation = Validate(surface, furniture, anchorCell, direction, out var occupiedCells);
            if (validation != FurniturePlacementFailureReason.None)
            {
                result = new FurniturePlacementResult(false, validation, furniture != null ? furniture.StableId : string.Empty, surface != null ? surface.SurfaceId : string.Empty, anchorCell, direction, occupiedCells);
                return false;
            }

            var objectTilemap = surface.ObjectTilemap;
            var occupancyTilemap = surface.OccupancyTilemap;
            var occupancyTile = ResolveOccupancyTile();
            var tileParts = furniture.GetTileParts(direction);
            for (int i = 0; i < tileParts.Count; i++)
            {
                var part = tileParts[i];
                var cell = anchorCell + new Vector3Int(part.LocalCell.x, part.LocalCell.y, 0);
                objectTilemap.SetTile(cell, part.Tile);
            }

            for (int i = 0; i < occupiedCells.Count; i++)
            {
                occupancyTilemap.SetTile(occupiedCells[i], occupancyTile);
            }

            objectTilemap.CompressBounds();
            occupancyTilemap.CompressBounds();
            result = new FurniturePlacementResult(true, FurniturePlacementFailureReason.None, furniture.StableId, surface.SurfaceId, anchorCell, direction, occupiedCells);
            return true;
        }

        private static FurniturePlacementFailureReason Validate(TilePlacementSurface surface, FurnitureDefinition furniture, Vector3Int anchorCell, FurniturePlacementDirection direction, out IReadOnlyList<Vector3Int> occupiedCells)
        {
            var cells = new List<Vector3Int>();
            occupiedCells = cells;

            if (surface == null)
            {
                return FurniturePlacementFailureReason.MissingSurface;
            }

            if (furniture == null)
            {
                return FurniturePlacementFailureReason.MissingFurniture;
            }

            if (surface.ObjectTilemap == null || surface.OccupancyTilemap == null)
            {
                return FurniturePlacementFailureReason.MissingTilemap;
            }

            if (surface.PlacementRules.HasFlag(TilePlacementRuleFlags.RequireGround) && surface.GroundTilemap == null)
            {
                return FurniturePlacementFailureReason.MissingTilemap;
            }

            if (furniture.AllowedSurfaceIds.Count > 0 && !ContainsSurface(furniture.AllowedSurfaceIds, surface.SurfaceId))
            {
                return FurniturePlacementFailureReason.SurfaceNotAllowed;
            }

            var footprint = furniture.GetFootprintCells(direction);
            for (int i = 0; i < footprint.Count; i++)
            {
                var local = footprint[i];
                var cell = anchorCell + new Vector3Int(local.x, local.y, 0);
                cells.Add(cell);

                if (surface.PlacementRules.HasFlag(TilePlacementRuleFlags.StayInsideBounds) && !surface.Bounds.Contains(cell))
                {
                    return FurniturePlacementFailureReason.OutOfBounds;
                }

                if (surface.PlacementRules.HasFlag(TilePlacementRuleFlags.RequireGround) && surface.GroundTilemap.GetTile(cell) == null)
                {
                    return FurniturePlacementFailureReason.MissingGround;
                }

                if (surface.PlacementRules.HasFlag(TilePlacementRuleFlags.RejectOccupiedCells) && surface.OccupancyTilemap.GetTile(cell) != null)
                {
                    return FurniturePlacementFailureReason.Occupied;
                }
            }

            return FurniturePlacementFailureReason.None;
        }

        private static bool ContainsSurface(IReadOnlyList<string> allowedSurfaceIds, string surfaceId)
        {
            for (int i = 0; i < allowedSurfaceIds.Count; i++)
            {
                if (string.Equals(allowedSurfaceIds[i], surfaceId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static Tile ResolveOccupancyTile()
        {
            if (_occupancyTile != null)
            {
                return _occupancyTile;
            }

            _occupancyTile = ScriptableObject.CreateInstance<Tile>();
            _occupancyTile.name = "FurnitureOccupancy";
            return _occupancyTile;
        }
    }
}
