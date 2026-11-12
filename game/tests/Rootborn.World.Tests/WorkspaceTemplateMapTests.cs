using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;
using Rootborn.Core.World;
using Rootborn.Core.Workspaces;
using Rootborn.Data;
using Rootborn.Data.Builders;

namespace Rootborn.World.Tests
{
    /// <summary>
    /// 일터 템플릿이 그 일터 면을 가진 맵(interactions.json의 placementSurface)의 방 칸에 맞는다. 맵과 면은 데이터에서 찾는다(맵 id를 적지 않는다).
    /// 방 칸은 Host·서버와 같은 방식(맵 안의 칸은 바닥, 막힌 칸은 고정 칸, 도착 지점 칸은 들어오는 칸, 가구 칸 y는 맵 칸 y를 뒤집는다 —
    /// Screens FurnitureRoom, Server HouseGrids.FromMap)으로 만든다. 자동 테스트.
    /// </summary>
    public sealed class WorkspaceTemplateMapTests
    {
        private static GameDefinitions Definitions()
        {
            var result = GameDataLoader.Load(Path.Combine(Repo.Root, "data"), RootbornBuilders.Register);
            Assert.That(result.Errors, Is.Empty, string.Join("\n", result.Errors));
            return result.Definitions;
        }

        /// <summary>맵 하나의 방 칸(FurnitureRoom·HouseGrids.FromMap과 같은 규칙).</summary>
        private static PlacementGrid RoomOf(WorldMap map)
        {
            int rows = map.Grid.Rows;
            var floor = new List<GridPoint>(map.Grid.Count);
            var fixedCells = new List<GridPoint>();
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < map.Grid.Columns; x++)
                {
                    var cell = new GridPoint(x, rows - 1 - y);
                    floor.Add(cell);
                    if (map.Collision.IsBlocked(x, y)) fixedCells.Add(cell);
                }
            var ids = map.SpawnIds.OrderBy(id => id, System.StringComparer.Ordinal).ToList();
            var spawns = new List<GridPoint>();
            foreach (string id in ids)
            {
                map.TryGetSpawn(id, out var p);
                CellPos c = CellPos.Containing(p.X, p.Y, map.Grid.CellSize);
                var g = new GridPoint(c.X, rows - 1 - c.Y);
                if (!spawns.Contains(g)) spawns.Add(g);
            }
            return new PlacementGrid(floor, fixedCells, spawns[0], spawns);
        }

        [Test]
        public void EveryWorkspaceTemplate_FitsItsMap()
        {
            GameDefinitions definitions = Definitions();
            InteractionData interactions = InteractionData.Load(Path.Combine(Repo.Root, InteractionData.DefaultRelativePath));
            WorldMapLoader loader = Repo.Loader();
            var catalog = definitions.All<FurnitureDefinition>().ToDictionary(f => f.StableId);
            var templates = definitions.All<WorkspaceTemplate>().ToList();
            Assert.That(templates, Is.Not.Empty);

            var problems = new List<string>();
            foreach (Workspace workspace in definitions.All<Workspace>())
            {
                var maps = interactions.MapIds.Where(id => interactions.Map(id).PlacementSurface == workspace.SurfaceId).ToList();
                if (maps.Count == 0) { problems.Add(workspace.Id + ": 면 " + workspace.SurfaceId + "을 가진 맵이 없다"); continue; }
                foreach (string mapId in maps)
                {
                    WorldMap map = loader.LoadMap(mapId).World;
                    foreach (WorkspaceTemplate template in templates.Where(t => t.WorkspaceId == workspace.Id))
                    {
                        var layout = new HouseFurnitureLayout(new HousePlacementModel(RoomOf(map)), workspace.SurfaceId, null, workspace.AcceptsSurfaceIds);
                        var requests = template.Placements
                            .Select(p => new FurniturePlacementRequest(catalog[p.FurnitureId], p.Anchor, p.Direction))
                            .ToList();
                        if (!layout.CanReplaceUserFurniture(requests))
                        {
                            // 어느 줄이 안 맞는지: 앞 줄들을 놓은 방에 그 줄을 놓아 본다.
                            string bad = "?";
                            for (int i = 0; i < requests.Count; i++)
                                if (!layout.CanReplaceUserFurniture(requests.Take(i + 1).ToList())) { bad = requests[i].Definition.StableId + " " + requests[i].Anchor; break; }
                            problems.Add(template.Id + " → " + mapId + ": 맞지 않는다(" + bad + ")");
                        }
                    }
                }
            }
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }
    }
}
