// 옮겨 옴: Assets/Tests/EditMode/Interiors/QuestInteriorUnlockRewardTests.cs(구매·해금·저장),
//          Assets/Tests/EditMode/Placement/FurniturePlacementSaveTests.cs(단계별 배치 파일)
using System;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Careers;
using Rootborn.Core.Inventory;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;
using Rootborn.Core.Quests;
using Rootborn.Core.Save;

namespace Rootborn.Core.Tests
{
    public sealed class FurnitureUnlockTests
    {
        private const string UnlockToken = "unlock.furniture.quest.library-chair";
        private const string SaleToken = "unlock.furniture.test.sale";
        private static readonly ItemDefinition Coin = new ItemDefinition("item.test-coin", 9999);

        private static FurnitureDefinition Piece(string id, string token = null, ItemDefinition priceItem = null, int price = 0, string location = null, string category = "Decor")
            => new FurnitureDefinition(id, id, null, category, new[] { "house" }, new[] { new FurnitureTilePart(new GridPoint(0, 0), "s.png", "s") }, null, true,
                FurniturePlacementPreference.Any, token, priceItem, price, location);

        private static FurnitureDefinition ForSale(int price) => Piece("sale", SaleToken, Coin, price, "location.test");

        // --- 구매 ---

        [Test]
        public void Purchase_ChargesOnceAndUnlocks()
        {
            var inventory = new ItemInventory();
            inventory.Add(Coin, 15);
            var unlocks = new FurnitureUnlockProgress("slot", "player");
            Assert.That(FurniturePurchase.Try(ForSale(10), inventory, unlocks), Is.EqualTo(FurniturePurchaseResult.Purchased));
            Assert.That(inventory.CountOf(Coin), Is.EqualTo(5));
            Assert.That(unlocks.IsUnlocked(SaleToken), Is.True);
            Assert.That(FurniturePurchase.Try(ForSale(10), inventory, unlocks), Is.EqualTo(FurniturePurchaseResult.AlreadyOwned));
            Assert.That(inventory.CountOf(Coin), Is.EqualTo(5), "이미 가진 가구는 다시 돈을 받지 않는다");
        }

        [Test]
        public void Purchase_WithoutEnoughMoney_ChangesNothing()
        {
            var inventory = new ItemInventory();
            inventory.Add(Coin, 9);
            var unlocks = new FurnitureUnlockProgress("slot", "player");
            Assert.That(FurniturePurchase.Try(ForSale(10), inventory, unlocks), Is.EqualTo(FurniturePurchaseResult.NotEnoughMoney));
            Assert.That(inventory.CountOf(Coin), Is.EqualTo(9));
            Assert.That(unlocks.IsUnlocked(SaleToken), Is.False);
        }

        [Test]
        public void Purchase_NotForSaleOrMissingOwner_ChangesNothing()
        {
            var inventory = new ItemInventory();
            inventory.Add(Coin, 20);
            var unlocks = new FurnitureUnlockProgress("slot", "player");
            Assert.That(FurniturePurchase.Try(Piece("plain"), inventory, unlocks), Is.EqualTo(FurniturePurchaseResult.NotForSale));
            Assert.That(FurniturePurchase.Try(Piece("no-place", SaleToken, Coin, 10), inventory, unlocks), Is.EqualTo(FurniturePurchaseResult.NotForSale));
            Assert.That(FurniturePurchase.Try(null, inventory, unlocks), Is.EqualTo(FurniturePurchaseResult.NotForSale));
            Assert.That(FurniturePurchase.Try(ForSale(10), null, unlocks), Is.EqualTo(FurniturePurchaseResult.Unavailable));
            Assert.That(FurniturePurchase.Try(ForSale(10), inventory, null), Is.EqualTo(FurniturePurchaseResult.Unavailable));
            Assert.That(inventory.CountOf(Coin), Is.EqualTo(20));
            Assert.That(unlocks.UnlockedCount, Is.Zero);
        }

        [Test]
        public void Purchase_SavedUnlock_DoesNotChargeAfterReload()
        {
            var inventory = new ItemInventory();
            inventory.Add(Coin, 20);
            var unlocks = new FurnitureUnlockProgress("slot", "player");
            FurniturePurchase.Try(ForSale(10), inventory, unlocks);
            var restored = FurnitureUnlockProgress.FromSaveData(unlocks.ToSaveData(), "slot", "player");
            Assert.That(FurniturePurchase.Try(ForSale(10), inventory, restored), Is.EqualTo(FurniturePurchaseResult.AlreadyOwned));
            Assert.That(inventory.CountOf(Coin), Is.EqualTo(10));
        }

        // --- 퀘스트 보상 ---

