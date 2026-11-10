using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Core.Inventory;

namespace Rootborn.Core.Tests
{
    // 옮겨 옴: Assets/Tests/EditMode/Careers/InventoryExchangeTests.cs, Assets/Tests/EditMode/InventoryEquipmentScenarioTests.cs (STK, DRG)
    public sealed class ItemInventoryTests
    {
        private static ItemDefinition Item(string id, int stack, ItemCategory category = ItemCategory.Resource) => new ItemDefinition(id, stack, category);

        [Test]
        public void INVENTORY_EXCHANGE_001_DuplicateCostsAreAggregatedBeforeAnyMutation()
        {
            var item = Item("currency", 99); var inventory = new ItemInventory(); inventory.Add(item, 9); bool committed = false;
            var costs = new[] { new InventoryGrant(item, 6), new InventoryGrant(item, 4) };
            Assert.That(inventory.CanExchange(costs, null), Is.False);
            Assert.That(inventory.TryExchange(costs, null, () => committed = true), Is.False);
            Assert.That(inventory.CountOf(item), Is.EqualTo(9)); Assert.That(committed, Is.False);
        }

        [Test]
        public void INVENTORY_EXCHANGE_002_MultiRewardCapacityFailureLeavesInventoryUnchanged()
        {
            var inventory = new ItemInventory(); for (int i = 0; i < 31; i++) inventory.Add(Item("occupied-" + i, 1));
            var a = Item("a", 1); var b = Item("b", 1); bool committed = false;
            Assert.That(inventory.TryExchange(null, new[] { new InventoryGrant(a, 1), new InventoryGrant(b, 1) }, () => committed = true), Is.False);
            Assert.That(inventory.Slots.Count, Is.EqualTo(31)); Assert.That(inventory.CountOf(a), Is.Zero); Assert.That(committed, Is.False);
        }

        [Test]
        public void INVENTORY_EXCHANGE_003_ConsumedStackFreesSpaceForReward()
        {
            var inventory = new ItemInventory(); var cost = Item("cost", 1); inventory.Add(cost);
            for (int i = 0; i < 31; i++) inventory.Add(Item("occupied-" + i, 1)); var reward = Item("reward", 1);
            Assert.That(inventory.TryExchange(new[] { new InventoryGrant(cost, 1) }, new[] { new InventoryGrant(reward, 1) }, null), Is.True);
            Assert.That(inventory.CountOf(cost), Is.Zero); Assert.That(inventory.CountOf(reward), Is.EqualTo(1)); Assert.That(inventory.Slots.Count, Is.EqualTo(32));
        }

        [Test]
        public void INVENTORY_EXCHANGE_004_ObserversSeeCommittedStateAndAllRewardsExactlyOnce()
        {
            var inventory = new ItemInventory(); var a = Item("a", 99); var b = Item("b", 99); bool committed = false; int notifications = 0;
            inventory.OnChanged += () => { notifications++; Assert.That(committed, Is.True); Assert.That(inventory.CountOf(a), Is.EqualTo(3)); Assert.That(inventory.CountOf(b), Is.EqualTo(4)); };
            Assert.That(inventory.TryExchange(null, new[] { new InventoryGrant(a, 3), new InventoryGrant(b, 4) }, () => committed = true), Is.True);
            Assert.That(notifications, Is.EqualTo(1));
        }

        [Test]
        public void INVENTORY_EXCHANGE_005_ObserverCannotReenterRewardTransaction()
        {
            var inventory = new ItemInventory(); var item = Item("a", 99); bool nested = true; int notifications = 0;
            inventory.OnChanged += () => { notifications++; if (notifications == 1) nested = inventory.TryExchange(null, new[] { new InventoryGrant(item, 10) }, null); };
            Assert.That(inventory.TryExchange(null, new[] { new InventoryGrant(item, 3) }, null), Is.True);
            Assert.That(nested, Is.False); Assert.That(inventory.CountOf(item), Is.EqualTo(3)); Assert.That(notifications, Is.EqualTo(1));
        }

        [Test]
        public void INVENTORY_EXCHANGE_006_InvalidAmountsFailBeforeCallingCommit()
        {
            var inventory = new ItemInventory(); var item = Item("a", 99); bool committed = false;
            Assert.That(inventory.TryExchange(new[] { new InventoryGrant(item, -1) }, null, () => committed = true), Is.False);
            Assert.That(inventory.TryExchange(null, new[] { new InventoryGrant(null, 2) }, () => committed = true), Is.False);
            Assert.That(committed, Is.False); Assert.That(inventory.Slots.Count, Is.Zero);
        }

