// 옮겨 옴: Assets/Tests/EditMode/World/AuthoredHousePlacementCaptureTests.cs, AuthoredHouseFurnitureReplayTests.cs,
//          AuthoredHousePlacementIntegrationTests.cs(배치 판정 부분), Assets/Tests/EditMode/Placement/FurniturePlacementDomainTests.cs,
//          FurniturePlacementServiceTests.cs(회전), FurniturePlacementRegistryTests.cs(칸으로 찾기·옮기기)
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;

namespace Rootborn.Core.Tests
{
    public sealed class FurniturePlacementTests
    {
        private static GridPoint P(int x, int y) => new GridPoint(x, y);

        private static FurnitureDefinition Furniture(string id, bool blocks = true, string[] surfaces = null, GridPoint[] parts = null, GridPoint[] footprint = null)
        {
            var tileParts = (parts ?? new[] { P(0, 0) }).Select(cell => new FurnitureTilePart(cell, "sprite.png", "tile")).ToArray();
            return new FurnitureDefinition(id, id, null, "Seating", surfaces ?? new[] { "house" }, tileParts, footprint, blocks, FurniturePlacementPreference.Any);
        }

        private static FurniturePlacementSaveData Save(string id, int x, int y, FurniturePlacementDirection direction = FurniturePlacementDirection.North)
            => new FurniturePlacementSaveData { FurnitureId = id, SurfaceId = "house", AnchorX = x, AnchorY = y, Direction = direction, StateJson = "{\"quality\":42}", TileName = "retained-original" };

        private static IEnumerable<GridPoint> Rect(int xMin, int xMax, int yMin, int yMax)
        {
            for (int x = xMin; x <= xMax; x++)
                for (int y = yMin; y <= yMax; y++)
                    yield return P(x, y);
        }

        // --- 정의 ---

        [Test]
        public void Definition_RequiresAStableId()
        {
            Assert.Throws<ArgumentException>(() => Furniture(""));
            Assert.Throws<ArgumentException>(() => Furniture(null));
        }

        [Test]
        public void Definition_UsesTilePartCellsAsDefaultFootprint()
        {
            var desk = new FurnitureDefinition("desk.large", "Large Desk", null, "desk", new[] { "house", "test-surface", "house", "" },
                new[] { new FurnitureTilePart(P(0, 0), "a.png", "a"), new FurnitureTilePart(P(1, 0), "b.png", "b") },
                null, true, FurniturePlacementPreference.AvoidBlockedCells);

            Assert.That(desk.StableId, Is.EqualTo("desk.large"));
            Assert.That(desk.DisplayName, Is.EqualTo("Large Desk"));
            Assert.That(desk.Category, Is.EqualTo("desk"));
            Assert.That(desk.AllowedSurfaceIds, Is.EqualTo(new[] { "house", "test-surface" }));
            Assert.That(desk.TileParts.Count, Is.EqualTo(2));
            Assert.That(desk.FootprintCells, Is.EquivalentTo(new[] { P(0, 0), P(1, 0) }));
            Assert.That(desk.BlocksMovement, Is.True);
            Assert.That(desk.PlacementPreference, Is.EqualTo(FurniturePlacementPreference.AvoidBlockedCells));
        }

        [Test]
        public void Definition_DisplayNameFallsBackToTheIdAndNegativePriceIsZero()
        {
            var chair = new FurnitureDefinition("chair", " ", null, null, null, null, null, true, FurniturePlacementPreference.Any, price: -5);
            Assert.That(chair.DisplayName, Is.EqualTo("chair"));
            Assert.That(chair.Price, Is.EqualTo(0));
            Assert.That(chair.AllowsSurface("anything"), Is.True, "빈 허용 목록은 모든 면에 놓을 수 있다");
        }

