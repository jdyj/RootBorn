using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Building;
using Rootborn.Core.Inventory;
using Rootborn.Core.Placement;

namespace Rootborn.Core.Tests.Building
{
    /// <summary>
    /// 잠긴 꾸밈을 가게에서 산다: 값을 내고 해금 토큰을 얻는다. 둘은 함께 바뀌고, 실패하면 아무것도 바뀌지 않는다(가구 사기와 같은 뜻).
    /// 생성자로 만든 정의, 메모리 가방. 자동 테스트.
    /// </summary>
    public sealed class RoomFinishPurchaseTests
    {
        private const string Token = "unlock.finish.test";
        private const string Shop = "location.test-shop";
        private static readonly ItemDefinition Coin = new ItemDefinition("item.test-coin", 9999);

        private static RoomFinishDefinition ForSale(int price = 15, string token = Token, string priceItem = "item.test-coin", string location = Shop) =>
            new RoomFinishDefinition("finish.test", RoomFinishSlots.Wall, "k", "s.png", "a", new[] { new ItemAmount<string>(Coin.Id, 2) }, token,
                priceItem, price, location);

        private static ItemInventory Wallet(int coins)
        {
            var inventory = new ItemInventory();
            if (coins > 0) inventory.Add(Coin, coins);
            return inventory;
        }

        [Test]
        public void SaleFields_ComeFromTheConstructor_AndAreEmptyByDefault()
        {
            var sale = ForSale();
            Assert.That((sale.PriceItemId, sale.Price, sale.PurchaseLocationId, sale.IsForSale), Is.EqualTo((Coin.Id, 15, Shop, true)));
            var plain = new RoomFinishDefinition("f", RoomFinishSlots.Wall, "k", "s.png", "a", null, Token);
            Assert.That((plain.PriceItemId, plain.Price, plain.PurchaseLocationId, plain.IsForSale), Is.EqualTo(("", 0, "", false)));
        }

        [TestCase(0, Token, "item.test-coin", Shop)]
        [TestCase(-5, Token, "item.test-coin", Shop)]
        [TestCase(15, "", "item.test-coin", Shop)]
        [TestCase(15, Token, "", Shop)]
        [TestCase(15, Token, "item.test-coin", "")]
        public void MissingSaleField_IsNotForSale(int price, string token, string priceItem, string location) =>
            Assert.That(ForSale(price, token, priceItem, location).IsForSale, Is.False);

        [Test]
        public void Purchased_ChargesThePrice_AndUnlocksTheToken()
        {
            var inventory = Wallet(20);
            var unlocks = new FurnitureUnlockProgress("slot", "player");
            Assert.That(RoomFinishPurchase.Try(ForSale(), Coin, inventory, unlocks), Is.EqualTo(FurniturePurchaseResult.Purchased));
            Assert.That(inventory.CountOf(Coin), Is.EqualTo(5));
            Assert.That(unlocks.IsUnlocked(Token), Is.True);
            Assert.That(HouseFinishes.IsUnlocked(ForSale(), unlocks), Is.True, "산 꾸밈은 쓸 수 있다");
        }

        [Test]
        public void NotEnoughMoney_ChangesNothing()
        {
            var inventory = Wallet(14);
            var unlocks = new FurnitureUnlockProgress("slot", "player");
            Assert.That(RoomFinishPurchase.Try(ForSale(), Coin, inventory, unlocks), Is.EqualTo(FurniturePurchaseResult.NotEnoughMoney));
            Assert.That(inventory.CountOf(Coin), Is.EqualTo(14));
            Assert.That(unlocks.UnlockedCount, Is.Zero);
        }

        [Test]
        public void AlreadyOwned_CostsNothing()
        {
            var inventory = Wallet(20);
            var unlocks = new FurnitureUnlockProgress("slot", "player");
            unlocks.TryUnlock(Token);
            Assert.That(RoomFinishPurchase.Try(ForSale(), Coin, inventory, unlocks), Is.EqualTo(FurniturePurchaseResult.AlreadyOwned));
            Assert.That(inventory.CountOf(Coin), Is.EqualTo(20));
            Assert.That(unlocks.UnlockTokens.ToArray(), Is.EqualTo(new[] { Token }));
        }

