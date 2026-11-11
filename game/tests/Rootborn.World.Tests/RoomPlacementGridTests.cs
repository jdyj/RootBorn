using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;
using Rootborn.Core.World;
using Rootborn.World;

namespace Rootborn.World.Tests
{
    /// <summary>가구 방 칸: 실제 집 맵(LDtk)과 interactions.json의 placementSurface. 자동 테스트.</summary>
    public sealed class RoomPlacementGridTests
    {
        private static WorldMap House() => Repo.Loader().LoadMap("House").World;

        [Test]
        public void HouseMap_IsMarkedAsTheHouseRoom_InTheInteractionData()
        {
            InteractionData data = InteractionData.Load(Path.Combine(Repo.Root, InteractionData.DefaultRelativePath));
            Assert.That(data.Map("House").PlacementSurface, Is.EqualTo("house"));
            Assert.That(data.Map("School").PlacementSurface, Is.Empty);
            Assert.That(data.Map("Town").PlacementSurface, Is.Empty);
        }

        [Test]
        public void FromMap_FloorIsEveryCell_BlockedCellsAreFixed_SpawnIsTheEntry()
        {
            WorldMap map = House();
            PlacementGrid grid = RoomPlacementGrid.FromMap(map);
            for (int y = 0; y < map.Grid.Rows; y++)
                for (int x = 0; x < map.Grid.Columns; x++)
                {
                    Assert.That(grid.HasFloor(new GridPoint(x, y)), Is.True);
                    Assert.That(grid.IsFixed(new GridPoint(x, y)), Is.EqualTo(map.Collision.IsBlocked(x, y)), x + "," + y);
                }
            Assert.That(grid.HasFloor(new GridPoint(map.Grid.Columns, 0)), Is.False, "맵 밖은 바닥이 아니다");
            map.TryGetSpawn("house-entry", out var entry);
            CellPos cell = CellPos.Containing(entry.X, entry.Y, map.Grid.CellSize);
            Assert.That(grid.Approaches, Does.Contain(new GridPoint(cell.X, cell.Y)));
            Assert.That(grid.Approaches, Does.Contain(grid.Spawn));
        }
    }
}
