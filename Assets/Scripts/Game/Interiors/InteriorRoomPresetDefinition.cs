using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Rootborn.Game.Interiors
{
    [Serializable]
    public readonly struct HomeDesignLayer1GridAnalysis
    {
        public HomeDesignLayer1GridAnalysis(string sourcePath, Vector2Int pixelSize, int cellPixelSize)
        {
            SourcePath = sourcePath ?? string.Empty;
            PixelSize = pixelSize;
            CellPixelSize = Mathf.Max(1, cellPixelSize);
            CellSize = new Vector2Int(PixelSize.x / CellPixelSize, PixelSize.y / CellPixelSize);
            RemainderPixels = new Vector2Int(PixelSize.x % CellPixelSize, PixelSize.y % CellPixelSize);
        }

        public string SourcePath { get; }
        public Vector2Int PixelSize { get; }
        public int CellPixelSize { get; }
        public Vector2Int CellSize { get; }
        public Vector2Int RemainderPixels { get; }
        public bool IsExactGrid => RemainderPixels == Vector2Int.zero;
    }

    public static class HomeDesignLayer1GridAnalyzer
    {
        public static HomeDesignLayer1GridAnalysis Analyze(string sourcePath, int cellPixelSize)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("Home design layer_1 path is required.", nameof(sourcePath));
            }

            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException("Home design layer_1 image was not found.", sourcePath);
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(sourcePath)))
                {
                    throw new InvalidOperationException("Home design layer_1 image could not be decoded: " + sourcePath);
                }

                return new HomeDesignLayer1GridAnalysis(sourcePath, new Vector2Int(texture.width, texture.height), cellPixelSize);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }

    [Serializable]
    public struct InteriorRoomPresetTileCell
    {
        [SerializeField] private Vector2Int _cell;
        [SerializeField] private TileBase _tile;

        public InteriorRoomPresetTileCell(Vector2Int cell, TileBase tile)
        {
            _cell = cell;
            _tile = tile;
        }

        public Vector2Int Cell => _cell;
        public TileBase Tile => _tile;
    }

    [CreateAssetMenu(fileName = "InteriorRoomPreset_New", menuName = "Rootborn/Interiors/Room Preset")]
    public sealed class InteriorRoomPresetDefinition : ScriptableObject
    {
        [SerializeField] private string _stableId = string.Empty;
        [SerializeField] private string _displayName = string.Empty;
        [SerializeField] private Vector2Int _size = Vector2Int.one;
        [SerializeField] private InteriorRoomPresetTileCell[] _baseLayerCells = Array.Empty<InteriorRoomPresetTileCell>();

        public string StableId => _stableId;
        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? _stableId : _displayName;
        public Vector2Int Size => new Vector2Int(Mathf.Max(1, _size.x), Mathf.Max(1, _size.y));
        public IReadOnlyList<InteriorRoomPresetTileCell> BaseLayerCells => _baseLayerCells ?? Array.Empty<InteriorRoomPresetTileCell>();

        public static InteriorRoomPresetDefinition CreateForTests(string stableId, string displayName, Vector2Int size, IEnumerable<InteriorRoomPresetTileCell> baseLayerCells)
        {
            var definition = CreateInstance<InteriorRoomPresetDefinition>();
            definition.Configure(stableId, displayName, size, baseLayerCells);
            return definition;
        }

        public void Configure(string stableId, string displayName, Vector2Int size, IEnumerable<InteriorRoomPresetTileCell> baseLayerCells)
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                throw new ArgumentException("Room preset stable id is required.", nameof(stableId));
            }

            _stableId = stableId;
            _displayName = displayName ?? string.Empty;
            _size = new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
            _baseLayerCells = (baseLayerCells ?? Array.Empty<InteriorRoomPresetTileCell>())
                .Where(cell => cell.Tile != null)
                .Distinct(new InteriorRoomPresetTileCellComparer())
                .ToArray();
        }

        private sealed class InteriorRoomPresetTileCellComparer : IEqualityComparer<InteriorRoomPresetTileCell>
        {
            public bool Equals(InteriorRoomPresetTileCell x, InteriorRoomPresetTileCell y)
            {
                return x.Cell == y.Cell && x.Tile == y.Tile;
            }

            public int GetHashCode(InteriorRoomPresetTileCell obj)
            {
                unchecked
                {
                    return (obj.Cell.GetHashCode() * 397) ^ (obj.Tile != null ? obj.Tile.GetHashCode() : 0);
                }
            }
        }
    }

    public static class InteriorRoomPresetApplier
    {
        public static Vector3Int ResolveCenteredOffset(Vector2Int size)
        {
            return new Vector3Int(-Mathf.Max(1, size.x) / 2, -Mathf.Max(1, size.y) / 2, 0);
        }

        public static void ApplyBaseLayer(InteriorRoomPresetDefinition preset, Tilemap target)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            if (target == null) throw new ArgumentNullException(nameof(target));

            target.ClearAllTiles();
            var offset = ResolveCenteredOffset(preset.Size);
            var cells = preset.BaseLayerCells;
            for (int i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                if (cell.Tile == null)
                {
                    continue;
                }

                target.SetTile(new Vector3Int(cell.Cell.x, cell.Cell.y, 0) + offset, cell.Tile);
            }

            target.CompressBounds();
        }

        public static InteriorGeneratedMap CreatePlacementMap(InteriorRoomPresetDefinition preset)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));

            var size = preset.Size;
            var map = new InteriorGeneratedMap(size.x, size.y);
            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    bool border = x == 0 || y == 0 || x == size.x - 1 || y == size.y - 1;
                    map.SetKind(new Vector2Int(x, y), border ? InteriorCellKind.Wall : InteriorCellKind.Floor);
                }
            }

            int doorX = Mathf.Clamp(size.x / 2, 1, size.x - 2);
            map.SetKind(new Vector2Int(doorX, 0), InteriorCellKind.Door);
            map.SetKind(new Vector2Int(doorX, 1), InteriorCellKind.Floor);
            map.SpawnCell = new Vector2Int(doorX, Mathf.Min(2, size.y - 2));
            map.SetKind(map.SpawnCell, InteriorCellKind.Floor);
            map.RoomCount = 1;
            return map;
        }
    }
}