        [Test]
        public void EastDirection_RotatesFootprintAndTilePartsTheSameWay()
        {
            var desk = Furniture("desk", parts: new[] { P(0, 0), P(1, 0) });
            Assert.That(desk.GetFootprintCells(FurniturePlacementDirection.East), Is.EquivalentTo(new[] { P(0, 0), P(0, -1) }));
            Assert.That(desk.GetTileParts(FurniturePlacementDirection.East).Select(p => p.LocalCell), Is.EquivalentTo(new[] { P(0, 0), P(0, -1) }));
            Assert.That(desk.GetFootprintCells(FurniturePlacementDirection.South), Is.EquivalentTo(new[] { P(0, 0), P(-1, 0) }));
            Assert.That(desk.GetFootprintCells(FurniturePlacementDirection.West), Is.EquivalentTo(new[] { P(0, 0), P(0, 1) }));
            Assert.That(desk.GetFootprintCells(FurniturePlacementDirection.North), Is.EquivalentTo(new[] { P(0, 0), P(1, 0) }));
        }

        // --- 격자와 판정 (AuthoredHousePlacementCaptureTests) ---

        private static readonly GridPoint Free = P(-4, -3);
        private static readonly GridPoint Spawn = P(-3, -3);
        private static readonly GridPoint Door = P(-2, -3);

        private static HousePlacementModel SmallRoom(params GridPoint[] fixedCells)
            => new HousePlacementModel(new PlacementGrid(Rect(-4, -2, -3, -2), fixedCells, Spawn, new[] { Door }));

        [Test]
        public void Grid_KeepsNegativeCoordinates()
        {
            var model = SmallRoom();
            Assert.That(model.CanOccupy(new[] { Free }), Is.True);
            Assert.That(model.CanOccupy(new[] { P(0, 0) }), Is.False);
        }

        [Test]
        public void Grid_RejectsFixedCells()
        {
            var wall = P(-4, -2);
            var model = SmallRoom(wall, Free);
            Assert.That(model.CanOccupy(new[] { wall }), Is.False);
            Assert.That(model.CanOccupy(new[] { Free }), Is.False);
        }

        [Test]
        public void Grid_ReservesSpawnAndDoorApproach()
        {
            var model = SmallRoom();
            Assert.That(model.CanOccupy(new[] { Spawn }), Is.False);
            Assert.That(model.CanOccupy(new[] { Door }), Is.False);
        }

        [Test]
        public void Grid_IsASnapshotOfTheCellsItWasGiven()
        {
            var floor = Rect(-4, -2, -3, -2).ToList();
            var model = new HousePlacementModel(new PlacementGrid(floor, null, Spawn, new[] { Door }));
            floor.Remove(Free);
            Assert.That(model.CanOccupy(new[] { Free }), Is.True);
        }

        [Test]
        public void BlockingPlacement_CannotSealTheOnlyRouteToTheDoor()
        {
            var model = new HousePlacementModel(new PlacementGrid(Rect(-4, -2, -3, -3), null, Free, new[] { Door }));
            Assert.That(model.PreservesAccess(new[] { Spawn }), Is.False);
            Assert.That(model.PreservesAccess(Array.Empty<GridPoint>()), Is.True);
        }

        [Test]
        public void Footprint_IsRejectedWholeWhenOneCellIsReserved()
        {
            Assert.That(SmallRoom().CanOccupy(new[] { Free, Spawn }), Is.False);
            Assert.That(SmallRoom().CanOccupy(Array.Empty<GridPoint>()), Is.False);
        }

        [Test]
        public void TheRoomSizeComesFromTheGrid()
        {
            // 집은 28x18이 기준이지만 더 커질 수 있다(사용자 결정). 판정은 격자가 준 칸만 본다.
            var big = new HousePlacementModel(new PlacementGrid(Rect(0, 59, 0, 39), null, P(0, 0), new[] { P(59, 39) }));
            Assert.That(big.CanOccupy(new[] { P(45, 30) }), Is.True);
            Assert.That(big.CanOccupy(new[] { P(60, 30) }), Is.False);
        }

        // --- 되살리기 계획 (AuthoredHouseFurnitureReplayTests) ---

