using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.World;

namespace Rootborn.Core.Tests
{
    /// <summary>건물 부지 놓기(문 칸 기준), 부지 검사, 문 열림 진행도, 서 있는 맵 다시 읽기. 설계 docs/superpowers/specs/2026-11-12-building-lots-doors-design.md 4절.</summary>
    public sealed class BuildingLotsTests
    {
        private const int Cell = 16;

        // 5x4칸 건물: 아래 세 줄이 막고, 문은 아래 줄 가운데에서 왼쪽 하나(좌우 대칭이 아니다).
        private static BuildingFootprint Small => new BuildingFootprint(5, 4, new CellRect(0, 1, 5, 3), new CellPos(1, 3));
        // 같은 건물의 2단계: 7x5칸, 문은 아래 줄 세 번째.
        private static BuildingFootprint Big => new BuildingFootprint(7, 5, new CellRect(0, 1, 7, 4), new CellPos(2, 4));
        private static BuildingLot Lot => new BuildingLot("lot", new CellRect(2, 1, 9, 7), new CellPos(6, 6));

        [Test]
        public void LOT_001_BuildingIsPlacedSoItsDoorCellLandsOnTheLotsDoorCell()
        {
            BuildingPlacementResult small = BuildingPlacement.Place(Lot, Small);
            Assert.That(small.Ok, Is.True, small.Problem);
            Assert.That(small.TopLeft, Is.EqualTo(new CellPos(5, 3)));
            Assert.That(small.Door, Is.EqualTo(new CellPos(6, 6)));

            BuildingPlacementResult big = BuildingPlacement.Place(Lot, Big);
            Assert.That(big.Ok, Is.True, big.Problem);
            Assert.That(big.TopLeft, Is.EqualTo(new CellPos(4, 2)));
            Assert.That(big.Door, Is.EqualTo(small.Door), "건물이 커져도 문 자리는 그대로");
        }

        [Test]
        public void LOT_002_BlockedCellsAreTheSolidRectangleWithoutTheDoorCell()
        {
            BuildingPlacementResult small = BuildingPlacement.Place(Lot, Small);
            Assert.That(small.Blocked.Count, Is.EqualTo(5 * 3 - 1));
            Assert.That(small.Blocked, Does.Not.Contain(small.Door), "문 칸은 막지 않는다(걸어 들어가야 열린다)");
            Assert.That(small.Blocked, Does.Contain(new CellPos(5, 4)));
            Assert.That(small.Blocked, Does.Not.Contain(new CellPos(5, 3)), "지붕 위 줄(solid 밖)은 막지 않는다");

            BuildingPlacementResult big = BuildingPlacement.Place(Lot, Big);
            Assert.That(big.Blocked.Count, Is.EqualTo(7 * 4 - 1));
            Assert.That(big.Blocked, Does.Contain(new CellPos(4, 6)), "2단계는 1단계보다 넓게 막는다");
            Assert.That(small.Blocked, Does.Not.Contain(new CellPos(4, 6)));
        }

        [Test]
        public void LOT_003_DoorAreaIsTheDoorCellAndTheCellBelowIsTheDoorFront()
        {
            BuildingPlacementResult small = BuildingPlacement.Place(Lot, Small);
            Assert.That(small.DoorArea(Cell), Is.EqualTo(new AreaRect(6 * Cell, 6 * Cell, Cell, Cell)));
            Assert.That(small.DoorFront, Is.EqualTo(new CellPos(6, 7)));
        }

        [Test]
        public void LOT_004_BuildingThatLeavesTheLotIsAProblemNotAnException()
        {
            var narrow = new BuildingLot("narrow", new CellRect(5, 3, 5, 4), new CellPos(6, 6));
            Assert.That(BuildingPlacement.Place(narrow, Small).Ok, Is.True);
            BuildingPlacementResult big = BuildingPlacement.Place(narrow, Big);
            Assert.That(big.Ok, Is.False);
            Assert.That(big.Problem, Does.Contain("narrow").And.Contain("밖"));
            Assert.That(big.Blocked, Is.Empty);
        }

