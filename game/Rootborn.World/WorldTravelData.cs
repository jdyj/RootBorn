using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Rootborn.Core.World;

namespace Rootborn.World
{
    /// <summary>
    /// 장면 목록과 새 게임 자리를 데이터 내보내기 결과(data/locations)에서 읽는다. Unity WorldTravelCatalogDefinition과
    /// WorldDestinationDefinition이 원본이다. 정의 로더(Rootborn.Data)는 이 두 종류를 아직 만들지 않아(NotPortedYet) 여기서 읽는다.
    /// </summary>
    public static class WorldTravelData
    {
        public const string DestinationsRelativePath = "data/locations/WorldDestinationDefinition.json";
        public const string CatalogRelativePath = "data/locations/WorldTravelCatalogDefinition.json";

        public static WorldTravelCatalog Load(string repoRoot) =>
            Parse(File.ReadAllText(Path.Combine(repoRoot, DestinationsRelativePath)), File.ReadAllText(Path.Combine(repoRoot, CatalogRelativePath)));

        public static WorldTravelCatalog Parse(string destinationsJson, string catalogJson)
        {
            var byId = new Dictionary<string, WorldDestination>(StringComparer.Ordinal);
            using (JsonDocument doc = JsonDocument.Parse(destinationsJson))
                foreach (JsonElement d in doc.RootElement.EnumerateArray())
                {
                    var spawns = new List<string>();
                    if (d.TryGetProperty("spawnIds", out JsonElement s)) foreach (JsonElement id in s.EnumerateArray()) spawns.Add(id.GetString());
                    byId[d.GetProperty("id").GetString()] = new WorldDestination(d.GetProperty("sceneName").GetString(), spawns);
                }

            using (JsonDocument doc = JsonDocument.Parse(catalogJson))
            {
                JsonElement[] catalogs = new List<JsonElement>(doc.RootElement.EnumerateArray()).ToArray();
                if (catalogs.Length != 1) throw new InvalidDataException("장면 목록 정의가 하나가 아니다: " + catalogs.Length);
                JsonElement c = catalogs[0];
                var destinations = new List<WorldDestination>();
                foreach (JsonElement r in c.GetProperty("destinations").EnumerateArray()) destinations.Add(Resolve(byId, r));
                WorldDestination opening = Resolve(byId, c.GetProperty("newGameDestination"));
                return new WorldTravelCatalog(destinations, opening.SceneName, c.GetProperty("newGameSpawnId").GetString());
            }
        }

        private static WorldDestination Resolve(Dictionary<string, WorldDestination> byId, JsonElement reference)
        {
            string id = reference.GetProperty("ref").GetString();
            return byId.TryGetValue(id, out WorldDestination d) ? d : throw new InvalidDataException("장면 정의가 없다: " + id);
        }
    }
}