        [Test]
        public void UnlockReward_NeedsProgressAndUnlocksExactlyOnce()
        {
            var reward = new FurnitureUnlockQuestReward(UnlockToken);
            var missing = new RewardRuntimeContext(null, null, null, null, null, null, null);
            Assert.That(reward.CanApply(in missing), Is.False);

            var progress = new FurnitureUnlockProgress("slot-a", "player-a");
            var context = new RewardRuntimeContext(null, null, null, null, null, null, progress);
            Assert.That(reward.CanApply(in context), Is.True);
            reward.Apply(in context);
            Assert.That(progress.IsUnlocked(UnlockToken), Is.True);
            Assert.That(reward.CanApply(in context), Is.False);
            reward.Apply(in context);
            Assert.That(progress.UnlockedCount, Is.EqualTo(1));
        }

        // --- 해금 상태 ---

        [Test]
        public void FromSaveData_DropsDuplicateAndEmptyTokens_AndWritesThemSorted()
        {
            var restored = FurnitureUnlockProgress.FromSaveData(new FurnitureUnlockProgressSaveData
            {
                SaveSlot = "slot-furniture",
                PlayerId = "player-a",
                UnlockTokens = new[] { "b", UnlockToken, UnlockToken, string.Empty, null, "a" },
            }, "fallback-slot", "fallback-player");

            Assert.That(restored.UnlockedCount, Is.EqualTo(3));
            Assert.That(restored.SaveSlot, Is.EqualTo("slot-furniture"));
            Assert.That(restored.ToSaveData().UnlockTokens, Is.EqualTo(new[] { "a", "b", UnlockToken }));
        }

        [Test]
        public void FromSaveData_WithoutDataUsesTheFallbackOwner()
        {
            var restored = FurnitureUnlockProgress.FromSaveData(null, "slot", null);
            Assert.That(restored.SaveSlot, Is.EqualTo("slot"));
            Assert.That(restored.PlayerId, Is.EqualTo("player"));
            Assert.That(restored.UnlockedCount, Is.Zero);
        }

        [Test]
        public void RestoreAndMerge_RefuseAnotherOwnersData()
        {
            var progress = new FurnitureUnlockProgress("slot", "a");
            progress.TryUnlock("x");
            var other = new FurnitureUnlockProgressSaveData { SaveSlot = "slot", PlayerId = "b", UnlockTokens = new[] { "y" } };
            Assert.That(progress.RestoreFromSaveData(other), Is.False);
            Assert.That(progress.MergeFromSaveData(other), Is.False);
            Assert.That(progress.UnlockTokens, Is.EquivalentTo(new[] { "x" }));

            var mine = new FurnitureUnlockProgressSaveData { SaveSlot = "slot", PlayerId = "a", UnlockTokens = new[] { "y" } };
            Assert.That(progress.MergeFromSaveData(mine), Is.True);
            Assert.That(progress.UnlockTokens, Is.EquivalentTo(new[] { "x", "y" }));
            Assert.That(progress.RestoreFromSaveData(mine), Is.True);
            Assert.That(progress.UnlockTokens, Is.EquivalentTo(new[] { "y" }));
        }

        [Test]
        public void Catalog_HidesLockedFurniture_AndListsEarnedFurnitureFirst()
        {
            var catalog = new[]
            {
                Piece("z.plain", category: "A"), Piece("a.plain", category: "B"), Piece("quest.chair", UnlockToken, category: "C"),
                Piece("locked", "unlock.never", category: "A"), null,
            };
            var unlocks = new FurnitureUnlockProgress("slot", "player");
            Assert.That(FurnitureCatalog.Available(catalog, unlocks).Select(f => f.StableId), Is.EqualTo(new[] { "z.plain", "a.plain" }));
            Assert.That(FurnitureCatalog.Available(catalog, null).Select(f => f.StableId), Is.EqualTo(new[] { "z.plain", "a.plain" }));

            unlocks.TryUnlock(UnlockToken);
            Assert.That(FurnitureCatalog.Available(catalog, unlocks).Select(f => f.StableId), Is.EqualTo(new[] { "quest.chair", "z.plain", "a.plain" }));
        }

        // --- 세이브 ---

        [Test]
        public void UnlockFile_IsKeptPerSaveSlotAndPlayer()
        {
            var store = new InMemorySaveStore();
            var progress = new FurnitureUnlockProgress("slot-furniture", "player-a");
            progress.TryUnlock(UnlockToken);
            FurnitureSaveFiles.SaveUnlocks(store, progress);

            Assert.That(FurnitureSaveFiles.LoadUnlocksOrCreate(store, "slot-furniture", "player-a").IsUnlocked(UnlockToken), Is.True);
            Assert.That(FurnitureSaveFiles.LoadUnlocksOrCreate(store, "slot-furniture", "player-b").IsUnlocked(UnlockToken), Is.False);
            Assert.That(FurnitureSaveFiles.LoadUnlocksOrCreate(store, "other-slot", "player-a").IsUnlocked(UnlockToken), Is.False);
        }

