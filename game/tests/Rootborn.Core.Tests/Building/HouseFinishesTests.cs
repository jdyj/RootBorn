using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Building;
using Rootborn.Core.Inventory;
using Rootborn.Core.Placement;
using Rootborn.Core.World;

namespace Rootborn.Core.Tests.Building
{
    /// <summary>꾸밈 상태: 가리킨 칸 → 대상, 덮는 칸, 바뀌는 칸만 세는 비용, 판정 순서, 적용, 기록 보존. 자동 테스트.</summary>
    public sealed class HouseFinishesTests
    {
        // 두 방(3x3) 사이에 폭 1 문간 (4,3). 위 두 줄 벽은 정면(faceHeight 2).
        private static readonly string[] Plan =
        {
            "#########",
            "#########",
            "#...#...#",
            "#.......#",
            "#...#...#",
            "#########",
        };

        private const string FrameId = "frame.test";
        private const string WallPlain = "wall.plain", WallBlue = "wall.blue";
        private const string FloorPlain = "floor.plain", FloorOak = "floor.oak", FloorLocked = "floor.locked", Roof = "roof.odd";
        private const string Coin = "coin", Wood = "wood", Token = "unlock.floor";

        private static readonly CellPos LeftMiddle = new CellPos(2, 3);
        private static readonly CellPos Doorway = new CellPos(4, 3);

        private static readonly Dictionary<string, RoomFinishDefinition> Defs = new[]
        {
            Def(WallPlain, RoomFinishSlots.Wall, null, (Coin, 1)),
            Def(WallBlue, RoomFinishSlots.Wall, null, (Coin, 2), (Wood, 1)),
            Def(FloorPlain, RoomFinishSlots.Floor, null, (Coin, 1)),
            Def(FloorOak, RoomFinishSlots.Floor, null, (Coin, 3)),
            Def(FloorLocked, RoomFinishSlots.Floor, Token, (Coin, 1)),
            Def(Roof, "roof", null, (Coin, 1)),
        }.ToDictionary(d => d.Id);

        private static RoomFinishDefinition Def(string id, string slot, string token, params (string Item, int Count)[] cost) =>
            new RoomFinishDefinition(id, slot, "name." + id, "sheet.png", "art." + id,
                cost.Select(c => new ItemAmount<string>(c.Item, c.Count)).ToArray(), token);

        private static RoomFinishDefinition Find(string id) => id != null && Defs.TryGetValue(id, out var d) ? d : null;

        private static RoomTilePlan TilePlan() => RoomTilePlan.Build(RoomFillTests.Grid(Plan), 2, 16, null);

        private static HouseBuildFrame Frame(string id = FrameId) => new HouseBuildFrame(id, TilePlan(), WallPlain, FloorPlain, "band.default");

        private static HouseFinishes Attached(HouseBuildSaveData data = null)
        {
            var f = new HouseFinishes();
            if (data != null) f.Load(data);
            f.Attach(Frame(), Find);
            return f;
        }

        private static readonly FurnitureUnlockProgress NoUnlocks = new FurnitureUnlockProgress("slot", "player");
        private static bool Pay(IReadOnlyList<ItemAmount<string>> _) => true;
        private static bool CannotPay(IReadOnlyList<ItemAmount<string>> _) => false;

        private static HouseFinishPreview Preview(HouseFinishes f, string finish, string tool, CellPos at,
            FurnitureUnlockProgress unlocks = null, Func<IReadOnlyList<ItemAmount<string>>, bool> canPay = null) =>
            f.Preview(finish, tool, at, unlocks ?? NoUnlocks, canPay ?? Pay);

        private static (string, int)[] CostOf(HouseFinishPreview p) => p.Cost.Select(c => (c.Item, c.Count)).ToArray();