        private static HousePlacementModel ReplayRoom()
            => new HousePlacementModel(new PlacementGrid(Rect(0, 4, 0, 2), new[] { P(2, 1) }, P(0, 0), new[] { P(4, 0) }));

        private static readonly FurnitureDefinition Chair = Furniture("chair");

        [Test]
        public void Replay_AcceptsValidAndKeepsAFixedPropConflict()
        {
            var plan = FurnitureReplay.Plan(ReplayRoom(), "house", new[] { Save("chair", 1, 1), Save("chair", 2, 1) }, new[] { Chair });
            Assert.That(plan.Accepted.Count, Is.EqualTo(1));
            Assert.That(plan.Unresolved.Single().Reason, Is.EqualTo(FurnitureReplayFailure.Blocked));
        }

        [Test]
        public void Replay_KeepsAnUnknownDefinitionInsteadOfDroppingIt()
        {
            var plan = FurnitureReplay.Plan(ReplayRoom(), "house", new[] { Save("missing-chair", 1, 1) }, new[] { Chair });
            Assert.That(plan.Accepted, Is.Empty);
            Assert.That(plan.Unresolved.Single().Reason, Is.EqualTo(FurnitureReplayFailure.MissingDefinition));
        }

        [Test]
        public void Replay_DuplicateFootprintDoesNotOverwriteTheFirstRecord()
        {
            var plan = FurnitureReplay.Plan(ReplayRoom(), "house", new[] { Save("chair", 1, 1), Save("chair", 1, 1) }, new[] { Chair });
            Assert.That(plan.Accepted.Single().SourceIndex, Is.EqualTo(0));
            Assert.That(plan.Unresolved.Single().Reason, Is.EqualTo(FurnitureReplayFailure.DuplicateFootprint));
        }

        [Test]
        public void Replay_RejectsFurnitureWhoseDefinitionDisallowsTheSurface()
        {
            var outdoor = Furniture("outdoor", surfaces: new[] { "farm" });
            var plan = FurnitureReplay.Plan(ReplayRoom(), "house", new[] { Save("outdoor", 1, 1) }, new[] { Chair, outdoor });
            Assert.That(plan.Accepted, Is.Empty);
            Assert.That(plan.Unresolved.Single().Reason, Is.EqualTo(FurnitureReplayFailure.WrongSurface));
        }

        [Test]
        public void Replay_RejectsRecordsOfAnotherSurface()
        {
            var record = Save("chair", 1, 1);
            record.SurfaceId = "town";
            var plan = FurnitureReplay.Plan(ReplayRoom(), "house", new[] { record }, new[] { Chair });
            Assert.That(plan.Unresolved.Single().Reason, Is.EqualTo(FurnitureReplayFailure.WrongSurface));
        }

        [Test]
        public void Replay_RejectsAmbiguousDuplicateDefinitionIds()
        {
            var plan = FurnitureReplay.Plan(ReplayRoom(), "house", new[] { Save("chair", 1, 1) }, new[] { Chair, Furniture("chair") });
            Assert.That(plan.Accepted, Is.Empty);
            Assert.That(plan.Unresolved.Single().Reason, Is.EqualTo(FurnitureReplayFailure.AmbiguousDefinition));
        }

        [Test]
        public void Replay_RejectsAnUndefinedDirectionInsteadOfTreatingItAsNorth()
        {
            var plan = FurnitureReplay.Plan(ReplayRoom(), "house", new[] { Save("chair", 1, 1, (FurniturePlacementDirection)99) }, new[] { Chair });
            Assert.That(plan.Unresolved.Single().Reason, Is.EqualTo(FurnitureReplayFailure.InvalidDirection));
        }

        [Test]
        public void Replay_IgnoresNullCatalogEntriesAndNullRecords()
        {
            var plan = FurnitureReplay.Plan(ReplayRoom(), "house", new[] { null, Save("chair", 1, 1) }, new[] { null, Chair });
            Assert.That(plan.Accepted.Single().SourceIndex, Is.EqualTo(1));
            Assert.That(plan.Unresolved, Is.Empty);
        }

