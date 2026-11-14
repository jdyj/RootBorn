using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Building;
using Rootborn.Core.Placement;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;
using Rootborn.Core.World;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 짓는 집 꾸밈 세이브(house-build.json)의 하위호환·상위호환(스킬 save-compat-change).
    /// 하위: 그 파일이 없는 옛 슬롯이 열리고 기본 꾸밈으로 보이며 읽기만으로 아무것도 쓰지 않는다. 지금 데이터에 없는 id, 잠긴 꾸밈, 벽·격자 밖 칸, 다른 틀의
    /// 기록을 버리지 않는다. 상위: 이 파일을 모르는 흐름(옛 빌드처럼 EnterHouseBuild 없이 플레이·저장)은 파일을 바이트 그대로 둔다.
    /// 실제 data/와 실제 집 맵, 임시 폴더 저장소와 고정 파일 사본. GameSession 명령으로 진행하는 자동 테스트다(실제 옛 빌드 실행 파일로 연 것이 아니다).
    /// </summary>
    public sealed class HouseBuildCompatTests
    {
        private const string Allowance = "item.allowance";
        private const string OldFixture = "monogame-20261112/slot-robust";
        private const string Unknown = "finish.gone-from-data";
        private static HouseBuildFrame Frame => HouseRoom.Frame;
        private static CellPos TopLeftRoom => HouseRoom.TopLeftRoom;

        private static RoomFinishDefinition FreeNonDefault(string slot, int skip = 0)
        {
            string fallback = slot == RoomFinishSlots.Wall ? Frame.DefaultWall : Frame.DefaultFloor;
            return TestWorld.Definitions.All<RoomFinishDefinition>().Where(f => f.Slot == slot && f.UnlockToken.Length == 0 && f.Id != fallback).Skip(skip).First();
        }

        private static RoomFinishDefinition Locked(string slot)
            => TestWorld.Definitions.All<RoomFinishDefinition>().First(f => f.Slot == slot && f.UnlockToken.Length > 0);

        private static void GiveMoney(GameSession game, int amount) => game.Inventory.Add(game.Item(Allowance), amount);

        private static GameSession InHouse(ISaveStore store, string slot = TestWorld.Slot)
        {
            var game = TestWorld.Start(store, slot);
            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            game.EnterHouseBuild(Frame);
            return game;
        }

        private static string BuildPath(TempSaves saves, string slot = TestWorld.Slot) => Path.Combine(saves.Path, slot, SaveFileNames.HouseBuild);

        private static void WriteBuild(TempSaves saves, string frameId, params HouseFinishCellSaveData[] cells)
            => saves.Store.Write(TestWorld.Slot, SaveFileNames.HouseBuild, new HouseBuildSaveData
            {
                Version = HouseBuildSaveData.KnownVersion, FrameId = frameId, Band = "", Finishes = cells,
            });

        private static HouseBuildSaveData ReadBuild(TempSaves saves, string slot = TestWorld.Slot)
        {
            Assert.That(saves.Store.TryRead<HouseBuildSaveData>(slot, SaveFileNames.HouseBuild, out var data), Is.True, "house-build.json이 있다");
            return data;
        }

        private static HouseFinishCellSaveData RecordAt(HouseBuildSaveData data, int x, int y)
            => data.Finishes.SingleOrDefault(f => f.X == x && f.Y == y);

        // 기록과 상관없는 다른 칸 하나를 칠해 기록 전체가 다시 쓰이게 한다(읽은 기록이 그대로 다시 쓰이는지 보려고).
        private static void ForceRewrite(GameSession game, CellPos cell)
        {
            var floor = FreeNonDefault(RoomFinishSlots.Floor, 1);
            GiveMoney(game, floor.Cost.Where(c => c.Item == Allowance).Sum(c => c.Count));
            Assert.That(game.ApplyHouseFinish(floor.Id, HouseFinishTools.Brush, cell), Is.True, "준비: 다른 칸 하나를 칠해 파일을 다시 쓴다");
        }

        // 왼쪽 위 방 밖의 바닥 칸 하나(방 채우기에 들지 않는 칸).
        private static CellPos FloorOutsideTopLeftRoom(GameSession game)
        {
            var fill = game.PreviewHouseFinish(FreeNonDefault(RoomFinishSlots.Floor).Id, HouseFinishTools.Room, TopLeftRoom).Cells;
            var grid = Frame.Tiles.Grid;
            for (int y = 0; y < grid.Rows; y++)
                for (int x = 0; x < grid.Columns; x++)
                    if (grid.At(x, y) == RoomCells.Floor && !fill.Contains(new CellPos(x, y))) return new CellPos(x, y);
            throw new InvalidOperationException("왼쪽 위 방 밖의 바닥 칸이 없다");
        }

        private static CellPos FirstWallCell()
        {
            var grid = Frame.Tiles.Grid;
            for (int y = 0; y < grid.Rows; y++)
                for (int x = 0; x < grid.Columns; x++)
                    if (grid.At(x, y) == RoomCells.Wall) return new CellPos(x, y);
            throw new InvalidOperationException("집에 벽 칸이 없다");
        }

        // ---------- 하위호환 ----------

        [Test]
        public void OldSlotWithoutFile_Opens_DefaultFinishes_ReadingWritesNothing()
        {
            using var saves = new TempSaves();
            saves.CopyFixture(OldFixture, SlotCheckpoints.Slot);
            var before = SlotCheckpoints.Read(saves);
            Assert.That(before.Keys, Does.Not.Contain(SaveFileNames.HouseBuild), "전제: 11-12 고정 슬롯에는 꾸밈 파일이 없다");

            var game = TestWorld.Start(saves.Store, SlotCheckpoints.Slot);
            Assert.That(SlotCheckpoints.Same(before, SlotCheckpoints.Read(saves)), Is.True, "읽기만으로 아무것도 쓰지 않는다");
            // 가구 방에 들어가는 것(EnterHouse)은 이 작업 전부터 일터 진행 파일을 쓴다(11-12 슬롯에는 없다) — 꾸밈과 상관없는 기존 동작이라 따로 본다.
            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            var afterHouse = SlotCheckpoints.Read(saves);
            game.EnterHouseBuild(Frame);

            Assert.That(game.HouseBuildOpen, Is.True, "옛 슬롯도 꾸밀 수 있는 집이 열린다");
            Assert.That(game.HouseFloorAt(TopLeftRoom), Is.EqualTo(Frame.DefaultFloor));
            Assert.That(game.HouseWallAt(TopLeftRoom), Is.EqualTo(Frame.DefaultWall));
            Assert.That(game.HouseBandColors(), Is.EqualTo(Frame.DefaultBand));
            Assert.That(game.House.Placed, Is.Not.Empty, "옛 슬롯의 가구가 그대로 있다");
            Assert.That(SlotCheckpoints.Same(afterHouse, SlotCheckpoints.Read(saves)), Is.True, "틀을 붙이고 꾸밈을 묻는 것만으로 아무것도 쓰지 않는다");
            Assert.That(afterHouse.Keys, Does.Not.Contain(SaveFileNames.HouseBuild), "꾸밈 파일을 새로 만들지 않는다");
            Assert.That(afterHouse.Keys, Is.SupersetOf(before.Keys), "옛 파일이 모두 남는다");
            foreach (var pair in before)
                Assert.That(afterHouse[pair.Key], Is.EqualTo(pair.Value), pair.Key + ": 집에 들어가도 옛 파일은 바이트 그대로");
        }

        [Test]
        public void OldSlotWithoutFile_FinishedThenReopened_KeepsFinishAndOldProgress()
        {
            using var saves = new TempSaves();
            saves.CopyFixture(OldFixture, SlotCheckpoints.Slot);
            var game = TestWorld.Start(saves.Store, SlotCheckpoints.Slot);
            int day = game.Student.CurrentDay;
            int placed;
            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            game.EnterHouseBuild(Frame);
            placed = game.House.Placed.Count;
            var floor = FreeNonDefault(RoomFinishSlots.Floor);
            GiveMoney(game, 100_000);
            Assert.That(game.ApplyHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom), Is.True);

            var next = TestWorld.Start(saves.Store, SlotCheckpoints.Slot);
            next.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            next.EnterHouseBuild(Frame);
            Assert.That(next.HouseFloorAt(TopLeftRoom), Is.EqualTo(floor.Id), "꾸밈이 다음 판에도 있다");
            Assert.That(next.Student.CurrentDay, Is.EqualTo(day), "옛 진행이 남는다");
            Assert.That(next.House.Placed.Count, Is.EqualTo(placed), "옛 가구가 남는다");
        }

        [Test]
        public void UnknownFinishId_KeptAndDrawnAsDefault()
        {
            using var saves = new TempSaves();
            WriteBuild(saves, Frame.FrameId, new HouseFinishCellSaveData { X = TopLeftRoom.X, Y = TopLeftRoom.Y, Wall = Unknown, Floor = Unknown });
            var game = InHouse(saves.Store);

            Assert.That(game.HouseFloorAt(TopLeftRoom), Is.EqualTo(Frame.DefaultFloor), "데이터에 없는 바닥은 기본으로 보인다");
            Assert.That(game.HouseWallAt(TopLeftRoom), Is.EqualTo(Frame.DefaultWall), "데이터에 없는 벽지는 기본으로 보인다");

            ForceRewrite(game, FloorOutsideTopLeftRoom(game));
            var next = InHouse(saves.Store);
            var kept = RecordAt(ReadBuild(saves), TopLeftRoom.X, TopLeftRoom.Y);
            Assert.That(kept, Is.Not.Null, "저장 → 새 판: 모르는 id의 기록이 남는다");
            Assert.That(kept.Floor, Is.EqualTo(Unknown));
            Assert.That(kept.Wall, Is.EqualTo(Unknown));
            Assert.That(next.HouseFloorAt(TopLeftRoom), Is.EqualTo(Frame.DefaultFloor));
        }

        [Test]
        public void LockedFinishAlreadyUsed_StaysVisible_ButCannotBeChosenAgain()
        {
            using var saves = new TempSaves();
            var locked = Locked(RoomFinishSlots.Floor);
            WriteBuild(saves, Frame.FrameId, new HouseFinishCellSaveData { X = TopLeftRoom.X, Y = TopLeftRoom.Y, Floor = locked.Id });
            var game = InHouse(saves.Store);
            GiveMoney(game, 100_000);
            var elsewhere = FloorOutsideTopLeftRoom(game);

            Assert.That(game.IsFinishUnlocked(locked), Is.False, "전제: 이 판은 해금하지 않았다");
            Assert.That(game.HouseFloorAt(TopLeftRoom), Is.EqualTo(locked.Id), "이미 쓴 잠긴 꾸밈은 보인다(빼앗지 않는다)");
            Assert.That(game.PreviewHouseFinish(locked.Id, HouseFinishTools.Brush, elsewhere).ReasonKey, Is.EqualTo(HouseFinishReasons.Locked), "다시 고를 수는 없다");
            Assert.That(game.ApplyHouseFinish(locked.Id, HouseFinishTools.Brush, elsewhere), Is.False);

            ForceRewrite(game, elsewhere);
            var next = InHouse(saves.Store);
            Assert.That(next.HouseFloorAt(TopLeftRoom), Is.EqualTo(locked.Id), "저장 → 새 판에서도 보인다");
        }

        [Test]
        public void RecordOnWallOrOutside_KeptNotApplied()
        {
            using var saves = new TempSaves();
            var wallCell = FirstWallCell();
            var floor = FreeNonDefault(RoomFinishSlots.Floor);
            var outside = new CellPos(Frame.Tiles.Grid.Columns + 5, Frame.Tiles.Grid.Rows + 5);
            WriteBuild(saves, Frame.FrameId,
                new HouseFinishCellSaveData { X = wallCell.X, Y = wallCell.Y, Floor = floor.Id },
                new HouseFinishCellSaveData { X = outside.X, Y = outside.Y, Floor = floor.Id, Wall = FreeNonDefault(RoomFinishSlots.Wall).Id });
            var game = InHouse(saves.Store);

            // 벽 칸·격자 밖 칸의 기록은 어느 방에도 들지 않는다: 방 채우기는 그 방 바닥 칸만 덮고, 그 방 칸들은 모두 기본으로 보인다.
            var fill = game.PreviewHouseFinish(floor.Id, HouseFinishTools.Room, TopLeftRoom);
            Assert.That(fill.Cells, Does.Not.Contain(wallCell).And.Not.Contain(outside));
            Assert.That(fill.ChangedCells.Count, Is.EqualTo(fill.Cells.Count), "그 방 칸은 모두 아직 기본이다(벽 칸 기록이 섞이지 않았다)");
            Assert.That(fill.Cells.All(c => game.HouseFloorAt(c) == Frame.DefaultFloor), Is.True);

            ForceRewrite(game, FloorOutsideTopLeftRoom(game));
            var data = ReadBuild(saves);
            Assert.That(RecordAt(data, wallCell.X, wallCell.Y)?.Floor, Is.EqualTo(floor.Id), "벽 칸 기록이 그대로 다시 쓰인다");
            Assert.That(RecordAt(data, outside.X, outside.Y)?.Floor, Is.EqualTo(floor.Id), "격자 밖 기록이 그대로 다시 쓰인다");
            Assert.That(RecordAt(data, outside.X, outside.Y)?.Wall, Is.EqualTo(FreeNonDefault(RoomFinishSlots.Wall).Id));
        }

        [Test]
        public void OtherFrameId_SameCellsApply_RecordsKept()
        {
            using var saves = new TempSaves();
            var floor = FreeNonDefault(RoomFinishSlots.Floor);
            WriteBuild(saves, "frame.an-older-house", new HouseFinishCellSaveData { X = TopLeftRoom.X, Y = TopLeftRoom.Y, Floor = floor.Id });
            Assert.That(Frame.FrameId, Is.Not.EqualTo("frame.an-older-house"), "전제: 다른 틀 id");
            var game = InHouse(saves.Store);

            Assert.That(game.HouseFloorAt(TopLeftRoom), Is.EqualTo(floor.Id), "틀 id가 달라도 같은 칸 좌표의 기록이 든다");

            ForceRewrite(game, FloorOutsideTopLeftRoom(game));
            var data = ReadBuild(saves);
            Assert.That(RecordAt(data, TopLeftRoom.X, TopLeftRoom.Y)?.Floor, Is.EqualTo(floor.Id), "기록이 남는다");
            Assert.That(data.FrameId, Is.EqualTo(Frame.FrameId), "다시 쓴 파일은 지금 틀 id");
            Assert.That(InHouse(saves.Store).HouseFloorAt(TopLeftRoom), Is.EqualTo(floor.Id));
        }

        [Test]
        public void NewPlayerInSlotWithOnlyHouseBuild_IsNewGameNotDamaged()
        {
            using var saves = new TempSaves();
            var floor = FreeNonDefault(RoomFinishSlots.Floor);
            WriteBuild(saves, Frame.FrameId, new HouseFinishCellSaveData { X = TopLeftRoom.X, Y = TopLeftRoom.Y, Floor = floor.Id });
            byte[] written = File.ReadAllBytes(BuildPath(saves));
            using var fresh = new TempSaves();
            var plain = TestWorld.Start(fresh.Store);

            GameSession game = null;
            Assert.DoesNotThrow(() => game = TestWorld.Start(saves.Store), "꾸밈 파일(슬롯 공용)만 있는 슬롯은 손상이 아니다");
            Assert.That(game.Student.CurrentDay, Is.EqualTo(plain.Student.CurrentDay), "새 게임으로 시작한다");
            Assert.That(game.Wallet.Balance, Is.EqualTo(plain.Wallet.Balance));
            game.VisitLocation("location.town-square"); // 새 게임의 첫 저장
            Assert.That(File.ReadAllBytes(BuildPath(saves)), Is.EqualTo(written), "새 게임의 저장이 꾸밈 파일을 덮지 않는다");

            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            game.EnterHouseBuild(Frame);
            Assert.That(game.HouseFloorAt(TopLeftRoom), Is.EqualTo(floor.Id), "슬롯의 집 꾸밈을 본다");
        }

        [Test]
        public void Fixture20261114_OpensWithItsFinish_AndReadingWritesNothing()
        {
            // 11-14 고정 슬롯(꾸밈 하나를 바꾼 house-finished 지점)을 뒤의 프로그램이 읽는다: 꾸밈이 보이고 읽기만으로 쓰지 않는다.
            using var saves = new TempSaves();
            saves.CopyFixture("monogame-20261114/" + SlotCheckpoints.Slot, SlotCheckpoints.Slot);
            var before = SlotCheckpoints.Read(saves);
            Assert.That(before.Keys, Does.Contain(SaveFileNames.HouseBuild), "전제: 11-14 고정 슬롯에는 꾸밈 파일이 있다");
            Assert.That(saves.Store.TryRead<HouseBuildSaveData>(SlotCheckpoints.Slot, SaveFileNames.HouseBuild, out var data), Is.True);
            var recorded = data.Finishes.First(f => f.Floor.Length > 0);

            var game = TestWorld.Start(saves.Store, SlotCheckpoints.Slot);
            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            game.EnterHouseBuild(Frame);
            Assert.That(game.HouseFloorAt(new CellPos(recorded.X, recorded.Y)), Is.EqualTo(recorded.Floor));
            Assert.That(SlotCheckpoints.Same(before, SlotCheckpoints.Read(saves)), Is.True);
        }

        // ---------- 상위호환 ----------

        [Test]
        public void OlderBuildVisit_FileUntouched_FurnitureStillPlays()
        {
            // 옛 빌드는 house-build.json을 모른다. 그 흐름(EnterHouseBuild 없이 집에 들어가 가구를 다루고 하루를 넘기고 저장)을 지나도 파일은 바이트 그대로이고,
            // 새 프로그램으로 돌아오면 꾸밈이 그대로다. 실제 옛 빌드 실행 파일로 연 것은 아니다.
            using var saves = SlotCheckpoints.Restore("house-finished");
            string path = Path.Combine(saves.Path, SlotCheckpoints.Slot, SaveFileNames.HouseBuild);
            byte[] written = File.ReadAllBytes(path);
            Assert.That(saves.Store.TryRead<HouseBuildSaveData>(SlotCheckpoints.Slot, SaveFileNames.HouseBuild, out var data), Is.True);
            var recorded = data.Finishes.First(f => f.Floor.Length > 0);

            var old = TestWorld.Start(saves.Store, SlotCheckpoints.Slot);
            old.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            int placed = old.House.Placed.Count;
            Assert.That(placed, Is.GreaterThan(0), "전제: 지점에 가구가 있다");
            Assert.That(old.PlaceFurniture(SlotCheckpoints.FreeDesk, HouseRoom.FirstFreeAnchor(old, SlotCheckpoints.FreeDesk), FurniturePlacementDirection.North), Is.True, "가구는 그대로 다룬다");
            int day = old.Student.CurrentDay;
            if (old.Student.DayState == StudentDayState.InProgress) old.VisitLocation("location.town-square");
            if (old.Student.DayState == StudentDayState.InProgress) Assert.That(old.EndDay().Ended, Is.True);
            Assert.That(old.StartNextDay(), Is.True);
            Assert.That(old.Student.CurrentDay, Is.EqualTo(day + 1));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(written), "꾸밈 파일을 모르는 흐름은 그 파일을 쓰지 않는다");

            var back = TestWorld.Start(saves.Store, SlotCheckpoints.Slot);
            back.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            back.EnterHouseBuild(Frame);
            Assert.That(back.HouseFloorAt(new CellPos(recorded.X, recorded.Y)), Is.EqualTo(recorded.Floor), "돌아오면 꾸밈이 그대로");
            Assert.That(back.House.Placed.Count, Is.EqualTo(placed + 1), "옛 흐름에서 놓은 가구도 남는다");
            Assert.That(back.Student.CurrentDay, Is.EqualTo(day + 1));
        }
    }
}
