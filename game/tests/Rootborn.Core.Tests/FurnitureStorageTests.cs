using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Inventory;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;
using Rootborn.Core.Save;

namespace Rootborn.Core.Tests
{
    /// <summary>
    /// 상자(보관 칸이 있는 가구): 가방 ↔ 상자 옮기기는 전부 아니면 전무, 같은 요청 id는 한 번만, 저장·다시 읽기 뒤에도 한 번만.
    /// 돈(지갑으로 가는 통화 아이템)은 넣지 못한다. 내용물이 든 상자는 치울 수 없고, 옮기면 내용물이 따라간다.
    /// </summary>
    public sealed class FurnitureStorageTests
    {
        private static GridPoint P(int x, int y) => new GridPoint(x, y);

        private static readonly ItemDefinition Wood = new ItemDefinition("Wood", maxStack: 10);
        private static readonly ItemDefinition Stone = new ItemDefinition("Stone", maxStack: 10);
        private static readonly ItemDefinition Coin = new ItemDefinition("item.allowance", maxStack: 9999, category: ItemCategory.Misc);

        private static ItemDefinition Find(string id) => id == Wood.Id ? Wood : id == Stone.Id ? Stone : id == Coin.Id ? Coin : null;

        private static FurnitureDefinition Furniture(string id)
            => new FurnitureDefinition(id, id, null, "Storage", new[] { "house" }, new[] { new FurnitureTilePart(P(0, 0), "s.png", "t") }, null, true,
                FurniturePlacementPreference.Any);

        private static readonly FurnitureDefinition Crate = Furniture("crate");
        private static readonly FurnitureDefinition Chair = Furniture("chair");

        private static StorageContents Chest(int slots, params (ItemDefinition item, int count)[] items)
        {
            var data = new FurnitureStorageSaveData
            {
                Items = items.Select(x => new PlayerInventoryItemSaveData { ItemId = x.item.Id, Count = x.count }).ToArray(),
            };
            return StorageContents.From(data, slots, Find);
        }

        private static ItemInventory Bag(params (ItemDefinition item, int count)[] items)
        {
            var bag = new ItemInventory(new PersonalWallet(), Coin);
            foreach (var (item, count) in items) bag.Add(item, count);
            return bag;
        }

        private static FurnitureStorageSaveData Move(ItemInventory bag, StorageContents chest, StorageDirection direction, ItemDefinition item, int count,
            string requestId, out StorageTransferResult result)
        {
            FurnitureStorageSaveData committed = null;
            result = StorageTransfer.TryMove(bag, chest, direction, item, count, requestId, next => committed = next);
            return committed;
        }

        // ---------- 정의 ----------

        [Test]
        public void STORAGE_001_StorageIsData_AFurnitureWithStorageSlotsIsAChest()
        {
            var catalog = new FurnitureStorageCatalog(new[] { new FurnitureStorageDefinition("storage.crate", Crate, 64) });
            Assert.That(catalog.SlotsOf("crate"), Is.EqualTo(64));
            Assert.That(catalog.SlotsOf("chair"), Is.EqualTo(0));
            Assert.That(catalog.IsStorage(Crate), Is.True);
            Assert.That(catalog.IsStorage(Chair), Is.False);
            Assert.Throws<ArgumentNullException>(() => new FurnitureStorageDefinition("x", null, 4));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FurnitureStorageDefinition("x", Crate, 0));
        }

        // ---------- 옮기기 다섯 경우 ----------

        [Test]
        public void STORAGE_002_Put_MovesFromBagToChest_Together()
        {
            var bag = Bag((Wood, 7));
            var chest = Chest(4);
            var next = Move(bag, chest, StorageDirection.Put, Wood, 5, "r1", out var result);

            Assert.That(result, Is.EqualTo(StorageTransferResult.Moved));
            Assert.That(bag.CountOf(Wood), Is.EqualTo(2));
            var after = StorageContents.From(next, 4, Find);
            Assert.That(after.CountOf(Wood), Is.EqualTo(5));
            Assert.That(after.Knows("r1"), Is.True);
        }

        [Test]
        public void STORAGE_003_Take_MovesFromChestToBag_Together()
        {
            var bag = Bag();
            var chest = Chest(4, (Stone, 10), (Stone, 3));
            var next = Move(bag, chest, StorageDirection.Take, Stone, 12, "r1", out var result);

            Assert.That(result, Is.EqualTo(StorageTransferResult.Moved));
            Assert.That(bag.CountOf(Stone), Is.EqualTo(12));
            Assert.That(StorageContents.From(next, 4, Find).CountOf(Stone), Is.EqualTo(1));
        }

