using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Rootborn.Core.Interaction;
using Rootborn.Core.NpcMovement;
using Rootborn.Core.World;

namespace Rootborn.World
{
    /// <summary>맵의 상호작용 물건 하나(종류, id, 가운데 픽셀, 우선순위).</summary>
    public sealed class InteractionThing
    {
        public string Kind, Id;
        public float X, Y;
        public int Priority;
        /// <summary>임시 지점인가(새 맵이 오기 전까지 메인 체인을 잇는 자리). 게임은 다른 지점과 똑같이 쓴다.</summary>
        public bool Temporary;
        public string Reason;
    }

    /// <summary>맵 하나의 물건과 NPC 자리(장소 id → 자리 목록).</summary>
    public sealed class MapInteractions
    {
        public static readonly MapInteractions Empty = new MapInteractions();
        public readonly List<InteractionThing> Things = new List<InteractionThing>();
        public readonly Dictionary<string, List<(float X, float Y)>> NpcAnchors = new Dictionary<string, List<(float X, float Y)>>(StringComparer.Ordinal);
        /// <summary>가구를 놓는 방이면 그 면 id(가구 정의의 allowedSurfaceIds, 집은 "house"). 아니면 빈 문자열.</summary>
        public string PlacementSurface = string.Empty;
    }

    /// <summary>상호작용 데이터(game/world/interactions.json).</summary>
    public sealed class InteractionData
    {
        public const string DefaultRelativePath = "game/world/interactions.json";

        public float RadiusCells = InteractionTargets.UnityRadiusCells;
        public int NpcPriority, DoorPriority;
        private readonly Dictionary<string, MapInteractions> _maps = new Dictionary<string, MapInteractions>(StringComparer.Ordinal);

        /// <summary>상호작용이 적힌 맵 id들.</summary>
        public IEnumerable<string> MapIds => _maps.Keys;

        public MapInteractions Map(string mapId) => mapId != null && _maps.TryGetValue(mapId, out MapInteractions m) ? m : MapInteractions.Empty;

        public static InteractionData Load(string path) => Parse(File.ReadAllText(path));

        public static InteractionData Parse(string json)
        {
            using JsonDocument doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            JsonElement root = doc.RootElement;
            var data = new InteractionData
            {
                RadiusCells = root.GetProperty("radiusCells").GetSingle(),
                NpcPriority = root.GetProperty("npcPriority").GetInt32(),
                DoorPriority = root.GetProperty("doorPriority").GetInt32(),
            };
            foreach (JsonProperty map in root.GetProperty("maps").EnumerateObject())
            {
                var m = new MapInteractions();
                if (map.Value.TryGetProperty("things", out JsonElement things))
                    foreach (JsonElement t in things.EnumerateArray())
                    {
                        JsonElement at = t.GetProperty("at");
                        m.Things.Add(new InteractionThing
                        {
                            Kind = t.GetProperty("kind").GetString(), Id = t.GetProperty("id").GetString(),
                            X = at[0].GetSingle(), Y = at[1].GetSingle(), Priority = t.GetProperty("priority").GetInt32(),
                            Temporary = t.TryGetProperty("temporary", out JsonElement tmp) && tmp.ValueKind == JsonValueKind.True,
                            Reason = t.TryGetProperty("reason", out JsonElement why) ? why.GetString() : null,
                        });
                        InteractionThing added = m.Things[m.Things.Count - 1];
                        if (added.Temporary && string.IsNullOrWhiteSpace(added.Reason))
                            throw new InvalidDataException(map.Name + " " + added.Kind + "/" + added.Id + ": 임시 지점에는 reason을 적는다");
                    }
                if (map.Value.TryGetProperty("npcAnchors", out JsonElement anchors))
                    foreach (JsonProperty location in anchors.EnumerateObject())
                    {
                        var list = new List<(float X, float Y)>();
                        foreach (JsonElement p in location.Value.EnumerateArray()) list.Add((p[0].GetSingle(), p[1].GetSingle()));
                        m.NpcAnchors[location.Name] = list;
                    }
                if (map.Value.TryGetProperty("placementSurface", out JsonElement surface)) m.PlacementSurface = surface.GetString() ?? string.Empty;
                data._maps[map.Name] = m;
            }
            return data;
        }
    }

    /// <summary>
    /// 장소 id의 NPC 자리(interactions.json npcAnchors, 레벨 픽셀 그대로). 맵 목록 순서로 찾아 그 장소의 자리가 처음 나오는 맵의 자리를 쓴다
    /// — 그 맵의 자리가 모자라면 다음 맵에서 찾지 않는다. 표는 만들 때 한 번 짓는다.
    /// </summary>
    public sealed class MapAnchors : INpcAnchors
    {
        private readonly Dictionary<string, (string MapId, List<(float X, float Y)> Spots)> _byLocation =
            new Dictionary<string, (string, List<(float, float)>)>(StringComparer.Ordinal);

        public MapAnchors(InteractionData data, IEnumerable<string> mapIds)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (mapIds == null) throw new ArgumentNullException(nameof(mapIds));
            foreach (string mapId in mapIds)
                foreach (var location in data.Map(mapId).NpcAnchors)
                    if (!_byLocation.ContainsKey(location.Key)) _byLocation[location.Key] = (mapId, location.Value);
        }