        /// <summary>출처표의 face·narrow 부분 주인 칸 전부(테스트가 따로 센다).</summary>
        private static HashSet<CellPos> FaceOwners(RoomTilePlan plan)
        {
            var set = new HashSet<CellPos>();
            foreach (var cell in plan.Cells)
                foreach (var part in cell.Parts)
                    if ((part.Source == TileSources.Face || part.Source == TileSources.Narrow) && part.HasOwner)
                        set.Add(new CellPos(cell.Cell.X + part.Owner.X, cell.Cell.Y + part.Owner.Y));
            return set;
        }

        [Test]
        public void Floor_RoomFill_ChangesOnlyDifferentCells_CostPerCell()
        {
            var f = Attached();
            Assert.That(f.Apply(Preview(f, FloorOak, HouseFinishTools.Brush, new CellPos(1, 2))), Is.True);

            var p = Preview(f, FloorOak, HouseFinishTools.Room, LeftMiddle);
            Assert.That(p.CanApply, Is.True);
            Assert.That(p.Slot, Is.EqualTo(RoomFinishSlots.Floor));
            Assert.That(p.HasTarget && p.Target.Equals(LeftMiddle), Is.True);
            Assert.That(p.Cells.Count, Is.EqualTo(10));                         // 방 9 + 문간 1
            Assert.That(p.ChangedCells.Count, Is.EqualTo(9));                   // 이미 떡갈나무인 칸은 빠진다
            Assert.That(p.ChangedCells, Does.Not.Contain(new CellPos(1, 2)));
            Assert.That(CostOf(p), Is.EqualTo(new[] { (Coin, 27) }));

            Assert.That(f.Apply(p), Is.True);
            foreach (var c in p.Cells)
                Assert.That(f.FloorAt(c), Is.EqualTo(FloorOak), c.ToString());
            Assert.That(f.FloorAt(new CellPos(6, 3)), Is.EqualTo(FloorPlain));  // 다른 방은 그대로
        }

        [Test]
        public void Wallpaper_CountsOnlyFaceOwnerCells()
        {
            var f = Attached();
            var owners = FaceOwners(f.Frame.Tiles);
            Assert.That(owners, Is.EquivalentTo(new[]
            {
                new CellPos(1, 2), new CellPos(2, 2), new CellPos(3, 2), new CellPos(5, 2), new CellPos(6, 2), new CellPos(7, 2), Doorway,
            }));

            var p = Preview(f, WallBlue, HouseFinishTools.Room, LeftMiddle);
            Assert.That(p.CanApply, Is.True);
            Assert.That(p.Cells.Count, Is.EqualTo(10));
            Assert.That(p.ChangedCells, Is.EquivalentTo(new[] { new CellPos(1, 2), new CellPos(2, 2), new CellPos(3, 2), Doorway }));
            Assert.That(CostOf(p), Is.EqualTo(new[] { (Coin, 8), (Wood, 4) }));   // 정의 순서, 칸당 × 4

            // 정면이 없는 바닥 칸을 붓으로 칠하면 바뀌는 칸이 없다
            var none = Preview(f, WallBlue, HouseFinishTools.Brush, new CellPos(2, 4));
            Assert.That(none.ReasonKey, Is.EqualTo(HouseFinishReasons.NothingToChange));
            Assert.That(none.Cells, Is.EqualTo(new[] { new CellPos(2, 4) }));
            Assert.That(none.ChangedCells, Is.Empty);
            Assert.That(none.Cost, Is.Empty);
        }

