using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Inventory;

namespace Rootborn.Core.Tests
{
    public sealed class ItemSlotsTests
    {
        private sealed class Item
        {
            public Item(string name, int maxStack) { Name = name; MaxStack = maxStack; }
            public string Name { get; }
            public int MaxStack { get; }
        }

        private sealed class Slot : ItemSlot<Item> { }

        private static readonly Item Wood = new Item("wood", 10);
        private static readonly Item Stone = new Item("stone", 5);
        private static readonly Item Chair = new Item("chair", 1);

        private static ItemSlots<Item, Slot> New(int maxSlots = 4) => new ItemSlots<Item, Slot>(maxSlots, i => i.MaxStack);

        private static string[] Show(ItemSlots<Item, Slot> slots) => slots.Slots.Select(s => (s.Item?.Name ?? "-") + ":" + s.Count).ToArray();

        private static ItemAmount<Item> A(Item item, int count) => new ItemAmount<Item>(item, count);

        [Test]
        public void Add_TopsUpThenFillsEmptiedSlotsThenAppends_AndDropsWhatDoesNotFit()
        {
            var slots = New(maxSlots: 3);
            slots.Add(Wood, 7);
            slots.Add(Stone, 5);
            slots.Add(Wood, 15);
            Assert.That(Show(slots), Is.EqualTo(new[] { "wood:10", "stone:5", "wood:10" }));
            slots.Add(Wood, 5);
            Assert.That(Show(slots), Is.EqualTo(new[] { "wood:10", "stone:5", "wood:10" }), "no room: the rest is dropped");
        }

        [Test]
        public void CanAdd_CountsEmptiedSlotsAsFree()
        {
            var slots = New(maxSlots: 2);
            slots.Add(Chair, 1);
            slots.Add(Stone, 5);
            Assert.That(slots.CanAdd(Chair, 1), Is.False);
            Assert.That(slots.TryExchange(new[] { A(Stone, 5) }, null), Is.True);
            Assert.That(slots.CanAdd(Chair, 1), Is.True);
        }

        [Test]
        public void CanAddAll_ChecksTheGrantsTogether()
        {
            var slots = New(maxSlots: 2);
            Assert.That(slots.CanAddAll(new[] { A(Chair, 1), A(Chair, 1) }), Is.True);
            Assert.That(slots.CanAddAll(new[] { A(Chair, 1), A(Chair, 1), A(Chair, 1) }), Is.False);
            Assert.That(slots.CanAddAll(null), Is.True);
        }

        [Test]
        public void Remove_TakesFromTheLastStacksAndDropsEmptiedSlots()
        {
            var slots = New();
            slots.Add(Wood, 10);
            slots.Add(Stone, 2);
            slots.Add(Wood, 4);
            Assert.That(slots.Remove(Wood, 6), Is.True);
            Assert.That(Show(slots), Is.EqualTo(new[] { "wood:8", "stone:2" }));
            Assert.That(slots.Remove(Wood, 9), Is.False, "not enough");
            Assert.That(slots.CountOf(Wood), Is.EqualTo(8));
        }

        [Test]
        public void MoveSlot_MovesIntoEmptySlotsAndMergesSameItems()
        {
            var slots = New();
            slots.Add(Wood, 4);
            Assert.That(slots.TryMoveSlot(0, 2), Is.True);
            Assert.That(Show(slots), Is.EqualTo(new[] { "-:0", "-:0", "wood:4" }));
            slots.Add(Wood, 8);
            Assert.That(Show(slots), Is.EqualTo(new[] { "wood:2", "-:0", "wood:10" }), "tops up the stack, then fills the first empty slot");
            Assert.That(slots.TryMoveSlot(2, 0), Is.True);
            Assert.That(Show(slots), Is.EqualTo(new[] { "wood:10", "-:0", "wood:2" }));
            slots.Add(Stone, 1);
            Assert.That(slots.TryMoveSlot(1, 0), Is.False, "different items do not swap");
            Assert.That(slots.TryMoveSlot(-1, 0), Is.False);
        }

        [Test]
        public void Exchange_IsAllOrNothingAndCommitsStateInside()
        {
            var slots = New(maxSlots: 2);
            slots.Add(Wood, 3);
            slots.Add(Stone, 5);
            int changes = 0;
            slots.Changed += () => changes++;
            var log = new List<string>();

            Assert.That(slots.TryExchange(new[] { A(Wood, 3) }, new[] { A(Chair, 1), A(Chair, 1) }, () => log.Add("x")), Is.False);
            Assert.That(Show(slots), Is.EqualTo(new[] { "wood:3", "stone:5" }));
            Assert.That(log, Is.Empty);
            Assert.That(changes, Is.EqualTo(0));

            Assert.That(slots.TryExchange(new[] { A(Wood, 3) }, new[] { A(Chair, 1) }, () => log.Add("committed:" + slots.CountOf(Chair))), Is.True);
            Assert.That(Show(slots), Is.EqualTo(new[] { "chair:1", "stone:5" }));
            Assert.That(log, Is.EqualTo(new[] { "committed:1" }), "the commit sees the new slots");
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void Exchange_RollsBackWhenTheCommitThrows()
        {
            var slots = New();
            slots.Add(Wood, 3);
            Assert.Throws<InvalidOperationException>(() => slots.TryExchange(null, new[] { A(Stone, 2) }, () => throw new InvalidOperationException()));
            Assert.That(Show(slots), Is.EqualTo(new[] { "wood:3" }));
        }

        [Test]
        public void Exchange_CannotStartAnotherExchangeFromInsideItsCommitOrObservers()
        {
            var slots = New();
            bool innerFromCommit = true, innerFromObserver = true;
            slots.Changed += () => innerFromObserver = slots.TryExchange(null, new[] { A(Stone, 1) });
            Assert.That(slots.TryExchange(null, new[] { A(Wood, 1) }, () => innerFromCommit = slots.TryExchange(null, new[] { A(Stone, 1) })), Is.True);
            Assert.That(innerFromCommit, Is.False);
            Assert.That(innerFromObserver, Is.False);
            Assert.That(slots.CountOf(Stone), Is.EqualTo(0));
            Assert.That(slots.CanExchange(null, new[] { A(Stone, 1) }), Is.True, "the guard is released afterwards");
        }

        [Test]
        public void Clear_EmptiesAndNotifiesOnlyWhenSomethingChanged()
        {
            var slots = New();
            int changes = 0;
            slots.Changed += () => changes++;
            slots.Clear();
            Assert.That(changes, Is.EqualTo(0));
            slots.Add(Wood, 1);
            slots.Clear();
            Assert.That(slots.Slots.Count, Is.EqualTo(0));
            Assert.That(changes, Is.EqualTo(2));
        }

        [Test]
        public void InvalidInputsChangeNothing()
        {
            var slots = New();
            slots.Add(null, 3);
            slots.Add(Wood, 0);
            Assert.That(slots.Slots.Count, Is.EqualTo(0));
            Assert.That(slots.CanAdd(null, 1), Is.False);
            Assert.That(slots.Remove(Wood, 0), Is.False);
            Assert.That(slots.CountOf(null), Is.EqualTo(0));
        }
    }
}