        [Test]
        public void Replay_DoesNotChangeTheSourceRecords()
        {
            var records = new[] { Save("chair", 1, 1), Save("chair", 2, 1) };
            var plan = FurnitureReplay.Plan(ReplayRoom(), "house", records, new[] { Chair });
            plan.Accepted[0].Record.AnchorX = 99;
            Assert.That(records[0].AnchorX, Is.EqualTo(1));
            Assert.That(records[1].StateJson, Is.EqualTo("{\"quality\":42}"));
        }

        [Test]
        public void Replay_RejectsABlockingPieceThatWouldSealTheDoor()
        {
            // 1줄 방: 출발 (0,0) → 문 (4,0). 가운데를 막는 가구는 받지 않는다. 막지 않는 가구는 받는다.
            var corridor = new HousePlacementModel(new PlacementGrid(Rect(0, 4, 0, 0), null, P(0, 0), new[] { P(4, 0) }));
            var rug = Furniture("rug", blocks: false);
            var plan = FurnitureReplay.Plan(corridor, "house", new[] { Save("chair", 2, 0), Save("rug", 2, 0) }, new[] { Chair, rug });
            Assert.That(plan.Unresolved.Single().Record.FurnitureId, Is.EqualTo("chair"));
            Assert.That(plan.Accepted.Single().Record.FurnitureId, Is.EqualTo("rug"));
        }

        // --- 집 가구 배치 (AuthoredHousePlacementIntegrationTests의 판정 부분) ---

        private static HouseFurnitureLayout Layout(Func<FurniturePlacementSaveData[], bool> commit = null)
            => new HouseFurnitureLayout(ReplayRoom(), "house", commit);

        [Test]
        public void RestoreAndExport_KeepUnknownAndBlockedRecordsWithTheirExtraFields()
        {
            var layout = Layout();
            layout.Restore(new[] { Save("chair", 1, 1), Save("missing", 3, 1), Save("chair", 2, 1) }, new[] { Chair });
            var records = layout.BuildSaveRecords();
            Assert.That(records.Length, Is.EqualTo(3));
            Assert.That(records.All(r => r.StateJson == "{\"quality\":42}" && r.TileName == "retained-original"), Is.True);
            Assert.That(layout.UnresolvedCount, Is.EqualTo(2));
        }

        [Test]
        public void RepeatedRestore_DoesNotDuplicateFurniture()
        {
            var layout = Layout();
            var records = new[] { Save("chair", 1, 1) };
            layout.Restore(records, new[] { Chair });
            layout.Restore(records, new[] { Chair });
            Assert.That(layout.BuildSaveRecords().Length, Is.EqualTo(1));
            Assert.That(layout.Placed.Count, Is.EqualTo(1));
        }

        [Test]
        public void ClearUserFurniture_KeepsUnresolvedRecords()
        {
            var layout = Layout();
            layout.Restore(new[] { Save("chair", 1, 1), Save("missing", 3, 1) }, new[] { Chair });
            Assert.That(layout.ClearUserFurniture(), Is.True);
            Assert.That(layout.Placed, Is.Empty);
            Assert.That(layout.BlockingCells, Is.Empty);
            Assert.That(layout.BuildSaveRecords().Single().FurnitureId, Is.EqualTo("missing"));
        }

        [Test]
        public void BlockingCells_FollowBlockingFurnitureOnly()
        {
            var layout = Layout();
            layout.Restore(new[] { Save("chair", 1, 1), Save("rug", 3, 1) }, new[] { Chair, Furniture("rug", blocks: false) });
            Assert.That(layout.BlockingCells, Is.EquivalentTo(new[] { P(1, 1) }));
        }

