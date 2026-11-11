using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Careers;
using Rootborn.Core.Placement;
using Rootborn.Core.Save;

namespace Rootborn.Game.Tests
{
    /// <summary>가구 구매·해금·배치를 GameSession 명령으로 확인한다. 방 칸은 실제 집 맵에서 만든다(자동 테스트).</summary>
    public sealed class FurnitureTests
    {
        // Objective_MyRoom이 받는 가구 중 해금이 필요 없는 것.
        private const string FreeDesk = "growth31.assembly.desk";
        private const string Shop = "location.corner-store";

        private static GameSession StartWithAllowance(TempSaves saves, int allowance)
        {
            var game = TestWorld.Start(saves.Store);
            game.VisitLocation("location.town-square"); // 한 번 저장해 스냅샷을 만든다.
            SaveEdits.EditSnapshot(saves.Store, snapshot => SaveEdits.Fill(snapshot, "item.allowance", allowance));
            return TestWorld.Start(saves.Store);
        }

        [Test]
        public void TheCornerStoreSellsFurniture_AndBoughtFurnitureIsNotListedBeforeBuying()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            var offers = game.FurnitureForSaleAt(Shop);

            Assert.That(offers, Is.Not.Empty);
            Assert.That(offers.All(f => f.IsForSale && f.PurchaseLocationId == Shop), Is.True);
            Assert.That(game.AvailableFurniture().Any(f => f.StableId == offers[0].StableId), Is.False);
            Assert.That(game.AvailableFurniture().Any(f => f.StableId == FreeDesk), Is.True);
        }

        [Test]
        public void Purchase_ChargesTheAllowanceOnce_UnlocksThePiece_AndSurvivesReading()
        {
            using var saves = new TempSaves();
            var game = StartWithAllowance(saves, 25);
            var offer = game.FurnitureForSaleAt(Shop)[0];
            var allowance = game.Item("item.allowance");

            Assert.That(game.PurchaseFurniture(offer.StableId), Is.EqualTo(FurniturePurchaseResult.Purchased));
            Assert.That(game.Inventory.CountOf(allowance), Is.EqualTo(25 - offer.Price));
            Assert.That(game.FurnitureUnlocks.IsUnlocked(offer.UnlockToken), Is.True);
            Assert.That(game.AvailableFurniture()[0].StableId, Is.EqualTo(offer.StableId), "얻은 가구가 목록 맨 앞");

            Assert.That(game.PurchaseFurniture(offer.StableId), Is.EqualTo(FurniturePurchaseResult.AlreadyOwned));
            Assert.That(game.Inventory.CountOf(allowance), Is.EqualTo(25 - offer.Price));

            var again = TestWorld.Start(saves.Store);
            Assert.That(again.FurnitureUnlocks.IsUnlocked(offer.UnlockToken), Is.True);
            Assert.That(again.Inventory.CountOf(allowance), Is.EqualTo(25 - offer.Price));
            Assert.That(saves.Store.TryRead<AdultCareerSnapshotSaveData>(TestWorld.Slot, SaveFileNames.AdultCareerSnapshot(TestWorld.Player), out var snapshot), Is.True);
            Assert.That(snapshot.FurnitureUnlocks.UnlockTokens, Does.Contain(offer.UnlockToken), "해금은 스냅샷에 들어간다");
        }

        [Test]
        public void Purchase_WithoutEnoughMoney_ChangesNothingAndWritesNothing()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            var offer = game.FurnitureForSaleAt(Shop)[0];

