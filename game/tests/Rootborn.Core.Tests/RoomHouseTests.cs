using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;
using Rootborn.Core.Room;
using Rootborn.Core.Save;

namespace Rootborn.Core.Tests
{
    /// <summary>
    /// 같이 쓰는 집(멀티플레이 설계 4.1절): 방이 놓인 가구 목록의 주인이고, 놓기·옮기기·치우기를 Core 배치 규칙으로 판정한다.
    /// 같은 칸에 먼저 온 요청만 되고, 같은 요청 id는 한 번만 반영된다. 플레이어 쪽은 요청만 적고 방이 알린 상태로 집을 채운다.
    /// </summary>
    public sealed class RoomHouseTests
    {
        private static GridPoint P(int x, int y) => new GridPoint(x, y);

        private static FurnitureDefinition Furniture(string id, bool blocks = true, GridPoint[] parts = null)
        {
            var tileParts = (parts ?? new[] { P(0, 0) }).Select(cell => new FurnitureTilePart(cell, "sprite.png", "tile")).ToArray();
            return new FurnitureDefinition(id, id, null, "Seating", new[] { "house" }, tileParts, null, blocks, FurniturePlacementPreference.Any);
        }

        // 5x5 바닥, (0,0)이 들어오는 칸이자 문 앞, (2,4)도 문 앞, (4,4)는 고정 칸.
        private static PlacementGrid Grid()
        {
            var floor = new List<GridPoint>();
            for (int x = 0; x < 5; x++) for (int y = 0; y < 5; y++) floor.Add(P(x, y));
            return new PlacementGrid(floor, new[] { P(4, 4) }, P(0, 0), new[] { P(0, 0), P(2, 4) });
        }

        private static readonly FurnitureDefinition Chair = Furniture("chair");
        private static readonly FurnitureDefinition Bench = Furniture("bench", parts: new[] { P(0, 0), P(1, 0) });
        private static readonly FurnitureDefinition Rug = Furniture("rug", blocks: false);

        private static RoomHouseRules Rules() => new RoomHouseRules(new[] { Chair, Bench, Rug }, surface => surface == "house" ? Grid() : null);

        private static RoomPieceSet Room(RoomHouseRules rules = null)
            => new RoomPieceSet(new RoomShareSettings(new[] { RoomHouse.PieceName }), RoomPieceCatalog.CreateDefault(rules ?? Rules()));

        private static RoomRequestSaveData Req(string id, string kind, params string[] ids)
            => new RoomRequestSaveData { Piece = RoomHouse.PieceName, RequestId = id, Kind = kind, Ids = ids };

        private static RoomRequestSaveData PlaceReq(string id, string furniture, int x, int y, int direction = 0)
            => Req(id, RoomHouse.Place, "house", furniture, x.ToString(), y.ToString(), direction.ToString());

        private static RoomAnswer Apply(RoomPieceSet room, string player, RoomRequestSaveData request, List<IRoomPiece> changed = null)
        {
            var answers = new List<RoomAnswer>();
            room.Apply(player, new[] { request }, 3, answers, changed);
            return answers[0];
        }

        private static RoomHouse House(RoomPieceSet room)
        {
            room.TryGet(RoomHouse.PieceName, out var piece);
            return (RoomHouse)piece;
        }

        [Test]
        public void HOUSE_001_APlacement_IsInTheRoomsState_WithTheRoomsInstanceId_AndWhoPlacedIt()
        {
            var room = Room();
            var changed = new List<IRoomPiece>();
            Assert.That(Apply(room, "ana", PlaceReq("r1", "chair", 2, 2), changed).Outcome, Is.EqualTo(RoomOutcomes.Applied));
            Assert.That(changed.Count, Is.EqualTo(1), "모두에게 다시 알릴 조각");

            var placed = House(room).Placed.Single();
            Assert.That(placed.FurnitureId, Is.EqualTo("chair"));
            Assert.That((placed.AnchorX, placed.AnchorY), Is.EqualTo((2, 2)));
            Assert.That(placed.PlacedBy, Is.EqualTo("ana"));
            Assert.That(placed.Day, Is.EqualTo(3));

            var state = House(room).State();
            Assert.That(state.Amount, Is.EqualTo(1));
            Assert.That(RoomHousePlacement.TryDecode(state.Ids[0], out var line), Is.True);
            Assert.That(line.InstanceId, Is.EqualTo(placed.InstanceId));
            Assert.That(line.Anchor, Is.EqualTo(P(2, 2)));
        }

        [Test]
        public void HOUSE_002_TwoPlacementsOnTheSameCell_OnlyTheFirstToArrive()
        {
            var room = Room();
            Assert.That(Apply(room, "ana", PlaceReq("a", "chair", 2, 2)).Outcome, Is.EqualTo(RoomOutcomes.Applied));
            var changed = new List<IRoomPiece>();
            Assert.That(Apply(room, "ben", PlaceReq("b", "bench", 1, 2), changed).Outcome, Is.EqualTo(RoomOutcomes.Refused), "벤치의 오른쪽 칸이 의자 칸이다");
            Assert.That(changed, Is.Empty, "거절이면 상태가 그대로다");
            Assert.That(House(room).Placed.Count, Is.EqualTo(1));
        }