        public bool TryGet(string locationId, int slot, out NpcSpot spot)
        {
            spot = default;
            if (locationId == null || slot < 0 || !_byLocation.TryGetValue(locationId, out var found) || slot >= found.Spots.Count) return false;
            spot = new NpcSpot(found.MapId, found.Spots[slot].X, found.Spots[slot].Y);
            return true;
        }
    }

    /// <summary>맵에 선 NPC 하나.</summary>
    public readonly struct PlacedNpc
    {
        public readonly string NpcId, LocationId;
        public readonly float X, Y;

        public PlacedNpc(string npcId, string locationId, float x, float y)
        {
            NpcId = npcId; LocationId = locationId; X = x; Y = y;
        }
    }

    /// <summary>
    /// NPC를 맵에 세우는 규칙(Unity LocationNpcRuntimeInstaller): NPC마다 일정이 정한 장소를 묻고, 그 장소에 앞서 배정된 NPC 수가
    /// 자리 번호다(순서는 레지스트리의 NPC 순서). 그 맵에 그 장소의 그 번호 자리가 없으면 세우지 않는다.
    /// </summary>
    public static class NpcPlacement
    {
        public static List<PlacedNpc> Place(IEnumerable<string> npcIdsInOrder, Func<string, string> locationOf, MapInteractions map)
        {
            var placed = new List<PlacedNpc>();
            var used = new Dictionary<string, int>(StringComparer.Ordinal);
            if (npcIdsInOrder == null || locationOf == null || map == null) return placed;
            foreach (string npcId in npcIdsInOrder)
            {
                string location = locationOf(npcId) ?? string.Empty;
                if (location.Length == 0) continue;
                used.TryGetValue(location, out int slot);
                used[location] = slot + 1;
                if (map.NpcAnchors.TryGetValue(location, out var anchors) && slot < anchors.Count)
                    placed.Add(new PlacedNpc(npcId, location, anchors[slot].X, anchors[slot].Y));
            }
            return placed;
        }

        /// <summary>상호작용 후보: 물건, NPC, 자동문(목적지가 있는 문 — 대상은 되지만 키로는 아무 일도 하지 않는다).</summary>
        public static List<InteractionCandidate> Candidates(InteractionData data, MapInteractions map, IEnumerable<PlacedNpc> npcs, IEnumerable<DoorArea> doors)
        {
            var list = new List<InteractionCandidate>();
            foreach (InteractionThing t in map.Things) list.Add(new InteractionCandidate(t.Kind, t.Id, t.X, t.Y, t.Priority, true));
            if (npcs != null) foreach (PlacedNpc n in npcs) list.Add(new InteractionCandidate("npc", n.NpcId, n.X, n.Y, data.NpcPriority, true));
            if (doors != null)
                foreach (DoorArea d in doors)
                    if (d.Destination.HasValue) list.Add(new InteractionCandidate("door", d.Target, d.Area.CenterX, d.Area.CenterY, data.DoorPriority, false));
            return list;
        }
    }

    /// <summary>NPC의 맵 그림(NpcDefinition worldTexture/worldSpriteRect). Unity 사각형(왼쪽 아래 원점)을 PNG 왼쪽 위 원점으로 바꿔 둔다.</summary>
    public sealed class NpcArt
    {
        public const string NpcsRelativePath = "data/npcs/NpcDefinition.json";
        public const string RegistryRelativePath = "data/registry.json";

        private readonly Dictionary<string, CharacterFrame> _frames = new Dictionary<string, CharacterFrame>(StringComparer.Ordinal);

        /// <summary>레지스트리의 NPC 순서(자리 번호를 정한다).</summary>
        public readonly List<string> Order = new List<string>();
        public readonly List<string> Problems = new List<string>();

        public CharacterFrame Frame(string npcId) => npcId != null && _frames.TryGetValue(npcId, out CharacterFrame f) ? f : null;

        public static NpcArt Load(string repoRoot)
        {
            var art = new NpcArt();
            using (JsonDocument registry = JsonDocument.Parse(File.ReadAllText(Path.Combine(repoRoot, RegistryRelativePath))))
                foreach (JsonElement id in registry.RootElement.GetProperty("npcs").EnumerateArray())
                    art.Order.Add(id.ValueKind == JsonValueKind.Object ? id.GetProperty("ref").GetString() : id.GetString());
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(repoRoot, NpcsRelativePath)));
            foreach (JsonElement n in doc.RootElement.EnumerateArray())
            {
                string id = n.GetProperty("id").GetString();
                if (!n.TryGetProperty("worldTexture", out JsonElement tex) || tex.ValueKind != JsonValueKind.Object || !n.TryGetProperty("worldSpriteRect", out JsonElement r))
                {
                    art.Problems.Add(id + "에 맵 그림이 없다");
                    continue;
                }
                string png = Path.GetFullPath(Path.Combine(repoRoot, tex.GetProperty("asset").GetString()));
                if (!File.Exists(png)) { art.Problems.Add(id + " 그림 파일이 없다: " + png); continue; }
                int x = (int)r.GetProperty("x").GetSingle(), y = (int)r.GetProperty("y").GetSingle();
                int w = (int)r.GetProperty("width").GetSingle(), h = (int)r.GetProperty("height").GetSingle();
                int top = PngHeight(png) - y - h;
                art._frames[id] = new CharacterFrame(png, x, top, w, h, 0.5f, 1f);
            }
            return art;
        }

        /// <summary>PNG 머리(IHDR)의 높이.</summary>
        public static int PngHeight(string path)
        {
            using FileStream s = File.OpenRead(path);
            var header = new byte[24];
            if (s.Read(header, 0, 24) != 24 || header[1] != 'P' || header[2] != 'N' || header[3] != 'G') throw new InvalidDataException("PNG가 아니다: " + path);
            return (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
        }
    }
}