        [Test]
        public void Placement_RejectsAFixedCell_AndAcceptsAFreeOne_ThenAnnouncesIt()
        {
            var layout = Layout();
            var announced = new List<string>();
            layout.PlacementCommitted += definition => announced.Add(definition.StableId);
            Assert.That(layout.TryPlace(Chair, P(2, 1), FurniturePlacementDirection.North), Is.False);
            Assert.That(layout.TryPlace(Chair, P(1, 1), FurniturePlacementDirection.North), Is.True);
            Assert.That(layout.TryPlace(Chair, P(1, 1), FurniturePlacementDirection.North), Is.False, "같은 칸에 두 번 놓지 않는다");
            Assert.That(announced, Is.EqualTo(new[] { "chair" }));
            Assert.That(layout.TryFindAt(P(1, 1), out var placed), Is.True);
            Assert.That(placed.Definition, Is.SameAs(Chair));
        }

        [Test]
        public void Placement_RejectsATilePartOverAnotherPiecesTile()
        {
            // 그림 칸이 차지 칸보다 넓은 가구: 그림이 (1,2)까지 올라간다.
            var lamp = Furniture("lamp", parts: new[] { P(0, 0), P(0, 1) }, footprint: new[] { P(0, 0) });
            var layout = Layout();
            Assert.That(layout.TryPlace(lamp, P(1, 1), FurniturePlacementDirection.North), Is.True);
            Assert.That(layout.CanPlace(Chair, P(1, 2), FurniturePlacementDirection.North), Is.False);
            Assert.That(layout.CanPlace(Chair, P(3, 2), FurniturePlacementDirection.North), Is.True);
        }

        [Test]
        public void Placement_RejectsNullUndefinedDirectionAndWrongSurface()
        {
            var layout = Layout();
            Assert.That(layout.CanPlace(null, P(1, 1), FurniturePlacementDirection.North), Is.False);
            Assert.That(layout.CanPlace(Chair, P(1, 1), (FurniturePlacementDirection)7), Is.False);
            Assert.That(layout.CanPlace(Furniture("outdoor", surfaces: new[] { "farm" }), P(1, 1), FurniturePlacementDirection.North), Is.False);
        }

        [Test]
        public void FailedPlacementWrite_ChangesNothing()
        {
            var layout = Layout(records => false);
            var announced = 0;
            layout.PlacementCommitted += _ => announced++;
            Assert.That(layout.TryPlace(Chair, P(1, 1), FurniturePlacementDirection.North), Is.False);
            Assert.That(layout.Placed, Is.Empty);
            Assert.That(announced, Is.Zero);
        }

        [Test]
        public void Move_KeepsExtraFieldsAndMovesTheBlockingCell()
        {
            var layout = Layout();
            layout.Restore(new[] { Save("chair", 1, 1) }, new[] { Chair });
            var id = layout.Placed.Single().InstanceId;
            Assert.That(layout.TryMove(id, P(3, 1), FurniturePlacementDirection.East), Is.True);
            var record = layout.BuildSaveRecords().Single();
            Assert.That(record.StateJson, Is.EqualTo("{\"quality\":42}"));
            Assert.That(record.AnchorX, Is.EqualTo(3));
            Assert.That(record.Direction, Is.EqualTo(FurniturePlacementDirection.East));
            Assert.That(layout.BlockingCells, Is.EquivalentTo(new[] { P(3, 1) }));
            Assert.That(layout.TryFindAt(P(1, 1), out _), Is.False);
        }

        [Test]
        public void RejectedMove_KeepsTheOldPlaceAndRecord()
        {
            var layout = Layout();
            layout.Restore(new[] { Save("chair", 1, 1) }, new[] { Chair });
            var id = layout.Placed.Single().InstanceId;
            Assert.That(layout.TryMove(id, P(2, 1), FurniturePlacementDirection.North), Is.False);
            Assert.That(layout.TryMove("nobody#1", P(3, 1), FurniturePlacementDirection.North), Is.False);
            Assert.That(layout.BuildSaveRecords().Single().AnchorX, Is.EqualTo(1));
            Assert.That(layout.BlockingCells, Is.EquivalentTo(new[] { P(1, 1) }));
        }

