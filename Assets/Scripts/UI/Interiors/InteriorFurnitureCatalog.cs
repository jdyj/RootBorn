using System;
using System.Collections.Generic;
using System.Linq;
using Rootborn.Game.Common;
using Rootborn.Game.Interiors;
using Rootborn.Game.Managers;
using Rootborn.Game.Placement;
using UnityEngine;
using UnityEngine.Tilemaps;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Rootborn.UI.Interiors
{
    public readonly struct InteriorFurnitureTile
    {
        public InteriorFurnitureTile(Vector2Int localCell, TileBase tile)
        {
            LocalCell = localCell;
            Tile = tile;
        }

        public Vector2Int LocalCell { get; }
        public TileBase Tile { get; }
    }

    public sealed class InteriorFurnitureDefinition
    {
        private readonly InteriorFurnitureTile[] _tiles;
        private readonly Vector2Int[] _footprint;

        public InteriorFurnitureDefinition(
            string id,
            string displayName,
            InteriorObjectKind objectKind,
            bool blocksMovement,
            InteriorPlacementPreference placementPreference,
            IEnumerable<InteriorFurnitureTile> tiles,
            IEnumerable<Vector2Int> footprint,
            FurnitureDefinition placementDefinition = null)
        {
            Id = id ?? string.Empty;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
            ObjectKind = objectKind;
            BlocksMovement = blocksMovement;
            PlacementPreference = placementPreference;
            PlacementDefinition = placementDefinition;
            _tiles = (tiles ?? Array.Empty<InteriorFurnitureTile>()).Where(tile => tile.Tile != null).ToArray();
            _footprint = (footprint ?? Array.Empty<Vector2Int>()).Distinct().ToArray();
            if (_footprint.Length == 0)
            {
                _footprint = _tiles.Length > 0
                    ? _tiles.Select(tile => tile.LocalCell).Distinct().ToArray()
                    : new[] { Vector2Int.zero };
            }
        }

        public string Id { get; }
        public string DisplayName { get; }
        public InteriorObjectKind ObjectKind { get; }
        public bool BlocksMovement { get; }
        public InteriorPlacementPreference PlacementPreference { get; }
        public FurnitureDefinition PlacementDefinition { get; }
        public IReadOnlyList<InteriorFurnitureTile> Tiles => _tiles;
        public IReadOnlyList<Vector2Int> Footprint => _footprint;

        public static InteriorFurnitureDefinition CreateSingleTileChair(string id, TileBase tile)
        {
            return new InteriorFurnitureDefinition(
                id,
                id,
                InteriorObjectKind.Chair,
                false,
                InteriorPlacementPreference.AvoidCorridor,
                new[] { new InteriorFurnitureTile(Vector2Int.zero, tile) },
                new[] { Vector2Int.zero });
        }
    }

    public static class InteriorFurnitureCatalog
    {
        private const string RegistryPath = "Assets/Data/Registry/GameDataRegistry.asset";

        public static List<InteriorFurnitureDefinition> LoadFurniture()
        {
            return LoadFurniture(ResolveRegistry());
        }

        public static List<InteriorFurnitureDefinition> LoadFurniture(GameDataRegistry registry)
        {
            if (registry == null || registry.FurnitureDefinitions == null)
            {
                return new List<InteriorFurnitureDefinition>();
            }

            return registry.FurnitureDefinitions
                .Where(definition => definition != null)
                .OrderBy(definition => definition.Category, StringComparer.Ordinal)
                .ThenBy(definition => definition.StableId, StringComparer.Ordinal)
                .Select(Convert)
                .Where(definition => definition != null)
                .ToList();
        }

        public static List<InteriorFurnitureDefinition> LoadChairFurniture()
        {
            return LoadChairFurniture(ResolveRegistry());
        }

        public static List<InteriorFurnitureDefinition> LoadChairFurniture(GameDataRegistry registry)
        {
            return LoadFurniture(registry)
                .Where(definition => definition.ObjectKind == InteriorObjectKind.Chair)
                .ToList();
        }

        private static InteriorFurnitureDefinition Convert(FurnitureDefinition definition)
        {
            if (definition == null)
            {
                return null;
            }

            var parts = definition.TileParts
                .Where(part => part.Tile != null)
                .Select(part => new InteriorFurnitureTile(part.LocalCell, part.Tile))
                .ToArray();
            if (parts.Length == 0)
            {
                return null;
            }

            return new InteriorFurnitureDefinition(
                definition.StableId,
                definition.DisplayName,
                ToInteriorObjectKind(definition.Category),
                definition.BlocksMovement,
                InteriorPlacementPreference.AvoidCorridor,
                parts,
                definition.FootprintCells,
                definition);
        }

        private static InteriorObjectKind ToInteriorObjectKind(string category)
        {
            if (string.Equals(category, "chair", StringComparison.OrdinalIgnoreCase)) return InteriorObjectKind.Chair;
            if (string.Equals(category, "desk", StringComparison.OrdinalIgnoreCase)) return InteriorObjectKind.Desk;
            if (string.Equals(category, "sofa", StringComparison.OrdinalIgnoreCase)) return InteriorObjectKind.Sofa;
            if (string.Equals(category, "plant", StringComparison.OrdinalIgnoreCase)) return InteriorObjectKind.Plant;
            return InteriorObjectKind.OfficeProp;
        }

        private static GameDataRegistry ResolveRegistry()
        {
            var registry = Managers.Data != null ? Managers.Data.Registry : null;
            if (HasFurnitureDefinitions(registry))
            {
                return registry;
            }

#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<GameDataRegistry>(RegistryPath);
#else
            return registry;
#endif
        }

        private static bool HasFurnitureDefinitions(GameDataRegistry registry)
        {
            return registry != null && registry.FurnitureDefinitions != null && registry.FurnitureDefinitions.Length > 0;
        }
    }
}