        [Test]
        public void INVENTORY_EXCHANGE_007_AddAllSharesTheAtomicNotificationContract()
        {
            var inventory = new ItemInventory(); var item = Item("a", 99); int changes = 0; inventory.OnChanged += () => changes++;
            Assert.That(inventory.TryAddAll(new[] { new InventoryGrant(item, 2), new InventoryGrant(item, 3) }), Is.True);
            Assert.That(inventory.CountOf(item), Is.EqualTo(5)); Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void INVENTORY_ADD_001_EmptySlotCreatedByDragIsReusedInsteadOfLosingItem()
        {
            var inventory = new ItemInventory(); var moved = Item("moved", 1); var added = Item("added", 1); inventory.Add(moved);
            Assert.That(inventory.TryMoveSlot(0, ItemInventory.MaxSlots - 1), Is.True);
            Assert.That(inventory.CanAdd(added), Is.True);
            inventory.Add(added);
            Assert.That(inventory.CountOf(added), Is.EqualTo(1)); Assert.That(inventory.CountOf(moved), Is.EqualTo(1)); Assert.That(inventory.Slots.Count, Is.EqualTo(ItemInventory.MaxSlots));
        }

        [Test]
        public void INVENTORY_ADD_002_CanAddTrueAlwaysMeansFullAmountIsStored()
        {
            var inventory = new ItemInventory(); var moved = Item("moved", 1); var added = Item("added", 2); inventory.Add(moved);
            Assert.That(inventory.TryMoveSlot(0, ItemInventory.MaxSlots - 1), Is.True);
            const int amount = 2 * (ItemInventory.MaxSlots - 1);
            Assert.That(inventory.CanAdd(added, amount), Is.True); Assert.That(inventory.CanAdd(added, amount + 1), Is.False);
            inventory.Add(added, amount);
            Assert.That(inventory.CountOf(added), Is.EqualTo(amount));
        }

        [Test]
        public void STK_002_AddingStackableItem_IncrementsExistingSlotCount()
        {
            var wood = Item("Wood", 99); var inventory = new ItemInventory();
            inventory.Add(wood, 3); inventory.Add(wood, 4);
            Assert.That(inventory.Slots.Count, Is.EqualTo(1)); Assert.That(inventory.Slots[0].Count, Is.EqualTo(7));
        }

        [Test]
        public void STK_003_AddingBeyondMaxStack_SplitsIntoNewSlot()
        {
            var wood = Item("Wood", 5); var inventory = new ItemInventory();
            inventory.Add(wood, 7);
            Assert.That(inventory.Slots.Count, Is.EqualTo(2)); Assert.That(inventory.Slots[0].Count, Is.EqualTo(5)); Assert.That(inventory.Slots[1].Count, Is.EqualTo(2));
        }

        [Test]
        public void STK_004_EquipmentItems_DoNotMergeLikeMaterials()
        {
            var axe = Item("StoneAxe", 1, ItemCategory.Tool); var inventory = new ItemInventory();
            inventory.Add(axe, 2);
            Assert.That(inventory.Slots.Count, Is.EqualTo(2)); Assert.That(inventory.Slots[0].Count, Is.EqualTo(1)); Assert.That(inventory.Slots[1].Count, Is.EqualTo(1));
        }

        [Test]
        public void DRG_001_MoveSlot_ToEmptyLogicalSlot_MovesItemWithoutDuplication()
        {
            var wood = Item("Wood", 99); var inventory = new ItemInventory(); inventory.Add(wood, 4);
            Assert.That(inventory.TryMoveSlot(0, 3), Is.True);
            Assert.That(inventory.Slots[0].Item, Is.Null); Assert.That(inventory.Slots[0].Count, Is.Zero);
            Assert.That(inventory.Slots[3].Item, Is.SameAs(wood)); Assert.That(inventory.Slots[3].Count, Is.EqualTo(4)); Assert.That(inventory.CountOf(wood), Is.EqualTo(4));
        }

        [Test]
        public void DRG_002_MoveStackableSlot_ToSameItemSlot_MergesAvailableCountOnly()
        {
            var wood = Item("Wood", 5); var inventory = new ItemInventory(); inventory.Add(wood, 5); inventory.Add(wood, 3);
            Assert.That(inventory.TryMoveSlot(1, 0), Is.False, "A full target stack cannot receive more items.");
            Assert.That(inventory.Slots[0].Count, Is.EqualTo(5)); Assert.That(inventory.Slots[1].Count, Is.EqualTo(3));
        }

        [Test]
        public void DRG_003_MoveSlot_ToIncompatibleOccupiedSlot_ReturnsFalseAndRestoresSource()
        {
            var wood = Item("Wood", 99); var axe = Item("StoneAxe", 1, ItemCategory.Tool); var inventory = new ItemInventory();
            inventory.Add(wood, 2); inventory.Add(axe, 1);
            Assert.That(inventory.TryMoveSlot(0, 1), Is.False);
            Assert.That(inventory.Slots[0].Item, Is.SameAs(wood)); Assert.That(inventory.Slots[0].Count, Is.EqualTo(2));
            Assert.That(inventory.Slots[1].Item, Is.SameAs(axe)); Assert.That(inventory.Slots[1].Count, Is.EqualTo(1));
        }

        [Test]
        public void DRG_004_MoveStackableSlot_OverflowRemainsInSourceSlot()
        {
            var stone = Item("Stone", 5); var inventory = new ItemInventory();
            inventory.Add(stone, 5); inventory.Add(Item("StoneAxe", 1, ItemCategory.Tool), 1); inventory.Add(stone, 4);
            Assert.That(inventory.TryMoveSlot(0, 2), Is.True);
            Assert.That(inventory.Slots[2].Count, Is.EqualTo(5)); Assert.That(inventory.Slots[0].Item, Is.SameAs(stone));
            Assert.That(inventory.Slots[0].Count, Is.EqualTo(4)); Assert.That(inventory.CountOf(stone), Is.EqualTo(9));
        }

        [Test]
        public void ItemDefinition_MaxStackIsAtLeastOne_AndDefaultsTo99()
        {
            Assert.That(new ItemDefinition("item.zero", 0).MaxStack, Is.EqualTo(1));
            Assert.That(new ItemDefinition("item.default").MaxStack, Is.EqualTo(99));
            Assert.That(new ItemDefinition(null).Id, Is.EqualTo(string.Empty));
        }

        [Test]
        public void SaveData_RoundTripsItemsByIdAndSkipsEmptySlotsAndUnknownIds()
        {
            var wood = Item("item.wood", 5); var axe = Item("item.axe", 1, ItemCategory.Tool); var noId = Item("", 99);
            var inventory = new ItemInventory();
            inventory.Add(wood, 7); inventory.Add(axe, 1); inventory.Add(noId, 2);
            Assert.That(inventory.TryMoveSlot(1, 5), Is.True, "leaves an empty slot behind");

            var saved = inventory.ToSaveData("item.axe");
            Assert.That(saved.EquippedToolItemId, Is.EqualTo("item.axe"));
            Assert.That(saved.Items.Length, Is.EqualTo(3), "empty slots and items without an id are not saved");

            var byId = new Dictionary<string, ItemDefinition> { { wood.Id, wood }, { axe.Id, axe } };
            var loaded = new ItemInventory();
            loaded.Add(Item("item.stale", 99), 4);
            loaded.LoadFromSaveData(saved, id => byId.TryGetValue(id ?? string.Empty, out var item) ? item : null);
            Assert.That(loaded.CountOf(wood), Is.EqualTo(7));
            Assert.That(loaded.CountOf(axe), Is.EqualTo(1));
            Assert.That(loaded.Slots.Count, Is.EqualTo(3), "loading replaces what was there");

            saved.Items = new[] { new PlayerInventoryItemSaveData { ItemId = "item.missing", Count = 3 }, new PlayerInventoryItemSaveData { ItemId = "item.wood", Count = 2 } };
            loaded.LoadFromSaveData(saved, id => byId.TryGetValue(id ?? string.Empty, out var item) ? item : null);
            Assert.That(loaded.CountOf(wood), Is.EqualTo(2));
            Assert.That(loaded.Slots.Count, Is.EqualTo(1));
        }

        [Test]
        public void LoadFromNullSaveData_ChangesNothing()
        {
            var wood = Item("item.wood", 99); var inventory = new ItemInventory(); inventory.Add(wood, 3);
            inventory.LoadFromSaveData(null, _ => wood);
            Assert.That(inventory.CountOf(wood), Is.EqualTo(3));
        }

        [Test]
        public void RewardInventory_ReceivesAllGrantsOrNone()
        {
            var inventory = new ItemInventory(); for (int i = 0; i < ItemInventory.MaxSlots - 1; i++) inventory.Add(Item("filler-" + i, 1));
            var a = Item("a", 1); var b = Item("b", 1);
            var grants = new List<ItemAmount<ItemDefinition>> { new ItemAmount<ItemDefinition>(a, 1), new ItemAmount<ItemDefinition>(b, 1) };
            Assert.That(inventory.CanReceive(grants), Is.False);
            Assert.That(inventory.TryReceive(grants, () => Assert.Fail("must not commit")), Is.False);
            Assert.That(inventory.CountOf(a) + inventory.CountOf(b), Is.Zero);
            grants.RemoveAt(1);
            bool committed = false;
            Assert.That(inventory.TryReceive(grants, () => committed = true), Is.True);
            Assert.That(committed, Is.True); Assert.That(inventory.CountOf(a), Is.EqualTo(1));
        }
    }
}
