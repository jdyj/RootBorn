// 옮겨 옴: game/Rootborn.Game/CareerInventoryAdapter.cs(테스트가 없던 것). 플레이어 인벤토리를 성인 직업 인벤토리(id)로 쓰는 다리.
using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Core.Careers;
using Rootborn.Core.Inventory;

namespace Rootborn.Core.Tests
{
    public sealed class ItemInventoryCareerInventoryTests
    {
        private static readonly ItemDefinition Coin = new ItemDefinition("item.coin", 999);
        private static readonly ItemDefinition Wood = new ItemDefinition("item.wood", 99);

        private static ItemDefinition Find(string id) => id == Coin.Id ? Coin : id == Wood.Id ? Wood : null;

        private static ItemAmount<string>[] Amounts(params (string id, int count)[] items)
        {
            var list = new List<ItemAmount<string>>();
            foreach (var (id, count) in items) list.Add(new ItemAmount<string>(id, count));
            return list.ToArray();
        }

        [Test]
        public void Exchange_ConvertsIdsToDefinitions_AndRunsTheCommitInside()
        {
            var inventory = new ItemInventory();
            inventory.Add(Coin, 10);
            var career = new ItemInventoryCareerInventory(inventory, Find);
            bool committed = false;

            Assert.That(career.CanExchange(Amounts(("item.coin", 4)), Amounts(("item.wood", 2))), Is.True);
            Assert.That(career.TryExchange(Amounts(("item.coin", 4)), Amounts(("item.wood", 2)), () => committed = true), Is.True);

            Assert.That(committed, Is.True);
            Assert.That(career.CountOf("item.coin"), Is.EqualTo(6));
            Assert.That(career.CountOf("item.wood"), Is.EqualTo(2));
        }

        [Test]
        public void AnUnknownId_RefusesTheWholeExchange_AndChangesNothing()
        {
            var inventory = new ItemInventory();
            inventory.Add(Coin, 10);
            var career = new ItemInventoryCareerInventory(inventory, Find);
            bool committed = false;

            Assert.That(career.CanExchange(Amounts(("item.coin", 1)), Amounts(("item.unknown", 1))), Is.False);
            Assert.That(career.TryExchange(Amounts(("item.coin", 1)), Amounts(("item.unknown", 1)), () => committed = true), Is.False);
            Assert.That(career.TryExchange(Amounts(("", 1)), null, () => committed = true), Is.False);

            Assert.That(committed, Is.False);
            Assert.That(career.CountOf("item.coin"), Is.EqualTo(10));
            Assert.That(career.CountOf("item.unknown"), Is.EqualTo(0));
            Assert.That(career.CountOf(null), Is.EqualTo(0));
        }

        [Test]
        public void NotEnoughToPay_IsRefused()
        {
            var inventory = new ItemInventory();
            inventory.Add(Coin, 2);
            var career = new ItemInventoryCareerInventory(inventory, Find);

            Assert.That(career.TryExchange(Amounts(("item.coin", 3)), null, null), Is.False);
            Assert.That(career.CountOf("item.coin"), Is.EqualTo(2));
            Assert.That(career.TryExchange(null, null, null), Is.True, "빈 교환은 된다");
        }
    }
}
