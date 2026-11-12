// 옮겨 옴: Assets/Scripts/Game/Save/SaveService.cs, SaveSlotMetadata.cs, Assets/Scripts/Game/World/WorldEntryService.cs, WorldPortal.cs(WorldProgressSaveData)
using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Core.Save;
using Rootborn.Core.World;

namespace Rootborn.Core.Tests
{
    public sealed class WorldSlotTests
    {
        private const long Now = 639300000000000000;

        private static WorldTravelCatalog Catalog() => new WorldTravelCatalog(new[]
        {
            new WorldDestination("House", new[] { "house-entry" }),
            new WorldDestination("Town", new[] { "town-home-entry", "town-shop-entry" }),
            new WorldDestination("ShopInterior", new[] { "shop-entry" }),
        }, "House", "house-entry");

        [Test]
        public void WORLD_SLOT_001_FileNamesAreTheUnityOnes()
        {
            Assert.That(SaveFileNames.Metadata, Is.EqualTo("metadata.json"));
            Assert.That(SaveFileNames.WorldProgress, Is.EqualTo("world-progress.json"));
            Assert.That(SaveFileNames.Wearables(SaveFileNames.DefaultPlayerId), Is.EqualTo("wearables-local-player.json"));
            Assert.That(SaveFileNames.Wearables("친구 1"), Is.EqualTo("wearables-친구_1.json"), "Unity WearableWardrobeStore.FileNameFor: 글자·숫자·-·_ 밖은 _");
        }

        [Test]
        public void WORLD_SLOT_002_CatalogIsValidOnlyWhenTheNewGameSpawnIsListed()
        {
            Assert.That(Catalog().IsValid, Is.True);
            Assert.That(new WorldTravelCatalog(new[] { new WorldDestination("House", new[] { "house-entry" }) }, "House", "elsewhere").IsValid, Is.False);
            Assert.That(new WorldTravelCatalog(new[] { new WorldDestination("House", new[] { "a", "a" }) }, "House", "a").IsValid, Is.False, "겹치는 도착 지점");
            Assert.That(new WorldTravelCatalog(new[] { new WorldDestination("House", new[] { "a" }), new WorldDestination("House", new[] { "b" }) }, "House", "a").IsValid, Is.False, "겹치는 장면");
            Assert.That(WorldEntryRules.ResolveNewGame(new WorldTravelCatalog(null, "House", "house-entry")), Is.Null);
        }

        [Test]
        public void WORLD_SLOT_003_ContinueFollowsTheUnityVersionAndSlotChecks()
        {
            WorldTravelCatalog catalog = Catalog();
            Assert.That(WorldEntryRules.ResolveContinue(catalog, "slot-0", null), Is.EqualTo(new WorldEntryPoint("House", "house-entry")), "파일이 없으면 새 게임 자리");
            var saved = new WorldProgressSaveData { Version = 1, SaveSlot = "slot-0", CurrentScene = "Town", CurrentEntryPointId = "town-shop-entry" };
            Assert.That(WorldEntryRules.ResolveContinue(catalog, "slot-0", saved), Is.EqualTo(new WorldEntryPoint("Town", "town-shop-entry")));
            Assert.That(WorldEntryRules.ResolveContinue(catalog, "slot-1", saved), Is.Null, "버전 1은 슬롯이 같아야 한다");
            saved.Version = 0;
            saved.SaveSlot = "";
            Assert.That(WorldEntryRules.ResolveContinue(catalog, "slot-1", saved), Is.Not.Null, "버전 0은 빈 슬롯을 받아 준다");
            saved.SaveSlot = "slot-0";
            Assert.That(WorldEntryRules.ResolveContinue(catalog, "slot-1", saved), Is.Null);
            saved.Version = 2;
            Assert.That(WorldEntryRules.ResolveContinue(catalog, "slot-0", saved), Is.Null);
            saved.Version = 1;
            saved.CurrentEntryPointId = "nowhere";
            Assert.That(WorldEntryRules.ResolveContinue(catalog, "slot-0", saved), Is.Null, "목록에 없는 도착 지점");
        }