        [Test]
        public void LOT_005_FootprintRefusesValuesThatCannotBeABuilding()
        {
            Assert.Throws<ArgumentException>(() => new BuildingFootprint(0, 3, new CellRect(0, 0, 1, 1), new CellPos(0, 0)));
            Assert.Throws<ArgumentException>(() => new BuildingFootprint(3, 3, new CellRect(0, 0, 4, 3), new CellPos(0, 0)), "막는 칸이 그림 밖");
            Assert.Throws<ArgumentException>(() => new BuildingFootprint(3, 3, new CellRect(0, 1, 3, 2), new CellPos(1, 0)), "문 칸이 막는 칸 밖");
        }

        [Test]
        public void LOT_006_LotsOutsideTheMapOrOverlappingEachOtherAreProblems()
        {
            var grid = new MapGrid(12, 10, Cell);
            var lots = new[]
            {
                new BuildingLot("a", new CellRect(0, 0, 4, 4), new CellPos(1, 3)),
                new BuildingLot("b", new CellRect(3, 3, 4, 4), new CellPos(4, 6)),
                new BuildingLot("c", new CellRect(10, 8, 4, 4), new CellPos(11, 9)),
                new BuildingLot("d", new CellRect(0, 5, 2, 2), new CellPos(5, 6)),
            };
            List<string> problems = BuildingPlacement.CheckLots(lots, grid);
            Assert.That(problems, Has.Some.Contains("a").And.Contains("b").And.Contains("겹친다"));
            Assert.That(problems, Has.Some.Contains("c").And.Contains("맵 밖"));
            Assert.That(problems, Has.Some.Contains("d").And.Contains("문 칸"));
            Assert.That(problems.Count, Is.EqualTo(3));
        }

        [Test]
        public void LOT_007_LevelIsClampedToTheBuildingsLevels()
        {
            Assert.That(BuildingPlacement.ClampLevel(0, 3), Is.EqualTo(1));
            Assert.That(BuildingPlacement.ClampLevel(-4, 3), Is.EqualTo(1));
            Assert.That(BuildingPlacement.ClampLevel(2, 3), Is.EqualTo(2));
            Assert.That(BuildingPlacement.ClampLevel(9, 3), Is.EqualTo(3), "세이브가 데이터보다 앞서도 멈추지 않는다");
        }

        // ---------- 문 열림 진행도 ----------

        [Test]
        public void DOOR_PROGRESS_001_ProgressCountsFromZeroAtTheEnteringFrameToOneAtTheOpenTime()
        {
            var door = new DoorArea("a", new AreaRect(0, 0, 32, 32), new TravelTarget("X", "x"), 0.3f);
            var watch = new DoorWatch(new[] { door });
            watch.Reset(new AreaRect(200, 200, 32, 32));
            Assert.That(watch.Progress, Is.EqualTo(0f));
            watch.Update(new AreaRect(0, 0, 32, 32), 1f / 60f);
            Assert.That(watch.Opening, Is.SameAs(door));
            Assert.That(watch.Progress, Is.EqualTo(0f), "들어선 프레임은 0");
            for (int i = 0; i < 9; i++) watch.Update(new AreaRect(0, 0, 32, 32), 1f / 60f);
            Assert.That(watch.Progress, Is.EqualTo(9f / 60f / 0.3f).Within(1e-4f));
            DoorArea done = null;
            for (int i = 0; i < 60 && done == null; i++) done = watch.Update(new AreaRect(0, 0, 32, 32), 1f / 60f);
            Assert.That(done, Is.SameAs(door));
            Assert.That(watch.Progress, Is.EqualTo(0f), "열린 뒤에는 열리는 문이 없다");
        }

        [Test]
        public void DOOR_PROGRESS_002_SessionExposesTheSameProgressAsItsDoorWatch()
        {
            var maps = new LotMaps();
            var session = new WorldSession(maps, WalkerSettings.UnityPlayer);
            Assert.That(session.Enter("Yard", "front"), Is.True);
            Assert.That(session.OpeningProgress, Is.EqualTo(0f));
            int frames = 0;
            while (session.OpeningDoor == null && frames++ < 120) session.Update(0, -1, 1f / 60f);
            Assert.That(session.OpeningDoor, Is.Not.Null);
            Assert.That(session.OpeningProgress, Is.EqualTo(0f));
            session.Update(0, -1, 1f / 60f);
            session.Update(0, -1, 1f / 60f);
            Assert.That(session.OpeningProgress, Is.EqualTo(2f / 60f / DoorWatch.UnityOpeningSeconds).Within(1e-4f));
        }

