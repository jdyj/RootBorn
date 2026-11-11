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
        /// <summary>interior-auto 층 이름 규칙(collision.groundKeys·solidKeys·propPrefixes). null이면 Core 기본값.</summary>
        public List<string> GroundKeys, SolidKeys, PropPrefixes;
        /// <summary>충돌 규칙이 막아도 열어 둘 칸(Unity 씬에 구워진 충돌에 없던 칸). 칸 좌표는 그림 기준.</summary>
        public List<CellPos> OpenCells = new List<CellPos>();
        public string DoorEntity = DefaultDoorEntity;
        /// <summary>문 엔티티에서 목적지 값을 읽는 칸(실내 Where, 마을 Door). 맵 목록에 없으면 Where.</summary>
        public string DoorField = DefaultDoorField;
        /// <summary>문 엔티티에서 도착 지점을 따로 정하는 칸(실내 Position, 마을 SpawnPointID). 맵 목록에 없으면 Position. 엔티티의 값이 비어 있으면 표의 도착 지점.</summary>
        public string SpawnField = DefaultSpawnField;

        public const string DefaultDoorEntity = "Door", DefaultDoorField = "Where", DefaultSpawnField = "Position";

        /// <summary>충돌 규칙에 넘길 값.</summary>
        public CollisionRuleOptions CollisionOptions => new CollisionRuleOptions(CollisionLayers, GroundKeys, SolidKeys, PropPrefixes);
        /// <summary>
        /// 문 엔티티와 칠한 Collision 층을 그림보다 몇 줄 아래로 읽을지. Unity 판의 LDtk 가져오기는 그림 타일을 LDtk 좌표보다
        /// 한 줄 위에 놓아, 게임에서는 문과 칠한 칸이 그림보다 한 줄 아래에 있었다. 그 판에 맞춰 만든 맵은 1로 둔다.
        /// </summary>
        public int EntityRowShift;
        /// <summary>시각에 따라 화면 색을 바꾸는가(Unity 판 씬에 낮밤 조명이 있었는가).</summary>
        public bool DayNight;
        /// <summary>LDtk 문이 들어선 뒤 열릴 때까지의 시간. null이면 Core 기본값(Unity 실내 문 0.35초).</summary>
        public float? DoorOpenSeconds;
        /// <summary>LDtk에 없는 경계 구역(Unity 씬의 TownLevelTransitionTrigger 등). 들어서면 문처럼 목적지로 간다.</summary>
        public readonly List<MapArea> Areas = new List<MapArea>();
        public Dictionary<string, TravelTarget> Doors = new Dictionary<string, TravelTarget>(StringComparer.Ordinal);
        public Dictionary<string, (float X, float Y)> Spawns = new Dictionary<string, (float X, float Y)>(StringComparer.Ordinal);
        /// <summary>
        /// 임시 연결("temporary": true를 단 문 경로·이동 구역·도착 지점). 새 맵이 오기 전까지 메인 체인을 걸어서 잇는 값이다.
        /// 게임은 다른 값과 똑같이 쓴다. 문서와 검사가 셀 수 있게 따로 모아 둔다.
        /// </summary>
        public readonly List<TemporaryLink> Temporary = new List<TemporaryLink>();
    }

    /// <summary>임시 연결 하나: 무엇(종류와 이름)과 왜.</summary>
    public readonly struct TemporaryLink
    {
        public readonly string Kind, Name, Reason;

        public TemporaryLink(string kind, string name, string reason)
        {
            Kind = kind; Name = name; Reason = reason ?? string.Empty;
        }

        public override string ToString() => Kind + " " + Name + ": " + Reason;
    }

    /// <summary>데이터로 정한 이동 구역 하나: 맵 픽셀 사각형(그림 기준, 줄 밀기 없음), 이름, 목적지, 열리는 시간.</summary>
    public sealed class MapArea
    {
        public string Name;
        public AreaRect Rect;
        public TravelTarget Destination;
        public float? OpenSeconds;
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
                };
                if (m.TryGetProperty("doorField", out JsonElement df)) entry.DoorField = df.GetString();
                if (m.TryGetProperty("spawnField", out JsonElement sf)) entry.SpawnField = sf.GetString();
                if (!seen.Add(entry.Id)) throw new InvalidDataException(sourceName + ": 맵 id가 겹친다: " + entry.Id);
                if (m.TryGetProperty("doorEntity", out JsonElement de)) entry.DoorEntity = de.GetString();
                if (m.TryGetProperty("entityRowShift", out JsonElement shift)) entry.EntityRowShift = shift.GetInt32();
                if (m.TryGetProperty("dayNight", out JsonElement dayNight)) entry.DayNight = dayNight.GetBoolean();
                if (m.TryGetProperty("doorOpenSeconds", out JsonElement openSeconds)) entry.DoorOpenSeconds = openSeconds.GetSingle();
                if (m.TryGetProperty("areas", out JsonElement areas))
                    foreach (JsonElement a in areas.EnumerateArray())
                    {
                        JsonElement r = a.GetProperty("rect"), to = a.GetProperty("to");
                        NoteTemporary(entry, a, "area", Required(a, "name", sourceName));
                        entry.Areas.Add(new MapArea
                        {
                            Name = Required(a, "name", sourceName),
                            Rect = new AreaRect(r[0].GetSingle(), r[1].GetSingle(), r[2].GetSingle(), r[3].GetSingle()),
                            Destination = new TravelTarget(Required(to, "map", sourceName), Required(to, "spawn", sourceName)),
                            OpenSeconds = a.TryGetProperty("openSeconds", out JsonElement s) ? s.GetSingle() : (float?)null,
                        });
                    }
                JsonElement collision = m.GetProperty("collision");
                entry.CollisionRule = Required(collision, "rule", sourceName);
                if (collision.TryGetProperty("layers", out JsonElement layers))
                    foreach (JsonElement l in layers.EnumerateArray()) entry.CollisionLayers.Add(l.GetString());
                entry.GroundKeys = Strings(collision, "groundKeys");
                entry.SolidKeys = Strings(collision, "solidKeys");
                entry.PropPrefixes = Strings(collision, "propPrefixes");
                CollisionRules.Create(entry.CollisionRule, entry.CollisionOptions); // 모르는 규칙은 여기서 알린다
                if (collision.TryGetProperty("openCells", out JsonElement open))
                    foreach (JsonElement c in open.EnumerateArray()) entry.OpenCells.Add(new CellPos(c[0].GetInt32(), c[1].GetInt32()));
                foreach (JsonProperty door in m.GetProperty("doors").EnumerateObject())
                {
                    entry.Doors[door.Name] = new TravelTarget(Required(door.Value, "map", sourceName), Required(door.Value, "spawn", sourceName));
                    NoteTemporary(entry, door.Value, "door", door.Name);
                }
                foreach (JsonProperty spawn in m.GetProperty("spawns").EnumerateObject())
                {
                    // [x, y] 또는 { "at": [x, y], "temporary": true, "reason": "..." }
                    JsonElement at = spawn.Value.ValueKind == JsonValueKind.Object ? spawn.Value.GetProperty("at") : spawn.Value;
                    entry.Spawns[spawn.Name] = (at[0].GetSingle(), at[1].GetSingle());
                    NoteTemporary(entry, spawn.Value, "spawn", spawn.Name);
                }
                catalog.Maps.Add(entry);
            }
            return catalog;
        }

        private static void NoteTemporary(MapEntry entry, JsonElement e, string kind, string name)
        {
            if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty("temporary", out JsonElement t) || t.ValueKind != JsonValueKind.True) return;
            string reason = e.TryGetProperty("reason", out JsonElement r) ? r.GetString() : null;
            if (string.IsNullOrWhiteSpace(reason)) throw new InvalidDataException(entry.Id + " " + kind + " " + name + ": 임시 연결에는 reason을 적는다");
            entry.Temporary.Add(new TemporaryLink(kind, name, reason));
        }

        private static List<string> Strings(JsonElement e, string name)
        {
            if (!e.TryGetProperty(name, out JsonElement v)) return null;
            var list = new List<string>();
            foreach (JsonElement s in v.EnumerateArray()) list.Add(s.GetString());
            return list;
        }

        private static string Required(JsonElement e, string name, string source)
        {
            if (!e.TryGetProperty(name, out JsonElement v) || v.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(v.GetString()))
                throw new InvalidDataException(source + ": '" + name + "' 값이 없다");
            return v.GetString();
        }
    }
}