        [Test]
        public void WORLD_SLOT_004_NewSlotWritesMetadataAndStartsAtTheNewGameSpawn()
        {
            var store = new InMemorySaveStore();
            var slot = new WorldSlot(store, "slot-2", Catalog());
            WorldSlotStart start = slot.Begin(Now);
            Assert.That(start.Ok, Is.True, start.Problem);
            Assert.That(start.NewGame, Is.True);
            Assert.That(start.Entry, Is.EqualTo(new WorldEntryPoint("House", "house-entry")));
            Assert.That(store.TryRead("slot-2", SaveFileNames.Metadata, out SaveSlotMetadata metadata), Is.True);
            Assert.That(metadata.SlotId, Is.EqualTo("slot-2"));
            Assert.That(metadata.DisplayName, Is.EqualTo("slot-2"));
            Assert.That(metadata.CreatedAtUtcTicks, Is.EqualTo(Now));
            Assert.That(metadata.UpdatedAtUtcTicks, Is.EqualTo(Now));
            Assert.That(metadata.WorldSeed, Is.EqualTo(SaveSlotMetadata.StableHash("slot-2", 0x13579BDF)), "Unity CreateDeterministicMetadata");
            Assert.That(metadata.TileSeed, Is.EqualTo(SaveSlotMetadata.StableHash("slot-2", 0x2468ACE)));
            Assert.That(metadata.Character, Is.Not.Null);
            Assert.That(metadata.Appearance, Is.Not.Null);
            Assert.That(store.Contains("slot-2", SaveFileNames.WorldProgress), Is.False, "도착해야 쓴다");
        }


        [Test]
        public void WORLD_SLOT_005_ArrivalIsRecordedAndContinueStartsThere()
        {
            var store = new InMemorySaveStore();
            var slot = new WorldSlot(store, "slot-0", Catalog());
            slot.Begin(Now);
            Assert.That(slot.Record("Town", "town-shop-entry", "town"), Is.True);
            Assert.That(store.TryRead("slot-0", SaveFileNames.WorldProgress, out WorldProgressSaveData saved), Is.True);
            Assert.That(saved.Version, Is.EqualTo(1));
            Assert.That(saved.SaveSlot, Is.EqualTo("slot-0"));
            Assert.That(saved.CurrentScene, Is.EqualTo("Town"));
            Assert.That(saved.CurrentEntryPointId, Is.EqualTo("town-shop-entry"));
            Assert.That(saved.LastPortalDisplayName, Is.EqualTo("town"));

            WorldSlotStart again = new WorldSlot(store, "slot-0", Catalog()).Begin(Now + 1);
            Assert.That(again.Ok, Is.True, again.Problem);
            Assert.That(again.NewGame, Is.False);
            Assert.That(again.Entry, Is.EqualTo(new WorldEntryPoint("Town", "town-shop-entry")));
            Assert.That(again.Metadata.CreatedAtUtcTicks, Is.EqualTo(Now), "이어 하기는 슬롯 정보를 새로 만들지 않는다");
        }

        [Test]
        public void WORLD_SLOT_006_ArrivalOutsideTheCatalogIsRefusedAndWritesNothing()
        {
            var store = new InMemorySaveStore();
            var slot = new WorldSlot(store, "slot-0", Catalog());
            slot.Begin(Now);
            int writes = store.Writes;
            Assert.That(slot.Record("Beach", "beach-top-entry", null), Is.False);
            Assert.That(store.Writes, Is.EqualTo(writes));
        }

        [Test]
        public void WORLD_SLOT_007_UnsafeSlotIdIsRefused()
        {
            var store = new InMemorySaveStore();
            Assert.That(new WorldSlot(store, "../x", Catalog()).Begin(Now).Ok, Is.False);
            Assert.That(new WorldSlot(store, "", Catalog()).Begin(Now).Ok, Is.False);
            Assert.That(store.Writes, Is.EqualTo(0));
        }

        [Test]
        public void WORLD_SLOT_008_BadWorldProgressRefusesToContinueInsteadOfStartingOver()
        {
            var store = new InMemorySaveStore();
            store.Put("slot-0", SaveFileNames.Metadata, new SaveSlotMetadata { SlotId = "slot-0", DisplayName = "slot-0", CreatedAtUtcTicks = 5 });
            store.Put("slot-0", SaveFileNames.WorldProgress, new WorldProgressSaveData { Version = 1, SaveSlot = "slot-9", CurrentScene = "Town", CurrentEntryPointId = "town-home-entry" });
            WorldSlotStart start = new WorldSlot(store, "slot-0", Catalog()).Begin(Now);
            Assert.That(start.Ok, Is.False);
            Assert.That(start.Problem, Is.Not.Empty);
            Assert.That(store.Writes, Is.EqualTo(0), "깨진 이어 하기를 새 게임으로 덮지 않는다");
        }

