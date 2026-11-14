using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Placement;
using Rootborn.Core.Save;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 상자(혼자 하기): 실제 data/의 상자 가구를 실제 집 맵에 놓고 GameSession 명령으로 넣고 꺼낸다. 새 GameSession으로 다시 읽어 같은지 본다.
    /// 화면 근거가 아니다(자동 테스트). 화면과 입력으로는 Host StorageChestHostTests.
    /// </summary>
    public sealed class StorageSessionTests
    {
        private static string ChestFurniture => TestWorld.Definitions.All<FurnitureStorageDefinition>().First().Furniture.StableId;

        private static (GameSession game, string chest) StartWithChest(TempSaves saves)
        {
            var game = TestWorld.Start(saves.Store);
            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            Assert.That(game.AvailableFurniture().Any(f => f.StableId == ChestFurniture), Is.True, "상자는 처음부터 놓을 수 있다");
            Assert.That(game.PlaceFurniture(ChestFurniture, HouseRoom.FirstFreeAnchor(game, ChestFurniture), FurniturePlacementDirection.North), Is.True);
            var placed = game.House.Placed.Single(p => p.Definition.StableId == ChestFurniture);
            Assert.That(game.IsStorage(placed.Definition), Is.True);
            return (game, placed.InstanceId);
        }

        private static GameSession Reload(TempSaves saves)
        {
            var game = TestWorld.Start(saves.Store);
            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            return game;
        }

        private static string ChestId(GameSession game) => game.House.Placed.Single(p => p.Definition.StableId == ChestFurniture).InstanceId;

        [Test]
        public void PutAndTake_ChangeBagAndChestTogether_AndANewSessionReadsTheSame()
        {
            using var saves = new TempSaves();
            var (game, chest) = StartWithChest(saves);
            var wood = game.Item("Wood");
            game.Inventory.Add(wood, 9);
            var storageChanges = 0;
            game.Changed += t => { if (t == GameChangeTopics.Storage) storageChanges++; };

            Assert.That(game.PutIntoStorage(chest, "Wood", 6), Is.EqualTo(StorageTransferResult.Moved));
            Assert.That(game.Inventory.CountOf(wood), Is.EqualTo(3));
            Assert.That(game.StorageAt(chest).CountOf(wood), Is.EqualTo(6));
            Assert.That(storageChanges, Is.EqualTo(1));

            var again = Reload(saves);
            string id = ChestId(again);
            Assert.That(again.Inventory.CountOf(wood), Is.EqualTo(3), "가방이 저장됐다");
            Assert.That(again.StorageAt(id).CountOf(wood), Is.EqualTo(6), "상자도 같은 묶음으로 저장됐다");

            Assert.That(again.TakeFromStorage(id, "Wood", 4), Is.EqualTo(StorageTransferResult.Moved));
            var third = Reload(saves);
            Assert.That(third.Inventory.CountOf(wood), Is.EqualTo(7));
            Assert.That(third.StorageAt(ChestId(third)).CountOf(wood), Is.EqualTo(2));
        }

        [Test]
        public void SameRequestId_AfterReload_IsAppliedOnce()
        {
            using var saves = new TempSaves();
            var (game, chest) = StartWithChest(saves);
            var wood = game.Item("Wood");
            game.Inventory.Add(wood, 5);
            Assert.That(game.PutIntoStorage(chest, "Wood", 2, "req-1"), Is.EqualTo(StorageTransferResult.Moved));
            Assert.That(game.PutIntoStorage(chest, "Wood", 2, "req-1"), Is.EqualTo(StorageTransferResult.AlreadyDone));

            var again = Reload(saves);
            Assert.That(again.PutIntoStorage(ChestId(again), "Wood", 2, "req-1"), Is.EqualTo(StorageTransferResult.AlreadyDone));
            Assert.That(again.Inventory.CountOf(wood), Is.EqualTo(3));
            Assert.That(again.StorageAt(ChestId(again)).CountOf(wood), Is.EqualTo(2));
        }

        [Test]
        public void Refusals_ChangeNothing_AndWriteNothing()
        {
            using var saves = new TempSaves();
            var (game, chest) = StartWithChest(saves);
            var money = game.Inventory.Currency;
            Assert.That(money, Is.Not.Null);
            Assert.That(game.PutIntoStorage(chest, money.Id, 1), Is.EqualTo(StorageTransferResult.Currency), "돈은 넣지 않는다");
            Assert.That(game.PutIntoStorage(chest, "Wood", 1), Is.EqualTo(StorageTransferResult.NotEnough));
            Assert.That(game.TakeFromStorage(chest, "Wood", 1), Is.EqualTo(StorageTransferResult.NotEnough));
            Assert.That(game.LastStorageResult, Is.EqualTo(StorageTransferResult.NotEnough));

            string other = game.AvailableFurniture().First(f => !game.IsStorage(f)).StableId;
            Assert.That(game.PlaceFurniture(other, HouseRoom.FirstFreeAnchor(game, other), FurniturePlacementDirection.North), Is.True);
            string notChest = game.House.Placed.First(p => !game.IsStorage(p.Definition)).InstanceId;
            Assert.That(game.StorageAt(notChest), Is.Null);
            Assert.That(game.PutIntoStorage(notChest, "Wood", 1), Is.EqualTo(StorageTransferResult.NotStorage));
        }

        [Test]
        public void AChestWithItems_CannotBeRemoved_MovingKeepsItems_EmptyCanBeRemoved()
        {
            using var saves = new TempSaves();
            var (game, chest) = StartWithChest(saves);
            var stone = game.Item("Stone");
            game.Inventory.Add(stone, 4);
            game.PutIntoStorage(chest, "Stone", 4);

            Assert.That(game.RemoveFurniture(chest), Is.False, "물건이 든 상자");
            Assert.That(game.House.Placed.Single(p => p.InstanceId == chest).HoldsItems, Is.True);
            var furniture = game.Definitions.Get<FurnitureDefinition>(ChestFurniture);
            var target = HouseRoom.FirstFreeAnchor(game, ChestFurniture, 5);
            Assert.That(game.MoveFurniture(chest, target, FurniturePlacementDirection.North), Is.True);

            var again = Reload(saves);
            string id = ChestId(again);
            Assert.That(again.House.Placed.Single(p => p.InstanceId == id).Anchor, Is.EqualTo(target));
            Assert.That(again.StorageAt(id).CountOf(stone), Is.EqualTo(4), "옮겨도 내용물이 따라갔다");

            Assert.That(again.TakeFromStorage(id, "Stone", 4), Is.EqualTo(StorageTransferResult.Moved));
            Assert.That(again.RemoveFurniture(id), Is.True, "비우면 치운다");
            Assert.That(furniture, Is.Not.Null);
        }

        [Test]
        public void AnOldSlotWithoutChests_StillReads()
        {
            using var saves = new TempSaves();
            HouseRoom.EmptyHouse(saves.Store); // 준비: 처음 배치 가구 없이(놓은 가구 하나만 보려고)
            var game = TestWorld.Start(saves.Store);
            game.EnterHouse(HouseRoom.Grid, HouseRoom.SurfaceId);
            string desk = game.AvailableFurniture().First(f => !game.IsStorage(f)).StableId;
            game.PlaceFurniture(desk, HouseRoom.FirstFreeAnchor(game, desk), FurniturePlacementDirection.North);
            // 상자 필드가 생기기 전(버전 1)의 배치 파일로 되돌린다.
            var path = System.IO.Path.Combine(saves.Path, TestWorld.Slot, SaveFileNames.HouseFurnitureLayout(0));
            string text = System.IO.File.ReadAllText(path).Replace("\"Version\": 2", "\"Version\": 1").Replace("\"Version\":2", "\"Version\":1");
            System.IO.File.WriteAllText(path, text);

            var again = Reload(saves);
            Assert.That(again.House.Placed.Single().Definition.StableId, Is.EqualTo(desk));
        }

        [Test]
        public void ManyMoves_KeepTheItemTotal()
        {
            using var saves = new TempSaves();
            var (game, chest) = StartWithChest(saves);
            var wood = game.Item("Wood");
            var stone = game.Item("Stone");
            game.Inventory.Add(wood, 40);
            game.Inventory.Add(stone, 30);
            var rng = new System.Random(3);
            for (int i = 0; i < 60; i++)
            {
                string item = rng.Next(2) == 0 ? "Wood" : "Stone";
                int count = 1 + rng.Next(15);
                if (rng.Next(2) == 0) game.PutIntoStorage(chest, item, count);
                else game.TakeFromStorage(chest, item, count);
            }

            var again = Reload(saves);
            var contents = again.StorageAt(ChestId(again));
            Assert.That(again.Inventory.CountOf(wood) + contents.CountOf(wood), Is.EqualTo(40));
            Assert.That(again.Inventory.CountOf(stone) + contents.CountOf(stone), Is.EqualTo(30));
        }
    }
}