        [Test]
        public void STORAGE_004_ChestFull_Put_ChangesNothing()
        {
            var bag = Bag((Wood, 5), (Stone, 5));
            var chest = Chest(1, (Wood, 8));
            var next = Move(bag, chest, StorageDirection.Put, Stone, 1, "r1", out var result);

            Assert.That(result, Is.EqualTo(StorageTransferResult.NoRoom));
            Assert.That(next, Is.Null, "상자 쪽을 바꾸지 않는다");
            Assert.That(bag.CountOf(Stone), Is.EqualTo(5), "가방도 그대로");
            Assert.That(chest.CountOf(Wood), Is.EqualTo(8));
        }

        [Test]
        public void STORAGE_005_PartOnlyFits_ChangesNothing()
        {
            // 상자에 나무 8(쌓기 10), 칸 1개: 3개 넣기는 2개만 들어간다 → 전부 거절.
            var bag = Bag((Wood, 3));
            var chest = Chest(1, (Wood, 8));
            Assert.That(Move(bag, chest, StorageDirection.Put, Wood, 3, "r1", out var put), Is.Null);
            Assert.That(put, Is.EqualTo(StorageTransferResult.NoRoom));
            Assert.That(bag.CountOf(Wood), Is.EqualTo(3));

            // 가방이 꽉 찼을 때 꺼내기: 들어가는 만큼만 꺼내지 않는다.
            var full = Bag();
            for (int i = 0; i < ItemInventory.MaxSlots; i++) full.Add(new ItemDefinition("filler" + i, maxStack: 1), 1);
            var stones = Chest(4, (Stone, 4));
            Assert.That(Move(full, stones, StorageDirection.Take, Stone, 2, "r2", out var take), Is.Null);
            Assert.That(take, Is.EqualTo(StorageTransferResult.NoRoom));
            Assert.That(full.CountOf(Stone), Is.EqualTo(0));
            Assert.That(stones.CountOf(Stone), Is.EqualTo(4));
        }

        [Test]
        public void STORAGE_006_NotEnough_ChangesNothing()
        {
            var bag = Bag((Wood, 2));
            Assert.That(Move(bag, Chest(4), StorageDirection.Put, Wood, 3, "r1", out var put), Is.Null);
            Assert.That(put, Is.EqualTo(StorageTransferResult.NotEnough));
            Assert.That(bag.CountOf(Wood), Is.EqualTo(2));

            Assert.That(Move(bag, Chest(4, (Stone, 1)), StorageDirection.Take, Stone, 2, "r2", out var take), Is.Null);
            Assert.That(take, Is.EqualTo(StorageTransferResult.NotEnough));
            Assert.That(bag.CountOf(Stone), Is.EqualTo(0));
        }

        [Test]
        public void STORAGE_007_SameRequestTwice_MovesOnce()
        {
            var bag = Bag((Wood, 6));
            var chest = Chest(4);
            var next = Move(bag, chest, StorageDirection.Put, Wood, 2, "r1", out _);
            var again = StorageContents.From(next, 4, Find);
            Assert.That(Move(bag, again, StorageDirection.Put, Wood, 2, "r1", out var second), Is.Null);
            Assert.That(second, Is.EqualTo(StorageTransferResult.AlreadyDone));
            Assert.That(bag.CountOf(Wood), Is.EqualTo(4));
            Assert.That(again.CountOf(Wood), Is.EqualTo(2));
        }

        [Test]
        public void STORAGE_008_SameRequestAfterSaveAndReload_MovesOnce()
        {
            var store = new InMemorySaveStore();
            var bag = Bag((Wood, 6));
            var next = Move(bag, Chest(4), StorageDirection.Put, Wood, 2, "r1", out _);
            store.Write("slot", "chest.json", next);
            store.Write("slot", "bag.json", bag.ToSaveData(null));

            Assert.That(store.TryRead("slot", "chest.json", out FurnitureStorageSaveData chestData), Is.True);
            Assert.That(store.TryRead("slot", "bag.json", out PlayerInventorySaveData bagData), Is.True);
            var reloadedBag = Bag();
            reloadedBag.LoadFromSaveData(bagData, Find);
            var reloadedChest = StorageContents.From(chestData, 4, Find);

            Assert.That(Move(reloadedBag, reloadedChest, StorageDirection.Put, Wood, 2, "r1", out var result), Is.Null);
            Assert.That(result, Is.EqualTo(StorageTransferResult.AlreadyDone));
            Assert.That(reloadedBag.CountOf(Wood), Is.EqualTo(4));
            Assert.That(reloadedChest.CountOf(Wood), Is.EqualTo(2));
        }

