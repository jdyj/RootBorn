using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rootborn.Game.Placement
{
    public sealed class FurniturePlacementRegistry
    {
        private readonly Dictionary<string, Entry> _entriesById = new Dictionary<string, Entry>();
        private readonly Dictionary<string, string> _instanceIdByCell = new Dictionary<string, string>();
        private int _nextInstanceNumber;

        public IReadOnlyCollection<FurniturePlacementInstance> Instances
        {
            get
            {
                var instances = new List<FurniturePlacementInstance>(_entriesById.Count);
                foreach (var pair in _entriesById)
                {
                    instances.Add(pair.Value.Instance);
                }

                return instances;
            }
        }

        public bool TryPlace(
            TilePlacementSurface surface,
            FurnitureDefinition furniture,
            Vector3Int anchorCell,
            FurniturePlacementDirection direction,
            out FurniturePlacementInstance instance,
            out FurniturePlacementResult placement)
        {
            instance = default;
            if (!FurniturePlacementService.TryPlace(surface, furniture, anchorCell, direction, out placement))
            {
                return false;
            }

            var instanceId = CreateInstanceId(furniture.StableId);
            var tileCells = ResolveTileCells(furniture, anchorCell, direction);
            instance = new FurniturePlacementInstance(instanceId, furniture.StableId, surface.SurfaceId, anchorCell, direction, placement.OccupiedCells, tileCells);
            Register(new Entry(instance, furniture));
            return true;
        }

        public bool TryFindAt(string surfaceId, Vector3Int cell, out FurniturePlacementInstance instance)
        {
            instance = default;
            if (!_instanceIdByCell.TryGetValue(CellKey(surfaceId, cell), out var instanceId))
            {
                return false;
            }

            if (!_entriesById.TryGetValue(instanceId, out var entry))
            {
                return false;
            }

            instance = entry.Instance;
            return true;
        }

        public bool TryDelete(TilePlacementSurface surface, string instanceId)
        {
            if (surface == null || string.IsNullOrEmpty(instanceId) || !_entriesById.TryGetValue(instanceId, out var entry))
            {
                return false;
            }

            ClearInstanceTiles(surface, entry.Instance);
            Unregister(entry.Instance);
            return true;
        }

        public int ClearSurface(TilePlacementSurface surface)
        {
            if (surface == null)
            {
                return 0;
            }

            var toDelete = new List<string>();
            foreach (var pair in _entriesById)
            {
                if (string.Equals(pair.Value.Instance.SurfaceId, surface.SurfaceId, StringComparison.Ordinal))
                {
                    toDelete.Add(pair.Key);
                }
            }

            for (int i = 0; i < toDelete.Count; i++)
            {
                TryDelete(surface, toDelete[i]);
            }

            return toDelete.Count;
        }

        public bool TryMove(
            TilePlacementSurface surface,
            string instanceId,
            Vector3Int newAnchorCell,
            FurniturePlacementDirection direction,
            out FurniturePlacementInstance moved,
            out FurniturePlacementResult placement)
        {
            moved = default;
            if (surface == null || string.IsNullOrEmpty(instanceId) || !_entriesById.TryGetValue(instanceId, out var entry))
            {
                placement = new FurniturePlacementResult(false, FurniturePlacementFailureReason.MissingFurniture, string.Empty, surface != null ? surface.SurfaceId : string.Empty, newAnchorCell, direction, Array.Empty<Vector3Int>());
                return false;
            }

            ClearInstanceTiles(surface, entry.Instance);
            Unregister(entry.Instance);
            if (!FurniturePlacementService.TryPlace(surface, entry.Furniture, newAnchorCell, direction, out placement))
            {
                FurniturePlacementService.TryPlace(surface, entry.Furniture, entry.Instance.AnchorCell, entry.Instance.Direction, out _);
                Register(entry);
                moved = entry.Instance;
                return false;
            }

            moved = new FurniturePlacementInstance(instanceId, entry.Instance.FurnitureId, entry.Instance.SurfaceId, newAnchorCell, direction, placement.OccupiedCells, ResolveTileCells(entry.Furniture, newAnchorCell, direction));
            Register(new Entry(moved, entry.Furniture));
            return true;
        }

        private void Register(Entry entry)
        {
            _entriesById[entry.Instance.InstanceId] = entry;
            for (int i = 0; i < entry.Instance.OccupiedCells.Count; i++)
            {
                _instanceIdByCell[CellKey(entry.Instance.SurfaceId, entry.Instance.OccupiedCells[i])] = entry.Instance.InstanceId;
            }
        }

        private void Unregister(FurniturePlacementInstance instance)
        {
            _entriesById.Remove(instance.InstanceId);
            for (int i = 0; i < instance.OccupiedCells.Count; i++)
            {
                _instanceIdByCell.Remove(CellKey(instance.SurfaceId, instance.OccupiedCells[i]));
            }
        }

        private static void ClearInstanceTiles(TilePlacementSurface surface, FurniturePlacementInstance instance)
        {
            if (surface.ObjectTilemap != null)
            {
                for (int i = 0; i < instance.TileCells.Count; i++)
                {
                    surface.ObjectTilemap.SetTile(instance.TileCells[i], null);
                }
            }

            if (surface.OccupancyTilemap != null)
            {
                for (int i = 0; i < instance.OccupiedCells.Count; i++)
                {
                    surface.OccupancyTilemap.SetTile(instance.OccupiedCells[i], null);
                }
            }
        }

        private string CreateInstanceId(string furnitureId)
        {
            _nextInstanceNumber++;
            return (string.IsNullOrWhiteSpace(furnitureId) ? "furniture" : furnitureId) + "#" + _nextInstanceNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static IReadOnlyList<Vector3Int> ResolveTileCells(FurnitureDefinition furniture, Vector3Int anchorCell, FurniturePlacementDirection direction)
        {
            if (furniture == null)
            {
                return Array.Empty<Vector3Int>();
            }

            var tileParts = furniture.GetTileParts(direction);
            var cells = new List<Vector3Int>(tileParts.Count);
            for (int i = 0; i < tileParts.Count; i++)
            {
                var local = tileParts[i].LocalCell;
                cells.Add(anchorCell + new Vector3Int(local.x, local.y, 0));
            }

            return cells;
        }

        private static string CellKey(string surfaceId, Vector3Int cell)
        {
            return (surfaceId ?? string.Empty) + ":" + cell.x + "," + cell.y + "," + cell.z;
        }

        private readonly struct Entry
        {
            public Entry(FurniturePlacementInstance instance, FurnitureDefinition furniture)
            {
                Instance = instance;
                Furniture = furniture;
            }

            public FurniturePlacementInstance Instance { get; }
            public FurnitureDefinition Furniture { get; }
        }
    }

    public readonly struct FurniturePlacementInstance
    {
        public FurniturePlacementInstance(
            string instanceId,
            string furnitureId,
            string surfaceId,
            Vector3Int anchorCell,
            FurniturePlacementDirection direction,
            IReadOnlyList<Vector3Int> occupiedCells,
            IReadOnlyList<Vector3Int> tileCells)
        {
            InstanceId = instanceId ?? string.Empty;
            FurnitureId = furnitureId ?? string.Empty;
            SurfaceId = surfaceId ?? string.Empty;
            AnchorCell = anchorCell;
            Direction = direction;
            OccupiedCells = occupiedCells ?? Array.Empty<Vector3Int>();
            TileCells = tileCells ?? Array.Empty<Vector3Int>();
        }

        public string InstanceId { get; }
        public string FurnitureId { get; }
        public string SurfaceId { get; }
        public Vector3Int AnchorCell { get; }
        public FurniturePlacementDirection Direction { get; }
        public IReadOnlyList<Vector3Int> OccupiedCells { get; }
        public IReadOnlyList<Vector3Int> TileCells { get; }
    }
}