        [Test]
        public void PointingAtWallFace_TargetsItsOwnerFloor()
        {
            var f = Attached();
            var p1 = Preview(f, WallBlue, HouseFinishTools.Brush, new CellPos(2, 1));   // 바닥 바로 위 정면
            var p2 = Preview(f, WallBlue, HouseFinishTools.Brush, new CellPos(2, 0));   // 그 위 정면
            Assert.That(p1.HasTarget && p1.Target.Equals(new CellPos(2, 2)), Is.True);
            Assert.That(p2.HasTarget && p2.Target.Equals(new CellPos(2, 2)), Is.True);
            Assert.That(p1.Cells, Is.EqualTo(new[] { new CellPos(2, 2) }));

            var room = Preview(f, FloorOak, HouseFinishTools.Room, new CellPos(6, 1));
            Assert.That(room.Target, Is.EqualTo(new CellPos(6, 2)));
            Assert.That(room.Cells, Is.SameAs(Preview(f, FloorOak, HouseFinishTools.Room, new CellPos(6, 4)).Cells));

            var door = Preview(f, WallBlue, HouseFinishTools.Brush, new CellPos(4, 2));  // 문간 위 좁은 정면
            Assert.That(door.Target, Is.EqualTo(Doorway));

            // 윗면 칸이라도 출처표에 옆 정면이 늘어 들어와 있으면 첫 face 부분(왼쪽, 주인 (3,2))이 대상이다
            var cap = Preview(f, WallBlue, HouseFinishTools.Brush, new CellPos(4, 0));
            Assert.That(f.Frame.Tiles.At(4, 0).Kind, Is.EqualTo("cap"));
            Assert.That(cap.Target, Is.EqualTo(new CellPos(3, 2)));
        }

        [Test]
        public void PointingAtBand_NoTarget()
        {
            var f = Attached();
            foreach (var at in new[] { new CellPos(0, 3), new CellPos(4, 4), new CellPos(-1, 0), new CellPos(40, 40) })
            {
                var p = Preview(f, WallBlue, HouseFinishTools.Room, at);
                Assert.That(p.ReasonKey, Is.EqualTo(HouseFinishReasons.NoTarget), at.ToString());
                Assert.That(p.HasTarget, Is.False);
                Assert.That(p.Cells, Is.Empty);
                Assert.That(p.CanApply, Is.False);
            }
        }

        [Test]
        public void Locked_Unknown_NotAttached_Reasons_InOrder()
        {
            var loose = new HouseFinishes();
            Assert.That(loose.Attached, Is.False);
            Assert.That(loose.Preview(FloorOak, HouseFinishTools.Room, LeftMiddle, NoUnlocks, Pay).ReasonKey, Is.EqualTo(HouseFinishReasons.NotInHouse));

            var f = Attached();
            Assert.That(f.Attached, Is.True);
            // 모르는 꾸밈은 대상이 없어도 UnknownFinish가 먼저
            Assert.That(Preview(f, "no.such", HouseFinishTools.Room, new CellPos(0, 0)).ReasonKey, Is.EqualTo(HouseFinishReasons.UnknownFinish));
            Assert.That(Preview(f, Roof, HouseFinishTools.Room, LeftMiddle).ReasonKey, Is.EqualTo(HouseFinishReasons.UnknownFinish));
            Assert.That(Preview(f, null, HouseFinishTools.Room, LeftMiddle).ReasonKey, Is.EqualTo(HouseFinishReasons.UnknownFinish));
            // 잠겨도 대상이 없으면 NoTarget이 먼저, 모르는 도구도 NoTarget
            Assert.That(Preview(f, FloorLocked, HouseFinishTools.Room, new CellPos(0, 0)).ReasonKey, Is.EqualTo(HouseFinishReasons.NoTarget));
            Assert.That(Preview(f, FloorOak, "spray", LeftMiddle).ReasonKey, Is.EqualTo(HouseFinishReasons.NoTarget));
            // 잠김은 바뀌는 칸·돈보다 먼저, 이유가 있어도 칸과 비용을 채운다
            var locked = Preview(f, FloorLocked, HouseFinishTools.Room, LeftMiddle, canPay: CannotPay);
            Assert.That(locked.ReasonKey, Is.EqualTo(HouseFinishReasons.Locked));
            Assert.That(locked.ChangedCells.Count, Is.EqualTo(10));
            Assert.That(CostOf(locked), Is.EqualTo(new[] { (Coin, 10) }));
            Assert.That(f.Apply(locked), Is.False);
            // 기본과 같은 것: 바뀌는 칸 0이 돈보다 먼저
            Assert.That(Preview(f, FloorPlain, HouseFinishTools.Room, LeftMiddle, canPay: CannotPay).ReasonKey, Is.EqualTo(HouseFinishReasons.NothingToChange));

            var unlocks = new FurnitureUnlockProgress("slot", "player");
            unlocks.TryUnlock(Token);
            Assert.That(Preview(f, FloorLocked, HouseFinishTools.Room, LeftMiddle, unlocks).CanApply, Is.True);
        }

