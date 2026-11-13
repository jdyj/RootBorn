using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Inventory;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;
using Rootborn.Core.Room;
using Rootborn.Core.Save;
using Rootborn.World;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// HOUSE-002: 집 맵이 새 집(벽이 서 있는 키트 방)으로 바뀐 뒤 옛 세이브의 가구 — 새 집에서 벽이 된 칸의 가구는 가까운 자리로 옮겨지고,
    /// 둘 곳이 없으면 보관되고, 아무것도 사라지지 않는다(설계 7.3절). 실제 data/와 실제 집 맵, 임시 폴더 저장소. GameSession 명령으로 진행하는
    /// 자동 테스트다(화면 근거 아님 — 보관 분류와 알림 화면은 다음 작업).
    /// 세이브 모양은 그대로다(배치 파일 FurniturePlacementLayoutSaveData). 바뀐 것은 "옛 좌표의 기록을 새 집이 어떻게 읽는가"라서
    /// 하위호환(옛 기록이 열리고 남는다)과 상위호환(옮긴 뒤 쓴 파일을 옛 빌드가 읽고 플레이한다)을 여기서 본다.
    /// </summary>
    public sealed class HouseMoveTests
    {
        private const string FreeDesk = "growth31.assembly.desk";
        private const string Unknown = "old.no-such-furniture";

        private static string ChestFurniture => TestWorld.Definitions.All<FurnitureStorageDefinition>().First().Furniture.StableId;

        private static string LayoutPath(TempSaves saves, string slot = TestWorld.Slot) => Path.Combine(saves.Path, slot, SaveFileNames.HouseFurnitureLayout(0));

        /// <summary>새 집 맵에서 막힌(벽) 안쪽 칸들(가구 칸 좌표 — HouseRoom은 맵 칸을 그대로 쓴다).</summary>
        private static List<GridPoint> InnerWallCells()
        {
            var map = WorldMapLoader.FromRepo(TestWorld.RepoRoot).LoadMap(HouseRoom.MapId).World;
            return map.Collision.BlockedCells()
                .Where(c => c.X > 0 && c.Y > 0 && c.X < map.Grid.Columns - 1 && c.Y < map.Grid.Rows - 1)
                .Select(c => new GridPoint(c.X, c.Y))
                .ToList();
        }

        private static FurniturePlacementSaveData Record(string id, GridPoint at, FurniturePlacementDirection direction = FurniturePlacementDirection.North) => new FurniturePlacementSaveData
        {
            FurnitureId = id, SurfaceId = HouseRoom.SurfaceId, AnchorX = at.X, AnchorY = at.Y, Direction = direction,
            StateJson = "{\"kept\":1}", TileName = "old-tile",
        };

        private static (string, string, int, int, FurniturePlacementDirection, string, string, string) Whole(FurniturePlacementSaveData r)
            => (r.FurnitureId, r.SurfaceId, r.AnchorX, r.AnchorY, r.Direction, r.StateJson, r.TileName,
                r.Storage == null ? "<none>" : string.Join(",", r.Storage.Items.Select(i => i.ItemId + "x" + i.Count)) + "|" + string.Join(",", r.Storage.Requests));

        private static (string, string, int, int, FurniturePlacementDirection, string, string, string) WholeAt(FurniturePlacementSaveData r, GridPoint at)
        {
            var copy = r.Clone();
            copy.AnchorX = at.X;
            copy.AnchorY = at.Y;
            return Whole(copy);
        }

        /// <summary>옛 세이브: 벽이 된 칸의 책상(동쪽), 물건이 든 상자(벽이 된 다른 칸), 데이터에 없는 가구.</summary>
        private static FurniturePlacementSaveData[] OldLayout()
        {
            var walls = InnerWallCells();
            var chest = Record(ChestFurniture, walls[walls.Count / 2], FurniturePlacementDirection.South);
            chest.Storage = new FurnitureStorageSaveData
            {
                Items = new[] { new PlayerInventoryItemSaveData { ItemId = "Wood", Count = 5 }, new PlayerInventoryItemSaveData { ItemId = "item.gone-from-data", Count = 2 } },
                Requests = new[] { "old-request" },
            };
            return new[] { Record(FreeDesk, walls[0], FurniturePlacementDirection.East), chest, Record(Unknown, walls[1]) };
        }

        private static void WriteLayout(TempSaves saves, IReadOnlyList<FurniturePlacementSaveData> records, string slot = TestWorld.Slot)
            => saves.Store.Write(slot, SaveFileNames.HouseFurnitureLayout(0),
                new FurniturePlacementLayoutSaveData { Version = FurniturePlacementLayoutSaveData.KnownVersion, Items = records.ToArray() });

        private static GameSession EnterHouse(TempSaves saves, string slot = TestWorld.Slot)
        {
            var game = TestWorld.Start(saves.Store, slot);
            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            return game;
        }

        private static IEnumerable<(string, GridPoint, FurniturePlacementDirection)> Placement(GameSession game)
            => game.House.Placed.Select(p => (p.Definition.StableId, p.Anchor, p.Direction));

        /// <summary>옛 빌드의 집: 옛 maps.json의 House는 충돌 규칙 collider-layers에 층이 비어 있어(커밋 17f854976 전) 28x18 모든 칸이 열린 바닥이었다. 도착 지점은 같다.</summary>
        private static PlacementGrid OldBuildHouseGrid()
        {
            var now = HouseRoom.Grid;
            return new PlacementGrid(now.FloorCells, null, now.Spawn, now.Approaches);
        }

        [Test]
        public void HOUSE_002_OldLayoutOnNewWalls_IsMovedOrStored_NothingLost()
        {
            using var saves = new TempSaves();
            var old = OldLayout();
            WriteLayout(saves, old);

            var game = EnterHouse(saves);
            var desk = game.House.Placed.Single(p => p.Definition.StableId == FreeDesk);
            var chest = game.House.Placed.Single(p => p.Definition.StableId == ChestFurniture);
            Assert.That(desk.Anchor, Is.Not.EqualTo(new GridPoint(old[0].AnchorX, old[0].AnchorY)), "벽 칸에서 옮겨졌다");
            Assert.That(desk.Direction, Is.EqualTo(FurniturePlacementDirection.East), "방향 그대로");
            Assert.That(chest.HoldsItems, Is.True);
            Assert.That(game.StoredFurniture(), Is.Empty);

            var records = game.House.BuildSaveRecords();
            Assert.That(records.Select(Whole), Is.EquivalentTo(new[] { WholeAt(old[0], desk.Anchor), WholeAt(old[1], chest.Anchor), Whole(old[2]) }),
                "옮긴 것은 기준 칸만 바뀌고(상태·그림 이름·상자 내용물·모르는 아이템·요청 그대로) 데이터에 없는 가구는 그대로");
            Assert.That(game.House.Placed.Count + game.House.UnresolvedCount, Is.EqualTo(old.Length), "개수 합 = 처음 기록 수");
            Assert.That(game.StorageAt(chest.InstanceId).CountOf(game.Item("Wood")), Is.EqualTo(5), "상자 안 물건을 꺼낼 수 있다");

            Assert.That(game.TryTakeHouseRelocationNotice(out int moved, out int stored), Is.True);
            Assert.That((moved, stored), Is.EqualTo((2, 0)));
            Assert.That(game.TryTakeHouseRelocationNotice(out _, out _), Is.False, "이 판에서 한 번만");
        }

        [Test]
        public void EditorFixtureSlot_DeskFromUnityCoordinates_IsMovedIntoTheHouse()
        {
            using var saves = new TempSaves();
            saves.CopyFixture("editor-20261105/slot-1", "slot-1");
            byte[] before = File.ReadAllBytes(LayoutPath(saves, "slot-1"));

            var game = EnterHouse(saves, "slot-1");
            var desk = game.House.Placed.Single(p => p.Definition.StableId == "story.study-desk");
            Assert.That(HouseRoom.Grid.HasFloor(desk.Anchor) && !HouseRoom.Grid.IsFixed(desk.Anchor), Is.True, "Unity 좌표 (4,-5)의 책상이 집 안 바닥으로 옮겨졌다");
            Assert.That(game.House.UnresolvedCount, Is.Zero);
            Assert.That(File.ReadAllBytes(LayoutPath(saves, "slot-1")), Is.EqualTo(before), "고정 파일에서 복사한 배치 파일을 읽기만으로 쓰지 않는다");
        }

        [Test]
        public void EnteringTheHouse_AnnouncesFurnitureWhenPiecesWereMoved_ForTheNotice_AndNotWhenNothingMoved()
        {
            using var saves = new TempSaves();
            WriteLayout(saves, OldLayout());
            var game = TestWorld.Start(saves.Store);
            int announced = 0;
            game.Changed += t => { if (t == GameChangeTopics.Furniture) announced++; };
            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            Assert.That(announced, Is.GreaterThan(0), "알림 화면이 열리도록 가구 알림이 온다");
            Assert.That(game.TryTakeHouseRelocationNotice(out _, out _), Is.True);

            using var empty = new TempSaves();
            var plain = TestWorld.Start(empty.Store);
            int none = 0;
            plain.Changed += t => { if (t == GameChangeTopics.Furniture) none++; };
            plain.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            Assert.That(none, Is.Zero, "옮긴 것도 보관한 것도 없으면 알리지 않는다");
        }

        [Test]
        public void ReadingDoesNotWriteTheLayoutFile()
        {
            using var saves = new TempSaves();
            WriteLayout(saves, OldLayout());
            byte[] before = File.ReadAllBytes(LayoutPath(saves));

            var game = EnterHouse(saves);
            Assert.That(game.House.Placed.Count, Is.EqualTo(2));
            game.VisitLocation("location.town-square"); // 다른 것을 저장하는 명령
            EnterHouse(saves);
            Assert.That(File.ReadAllBytes(LayoutPath(saves)), Is.EqualTo(before), "들어가 읽고 저장해도 배치 파일은 그대로(옮긴 자리는 바꿀 때 쓴다)");
        }

        [Test]
        public void AfterAChange_TheFileHoldsMovedPositions_OldShapeReadsIt()
        {
            using var saves = new TempSaves();
            var old = OldLayout();
            WriteLayout(saves, old);
            var game = EnterHouse(saves);
            var moved = Placement(game).ToList();

            // 플레이어가 무엇을 바꾼다(책상을 하나 더 놓는다) → 배치 파일이 옮긴 자리로 다시 써진다.
            var extra = HouseRoom.FirstFreeAnchor(game, FreeDesk);
            Assert.That(game.PlaceFurniture(FreeDesk, extra, FurniturePlacementDirection.North), Is.True);
            Assert.That(saves.Store.TryRead<FurniturePlacementLayoutSaveData>(TestWorld.Slot, SaveFileNames.HouseFurnitureLayout(0), out var file), Is.True);
            Assert.That(file.Version, Is.EqualTo(FurniturePlacementLayoutSaveData.KnownVersion), "형식 버전을 올리지 않았다(모양 그대로)");
            var desk = moved.Single(m => m.Item1 == FreeDesk).Item2;
            var chest = moved.Single(m => m.Item1 == ChestFurniture).Item2;
            Assert.That(file.Items.Select(Whole), Is.EquivalentTo(new[]
            {
                WholeAt(old[0], desk), WholeAt(old[1], chest), Whole(old[2]),
                (FreeDesk, HouseRoom.SurfaceId, extra.X, extra.Y, FurniturePlacementDirection.North, string.Empty, string.Empty, "<none>"),
            }), "옮긴 것은 새 자리, 모르는 것은 원래 자리, 아무것도 빠지지 않는다");

            // 상위호환: 옛 빌드(옮기기가 없는 Restore, 모든 칸이 바닥인 옛 집)가 같은 파일을 읽으면 가구가 모두 놓인다(새 벽 칸은 옛 집에서 바닥).
            var oldBuild = new HouseFurnitureLayout(new HousePlacementModel(OldBuildHouseGrid()), HouseRoom.SurfaceId);
            oldBuild.Restore(file.Items, TestWorld.Definitions.All<FurnitureDefinition>());
            Assert.That(oldBuild.Placed.Select(p => (p.Definition.StableId, p.Anchor, p.Direction)), Is.EquivalentTo(Placement(game)));
            Assert.That(oldBuild.UnresolvedCount, Is.EqualTo(1), "데이터에 없는 가구는 옛 빌드도 그대로 들고 있다");
            Assert.That(oldBuild.BuildSaveRecords().Select(Whole), Is.EquivalentTo(file.Items.Select(Whole)), "옛 빌드가 다시 써도 기록이 그대로");

            // 옛 빌드가 다녀간 뒤(그 집에서 책상을 옛 벽 자리로 되돌려 저장) 새 빌드로 돌아오면 다시 옮긴다.
            var back = file.Items.Select(i => i.Clone()).ToArray();
            back[0].AnchorX = old[0].AnchorX;
            back[0].AnchorY = old[0].AnchorY;
            WriteLayout(saves, back);
            var again = EnterHouse(saves);
            Assert.That(Placement(again).Single(p => p.Item1 == FreeDesk && p.Item3 == FurniturePlacementDirection.East).Item2, Is.EqualTo(desk), "같은 규칙으로 같은 자리");
        }

        [Test]
        public void NextSession_SamePlacement()
        {
            using var saves = new TempSaves();
            var records = OldLayout().ToList();
            var rug = Rug();
            var free = FreeCellsFor(rug);
            // 한 칸 깔개로 집의 빈 칸을 모두 채우고 하나를 더(그것은 둘 곳이 없어 보관된다).
            for (int i = 0; i <= free; i++) records.Add(Record(rug, InnerWallCells()[2]));
            WriteLayout(saves, records);

            var first = EnterHouse(saves);
            Assert.That(first.StoredFurniture().Count, Is.GreaterThan(0));
            var placed = Placement(first).ToList();
            var stored = first.StoredFurniture().Select(s => (s.Index, s.Definition.StableId, s.Direction)).ToList();

            var second = EnterHouse(saves);
            Assert.That(Placement(second), Is.EqualTo(placed), "읽을 때마다 같은 자리");
            Assert.That(second.StoredFurniture().Select(s => (s.Index, s.Definition.StableId, s.Direction)), Is.EqualTo(stored));

            // 바꾼 뒤 저장 → 새 판에서도 같은 배치(이제는 옮긴 자리가 파일에 있다).
            var desk = first.House.Placed.First(p => p.Definition.StableId == FreeDesk);
            Assert.That(first.MoveFurniture(desk.InstanceId, desk.Anchor, desk.Direction), Is.True);
            var third = EnterHouse(saves);
            Assert.That(Placement(third), Is.EquivalentTo(placed));
            Assert.That(third.StoredFurniture().Select(s => (s.Definition.StableId, s.Direction)), Is.EqualTo(stored.Select(s => (s.Item2, s.Item3))));
            Assert.That(third.House.Placed.Count + third.House.UnresolvedCount, Is.EqualTo(records.Count));
        }

        [Test]
        public void PlaceStored_PlacesTheRecord_AndSecondRequestDoesNothing()
        {
            using var saves = new TempSaves();
            var rug = Rug();
            var wall = InnerWallCells()[2];
            var records = new List<FurniturePlacementSaveData>();
            int free = FreeCellsFor(rug);
            for (int i = 0; i <= free; i++) records.Add(Record(rug, wall));
            records[records.Count - 1].StateJson = "{\"last\":true}";
            WriteLayout(saves, records);

            var game = EnterHouse(saves);
            var stored = game.StoredFurniture().Single();
            Assert.That(game.TryTakeHouseRelocationNotice(out int moved, out int storedCount), Is.True);
            Assert.That((moved, storedCount), Is.EqualTo((free, 1)));
            Assert.That(game.TryTakeHouseRelocationNotice(out _, out _), Is.False);

            // 둘 곳이 없다: 아무것도 바뀌지 않는다.
            var anyCell = game.House.Placed[0].Anchor;
            Assert.That(game.CanPlaceStoredFurniture(stored.Index, anyCell, FurniturePlacementDirection.North), Is.False);
            Assert.That(game.PlaceStoredFurniture(stored.Index, anyCell, FurniturePlacementDirection.North), Is.False);

            // 깔개 하나를 치워 자리를 만든 뒤 보관한 것을 그 자리에.
            var removed = game.House.Placed[0];
            Assert.That(game.RemoveFurniture(removed.InstanceId), Is.True);
            int changes = 0;
            game.Changed += t => { if (t == GameChangeTopics.Furniture) changes++; };
            Assert.That(game.CanPlaceStoredFurniture(stored.Index, removed.Anchor, FurniturePlacementDirection.North), Is.True);
            Assert.That(game.PlaceStoredFurniture(stored.Index, removed.Anchor, FurniturePlacementDirection.North), Is.True);
            Assert.That(changes, Is.GreaterThan(0));
            Assert.That(game.StoredFurniture(), Is.Empty);
            Assert.That(game.House.Placed.Last().Anchor, Is.EqualTo(removed.Anchor));
            byte[] after = File.ReadAllBytes(LayoutPath(saves));
            Assert.That(saves.Store.TryRead<FurniturePlacementLayoutSaveData>(TestWorld.Slot, SaveFileNames.HouseFurnitureLayout(0), out var file), Is.True);
            Assert.That(file.Items.Count(i => i.StateJson == "{\"last\":true}" && i.AnchorX == removed.Anchor.X && i.AnchorY == removed.Anchor.Y), Is.EqualTo(1),
                "보관한 기록이 새 자리로 저장됐다(상태 그대로)");
            Assert.That(file.Items.Length, Is.EqualTo(records.Count - 1), "치운 하나만 빠졌다");

            // 같은 요청 두 번: 두 번째는 아무것도 하지 않는다.
            Assert.That(game.PlaceStoredFurniture(stored.Index, removed.Anchor, FurniturePlacementDirection.North), Is.False);
            Assert.That(File.ReadAllBytes(LayoutPath(saves)), Is.EqualTo(after));

            // 다음 판에서도 놓인 채다.
            var next = EnterHouse(saves);
            Assert.That(next.StoredFurniture(), Is.Empty);
            Assert.That(next.TryTakeHouseRelocationNotice(out _, out _), Is.False, "옮긴 것·보관한 것이 없다");
        }

        [Test]
        public void SharedHouse_GuestAndRoomPlaceTheSame()
        {
            using var saves = new TempSaves();
            var walls = InnerWallCells();
            var rules = new RoomHouseRules(TestWorld.Definitions.All<FurnitureDefinition>(), s => s == HouseRoom.SurfaceId ? HouseRoom.Grid : null);
            var pieces = new RoomPieceSet(new RoomShareSettings(new[] { RoomHouse.PieceName }), RoomPieceCatalog.CreateDefault(rules));
            Assert.That(pieces.TryGet(RoomHouse.PieceName, out var piece), Is.True);
            var house = (RoomHouse)piece;

            // 방의 세이브: 벽이 된 칸의 책상 둘(같은 칸), 데이터에 없는 가구, 제자리 책상.
            var probe = EnterHouse(saves);
            var free = HouseRoom.FirstFreeAnchor(probe, FreeDesk);
            RoomHousePlacementSaveData P(string instance, string furniture, GridPoint at) => new RoomHousePlacementSaveData
            {
                InstanceId = instance, FurnitureId = furniture, SurfaceId = HouseRoom.SurfaceId, AnchorX = at.X, AnchorY = at.Y,
                Direction = FurniturePlacementDirection.North, PlacedBy = "a", Day = 1,
            };
            saves.Store.Write("room", SaveFileNames.RoomPiece(RoomHouse.PieceName), new RoomHouseSaveData
            {
                Version = RoomHouseSaveData.KnownVersion, NextNumber = 5,
                Placed = new[] { P("1", FreeDesk, walls[0]), P("2", FreeDesk, walls[0]), P("3", Unknown, walls[1]), P("4", FreeDesk, free) },
            });
            house.Load(saves.Store, "room");

            var shares = new RoomShares(pieces.Settings, RoomPieceCatalog.CreateDefault(), () => "guest-r1");
            foreach (var p in pieces.Pieces) shares.Receive(p.State());
            var guest = GameSession.Start(TestWorld.Definitions, TestWorld.Settings, saves.Store, "slot-guest", "guest", shares: shares);
            guest.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);

            var records = house.Placed.Select(p => new FurniturePlacementSaveData
            {
                FurnitureId = p.FurnitureId, SurfaceId = p.SurfaceId, AnchorX = p.AnchorX, AnchorY = p.AnchorY, Direction = p.Direction,
            }).ToList();
            var judged = rules.Layout(HouseRoom.SurfaceId, records);
            Assert.That(guest.House.Placed.Select(p => (p.Definition.StableId, p.Anchor, p.Direction)),
                Is.EqualTo(judged.Placed.Select(p => (p.Definition.StableId, p.Anchor, p.Direction))), "방(서버)과 손님이 같은 Core 코드로 같은 자리");
            Assert.That(guest.House.Placed.Count, Is.EqualTo(3), "벽 칸의 책상 둘이 모두 옮겨졌다");
            Assert.That(guest.House.UnresolvedCount, Is.EqualTo(1), "데이터에 없는 가구는 옮기지 않는다");
            Assert.That(guest.House.Placed.Select(p => p.InstanceId), Is.EqualTo(new[] { "1", "2", "4" }), "방이 준 인스턴스 id");
            Assert.That(house.Placed.Select(p => (p.AnchorX, p.AnchorY)), Is.EqualTo(new[] { (walls[0].X, walls[0].Y), (walls[0].X, walls[0].Y), (walls[1].X, walls[1].Y), (free.X, free.Y) }),
                "방의 기록은 바뀌지 않는다(읽을 때마다 같은 규칙으로 옮겨 본다)");

            // 방의 판정도 옮긴 자리를 안다: 그 자리에 다른 가구를 놓는 요청은 거절된다.
            var movedAt = guest.House.Placed[0].Anchor;
            Assert.That(guest.PlaceStoredFurniture(0, movedAt, FurniturePlacementDirection.North), Is.False, "같이 쓰는 집에서는 보관한 것을 꺼내지 않는다(다음 계획)");
            var answers = new List<RoomAnswer>();
            pieces.Apply("b", new[] { new RoomRequestSaveData
            {
                Piece = RoomHouse.PieceName, RequestId = "b-1", Kind = RoomHouse.Place,
                Ids = new[] { HouseRoom.SurfaceId, FreeDesk, movedAt.X.ToString(), movedAt.Y.ToString(), "0" },
            } }, 1, answers, new List<IRoomPiece>());
            Assert.That(answers.Single().Outcome, Is.EqualTo(RoomOutcomes.Refused));
            Assert.That(guest.House.CanPlace(TestWorld.Definitions.Get<FurnitureDefinition>(FreeDesk), movedAt, FurniturePlacementDirection.North), Is.False, "손님 쪽 판정도 같다");
        }

        [Test]
        public void Workspace_DoesNotMoveItsRecords()
        {
            using var saves = new TempSaves();
            var chef = TestWorld.Definitions.Get<Rootborn.Core.Workspaces.Workspace>("workspace.chef");
            var grid = WorkspaceRoom.GridFor(TestWorld.Definitions, chef.Id);
            var outside = new GridPoint(-500, -500);
            // 기본 템플릿에 든 한 칸 가구(일터가 받는다). 일터 칸 밖에 있어 놓이지 않는 기록.
            var record = new FurniturePlacementSaveData { FurnitureId = "school.potted.plant", SurfaceId = chef.SurfaceId, AnchorX = outside.X, AnchorY = outside.Y };
            Assert.That(FurnitureReplay.PlanRelocating(new HousePlacementModel(grid), chef.SurfaceId, new[] { record }, TestWorld.Definitions.All<FurnitureDefinition>(),
                chef.AcceptsSurfaceIds).Accepted.Single().Moved, Is.True, "대조: 옮기기를 쓰면 옮겨지는 기록이다");
            saves.Store.Write(TestWorld.Slot, SaveFileNames.WorkspaceLayout(chef.SurfaceId, TestWorld.Player),
                new FurniturePlacementLayoutSaveData { Version = FurniturePlacementLayoutSaveData.KnownVersion, Items = new[] { record } });

            var game = TestWorld.Start(saves.Store);
            game.EnterPlacementSurface(grid, chef.SurfaceId);
            Assert.That(game.ActiveWorkspace, Is.Not.Null);
            Assert.That(game.House.Placed, Is.Empty, "일터는 옮기지 않는다(맵이 바뀌지 않았다 — Restore 그대로)");
            Assert.That(game.House.UnresolvedCount, Is.EqualTo(1));
            Assert.That(game.StoredFurniture(), Is.Empty, "보관 목록은 집에만");
            Assert.That(game.TryTakeHouseRelocationNotice(out _, out _), Is.False);
            Assert.That(game.PlaceStoredFurniture(0, grid.Spawn, FurniturePlacementDirection.North), Is.False);
        }

        [Test]
        public void TwoHundredOldRecords_AreRestoredQuickly()
        {
            using var saves = new TempSaves();
            var walls = InnerWallCells();
            var chest = ChestFurniture;
            var records = new List<FurniturePlacementSaveData>(200);
            for (int i = 0; i < 200; i++) records.Add(Record(i % 2 == 0 ? FreeDesk : chest, walls[i % walls.Count]));
            WriteLayout(saves, records);

            var game = TestWorld.Start(saves.Store);
            var watch = Stopwatch.StartNew();
            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            watch.Stop();
            TestContext.Out.WriteLine($"가구 기록 200개(모두 벽 칸) 집에 들어가 옮기기: {watch.ElapsedMilliseconds} ms, 놓임 {game.House.Placed.Count}, 보관 {game.StoredFurniture().Count}");
            Assert.That(game.House.Placed.Count + game.House.UnresolvedCount, Is.EqualTo(200));
            Assert.That(watch.ElapsedMilliseconds, Is.LessThan(5000), "읽을 때 한 번 하는 일이다 — 넉넉한 상한");
        }

        // 해금이 필요 없고 막지 않는 한 칸짜리 가구(그림도 한 칸).
        private static string Rug()
        {
            using var saves = new TempSaves();
            var probe = TestWorld.Start(saves.Store);
            return probe.AvailableFurniture().First(f => !f.BlocksMovement && f.AllowsSurface(HouseRoom.SurfaceId)
                && f.GetFootprintCells(FurniturePlacementDirection.North).Count == 1 && f.GetTileParts(FurniturePlacementDirection.North).Count == 1).StableId;
        }

        // 빈 새 집에 그 가구를 놓을 수 있는 칸 수.
        private static int FreeCellsFor(string furnitureId)
        {
            var definition = TestWorld.Definitions.Get<FurnitureDefinition>(furnitureId);
            var empty = new HouseFurnitureLayout(new HousePlacementModel(HouseRoom.Grid), HouseRoom.SurfaceId);
            return HouseRoom.Grid.FloorCells.Count(c => empty.CanPlace(definition, c, FurniturePlacementDirection.North));
        }
    }
}