        [Test]
        public void WORLD_SLOT_020_SlotInfoWithoutASlotId_IsRefused_NotRewritten()
        {
            // Unity 판과 전에는 슬롯 정보를 새로 만들어 덮어썼다(시드·겉모습이 바뀌고 진행 파일은 그대로 이어졌다).
            var store = new InMemorySaveStore();
            store.Put("slot-0", SaveFileNames.Metadata, new SaveSlotMetadata { SlotId = "", DisplayName = "하늘", WorldSeed = 77 });
            WorldSlotStart start = new WorldSlot(store, "slot-0", Catalog()).Begin(Now);
            Assert.That(start.Ok, Is.False);
            Assert.That(start.Problem, Does.Contain(SaveFileNames.Metadata));
            Assert.That(store.Writes, Is.EqualTo(0));
        }

        [Test]
        public void WORLD_SLOT_021_ProgressWithoutSlotInfo_IsRefused_NotStartedOver()
        {
            var store = new InMemorySaveStore();
            store.Put("slot-0", SaveFileNames.WorldProgress, new WorldProgressSaveData { Version = 1, SaveSlot = "slot-0", CurrentScene = "Town", CurrentEntryPointId = "town-home-entry" });
            WorldSlotStart start = new WorldSlot(store, "slot-0", Catalog()).Begin(Now);
            Assert.That(start.Ok, Is.False);
            Assert.That(start.Problem, Does.Contain(SaveFileNames.Metadata));
            Assert.That(store.Writes, Is.EqualTo(0), "슬롯 정보를 새로 써서 이어 붙이지 않는다");
        }

        [Test]
        public void WORLD_SLOT_009_MetadataWithoutCharacterPartsGetsEmptyOnes()
        {
            var store = new InMemorySaveStore();
            store.Put("slot-0", SaveFileNames.Metadata, new SaveSlotMetadata { SlotId = "slot-0", Character = null, Appearance = null });
            WorldSlotStart start = new WorldSlot(store, "slot-0", Catalog()).Begin(Now);
            Assert.That(start.Ok, Is.True, start.Problem);
            Assert.That(start.Metadata.Character, Is.Not.Null);
            Assert.That(start.Metadata.Appearance, Is.Not.Null);
            Assert.That(start.Entry, Is.EqualTo(new WorldEntryPoint("House", "house-entry")), "world-progress가 없으면 새 게임 자리(Unity ResolveContinue)");
        }

        [Test]
        public void WORLD_SLOT_010_SessionRecordsEachArrivalAndRefusedRecordKeepsThePlace()
        {
            var maps = new Dictionary<string, WorldMap>();
            CollisionGrid grid = WorldMovementTests.Grid("#####", "#...#", "#####");
            maps["Town"] = new WorldMap("Town", grid, null, new Dictionary<string, (float, float)> { { "town-home-entry", (48f, 48f) }, { "town-shop-entry", (80f, 48f) } });
            maps["Beach"] = new WorldMap("Beach", grid, null, new Dictionary<string, (float, float)> { { "beach-top-entry", (48f, 48f) } });
            var store = new InMemorySaveStore();
            var slot = new WorldSlot(store, "slot-0", Catalog());
            slot.Begin(Now);
            var session = new WorldSession(new DictionarySource(maps), WalkerSettings.UnityPlayer) { Arrivals = slot };
            Assert.That(session.Enter("Town", "town-home-entry"), Is.True);
            Assert.That(store.TryRead("slot-0", SaveFileNames.WorldProgress, out WorldProgressSaveData saved), Is.True);
            Assert.That(saved.CurrentEntryPointId, Is.EqualTo("town-home-entry"));
            Assert.That(session.Enter("Beach", "beach-top-entry"), Is.False, "목록에 없는 곳은 기록할 수 없어 들어가지 않는다(Unity: 도착 기록 실패면 옮기지 않는다)");
            Assert.That(session.Map.Id, Is.EqualTo("Town"));
            Assert.That(session.LastProblem, Is.Not.Null);
        }

        private sealed class DictionarySource : IWorldMapSource
        {
            private readonly Dictionary<string, WorldMap> _maps;
            public DictionarySource(Dictionary<string, WorldMap> maps) { _maps = maps; }
            public WorldMap Load(string mapId) => _maps.TryGetValue(mapId, out var m) ? m : null;
        }
    }
}