        [Test]
        public void FailedMoveWrite_DoesNotPublishTheCandidatePlace()
        {
            var writes = 0;
            var layout = Layout(records => ++writes == 0);
            layout.Restore(new[] { Save("chair", 1, 1) }, new[] { Chair });
            var id = layout.Placed.Single().InstanceId;
            Assert.That(layout.TryMove(id, P(3, 1), FurniturePlacementDirection.North), Is.False);
            Assert.That(layout.BlockingCells, Is.EquivalentTo(new[] { P(1, 1) }));
            Assert.That(layout.BuildSaveRecords().Single().AnchorX, Is.EqualTo(1));
        }

        [Test]
        public void Move_OntoItsOwnOldCellsIsAllowed()
        {
            var desk = Furniture("desk", parts: new[] { P(0, 0), P(1, 0) });
            var layout = Layout();
            Assert.That(layout.TryPlace(desk, P(0, 2), FurniturePlacementDirection.North), Is.True);
            Assert.That(layout.TryMove(layout.Placed.Single().InstanceId, P(1, 2), FurniturePlacementDirection.North), Is.True);
            Assert.That(layout.Placed.Single().OccupiedCells, Is.EquivalentTo(new[] { P(1, 2), P(2, 2) }));
        }

        [Test]
        public void Delete_RemovesOnlyThatPieceAndWritesTheRest()
        {
            FurniturePlacementSaveData[] written = null;
            var layout = Layout(records => { written = records; return true; });
            layout.Restore(new[] { Save("chair", 1, 1), Save("chair", 3, 1), Save("missing", 3, 2) }, new[] { Chair });
            var first = layout.Placed[0].InstanceId;
            Assert.That(layout.Delete(first), Is.True);
            Assert.That(layout.Delete(first), Is.False);
            Assert.That(written.Select(r => r.FurnitureId + "@" + r.AnchorX), Is.EqualTo(new[] { "chair@3", "missing@3" }));
            Assert.That(layout.TryFindAt(P(1, 1), out _), Is.False);
        }

        [Test]
        public void InstanceIds_AreFurnitureIdAndANumberThatIsNotReused()
        {
            var layout = Layout();
            Assert.That(layout.TryPlace(Chair, P(1, 1), FurniturePlacementDirection.North), Is.True);
            var first = layout.Placed.Single().InstanceId;
            layout.Delete(first);
            Assert.That(layout.TryPlace(Chair, P(1, 1), FurniturePlacementDirection.North), Is.True);
            Assert.That(first, Is.EqualTo("chair#1"));
            Assert.That(layout.Placed.Single().InstanceId, Is.EqualTo("chair#2"));
        }

        // --- 받는 면 목록과 한꺼번에 바꾸기 (일터·템플릿) ---

        private const string Workplace = "workplace.test";

        private static FurniturePlacementRequest Req(FurnitureDefinition definition, int x, int y, FurniturePlacementDirection direction = FurniturePlacementDirection.North)
            => new FurniturePlacementRequest(definition, P(x, y), direction);

        [Test]
        public void AcceptedSurface_AllowsHouseFurnitureOnOtherSurface()
        {
            FurniturePlacementSaveData[] written = null;
            var layout = new HouseFurnitureLayout(ReplayRoom(), Workplace, records => { written = records; return true; }, new[] { "house" });

            Assert.That(layout.TryPlace(Chair, P(1, 1), FurniturePlacementDirection.North), Is.True);
            Assert.That(written.Single().SurfaceId, Is.EqualTo(Workplace), "기록의 면은 이 배치의 면이다");
            Assert.That(layout.TryMove(layout.Placed.Single().InstanceId, P(3, 1), FurniturePlacementDirection.North), Is.True);

            var restored = new HouseFurnitureLayout(ReplayRoom(), Workplace, null, new[] { "house" });
            restored.Restore(layout.BuildSaveRecords(), new[] { Chair });
            Assert.That(restored.Placed.Single().Anchor, Is.EqualTo(P(3, 1)));
            Assert.That(restored.UnresolvedCount, Is.Zero);
        }

