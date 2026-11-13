// 옮겨 옴: Assets/Scripts/Game/Player/PlayerController.cs (이동 속도·방향), Assets/Scripts/Game/World/InteriorDoorPortalRuntimeInstaller.cs
// (문 상자·목적지), Assets/Scripts/Game/World/TownAutoDoorPortal.cs (닿으면 0.35초 뒤 이동), Assets/Scripts/Game/Player/CameraFollow.cs (맵 밖 안 비춤),
// Assets/Scripts/Editor/LDtk/LdtkImportValidator.cs (도착 지점 검사)
using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Core.World;

namespace Rootborn.Core.Tests
{
    public sealed class WorldMovementTests
    {
        private const int Cell = 32;

        /// <summary>'#'는 막힌 칸.</summary>
        internal static CollisionGrid Grid(params string[] rows)
        {
            int cols = rows[0].Length;
            var blocked = new bool[cols * rows.Length];
            for (int y = 0; y < rows.Length; y++)
                for (int x = 0; x < cols; x++)
                    blocked[y * cols + x] = rows[y][x] == '#';
            return new CollisionGrid(new MapGrid(cols, rows.Length, Cell), blocked);
        }

        private static Walker WalkerAt(CollisionGrid grid, int cx, int cy)
        {
            var walker = new Walker(WalkerSettings.UnityPlayer, grid.Grid.CellSize);
            walker.PlaceAt(CellPos.Center(new CellPos(cx, cy), Cell));
            return walker;
        }

        private static void Walk(Walker walker, CollisionGrid grid, int dx, int dy, float seconds, float dt = 1f / 60f)
        {
            for (float t = 0; t < seconds - 1e-4f; t += dt) walker.Step(dx, dy, dt, grid);
        }

        [Test]
        public void WORLD_MOVE_001_UnityPlayerWalksFourCellsPerSecondWithAOneCellBody()
        {
            Assert.That(WalkerSettings.UnityPlayer.SpeedCellsPerSecond, Is.EqualTo(4f));
            Assert.That(WalkerSettings.UnityPlayer.BodyCells, Is.EqualTo(1f));
            CollisionGrid grid = Grid("..........");
            Walker walker = WalkerAt(grid, 1, 0);
            Walk(walker, grid, 1, 0, 1f);
            Assert.That(walker.X, Is.EqualTo(1.5f * Cell + 4 * Cell).Within(0.5f));
            Assert.That(walker.Facing, Is.EqualTo(Facing.Right));
            Assert.That(walker.Moving, Is.True);
        }

        [Test]
        public void WORLD_MOVE_002_DiagonalIsNormalizedAndFacesVertically()
        {
            CollisionGrid grid = Grid("..........", "..........", "..........", "..........", "..........", "..........");
            Walker walker = WalkerAt(grid, 1, 1);
            Walk(walker, grid, 1, 1, 0.5f);
            float moved = (walker.X - 1.5f * Cell) * 1.41421356f;
            Assert.That(moved, Is.EqualTo(2 * Cell).Within(1f));
            Assert.That(walker.Facing, Is.EqualTo(Facing.Down), "Unity 판: |x|가 |y|보다 클 때만 옆을 본다");
        }

        [Test]
        public void WORLD_MOVE_003_WallStopsTheBodyAtItsEdge()
        {
            CollisionGrid grid = Grid("...#");
            Walker walker = WalkerAt(grid, 0, 0);
            Walk(walker, grid, 1, 0, 2f);
            Assert.That(walker.Box.Right, Is.LessThanOrEqualTo(3 * Cell + 0.001f));
            Assert.That(walker.Box.Right, Is.GreaterThan(3 * Cell - 3f), "벽 바로 앞까지 붙는다");
        }