        [Test]
        public void NotEnoughMoney_StillListsCellsAndCost()
        {
            var f = Attached();
            IReadOnlyList<ItemAmount<string>> asked = null;
            var p = Preview(f, FloorOak, HouseFinishTools.Room, LeftMiddle, canPay: cost => { asked = cost; return false; });
            Assert.That(p.ReasonKey, Is.EqualTo(HouseFinishReasons.NotEnoughMoney));
            Assert.That(p.CanApply, Is.False);
            Assert.That(p.Cells.Count, Is.EqualTo(10));
            Assert.That(p.ChangedCells.Count, Is.EqualTo(10));
            Assert.That(CostOf(p), Is.EqualTo(new[] { (Coin, 30) }));
            Assert.That(asked, Is.SameAs(p.Cost));
            int before = f.Version;
            Assert.That(f.Apply(p), Is.False);
            Assert.That(f.Version, Is.EqualTo(before));
            Assert.That(f.FloorAt(LeftMiddle), Is.EqualTo(FloorPlain));
        }

        [Test]
        public void Apply_SecondTimeNothingToChange()
        {
            var f = Attached();
            var p = Preview(f, FloorOak, HouseFinishTools.Room, LeftMiddle);
            int before = f.Version;
            Assert.That(f.Apply(p), Is.True);
            Assert.That(f.Version, Is.EqualTo(before + 1));
            var again = Preview(f, FloorOak, HouseFinishTools.Room, LeftMiddle);
            Assert.That(again.ReasonKey, Is.EqualTo(HouseFinishReasons.NothingToChange));
            Assert.That(again.Cost, Is.Empty);
            Assert.That(f.Apply(again), Is.False);
            Assert.That(f.Version, Is.EqualTo(before + 1));
        }

        [Test]
        public void Apply_StalePreview_Refused()
        {
            var f = Attached();
            var a = Preview(f, FloorOak, HouseFinishTools.Room, LeftMiddle);
            var b = Preview(f, WallBlue, HouseFinishTools.Room, LeftMiddle);
            Assert.That(a.SameAs(Preview(f, FloorOak, HouseFinishTools.Room, new CellPos(1, 4))), Is.True);   // 같은 방, 같은 판
            Assert.That(f.Apply(b), Is.True);
            Assert.That(a.StateVersion, Is.Not.EqualTo(f.Version));
            Assert.That(f.Apply(a), Is.False);
            Assert.That(f.FloorAt(LeftMiddle), Is.EqualTo(FloorPlain));
            Assert.That(a.SameAs(Preview(f, FloorOak, HouseFinishTools.Room, LeftMiddle)), Is.False);        // 판이 바뀌었다
            Assert.That(f.Apply(null), Is.False);
            Assert.That(f.Apply(HouseFinishPreview.Refused(FloorOak, RoomFinishSlots.Floor, HouseFinishTools.Room, HouseFinishReasons.SharedHouse)), Is.False);
        }