        [Test]
        public void WithoutAcceptedSurface_StillRefused()
        {
            var plain = new HouseFurnitureLayout(ReplayRoom(), Workplace);
            Assert.That(plain.CanPlace(Chair, P(1, 1), FurniturePlacementDirection.North), Is.False);
            var record = Save("chair", 1, 1);
            record.SurfaceId = Workplace;
            plain.Restore(new[] { record }, new[] { Chair });
            Assert.That(plain.Placed, Is.Empty);
            Assert.That(plain.UnresolvedCount, Is.EqualTo(1));

            var accepting = new HouseFurnitureLayout(ReplayRoom(), Workplace, null, new[] { "house" });
            Assert.That(accepting.CanPlace(Furniture("outdoor", surfaces: new[] { "farm" }), P(1, 1), FurniturePlacementDirection.North), Is.False, "받는 목록에 없는 면의 가구");
            Assert.That(accepting.CanReplaceUserFurniture(new[] { Req(Furniture("outdoor", surfaces: new[] { "farm" }), 1, 1) }), Is.False);
        }

        [Test]
        public void ReplaceAll_AllOrNothing()
        {
            int commits = 0;
            var layout = Layout(records => { commits++; return true; });
            Assert.That(layout.TryPlace(Chair, P(1, 1), FurniturePlacementDirection.North), Is.True);
            var before = layout.BuildSaveRecords();

            var overlapping = new[] { Req(Chair, 3, 1), Req(Chair, 3, 1) };
            Assert.That(layout.CanReplaceUserFurniture(overlapping), Is.False);
            Assert.That(layout.TryReplaceUserFurniture(overlapping), Is.False, "둘째가 첫째와 겹친다");
            Assert.That(layout.TryReplaceUserFurniture(new[] { Req(Chair, 3, 1), Req(Chair, 2, 1) }), Is.False, "둘째가 고정물 칸");
            Assert.That(layout.TryReplaceUserFurniture(new[] { Req(Chair, 3, 1), Req(null, 1, 2) }), Is.False);
            Assert.That(layout.TryReplaceUserFurniture(null), Is.False);

            Assert.That(commits, Is.EqualTo(1));
            Assert.That(layout.Placed.Single().Anchor, Is.EqualTo(P(1, 1)));
            Assert.That(layout.BuildSaveRecords().Select(r => r.FurnitureId + "@" + r.AnchorX + "," + r.AnchorY),
                Is.EqualTo(before.Select(r => r.FurnitureId + "@" + r.AnchorX + "," + r.AnchorY)));
        }

        [Test]
        public void ReplaceAll_CommitsOnce()
        {
            int commits = 0;
            FurniturePlacementSaveData[] written = null;
            var layout = Layout(records => { commits++; written = records; return true; });
            Assert.That(layout.TryPlace(Chair, P(1, 1), FurniturePlacementDirection.North), Is.True);
            int announced = 0;
            layout.PlacementCommitted += _ => announced++;
            var rug = Furniture("rug", blocks: false);
            // 새 배치는 빈 방 기준이다: 지금 의자가 있는 (1,1)에도 놓을 수 있다.
            var template = new[] { Req(rug, 1, 1), Req(Chair, 3, 1, FurniturePlacementDirection.East) };

            Assert.That(layout.CanReplaceUserFurniture(template), Is.True);
            Assert.That(commits, Is.EqualTo(1), "Can은 저장하지 않는다");
            Assert.That(layout.Placed.Single().Definition, Is.SameAs(Chair), "Can은 바꾸지 않는다");

            Assert.That(layout.TryReplaceUserFurniture(template), Is.True);
            Assert.That(commits, Is.EqualTo(2));
            Assert.That(announced, Is.Zero, "한꺼번에 바꾸기는 놓기 알림을 내지 않는다");
            Assert.That(written.Select(r => r.FurnitureId + "@" + r.AnchorX + "," + r.AnchorY + ":" + r.Direction),
                Is.EqualTo(new[] { "rug@1,1:North", "chair@3,1:East" }));
            Assert.That(written.All(r => r.SurfaceId == "house"), Is.True);
            Assert.That(layout.Placed.Select(p => p.Definition.StableId), Is.EqualTo(new[] { "rug", "chair" }));
            Assert.That(layout.Placed.Select(p => p.InstanceId), Is.EqualTo(new[] { "rug#2", "chair#3" }), "인스턴스 번호는 이어진다");
            Assert.That(layout.BlockingCells, Is.EquivalentTo(new[] { P(3, 1) }));

            Assert.That(layout.TryReplaceUserFurniture(Array.Empty<FurniturePlacementRequest>()), Is.True, "빈 템플릿은 모두 치운다");
            Assert.That(layout.Placed, Is.Empty);
            Assert.That(commits, Is.EqualTo(3));
        }

