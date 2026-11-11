using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Inventory;

namespace Rootborn.Core.Tests
{
    public sealed class InventoryPlannerTests
    {
        private sealed class Item
        {
            public Item(string name, int maxStack) { Name = name; MaxStack = maxStack; }
            public string Name { get; }
            public int MaxStack { get; }
            public override string ToString() => Name;
        }

        private static readonly Item Wood = new Item("wood", 10);
        private static readonly Item Stone = new Item("stone", 5);
        private static readonly Item Chair = new Item("chair", 1);

        private static ItemStack<Item> S(Item item, int count) => new ItemStack<Item>(item, count);
        private static ItemAmount<Item> A(Item item, int count) => new ItemAmount<Item>(item, count);

        private static bool Plan(List<ItemStack<Item>> slots, int maxSlots, ItemAmount<Item>[] costs, ItemAmount<Item>[] rewards,
            out List<ItemStack<Item>> next)
        {
            return InventoryPlanner.TryPlanExchange(slots, maxSlots, costs, rewards, i => i.MaxStack, out next);
        }

        private static string[] Show(IEnumerable<ItemStack<Item>> slots) => slots.Select(s => (s.Item?.Name ?? "-") + ":" + s.Count).ToArray();

        [Test]
        public void Rewards_TopUpExistingStacksThenReuseEmptySlotsThenAppend()
        {
            var slots = new List<ItemStack<Item>> { S(Wood, 7), S(null, 0), S(Stone, 5) };
            Assert.That(Plan(slots, 8, null, new[] { A(Wood, 15) }, out var next), Is.True);
            Assert.That(Show(next), Is.EqualTo(new[] { "wood:10", "wood:10", "stone:5", "wood:2" }));
        }

        [Test]
        public void Rewards_ThatDoNotFitFailAndLeaveTheInputUnchanged()
        {
            var slots = new List<ItemStack<Item>> { S(Chair, 1), S(Stone, 5) };
            Assert.That(Plan(slots, 3, null, new[] { A(Chair, 1), A(Wood, 1) }, out _), Is.False, "two new stacks, one free slot");
            Assert.That(Show(slots), Is.EqualTo(new[] { "chair:1", "stone:5" }));
        }

        [Test]
        public void Rewards_AllFitTogetherOrNoneAreGiven()
        {
            var slots = new List<ItemStack<Item>>();
            Assert.That(Plan(slots, 2, null, new[] { A(Chair, 1), A(Chair, 1) }, out _), Is.True);
            Assert.That(Plan(slots, 2, null, new[] { A(Chair, 1), A(Chair, 1), A(Chair, 1) }, out _), Is.False,
                "each reward fitting alone does not mean they all fit");
        }

        [Test]
        public void Costs_AreTakenFromTheLastStacksAndEmptiedSlotsBecomeFree()
        {
            var slots = new List<ItemStack<Item>> { S(Wood, 10), S(Stone, 2), S(Wood, 4) };
            Assert.That(Plan(slots, 3, new[] { A(Wood, 6) }, null, out var next), Is.True);
            Assert.That(Show(next), Is.EqualTo(new[] { "wood:8", "stone:2", "-:0" }));
        }

        [Test]
        public void Costs_ForTheSameItemAreSummedBeforeCheckingWhatIsAvailable()
        {
            var slots = new List<ItemStack<Item>> { S(Wood, 5) };
            Assert.That(Plan(slots, 4, new[] { A(Wood, 3), A(Wood, 3) }, null, out _), Is.False);
            Assert.That(Plan(slots, 4, new[] { A(Wood, 3), A(Wood, 2) }, null, out var next), Is.True);
            Assert.That(Show(next), Is.EqualTo(new[] { "-:0" }));
        }

        [Test]
        public void Exchange_PaysCostsFirstSoTheFreedSlotCanHoldTheReward()
        {
            var slots = new List<ItemStack<Item>> { S(Wood, 3), S(Stone, 5) };
            Assert.That(Plan(slots, 2, new[] { A(Wood, 3) }, new[] { A(Chair, 1) }, out var next), Is.True);
            Assert.That(Show(next), Is.EqualTo(new[] { "chair:1", "stone:5" }));
        }

        [Test]
        public void InvalidAmountsFailTheWholeExchange()
        {
            var slots = new List<ItemStack<Item>> { S(Wood, 5) };
            Assert.That(Plan(slots, 4, new[] { A(null, 1) }, null, out _), Is.False);
            Assert.That(Plan(slots, 4, new[] { A(Wood, 0) }, null, out _), Is.False);
            Assert.That(Plan(slots, 4, null, new[] { A(Wood, -1) }, out _), Is.False);
            Assert.That(Plan(slots, 4, null, new[] { A(null, 1) }, out _), Is.False);
        }

        [Test]
        public void NothingToExchangeSucceedsWithACopyOfTheSlots()
        {
            var slots = new List<ItemStack<Item>> { S(Wood, 5) };
            Assert.That(Plan(slots, 4, null, null, out var next), Is.True);
            Assert.That(next, Is.Not.SameAs(slots));
            Assert.That(Show(next), Is.EqualTo(Show(slots)));
        }

        [Test]
        public void ItemsAreMatchedWithTheDefaultEqualityOfTheItemType()
        {
            var slots = new List<ItemStack<string>> { new ItemStack<string>("wood", 2) };
            string sameNameOtherInstance = new string("wood".ToCharArray());
            Assert.That(InventoryPlanner.TryPlanExchange(slots, 1, new[] { new ItemAmount<string>(sameNameOtherInstance, 2) }, null,
                _ => 10, out _), Is.True);
        }
    }
}