        [Test]
        public void STORAGE_009_Money_CannotBePut()
        {
            var bag = Bag((Coin, 50));
            Assert.That(bag.CountOf(Coin), Is.EqualTo(50), "돈은 지갑에 있다");
            Assert.That(Move(bag, Chest(4), StorageDirection.Put, Coin, 10, "r1", out var result), Is.Null);
            Assert.That(result, Is.EqualTo(StorageTransferResult.Currency));
            Assert.That(bag.CountOf(Coin), Is.EqualTo(50));
        }

        [Test]
        public void STORAGE_010_InvalidAmounts_ChangeNothing()
        {
            var bag = Bag((Wood, 3));
            Assert.That(StorageTransfer.TryMove(bag, Chest(4), StorageDirection.Put, Wood, 0, "r1", _ => Assert.Fail()), Is.EqualTo(StorageTransferResult.Invalid));
            Assert.That(StorageTransfer.TryMove(bag, Chest(4), StorageDirection.Put, null, 1, "r2", _ => Assert.Fail()), Is.EqualTo(StorageTransferResult.Invalid));
            Assert.That(StorageTransfer.TryMove(bag, Chest(4), StorageDirection.Put, Wood, 1, "", _ => Assert.Fail()), Is.EqualTo(StorageTransferResult.Invalid));
            Assert.That(bag.CountOf(Wood), Is.EqualTo(3));
        }

        [Test]
        public void STORAGE_011_TotalItemsAreConserved_OverManyMoves()
        {
            var rng = new Random(7);
            var bag = Bag((Wood, 25), (Stone, 17));
            var chest = Chest(3);
            for (int i = 0; i < 300; i++)
            {
                var item = rng.Next(2) == 0 ? Wood : Stone;
                var dir = rng.Next(2) == 0 ? StorageDirection.Put : StorageDirection.Take;
                var next = Move(bag, chest, dir, item, 1 + rng.Next(12), "r" + i, out _);
                if (next != null) chest = StorageContents.From(next, 3, Find);
                Assert.That(bag.CountOf(Wood) + chest.CountOf(Wood), Is.EqualTo(25));
                Assert.That(bag.CountOf(Stone) + chest.CountOf(Stone), Is.EqualTo(17));
                Assert.That(chest.UsedSlots, Is.LessThanOrEqualTo(3));
            }
        }

        [Test]
        public void STORAGE_012_UnknownItemsInTheSave_AreKept_AndTakeUpASlot()
        {
            var data = new FurnitureStorageSaveData { Items = new[] { new PlayerInventoryItemSaveData { ItemId = "item.gone", Count = 3 } } };
            var chest = StorageContents.From(data, 1, Find);
            Assert.That(chest.IsEmpty, Is.False);
            Assert.That(Move(Bag((Wood, 1)), chest, StorageDirection.Put, Wood, 1, "r1", out var result), Is.Null);
            Assert.That(result, Is.EqualTo(StorageTransferResult.NoRoom));
            var saved = chest.ToSaveData();
            Assert.That(saved.Items.Single().ItemId, Is.EqualTo("item.gone"));
            Assert.That(saved.Items.Single().Count, Is.EqualTo(3));
        }

        [Test]
        public void STORAGE_013_RecentRequestIds_AreBounded()
        {
            var bag = Bag((Wood, 10));
            var chest = Chest(4);
            for (int i = 0; i < StorageTransfer.RecentRequestCapacity + 10; i++)
            {
                var next = Move(bag, chest, i % 2 == 0 ? StorageDirection.Put : StorageDirection.Take, Wood, 1, "r" + i, out _);
                chest = StorageContents.From(next, 4, Find);
            }

            Assert.That(chest.ToSaveData().Requests.Length, Is.EqualTo(StorageTransfer.RecentRequestCapacity));
            Assert.That(chest.Knows("r" + (StorageTransfer.RecentRequestCapacity + 9)), Is.True);
        }

        // ---------- 놓인 상자: 치우기, 옮기기, 저장 ----------

