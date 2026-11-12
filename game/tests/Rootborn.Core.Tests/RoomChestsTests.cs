using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Inventory;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;
using Rootborn.Core.Room;

namespace Rootborn.Core.Tests
{
    /// <summary>
    /// 같이 쓰는 상자(멀티플레이 설계 4.1절): 같이 쓰는 집에 놓인 상자의 내용물은 방의 것. 넣기·꺼내기는 잡기 → 쓰기/풀기이고, 방이 한 번에 하나씩 판정한다.
    /// 같은 물건을 둘이 꺼내면 먼저 온 요청만, 물건이 든 상자는 치울 수 없고, 서버를 끄고 켜도 이어진다.
    /// </summary>
    public sealed class RoomChestsTests
    {
        private static GridPoint P(int x, int y) => new GridPoint(x, y);

        private static FurnitureDefinition Furniture(string id)
            => new FurnitureDefinition(id, id, null, "Storage", new[] { "house" }, new[] { new FurnitureTilePart(P(0, 0), "s.png", "t") }, null, true,
                FurniturePlacementPreference.Any);

        private static readonly FurnitureDefinition Crate = Furniture("crate");
        private static readonly FurnitureDefinition Chair = Furniture("chair");
        private static readonly ItemDefinition Wood = new ItemDefinition("Wood", maxStack: 10);
        private static readonly ItemDefinition Stone = new ItemDefinition("Stone", maxStack: 10);

        private static PlacementGrid Grid()
        {
            var floor = new List<GridPoint>();
            for (int x = 0; x < 5; x++) for (int y = 0; y < 5; y++) floor.Add(P(x, y));
            return new PlacementGrid(floor, null, P(0, 0), new[] { P(0, 0) });
        }

        private static RoomHouseRules Rules(int slots = 2)
            => new RoomHouseRules(new[] { Crate, Chair }, s => s == "house" ? Grid() : null,
                new FurnitureStorageCatalog(new[] { new FurnitureStorageDefinition("storage.crate", Crate, slots) }), new[] { Wood, Stone });

        private static RoomPieceSet Room(int slots = 2, RoomShareSettings settings = null)
            => new RoomPieceSet(settings ?? new RoomShareSettings(new[] { RoomHouse.PieceName }), RoomPieceCatalog.CreateDefault(Rules(slots)));

        private static RoomAnswer Apply(RoomPieceSet room, string player, RoomRequestSaveData request)
        {
            var answers = new List<RoomAnswer>();
            room.Apply(player, new[] { request }, 1, answers, null);
            return answers[0];
        }

        private static RoomRequestSaveData Req(string piece, string id, string kind, int amount, params string[] ids)
            => new RoomRequestSaveData { Piece = piece, RequestId = id, Kind = kind, Amount = amount, Ids = ids };

        private static string PlaceCrate(RoomPieceSet room, int x = 2, int y = 2, string furniture = "crate")
        {
            Assert.That(Apply(room, "ana", Req(RoomHouse.PieceName, "place" + x + y, RoomHouse.Place, 0, "house", furniture, x.ToString(), y.ToString(), "0")).Outcome,
                Is.EqualTo(RoomOutcomes.Applied));
            room.TryGet(RoomHouse.PieceName, out var house);
            return ((RoomHouse)house).Placed.Last().InstanceId;
        }

        private static RoomChests Chests(RoomPieceSet room)
        {
            Assert.That(room.TryGet(RoomChests.PieceName, out var piece), Is.True, "집을 같이 쓰면 상자도 같이 쓴다");
            return (RoomChests)piece;
        }

        private static RoomAnswer Put(RoomPieceSet room, string player, string id, string chest, ItemDefinition item, int count)
            => Apply(room, player, Req(RoomChests.PieceName, id, RoomChests.Put, count, chest, item.Id));

        private static RoomAnswer Take(RoomPieceSet room, string player, string id, string chest, ItemDefinition item, int count)
            => Apply(room, player, Req(RoomChests.PieceName, id, RoomChests.Take, count, chest, item.Id));

        private static RoomAnswer Use(RoomPieceSet room, string player, string id, string hold)
            => Apply(room, player, Req(RoomChests.PieceName, id, RoomChests.Use, 0, hold));

        private static RoomAnswer Release(RoomPieceSet room, string player, string id, string hold)
            => Apply(room, player, Req(RoomChests.PieceName, id, RoomChests.Release, 0, hold));