        [Test]
        public void ReplaceAll_CommitFails_NothingChanges()
        {
            int writes = 0;
            var layout = Layout(records => ++writes == 1);
            Assert.That(layout.TryPlace(Chair, P(1, 1), FurniturePlacementDirection.North), Is.True);

            Assert.That(layout.TryReplaceUserFurniture(new[] { Req(Chair, 3, 1) }), Is.False);
            Assert.That(writes, Is.EqualTo(2));
            Assert.That(layout.Placed.Single().Anchor, Is.EqualTo(P(1, 1)));
            Assert.That(layout.Placed.Single().InstanceId, Is.EqualTo("chair#1"));
            Assert.That(layout.BlockingCells, Is.EquivalentTo(new[] { P(1, 1) }));
            Assert.That(layout.BuildSaveRecords().Single().AnchorX, Is.EqualTo(1));
        }

        [Test]
        public void ReplaceAll_KeepsUnresolvedRecords()
        {
            FurniturePlacementSaveData[] written = null;
            var layout = Layout(records => { written = records; return true; });
            layout.Restore(new[] { Save("chair", 1, 1), Save("missing", 3, 2) }, new[] { Chair });

            Assert.That(layout.TryReplaceUserFurniture(new[] { Req(Chair, 3, 1) }), Is.True);
            Assert.That(layout.UnresolvedCount, Is.EqualTo(1));
            Assert.That(written.Select(r => r.FurnitureId + "@" + r.AnchorX), Is.EqualTo(new[] { "chair@3", "missing@3" }));
            Assert.That(written[1].StateJson, Is.EqualTo("{\"quality\":42}"), "놓지 못한 기록은 그대로");
        }

        [Test]
        public void ReplaceAll_KeepsChestsThatHoldItems_AndPlacesAroundThem()
        {
            var layout = Layout();
            Assert.That(layout.TryPlace(Chair, P(1, 1), FurniturePlacementDirection.North), Is.True);
            Assert.That(layout.TryPlace(Chair, P(3, 1), FurniturePlacementDirection.North), Is.True);
            var chest = layout.Placed[1].InstanceId;
            layout.SetStorage(chest, new FurnitureStorageSaveData { Items = new[] { new Rootborn.Core.Inventory.PlayerInventoryItemSaveData { ItemId = "item.wood", Count = 2 } } });

            Assert.That(layout.CanReplaceUserFurniture(new[] { Req(Chair, 3, 1) }), Is.False, "물건이 든 상자 자리에는 놓지 않는다");
            Assert.That(layout.TryReplaceUserFurniture(new[] { Req(Chair, 1, 2) }), Is.True);
            Assert.That(layout.Placed.Select(p => p.Anchor), Is.EqualTo(new[] { P(3, 1), P(1, 2) }));
            Assert.That(layout.Placed[0].InstanceId, Is.EqualTo(chest));
            Assert.That(layout.Placed[0].HoldsItems, Is.True);
        }
    }
}
