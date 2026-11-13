using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Inventory;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;

namespace Rootborn.Core.Tests
{
    /// <summary>
    /// 집 맵을 바꾼 뒤 옛 가구 기록 — 못 놓은 기록을 가까운 자리로 옮기고, 그래도 못 놓은 것은 보관한다(HOUSE-002, 설계 7.3절).
    /// 옮기기는 아무것도 잃지 않는다: 방향·상태·그림 이름·상자 내용물이 그대로이고, 데이터에 없는 가구와 다른 면의 기록은 옮기지 않고 그대로 둔다.
    /// </summary>
    public sealed class FurnitureRelocationTests
    {
        private const string Surface = "house";

        private static GridPoint P(int x, int y) => new GridPoint(x, y);

        private static FurnitureDefinition Furniture(string id, bool blocks = true, GridPoint[] parts = null, GridPoint[] footprint = null)
        {
            var tileParts = (parts ?? new[] { P(0, 0) }).Select(cell => new FurnitureTilePart(cell, "sprite.png", "tile")).ToArray();
            return new FurnitureDefinition(id, id, null, "Seating", new[] { Surface }, tileParts, footprint, blocks, FurniturePlacementPreference.Any);
        }

        private static readonly FurnitureDefinition Chair = Furniture("chair");
        private static readonly FurnitureDefinition Rug = Furniture("rug", blocks: false);
        private static readonly FurnitureDefinition Desk = Furniture("desk", parts: new[] { P(0, 0), P(1, 0) });

        // 그림이 기준 칸 위 칸까지 올라가는 등(차지 칸은 기준 칸 하나).
        private static readonly FurnitureDefinition Lamp = Furniture("lamp", blocks: false, parts: new[] { P(0, 0), P(0, 1) }, footprint: new[] { P(0, 0) });

        private static readonly FurnitureDefinition[] Catalog = { Chair, Rug, Desk, Lamp };

        private static FurniturePlacementSaveData Save(string id, int x, int y, FurniturePlacementDirection direction = FurniturePlacementDirection.North, string surface = Surface)
            => new FurniturePlacementSaveData
            {
                FurnitureId = id, SurfaceId = surface, AnchorX = x, AnchorY = y, Direction = direction,
                StateJson = "{\"quality\":42}", TileName = "retained-original",
            };

        private static IEnumerable<GridPoint> Rect(int xMin, int xMax, int yMin, int yMax)
        {
            for (int x = xMin; x <= xMax; x++)
                for (int y = yMin; y <= yMax; y++)
                    yield return P(x, y);
        }

        /// <summary>x 0..5, y 0..4 바닥. 들어오는 칸 (0,0).</summary>
        private static HousePlacementModel Room(params GridPoint[] fixedCells)
            => new HousePlacementModel(new PlacementGrid(Rect(0, 5, 0, 4), fixedCells, P(0, 0), new[] { P(0, 0) }));

        /// <summary>y=0 한 줄 복도(x 0..4) + 옆 주머니 (2,1). 들어오는 칸 (0,0), 문 앞 (4,0).</summary>
        private static HousePlacementModel Corridor(bool pocket)
        {
            var floor = Rect(0, 4, 0, 0).ToList();
            if (pocket) floor.Add(P(2, 1));
            return new HousePlacementModel(new PlacementGrid(floor, null, P(0, 0), new[] { P(0, 0), P(4, 0) }));
        }

        private static (string, string, int, int, FurniturePlacementDirection, string, string, string) Whole(FurniturePlacementSaveData r)
            => (r.FurnitureId, r.SurfaceId, r.AnchorX, r.AnchorY, r.Direction, r.StateJson, r.TileName,
                r.Storage == null ? "<none>" : string.Join(",", r.Storage.Items.Select(i => i.ItemId + "x" + i.Count)) + "|" + string.Join(",", r.Storage.Requests));

