using System;
using System.Collections.Generic;
using System.IO;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;
using Rootborn.Core.Room;
using Rootborn.Core.World;
using Rootborn.Data;
using Rootborn.World;

namespace Rootborn.Server
{
    /// <summary>
    /// 같이 쓰는 집의 판정 칸(방 쪽): 가구를 놓는 맵(game/world/interactions.json의 placementSurface)마다 그 맵(LDtk)에서 방 칸을 만든다.
    /// 플레이어 쪽 화면(Rootborn.Screens FurnitureRoom)이 GameSession.EnterHouse에 넘기는 칸과 같아야 한다 — 맵 안의 칸은 바닥, 막힌 칸은 고정 칸,
    /// 도착 지점 칸(id 순)은 들어오는 칸과 문 앞, 가구 칸의 y는 위로 커진다(맵 칸 y를 뒤집는다). 같은지는 HouseGridsTests가 본다.
    /// 맵은 처음 물을 때 읽는다(집을 같이 쓰지 않는 방은 읽지 않는다). 읽지 못하면 로그를 남기고 그 면을 모른다 — 방은 가구 놓기를 거절한다.
    /// </summary>
    public static class HouseGrids
    {
        /// <summary>집 공유의 배치 판정(가구 정의 + 저장소의 맵).</summary>
        public static RoomHouseRules Rules(GameDefinitions definitions, string repoRoot, Action<string> log)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            Dictionary<string, IPlacementGrid> grids = null;
            return new RoomHouseRules(definitions.All<FurnitureDefinition>(), surface =>
            {
                grids ??= Load(repoRoot, log);
                return surface != null && grids.TryGetValue(surface, out var grid) ? grid : null;
            });
        }

        /// <summary>저장소(맵 목록·상호작용 데이터·LDtk)에서 면 id → 방 칸. 읽지 못하면 빈 표와 로그 한 줄.</summary>
        public static Dictionary<string, IPlacementGrid> Load(string repoRoot, Action<string> log)
        {
            var grids = new Dictionary<string, IPlacementGrid>(StringComparer.Ordinal);
            try
            {
                var interactions = InteractionData.Load(Path.Combine(repoRoot, InteractionData.DefaultRelativePath));
                var maps = WorldMapLoader.FromRepo(repoRoot);
                foreach (string mapId in interactions.MapIds)
                {
                    string surface = interactions.Map(mapId).PlacementSurface;
                    if (string.IsNullOrEmpty(surface) || grids.ContainsKey(surface)) continue;
                    grids[surface] = FromMap(maps.LoadMap(mapId).World);
                }
            }
            catch (Exception e) when (e is IOException || e is InvalidOperationException || e is UnauthorizedAccessException || e is System.Text.Json.JsonException
                                      || e is ArgumentException || e is FormatException)
            {
                log?.Invoke("집 맵을 읽지 못했다 — 같이 쓰는 집에 가구를 놓을 수 없다: " + e.Message);
                grids.Clear();
            }

            return grids;
        }

        /// <summary>맵 하나의 방 칸(가구 칸 좌표: x는 맵 칸과 같고 y는 뒤집는다).</summary>
        public static PlacementGrid FromMap(WorldMap map)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            int rows = map.Grid.Rows;
            var floor = new List<GridPoint>(map.Grid.Columns * rows);
            var fixedCells = new List<GridPoint>();
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < map.Grid.Columns; x++)
                {
                    var cell = new GridPoint(x, rows - 1 - y);
                    floor.Add(cell);
                    if (map.Collision.IsBlocked(x, y)) fixedCells.Add(cell);
                }

            var ids = new List<string>(map.SpawnIds);
            ids.Sort(StringComparer.Ordinal);
            var spawns = new List<GridPoint>(ids.Count);
            foreach (string id in ids)
            {
                map.TryGetSpawn(id, out var point);
                CellPos c = CellPos.Containing(point.X, point.Y, map.Grid.CellSize);
                var p = new GridPoint(c.X, rows - 1 - c.Y);
                if (!spawns.Contains(p)) spawns.Add(p);
            }

            if (spawns.Count == 0) throw new InvalidOperationException(map.Id + ": 방에 도착 지점이 없다");
            return new PlacementGrid(floor, fixedCells, spawns[0], spawns);
        }
    }
}