        [Test]
        public void CHEST_ROOM_001_ChestsFollowTheSharedHouse_AndAreNotChosenSeparately()
        {
            var catalog = RoomPieceCatalog.CreateDefault(Rules());
            Assert.That(catalog.Names, Does.Not.Contain(RoomChests.PieceName), "방 설정에서 고르지 않는다");
            Assert.That(catalog.Knows(RoomChests.PieceName), Is.False);
            Assert.That(Room().TryGet(RoomChests.PieceName, out _), Is.True);
            Assert.That(Room(settings: new RoomShareSettings(new[] { RoomWallet.PieceName })).TryGet(RoomChests.PieceName, out _), Is.False,
                "집을 각자 쓰면 각자의 상자");
            var shares = new RoomShares(new RoomShareSettings(new[] { RoomHouse.PieceName }), catalog);
            Assert.That(shares.Find<RoomChestsClient>(), Is.Not.Null);
        }

        [Test]
        public void CHEST_ROOM_002_PutThenUse_ItemsEnterTheChest_OnlyAfterUse()
        {
            var room = Room();
            string chest = PlaceCrate(room);
            Assert.That(Put(room, "ana", "h1", chest, Wood, 5).Outcome, Is.EqualTo(RoomOutcomes.Applied));
            Assert.That(Chests(room).CountOf(chest, "Wood"), Is.EqualTo(0), "잡기만 했다");
            Assert.That(Use(room, "ana", "u1", "h1").Outcome, Is.EqualTo(RoomOutcomes.Applied));
            Assert.That(Chests(room).CountOf(chest, "Wood"), Is.EqualTo(5));
            var state = Chests(room).State();
            Assert.That(RoomChestLine.TryDecode(state.Ids.Single(), out var c, out var item, out var count), Is.True);
            Assert.That((c, item, count), Is.EqualTo((chest, "Wood", 5)));
        }

        [Test]
        public void CHEST_ROOM_003_OnePutsAnotherTakes()
        {
            var room = Room();
            string chest = PlaceCrate(room);
            Put(room, "ana", "h1", chest, Wood, 5);
            Use(room, "ana", "u1", "h1");
            Assert.That(Take(room, "bo", "t1", chest, Wood, 3).Outcome, Is.EqualTo(RoomOutcomes.Applied));
            Assert.That(Chests(room).CountOf(chest, "Wood"), Is.EqualTo(2), "잡힌 물건은 상자에서 빠진다");
            Assert.That(Use(room, "bo", "u2", "t1").Outcome, Is.EqualTo(RoomOutcomes.Applied));
            Assert.That(Chests(room).CountOf(chest, "Wood"), Is.EqualTo(2));
            Assert.That(Chests(room).HeldCount, Is.EqualTo(0));
        }

        [Test]
        public void CHEST_ROOM_004_TwoTakeTheSameItemsAtOnce_OnlyTheFirstWins()
        {
            var room = Room();
            string chest = PlaceCrate(room);
            Put(room, "ana", "h1", chest, Wood, 4);
            Use(room, "ana", "u1", "h1");
            var answers = new List<RoomAnswer>();
            room.Apply("ana", new[] { Req(RoomChests.PieceName, "ta", RoomChests.Take, 4, chest, "Wood") }, 1, answers, null);
            room.Apply("bo", new[] { Req(RoomChests.PieceName, "tb", RoomChests.Take, 4, chest, "Wood") }, 1, answers, null);
            Assert.That(answers.Select(a => a.Outcome), Is.EqualTo(new[] { RoomOutcomes.Applied, RoomOutcomes.Refused }));
        }

        [Test]
        public void CHEST_ROOM_005_NoRoom_CountsHeldSpace_AndRefuses()
        {
            var room = Room(slots: 1);
            string chest = PlaceCrate(room);
            Assert.That(Put(room, "ana", "h1", chest, Wood, 8).Outcome, Is.EqualTo(RoomOutcomes.Applied));
            Assert.That(Put(room, "bo", "h2", chest, Wood, 3).Outcome, Is.EqualTo(RoomOutcomes.Refused), "잡힌 자리도 센다(10칸 쌓기에 8+3)");
            Assert.That(Put(room, "bo", "h3", chest, Stone, 1).Outcome, Is.EqualTo(RoomOutcomes.Refused), "칸이 하나뿐");
            Assert.That(Put(room, "bo", "h4", chest, Wood, 2).Outcome, Is.EqualTo(RoomOutcomes.Applied));
        }

