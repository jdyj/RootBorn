using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Placement;
using Rootborn.Core.Room;
using Rootborn.Core.Save;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 같이 쓰는 집(멀티플레이 설계 4.1절): 집 공유를 켠 방에서 게임 한 판의 집 배치는 방의 것이다. 방 쪽은 Core RoomPieceSet을 테스트가 손으로 돌린다
    /// (보낼 요청을 방에 넘기고 답과 상태를 돌려준다 — 서버가 할 일). 방의 판정 칸은 실제 집 맵에서 만든 칸이다(HouseRoom).
    /// GameSession 명령으로 진행하는 자동 테스트다(화면·전송 근거 아님). 전송을 거친 흐름은 Rootborn.Server.Tests의 SharedHouseRoomTests.
    /// </summary>
    public sealed class SharedHouseSessionTests
    {
        private sealed class FakeRoom
        {
            private readonly List<(string Player, RoomShares Shares)> _players = new List<(string, RoomShares)>();
            private int _ids;

            public FakeRoom(params string[] shared)
            {
                var rules = new RoomHouseRules(TestWorld.Definitions.All<FurnitureDefinition>(), s => s == HouseRoom.SurfaceId ? HouseRoom.Grid : null);
                Pieces = new RoomPieceSet(new RoomShareSettings(shared), RoomPieceCatalog.CreateDefault(rules));
            }

            public RoomPieceSet Pieces { get; }

            public RoomShares Join(string playerId)
            {
                var shares = new RoomShares(Pieces.Settings, RoomPieceCatalog.CreateDefault(), () => playerId + "-r" + (++_ids));
                foreach (var piece in Pieces.Pieces) shares.Receive(piece.State());
                _players.Add((playerId, shares));
                return shares;
            }

            public void Deliver(RoomShares from)
            {
                var (player, _) = _players.First(p => p.Shares == from);
                var answers = new List<RoomAnswer>();
                var changed = new List<IRoomPiece>();
                Pieces.Apply(player, from.Outbox.Requests.Select(r => r.Copy()).ToList(), 1, answers, changed);
                foreach (var a in answers) from.Receive(a);
                foreach (var piece in changed)
                    foreach (var p in _players) p.Shares.Receive(piece.State());
            }

            public RoomHouse House => Pieces.TryGet(RoomHouse.PieceName, out var h) ? (RoomHouse)h : null;
        }

        private static GameSession Start(TempSaves saves, RoomShares shares, string slot, string player)
        {
            var game = GameSession.Start(TestWorld.Definitions, TestWorld.Settings, saves.Store, slot, player, shares: shares);
            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            return game;
        }

        private static string FreeFurniture(GameSession game) => game.AvailableFurniture()[0].StableId;

        [Test]
        public void HouseShareOn_PlacingWaitsForTheRoom_ThenEveryoneSeesIt()
        {
            using var saves = new TempSaves();
            var room = new FakeRoom(RoomHouse.PieceName);
            var aShares = room.Join("a");
            var bShares = room.Join("b");
            var a = Start(saves, aShares, "slot-a", "a");
            var b = Start(saves, bShares, "slot-b", "b");
            Assert.That(a.SharesHouse, Is.True);

            string chair = FreeFurniture(a);
            var anchor = HouseRoom.FirstFreeAnchor(a, chair);
            var furnitureChanges = 0;
            b.Changed += topic => { if (topic == GameChangeTopics.Furniture) furnitureChanges++; };

            Assert.That(a.PlaceFurniture(chair, anchor, FurniturePlacementDirection.North), Is.False, "지금 바뀐 것은 없다 — 방에 요청만 했다");
            Assert.That(a.WaitingForRoom, Is.True);
            Assert.That(a.House.Placed, Is.Empty, "허락 전에는 집이 그대로다");
            Assert.That(a.PlaceFurniture(chair, HouseRoom.FirstFreeAnchor(a, chair, 3), FurniturePlacementDirection.North), Is.False);
            Assert.That(aShares.Outbox.Requests.Count(r => r.Piece == RoomHouse.PieceName), Is.EqualTo(1), "기다리는 동안 다른 요청을 적지 않는다");
            Assert.That(saves.Store.TryRead<RoomOutboxSaveData>("slot-a", SaveFileNames.RoomOutbox("a"), out var outbox) && outbox.Requests.Length == 1, Is.True,
                "요청은 세이브와 함께 간다");

            room.Deliver(aShares);
            Assert.That(a.WaitingForRoom, Is.False);
            Assert.That(a.LastRoomCommandApplied, Is.True);
            Assert.That(a.House.Placed.Single().Anchor, Is.EqualTo(anchor));
            Assert.That(b.House.Placed.Single().Anchor, Is.EqualTo(anchor), "다른 사람의 집에도 보인다");
            Assert.That(b.House.Placed[0].InstanceId, Is.EqualTo(a.House.Placed[0].InstanceId), "같은 방의 인스턴스 id");
            Assert.That(furnitureChanges, Is.GreaterThan(0), "다른 사람에게 Furniture를 알린다(Host가 그림·충돌을 다시 맞춘다)");
            Assert.That(room.House.Placed[0].PlacedBy, Is.EqualTo("a"));
            Assert.That(saves.Store.TryRead<FurniturePlacementLayoutSaveData>("slot-a", SaveFileNames.HouseFurnitureLayout(a.HouseStage), out _), Is.False,
                "자기 배치 파일은 쓰지 않는다");
        }

        [Test]
        public void HouseShareOn_TwoOnTheSameCellAtOnce_OnlyTheFirstToArrive()
        {
            using var saves = new TempSaves();
            var room = new FakeRoom(RoomHouse.PieceName);
            var aShares = room.Join("a");
            var bShares = room.Join("b");
            var a = Start(saves, aShares, "slot-a", "a");
            var b = Start(saves, bShares, "slot-b", "b");
            string chair = FreeFurniture(a);
            var anchor = HouseRoom.FirstFreeAnchor(a, chair);

            a.PlaceFurniture(chair, anchor, FurniturePlacementDirection.North);
            b.PlaceFurniture(chair, anchor, FurniturePlacementDirection.North);
            Assert.That(b.WaitingForRoom, Is.True, "b가 보기에는 아직 빈 칸이다");

            room.Deliver(aShares);
            room.Deliver(bShares);
            Assert.That(b.WaitingForRoom, Is.False);
            Assert.That(b.LastRoomCommandApplied, Is.False, "방이 거절했다 — 먼저 온 a만");
            Assert.That(room.House.Placed.Count, Is.EqualTo(1));
            Assert.That(b.House.Placed.Single().Anchor, Is.EqualTo(anchor));
        }

        [Test]
        public void HouseShareOn_SomeoneElseRemovesIt_ThePlacerStillHasTheFurniture_AndCanPlaceItAgain()
        {
            using var saves = new TempSaves();
            var room = new FakeRoom(RoomHouse.PieceName);
            var aShares = room.Join("a");
            var bShares = room.Join("b");
            var a = Start(saves, aShares, "slot-a", "a");
            var b = Start(saves, bShares, "slot-b", "b");
            string chair = FreeFurniture(a);
            a.PlaceFurniture(chair, HouseRoom.FirstFreeAnchor(a, chair), FurniturePlacementDirection.North);
            room.Deliver(aShares);
            int available = a.AvailableFurniture().Count;

            Assert.That(b.RemoveFurniture(b.House.Placed[0].InstanceId), Is.False);
            room.Deliver(bShares);
            Assert.That(a.House.Placed, Is.Empty);
            Assert.That(b.House.Placed, Is.Empty);
            Assert.That(a.AvailableFurniture().Count, Is.EqualTo(available), "해금은 놓아도 줄지 않아 치워도 돌려줄 것이 없다 — 놓은 사람은 그대로 가진다");

            a.PlaceFurniture(chair, HouseRoom.FirstFreeAnchor(a, chair), FurniturePlacementDirection.North);
            room.Deliver(aShares);
            Assert.That(a.House.Placed.Count, Is.EqualTo(1));
        }

        [Test]
        public void HouseShareOn_MoveAndClear_GoThroughTheRoom()
        {
            using var saves = new TempSaves();
            var room = new FakeRoom(RoomHouse.PieceName);
            var shares = room.Join("a");
            var a = Start(saves, shares, "slot-a", "a");
            string chair = FreeFurniture(a);
            a.PlaceFurniture(chair, HouseRoom.FirstFreeAnchor(a, chair), FurniturePlacementDirection.North);
            room.Deliver(shares);
            var target = HouseRoom.FirstFreeAnchor(a, chair, 5);

            Assert.That(a.MoveFurniture(a.House.Placed[0].InstanceId, target, FurniturePlacementDirection.North), Is.False);
            Assert.That(a.WaitingForRoom, Is.True);
            room.Deliver(shares);
            Assert.That(a.House.Placed.Single().Anchor, Is.EqualTo(target));

            Assert.That(a.ClearFurniture(), Is.False);
            room.Deliver(shares);
            Assert.That(a.House.Placed, Is.Empty);
            Assert.That(a.MoveFurniture("no-such", target, FurniturePlacementDirection.North), Is.False);
            Assert.That(a.WaitingForRoom, Is.False, "없는 가구는 방에 묻지 않는다");
        }

        [Test]
        public void HouseShareOn_StartingAgain_RestoresTheRoomsHouse_NotTheOwnFile()
        {
            using var saves = new TempSaves();
            // 혼자 할 때 놓은 가구가 있는 슬롯(처음 배치 가구 없이 — 놓은 의자 하나만 보려고).
            HouseRoom.EmptyHouse(saves.Store, "slot-a");
            var solo =GameSession.Start(TestWorld.Definitions, TestWorld.Settings, saves.Store, "slot-a", "a");
            solo.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            string chair = FreeFurniture(solo);
            var soloAnchor = HouseRoom.FirstFreeAnchor(solo, chair);
            Assert.That(solo.PlaceFurniture(chair, soloAnchor, FurniturePlacementDirection.North), Is.True);

            var room = new FakeRoom(RoomHouse.PieceName);
            var shares = room.Join("a");
            var a = Start(saves, shares, "slot-a", "a");
            Assert.That(a.House.Placed, Is.Empty, "같이 쓰는 집은 방의 것이다(처음엔 비어 있다)");
            var roomAnchor = HouseRoom.FirstFreeAnchor(a, chair, 7);
            a.PlaceFurniture(chair, roomAnchor, FurniturePlacementDirection.North);
            room.Deliver(shares);

            var again = Start(saves, room.Join("a"), "slot-a", "a");
            Assert.That(again.House.Placed.Single().Anchor, Is.EqualTo(roomAnchor), "다시 시작하면 방의 집");

            var alone = GameSession.Start(TestWorld.Definitions, TestWorld.Settings, saves.Store, "slot-a", "a");
            alone.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            Assert.That(alone.House.Placed.Single().Anchor, Is.EqualTo(soloAnchor), "혼자 하기의 집은 그대로 남았다");
        }

        [Test]
        public void HouseShareOff_EachHasTheirOwnHouse()
        {
            using var saves = new TempSaves();
            var room = new FakeRoom(RoomWallet.PieceName);
            var aShares = room.Join("a");
            HouseRoom.EmptyHouse(saves.Store, "slot-b"); // 준비: b의 집은 처음 배치 가구 없이(a가 놓은 것이 오지 않는지 보려고)
            var a = Start(saves, aShares, "slot-a", "a");
            var b = Start(saves, room.Join("b"), "slot-b", "b");
            Assert.That(a.SharesHouse, Is.False);
            string chair = FreeFurniture(a);
            Assert.That(a.PlaceFurniture(chair, HouseRoom.FirstFreeAnchor(a, chair), FurniturePlacementDirection.North), Is.True, "곧바로 놓인다");
            room.Deliver(aShares);
            Assert.That(b.House.Placed, Is.Empty);
            Assert.That(aShares.Outbox.Requests.Where(r => r.Piece == RoomHouse.PieceName), Is.Empty);
        }

        [Test]
        public void HouseShareOn_FurnitureINeverUnlocked_IsNotAskedFor()
        {
            using var saves = new TempSaves();
            var room = new FakeRoom(RoomHouse.PieceName);
            var shares = room.Join("a");
            var a = Start(saves, shares, "slot-a", "a");
            var locked = a.Definitions.All<FurnitureDefinition>().First(f => !FurnitureCatalog.IsAvailable(f, a.FurnitureUnlocks));
            var anyAnchor = HouseRoom.FirstFreeAnchor(a, FreeFurniture(a));
            Assert.That(a.PlaceFurniture(locked.StableId, anyAnchor, FurniturePlacementDirection.North), Is.False);
            Assert.That(a.WaitingForRoom, Is.False);
            Assert.That(shares.Outbox.Requests, Is.Empty, "놓는 사람이 가진 가구만 놓는다(해금은 각자)");
        }
    }
}
