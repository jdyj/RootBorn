using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Rootborn.World.Ldtk
{
    /// <summary>타일 하나: 층 안 픽셀 위치(왼쪽 위), 타일셋 안 원본 위치, 뒤집기(1 가로, 2 세로), 불투명도.</summary>
    public readonly struct LdtkTile
    {
        public readonly int Px, Py, SrcX, SrcY, Flip;
        public readonly float Alpha;

        public LdtkTile(int px, int py, int srcX, int srcY, int flip, float alpha)
        {
            Px = px; Py = py; SrcX = srcX; SrcY = srcY; Flip = flip; Alpha = alpha;
        }
    }

    /// <summary>엔티티 하나. X·Y는 피벗 자리(LDtk "px"), 그림이 있으면 타일셋과 원본 사각형.</summary>
    public sealed class LdtkEntity
    {
        public string Name;
        public int X, Y, Width, Height;
        public float PivotX, PivotY;
        public string TilesetPath;
        public int TileX, TileY, TileW, TileH;
        public readonly Dictionary<string, string> Fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public bool HasTile => TilesetPath != null && TileW > 0 && TileH > 0;
        public float Left => X - PivotX * Width;
        public float Top => Y - PivotY * Height;

        public string Field(string name) => name != null && Fields.TryGetValue(name, out string value) ? value : null;
    }

    public sealed class LdtkLayer
    {
        public string Name;
        public string Type;
        public int GridSize, Columns, Rows, OffsetX, OffsetY;
        public float Opacity = 1f;
        public bool Visible = true;
        /// <summary>타일셋 PNG의 절대 경로. 타일셋이 없는 층은 null.</summary>
        public string TilesetPath;
        public bool TilesetMissing;
        /// <summary>층 정의의 태그(uiFilterTags). "art"는 엔티티 그림을 게임에서 그린다는 뜻, "ysort"는 캐릭터와 발 위치로 앞뒤를 가린다는 뜻.</summary>
        public readonly HashSet<string> Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        /// <summary>LDtk 파일 안 순서대로(같은 칸에 겹친 타일은 뒤의 것이 위).</summary>
        public readonly List<LdtkTile> Tiles = new List<LdtkTile>();
        public int[] IntGrid;
        public readonly List<LdtkEntity> Entities = new List<LdtkEntity>();
    }

    public sealed class LdtkLevel
    {
        public string Name;
        public int PixelWidth, PixelHeight;
        public string BackgroundColor;
        /// <summary>아래 층부터 위 층 순서(그리는 순서). LDtk 파일은 위 층이 먼저라 뒤집어 담는다.</summary>
        public readonly List<LdtkLayer> Layers = new List<LdtkLayer>();

        public LdtkLayer Layer(string name)
        {
            foreach (LdtkLayer layer in Layers) if (layer.Name == name) return layer;
            return null;
        }

        public IEnumerable<LdtkEntity> Entities(string identifier)
        {
            foreach (LdtkLayer layer in Layers)
                foreach (LdtkEntity e in layer.Entities)
                    if (string.Equals(e.Name, identifier, StringComparison.OrdinalIgnoreCase)) yield return e;
        }
    }

    public sealed class LdtkTileset
    {
        public int Uid;
        public string Identifier;
        public string RelPath;
        public string Path;
        public int Width, Height, GridSize;
        public bool Missing;
    }

    /// <summary>
    /// LDtk 프로젝트 파일(.ldtk)과 외부 레벨(.ldtkl). JSON을 그대로 읽는다. 타일셋 PNG 경로는 프로젝트 파일 기준 상대 경로를 풀어 둔다.
    /// 저장소에 없는 PNG를 가리키는 타일셋은 Missing으로 표시하고, 그 타일셋을 쓰는 층은 그리지 않는다.
    /// </summary>
    public sealed class LdtkProject
    {
        public string Path;
        public readonly Dictionary<int, LdtkTileset> Tilesets = new Dictionary<int, LdtkTileset>();
        private readonly Dictionary<string, HashSet<string>> _layerTags = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, JsonElement> _inlineLevels = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _externalLevels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private JsonDocument _document;

        public IEnumerable<string> LevelNames
        {
            get
            {
                foreach (string name in _externalLevels.Keys) yield return name;
                foreach (string name in _inlineLevels.Keys) yield return name;
            }
        }

        public static LdtkProject Load(string path)
        {
            var project = new LdtkProject { Path = System.IO.Path.GetFullPath(path) };
            string folder = System.IO.Path.GetDirectoryName(project.Path);
            project._document = Parse(project.Path);
            JsonElement root = project._document.RootElement;
            JsonElement defs = root.GetProperty("defs");
            foreach (JsonElement ts in defs.GetProperty("tilesets").EnumerateArray())
            {
                string rel = ts.TryGetProperty("relPath", out JsonElement r) && r.ValueKind == JsonValueKind.String ? r.GetString() : null;
                var tileset = new LdtkTileset
                {
                    Uid = ts.GetProperty("uid").GetInt32(),
                    Identifier = ts.GetProperty("identifier").GetString(),
                    RelPath = rel,
                    Width = ts.GetProperty("pxWid").GetInt32(),
                    Height = ts.GetProperty("pxHei").GetInt32(),
                    GridSize = ts.GetProperty("tileGridSize").GetInt32(),
                };
                if (rel != null)
                {
                    tileset.Path = System.IO.Path.GetFullPath(System.IO.Path.Combine(folder, rel));
                    tileset.Missing = !File.Exists(tileset.Path);
                }
                else tileset.Missing = true;
                project.Tilesets[tileset.Uid] = tileset;
            }
            foreach (JsonElement layer in defs.GetProperty("layers").EnumerateArray())
            {
                var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (layer.TryGetProperty("uiFilterTags", out JsonElement t) && t.ValueKind == JsonValueKind.Array)
                    foreach (JsonElement tag in t.EnumerateArray()) tags.Add(tag.GetString());
                project._layerTags[layer.GetProperty("identifier").GetString()] = tags;
            }
            foreach (JsonElement level in root.GetProperty("levels").EnumerateArray())
            {
                string name = level.GetProperty("identifier").GetString();
                if (level.TryGetProperty("externalRelPath", out JsonElement ext) && ext.ValueKind == JsonValueKind.String)
                    project._externalLevels[name] = System.IO.Path.GetFullPath(System.IO.Path.Combine(folder, ext.GetString()));
                else project._inlineLevels[name] = level;
            }
            return project;
        }

        private static JsonDocument Parse(string path)
        {
            // File.ReadAllText는 UTF-8 BOM을 지운다(LDtk 파일 일부는 BOM이 붙어 있다).
            return JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { AllowTrailingCommas = true });
        }

        public LdtkLevel LoadLevel(string name)
        {
            if (_inlineLevels.TryGetValue(name, out JsonElement inline)) return ReadLevel(inline);
            if (!_externalLevels.TryGetValue(name, out string file)) throw new InvalidOperationException(Path + "에 레벨 " + name + "이 없다");
            using JsonDocument doc = Parse(file);
            return ReadLevel(doc.RootElement);
        }

        private LdtkLevel ReadLevel(JsonElement root)
        {
            var level = new LdtkLevel
            {
                Name = root.GetProperty("identifier").GetString(),
                PixelWidth = root.GetProperty("pxWid").GetInt32(),
                PixelHeight = root.GetProperty("pxHei").GetInt32(),
                BackgroundColor = root.TryGetProperty("__bgColor", out JsonElement bg) && bg.ValueKind == JsonValueKind.String ? bg.GetString() : null,
            };
            if (!root.TryGetProperty("layerInstances", out JsonElement layers) || layers.ValueKind != JsonValueKind.Array) return level;
            foreach (JsonElement li in layers.EnumerateArray())
            {
                var layer = new LdtkLayer
                {
                    Name = li.GetProperty("__identifier").GetString(),
                    Type = li.GetProperty("__type").GetString(),
                    GridSize = li.GetProperty("__gridSize").GetInt32(),
                    Columns = li.GetProperty("__cWid").GetInt32(),
                    Rows = li.GetProperty("__cHei").GetInt32(),
                    OffsetX = li.GetProperty("__pxTotalOffsetX").GetInt32(),
                    OffsetY = li.GetProperty("__pxTotalOffsetY").GetInt32(),
                    Opacity = li.TryGetProperty("__opacity", out JsonElement op) ? op.GetSingle() : 1f,
                    Visible = !li.TryGetProperty("visible", out JsonElement vis) || vis.GetBoolean(),
                };
                if (_layerTags.TryGetValue(layer.Name, out HashSet<string> tags)) layer.Tags.UnionWith(tags);
                int? tilesetUid = null;
                if (li.TryGetProperty("overrideTilesetUid", out JsonElement over) && over.ValueKind == JsonValueKind.Number) tilesetUid = over.GetInt32();
                else if (li.TryGetProperty("__tilesetDefUid", out JsonElement tsu) && tsu.ValueKind == JsonValueKind.Number) tilesetUid = tsu.GetInt32();
                if (tilesetUid.HasValue && Tilesets.TryGetValue(tilesetUid.Value, out LdtkTileset tileset))
                {
                    layer.TilesetPath = tileset.Path;
                    layer.TilesetMissing = tileset.Missing;
                }
                foreach (string key in new[] { "gridTiles", "autoLayerTiles" })
                {
                    if (!li.TryGetProperty(key, out JsonElement tiles)) continue;
                    foreach (JsonElement t in tiles.EnumerateArray())
                    {
                        JsonElement px = t.GetProperty("px"), src = t.GetProperty("src");
                        float alpha = t.TryGetProperty("a", out JsonElement a) ? a.GetSingle() : 1f;
                        layer.Tiles.Add(new LdtkTile(px[0].GetInt32(), px[1].GetInt32(), src[0].GetInt32(), src[1].GetInt32(), t.GetProperty("f").GetInt32(), alpha));
                    }
                }
                if (li.TryGetProperty("intGridCsv", out JsonElement csv) && csv.ValueKind == JsonValueKind.Array && csv.GetArrayLength() > 0)
                {
                    layer.IntGrid = new int[csv.GetArrayLength()];
                    int i = 0;
                    foreach (JsonElement v in csv.EnumerateArray()) layer.IntGrid[i++] = v.GetInt32();
                }
                if (li.TryGetProperty("entityInstances", out JsonElement entities))
                    foreach (JsonElement e in entities.EnumerateArray()) layer.Entities.Add(ReadEntity(e));
                level.Layers.Insert(0, layer);
            }
            return level;
        }

        private LdtkEntity ReadEntity(JsonElement e)
        {
            JsonElement px = e.GetProperty("px");
            var entity = new LdtkEntity
            {
                Name = e.GetProperty("__identifier").GetString(),
                X = px[0].GetInt32(),
                Y = px[1].GetInt32(),
                Width = e.GetProperty("width").GetInt32(),
                Height = e.GetProperty("height").GetInt32(),
            };
            if (e.TryGetProperty("__pivot", out JsonElement pivot) && pivot.ValueKind == JsonValueKind.Array)
            {
                entity.PivotX = pivot[0].GetSingle();
                entity.PivotY = pivot[1].GetSingle();
            }
            if (e.TryGetProperty("__tile", out JsonElement tile) && tile.ValueKind == JsonValueKind.Object
                && Tilesets.TryGetValue(tile.GetProperty("tilesetUid").GetInt32(), out LdtkTileset ts) && !ts.Missing)
            {
                entity.TilesetPath = ts.Path;
                entity.TileX = tile.GetProperty("x").GetInt32();
                entity.TileY = tile.GetProperty("y").GetInt32();
                entity.TileW = tile.GetProperty("w").GetInt32();
                entity.TileH = tile.GetProperty("h").GetInt32();
            }
            if (e.TryGetProperty("fieldInstances", out JsonElement fields))
                foreach (JsonElement f in fields.EnumerateArray())
                {
                    JsonElement v = f.GetProperty("__value");
                    entity.Fields[f.GetProperty("__identifier").GetString()] = v.ValueKind == JsonValueKind.Null ? null
                        : v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText();
                }
            return entity;
        }
    }
}