        [Test]
        public void HOUSE_003_TheSameRequestTwice_IsAppliedOnce_AndGetsTheSameAnswer()
        {
            var room = Room();
            var first = Apply(room, "ana", PlaceReq("a", "chair", 2, 2));
            var again = Apply(room, "ana", PlaceReq("a", "chair", 3, 3));
            Assert.That(again.Outcome, Is.EqualTo(first.Outcome));
            Assert.That(House(room).Placed.Count, Is.EqualTo(1));
            Assert.That(Apply(room, "ben", PlaceReq("a", "chair", 3, 3)).Outcome, Is.EqualTo(RoomOutcomes.Invalid), "남의 요청 id");
        }

        [Test]
        public void HOUSE_004_TheRoomJudgesWithTheCoreRules_FixedCellsEntryAndBlockedPaths()
        {
            var room = Room();
            Assert.That(Apply(room, "ana", PlaceReq("a", "chair", 4, 4)).Outcome, Is.EqualTo(RoomOutcomes.Refused), "고정 칸");
            Assert.That(Apply(room, "ana", PlaceReq("b", "chair", 0, 0)).Outcome, Is.EqualTo(RoomOutcomes.Refused), "들어오는 칸");
            Assert.That(Apply(room, "ana", PlaceReq("c", "chair", 9, 9)).Outcome, Is.EqualTo(RoomOutcomes.Refused), "바닥 밖");
            Assert.That(Apply(room, "ana", Req("d", RoomHouse.Place, "attic", "chair", "1", "1", "0")).Outcome, Is.EqualTo(RoomOutcomes.Refused), "칸을 모르는 면");
            Assert.That(Apply(room, "ana", PlaceReq("e", "sofa", 1, 1)).Outcome, Is.EqualTo(RoomOutcomes.Invalid), "모르는 가구");
            Assert.That(Apply(room, "ana", PlaceReq("f", "chair", 1, 1, 9)).Outcome, Is.EqualTo(RoomOutcomes.Invalid), "모르는 방향");
            Assert.That(Apply(room, "ana", Req("g", RoomHouse.Place, "house", "ch|air", "1", "1", "0")).Outcome, Is.EqualTo(RoomOutcomes.Invalid), "구분 글자");
            Assert.That(House(room).Placed, Is.Empty);

            // 막는 가구 둘로 들어오는 칸(0,0)을 가두면(문 앞 (2,4)에 못 감) 두 번째는 안 된다: (1,0)과 (0,1).
            Assert.That(Apply(room, "ana", PlaceReq("h", "chair", 1, 0)).Outcome, Is.EqualTo(RoomOutcomes.Applied));
            Assert.That(Apply(room, "ben", PlaceReq("i", "chair", 0, 1)).Outcome, Is.EqualTo(RoomOutcomes.Refused), "둘이 따로는 되지만 같이 놓으면 길이 막힌다");
            Assert.That(Apply(room, "ben", PlaceReq("j", "rug", 0, 1)).Outcome, Is.EqualTo(RoomOutcomes.Applied), "막지 않는 가구는 된다");
        }

        [Test]
        public void HOUSE_005_MoveAndRemove_ByAnyoneInTheRoom_FirstOneWins()
        {
            var room = Room();
            Apply(room, "ana", PlaceReq("a", "chair", 2, 2));
            Apply(room, "ana", PlaceReq("b", "chair", 3, 3));
            string first = House(room).Placed[0].InstanceId;
            string second = House(room).Placed[1].InstanceId;

            Assert.That(Apply(room, "ben", Req("m1", RoomHouse.Move, first, "3", "3", "0")).Outcome, Is.EqualTo(RoomOutcomes.Refused), "다른 가구 칸");
            Assert.That(Apply(room, "ben", Req("m2", RoomHouse.Move, first, "2", "2", "1")).Outcome, Is.EqualTo(RoomOutcomes.Applied), "자기 옛 칸에서 돌리기");
            Assert.That(Apply(room, "ben", Req("m3", RoomHouse.Move, first, "1", "3", "0")).Outcome, Is.EqualTo(RoomOutcomes.Applied));
            var moved = House(room).Placed[0];
            Assert.That((moved.AnchorX, moved.AnchorY, moved.PlacedBy), Is.EqualTo((1, 3, "ana")), "옮겨도 놓은 사람은 그대로");

            Assert.That(Apply(room, "ben", Req("x1", RoomHouse.Remove, second)).Outcome, Is.EqualTo(RoomOutcomes.Applied));
            Assert.That(Apply(room, "ana", Req("x2", RoomHouse.Remove, second)).Outcome, Is.EqualTo(RoomOutcomes.Refused), "누가 먼저 치웠다");
            Assert.That(Apply(room, "ana", Req("m4", RoomHouse.Move, second, "2", "2", "0")).Outcome, Is.EqualTo(RoomOutcomes.Refused), "이미 없는 가구");

            Assert.That(Apply(room, "ana", Req("c", RoomHouse.Clear, "house")).Outcome, Is.EqualTo(RoomOutcomes.Applied));
            Assert.That(House(room).Placed, Is.Empty);
        }

