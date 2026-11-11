using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Careers;
using Rootborn.Core.Inventory;
using Rootborn.Core.Placement;
using Rootborn.Core.Save;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 돈은 가방이 아니라 지갑에 있다(멀티플레이 설계 2.2절, 7절 2단계). 알바로 벌고, 가게에서 쓰고, 모자라면 거절되고,
    /// 가방이 찼을 때 물건과 함께 주는 보상은 돈도 들어오지 않는다. 같은 요청은 한 번만, 저장하고 다시 읽어도 같다.
    /// 옛 세이브(가방에 돈 칸)는 읽을 때 지갑으로 옮긴다. GameSession 명령으로 진행하는 자동 테스트다(화면 근거 아님).
    /// </summary>
    public sealed class WalletTests
    {
        private const string MoneyOnlyWork = "work.clinic-helper";   // 보상: 용돈만
        private const string MoneyAndWoodWork = "work.shift.corner-store"; // 보상: 나무 + 용돈
        private const string Shop = "location.corner-store";
        private const string OldSaveFolder = "wallet-v2/slot-1";

        private static ItemDefinition Money(GameSession game) => game.Definitions.Currency;

        private static int MoneySlots(GameSession game) => game.Inventory.Slots.Count(s => s.Item == Money(game) && s.Count > 0);

        private static AdultCareerSnapshotSaveData Snapshot(TempSaves saves, string slot = TestWorld.Slot)
        {
            Assert.That(saves.Store.TryRead<AdultCareerSnapshotSaveData>(slot, SaveFileNames.AdultCareerSnapshot(TestWorld.Player), out var snapshot), Is.True);
            return snapshot;
        }

        private static void Shift(GameSession game, string work, string request)
        {
            Assert.That(game.BeginPartTimeWork(work, request, 1).Applied, Is.True, request);
            Assert.That(game.SettleMinigame(request, true).Applied, Is.True, request);
        }

        [Test]
        public void TheGameCurrency_IsTheAllowanceItem()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            Assert.That(Money(game), Is.SameAs(game.Item("item.allowance")));
            Assert.That(game.Inventory.Currency, Is.SameAs(Money(game)));
            Assert.That(game.Wallet.Balance, Is.Zero, "새 게임은 빈 지갑");
        }

        [Test]
        public void PartTimeWork_EarnsIntoTheWallet_NotABagSlot()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            Shift(game, MoneyAndWoodWork, "work:1");

            Assert.That(game.Wallet.Balance, Is.EqualTo(5));
            Assert.That(game.Inventory.CountOf(Money(game)), Is.EqualTo(5), "용돈 조회는 지갑을 본다");
            Assert.That(MoneySlots(game), Is.Zero, "돈은 가방 칸을 쓰지 않는다");
            Assert.That(game.Inventory.CountOf(game.Item("Wood")), Is.EqualTo(3), "물건은 가방에");
        }

        [Test]
        public void TheShop_SpendsFromTheWallet_Once()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            var offer = game.FurnitureForSaleAt(Shop)[0];
            for (int i = 0; game.Wallet.Balance < offer.Price; i++) Shift(game, MoneyOnlyWork, "work:" + i);
            int before = game.Wallet.Balance;

            Assert.That(game.PurchaseFurniture(offer.StableId), Is.EqualTo(FurniturePurchaseResult.Purchased));
            Assert.That(game.Wallet.Balance, Is.EqualTo(before - offer.Price));
            Assert.That(game.PurchaseFurniture(offer.StableId), Is.EqualTo(FurniturePurchaseResult.AlreadyOwned));
            Assert.That(game.Wallet.Balance, Is.EqualTo(before - offer.Price), "다시 사도 값을 내지 않는다");
            Assert.That(TestWorld.Start(saves.Store).Wallet.Balance, Is.EqualTo(before - offer.Price), "다시 읽어도 같다");
        }

        [Test]
        public void NotEnoughMoney_IsRefused_AndNothingChanges()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            Shift(game, MoneyOnlyWork, "work:1");
            var offer = game.FurnitureForSaleAt(Shop)[0];
            Assume.That(offer.Price, Is.GreaterThan(5));

            Assert.That(game.PurchaseFurniture(offer.StableId), Is.EqualTo(FurniturePurchaseResult.NotEnoughMoney));
            Assert.That(game.Wallet.Balance, Is.EqualTo(5));
            Assert.That(game.FurnitureUnlocks.IsUnlocked(offer.UnlockToken), Is.False);
        }

        [Test]
        public void AFullBag_StillTakesAMoneyOnlyReward()
        {
            using var saves = new TempSaves();
            TestWorld.Start(saves.Store).TalkTo("npc.first-guide");
            var stone = Stone();
            SaveEdits.EditSnapshot(saves.Store, snapshot => SaveEdits.Fill(snapshot, "Stone", stone.MaxStack * ItemInventory.MaxSlots));
            var game = TestWorld.Start(saves.Store);
            Assert.That(game.Inventory.CanAdd(stone, 1), Is.False, "가방이 찼다");

            Shift(game, MoneyOnlyWork, "work:full");

            Assert.That(game.Wallet.Balance, Is.EqualTo(5), "돈은 칸이 필요 없다");
        }

        [Test]
        public void AFullBag_RefusesARewardOfItemsAndMoney_AndTheMoneyDoesNotComeAlone()
        {
            using var saves = new TempSaves();
            TestWorld.Start(saves.Store).TalkTo("npc.first-guide");
            var stone = Stone();
            SaveEdits.EditSnapshot(saves.Store, snapshot => SaveEdits.Fill(snapshot, "Stone", stone.MaxStack * ItemInventory.MaxSlots));
            var game = TestWorld.Start(saves.Store);
            var before = game.Student.ToSaveData();

            Assert.That(game.BeginPartTimeWork(MoneyAndWoodWork, "work:full", 1).Kind, Is.EqualTo(AdultCareerResultKind.InsufficientCapacity));

            Assert.That(game.Wallet.Balance, Is.Zero, "나무가 못 들어가면 돈도 들어오지 않는다");
            Assert.That(game.Inventory.CountOf(game.Item("Wood")), Is.Zero);
            Assert.That(game.Student.ToSaveData().Energy, Is.EqualTo(before.Energy));
            Assert.That(game.Student.HasPendingMinigame, Is.False);
        }

        [Test]
        public void TheSameRequestTwice_PaysOnce_EvenAfterSaveAndLoad()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            Shift(game, MoneyOnlyWork, "work:1");
            Assert.That(game.SettleMinigame("work:1", true).Applied, Is.False);
            Assert.That(game.BeginPartTimeWork(MoneyOnlyWork, "work:1", 1).Kind, Is.EqualTo(AdultCareerResultKind.DuplicateRequest));

            var again = TestWorld.Start(saves.Store);
            Assert.That(again.BeginPartTimeWork(MoneyOnlyWork, "work:1", 1).Kind, Is.EqualTo(AdultCareerResultKind.DuplicateRequest));
            Assert.That(again.Wallet.Balance, Is.EqualTo(5));
        }

        [Test]
        public void SaveAndLoad_KeepsTheBalance_InTheWallet_NotTheBag()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            Shift(game, MoneyAndWoodWork, "work:1");

            var snapshot = Snapshot(saves);
            Assert.That(snapshot.Version, Is.EqualTo(AdultCareerSnapshotSaveData.KnownVersion));
            Assert.That(snapshot.Wallet, Is.Not.Null);
            Assert.That(snapshot.Wallet.Version, Is.EqualTo(WalletSaveData.KnownVersion));
            Assert.That(snapshot.Wallet.Balance, Is.EqualTo(5));
            Assert.That(snapshot.Inventory.Items.Select(i => i.ItemId), Does.Not.Contain("item.allowance"), "가방 세이브에 돈 칸이 없다");

            var again = TestWorld.Start(saves.Store);
            Assert.That(again.Wallet.Balance, Is.EqualTo(5));
            Assert.That(MoneySlots(again), Is.Zero);
        }

        [Test]
        public void AnOldSave_WithMoneyInTheBag_MovesItToTheWallet_Once()
        {
            // 고정 파일: 스냅샷 버전 2(지갑 없음), 가방에 용돈 37 + 나무 3 + 용돈 8(칸 둘에 나뉜 돈).
            using var saves = new TempSaves();
            CopyOldSave(saves, "slot-1");
            var game = TestWorld.Start(saves.Store, "slot-1");

            Assert.That(game.Wallet.Balance, Is.EqualTo(45), "가방의 돈 칸 전부가 지갑으로");
            Assert.That(MoneySlots(game), Is.Zero, "가방의 돈 칸은 비운다");
            Assert.That(game.Inventory.CountOf(game.Item("Wood")), Is.EqualTo(3), "다른 물건은 그대로");

            Assert.That(game.VisitLocation("location.town-square"), Is.Not.Null); // 명령 하나로 저장한다.
            var written = Snapshot(saves, "slot-1");
            Assert.That(written.Version, Is.EqualTo(AdultCareerSnapshotSaveData.KnownVersion));
            Assert.That(written.Wallet.Balance, Is.EqualTo(45));
            Assert.That(written.Inventory.Items.Select(i => i.ItemId), Does.Not.Contain("item.allowance"));

            var again = TestWorld.Start(saves.Store, "slot-1");
            Assert.That(again.Wallet.Balance, Is.EqualTo(45), "다시 읽어도 두 배가 되지 않는다");
            Assert.That(again.Inventory.CountOf(again.Item("Wood")), Is.EqualTo(3));
        }

        [Test]
        public void TheBalanceChanged_TellsTheInventoryTopic()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            var topics = new System.Collections.Generic.List<string>();
            game.Changed += topics.Add;
            Shift(game, MoneyOnlyWork, "work:1");
            Assert.That(topics, Does.Contain(GameChangeTopics.Inventory), "용돈 표시(HUD·가방)가 다시 읽는다");
        }

        private static ItemDefinition Stone() => TestWorld.Definitions.Get<ItemDefinition>("Stone");

        private static void CopyOldSave(TempSaves saves, string slot)
        {
            var source = Path.Combine(System.AppContext.BaseDirectory, "Fixtures", OldSaveFolder);
            var target = Path.Combine(saves.Path, slot);
            Directory.CreateDirectory(target);
            foreach (var file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }
    }
}