        [Test]
        public void BlockedRecord_MovesToTheNearestFittingCell_SameDirection()
        {
            // 동쪽 책상은 기준 칸과 그 아래 칸을 차지한다. (2,2)가 막혀 있다.
            var model = Room(P(2, 2));
            var plan = FurnitureReplay.PlanRelocating(model, Surface, new[] { Save("desk", 2, 2, FurniturePlacementDirection.East) }, Catalog);

            Assert.That(plan.Unresolved, Is.Empty);
            var entry = plan.Accepted.Single();
            Assert.That(entry.Moved, Is.True);
            // 거리 1: 위 (2,3)은 아래 칸 (2,2)가 막혀 안 되고, 다음은 같은 줄 왼쪽 (1,2).
            Assert.That(P(entry.Record.AnchorX, entry.Record.AnchorY), Is.EqualTo(P(1, 2)));
            Assert.That(entry.Record.Direction, Is.EqualTo(FurniturePlacementDirection.East), "같은 방향으로 놓는다");
            Assert.That(entry.Cells, Is.EquivalentTo(new[] { P(1, 2), P(1, 1) }));
            Assert.That(entry.SourceIndex, Is.EqualTo(0));
        }

        [Test]
        public void TiesGoUpThenLeft()
        {
            var model = Room(P(2, 1));
            var records = new[] { Save("rug", 2, 1), Save("rug", 2, 1), Save("rug", 2, 1), Save("rug", 2, 1) };
            var plan = FurnitureReplay.PlanRelocating(model, Surface, records, Catalog);

            // 거리 1인 네 칸: 위 (2,2), 같은 줄 왼쪽 (1,1)·오른쪽 (3,1), 아래 (2,0).
            Assert.That(plan.Accepted.Select(e => P(e.Record.AnchorX, e.Record.AnchorY)), Is.EqualTo(new[] { P(2, 2), P(1, 1), P(3, 1), P(2, 0) }));
            Assert.That(plan.Accepted.Select(e => e.SourceIndex), Is.EqualTo(new[] { 0, 1, 2, 3 }), "기록 순서대로");
        }

        [Test]
        public void RecordsThatFitStay_AndAreNotDisplacedByMovedOnes()
        {
            var model = Room(P(2, 1));
            // 앞의 기록은 막힌 칸, 뒤의 기록은 그 가장 가까운 칸 (2,2)에 제자리로 있다.
            var plan = FurnitureReplay.PlanRelocating(model, Surface, new[] { Save("rug", 2, 1), Save("rug", 2, 2) }, Catalog);

            Assert.That(plan.Accepted.Select(e => (e.SourceIndex, e.Moved, P(e.Record.AnchorX, e.Record.AnchorY))),
                Is.EqualTo(new[] { (0, true, P(1, 1)), (1, false, P(2, 2)) }), "제자리 기록을 먼저 받는다 — 옮기는 기록이 밀어내지 않는다");
        }

        [Test]
        public void MovedFurniture_DoesNotOverlapCellsOrTiles_AndKeepsAccess()
        {
            // 그림 칸: 등(제자리 (2,2))의 그림이 (2,3)까지 올라간다. 위 줄 (1..3,4)가 막혀 있어 (2,4)의 의자는 거리 1의 (2,3)이 먼저지만 그림 칸이라 안 된다.
            var model = Room(P(1, 4), P(2, 4), P(3, 4));
            var plan = FurnitureReplay.PlanRelocating(model, Surface, new[] { Save("lamp", 2, 2), Save("rug", 2, 4) }, Catalog);
            var rug = plan.Accepted.Single(e => e.Record.FurnitureId == "rug");
            Assert.That(rug.Moved, Is.True);
            Assert.That(P(rug.Record.AnchorX, rug.Record.AnchorY), Is.EqualTo(P(0, 4)), "그림 칸 (2,3)과 차지 칸 (2,2)를 건너뛰고 거리 2의 위·왼쪽");

            // 길: 복도 가운데 (2,0)이 가장 가깝지만 막는 의자를 두면 문 앞 (4,0)에 못 간다 → 옆 주머니 (2,1).
            var corridor = Corridor(pocket: true);
            var chair = FurnitureReplay.PlanRelocating(corridor, Surface, new[] { Save("chair", 2, -1) }, Catalog).Accepted.Single();
            Assert.That(P(chair.Record.AnchorX, chair.Record.AnchorY), Is.EqualTo(P(2, 1)));
            Assert.That(corridor.PreservesAccess(chair.Cells), Is.True);
        }