        [Test]
        public void WORLD_MOVE_004_AxesMoveSeparatelySoTheBodySlidesAlongAWall()
        {
            CollisionGrid grid = Grid(
                "######",
                "......",
                "......");
            Walker walker = WalkerAt(grid, 1, 1);
            Walk(walker, grid, 1, -1, 0.5f);
            Assert.That(walker.X, Is.GreaterThan(1.5f * Cell + Cell), "위가 막혀도 옆으로는 간다");
            Assert.That(walker.Box.Top, Is.GreaterThanOrEqualTo(Cell - 0.001f));
        }

        [Test]
        public void WORLD_MOVE_005_NearlyAlignedBodySlipsIntoAOneCellGap()
        {
            // 한 칸 몸으로 한 칸 틈에 들어가려면 정확히 맞아야 한다. Unity 판은 물리 밀어내기로 미끄러져 들어갔다. 여기서는 모서리 보정으로 근사한다.
            CollisionGrid grid = Grid(
                "......",
                "##.###",
                "##.###");
            Walker walker = WalkerAt(grid, 2, 0);
            walker.PlaceAt(walker.X + 6f, walker.Y);
            Walk(walker, grid, 0, 1, 1f);
            Assert.That(walker.Y, Is.GreaterThan(1.5f * Cell), "틈으로 내려갔다");
            Assert.That(walker.X, Is.EqualTo(2.5f * Cell).Within(0.01f));
        }

        [Test]
        public void WORLD_MOVE_006_FarOffBodyDoesNotSlipAroundACorner()
        {
            CollisionGrid grid = Grid(
                "......",
                "##.###");
            Walker walker = WalkerAt(grid, 1, 0);
            Walk(walker, grid, 0, 1, 1f);
            Assert.That(walker.Y, Is.EqualTo(0.5f * Cell).Within(0.01f), "반 칸 넘게 어긋나면 막힌다");
        }

        [Test]
        public void WORLD_MOVE_009_ATinyFloatingErrorPastACellEdgeDoesNotCountAsTheNextCell()
        {
            // 집에서 실제로 났던 일: Y가 144.00006이 되어 몸 아래 가장자리가 다음 줄(막힌 벽)에 걸린 것으로 판정되어 옆으로 못 갔다.
            CollisionGrid grid = Grid(
                "......",
                "......",
                "######");
            Walker walker = WalkerAt(grid, 4, 1);
            walker.PlaceAt(walker.X, 1.5f * Cell + 0.00006f);
            Walk(walker, grid, -1, 0, 0.5f);
            Assert.That(walker.X, Is.LessThan(4.5f * Cell - Cell), "옆으로 걸어간다");
        }

        [Test]
        public void WORLD_MOVE_007_StandingStillResetsWalkTimeAndKeepsFacing()
        {
            CollisionGrid grid = Grid("......");
            Walker walker = WalkerAt(grid, 2, 0);
            Walk(walker, grid, -1, 0, 0.2f);
            Assert.That(walker.WalkSeconds, Is.GreaterThan(0f));
            walker.Step(0, 0, 1f / 60f, grid);
            Assert.That(walker.Moving, Is.False);
            Assert.That(walker.WalkSeconds, Is.EqualTo(0f));
            Assert.That(walker.Facing, Is.EqualTo(Facing.Left));
        }

        [Test]
        public void WORLD_MOVE_008_NegativeOrHugeTimeStepsAreBounded()
        {
            CollisionGrid grid = Grid("..........");
            Walker walker = WalkerAt(grid, 1, 0);
            walker.Step(1, 0, -1f, grid);
            Assert.That(walker.X, Is.EqualTo(1.5f * Cell));
            walker.Step(1, 0, 10f, grid); // 멈췄다 돌아온 프레임 하나로 벽을 뚫지 않는다
            Assert.That(walker.Box.Right, Is.LessThanOrEqualTo(10 * Cell));
        }

