using System;
using NUnit.Framework;
using Rootborn.Core.Inventory;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Tests
{
    /// <summary>
    /// 지갑(멀티플레이 설계 2.2절): 돈은 가방 칸을 쓰지 않는다. 가방(ItemInventory)에 통화 아이템을 주고받으면 지갑이 바뀌고,
    /// 지갑과 가방은 한 번에 확인하고 한 번에 바뀐다(전부 아니면 전무).
    /// </summary>
    public sealed class WalletTests
    {
        private static ItemDefinition Item(string id, int stack = 99, ItemCategory category = ItemCategory.Resource) => new ItemDefinition(id, stack, category);

        private static (ItemInventory Bag, PersonalWallet Wallet, ItemDefinition Money) BagWithWallet(int balance = 0)
        {
            var wallet = new PersonalWallet();
            wallet.LoadFromSaveData(new WalletSaveData { Balance = balance });
            var money = Item("money", 9999, ItemCategory.Misc);
            return (new ItemInventory(wallet, money), wallet, money);
        }

        // ---------- 지갑 하나 ----------

        [Test]
        public void WALLET_001_TakeAndGive_ChangeTheBalance()
        {
            var wallet = new PersonalWallet();
            Assert.That(wallet.TryExchange(0, 30, null), Is.True);
            Assert.That(wallet.TryExchange(10, 0, null), Is.True);
            Assert.That(wallet.Balance, Is.EqualTo(20));
        }

        [Test]
        public void WALLET_002_NotEnough_IsRefused_AndNeverNegative()
        {
            var wallet = new PersonalWallet();
            wallet.LoadFromSaveData(new WalletSaveData { Balance = 5 });
            bool committed = false;
            Assert.That(wallet.CanExchange(6, 0), Is.False);
            Assert.That(wallet.TryExchange(6, 0, () => committed = true), Is.False);
            Assert.That(wallet.Balance, Is.EqualTo(5));
            Assert.That(committed, Is.False);
        }

        [Test]
        public void WALLET_003_NegativeAmounts_AreRefused()
        {
            var wallet = new PersonalWallet();
            wallet.LoadFromSaveData(new WalletSaveData { Balance = 5 });
            Assert.That(wallet.TryExchange(-1, 0, null), Is.False);
            Assert.That(wallet.TryExchange(0, -1, null), Is.False);
            Assert.That(wallet.Balance, Is.EqualTo(5));
        }

        [Test]
        public void WALLET_004_PastTheTop_IsRefused_NotWrapped()
        {
            var wallet = new PersonalWallet();
            wallet.LoadFromSaveData(new WalletSaveData { Balance = PersonalWallet.MaxBalance - 1 });
            Assert.That(wallet.TryExchange(0, 2, null), Is.False);
            Assert.That(wallet.Balance, Is.EqualTo(PersonalWallet.MaxBalance - 1));
            Assert.That(wallet.TryExchange(0, 1, null), Is.True);
            Assert.That(wallet.Balance, Is.EqualTo(PersonalWallet.MaxBalance));
        }

        [Test]
        public void WALLET_005_CommitThrows_BalanceIsRestored()
        {
            var wallet = new PersonalWallet();
            int notified = 0;
            wallet.Changed += () => notified++;
            Assert.Throws<InvalidOperationException>(() => wallet.TryExchange(0, 10, () => throw new InvalidOperationException()));
            Assert.That(wallet.Balance, Is.Zero);
            Assert.That(notified, Is.Zero);
        }

        [Test]
        public void WALLET_006_ObserverCannotReenter()
        {
            var wallet = new PersonalWallet();
            bool nested = true;
            wallet.Changed += () => nested = wallet.TryExchange(0, 100, null);
            Assert.That(wallet.TryExchange(0, 1, null), Is.True);
            Assert.That(nested, Is.False);
            Assert.That(wallet.Balance, Is.EqualTo(1));
        }

        [Test]
        public void WALLET_007_SaveRoundTrip_KeepsTheBalance_AndAVersion()
        {
            var wallet = new PersonalWallet();
            wallet.TryExchange(0, 42, null);
            var saved = wallet.ToSaveData();
            Assert.That(saved.Version, Is.EqualTo(WalletSaveData.KnownVersion));
            var again = new PersonalWallet();
            again.LoadFromSaveData(saved);
            Assert.That(again.Balance, Is.EqualTo(42));
        }

        [Test]
        public void WALLET_008_ANegativeSavedBalance_ReadsAsZero()
        {
            var wallet = new PersonalWallet();
            wallet.LoadFromSaveData(new WalletSaveData { Balance = -7 });
            Assert.That(wallet.Balance, Is.Zero);
        }

        // ---------- 가방과 함께 ----------

        [Test]
        public void BAG_WALLET_001_MoneyGoesToTheWallet_NotABagSlot()
        {
            var (bag, wallet, money) = BagWithWallet();
            bag.Add(money, 25);
            Assert.That(wallet.Balance, Is.EqualTo(25));
            Assert.That(bag.CountOf(money), Is.EqualTo(25), "가방에 묻는 돈 수는 지갑 잔액");
            Assert.That(bag.Slots, Is.Empty, "돈은 가방 칸을 쓰지 않는다");
        }

        [Test]
        public void BAG_WALLET_002_AFullBag_StillTakesMoney()
        {
            var (bag, wallet, money) = BagWithWallet();
            for (int i = 0; i < ItemInventory.MaxSlots; i++) bag.Add(Item("occupied-" + i, 1));
            Assert.That(bag.CanAdd(money, 5), Is.True);
            Assert.That(bag.TryAddAll(new[] { new InventoryGrant(money, 5) }), Is.True);
            Assert.That(wallet.Balance, Is.EqualTo(5));
        }

        [Test]
        public void BAG_WALLET_003_MoneyAndItemsTogether_AllOrNothing_WhenTheBagIsFull()
        {
            var (bag, wallet, money) = BagWithWallet();
            for (int i = 0; i < ItemInventory.MaxSlots; i++) bag.Add(Item("occupied-" + i, 1));
            var wood = Item("wood");
            bool committed = false;
            var rewards = new[] { new InventoryGrant(money, 5), new InventoryGrant(wood, 1) };
            Assert.That(bag.CanAddAll(rewards), Is.False);
            Assert.That(bag.TryAddAll(rewards, () => committed = true), Is.False);
            Assert.That(wallet.Balance, Is.Zero, "가방에 못 들어가면 돈도 들어오지 않는다");
            Assert.That(bag.CountOf(wood), Is.Zero);
            Assert.That(committed, Is.False);
        }

        [Test]
        public void BAG_WALLET_004_NotEnoughMoney_TheItemSideDoesNotChangeEither()
        {
            var (bag, wallet, money) = BagWithWallet(3);
            var material = Item("material");
            bag.Add(material, 4);
            var costs = new[] { new InventoryGrant(money, 5), new InventoryGrant(material, 2) };
            Assert.That(bag.CanExchange(costs, null), Is.False);
            Assert.That(bag.TryExchange(costs, null, null), Is.False);
            Assert.That(wallet.Balance, Is.EqualTo(3));
            Assert.That(bag.CountOf(material), Is.EqualTo(4));
        }

        [Test]
        public void BAG_WALLET_005_ExchangeTakesMoneyAndGivesAnItem_Together()
        {
            var (bag, wallet, money) = BagWithWallet(10);
            var chair = Item("chair");
            bool committed = false;
            Assert.That(bag.TryExchange(new[] { new InventoryGrant(money, 7) }, new[] { new InventoryGrant(chair, 1) }, () => committed = true), Is.True);
            Assert.That(wallet.Balance, Is.EqualTo(3));
            Assert.That(bag.CountOf(chair), Is.EqualTo(1));
            Assert.That(committed, Is.True);
        }

        [Test]
        public void BAG_WALLET_006_CommitThrows_BothSidesAreRestored()
        {
            var (bag, wallet, money) = BagWithWallet(10);
            var chair = Item("chair");
            Assert.Throws<InvalidOperationException>(() =>
                bag.TryExchange(new[] { new InventoryGrant(money, 7) }, new[] { new InventoryGrant(chair, 1) }, () => throw new InvalidOperationException()));
            Assert.That(wallet.Balance, Is.EqualTo(10));
            Assert.That(bag.CountOf(chair), Is.Zero);
        }

        [Test]
        public void BAG_WALLET_007_OneExchange_NotifiesOnce_AfterBothSides()
        {
            var (bag, wallet, money) = BagWithWallet();
            var wood = Item("wood");
            int notified = 0;
            bag.OnChanged += () =>
            {
                notified++;
                Assert.That(wallet.Balance, Is.EqualTo(5));
                Assert.That(bag.CountOf(wood), Is.EqualTo(2));
            };
            Assert.That(bag.TryAddAll(new[] { new InventoryGrant(money, 5), new InventoryGrant(wood, 2) }), Is.True);
            Assert.That(notified, Is.EqualTo(1));
        }

        [Test]
        public void BAG_WALLET_008_MoneyOnlyExchange_NotifiesOnce_AndObserverCannotReenter()
        {
            var (bag, _, money) = BagWithWallet();
            int notified = 0;
            bool nested = true;
            bag.OnChanged += () => { notified++; nested = bag.TryAddAll(new[] { new InventoryGrant(money, 100) }); };
            Assert.That(bag.TryAddAll(new[] { new InventoryGrant(money, 5) }), Is.True);
            Assert.That(notified, Is.EqualTo(1));
            Assert.That(nested, Is.False);
            Assert.That(bag.CountOf(money), Is.EqualTo(5));
        }

        [Test]
        public void BAG_WALLET_009_RemoveMoney_TakesFromTheWallet()
        {
            var (bag, wallet, money) = BagWithWallet(8);
            Assert.That(bag.Remove(money, 9), Is.False);
            Assert.That(bag.Remove(money, 3), Is.True);
            Assert.That(wallet.Balance, Is.EqualTo(5));
        }

        [Test]
        public void BAG_WALLET_010_TheWalletChangedFromOutside_TheBagTellsItsObservers()
        {
            var (bag, wallet, _) = BagWithWallet();
            int notified = 0;
            bag.OnChanged += () => notified++;
            wallet.TryExchange(0, 4, null);
            Assert.That(notified, Is.EqualTo(1));
        }

        [Test]
        public void BAG_WALLET_011_InvalidMoneyAmounts_FailBeforeAnything()
        {
            var (bag, wallet, money) = BagWithWallet(5);
            Assert.That(bag.TryAddAll(new[] { new InventoryGrant(money, 0) }), Is.False);
            Assert.That(bag.TryExchange(new[] { new InventoryGrant(money, -1) }, null, null), Is.False);
            Assert.That(wallet.Balance, Is.EqualTo(5));
        }

        [Test]
        public void BAG_WALLET_012_OldSaveWithMoneyInTheBag_MovesItToTheWallet_AndSavesTheBagWithoutIt()
        {
            var (bag, wallet, money) = BagWithWallet(2);
            var wood = Item("wood");
            var old = new PlayerInventorySaveData
            {
                Items = new[]
                {
                    new PlayerInventoryItemSaveData { ItemId = "money", Count = 9999 },
                    new PlayerInventoryItemSaveData { ItemId = "wood", Count = 3 },
                    new PlayerInventoryItemSaveData { ItemId = "money", Count = 40 },
                },
            };
            bag.LoadFromSaveData(old, id => id == "money" ? money : id == "wood" ? wood : null);
            Assert.That(wallet.Balance, Is.EqualTo(2 + 9999 + 40), "지갑에 있던 것 + 가방의 돈 칸 전부");
            Assert.That(bag.CountOf(wood), Is.EqualTo(3));
            Assert.That(bag.Slots.Count, Is.EqualTo(1), "돈 칸은 비운다");
            Assert.That(bag.ToSaveData(string.Empty).Items, Has.None.Matches<PlayerInventoryItemSaveData>(i => i.ItemId == "money"));
        }

        [Test]
        public void BAG_WALLET_013_WithoutAWallet_MoneyIsAnOrdinaryItem()
        {
            var bag = new ItemInventory();
            var money = Item("money", 9999, ItemCategory.Misc);
            bag.Add(money, 5);
            Assert.That(bag.Slots.Count, Is.EqualTo(1));
            Assert.That(bag.CountOf(money), Is.EqualTo(5));
        }

        [Test]
        public void BAG_WALLET_014_ClearEmptiesTheBagSlots_NotTheWallet()
        {
            var (bag, wallet, money) = BagWithWallet(6);
            bag.Add(Item("wood"), 1);
            bag.Clear();
            Assert.That(bag.Slots, Is.Empty);
            Assert.That(wallet.Balance, Is.EqualTo(6));
        }

        [Test]
        public void BAG_WALLET_015_DaySummary_StillSeesTheMoney()
        {
            var (bag, _, money) = BagWithWallet();
            var before = StudentDayQuestInventorySnapshot.Capture(null, null, bag);
            bag.Add(money, 5);
            var after = StudentDayQuestInventorySnapshot.Capture(null, null, bag);
            Assert.That(before.Items, Is.Empty);
            Assert.That(after.Items.Length, Is.EqualTo(1));
            Assert.That(after.Items[0].ItemId, Is.EqualTo("money"));
            Assert.That(after.Items[0].Count, Is.EqualTo(5));
        }
    }
}