        [Test]
        public void SameRequestTwice_SecondIsAlreadyOwned_AndChargesOnce()
        {
            var inventory = Wallet(40);
            var unlocks = new FurnitureUnlockProgress("slot", "player");
            Assert.That(RoomFinishPurchase.Try(ForSale(), Coin, inventory, unlocks), Is.EqualTo(FurniturePurchaseResult.Purchased));
            Assert.That(RoomFinishPurchase.Try(ForSale(), Coin, inventory, unlocks), Is.EqualTo(FurniturePurchaseResult.AlreadyOwned));
            Assert.That(inventory.CountOf(Coin), Is.EqualTo(25), "값은 한 번만");
            Assert.That(unlocks.UnlockedCount, Is.EqualTo(1));
        }

        [Test]
        public void NotForSale_ChangesNothing()
        {
            var inventory = Wallet(40);
            var unlocks = new FurnitureUnlockProgress("slot", "player");
            Assert.That(RoomFinishPurchase.Try(ForSale(price: 0), Coin, inventory, unlocks), Is.EqualTo(FurniturePurchaseResult.NotForSale));
            Assert.That(RoomFinishPurchase.Try(ForSale(location: ""), Coin, inventory, unlocks), Is.EqualTo(FurniturePurchaseResult.NotForSale));
            Assert.That(RoomFinishPurchase.Try(null, Coin, inventory, unlocks), Is.EqualTo(FurniturePurchaseResult.NotForSale));
            Assert.That(inventory.CountOf(Coin), Is.EqualTo(40));
            Assert.That(unlocks.UnlockedCount, Is.Zero);
        }

        [Test]
        public void MissingOrWrongPriceItem_OrOwner_IsUnavailable_AndChangesNothing()
        {
            var inventory = Wallet(40);
            var unlocks = new FurnitureUnlockProgress("slot", "player");
            var other = new ItemDefinition("item.other", 9999);
            Assert.That(RoomFinishPurchase.Try(ForSale(), null, inventory, unlocks), Is.EqualTo(FurniturePurchaseResult.Unavailable));
            Assert.That(RoomFinishPurchase.Try(ForSale(), other, inventory, unlocks), Is.EqualTo(FurniturePurchaseResult.Unavailable), "정의가 말하는 아이템이 아니다");
            Assert.That(RoomFinishPurchase.Try(ForSale(), Coin, null, unlocks), Is.EqualTo(FurniturePurchaseResult.Unavailable));
            Assert.That(RoomFinishPurchase.Try(ForSale(), Coin, inventory, null), Is.EqualTo(FurniturePurchaseResult.Unavailable));
            Assert.That(inventory.CountOf(Coin), Is.EqualTo(40));
            Assert.That(unlocks.UnlockedCount, Is.Zero);
        }

        [Test]
        public void ForSaleAt_ListsOnlyFinishesSoldThere_InOrder()
        {
            var a = ForSale();
            var b = new RoomFinishDefinition("finish.b", RoomFinishSlots.Floor, "k", "s.png", "b", null, "unlock.b", Coin.Id, 3, Shop);
            var elsewhere = new RoomFinishDefinition("finish.c", RoomFinishSlots.Floor, "k", "s.png", "c", null, "unlock.c", Coin.Id, 3, "location.other");
            var free = new RoomFinishDefinition("finish.d", RoomFinishSlots.Floor, "k", "s.png", "d", null, "");
            var all = new[] { a, elsewhere, free, null, b };
            Assert.That(RoomFinishPurchase.ForSaleAt(all, Shop), Is.EqualTo(new[] { a, b }));
            Assert.That(RoomFinishPurchase.ForSaleAt(all, ""), Is.Empty);
            Assert.That(RoomFinishPurchase.ForSaleAt(null, Shop), Is.Empty);
        }
    }
}