        [Test]
        public void CHEST_ROOM_006_ReleasedTake_ReturnsItems_EvenIfOthersFilledTheChest()
        {
            var room = Room(slots: 1);
            string chest = PlaceCrate(room);
            Put(room, "ana", "h1", chest, Wood, 10);
            Use(room, "ana", "u1", "h1");
            Assert.That(Take(room, "bo", "t1", chest, Wood, 10).Outcome, Is.EqualTo(RoomOutcomes.Applied));
            Assert.That(Put(room, "ana", "h2", chest, Stone, 1).Outcome, Is.EqualTo(RoomOutcomes.Refused), "꺼내는 중인 물건 자리는 비지 않는다");
            Assert.That(Release(room, "bo", "r1", "t1").Outcome, Is.EqualTo(RoomOutcomes.Applied));
            Assert.That(Chests(room).CountOf(chest, "Wood"), Is.EqualTo(10), "가방이 받지 못해 돌아왔다");
        }

        [Test]
        public void CHEST_ROOM_007_SameRequestTwice_AppliesOnce_AndOthersCannotUseMyHold()
        {
            var room = Room();
            string chest = PlaceCrate(room);
            Put(room, "ana", "h1", chest, Wood, 3);
            Use(room, "ana", "u1", "h1");
            Assert.That(Use(room, "ana", "u1", "h1").Outcome, Is.EqualTo(RoomOutcomes.Applied), "장부가 같은 답을 준다");
            Assert.That(Chests(room).CountOf(chest, "Wood"), Is.EqualTo(3), "두 번 들어가지 않는다");

            Take(room, "ana", "t1", chest, Wood, 1);
            Assert.That(Use(room, "bo", "u2", "t1").Outcome, Is.EqualTo(RoomOutcomes.Invalid), "남이 잡은 것");
            Assert.That(Release(room, "ana", "u3", "missing").Outcome, Is.EqualTo(RoomOutcomes.Invalid));
        }

        [Test]
        public void CHEST_ROOM_008_AChestWithItemsOrHolds_CannotBeRemoved_OrCleared()
        {
            var room = Room();
            string chest = PlaceCrate(room);
            string chair = PlaceCrate(room, 3, 3, "chair");
            Put(room, "ana", "h1", chest, Wood, 1);
            Assert.That(Apply(room, "bo", Req(RoomHouse.PieceName, "rm1", RoomHouse.Remove, 0, chest)).Outcome, Is.EqualTo(RoomOutcomes.Refused), "잡힌 자리가 있다");
            Use(room, "ana", "u1", "h1");
            Assert.That(Apply(room, "bo", Req(RoomHouse.PieceName, "rm2", RoomHouse.Remove, 0, chest)).Outcome, Is.EqualTo(RoomOutcomes.Refused), "물건이 들었다");
            Apply(room, "bo", Req(RoomHouse.PieceName, "cl", RoomHouse.Clear, 0, "house"));
            room.TryGet(RoomHouse.PieceName, out var house);
            Assert.That(((RoomHouse)house).Placed.Select(p => p.InstanceId), Is.EqualTo(new[] { chest }), "의자는 치워지고 상자는 남는다");

            Take(room, "bo", "t1", chest, Wood, 1);
            Use(room, "bo", "u2", "t1");
            Assert.That(Apply(room, "bo", Req(RoomHouse.PieceName, "rm3", RoomHouse.Remove, 0, chest)).Outcome, Is.EqualTo(RoomOutcomes.Applied), "비우면 치운다");
            Assert.That(chair, Is.Not.Null);
        }

        [Test]
        public void CHEST_ROOM_009_NotAChest_UnknownItem_OrBadAmount_AreRefused()
        {
            var room = Room();
            string chair = PlaceCrate(room, 3, 3, "chair");
            Assert.That(Put(room, "ana", "h1", chair, Wood, 1).Outcome, Is.EqualTo(RoomOutcomes.Refused));
            Assert.That(Put(room, "ana", "h2", "999", Wood, 1).Outcome, Is.EqualTo(RoomOutcomes.Refused));
            string chest = PlaceCrate(room);
            Assert.That(Apply(room, "ana", Req(RoomChests.PieceName, "h3", RoomChests.Put, 1, chest, "item.unknown")).Outcome, Is.EqualTo(RoomOutcomes.Invalid));
            Assert.That(Put(room, "ana", "h4", chest, Wood, 0).Outcome, Is.EqualTo(RoomOutcomes.Invalid));
            Assert.That(Take(room, "ana", "h5", chest, Wood, 1).Outcome, Is.EqualTo(RoomOutcomes.Refused), "빈 상자");
        }