        [Test]
        public void HOUSE_006_TheRoomsHouse_IsSavedAndRead_WithNewIdsNeverReused()
        {
            var store = new InMemorySaveStore();
            var room = Room();
            Apply(room, "ana", PlaceReq("a", "chair", 2, 2));
            Apply(room, "ana", PlaceReq("b", "chair", 3, 3));
            string removed = House(room).Placed[1].InstanceId;
            Apply(room, "ana", Req("x", RoomHouse.Remove, removed));
            room.Save(store, "room");

            var again = Room();
            again.Load(store, "room");
            Assert.That(House(again).Placed.Single().PlacedBy, Is.EqualTo("ana"));
            Assert.That(Apply(again, "ana", PlaceReq("a", "chair", 1, 1)).Outcome, Is.EqualTo(RoomOutcomes.Applied), "장부도 읽었다 — 같은 답을 다시 준다");
            Assert.That(House(again).Placed.Count, Is.EqualTo(1));
            Apply(again, "ben", PlaceReq("c", "chair", 1, 1));
            Assert.That(House(again).Placed[1].InstanceId, Is.Not.EqualTo(removed), "치운 가구의 id를 다시 쓰지 않는다");
        }

        [Test]
        public void HOUSE_007_WithoutRules_TheRoomRefusesToPlace_ButCanStillRemove()
        {
            var room = new RoomPieceSet(new RoomShareSettings(new[] { RoomHouse.PieceName }), RoomPieceCatalog.CreateDefault());
            Assert.That(Apply(room, "ana", PlaceReq("a", "chair", 2, 2)).Outcome, Is.EqualTo(RoomOutcomes.Refused));
            Assert.That(Apply(room, "ana", Req("b", RoomHouse.Clear, "house")).Outcome, Is.EqualTo(RoomOutcomes.Applied));
        }

        [Test]
        public void HOUSE_008_TheCatalog_KnowsTheHouse()
        {
            Assert.That(RoomPieceCatalog.CreateDefault().Names, Does.Contain(RoomHouse.PieceName));
        }

        [Test]
        public void HOUSE_009_PlayerSide_WritesRequests_FollowsTheRoomsState_AndHearsAnswers()
        {
            var shares = new RoomShares(new RoomShareSettings(new[] { RoomHouse.PieceName }), RoomPieceCatalog.CreateDefault());
            var house = shares.Find<RoomHouseClient>();
            Assert.That(house, Is.Not.Null);
            int changes = 0;
            shares.Changed += _ => changes++;
            var answers = new List<(string Kind, bool Applied)>();
            house.Answered += (r, ok) => answers.Add((r.Kind, ok));

            string id = house.RequestPlace("house", "chair", P(2, 2), FurniturePlacementDirection.East);
            Assert.That(house.HasPending, Is.True);
            var sent = shares.Outbox.Requests.Single();
            Assert.That(sent.Ids, Is.EqualTo(new[] { "house", "chair", "2", "2", ((int)FurniturePlacementDirection.East).ToString() }));

            // 방이 같은 요청을 판정해 상태를 알린다.
            var room = Room();
            var answer = Apply(room, "ana", sent);
            shares.Receive(new RoomAnswer(answer.Piece, id, answer.Outcome, answer.Amount));
            Assert.That(answers, Is.EqualTo(new[] { (RoomHouse.Place, true) }));
            Assert.That(house.HasPending, Is.False);
            shares.Receive(House(room).State());
            Assert.That(changes, Is.EqualTo(1));
            shares.Receive(House(room).State());
            Assert.That(changes, Is.EqualTo(1), "같은 상태면 알리지 않는다");

            var records = new List<FurniturePlacementSaveData>();
            var ids = new List<string>();
            house.CopySurface("house", records, ids);
            Assert.That(ids, Is.EqualTo(new[] { House(room).Placed[0].InstanceId }));
            Assert.That(records[0].Direction, Is.EqualTo(FurniturePlacementDirection.East));

            Assert.Throws<ArgumentException>(() => house.RequestRemove("a|b"));
        }

        [Test]
        public void HOUSE_010_ALayout_RestoredWithTheRoomsIds_UsesThem()
        {
            var layout = new HouseFurnitureLayout(new HousePlacementModel(Grid()), "house");
            var records = new[]
            {
                new FurniturePlacementSaveData { FurnitureId = "chair", SurfaceId = "house", AnchorX = 4, AnchorY = 4 }, // 고정 칸 — 되살리지 못함
                new FurniturePlacementSaveData { FurnitureId = "chair", SurfaceId = "house", AnchorX = 2, AnchorY = 2 },
            };
            layout.Restore(records, new[] { Chair }, new[] { "7", "9" });
            Assert.That(layout.Placed.Single().InstanceId, Is.EqualTo("9"), "기록 순서로 짝을 맞춘다");
            Assert.Throws<ArgumentException>(() => layout.Restore(records, new[] { Chair }, new[] { "7" }));
        }
    }
}
