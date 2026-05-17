using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Rootborn.Game.Placement
{
    [CreateAssetMenu(fileName = "FurnitureDefinition", menuName = "Rootborn/Placement/Furniture Definition")]
    public sealed class FurnitureDefinition : ScriptableObject
    {
        [SerializeField] private string _stableId = string.Empty;
        [SerializeField] private string _displayName = string.Empty;
        [SerializeField] private string _category = string.Empty;
        [SerializeField] private string[] _allowedSurfaceIds = Array.Empty<string>();
        [SerializeField] private FurnitureTilePartData[] _tileParts = Array.Empty<FurnitureTilePartData>();
        [SerializeField] private Vector2Int[] _footprintCells = Array.Empty<Vector2Int>();
        [SerializeField] private bool _blocksMovement = true;
        [SerializeField] private FurniturePlacementPreference _placementPreference = FurniturePlacementPreference.Any;
        [SerializeField] private string _unlockToken = string.Empty;

        public string StableId => _stableId;
        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? _stableId : _displayName;
        public string Category => _category;
        public IReadOnlyList<string> AllowedSurfaceIds => _allowedSurfaceIds ?? Array.Empty<string>();
        public IReadOnlyList<FurnitureTilePart> TileParts => BuildRuntimeParts();
        public IReadOnlyList<Vector2Int> FootprintCells => ResolveFootprintCells();
        public bool BlocksMovement => _blocksMovement;
        public FurniturePlacementPreference PlacementPreference => _placementPreference;
        public string UnlockToken => _unlockToken;

        public static FurnitureDefinition CreateForTests(
            string stableId,
            string displayName,
            string category,
            IEnumerable<string> allowedSurfaceIds,
            IEnumerable<FurnitureTilePart> tileParts,
            IEnumerable<Vector2Int> footprintCells,
            bool blocksMovement,
            FurniturePlacementPreference placementPreference)
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                throw new ArgumentException("Furniture stable id is required.", nameof(stableId));
            }

            var parts = (tileParts ?? Array.Empty<FurnitureTilePart>())
                .Where(part => part.Tile != null)
                .ToArray();
            if (parts.Length == 0)
            {
                throw new ArgumentException("Furniture needs at least one tile part.", nameof(tileParts));
            }

            var definition = CreateInstance<FurnitureDefinition>();
            definition._stableId = stableId;
            definition._displayName = displayName ?? string.Empty;
            definition._category = category ?? string.Empty;
            definition._allowedSurfaceIds = (allowedSurfaceIds ?? Array.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToArray();
            definition._tileParts = parts
                .Select(part => new FurnitureTilePartData(part.LocalCell, part.Tile))
                .ToArray();
            var explicitFootprint = (footprintCells ?? Array.Empty<Vector2Int>()).Distinct().ToArray();
            definition._footprintCells = explicitFootprint.Length > 0
                ? explicitFootprint
                : parts.Select(part => part.LocalCell).Distinct().ToArray();
            definition._blocksMovement = blocksMovement;
            definition._placementPreference = placementPreference;
            return definition;
        }

        public IReadOnlyList<FurnitureTilePart> GetTileParts(FurniturePlacementDirection direction)
        {
            var source = BuildRuntimeParts();
            if (direction == FurniturePlacementDirection.North || source.Count == 0)
            {
                return source;
            }

            var rotated = new List<FurnitureTilePart>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                rotated.Add(new FurnitureTilePart(RotateLocalCell(source[i].LocalCell, direction), source[i].Tile));
            }

            return rotated;
        }

        public IReadOnlyList<Vector2Int> GetFootprintCells(FurniturePlacementDirection direction)
        {
            var source = ResolveFootprintCells();
            if (direction == FurniturePlacementDirection.North || source.Count == 0)
            {
                return source;
            }

            return source.Select(cell => RotateLocalCell(cell, direction)).Distinct().ToArray();
        }

        private IReadOnlyList<FurnitureTilePart> BuildRuntimeParts()
        {
            if (_tileParts == null || _tileParts.Length == 0)
            {
                return Array.Empty<FurnitureTilePart>();
            }

            var parts = new List<FurnitureTilePart>(_tileParts.Length);
            for (int i = 0; i < _tileParts.Length; i++)
            {
                if (_tileParts[i].Tile != null)
                {
                    parts.Add(new FurnitureTilePart(_tileParts[i].LocalCell, _tileParts[i].Tile));
                }
            }

            return parts;
        }

        private IReadOnlyList<Vector2Int> ResolveFootprintCells()
        {
            if (_footprintCells != null && _footprintCells.Length > 0)
            {
                return _footprintCells;
            }

            return BuildRuntimeParts().Select(part => part.LocalCell).Distinct().ToArray();
        }

        private static Vector2Int RotateLocalCell(Vector2Int cell, FurniturePlacementDirection direction)
        {
            switch (direction)
            {
                case FurniturePlacementDirection.East:
                    return new Vector2Int(cell.y, -cell.x);
                case FurniturePlacementDirection.South:
                    return new Vector2Int(-cell.x, -cell.y);
                case FurniturePlacementDirection.West:
                    return new Vector2Int(-cell.y, cell.x);
                default:
                    return cell;
            }
        }

        [Serializable]
        private sealed class FurnitureTilePartData
        {
            [SerializeField] private Vector2Int _localCell;
            [SerializeField] private TileBase _tile;

            public FurnitureTilePartData(Vector2Int localCell, TileBase tile)
            {
                _localCell = localCell;
                _tile = tile;
            }

            public Vector2Int LocalCell => _localCell;
            public TileBase Tile => _tile;
        }
    }
}