        [Test]
        public void UnlockFileNames_MatchTheUnityGame()
        {
            Assert.That(SaveFileNames.FurnitureUnlocks(null), Is.EqualTo("furniture-unlocks.json"));
            Assert.That(SaveFileNames.FurnitureUnlocks("player"), Is.EqualTo("furniture-unlocks.json"));
            Assert.That(SaveFileNames.FurnitureUnlocks("local-player"), Is.EqualTo("furniture-unlocks.json"));
            Assert.That(SaveFileNames.FurnitureUnlocks("steam:7"), Is.EqualTo("furniture-unlocks-steam_7.json"));
        }

        [Test]
        public void Snapshot_CarriesTheUnlocks()
        {
            var progress = new FurnitureUnlockProgress("slot-1", "local-player");
            progress.TryUnlock("unlock.furniture.story.study-desk");
            var snapshot = new AdultCareerSnapshotSaveData { FurnitureUnlocks = progress.ToSaveData() };
            var restored = FurnitureUnlockProgress.FromSaveData(snapshot.FurnitureUnlocks, "slot-1", "local-player");
            Assert.That(restored.IsUnlocked("unlock.furniture.story.study-desk"), Is.True);
        }

        [Test]
        public void LayoutFiles_OfEachStageDoNotOverwriteEachOther()
        {
            var store = new InMemorySaveStore();
            FurnitureSaveFiles.SaveHouseLayout(store, "slot", 0, new[] { Record("chair", 1) });
            FurnitureSaveFiles.SaveHouseLayout(store, "slot", 1, new[] { Record("desk", 2), Record("lamp", 3) });

            Assert.That(FurnitureSaveFiles.LoadHouseLayout(store, "slot", 0).Single().FurnitureId, Is.EqualTo("chair"));
            Assert.That(FurnitureSaveFiles.LoadHouseLayout(store, "slot", 1).Select(r => r.FurnitureId), Is.EqualTo(new[] { "desk", "lamp" }));
            Assert.That(store.Contains("slot", "house-furniture-layout-stage-0.json"), Is.True);
            Assert.That(store.Contains("slot", "house-furniture-layout-stage-1.json"), Is.True);
        }

        [Test]
        public void MissingStageLayout_IsEmpty_AndNegativeStageIsStageZero()
        {
            var store = new InMemorySaveStore();
            Assert.That(FurnitureSaveFiles.LoadHouseLayout(store, "slot", 2), Is.Empty);
            Assert.That(SaveFileNames.HouseFurnitureLayout(-3), Is.EqualTo("house-furniture-layout-stage-0.json"));
        }

        [Test]
        public void StageZero_FallsBackToTheOldSingleLayoutFile_ButOtherStagesDoNot()
        {
            var store = new InMemorySaveStore();
            store.Put("slot", "house-furniture-layout.json", new FurniturePlacementLayoutSaveData { Items = new[] { Record("old", 1) } });
            Assert.That(FurnitureSaveFiles.LoadHouseLayout(store, "slot", 0).Single().FurnitureId, Is.EqualTo("old"));
            Assert.That(FurnitureSaveFiles.LoadHouseLayout(store, "slot", 1), Is.Empty);

            FurnitureSaveFiles.SaveHouseLayout(store, "slot", 0, new[] { Record("new", 1) });
            Assert.That(FurnitureSaveFiles.LoadHouseLayout(store, "slot", 0).Single().FurnitureId, Is.EqualTo("new"));
        }

        [Test]
        public void LayoutFileWithoutItems_IsNotTreatedAsAnEmptyRoom()
        {
            var store = new InMemorySaveStore();
            store.Put("slot", "house-furniture-layout-stage-0.json", new { Something = 1 });
            Assert.Throws<InvalidOperationException>(() => FurnitureSaveFiles.LoadHouseLayout(store, "slot", 0));
        }

        [Test]
        public void LayoutRecords_RoundTripTheirExtraFields_AndSkipNullItems()
        {
            var store = new InMemorySaveStore();
            var record = Record("chair", 4);
            record.StateJson = "{\"q\":1}";
            record.TileName = "t";
            record.Direction = FurniturePlacementDirection.West;
            FurnitureSaveFiles.SaveHouseLayout(store, "slot", 0, new[] { record, null });
            var read = FurnitureSaveFiles.LoadHouseLayout(store, "slot", 0).Single();
            Assert.That((read.AnchorX, read.AnchorY, read.Direction, read.StateJson, read.TileName), Is.EqualTo((4, -5, FurniturePlacementDirection.West, "{\"q\":1}", "t")));
        }

        private static FurniturePlacementSaveData Record(string id, int x)
            => new FurniturePlacementSaveData { FurnitureId = id, SurfaceId = "house", AnchorX = x, AnchorY = -5 };
    }
}