        [Test]
        public void WORLD_DOOR_001_DoorAreaIsTheEntityBoxGrownToAtLeastThreeQuartersByOneCell()
        {
            DoorArea small = DoorArea.FromEntity(100, 200, 16, 16, "town", Cell, null);
            Assert.That(small.Area.W, Is.EqualTo(0.75f * Cell));
            Assert.That(small.Area.H, Is.EqualTo(1f * Cell));
            Assert.That(small.Area.CenterX, Is.EqualTo(108f));
            Assert.That(small.Area.CenterY, Is.EqualTo(208f));
            DoorArea big = DoorArea.FromEntity(288, 32, 64, 64, "town", Cell, null);
            Assert.That(big.Area, Is.EqualTo(new AreaRect(288, 32, 64, 64)));
        }

        [Test]
        public void WORLD_DOOR_002_EnteringADoorOpensItAndTravelsAfterThirtyFiveHundredths()
        {
            var door = DoorArea.FromEntity(64, 0, 32, 32, "town", Cell, new TravelTarget("Town", "x"));
            var watch = new DoorWatch(new[] { door });
            var outside = new AreaRect(64, 64, 32, 32);
            var inside = new AreaRect(64, 20, 32, 32);
            watch.Reset(outside);
            Assert.That(watch.Update(outside, 0.1f), Is.Null);
            Assert.That(watch.Update(inside, 0.1f), Is.Null);
            Assert.That(watch.Opening, Is.SameAs(door));
            Assert.That(watch.Update(outside, 0.2f), Is.Null, "열리기 시작하면 걸어 나가도 이어진다(Unity 판 코루틴)");
            Assert.That(watch.Update(outside, 0.15f), Is.SameAs(door));
            Assert.That(watch.Opening, Is.Null);
        }

        [Test]
        public void WORLD_DOOR_003_StandingInADoorWhenArrivingDoesNotTrigger()
        {
            // Unity 판은 상자에 "들어올 때"만 반응했다. 도착하자마자 다시 나가지 않는다.
            var door = DoorArea.FromEntity(64, 0, 32, 32, "town", Cell, new TravelTarget("Town", "x"));
            var watch = new DoorWatch(new[] { door });
            var inside = new AreaRect(64, 10, 32, 32);
            watch.Reset(inside);
            Assert.That(watch.Update(inside, 1f), Is.Null);
            Assert.That(watch.Update(new AreaRect(64, 64, 32, 32), 0.1f), Is.Null);
            watch.Update(inside, 0.1f);
            Assert.That(watch.Update(inside, 0.35f), Is.SameAs(door));
        }

        [Test]
        public void WORLD_DOOR_004_TouchingAnEdgeIsNotEntering()
        {
            var door = DoorArea.FromEntity(64, 0, 32, 32, "town", Cell, new TravelTarget("Town", "x"));
            var watch = new DoorWatch(new[] { door });
            watch.Reset(new AreaRect(64, 64, 32, 32));
            watch.Update(new AreaRect(64, 32, 32, 32), 0.1f);
            Assert.That(watch.Opening, Is.Null);
        }

        [Test]
        public void WORLD_DOOR_006_WithUnityContactSkinStandingAgainstTheWallUnderADoorCounts()
        {
            // Unity 판 Box2D 껍질(0.01유닛씩): 문이 벽 안에 있어도 그 벽에 붙어 서면 닿은 것이다.
            var door = DoorArea.FromEntity(64, 0, 32, 32, "police", Cell, new TravelTarget("PoliceInterior", "police-entry"));
            var watch = new DoorWatch(new[] { door }, DoorWatch.UnityOpeningSeconds, DoorWatch.UnityContactSkinCells * Cell);
            watch.Reset(new AreaRect(64, 64, 32, 32));
            watch.Update(new AreaRect(64, 32, 32, 32), 0.1f);
            Assert.That(watch.Opening, Is.SameAs(door));
            var far = new DoorWatch(new[] { door }, DoorWatch.UnityOpeningSeconds, DoorWatch.UnityContactSkinCells * Cell);
            far.Reset(new AreaRect(64, 64, 32, 32));
            far.Update(new AreaRect(64, 33, 32, 32), 0.1f);
            Assert.That(far.Opening, Is.Null, "1픽셀 떨어지면 닿지 않았다");
        }

