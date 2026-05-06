using System;
using System.Collections.Generic;
using System.Text;
using Rootborn.Game.Resources;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Rootborn.Game.WorldGeneration
{
    public static class SeededWorldGenerator
    {
        public readonly struct PropPlacement
        {
            public readonly ResourceNodeDefinition Resource;
            public readonly Vector2Int Cell;

            public PropPlacement(ResourceNodeDefinition resource, Vector2Int cell)
            {
                Resource = resource;
                Cell = cell;
            }
        }

        public sealed class GeneratedWorld
        {
            private readonly TileBase[,] _tiles;

            public GeneratedWorld(int width, int height, TileBase[,] tiles, PropPlacement[] props)
            {
                Width = Mathf.Max(1, width);
                Height = Mathf.Max(1, height);
                _tiles = tiles;
                Props = props ?? Array.Empty<PropPlacement>();
            }

            public int Width { get; }
            public int Height { get; }
            public PropPlacement[] Props { get; }

            public TileBase GetTile(int x, int y)
            {
                if (x < 0 || y < 0 || x >= Width || y >= Height) return null;
                return _tiles[x, y];
            }

            public string Signature()
            {
                var builder = new StringBuilder(Width * Height * 2 + Props.Length * 16);
                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        var tile = GetTile(x, y);
                        builder.Append(tile != null ? tile.GetInstanceID() : 0);
                        builder.Append(',');
                    }
                }

                builder.Append('|');
                for (int i = 0; i < Props.Length; i++)
                {
                    builder.Append(Props[i].Resource != null ? Props[i].Resource.GetInstanceID() : 0);
                    builder.Append('@');
                    builder.Append(Props[i].Cell.x);
                    builder.Append(':');
                    builder.Append(Props[i].Cell.y);
                    builder.Append(';');
                }

                return builder.ToString();
            }
        }

        public static GeneratedWorld Generate(TerrainGenerationDefinition definition, int worldSeed, int tileSeed)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            int width = definition.Width;
            int height = definition.Height;
            var tiles = new TileBase[width, height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var variants = definition.BasePattern != null ? definition.BasePattern.GetVariantSet(x, y) : null;
                    tiles[x, y] = variants != null ? variants.Pick(tileSeed, x, y, 0) : null;
                }
            }

            var props = GenerateProps(definition, worldSeed);
            return new GeneratedWorld(width, height, tiles, props.ToArray());
        }

        private static List<PropPlacement> GenerateProps(TerrainGenerationDefinition definition, int propSeed)
        {
            var placements = new List<PropPlacement>();
            var occupied = new HashSet<Vector2Int>();
            var spawns = definition.NaturalPropSpawns;
            if (spawns == null || spawns.Length == 0)
            {
                return placements;
            }

            for (int i = 0; i < spawns.Length; i++)
            {
                var spawn = spawns[i];
                int targetCount = spawn != null ? spawn.TargetCount : 0;
                if (spawn == null || spawn.Resource == null || targetCount <= 0)
                {
                    continue;
                }

                var rng = new System.Random(Hash(propSeed, i, targetCount, spawn.SpawnableArea.xMin));
                int placedForRule = 0;
                int attempts = 0;
                while (placedForRule < targetCount && attempts < spawn.MaxAttempts)
                {
                    attempts++;
                    var area = spawn.SpawnableArea;
                    if (area.width <= 0 || area.height <= 0)
                    {
                        break;
                    }

                    var anchor = new Vector2Int(
                        rng.Next(area.xMin, area.xMax),
                        rng.Next(area.yMin, area.yMax));

                    if (!TryAddPlacement(definition, spawn, anchor, placements, occupied))
                    {
                        continue;
                    }

                    placedForRule++;
                    for (int clusterIndex = 1; clusterIndex < spawn.ClusterSize && placedForRule < targetCount; clusterIndex++)
                    {
                        if (TryAddClusterNeighbor(definition, spawn, anchor, clusterIndex, placements, occupied))
                        {
                            placedForRule++;
                        }
                    }
                }
            }

            placements.Sort((a, b) =>
            {
                int y = a.Cell.y.CompareTo(b.Cell.y);
                return y != 0 ? y : a.Cell.x.CompareTo(b.Cell.x);
            });
            return placements;
        }

        private static bool TryAddClusterNeighbor(TerrainGenerationDefinition definition, NaturalPropSpawnDefinition spawn, Vector2Int anchor, int clusterIndex, List<PropPlacement> placements, HashSet<Vector2Int> occupied)
        {
            int radius = spawn.ClusterRadius;
            if (radius <= 0)
            {
                return false;
            }

            int diameter = radius * 2 + 1;
            int candidateCount = diameter * diameter - 1;
            for (int i = 0; i < candidateCount; i++)
            {
                int offsetIndex = (clusterIndex + i) % candidateCount;
                int ox = offsetIndex % diameter - radius;
                int oy = offsetIndex / diameter - radius;
                if (ox == 0 && oy == 0)
                {
                    continue;
                }

                var cell = new Vector2Int(anchor.x + ox, anchor.y + oy);
                if (TryAddPlacement(definition, spawn, cell, placements, occupied))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryAddPlacement(TerrainGenerationDefinition definition, NaturalPropSpawnDefinition spawn, Vector2Int cell, List<PropPlacement> placements, HashSet<Vector2Int> occupied)
        {
            if (!spawn.SpawnableArea.Contains(cell)) return false;
            if (occupied.Contains(cell)) return false;
            if (IsReserved(definition.ReservedAreas, cell)) return false;
            if (!IsSpaced(placements, cell, spawn.MinDistanceBetweenProps)) return false;

            placements.Add(new PropPlacement(spawn.Resource, cell));
            occupied.Add(cell);
            return true;
        }

        private static bool IsReserved(TerrainReservedArea[] reservedAreas, Vector2Int cell)
        {
            if (reservedAreas == null) return false;
            for (int i = 0; i < reservedAreas.Length; i++)
            {
                if (reservedAreas[i].Contains(cell)) return true;
            }
            return false;
        }

        private static bool IsSpaced(List<PropPlacement> placements, Vector2Int cell, int minSpacing)
        {
            if (minSpacing <= 0) return true;
            int minSqr = minSpacing * minSpacing;
            for (int i = 0; i < placements.Count; i++)
            {
                if ((placements[i].Cell - cell).sqrMagnitude < minSqr)
                {
                    return false;
                }
            }
            return true;
        }

        private static int Hash(int a, int b, int c, int d)
        {
            unchecked
            {
                int h = a;
                h = (h * 397) ^ b;
                h = (h * 397) ^ c;
                h = (h * 397) ^ d;
                return h;
            }
        }
    }
}