        [Test]
        public void ChestWithItems_MovesWithItsStorage()
        {
            var model = Room(P(3, 3));
            var chest = Save("chair", 3, 3, FurniturePlacementDirection.South);
            chest.Storage = new FurnitureStorageSaveData
            {
                Items = new[] { new PlayerInventoryItemSaveData { ItemId = "item.wood", Count = 4 }, new PlayerInventoryItemSaveData { ItemId = "item.gone", Count = 1 } },
                Requests = new[] { "req-1" },
            };

            var layout = new HouseFurnitureLayout(model, Surface);
            var relocation = layout.RestoreRelocating(new[] { chest }, Catalog);

            Assert.That(relocation.Moved, Is.EqualTo(1));
            var placed = layout.Placed.Single();
            Assert.That(placed.HoldsItems, Is.True);
            var saved = layout.BuildSaveRecords().Single();
            var expected = chest.Clone();
            expected.AnchorX = placed.Anchor.X;
            expected.AnchorY = placed.Anchor.Y;
            Assert.That(Whole(saved), Is.EqualTo(Whole(expected)), "기준 칸만 바뀌고 방향·상태·그림 이름·내용물(모르는 아이템·요청 포함)은 그대로");
            Assert.That(P(chest.AnchorX, chest.AnchorY), Is.EqualTo(P(3, 3)), "원래 기록은 바뀌지 않는다(사본)");
            Assert.That(layout.Delete(placed.InstanceId), Is.False, "물건이 든 상자는 옮긴 뒤에도 치우지 않는다");
        }

        [Test]
        public void UnknownOrWrongSurfaceRecords_AreKeptUnmoved_AndNotInStored()
        {
            var model = Room(P(2, 2));
            var records = new[] { Save("old.no-such-furniture", 2, 2), Save("chair", 2, 2, surface: "workspace.other"), Save("chair", 9, 9, (FurniturePlacementDirection)17) };
            var layout = new HouseFurnitureLayout(model, Surface);
            var relocation = layout.RestoreRelocating(records, Catalog);

            Assert.That(layout.Placed, Is.Empty);
            Assert.That(layout.UnresolvedCount, Is.EqualTo(3));
            Assert.That(layout.Stored, Is.Empty, "보관 목록은 정의가 있고 칸이 막혀 못 놓은 것만");
            Assert.That((relocation.Moved, relocation.Stored), Is.EqualTo((0, 0)));
            Assert.That(layout.BuildSaveRecords().Select(Whole), Is.EqualTo(records.Select(Whole)), "그대로 다시 쓴다");
        }

        [Test]
        public void NoRoomAnywhere_GoesToStored_AndIsSavedBack()
        {
            // 주머니 없는 복도: 막는 의자를 어디 두어도 문 앞에 못 간다(양 끝은 들어오는 칸과 문 앞).
            var model = Corridor(pocket: false);
            var chest = Save("chair", 2, 0, FurniturePlacementDirection.West);
            chest.Storage = new FurnitureStorageSaveData { Items = new[] { new PlayerInventoryItemSaveData { ItemId = "item.wood", Count = 2 } } };
            var records = new[] { Save("rug", 1, 0), chest };
            var layout = new HouseFurnitureLayout(model, Surface);
            var relocation = layout.RestoreRelocating(records, Catalog);

            Assert.That(layout.Placed.Select(p => p.Definition.StableId), Is.EqualTo(new[] { "rug" }));
            var stored = layout.Stored.Single();
            Assert.That((stored.Index, stored.Definition, stored.Direction, stored.HoldsItems), Is.EqualTo((1, Chair, FurniturePlacementDirection.West, true)));
            Assert.That((relocation.Moved, relocation.Stored), Is.EqualTo((0, 1)));
            Assert.That(layout.BuildSaveRecords().Select(Whole), Is.EqualTo(records.Select(Whole)), "보관한 것은 원래 기준 칸으로 남는다");
        }

