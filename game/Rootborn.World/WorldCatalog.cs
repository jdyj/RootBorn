using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Rootborn.Core.World;

namespace Rootborn.World
{
    /// <summary>맵 목록의 맵 하나. 맵 크기는 여기 적지 않는다(언제나 LDtk 레벨에서 읽는다).</summary>
    public sealed class MapEntry
    {
        public string Id;
        /// <summary>저장소 뿌리 기준 LDtk 프로젝트 경로.</summary>
        public string Ldtk;
        public string Level;
        /// <summary>화면에 보일 높이(칸). Unity 판 카메라 orthographic size × 2.</summary>
        public float ViewCells;
        public string CollisionRule;
        public List<string> CollisionLayers = new List<string>();
        public string DoorEntity = "Door";
        /// <summary>문 엔티티에서 목적지 값을 읽는 칸(실내 Where, 마을 Door).</summary>
        public string DoorField;
        /// <summary>문 엔티티에서 도착 지점을 따로 정하는 칸(실내 Position, 마을 SpawnPointID). 비어 있으면 표의 도착 지점.</summary>
        public string SpawnField;
        /// <summary>
        /// 문 엔티티와 칠한 Collision 층을 그림보다 몇 줄 아래로 읽을지. Unity 판의 LDtk 가져오기는 그림 타일을 LDtk 좌표보다
        /// 한 줄 위에 놓아, 게임에서는 문과 칠한 칸이 그림보다 한 줄 아래에 있었다. 그 판에 맞춰 만든 맵은 1로 둔다.
        /// </summary>
        public int EntityRowShift;
        public Dictionary<string, TravelTarget> Doors = new Dictionary<string, TravelTarget>(StringComparer.Ordinal);
        public Dictionary<string, (float X, float Y)> Spawns = new Dictionary<string, (float X, float Y)>(StringComparer.Ordinal);
    }

    /// <summary>
    /// 맵 목록(game/world/maps.json). Unity 판에서 씬과 코드에 흩어져 있던 값을 데이터로 모았다:
    /// 맵 id(Unity 씬 이름, 세이브의 CurrentScene과 같다)와 LDtk 레벨, 문 값의 목적지, 도착 지점 좌표(LDtk 픽셀), 충돌 규칙.
    /// </summary>
    public sealed class WorldCatalog
    {
        public const string DefaultRelativePath = "game/world/maps.json";

        public WalkerSettings Walker = WalkerSettings.UnityPlayer;
        public readonly List<MapEntry> Maps = new List<MapEntry>();

        /// <summary>id 또는 LDtk 레벨 이름(대소문자 무시)으로 찾는다. 예: "ShopInterior" 또는 "Shop".</summary>
        public MapEntry Find(string idOrLevel)
        {
            foreach (MapEntry m in Maps) if (string.Equals(m.Id, idOrLevel, StringComparison.OrdinalIgnoreCase)) return m;
            foreach (MapEntry m in Maps) if (string.Equals(m.Level, idOrLevel, StringComparison.OrdinalIgnoreCase)) return m;
            return null;
        }

        public static WorldCatalog Load(string path) => Parse(File.ReadAllText(path), path);

        public static WorldCatalog Parse(string json, string sourceName = "maps.json")
        {
            using JsonDocument doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            JsonElement root = doc.RootElement;
            var catalog = new WorldCatalog();
            if (root.TryGetProperty("walker", out JsonElement w))
                catalog.Walker = new WalkerSettings(w.GetProperty("bodyCells").GetSingle(), w.GetProperty("speedCellsPerSecond").GetSingle());
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (JsonElement m in root.GetProperty("maps").EnumerateArray())
            {
                var entry = new MapEntry
                {
                    Id = Required(m, "id", sourceName),
                    Ldtk = Required(m, "ldtk", sourceName),
                    Level = Required(m, "level", sourceName),
                    ViewCells = m.GetProperty("viewCells").GetSingle(),
                    DoorField = Required(m, "doorField", sourceName),
                    SpawnField = m.TryGetProperty("spawnField", out JsonElement sf) ? sf.GetString() : null,
                };
                if (!seen.Add(entry.Id)) throw new InvalidDataException(sourceName + ": 맵 id가 겹친다: " + entry.Id);
                if (m.TryGetProperty("doorEntity", out JsonElement de)) entry.DoorEntity = de.GetString();
                if (m.TryGetProperty("entityRowShift", out JsonElement shift)) entry.EntityRowShift = shift.GetInt32();
                JsonElement collision = m.GetProperty("collision");
                entry.CollisionRule = Required(collision, "rule", sourceName);
                if (collision.TryGetProperty("layers", out JsonElement layers))
                    foreach (JsonElement l in layers.EnumerateArray()) entry.CollisionLayers.Add(l.GetString());
                CollisionRules.Create(entry.CollisionRule, entry.CollisionLayers); // 모르는 규칙은 여기서 알린다
                foreach (JsonProperty door in m.GetProperty("doors").EnumerateObject())
                    entry.Doors[door.Name] = new TravelTarget(Required(door.Value, "map", sourceName), Required(door.Value, "spawn", sourceName));
                foreach (JsonProperty spawn in m.GetProperty("spawns").EnumerateObject())
                    entry.Spawns[spawn.Name] = (spawn.Value[0].GetSingle(), spawn.Value[1].GetSingle());
                catalog.Maps.Add(entry);
            }
            return catalog;
        }

        private static string Required(JsonElement e, string name, string source)
        {
            if (!e.TryGetProperty(name, out JsonElement v) || v.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(v.GetString()))
                throw new InvalidDataException(source + ": '" + name + "' 값이 없다");
            return v.GetString();
        }
    }
}
