using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Building;
using Rootborn.Core.Careers;
using Rootborn.Core.Placement;
using Rootborn.Core.Save;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 잠긴 꾸밈을 동네 가게에서 산다(가구와 같은 판매 구조, 같은 해금 토큰 기록). 사면 꾸미기 화면의 카드를 쓸 수 있다.
    /// 실제 data/, 임시 폴더 저장소. GameSession 명령으로 진행하는 자동 테스트다(화면 근거 아님).
    /// </summary>
    public sealed class HouseFinishShopTests
    {
        private const string Shop = "location.corner-store";
        private const string Allowance = "item.allowance";

        private static GameSession StartWithAllowance(TempSaves saves, int allowance)
        {
            var game = TestWorld.Start(saves.Store);
            game.VisitLocation("location.town-square"); // 한 번 저장해 스냅샷을 만든다.
            SaveEdits.EditSnapshot(saves.Store, snapshot => SaveEdits.Fill(snapshot, Allowance, allowance));
            return TestWorld.Start(saves.Store);
        }

        private static int Money(GameSession game) => game.Inventory.CountOf(game.Item(Allowance));

        private static AdultCareerSnapshotSaveData Snapshot(TempSaves saves)
        {
            Assert.That(saves.Store.TryRead<AdultCareerSnapshotSaveData>(TestWorld.Slot, SaveFileNames.AdultCareerSnapshot(TestWorld.Player), out var snapshot), Is.True);
            return snapshot;
        }

        [Test]
        public void ShopListsLockedFinishes_BuyUnlocks_CardBecomesUsable()
        {
            using var saves = new TempSaves();
            var game = StartWithAllowance(saves, 100);
            var offers = game.FinishesForSaleAt(Shop);
            var locked = TestWorld.Definitions.All<RoomFinishDefinition>().Where(f => f.UnlockToken.Length > 0).ToList();

            Assert.That(locked, Is.Not.Empty);
            Assert.That(offers.Select(f => f.Id), Is.EqualTo(locked.Select(f => f.Id)), "잠긴 꾸밈이 모두 가게에 데이터 순서로");
            Assert.That(offers.All(f => f.IsForSale && f.PurchaseLocationId == Shop), Is.True);
            Assert.That(game.FinishesForSaleAt("location.school"), Is.Empty);

            var offer = offers[0];
            Assert.That(game.IsFinishUnlocked(offer), Is.False, "사기 전에는 잠겨 있다");
            int before = Money(game);
            Assert.That(game.PurchaseFinish(offer.Id), Is.EqualTo(FurniturePurchaseResult.Purchased));
            Assert.That(Money(game), Is.EqualTo(before - offer.Price));
            Assert.That(game.FurnitureUnlocks.IsUnlocked(offer.UnlockToken), Is.True);
            Assert.That(game.IsFinishUnlocked(offer), Is.True, "산 꾸밈의 카드를 쓸 수 있다");
            Assert.That(offers.Skip(1).All(f => !game.IsFinishUnlocked(f)), Is.True, "다른 꾸밈은 그대로 잠겨 있다");
        }

        [Test]
        public void NotEnoughMoney_ChangesNothingAndWritesNothing_AndUnknownOrFreeIsNotForSale()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            var offer = game.FinishesForSaleAt(Shop)[0];
            Assume.That(Money(game), Is.LessThan(offer.Price), "새 게임 용돈으로는 살 수 없다");
            int before = Money(game);

            Assert.That(game.PurchaseFinish(offer.Id), Is.EqualTo(FurniturePurchaseResult.NotEnoughMoney));
            Assert.That(Money(game), Is.EqualTo(before));
            Assert.That(game.FurnitureUnlocks.IsUnlocked(offer.UnlockToken), Is.False);
            Assert.That(saves.Store.TryRead<AdultCareerSnapshotSaveData>(TestWorld.Slot, SaveFileNames.AdultCareerSnapshot(TestWorld.Player), out _), Is.False, "거절은 쓰지 않는다");

            var free = TestWorld.Definitions.All<RoomFinishDefinition>().First(f => f.UnlockToken.Length == 0);
            Assert.That(game.PurchaseFinish(free.Id), Is.EqualTo(FurniturePurchaseResult.NotForSale), "열린 꾸밈은 팔지 않는다");
            Assert.That(game.PurchaseFinish("finish.no-such"), Is.EqualTo(FurniturePurchaseResult.NotForSale));
            Assert.That(Money(game), Is.EqualTo(before));
        }

        [Test]
        public void SaveAndNewSession_StillUnlocked_SecondBuyAlreadyOwned()
        {
            using var saves = new TempSaves();
            var game = StartWithAllowance(saves, 100);
            var offer = game.FinishesForSaleAt(Shop)[0];
            Assert.That(game.PurchaseFinish(offer.Id), Is.EqualTo(FurniturePurchaseResult.Purchased));
            int after = Money(game);
            Assert.That(game.PurchaseFinish(offer.Id), Is.EqualTo(FurniturePurchaseResult.AlreadyOwned), "같은 요청을 다시 하면 이미 가짐");
            Assert.That(Money(game), Is.EqualTo(after));

            var again = TestWorld.Start(saves.Store);
            Assert.That(Money(again), Is.EqualTo(after), "산 값이 저장됐다");
            Assert.That(again.IsFinishUnlocked(again.FinishesForSaleAt(Shop)[0]), Is.True, "새 판에서도 쓸 수 있다");
            Assert.That(again.PurchaseFinish(offer.Id), Is.EqualTo(FurniturePurchaseResult.AlreadyOwned));
            Assert.That(Money(again), Is.EqualTo(after), "다시 읽은 뒤 다시 사도 값을 받지 않는다");
        }

        [Test]
        public void UnlockTokenSurvivesOlderShape()
        {
            using var saves = new TempSaves();
            var game = StartWithAllowance(saves, 100);
            var offer = game.FinishesForSaleAt(Shop)[0];
            Assert.That(game.PurchaseFinish(offer.Id), Is.EqualTo(FurniturePurchaseResult.Purchased));

            // 토큰은 있는 모양(가구 해금 토큰 글 목록) 그대로 스냅샷에 든다 — 세이브 모양이 늘지 않는다.
            var written = Snapshot(saves).FurnitureUnlocks;
            Assert.That(written.UnlockTokens, Does.Contain(offer.UnlockToken));

            // 옛 빌드처럼 그 모양만 읽고 다시 써도 토큰이 남는다(해금 기록은 정의로 거르지 않는다).
            var reread = FurnitureUnlockProgress.FromSaveData(written, TestWorld.Slot, TestWorld.Player).ToSaveData();
            Assert.That(reread.UnlockTokens, Is.EqualTo(written.UnlockTokens));

            // 다른 일로 다시 저장해도 남는다.
            var again = TestWorld.Start(saves.Store);
            again.VisitLocation("location.town-square");
            Assert.That(Snapshot(saves).FurnitureUnlocks.UnlockTokens, Does.Contain(offer.UnlockToken));

            // 옛 빌드에는 그 토큰을 쓰는 가구가 없어 아무것도 열리지 않는다.
            Assert.That(TestWorld.Definitions.All<FurnitureDefinition>().Any(f => f.UnlockToken == offer.UnlockToken), Is.False);
        }
    }
}
