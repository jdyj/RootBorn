using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Inventory;
using Rootborn.Core.Placement;
using Rootborn.Core.Room;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 같이 쓰는 상자(집을 같이 쓰는 방): 상자의 내용물은 방의 것. 넣기·꺼내기는 방이 허락한 뒤에만 가방이 바뀌고, 가방끼리는 주고받지 않는다.
    /// 방 쪽은 Core RoomPieceSet을 테스트가 손으로 돌린다(서버가 할 일). GameSession 명령으로 진행하는 자동 테스트다(화면·전송 근거 아님).
    /// 전송을 거친 흐름은 Rootborn.Server.Tests의 SharedChestRoomTests.
    /// </summary>
    public sealed class SharedChestSessionTests
    {
        private sealed class FakeRoom
        {
            private readonly List<(string Player, RoomShares Shares)> _players = new List<(string, RoomShares)>();
            private int _ids;

            public FakeRoom(params string[] shared)
            {
                var d = TestWorld.Definitions;
                var rules = new RoomHouseRules(d.All<FurnitureDefinition>(), s => s == HouseRoom.SurfaceId ? HouseRoom.Grid : null,
                    new FurnitureStorageCatalog(d.All<FurnitureStorageDefinition>()), d.All<ItemDefinition>());
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

            /// <summary>그 사람의 보낼 요청을 방이 반영하고 답과 바뀐 상태를 돌려준다. 답에 따라 새 요청(쓰기·풀기)이 생기면 그것도 보낸다.</summary>
            public void Deliver(RoomShares from)
            {
                for (int round = 0; round < 4 && from.Outbox.Requests.Count > 0; round++)
                {
                    var (player, _) = _players.First(p => p.Shares == from);
                    var answers = new List<RoomAnswer>();
                    var changed = new List<IRoomPiece>();
                    Pieces.Apply(player, from.Outbox.Requests.Select(r => r.Copy()).ToList(), 1, answers, changed);
                    foreach (var piece in changed)
                        foreach (var p in _players) p.Shares.Receive(piece.State());
                    foreach (var a in answers) from.Receive(a);
                }
            }

            public RoomChests Chests => Pieces.TryGet(RoomChests.PieceName, out var c) ? (RoomChests)c : null;
        }

        private static string ChestFurniture => TestWorld.Definitions.All<FurnitureStorageDefinition>().First().Furniture.StableId;

        private static GameSession Start(TempSaves saves, RoomShares shares, string slot, string player)
        {
            var game = GameSession.Start(TestWorld.Definitions, TestWorld.Settings, saves.Store, slot, player, shares: shares);
            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            return game;
        }

        private static string PlaceChest(FakeRoom room, RoomShares shares, GameSession game)
        {
            game.PlaceFurniture(ChestFurniture, HouseRoom.FirstFreeAnchor(game, ChestFurniture), FurniturePlacementDirection.North);
            room.Deliver(shares);
            return game.House.Placed.Single(p => p.Definition.StableId == ChestFurniture).InstanceId;
        }

        [Test]
        public void OnePutsAnotherTakes_TheBagChangesOnlyAfterTheRoomAllows()
        {
            using var saves = new TempSaves();
            var room = new FakeRoom(RoomHouse.PieceName);
            var aShares = room.Join("a");
            var bShares = room.Join("b");
            var a = Start(saves, aShares, "slot-a", "a");
            var b = Start(saves, bShares, "slot-b", "b");
            string chest = PlaceChest(room, aShares, a);
            Assert.That(b.House.Placed.Any(p => p.InstanceId == chest), Is.True, "b도 같은 상자를 본다");

            var wood = a.Item("Wood");
            a.Inventory.Add(wood, 8);
            Assert.That(a.PutIntoStorage(chest, "Wood", 5), Is.EqualTo(StorageTransferResult.Requested));
            Assert.That(a.WaitingForStorage, Is.True);
            Assert.That(a.Inventory.CountOf(wood), Is.EqualTo(8), "허락 전에는 가방이 그대로다");
            Assert.That(a.PutIntoStorage(chest, "Wood", 1), Is.EqualTo(StorageTransferResult.Busy));

            room.Deliver(aShares);
            Assert.That(a.WaitingForStorage, Is.False);
            Assert.That(a.LastStorageResult, Is.EqualTo(StorageTransferResult.Moved));
            Assert.That(a.Inventory.CountOf(wood), Is.EqualTo(3));
            Assert.That(room.Chests.CountOf(chest, "Wood"), Is.EqualTo(5));
            Assert.That(b.StorageAt(chest).CountOf(b.Item("Wood")), Is.EqualTo(5), "b가 방의 상태로 본다");

            Assert.That(b.TakeFromStorage(chest, "Wood", 5), Is.EqualTo(StorageTransferResult.Requested));
            room.Deliver(bShares);
            Assert.That(b.Inventory.CountOf(b.Item("Wood")), Is.EqualTo(5));
            Assert.That(room.Chests.CountOf(chest, "Wood"), Is.EqualTo(0));
            Assert.That(a.Inventory.CountOf(wood), Is.EqualTo(3), "a의 가방은 그대로 — 가방끼리 주고받지 않는다");
        }

        [Test]
        public void BothTakeTheSameItems_OnlyTheFirstGetsThem()
        {
            using var saves = new TempSaves();
            var room = new FakeRoom(RoomHouse.PieceName);
            var aShares = room.Join("a");
            var bShares = room.Join("b");
            var a = Start(saves, aShares, "slot-a", "a");
            var b = Start(saves, bShares, "slot-b", "b");
            string chest = PlaceChest(room, aShares, a);
            a.Inventory.Add(a.Item("Stone"), 4);
            a.PutIntoStorage(chest, "Stone", 4);
            room.Deliver(aShares);

            Assert.That(a.TakeFromStorage(chest, "Stone", 4), Is.EqualTo(StorageTransferResult.Requested));
            Assert.That(b.TakeFromStorage(chest, "Stone", 4), Is.EqualTo(StorageTransferResult.Requested));
            room.Deliver(bShares); // b의 요청이 먼저 도착
            room.Deliver(aShares);
            Assert.That(b.Inventory.CountOf(b.Item("Stone")), Is.EqualTo(4));
            Assert.That(a.Inventory.CountOf(a.Item("Stone")), Is.EqualTo(0));
            Assert.That(a.LastStorageResult, Is.EqualTo(StorageTransferResult.NotEnough), "늦은 쪽은 거절, 가방 그대로");
            Assert.That(room.Chests.CountOf(chest, "Stone"), Is.EqualTo(0));
        }

        [Test]
        public void BagChangedBeforeTheAnswer_ReleasesTheHold_NothingIsLostOrCopied()
        {
            using var saves = new TempSaves();
            var room = new FakeRoom(RoomHouse.PieceName);
            var aShares = room.Join("a");
            var a = Start(saves, aShares, "slot-a", "a");
            string chest = PlaceChest(room, aShares, a);
            var wood = a.Item("Wood");
            a.Inventory.Add(wood, 3);
            a.PutIntoStorage(chest, "Wood", 3);
            a.Inventory.Remove(wood, 2); // 답이 오기 전에 가방의 나무를 다른 데 썼다
            room.Deliver(aShares);

            Assert.That(a.LastStorageResult, Is.EqualTo(StorageTransferResult.NotEnough));
            Assert.That(a.Inventory.CountOf(wood), Is.EqualTo(1));
            Assert.That(room.Chests.CountOf(chest, "Wood"), Is.EqualTo(0), "자리만 풀렸다");
            Assert.That(room.Chests.HeldCount, Is.EqualTo(0));
        }

        [Test]
        public void ARestartBetweenRequestAndAnswer_FinishesTheMoveOnce()
        {
            using var saves = new TempSaves();
            var room = new FakeRoom(RoomHouse.PieceName);
            var aShares = room.Join("a");
            var a = Start(saves, aShares, "slot-a", "a");
            string chest = PlaceChest(room, aShares, a);
            var wood = a.Item("Wood");
            a.Inventory.Add(wood, 6);
            a.PutIntoStorage(chest, "Wood", 6); // 요청과 가방(6)이 저장됐다

            // 꺼졌다 켜짐: 새 접속, 새 게임 한 판. 같은 요청이 다시 가고, 방이 한 번만 반영한다.
            var again = room.Join("a");
            var a2 = GameSession.Start(TestWorld.Definitions, TestWorld.Settings, saves.Store, "slot-a", "a", shares: again);
            a2.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            Assert.That(again.Outbox.Requests.Count, Is.EqualTo(1), "보낼 요청이 세이브에 남았다");
            room.Deliver(again);
            room.Deliver(again);
            Assert.That(a2.Inventory.CountOf(wood), Is.EqualTo(0));
            Assert.That(room.Chests.CountOf(chest, "Wood"), Is.EqualTo(6), "한 번만 들어갔다");

            var a3 = GameSession.Start(TestWorld.Definitions, TestWorld.Settings, saves.Store, "slot-a", "a", shares: room.Join("a"));
            Assert.That(a3.Inventory.CountOf(wood), Is.EqualTo(0), "가방도 그대로 저장됐다");
        }

        [Test]
        public void HouseNotShared_EachPlayerHasTheirOwnChests()
        {
            using var saves = new TempSaves();
            var room = new FakeRoom(RoomWallet.PieceName);
            var shares = room.Join("a");
            Assert.That(room.Chests, Is.Null, "집을 각자 쓰는 방에는 같이 쓰는 상자가 없다");
            HouseRoom.EmptyHouse(saves.Store, "slot-a"); // 준비: 처음 배치 가구 없이(놓은 상자 하나만 보려고)
            var a = Start(saves, shares, "slot-a", "a");
            a.PlaceFurniture(ChestFurniture, HouseRoom.FirstFreeAnchor(a, ChestFurniture), FurniturePlacementDirection.North);
            string chest = a.House.Placed.Single().InstanceId;
            a.Inventory.Add(a.Item("Wood"), 2);
            Assert.That(a.PutIntoStorage(chest, "Wood", 2), Is.EqualTo(StorageTransferResult.Moved), "자기 집의 상자는 바로 바뀐다");
        }
    }
}
