using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;
using Rootborn.Core.Save;
using Rootborn.World;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 집 맵이 키트 방(벽이 두꺼운 새 집)으로 바뀌어도 옛 세이브의 가구 기록은 사라지지 않는다(자동 테스트, GameSession 명령).
    /// 새 집에서 벽이 된 칸의 기록과 데이터에 없는 가구 기록은 놓이지 않은 채(HouseFurnitureLayout의 못 놓은 기록) 들고 있다가,
    /// 플레이어가 무엇을 바꿔 배치 파일이 다시 써질 때 그대로 다시 쓰인다. 읽기만으로는 파일을 쓰지 않는다.
    /// 가까운 자리로 옮기기와 보관 목록은 다음 작업(설계서 7.3절, 1-2)이다.
    /// </summary>
    public sealed class NewHouseKeepsOldFurnitureTests
    {
        private const string FreeDesk = "growth31.assembly.desk";

        [Test]
        public void OldFurnitureOnWhatIsNowAWall_IsKeptAndWrittenBack()
        {
            using var saves = new TempSaves();
            // 놓을 수 있는 칸 하나(새 집에서), 새 집에서 벽이 된 안쪽 칸 하나, 데이터에 없는 가구 하나.
            var probe = TestWorld.Start(saves.Store);
            probe.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            GridPoint free = HouseRoom.FirstFreeAnchor(probe, FreeDesk);
            var map = WorldMapLoader.FromRepo(TestWorld.RepoRoot).LoadMap(HouseRoom.MapId).World;
            Rootborn.Core.World.CellPos wall = map.Collision.BlockedCells()
                .First(c => c.X > 0 && c.Y > 0 && c.X < map.Grid.Columns - 1 && c.Y < map.Grid.Rows - 1);
            Assert.That(saves.Store.TryRead<FurniturePlacementLayoutSaveData>(TestWorld.Slot, SaveFileNames.HouseFurnitureLayout(0), out _), Is.False);

            FurniturePlacementSaveData Record(string id, int x, int y) => new FurniturePlacementSaveData
            {
                FurnitureId = id, SurfaceId = HouseRoom.SurfaceId, AnchorX = x, AnchorY = y, Direction = FurniturePlacementDirection.North,
            };
            var old = new FurniturePlacementLayoutSaveData
            {
                Version = FurniturePlacementLayoutSaveData.KnownVersion,
                Items = new[] { Record(FreeDesk, free.X, free.Y), Record(FreeDesk, wall.X, wall.Y), Record("old.no-such-furniture", free.X + 1, free.Y) },
            };
            saves.Store.Write(TestWorld.Slot, SaveFileNames.HouseFurnitureLayout(0), old);
            string file = Path.Combine(saves.Path, TestWorld.Slot, SaveFileNames.HouseFurnitureLayout(0));
            byte[] written = File.ReadAllBytes(file);

            var game = TestWorld.Start(saves.Store);
            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            Assert.That(game.House.Placed.Select(p => (p.Definition.StableId, p.Anchor)), Is.EqualTo(new[] { (FreeDesk, free) }), "놓을 수 있는 기록만 놓인다");
            Assert.That(game.House.UnresolvedCount, Is.EqualTo(2), "벽이 된 칸의 기록과 데이터에 없는 가구는 못 놓은 기록으로 든다");
            Assert.That(File.ReadAllBytes(file), Is.EqualTo(written), "들어가 읽기만 해서는 배치 파일을 쓰지 않는다");

            // 플레이어가 하나 더 놓으면 파일이 다시 써진다 — 못 놓은 기록도 그대로 남는다.
            GridPoint second = HouseRoom.FirstFreeAnchor(game, FreeDesk);
            Assert.That(game.PlaceFurniture(FreeDesk, second, FurniturePlacementDirection.North), Is.True);
            Assert.That(saves.Store.TryRead<FurniturePlacementLayoutSaveData>(TestWorld.Slot, SaveFileNames.HouseFurnitureLayout(0), out var after), Is.True);
            Assert.That(after.Items.Select(i => (i.FurnitureId, i.AnchorX, i.AnchorY)), Is.SupersetOf(old.Items.Select(i => (i.FurnitureId, i.AnchorX, i.AnchorY))),
                "옛 기록 셋이 모두 파일에 남는다");
            Assert.That(after.Items.Length, Is.EqualTo(4));

            var again = TestWorld.Start(saves.Store);
            again.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            Assert.That(again.House.Placed.Count, Is.EqualTo(2));
            Assert.That(again.House.UnresolvedCount, Is.EqualTo(2), "다음 판에서도 못 놓은 기록을 들고 있다");
        }
    }
}