        private static PlacementGrid Grid()
        {
            var floor = new List<GridPoint>();
            for (int x = 0; x < 5; x++) for (int y = 0; y < 5; y++) floor.Add(P(x, y));
            return new PlacementGrid(floor, null, P(0, 0), new[] { P(0, 0) });
        }

        private static HouseFurnitureLayout Layout(Func<FurniturePlacementSaveData[], bool> commit = null)
            => new HouseFurnitureLayout(new HousePlacementModel(Grid()), "house", commit);

        private static FurnitureStorageSaveData WithWood(int count)
            => new FurnitureStorageSaveData { Items = new[] { new PlayerInventoryItemSaveData { ItemId = Wood.Id, Count = count } } };

        [Test]
        public void STORAGE_014_AChestWithItems_CannotBeRemoved_AnEmptyOneCan()
        {
            var layout = Layout();
            Assert.That(layout.TryPlace(Crate, P(2, 2), FurniturePlacementDirection.North), Is.True);
            string id = layout.Placed[0].InstanceId;
            Assert.That(layout.SetStorage(id, WithWood(3)), Is.True);
            Assert.That(layout.Placed[0].HoldsItems, Is.True);

            Assert.That(layout.Delete(id), Is.False, "내용물이 든 상자");
            Assert.That(layout.Placed.Count, Is.EqualTo(1));

            Assert.That(layout.SetStorage(id, new FurnitureStorageSaveData()), Is.True);
            Assert.That(layout.Delete(id), Is.True, "비운 상자");
        }

        [Test]
        public void STORAGE_015_ClearAll_KeepsChestsWithItems()
        {
            var layout = Layout();
            layout.TryPlace(Crate, P(2, 2), FurniturePlacementDirection.North);
            layout.TryPlace(Chair, P(3, 3), FurniturePlacementDirection.North);
            layout.TryPlace(Crate, P(1, 3), FurniturePlacementDirection.North);
            layout.SetStorage(layout.Placed[0].InstanceId, WithWood(1));

            Assert.That(layout.ClearUserFurniture(), Is.True);
            Assert.That(layout.Placed.Single().Anchor, Is.EqualTo(P(2, 2)), "물건이 든 상자만 남는다");
        }

        [Test]
        public void STORAGE_016_MovingAChest_KeepsItsItems_AndSavingKeepsThem()
        {
            FurniturePlacementSaveData[] last = null;
            var layout = Layout(records => { last = records; return true; });
            layout.TryPlace(Crate, P(2, 2), FurniturePlacementDirection.North);
            string id = layout.Placed[0].InstanceId;
            layout.SetStorage(id, WithWood(4));

            Assert.That(layout.TryMove(id, P(3, 1), FurniturePlacementDirection.East), Is.True);
            Assert.That(layout.StorageOf(id).Items.Single().Count, Is.EqualTo(4));
            Assert.That(last.Single().Storage.Items.Single().Count, Is.EqualTo(4), "옮긴 기록에 내용물이 있다");

            var reloaded = Layout();
            reloaded.Restore(layout.BuildSaveRecords(), new[] { Crate, Chair });
            Assert.That(reloaded.StorageOf(reloaded.Placed[0].InstanceId).Items.Single().Count, Is.EqualTo(4));
        }

        [Test]
        public void STORAGE_017_StorageOf_IsACopy_AndUnknownInstanceIsRefused()
        {
            var layout = Layout();
            layout.TryPlace(Crate, P(2, 2), FurniturePlacementDirection.North);
            string id = layout.Placed[0].InstanceId;
            layout.SetStorage(id, WithWood(2));
            var copy = layout.StorageOf(id);
            copy.Items[0].Count = 99;
            Assert.That(layout.StorageOf(id).Items[0].Count, Is.EqualTo(2));
            Assert.That(layout.SetStorage("nope", WithWood(1)), Is.False);
            Assert.That(layout.StorageOf("nope").Items, Is.Empty);
        }

        [Test]
        public void STORAGE_018_LayoutFileVersion_IsRaised_ForTheStorageField()
        {
            Assert.That(FurniturePlacementLayoutSaveData.KnownVersion, Is.EqualTo(2));
            var record = new FurniturePlacementSaveData { FurnitureId = "crate", Storage = WithWood(2) };
            var clone = record.Clone();
            clone.Storage.Items[0].Count = 5;
            Assert.That(record.Storage.Items[0].Count, Is.EqualTo(2), "깊은 사본");
            Assert.That(new FurniturePlacementSaveData().Clone().Storage, Is.Null, "상자가 아닌 기록은 비어 있다");
        }
    }
}
