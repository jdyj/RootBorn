using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Core.Inventory;
using Rootborn.Core.Rewards;

namespace Rootborn.Core.Tests
{
    public sealed class RewardTransactionTests
    {
        private sealed class Item
        {
            public Item(string name) { Name = name; }
            public string Name { get; }
        }

        private static readonly Item Wood = new Item("wood");

        /// <summary>Holds at most Capacity items in total; TryReceive runs the commit inside, like the game inventory.</summary>
        private sealed class FakeInventory : IRewardInventory<Item>
        {
            public int Capacity = 10;
            public int Held;
            public bool RefuseOnReceive;
            public readonly List<string> Log;

            public FakeInventory(List<string> log) { Log = log; }

            private static int Total(IReadOnlyList<ItemAmount<Item>> grants)
            {
                int total = 0;
                foreach (var g in grants) total += g.Count;
                return total;
            }

            public bool CanReceive(IReadOnlyList<ItemAmount<Item>> grants) => Held + Total(grants) <= Capacity;

            public bool TryReceive(IReadOnlyList<ItemAmount<Item>> grants, Action commit)
            {
                if (RefuseOnReceive || !CanReceive(grants)) return false;
                int before = Held;
                Held += Total(grants);
                Log.Add("items+" + Total(grants));
                try { commit(); }
                catch { Held = before; Log.Add("items-rollback"); throw; }
                return true;
            }
        }

        private sealed class Step : IRewardStep
        {
            private readonly string _name;
            private readonly List<string> _log;
            public bool Allowed = true;
            public Action OnApply;

            public Step(string name, List<string> log) { _name = name; _log = log; }
            public bool CanApply() => Allowed;
            public void Apply() { _log.Add(_name); OnApply?.Invoke(); }
        }

        private static RewardBundle<Item> Bundle(int wood, params IRewardStep[] steps)
            => new RewardBundle<Item>(wood > 0 ? new[] { new ItemAmount<Item>(Wood, wood) } : null, steps);

        [Test]
        public void Apply_GivesItemsThenRunsStepsThenCommitsStateOnce()
        {
            var log = new List<string>();
            var inventory = new FakeInventory(log);
            var bundle = Bundle(3, new Step("money", log), new Step("flag", log));

            Assert.That(RewardTransaction.TryApply(bundle, inventory, () => log.Add("claimed")), Is.True);
            Assert.That(log, Is.EqualTo(new[] { "items+3", "money", "flag", "claimed" }));
            Assert.That(inventory.Held, Is.EqualTo(3));
        }

        [Test]
        public void Apply_ChangesNothingWhenAnyStepCannotApply()
        {
            var log = new List<string>();
            var inventory = new FakeInventory(log);
            var blocked = new Step("flag", log) { Allowed = false };

            Assert.That(RewardTransaction.CanApply(Bundle(3, new Step("money", log), blocked), inventory), Is.False);
            Assert.That(RewardTransaction.TryApply(Bundle(3, new Step("money", log), blocked), inventory, () => log.Add("claimed")), Is.False);
            Assert.That(log, Is.Empty);
            Assert.That(inventory.Held, Is.EqualTo(0));
        }

        [Test]
        public void Apply_ChangesNothingWhenTheItemsDoNotFit()
        {
            var log = new List<string>();
            var inventory = new FakeInventory(log) { Held = 9 };

            Assert.That(RewardTransaction.TryApply(Bundle(3, new Step("money", log)), inventory, () => log.Add("claimed")), Is.False);
            Assert.That(log, Is.Empty);
            Assert.That(inventory.Held, Is.EqualTo(9));
        }

        [Test]
        public void Apply_ChangesNothingWhenTheInventoryRefusesAtCommitTime()
        {
            var log = new List<string>();
            var inventory = new FakeInventory(log) { RefuseOnReceive = true };

            Assert.That(RewardTransaction.TryApply(Bundle(3, new Step("money", log)), inventory, () => log.Add("claimed")), Is.False);
            Assert.That(log, Is.Empty);
        }

        [Test]
        public void ItemRewardsNeedAnInventory_OtherRewardsDoNot()
        {
            var log = new List<string>();
            Assert.That(RewardTransaction.CanApply(Bundle(1), null), Is.False);
            Assert.That(RewardTransaction.TryApply(Bundle(0, new Step("flag", log)), null, () => log.Add("claimed")), Is.True);
            Assert.That(log, Is.EqualTo(new[] { "flag", "claimed" }));
        }

        [Test]
        public void Apply_RollsTheItemsBackWhenCommittingStateThrows()
        {
            var log = new List<string>();
            var inventory = new FakeInventory(log);

            Assert.Throws<InvalidOperationException>(() =>
                RewardTransaction.TryApply(Bundle(3), inventory, () => throw new InvalidOperationException()));
            Assert.That(inventory.Held, Is.EqualTo(0));
        }

        [Test]
        public void Gate_PaysOnceAndRefusesAfterTheClaimIsRecorded()
        {
            var log = new List<string>();
            var inventory = new FakeInventory(log);
            var gate = new RewardClaimGate();
            bool claimed = false;
            var bundle = Bundle(2, new Step("money", log));

            bool Claim() => gate.TryClaim(() => !claimed, () => RewardTransaction.TryApply(bundle, inventory, () => claimed = true));

            Assert.That(Claim(), Is.True);
            Assert.That(Claim(), Is.False, "the same claim again pays nothing");
            Assert.That(inventory.Held, Is.EqualTo(2));
            Assert.That(log, Is.EqualTo(new[] { "items+2", "money" }));
        }

        [Test]
        public void Gate_RefusesAClaimStartedFromInsideAnotherClaim()
        {
            var log = new List<string>();
            var inventory = new FakeInventory(log);
            var gate = new RewardClaimGate();
            bool claimed = false;
            var reentrant = new Step("money", log);
            var bundle = Bundle(1, reentrant);
            bool inner = true;
            reentrant.OnApply = () => inner = gate.TryClaim(() => true, () => RewardTransaction.TryApply(bundle, inventory, null));

            Assert.That(gate.TryClaim(() => !claimed, () => RewardTransaction.TryApply(bundle, inventory, () => claimed = true)), Is.True);
            Assert.That(inner, Is.False);
            Assert.That(inventory.Held, Is.EqualTo(1));
        }

        [Test]
        public void Gate_IsReleasedAfterAFailedOrThrowingClaim()
        {
            var gate = new RewardClaimGate();
            Assert.That(gate.TryClaim(() => false, () => true), Is.False);
            Assert.Throws<InvalidOperationException>(() => gate.TryClaim(() => true, () => throw new InvalidOperationException()));
            Assert.That(gate.TryClaim(() => true, () => true), Is.True);
        }
    }
}
