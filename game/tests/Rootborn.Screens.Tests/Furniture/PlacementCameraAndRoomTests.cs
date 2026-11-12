using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Minigames;
using Rootborn.Core.World;
using Rootborn.Screens.Furniture;

namespace Rootborn.Screens.Tests.Furniture
{
    /// <summary>배치 카메라 계산(Unity HousePlacementCameraController 수치)과 가구 방 칸 좌표(자동 테스트).</summary>
    public sealed class PlacementCameraAndRoomTests
    {
        [Test]
        public void FullView_FitsTheWholeMapWithPadding_WithinTheZoomLimits()
        {
            var camera = new PlacementCamera();
            camera.Begin(30 * 32, 18 * 32, 32, 16f / 9f);
            // 반 높이 = max(18/2, 30/(16/9)/2) * 1.08 = 9 * 1.08
            Assert.That(camera.HalfHeightCells, Is.EqualTo(9f * 1.08f).Within(0.001f));
            Assert.That(camera.CenterX, Is.EqualTo(15 * 32));
            Assert.That(camera.CenterY, Is.EqualTo(9 * 32));

            camera.Begin(4 * 32, 4 * 32, 32, 16f / 9f);
            Assert.That(camera.HalfHeightCells, Is.EqualTo(PlacementCamera.Settings.MinHalfHeightCells), "작은 방도 최소 확대 안에서");
            camera.Begin(200 * 32, 100 * 32, 32, 16f / 9f);
            Assert.That(camera.HalfHeightCells, Is.EqualTo(PlacementCamera.Settings.MaxHalfHeightCells));
        }

        [Test]
        public void Pan_StaysInsideTheMap_ZoomStepsAndClamps()
        {
            var camera = new PlacementCamera();
            camera.Begin(100 * 32, 40 * 32, 32, 16f / 9f);
            camera.Wheel(1f); // 가까이 한 칸
            float half = camera.HalfHeightCells;
            camera.Pan(1, 0, 1f);
            Assert.That(camera.CenterX, Is.EqualTo(50 * 32 + 10 * 32).Within(0.01f), "초당 10칸");
            for (int i = 0; i < 100; i++) camera.Pan(1, 1, 1f);
            float halfW = half * 32 * 16f / 9f;
            Assert.That(camera.CenterX, Is.EqualTo(100 * 32 - halfW).Within(0.01f), "맵 오른쪽 끝을 넘지 않는다");
            Assert.That(camera.CenterY, Is.EqualTo(40 * 32 - half * 32).Within(0.01f));

            for (int i = 0; i < 30; i++) camera.Wheel(1f);
            Assert.That(camera.HalfHeightCells, Is.EqualTo(PlacementCamera.Settings.MinHalfHeightCells));
            camera.KeyboardZoom(1, 0.5f);
            Assert.That(camera.HalfHeightCells, Is.EqualTo(PlacementCamera.Settings.MinHalfHeightCells + 4f).Within(0.001f), "초당 8칸");
            camera.FullView();
            Assert.That(camera.CenterX, Is.EqualTo(50 * 32));
            Assert.That(camera.Scale(1080), Is.EqualTo(1080f / (2f * camera.HalfHeightCells * 32)).Within(0.0001f));
        }

        [Test]
        public void SetFurnitureBlocking_IsTrueOnlyWhenTheBlockedCellsChange()
        {
            // 걷는 NPC는 이것이 true일 때만 그 방의 길을 다시 찾는다(배치 화면이 열려 있는 동안 같은 칸으로 다시 불려도 다시 찾지 않는다).
            var room = new FurnitureRoom(FurniturePlacementScreenTests.TestRoomMap());
            GridPoint desk = room.ToPlacement(new CellPos(3, 3)), wall = room.ToPlacement(new CellPos(0, 3));
            Assert.That(room.SetFurnitureBlocking(Enumerable.Empty<GridPoint>()), Is.False, "처음과 같다(가구 없음)");
            Assert.That(room.SetFurnitureBlocking(new[] { desk }), Is.True, "칸이 막혔다");
            Assert.That(room.SetFurnitureBlocking(new[] { desk }), Is.False, "같은 칸");
            Assert.That(room.SetFurnitureBlocking(new[] { desk, wall, new GridPoint(500, 500) }), Is.False, "원래 막힌 칸과 맵 밖 칸은 바꾸지 않는다");
            Assert.That(room.SetFurnitureBlocking(null), Is.True, "가구를 치웠다");
            Assert.That(room.SetFurnitureBlocking(null), Is.False);
        }

        [Test]
        public void RoomCells_HaveYUp_LikeTheFurnitureDefinitions_AndBlockingReachesTheWalkMap()
        {
            WorldMap map = FurniturePlacementScreenTests.TestRoomMap();
            var room = new FurnitureRoom(map);
            // 맵 위 줄(0)은 가구 칸 맨 위(7). 가구 칸 (x, y+1)은 맵에서 한 줄 위.
            Assert.That(room.ToPlacement(new CellPos(2, 0)), Is.EqualTo(new GridPoint(2, 7)));
            Assert.That(room.ToMap(new GridPoint(2, 4)).Y - room.ToMap(new GridPoint(2, 5)).Y, Is.EqualTo(1));
            Assert.That(room.ToMap(room.ToPlacement(new CellPos(3, 5))), Is.EqualTo(new CellPos(3, 5)));
            Assert.That(room.Grid.Spawn, Is.EqualTo(room.ToPlacement(new CellPos(5, 6))));
            Assert.That(room.FloorCells.Count, Is.EqualTo(80));
            Assert.That(room.Grid.IsFixed(room.ToPlacement(new CellPos(0, 3))), Is.True, "벽은 고정 칸");

            Assert.That(room.WalkMap.Collision.IsBlocked(3, 3), Is.False);
            room.SetFurnitureBlocking(new[] { room.ToPlacement(new CellPos(3, 3)) });
            Assert.That(room.WalkMap.Collision.IsBlocked(3, 3), Is.True, "가구가 막는 칸");
            Assert.That(map.Collision.IsBlocked(3, 3), Is.False, "원래 맵은 그대로");
            room.SetFurnitureBlocking(Enumerable.Empty<GridPoint>());
            Assert.That(room.WalkMap.Collision.IsBlocked(3, 3), Is.False);
            Assert.That(room.WalkMap.Collision.IsBlocked(0, 0), Is.True);
        }
    }
}