        [Test]
        public void CHEST_ROOM_010_RoomRestart_KeepsContentsHoldsAndLedger()
        {
            var store = new InMemorySaveStore();
            var room = Room();
            string chest = PlaceCrate(room);
            Put(room, "ana", "h1", chest, Wood, 6);
            Use(room, "ana", "u1", "h1");
            Take(room, "bo", "t1", chest, Wood, 2);
            room.Save(store, "slot");

            var again = Room();
            again.Load(store, "slot");
            Assert.That(Chests(again).CountOf(chest, "Wood"), Is.EqualTo(4));
            Assert.That(Use(again, "ana", "u1", "h1").Outcome, Is.EqualTo(RoomOutcomes.Applied), "장부도 이어진다");
            Assert.That(Chests(again).CountOf(chest, "Wood"), Is.EqualTo(4), "다시 들어가지 않는다");
            Assert.That(Release(again, "bo", "r1", "t1").Outcome, Is.EqualTo(RoomOutcomes.Applied), "잡은 것도 이어진다");
            Assert.That(Chests(again).CountOf(chest, "Wood"), Is.EqualTo(6));
        }

        [Test]
        public void CHEST_ROOM_011_ClientReadsTheStateLines_AndWritesHoldUseRelease()
        {
            var shares = new RoomShares(new RoomShareSettings(new[] { RoomHouse.PieceName }), RoomPieceCatalog.CreateDefault(), () => "id" + System.Guid.NewGuid().ToString("N"));
            var client = shares.Find<RoomChestsClient>();
            int changed = 0;
            client.Changed += () => changed++;
            shares.Receive(new RoomPieceState(RoomChests.PieceName, 2, new[] { RoomChestLine.Encode("7", "Wood", 10), RoomChestLine.Encode("7", "Wood", 3) }));
            Assert.That(client.ContentsOf("7").Sum(s => s.Count), Is.EqualTo(13));
            Assert.That(client.ContentsOf("8"), Is.Empty);
            Assert.That(changed, Is.EqualTo(1));

            string hold = client.RequestTake("7", "Wood", 2);
            Assert.That(client.HasPending, Is.True);
            RoomRequestSaveData answered = null;
            bool applied = false;
            client.Answered += (r, ok) => { answered = r; applied = ok; };
            shares.Receive(new RoomAnswer(RoomChests.PieceName, hold, RoomOutcomes.Applied, 0));
            Assert.That(answered.RequestId, Is.EqualTo(hold));
            Assert.That(applied, Is.True);
            Assert.That(client.HasPending, Is.False);
            client.AddUse(hold);
            Assert.That(shares.Outbox.Requests.Single().Kind, Is.EqualTo(RoomChests.Use));
        }

        [Test]
        public void CHEST_ROOM_012_ItemTotal_IsConserved_UnderRandomInterleavedRequests()
        {
            var rng = new System.Random(11);
            var room = Room(slots: 2);
            string chest = PlaceCrate(room);
            // 둘의 가방(아이템 id → 수). 허락이 오면 가방을 바꾸고 쓰기, 가방이 못 하면 풀기(되돌리는 흐름 없음).
            var bags = new Dictionary<string, Dictionary<string, int>>
            {
                ["ana"] = new Dictionary<string, int> { ["Wood"] = 15, ["Stone"] = 5 },
                ["bo"] = new Dictionary<string, int> { ["Wood"] = 5, ["Stone"] = 12 },
            };
            int n = 0;
            for (int i = 0; i < 400; i++)
            {
                string who = rng.Next(2) == 0 ? "ana" : "bo";
                string item = rng.Next(2) == 0 ? "Wood" : "Stone";
                int count = 1 + rng.Next(6);
                bool put = rng.Next(2) == 0;
                string hold = "h" + (n++);
                var answer = Apply(room, who, Req(RoomChests.PieceName, hold, put ? RoomChests.Put : RoomChests.Take, count, chest, item));
                if (!answer.Accepted) continue;
                bool bagOk = put ? bags[who][item] >= count : rng.Next(4) != 0; // 꺼낼 때 가끔 가방이 꽉 찼다고 본다
                if (bagOk) bags[who][item] += put ? -count : count;
                Apply(room, who, Req(RoomChests.PieceName, "x" + (n++), bagOk ? RoomChests.Use : RoomChests.Release, 0, hold));
                foreach (var id in new[] { "Wood", "Stone" })
                    Assert.That(bags["ana"][id] + bags["bo"][id] + Chests(room).CountOf(chest, id), Is.EqualTo(id == "Wood" ? 20 : 17), "가방 + 상자의 총량은 그대로");
            }

            Assert.That(Chests(room).HeldCount, Is.EqualTo(0));
        }
    }
}
