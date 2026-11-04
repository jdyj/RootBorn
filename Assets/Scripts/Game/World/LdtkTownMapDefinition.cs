using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rootborn.Game.World
{
    [CreateAssetMenu(fileName = "LdtkTownMapDefinition", menuName = "Rootborn/World/LDtk Town Map")]
    public sealed class LdtkTownMapDefinition : ScriptableObject
    {
        [SerializeField] private Object _projectAsset;
        [SerializeField] private Sprite _compositeSprite;
        [SerializeField] private int _pixelsPerUnit = 32;
        [SerializeField] private Vector2 _worldCenter;

        public Object ProjectAsset => _projectAsset;
        public Sprite CompositeSprite => _compositeSprite;
        public int PixelsPerUnit => _pixelsPerUnit <= 0 ? 32 : _pixelsPerUnit;
        public Vector2 WorldCenter => _worldCenter;

        public LdtkTownMapAnalysis Analyze()
        {
            string projectJson = GetProjectJsonText();
            if (string.IsNullOrWhiteSpace(projectJson))
            {
                return LdtkTownMapAnalysis.Empty(_compositeSprite, PixelsPerUnit);
            }

            string projectAssetPath = GetProjectAssetPath();
            using JsonDocument document = JsonDocument.Parse(projectJson);
            JsonElement root = document.RootElement;
            JsonElement levels = root.GetProperty("levels");
            int levelCount = levels.GetArrayLength();
            if (levelCount == 0)
            {
                return LdtkTownMapAnalysis.Empty(_compositeSprite, PixelsPerUnit);
            }

            var layersByName = new Dictionary<string, MutableLayerSummary>(StringComparer.Ordinal);
            bool hasGameplayLayers = false;
            int gridSize = PixelsPerUnit;
            string firstIdentifier = string.Empty;
            bool hasBounds = false;
            int minX = 0;
            int minY = 0;
            int maxX = 0;
            int maxY = 0;

            foreach (JsonElement level in levels.EnumerateArray())
            {
                if (string.IsNullOrEmpty(firstIdentifier) && level.TryGetProperty("identifier", out JsonElement idProperty))
                {
                    firstIdentifier = idProperty.GetString() ?? string.Empty;
                }

                int worldX = level.TryGetProperty("worldX", out JsonElement worldXProperty) ? worldXProperty.GetInt32() : 0;
                int worldY = level.TryGetProperty("worldY", out JsonElement worldYProperty) ? worldYProperty.GetInt32() : 0;
                int pixelWidth = level.TryGetProperty("pxWid", out JsonElement widthProperty) ? widthProperty.GetInt32() : 0;
                int pixelHeight = level.TryGetProperty("pxHei", out JsonElement heightProperty) ? heightProperty.GetInt32() : 0;
                if (!hasBounds)
                {
                    minX = worldX;
                    minY = worldY;
                    maxX = worldX + pixelWidth;
                    maxY = worldY + pixelHeight;
                    hasBounds = true;
                }
                else
                {
                    minX = Math.Min(minX, worldX);
                    minY = Math.Min(minY, worldY);
                    maxX = Math.Max(maxX, worldX + pixelWidth);
                    maxY = Math.Max(maxY, worldY + pixelHeight);
                }

                if (level.TryGetProperty("layerInstances", out JsonElement inlineLayers) && inlineLayers.ValueKind == JsonValueKind.Array)
                {
                    ProcessLayerInstances(inlineLayers, layersByName, ref hasGameplayLayers, ref gridSize);
                    continue;
                }

                if (!level.TryGetProperty("externalRelPath", out JsonElement externalPathProperty))
                {
                    continue;
                }

                string externalRelativePath = externalPathProperty.GetString() ?? string.Empty;
                string externalJson = GetExternalLevelJsonText(projectAssetPath, externalRelativePath);
                if (string.IsNullOrWhiteSpace(externalJson))
                {
                    continue;
                }

                using JsonDocument externalDocument = JsonDocument.Parse(externalJson);
                if (externalDocument.RootElement.TryGetProperty("layerInstances", out JsonElement externalLayers) && externalLayers.ValueKind == JsonValueKind.Array)
                {
                    ProcessLayerInstances(externalLayers, layersByName, ref hasGameplayLayers, ref gridSize);
                }
            }

            var layers = new List<LdtkTownLayerSummary>(layersByName.Count);
            foreach (MutableLayerSummary layer in layersByName.Values)
            {
                layers.Add(layer.ToSummary());
            }

            return new LdtkTownMapAnalysis(
                firstIdentifier,
                levelCount,
                new Vector2Int(Math.Max(0, maxX - minX), Math.Max(0, maxY - minY)),
                gridSize,
                layers.ToArray(),
                hasGameplayLayers,
                _compositeSprite,
                PixelsPerUnit);
        }

        public void Configure(Object projectAsset, Sprite compositeSprite, int pixelsPerUnit, Vector2 worldCenter)
        {
            _projectAsset = projectAsset;
            _compositeSprite = compositeSprite;
            _pixelsPerUnit = pixelsPerUnit;
            _worldCenter = worldCenter;
        }

        public void ConfigureForTests(Object projectAsset, Sprite compositeSprite, int pixelsPerUnit, Vector2 worldCenter)
        {
            Configure(projectAsset, compositeSprite, pixelsPerUnit, worldCenter);
        }

        private string GetProjectJsonText()
        {
            if (_projectAsset is TextAsset textAsset)
            {
                return textAsset.text;
            }

            string assetPath = GetProjectAssetPath();
            if (!string.IsNullOrEmpty(assetPath) && File.Exists(assetPath))
            {
                return File.ReadAllText(assetPath);
            }

            return string.Empty;
        }

        private string GetProjectAssetPath()
        {
#if UNITY_EDITOR
            if (_projectAsset != null)
            {
                return UnityEditor.AssetDatabase.GetAssetPath(_projectAsset);
            }
#endif
            return string.Empty;
        }

        private static string GetExternalLevelJsonText(string projectAssetPath, string externalRelativePath)
        {
            if (string.IsNullOrEmpty(projectAssetPath) || string.IsNullOrEmpty(externalRelativePath))
            {
                return string.Empty;
            }

            string projectDirectory = Path.GetDirectoryName(projectAssetPath) ?? string.Empty;
            string externalPath = Path.GetFullPath(Path.Combine(projectDirectory, externalRelativePath));
            if (!File.Exists(externalPath))
            {
                return string.Empty;
            }

            return File.ReadAllText(externalPath);
        }

        private static void ProcessLayerInstances(JsonElement layerInstances, Dictionary<string, MutableLayerSummary> layersByName, ref bool hasGameplayLayers, ref int gridSize)
        {
            foreach (JsonElement layer in layerInstances.EnumerateArray())
            {
                string layerName = layer.GetProperty("__identifier").GetString() ?? string.Empty;
                string layerType = layer.TryGetProperty("__type", out JsonElement typeProperty) ? typeProperty.GetString() ?? string.Empty : string.Empty;
                int cellWidth = layer.TryGetProperty("__cWid", out JsonElement cWidProperty) ? cWidProperty.GetInt32() : 0;
                int cellHeight = layer.TryGetProperty("__cHei", out JsonElement cHeiProperty) ? cHeiProperty.GetInt32() : 0;
                int layerGridSize = layer.TryGetProperty("__gridSize", out JsonElement gridProperty) ? gridProperty.GetInt32() : gridSize;
                if (layerGridSize > 0)
                {
                    gridSize = layerGridSize;
                }

                int tileCount = CountArray(layer, "gridTiles") + CountArray(layer, "autoLayerTiles");
                int entityCount = CountArray(layer, "entityInstances");
                int intGridCount = layer.TryGetProperty("intGridCsv", out JsonElement intGridCsv) ? CountNonZeroIntGridValues(intGridCsv) : 0;
                hasGameplayLayers |= string.Equals(layerType, "Entities", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(layerType, "IntGrid", StringComparison.OrdinalIgnoreCase)
                    || entityCount > 0
                    || intGridCount > 0;

                if (!layersByName.TryGetValue(layerName, out MutableLayerSummary summary))
                {
                    summary = new MutableLayerSummary(layerName, layerType, cellWidth, cellHeight);
                    layersByName.Add(layerName, summary);
                }

                summary.TileCount += tileCount;
                summary.EntityCount += entityCount;
                summary.IntGridCellCount += intGridCount;
            }
        }

        private static int CountArray(JsonElement parent, string propertyName)
        {
            return parent.TryGetProperty(propertyName, out JsonElement array) && array.ValueKind == JsonValueKind.Array ? array.GetArrayLength() : 0;
        }

        private static int CountNonZeroIntGridValues(JsonElement values)
        {
            int count = 0;
            foreach (JsonElement value in values.EnumerateArray())
            {
                if (value.GetInt32() != 0)
                {
                    count++;
                }
            }

            return count;
        }

        private sealed class MutableLayerSummary
        {
            public MutableLayerSummary(string identifier, string type, int cellWidth, int cellHeight)
            {
                Identifier = identifier ?? string.Empty;
                Type = type ?? string.Empty;
                CellWidth = cellWidth;
                CellHeight = cellHeight;
            }

            public string Identifier { get; }
            public string Type { get; }
            public int CellWidth { get; }
            public int CellHeight { get; }
            public int TileCount { get; set; }
            public int EntityCount { get; set; }
            public int IntGridCellCount { get; set; }

            public LdtkTownLayerSummary ToSummary()
            {
                return new LdtkTownLayerSummary(Identifier, Type, CellWidth, CellHeight, TileCount, EntityCount, IntGridCellCount);
            }
        }
    }

    public readonly struct LdtkTownMapAnalysis
    {
        private readonly Dictionary<string, int> _tileCountsByLayer;

        public LdtkTownMapAnalysis(string levelIdentifier, int levelCount, Vector2Int pixelSize, int gridSize, LdtkTownLayerSummary[] layers, bool hasGameplayLayers, Sprite compositeSprite, int pixelsPerUnit)
        {
            LevelIdentifier = levelIdentifier ?? string.Empty;
            LevelCount = Math.Max(0, levelCount);
            PixelSize = pixelSize;
            GridSize = gridSize <= 0 ? 32 : gridSize;
            Layers = layers ?? Array.Empty<LdtkTownLayerSummary>();
            HasGameplayLayers = hasGameplayLayers;
            CompositePixelSize = compositeSprite != null ? Vector2Int.RoundToInt(compositeSprite.rect.size) : Vector2Int.zero;
            int ppu = pixelsPerUnit <= 0 ? 32 : pixelsPerUnit;
            CompositeWorldSize = compositeSprite != null ? compositeSprite.rect.size / ppu : Vector2.zero;
            CompositeMatchesPixelSize = compositeSprite != null && CompositePixelSize == pixelSize && Mathf.Approximately(compositeSprite.pixelsPerUnit, ppu);

            _tileCountsByLayer = new Dictionary<string, int>(StringComparer.Ordinal);
            int totalTileCount = 0;
            for (int i = 0; i < Layers.Length; i++)
            {
                _tileCountsByLayer[Layers[i].Identifier] = Layers[i].TileCount;
                totalTileCount += Layers[i].TileCount;
            }

            TotalTileCount = totalTileCount;
        }

        public string LevelIdentifier { get; }
        public int LevelCount { get; }
        public Vector2Int PixelSize { get; }
        public Vector2Int CellSize => GridSize <= 0 ? Vector2Int.zero : new Vector2Int(PixelSize.x / GridSize, PixelSize.y / GridSize);
        public int GridSize { get; }
        public LdtkTownLayerSummary[] Layers { get; }
        public int LayerCount => Layers.Length;
        public int TotalTileCount { get; }
        public bool HasGameplayLayers { get; }
        public Vector2Int CompositePixelSize { get; }
        public Vector2 CompositeWorldSize { get; }
        public bool CompositeMatchesPixelSize { get; }

        public int GetLayerTileCount(string layerIdentifier)
        {
            if (string.IsNullOrEmpty(layerIdentifier) || _tileCountsByLayer == null)
            {
                return 0;
            }

            return _tileCountsByLayer.TryGetValue(layerIdentifier, out int count) ? count : 0;
        }

        public static LdtkTownMapAnalysis Empty(Sprite compositeSprite, int pixelsPerUnit)
        {
            return new LdtkTownMapAnalysis(string.Empty, 0, Vector2Int.zero, pixelsPerUnit, Array.Empty<LdtkTownLayerSummary>(), false, compositeSprite, pixelsPerUnit);
        }
    }

    public readonly struct LdtkTownLayerSummary
    {
        public LdtkTownLayerSummary(string identifier, string type, int cellWidth, int cellHeight, int tileCount, int entityCount, int intGridCellCount)
        {
            Identifier = identifier ?? string.Empty;
            Type = type ?? string.Empty;
            CellSize = new Vector2Int(cellWidth, cellHeight);
            TileCount = tileCount;
            EntityCount = entityCount;
            IntGridCellCount = intGridCellCount;
        }

        public string Identifier { get; }
        public string Type { get; }
        public Vector2Int CellSize { get; }
        public int TileCount { get; }
        public int EntityCount { get; }
        public int IntGridCellCount { get; }
    }
}