            Assert.That(game.PurchaseFurniture(offer.StableId), Is.EqualTo(FurniturePurchaseResult.NotEnoughMoney));
            Assert.That(game.FurnitureUnlocks.IsUnlocked(offer.UnlockToken), Is.False);
            Assert.That(saves.Store.TryRead<AdultCareerSnapshotSaveData>(TestWorld.Slot, SaveFileNames.AdultCareerSnapshot(TestWorld.Player), out _), Is.False);
            Assert.That(game.PurchaseFurniture("no.such.furniture"), Is.EqualTo(FurniturePurchaseResult.NotForSale));
            Assert.That(game.PurchaseFurniture(FreeDesk), Is.EqualTo(FurniturePurchaseResult.NotForSale), "파는 가구가 아니다");
        }

        [Test]
        public void Placing_NeedsTheHouse_AndOnlyListedPieces()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            var offer = game.FurnitureForSaleAt(Shop)[0];
            Assert.That(game.PlaceFurniture(FreeDesk, new Rootborn.Core.Minigames.GridPoint(5, 5), FurniturePlacementDirection.North), Is.False, "집에 들어오기 전");

            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            var anchor = HouseRoom.FirstFreeAnchor(game, offer.StableId);
            Assert.That(game.PlaceFurniture(offer.StableId, anchor, FurniturePlacementDirection.North), Is.False, "사지 않은 가구");
            Assert.That(game.PlaceFurniture("no.such.furniture", anchor, FurniturePlacementDirection.North), Is.False);
            Assert.That(game.House.Placed, Is.Empty);
            Assert.That(saves.Store.TryRead<FurniturePlacementLayoutSaveData>(TestWorld.Slot, SaveFileNames.HouseFurnitureLayout(0), out _), Is.False);
        }

        [Test]
        public void PlacedFurniture_IsSavedToTheStageFile_AndComesBackWhenEnteringAgain()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            var anchor = HouseRoom.FirstFreeAnchor(game, FreeDesk);

            Assert.That(game.PlaceFurniture(FreeDesk, anchor, FurniturePlacementDirection.North), Is.True);
            Assert.That(saves.Store.TryRead<FurniturePlacementLayoutSaveData>(TestWorld.Slot, SaveFileNames.HouseFurnitureLayout(0), out var layout), Is.True);
            Assert.That(layout.Items.Select(i => i.FurnitureId), Is.EqualTo(new[] { FreeDesk }));

            var again = TestWorld.Start(saves.Store);
            Assert.That(again.House, Is.Null);
            again.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            Assert.That(again.House.Placed.Select(p => (p.Definition.StableId, p.Anchor)), Is.EqualTo(new[] { (FreeDesk, anchor) }));
        }

        [Test]
        public void MovingRemovingAndClearing_AreSaved()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            Assert.That(game.PlaceFurniture(FreeDesk, HouseRoom.FirstFreeAnchor(game, FreeDesk), FurniturePlacementDirection.North), Is.True);
            Assert.That(game.PlaceFurniture(FreeDesk, HouseRoom.FirstFreeAnchor(game, FreeDesk), FurniturePlacementDirection.North), Is.True);
            var first = game.House.Placed[0];
            var target = HouseRoom.FirstFreeAnchor(game, FreeDesk, 3);

            Assert.That(game.MoveFurniture(first.InstanceId, target, FurniturePlacementDirection.North), Is.True);
            Assert.That(game.MoveFurniture("missing#1", target, FurniturePlacementDirection.North), Is.False);
            var moved = TestWorld.Start(saves.Store);
            moved.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            Assert.That(moved.House.Placed[0].Anchor, Is.EqualTo(target), "옮긴 가구는 기록 안의 자리를 지킨다");

            Assert.That(moved.RemoveFurniture(moved.House.Placed[1].InstanceId), Is.True);
            var removed = TestWorld.Start(saves.Store);
            removed.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            Assert.That(removed.House.Placed.Count, Is.EqualTo(1));

            Assert.That(removed.ClearFurniture(), Is.True);
            var cleared = TestWorld.Start(saves.Store);
            cleared.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            Assert.That(cleared.House.Placed, Is.Empty);
        }

        [Test]
        public void TheHouseStageFromHouseState_ChoosesTheLayoutFile()
        {
            using var saves = new TempSaves();
            saves.Store.Write(TestWorld.Slot, SaveFileNames.HouseState, new HouseStateSaveData { CurrentStageIndex = 2 });
            var game = TestWorld.Start(saves.Store);
            Assert.That(game.HouseStage, Is.EqualTo(2));
            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);

            Assert.That(game.PlaceFurniture(FreeDesk, HouseRoom.FirstFreeAnchor(game, FreeDesk), FurniturePlacementDirection.North), Is.True);
            Assert.That(saves.Store.TryRead<FurniturePlacementLayoutSaveData>(TestWorld.Slot, SaveFileNames.HouseFurnitureLayout(2), out _), Is.True);
            Assert.That(saves.Store.TryRead<FurniturePlacementLayoutSaveData>(TestWorld.Slot, SaveFileNames.HouseFurnitureLayout(0), out _), Is.False);
        }

        [Test]
        public void UnlocksFromAnOlderSlotWithoutASnapshot_AreReadFromTheUnlockFile()
        {
            using var saves = new TempSaves();
            saves.Store.Write(TestWorld.Slot, SaveFileNames.FurnitureUnlocks(TestWorld.Player),
                new FurnitureUnlockProgressSaveData { SaveSlot = TestWorld.Slot, PlayerId = TestWorld.Player, UnlockTokens = new[] { "unlock.furniture.story.photo-board" } });

            var game = TestWorld.Start(saves.Store);
            Assert.That(game.FurnitureUnlocks.IsUnlocked("unlock.furniture.story.photo-board"), Is.True);
            game.VisitLocation("location.town-square");
            Assert.That(TestWorld.Start(saves.Store).FurnitureUnlocks.IsUnlocked("unlock.furniture.story.photo-board"), Is.True, "스냅샷으로 넘어간다");
        }
    }
}