        [Test]
        public void WORLD_DOOR_005_DoorWithoutDestinationNeverOpens()
        {
            var door = DoorArea.FromEntity(64, 0, 32, 32, "", Cell, null);
            var watch = new DoorWatch(new[] { door });
            watch.Reset(new AreaRect(64, 64, 32, 32));
            watch.Update(new AreaRect(64, 10, 32, 32), 0.1f);
            Assert.That(watch.Opening, Is.Null);
            Assert.That(watch.Update(new AreaRect(64, 10, 32, 32), 1f), Is.Null);
        }

        [Test]
        public void WORLD_ROUTE_001_DoorValuesAreTrimmedLowercasedAndDashesBecomeSpaces()
        {
            Assert.That(DoorRoutes.Normalize("  Shopping-Center "), Is.EqualTo("shopping center"));
            Assert.That(DoorRoutes.Normalize(null), Is.EqualTo(""));
        }

        [Test]
        public void WORLD_ROUTE_002_RouteTableResolvesDestinationAndSpawnFieldOverridesSpawn()
        {
            var routes = new DoorRoutes(new Dictionary<string, TravelTarget>
            {
                { "Town", new TravelTarget("Town", "town-shop-entry") },
                { "outside", new TravelTarget("Town", "town-shop-entry") },
            });
            Assert.That(routes.Resolve("TOWN", null), Is.EqualTo(new TravelTarget("Town", "town-shop-entry")));
            Assert.That(routes.Resolve("outside", " town-entry "), Is.EqualTo(new TravelTarget("Town", "town-entry")));
            Assert.That(routes.Resolve("hospital_2", null), Is.Null, "목적지를 모르는 문은 문이 아니다");
            Assert.That(routes.Resolve("", null), Is.Null);
        }

        [Test]
        public void WORLD_PATH_001_FindsAShortestFourWayPathAroundWalls()
        {
            CollisionGrid grid = Grid(
                ".....",
                ".###.",
                ".....");
            List<CellPos> path = GridPath.Find(grid, new CellPos(0, 1), c => c.Equals(new CellPos(4, 1)));
            Assert.That(path, Is.Not.Null);
            Assert.That(path.Count, Is.EqualTo(6));
            Assert.That(path[path.Count - 1], Is.EqualTo(new CellPos(4, 1)));
            foreach (CellPos c in path) Assert.That(grid.IsBlocked(c.X, c.Y), Is.False);
        }

        [Test]
        public void WORLD_PATH_002_NoPathThroughWallsAndStartIsNotPartOfThePath()
        {
            CollisionGrid grid = Grid("..#..");
            Assert.That(GridPath.Find(grid, new CellPos(0, 0), c => c.X == 4), Is.Null);
            List<CellPos> already = GridPath.Find(grid, new CellPos(0, 0), c => c.X == 0);
            Assert.That(already, Is.Empty);
        }

        [Test]
        public void WORLD_ARRIVE_001_ArrivalMustBeOnOpenFloorAwayFromExitsWithEveryExitReachable()
        {
            CollisionGrid grid = Grid(
                "#..##",
                "#..##",
                "#..#.",
                "#####");
            var exit = DoorArea.FromEntity(32, 0, 32, 32, "town", Cell, new TravelTarget("Town", "a"));
            var unreachable = DoorArea.FromEntity(128, 64, 32, 32, "shop", Cell, new TravelTarget("Shop", "b"));
            Assert.That(ArrivalRules.Check(grid, new[] { exit }, CellPos.Center(new CellPos(1, 2), Cell)), Is.Empty);
            Assert.That(ArrivalRules.Check(grid, new[] { exit }, CellPos.Center(new CellPos(0, 2), Cell)), Has.Some.Contains("막힌"));
            Assert.That(ArrivalRules.Check(grid, new[] { exit }, CellPos.Center(new CellPos(1, 1), Cell)), Has.Some.Contains("0.9"));
            Assert.That(ArrivalRules.Check(grid, new[] { exit, unreachable }, CellPos.Center(new CellPos(1, 2), Cell)), Has.Some.Contains("shop"));
            // 바깥 벽 칸의 문(키트 방의 현관): 문 칸은 막혔지만 안쪽 바닥에서 몸이 닿으면 열린다(DoorWatch·MapReach.Touches와 같은 기준).
            var wallDoor = DoorArea.FromEntity(32, 96, 32, 32, "town", Cell, new TravelTarget("Town", "a"));
            Assert.That(ArrivalRules.Check(grid, new[] { wallDoor }, CellPos.Center(new CellPos(1, 0), Cell)), Is.Empty, "막힌 벽 칸의 문도 맞닿은 바닥까지 걸어가면 닿는다");
        }