        // ---------- 다시 읽기 ----------

        /// <summary>마당 하나: 단계에 따라 막힌 칸이 달라진다(Big은 Small보다 넓다).</summary>
        private sealed class LotMaps : IWorldMapSource
        {
            public BuildingFootprint Footprint = Small;
            public int Loads;

            public WorldMap Load(string mapId)
            {
                Loads++;
                var grid = new MapGrid(12, 10, Cell);
                var blocked = new bool[grid.Count];
                BuildingPlacementResult placed = BuildingPlacement.Place(Lot, Footprint);
                foreach (CellPos c in placed.Blocked) blocked[grid.Index(c.X, c.Y)] = true;
                var door = new DoorArea("lot", placed.DoorArea(Cell), new TravelTarget("Inside", "in"));
                var spawns = new Dictionary<string, (float X, float Y)> { { "front", CellPos.Center(new CellPos(6, 8), Cell) } };
                return new WorldMap(mapId, new CollisionGrid(grid, blocked), new[] { door }, spawns);
            }
        }

        [Test]
        public void RELOAD_001_ReloadReadsTheMapAgainAndMovesTheBodyOutOfNewlyBlockedCells()
        {
            var maps = new LotMaps();
            var session = new WorldSession(maps, WalkerSettings.UnityPlayer);
            Assert.That(session.Enter("Yard", "front"), Is.True);
            // 준비: 1단계에서 열려 있고 2단계에서 막히는 칸(4,6)에 선다.
            session.Walker.PlaceAt(CellPos.Center(new CellPos(4, 6), Cell));
            Assert.That(session.Map.Collision.AnyBlocked(session.Walker.Box), Is.False);

            maps.Footprint = Big;
            int travels = session.Travels;
            Assert.That(session.Reload(), Is.True);
            Assert.That(maps.Loads, Is.EqualTo(2));
            Assert.That(session.Map.Collision.IsBlocked(4, 6), Is.True, "새 맵을 쓴다");
            Assert.That(session.Map.Collision.AnyBlocked(session.Walker.Box), Is.False, "몸은 열린 칸에 있다");
            CellPos now = CellPos.Containing(session.Walker.X, session.Walker.Y, Cell);
            Assert.That(Math.Abs(now.X - 4) + Math.Abs(now.Y - 6), Is.EqualTo(1), "가장 가까운 열린 칸(한 칸 옆)");
            Assert.That(session.Travels, Is.EqualTo(travels), "다시 읽기는 이동이 아니다");
        }

        [Test]
        public void RELOAD_002_ReloadOnAnOpenCellKeepsThePlaceAndRestartsTheDoorWatch()
        {
            var maps = new LotMaps();
            var session = new WorldSession(maps, WalkerSettings.UnityPlayer);
            session.Enter("Yard", "front");
            for (int i = 0; i < 120 && session.OpeningDoor == null; i++) session.Update(0, -1, 1f / 60f);
            Assert.That(session.OpeningDoor, Is.Not.Null);
            float x = session.Walker.X, y = session.Walker.Y;
            Assert.That(session.Reload(), Is.True);
            Assert.That((session.Walker.X, session.Walker.Y), Is.EqualTo((x, y)));
            Assert.That(session.OpeningDoor, Is.Null, "문 지켜보기는 새 문으로 다시 시작한다");
            Assert.That(session.Map.Doors.Single(), Is.Not.Null);
            // 문 상자에 이미 겹쳐 있었으니 나갔다 다시 들어와야 열린다.
            for (int i = 0; i < 5; i++) session.Update(0, -1, 1f / 60f);
            Assert.That(session.OpeningDoor, Is.Null);
        }
    }
}
