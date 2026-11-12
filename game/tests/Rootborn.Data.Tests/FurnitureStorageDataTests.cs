using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Inventory;
using Rootborn.Core.Placement;
using Rootborn.Data.Builders;
using Rootborn.Data.Save;

namespace Rootborn.Data.Tests
{
    /// <summary>상자 데이터(data/interiors/FurnitureStorageDefinition.json): 상자 가구는 처음부터 쓸 수 있고, 칸 수는 가방보다 넉넉하다. 내용물이 세이브에 남는다.</summary>
    public sealed class FurnitureStorageDataTests
    {
        [Test]
        public void TheStorageCrate_IsAFurnitureAvailableFromTheStart_WithMoreSlotsThanTheBag()
        {
            var result = GameDataLoader.Load(RepoPaths.DataDirectory, RootbornBuilders.Register);
            result.ThrowIfFailed();
            var storage = result.Definitions.All<FurnitureStorageDefinition>();
            Assert.That(storage, Is.Not.Empty, "상자가 하나 이상 있다");
            foreach (var s in storage)
            {
                Assert.That(s.Furniture.UnlockToken, Is.Empty, s.Id + ": 처음부터 놓을 수 있다");
                Assert.That(s.Furniture.AllowedSurfaceIds.Count == 0 || s.Furniture.AllowsSurface("house"), Is.True, s.Id + ": 집에 놓인다");
                Assert.That(s.Slots, Is.GreaterThanOrEqualTo(ItemInventory.MaxSlots), s.Id + ": 넉넉한 칸");
            }

            Assert.That(storage.Select(s => s.Furniture.StableId), Is.Unique);
        }

        [Test]
        public void ChestContents_SurviveTheSaveFile_AndAnOldVersion1LayoutStillReads()
        {
            using var dir = new TempDir();
            var store = new FileSaveStore(dir.Path);
            var record = new FurniturePlacementSaveData
            {
                FurnitureId = "studentlife.snack.delivery.crate", SurfaceId = "house", AnchorX = 2, AnchorY = 3,
                Storage = new FurnitureStorageSaveData
                {
                    Items = new[] { new PlayerInventoryItemSaveData { ItemId = "Wood", Count = 7 } }, Requests = new[] { "r1" },
                },
            };
            FurnitureSaveFiles.SaveHouseLayout(store, "slot", 0, new[] { record });
            var read = FurnitureSaveFiles.LoadHouseLayout(store, "slot", 0).Single();
            Assert.That(read.Storage.Items.Single().Count, Is.EqualTo(7));
            Assert.That(read.Storage.Requests, Is.EqualTo(new[] { "r1" }));
            Assert.That(System.IO.File.ReadAllText(System.IO.Path.Combine(dir.Path, "slot", SaveFileNames(0))), Does.Contain("\"Version\": 2").Or.Contain("\"Version\":2"));

            // 상자 필드가 없던 버전 1 파일
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir.Path, "slot", SaveFileNames(0)),
                "{\"Version\":1,\"Items\":[{\"FurnitureId\":\"chair.black\",\"SurfaceId\":\"house\",\"AnchorX\":1,\"AnchorY\":1,\"Direction\":0,\"StateJson\":\"\",\"TileName\":\"\"}]}");
            var old = FurnitureSaveFiles.LoadHouseLayout(store, "slot", 0).Single();
            Assert.That(old.FurnitureId, Is.EqualTo("chair.black"));
            Assert.That(FurnitureStorageSaveData.HoldsItems(old.Storage), Is.False);
        }

        private static string SaveFileNames(int stage) => Rootborn.Core.Save.SaveFileNames.HouseFurnitureLayout(stage);
    }
}