        [Test]
        public void SameInput_SameResult()
        {
            var model = Room(P(2, 1), P(3, 1), P(2, 3));
            var records = new[] { Save("desk", 2, 1), Save("chair", 3, 1), Save("rug", 2, 3), Save("chair", 0, 0), Save("desk", 2, 3, FurniturePlacementDirection.East) };
            var first = FurnitureReplay.PlanRelocating(model, Surface, records, Catalog);
            var second = FurnitureReplay.PlanRelocating(Room(P(2, 1), P(3, 1), P(2, 3)), Surface, records, Catalog);

            Assert.That(second.Accepted.Select(e => (e.SourceIndex, e.Moved, Whole(e.Record))), Is.EqualTo(first.Accepted.Select(e => (e.SourceIndex, e.Moved, Whole(e.Record)))));
            Assert.That(second.Unresolved.Select(e => (e.SourceIndex, e.Reason)), Is.EqualTo(first.Unresolved.Select(e => (e.SourceIndex, e.Reason))));

            // 옮긴 자리를 저장한 뒤 다시 읽으면 모두 제자리다(같은 배치).
            var layout = new HouseFurnitureLayout(model, Surface);
            layout.RestoreRelocating(records, Catalog);
            var again = new HouseFurnitureLayout(model, Surface);
            var relocation = again.RestoreRelocating(layout.BuildSaveRecords(), Catalog);
            Assert.That(relocation.Moved, Is.Zero);
            Assert.That(again.Placed.Select(p => (p.Definition.StableId, p.Anchor, p.Direction)), Is.EquivalentTo(layout.Placed.Select(p => (p.Definition.StableId, p.Anchor, p.Direction))));
        }

        [Test]
        public void Restore_IsUnchanged()
        {
            var model = Room(P(2, 2));
            var layout = new HouseFurnitureLayout(model, Surface);
            layout.Restore(new[] { Save("chair", 2, 2), Save("chair", 4, 4) }, Catalog);

            Assert.That(layout.Placed.Select(p => p.Anchor), Is.EqualTo(new[] { P(4, 4) }), "Restore는 옮기지 않는다(일터가 쓴다)");
            Assert.That(layout.UnresolvedCount, Is.EqualTo(1));
            Assert.That(FurnitureReplay.Plan(model, Surface, new[] { Save("chair", 2, 2) }, Catalog).Accepted, Is.Empty);
        }