        [Test]
        public void SameAs_BrushComparesCellsByValue()
        {
            var f = Attached();
            var a = Preview(f, FloorOak, HouseFinishTools.Brush, LeftMiddle);
            Assert.That(a.SameAs(Preview(f, FloorOak, HouseFinishTools.Brush, LeftMiddle)), Is.True);
            Assert.That(a.SameAs(Preview(f, FloorOak, HouseFinishTools.Brush, new CellPos(1, 3))), Is.False);
            Assert.That(a.SameAs(Preview(f, FloorPlain, HouseFinishTools.Brush, LeftMiddle)), Is.False);
            Assert.That(a.SameAs(null), Is.False);
            var r1 = HouseFinishPreview.Refused(FloorOak, RoomFinishSlots.Floor, HouseFinishTools.Room, HouseFinishReasons.NotInHouse);
            var r2 = HouseFinishPreview.Refused(FloorOak, RoomFinishSlots.Floor, HouseFinishTools.Room, HouseFinishReasons.NotInHouse);
            Assert.That(r1.SameAs(r2), Is.True);
            Assert.That(r1.CanApply, Is.False);
            Assert.That((r1.FinishId, r1.Slot, r1.Tool, r1.HasTarget), Is.EqualTo((FloorOak, RoomFinishSlots.Floor, HouseFinishTools.Room, false)));
            Assert.That(r1.Cells, Is.Empty);
        }

        [Test]
        public void ChoosingDefault_ClearsTheSlot_AndEmptyRecordIsRemoved()
        {
            var f = Attached();
            var cell = new CellPos(1, 2);
            Assert.That(f.Apply(Preview(f, FloorOak, HouseFinishTools.Brush, cell)), Is.True);
            Assert.That(f.Apply(Preview(f, WallBlue, HouseFinishTools.Brush, cell)), Is.True);
            var rec = f.ToSaveData().Finishes.Single();
            Assert.That((rec.X, rec.Y, rec.Wall, rec.Floor), Is.EqualTo((1, 2, WallBlue, FloorOak)));

            Assert.That(f.Apply(Preview(f, FloorPlain, HouseFinishTools.Brush, cell)), Is.True);
            rec = f.ToSaveData().Finishes.Single();
            Assert.That((rec.Wall, rec.Floor), Is.EqualTo((WallBlue, "")));        // 기본을 고르면 그 자리는 빈 글

            Assert.That(f.Apply(Preview(f, WallPlain, HouseFinishTools.Brush, cell)), Is.True);
            Assert.That(f.ToSaveData().Finishes, Is.Empty);                        // 두 자리가 다 빈 기록은 지운다
            Assert.That((f.WallAt(cell), f.FloorAt(cell)), Is.EqualTo((WallPlain, FloorPlain)));
        }

        [Test]
        public void UnknownIdRecord_LooksDefault_ChoosingDefaultKeepsRecord()
        {
            var data = new HouseBuildSaveData
            {
                Version = HouseBuildSaveData.KnownVersion,
                FrameId = FrameId,
                Finishes = new[]
                {
                    new HouseFinishCellSaveData { X = 1, Y = 3, Floor = "floor.ghost" },
                    new HouseFinishCellSaveData { X = 2, Y = 2, Wall = FloorOak },     // 자리가 다른 정의
                },
            };
            var f = Attached(data);
            Assert.That(f.FloorAt(new CellPos(1, 3)), Is.EqualTo(FloorPlain));
            Assert.That(f.WallAt(new CellPos(2, 2)), Is.EqualTo(WallPlain));

            Assert.That(Preview(f, FloorPlain, HouseFinishTools.Brush, new CellPos(1, 3)).ReasonKey, Is.EqualTo(HouseFinishReasons.NothingToChange));
            Assert.That(Preview(f, WallPlain, HouseFinishTools.Brush, new CellPos(2, 2)).ReasonKey, Is.EqualTo(HouseFinishReasons.NothingToChange));
            var saved = f.ToSaveData().Finishes;
            Assert.That(saved.Select(r => (r.X, r.Y, r.Wall, r.Floor)),
                Is.EqualTo(new[] { (2, 2, FloorOak, ""), (1, 3, "", "floor.ghost") }));
        }