        [Test]
        public void WORLD_ARRIVE_002_AWallDoorTouchedOnlyAtACornerCountsAsReachable()
        {
            // 지금 동작을 고정한다: 걸어서 닿는 칸이 출구 상자와 모서리(대각선)로만 맞닿아도 닿는다(MapReach.Touches의 1px 여유).
            // 실제 문 판정(DoorWatch, 0.02칸 여유)도 같아서 바깥 벽 문은 안쪽 바닥과 그 양옆 바닥 칸에서 열린다(약 2칸 폭).
            CollisionGrid grid = Grid(
                "#....",
                "#####");
            var cornerDoor = DoorArea.FromEntity(0, 32, 32, 32, "town", Cell, new TravelTarget("Town", "a"));   // 칸 (0,1), 상하좌우는 모두 벽
            Assert.That(ArrivalRules.Check(grid, new[] { cornerDoor }, CellPos.Center(new CellPos(3, 0), Cell)), Is.Empty,
                "바닥 (1,0)이 문 칸 (0,1)과 대각선으로만 붙어도 닿는다");
            CollisionGrid sealedCorner = Grid(
                "##...",
                "#####");
            Assert.That(ArrivalRules.Check(sealedCorner, new[] { cornerDoor }, CellPos.Center(new CellPos(3, 0), Cell)),
                Has.Some.Contains("걸어갈 수 없다"), "대각선 칸도 벽이면 닿지 않는다");
        }

        [Test]
        public void WORLD_CAMERA_001_CameraFollowsButNeverShowsOutsideAndCentersSmallMaps()
        {
            Assert.That(WorldCamera.Offset(focus: 500, mapSize: 2000, viewSize: 400), Is.EqualTo(300f), "가운데에 둔다");
            Assert.That(WorldCamera.Offset(focus: 50, mapSize: 2000, viewSize: 400), Is.EqualTo(0f), "왼쪽 끝");
            Assert.That(WorldCamera.Offset(focus: 1990, mapSize: 2000, viewSize: 400), Is.EqualTo(1600f), "오른쪽 끝");
            Assert.That(WorldCamera.Offset(focus: 10, mapSize: 300, viewSize: 400), Is.EqualTo(-50f), "맵이 화면보다 작으면 가운데");
        }

        [Test]
        public void WORLD_CAMERA_002_ZoomIsTheNearestWholeNumberForTheMapsViewHeight()
        {
            // Unity 판 AuthoredWorldScene.FitPixelFrame처럼 가까운 정수 배율. 보이는 높이(칸)는 맵이 정한다.
            Assert.That(WorldCamera.Zoom(screenHeight: 1080, viewCells: 18, cellSize: 32), Is.EqualTo(2));
            Assert.That(WorldCamera.Zoom(screenHeight: 720, viewCells: 13, cellSize: 32), Is.EqualTo(2));
            Assert.That(WorldCamera.Zoom(screenHeight: 1080, viewCells: 10, cellSize: 32), Is.EqualTo(3));
            Assert.That(WorldCamera.Zoom(screenHeight: 200, viewCells: 18, cellSize: 32), Is.EqualTo(1), "1보다 작아지지 않는다");
        }
    }
}