        [Test]
        public void PlaceStored_FitsOrChangesNothing()
        {
            // 바닥: 들어오는 칸 (0,0)과 한 줄 (0..2,1). 동쪽(세로 두 칸) 탁자는 어디에도 안 들어가 보관되고, 북쪽(가로 두 칸)으로는 들어간다.
            var table = Furniture("table", blocks: false, parts: new[] { P(0, 0), P(1, 0) });
            var catalog = new[] { table };
            var model = new HousePlacementModel(new PlacementGrid(new[] { P(0, 0), P(0, 1), P(1, 1), P(2, 1) }, null, P(0, 0), new[] { P(0, 0) }));
            var chest = Save("table", 5, 5, FurniturePlacementDirection.East);
            chest.Storage = new FurnitureStorageSaveData { Items = new[] { new PlayerInventoryItemSaveData { ItemId = "item.wood", Count = 2 } } };
            var unknown = Save("old.no-such-furniture", 1, 1);
            bool allow = false;
            int commits = 0;
            FurniturePlacementSaveData[] committed = null;
            var layout = new HouseFurnitureLayout(model, Surface, r => { commits++; committed = r; return allow; });
            int announced = 0;
            layout.PlacementCommitted += _ => announced++;
            layout.RestoreRelocating(new[] { chest, unknown }, catalog);
            var before = layout.BuildSaveRecords().Select(Whole).ToArray();
            int index = layout.Stored.Single().Index;

            Assert.That(index, Is.EqualTo(0), "Index는 읽은 기록 안의 자리다");

            // 맞지 않는 칸·방향: 아무것도 하지 않는다.
            Assert.That(layout.CanPlaceStored(index, P(2, 1), FurniturePlacementDirection.North), Is.False, "(3,1)은 바닥이 아니다");
            Assert.That(layout.TryPlaceStored(index, P(0, 1), FurniturePlacementDirection.East), Is.False, "처음 방향으로는 어디에도 안 들어간다");
            Assert.That(layout.TryPlaceStored(7, P(0, 1), FurniturePlacementDirection.North), Is.False, "보관 목록에 없는 index");
            Assert.That(commits, Is.Zero, "맞지 않으면 저장하려 하지도 않는다");

            // 맞지만 저장이 실패한다: 무변경.
            Assert.That(layout.CanPlaceStored(index, P(0, 1), FurniturePlacementDirection.North), Is.True);
            Assert.That(layout.TryPlaceStored(index, P(0, 1), FurniturePlacementDirection.North), Is.False);
            Assert.That(commits, Is.EqualTo(1));
            Assert.That(layout.Placed, Is.Empty);
            Assert.That(layout.Stored.Count, Is.EqualTo(1));
            Assert.That(layout.BuildSaveRecords().Select(Whole), Is.EqualTo(before));

            // 저장이 되면 놓인다: 새 기준 칸·방향, 상태·그림 이름·내용물 그대로, 모르는 기록도 함께 저장.
            allow = true;
            Assert.That(layout.TryPlaceStored(index, P(0, 1), FurniturePlacementDirection.North), Is.True);
            var placed = layout.Placed.Single();
            Assert.That((placed.Anchor, placed.Direction, placed.HoldsItems), Is.EqualTo((P(0, 1), FurniturePlacementDirection.North, true)));
            Assert.That(layout.Stored, Is.Empty);
            var expected = chest.Clone();
            expected.AnchorX = 0;
            expected.AnchorY = 1;
            expected.Direction = FurniturePlacementDirection.North;
            Assert.That(committed.Select(Whole), Is.EqualTo(new[] { Whole(expected), Whole(unknown) }));
            Assert.That(layout.BuildSaveRecords().Select(Whole), Is.EqualTo(committed.Select(Whole)));
            Assert.That(announced, Is.Zero, "다시 놓은 것은 새 가구 배치 이벤트가 아니다");

            // 같은 요청 두 번: 두 번째는 아무것도 하지 않는다.
            Assert.That(layout.TryPlaceStored(index, P(0, 1), FurniturePlacementDirection.North), Is.False);
            Assert.That(commits, Is.EqualTo(2));
        }

        [Test]
        public void RelocationCounts()
        {
            var model = Corridor(pocket: true);
            var records = new[] { Save("rug", 1, 0), Save("chair", 2, -1), Save("chair", 2, -2), Save("old.no-such-furniture", 0, 0), Save("rug", 9, 9) };
            var layout = new HouseFurnitureLayout(model, Surface);
            var relocation = layout.RestoreRelocating(records, Catalog);

            // 의자 하나는 주머니 (2,1)로, 둘째 의자는 둘 곳이 없어 보관, 멀리 있던 깔개는 빈 복도 칸으로.
            Assert.That((relocation.Moved, relocation.Stored), Is.EqualTo((2, 1)));
            Assert.That(layout.Stored.Single().Index, Is.EqualTo(2));
            Assert.That(layout.Placed.Count + layout.UnresolvedCount, Is.EqualTo(records.Length), "아무것도 잃지 않는다");
        }
    }
}
