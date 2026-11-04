using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Quests;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Careers
{
    public sealed class InventoryExchangeTests
    {
        private readonly List<ItemDefinition> _items = new List<ItemDefinition>();
        [TearDown] public void TearDown() { foreach (var item in _items) UnityEngine.Object.DestroyImmediate(item); _items.Clear(); }

        [Test] public void INVENTORY_EXCHANGE_001_DuplicateCostsAreAggregatedBeforeAnyMutation()
        {
            var item = Item("currency", 99); var inventory = new Inventory(); inventory.Add(item, 9); bool committed = false;
            var costs = new[] { new InventoryGrant(item, 6), new InventoryGrant(item, 4) };
            Assert.That(CanExchange(inventory, costs, null), Is.False);
            Assert.That(Exchange(inventory, costs, null, () => committed = true), Is.False);
            Assert.That(inventory.CountOf(item), Is.EqualTo(9)); Assert.That(committed, Is.False);
        }

        [Test] public void INVENTORY_EXCHANGE_002_MultiRewardCapacityFailureLeavesInventoryUnchanged()
        {
            var inventory = new Inventory(); for (int i = 0; i < 31; i++) inventory.Add(Item("occupied-" + i, 1));
            var a = Item("a", 1); var b = Item("b", 1); bool committed = false;
            Assert.That(Exchange(inventory, null, new[] { new InventoryGrant(a, 1), new InventoryGrant(b, 1) }, () => committed = true), Is.False);
            Assert.That(inventory.Slots.Count, Is.EqualTo(31)); Assert.That(inventory.CountOf(a), Is.Zero); Assert.That(committed, Is.False);
        }

        [Test] public void INVENTORY_EXCHANGE_003_ConsumedStackFreesSpaceForReward()
        {
            var inventory = new Inventory(); var cost = Item("cost", 1); inventory.Add(cost);
            for (int i = 0; i < 31; i++) inventory.Add(Item("occupied-" + i, 1)); var reward = Item("reward", 1);
            Assert.That(Exchange(inventory, new[] { new InventoryGrant(cost, 1) }, new[] { new InventoryGrant(reward, 1) }, null), Is.True);
            Assert.That(inventory.CountOf(cost), Is.Zero); Assert.That(inventory.CountOf(reward), Is.EqualTo(1)); Assert.That(inventory.Slots.Count, Is.EqualTo(32));
        }

        [Test] public void INVENTORY_EXCHANGE_004_ObserversSeeCommittedStateAndAllRewardsExactlyOnce()
        {
            var inventory = new Inventory(); var a = Item("a", 99); var b = Item("b", 99); bool committed = false; int notifications = 0;
            inventory.OnChanged += () => { notifications++; Assert.That(committed, Is.True); Assert.That(inventory.CountOf(a), Is.EqualTo(3)); Assert.That(inventory.CountOf(b), Is.EqualTo(4)); };
            Assert.That(Exchange(inventory, null, new[] { new InventoryGrant(a, 3), new InventoryGrant(b, 4) }, () => committed = true), Is.True);
            Assert.That(notifications, Is.EqualTo(1));
        }

        [Test] public void INVENTORY_EXCHANGE_005_ObserverCannotReenterRewardTransaction()
        {
            var inventory = new Inventory(); var item = Item("a", 99); bool nested = true; int notifications = 0;
            inventory.OnChanged += () => { notifications++; if (notifications == 1) nested = Exchange(inventory, null, new[] { new InventoryGrant(item, 10) }, null); };
            Assert.That(Exchange(inventory, null, new[] { new InventoryGrant(item, 3) }, null), Is.True);
            Assert.That(nested, Is.False); Assert.That(inventory.CountOf(item), Is.EqualTo(3)); Assert.That(notifications, Is.EqualTo(1));
        }

        [Test] public void INVENTORY_EXCHANGE_006_InvalidAmountsFailBeforeCallingCommit()
        {
            var inventory = new Inventory(); var item = Item("a", 99); bool committed = false;
            Assert.That(Exchange(inventory, new[] { new InventoryGrant(item, -1) }, null, () => committed = true), Is.False);
            Assert.That(Exchange(inventory, null, new[] { new InventoryGrant(null, 2) }, () => committed = true), Is.False);
            Assert.That(committed, Is.False); Assert.That(inventory.Slots.Count, Is.Zero);
        }

        [Test] public void INVENTORY_EXCHANGE_007_AddAllSharesTheAtomicNotificationContract()
        {
            var inventory = new Inventory(); var item = Item("a", 99); int changes = 0; inventory.OnChanged += () => changes++;
            var method = typeof(Inventory).GetMethod("TryAddAll"); Assert.That(method, Is.Not.Null, "Inventory.TryAddAll missing");
            Assert.That((bool)method.Invoke(inventory, new object[] { new[] { new InventoryGrant(item, 2), new InventoryGrant(item, 3) }, null }), Is.True);
            Assert.That(inventory.CountOf(item), Is.EqualTo(5)); Assert.That(changes, Is.EqualTo(1));
        }

        [Test] public void INVENTORY_ADD_001_EmptySlotCreatedByDragIsReusedInsteadOfLosingItem()
        {
            var inventory = new Inventory(); var moved = Item("moved", 1); var added = Item("added", 1); inventory.Add(moved);
            Assert.That(inventory.TryMoveSlot(0, Inventory.MaxSlots - 1), Is.True);
            Assert.That(inventory.CanAdd(added), Is.True);
            inventory.Add(added);
            Assert.That(inventory.CountOf(added), Is.EqualTo(1)); Assert.That(inventory.CountOf(moved), Is.EqualTo(1)); Assert.That(inventory.Slots.Count, Is.EqualTo(Inventory.MaxSlots));
        }

        [Test] public void INVENTORY_ADD_002_CanAddTrueAlwaysMeansFullAmountIsStored()
        {
            var inventory = new Inventory(); var moved = Item("moved", 1); var added = Item("added", 2); inventory.Add(moved);
            Assert.That(inventory.TryMoveSlot(0, Inventory.MaxSlots - 1), Is.True);
            const int amount = 2 * (Inventory.MaxSlots - 1);
            Assert.That(inventory.CanAdd(added, amount), Is.True); Assert.That(inventory.CanAdd(added, amount + 1), Is.False);
            inventory.Add(added, amount);
            Assert.That(inventory.CountOf(added), Is.EqualTo(amount));
        }

        private ItemDefinition Item(string id, int stack) { var item = ScriptableObject.CreateInstance<ItemDefinition>(); _items.Add(item); typeof(ItemDefinition).GetField("_id", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(item, id); typeof(ItemDefinition).GetField("_maxStack", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(item, stack); return item; }
        private static bool CanExchange(Inventory inventory, IReadOnlyList<InventoryGrant> costs, IReadOnlyList<InventoryGrant> rewards) { var method = typeof(Inventory).GetMethod("CanExchange"); Assert.That(method, Is.Not.Null, "Inventory.CanExchange missing"); return (bool)method.Invoke(inventory, new object[] { costs, rewards }); }
        private static bool Exchange(Inventory inventory, IReadOnlyList<InventoryGrant> costs, IReadOnlyList<InventoryGrant> rewards, Action commit) { var method = typeof(Inventory).GetMethod("TryExchange"); Assert.That(method, Is.Not.Null, "Inventory.TryExchange missing"); return (bool)method.Invoke(inventory, new object[] { costs, rewards, commit }); }
    }
}