        [Test]
        public void RecordsOnWallsOrOutside_KeptAndWrittenBack()
        {
            var data = new HouseBuildSaveData
            {
                Version = HouseBuildSaveData.KnownVersion,
                FrameId = FrameId,
                Band = "band.red",
                Finishes = new[]
                {
                    new HouseFinishCellSaveData { X = 50, Y = 50, Wall = WallBlue },
                    new HouseFinishCellSaveData { X = 0, Y = 0, Floor = FloorOak },
                    new HouseFinishCellSaveData { X = -3, Y = 1, Wall = "wall.ghost", Floor = "floor.ghost" },
                },
            };
            var f = Attached(data);
            Assert.That(f.Apply(Preview(f, FloorOak, HouseFinishTools.Room, LeftMiddle)), Is.True);
            var saved = f.ToSaveData();
            Assert.That(saved.Band, Is.EqualTo("band.red"));
            Assert.That(saved.Finishes.Where(r => r.X == 50 || r.X == 0 && r.Y == 0 || r.X == -3).Select(r => (r.X, r.Y, r.Wall, r.Floor)),
                Is.EquivalentTo(new[] { (50, 50, WallBlue, ""), (0, 0, "", FloorOak), (-3, 1, "wall.ghost", "floor.ghost") }));
            Assert.That(saved.Finishes.Length, Is.EqualTo(3 + 10));
        }

        [Test]
        public void OtherFrameId_SameCellsApply()
        {
            var data = new HouseBuildSaveData
            {
                Version = HouseBuildSaveData.KnownVersion,
                FrameId = "frame.old",
                Finishes = new[] { new HouseFinishCellSaveData { X = 1, Y = 3, Floor = FloorOak } },
            };
            var f = new HouseFinishes();
            f.Load(data);
            Assert.That(f.ToSaveData().FrameId, Is.EqualTo("frame.old"));     // 붙기 전에는 읽은 값
            f.Attach(Frame(), Find);
            Assert.That(f.FloorAt(new CellPos(1, 3)), Is.EqualTo(FloorOak));
            Assert.That(f.ToSaveData().FrameId, Is.EqualTo(FrameId));         // 붙은 뒤에는 프레임의 것
        }

        [Test]
        public void SaveData_SortedAndRoundTrips()
        {
            var data = new HouseBuildSaveData
            {
                Version = HouseBuildSaveData.KnownVersion,
                FrameId = FrameId,
                Band = "band.blue",
                Finishes = new[]
                {
                    new HouseFinishCellSaveData { X = 6, Y = 3, Floor = FloorOak },
                    new HouseFinishCellSaveData { X = 2, Y = 4, Floor = FloorOak },
                    new HouseFinishCellSaveData { X = 1, Y = 3, Wall = WallBlue },
                    new HouseFinishCellSaveData { X = 7, Y = 2, Wall = WallBlue, Floor = FloorOak },
                },
            };
            var f = Attached(data);
            var saved = f.ToSaveData();
            Assert.That(saved.Version, Is.EqualTo(HouseBuildSaveData.KnownVersion));
            Assert.That(saved.Band, Is.EqualTo("band.blue"));
            Assert.That(saved.Finishes.Select(r => (r.X, r.Y)), Is.EqualTo(new[] { (7, 2), (1, 3), (6, 3), (2, 4) }));

            var again = Attached(saved);
            Assert.That(again.ToSaveData().Finishes.Select(r => (r.X, r.Y, r.Wall, r.Floor)),
                Is.EqualTo(saved.Finishes.Select(r => (r.X, r.Y, r.Wall, r.Floor))));
            Assert.That(again.FloorAt(new CellPos(7, 2)), Is.EqualTo(FloorOak));
            Assert.That(again.WallAt(new CellPos(7, 2)), Is.EqualTo(WallBlue));
            Assert.That(again.Band, Is.EqualTo("band.blue"));

            var empty = new HouseFinishes().ToSaveData();
            Assert.That((empty.FrameId, empty.Band, empty.Finishes.Length), Is.EqualTo(("", "", 0)));
        }
    }
}
