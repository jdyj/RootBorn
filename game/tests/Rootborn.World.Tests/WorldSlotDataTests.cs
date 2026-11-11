using System;
using System.IO;
using NUnit.Framework;
using Rootborn.Core.Save;
using Rootborn.Core.World;
using Rootborn.Data.Save;

namespace Rootborn.World.Tests
{
    /// <summary>자동 테스트: 실제 장면 목록 데이터와 Unity 판 QA 세이브 고정 파일. 화면 근거가 아니다.</summary>
    public sealed class WorldSlotDataTests
    {
        private static string FixtureSaves => Path.Combine(Repo.Root, "game/tests/Rootborn.Data.Tests/Fixtures/saves");

        [Test]
        public void TravelCatalog_FromDataStartsANewGameAtTheHouseEntry()
        {
            WorldTravelCatalog catalog = WorldTravelData.From(Repo.Definitions);
            Assert.That(catalog.IsValid, Is.True);
            Assert.That(WorldEntryRules.ResolveNewGame(catalog), Is.EqualTo(new WorldEntryPoint("House", "house-entry")), "Unity WorldTravelCatalog.asset: _newGameDestination Destination_House, _newGameSpawnId house-entry");
            Assert.That(catalog.Destinations.Count, Is.EqualTo(10));
        }

        [Test]
        public void EveryMapSpawnAndDoorDestinationIsInTheTravelCatalog()
        {
            WorldTravelCatalog travel = WorldTravelData.From(Repo.Definitions);
            WorldCatalog maps = WorldCatalog.Load(Path.Combine(Repo.Root, WorldCatalog.DefaultRelativePath));
            foreach (MapEntry map in maps.Maps)
            {
                WorldDestination d = travel.Find(map.Id);
                Assert.That(d, Is.Not.Null, "장면 목록에 없는 맵: " + map.Id);
                foreach (string spawn in map.Spawns.Keys) Assert.That(d.ContainsSpawn(spawn), Is.True, map.Id + "/" + spawn);
                foreach (var door in map.Doors)
                    Assert.That(WorldEntryRules.ResolvePair(travel, door.Value.MapId, door.Value.SpawnId), Is.Not.Null, map.Id + " 문 " + door.Key + " → " + door.Value);
            }
        }

        [TestCase("exe-20261105", "slot-0", "Town", "town-home-entry", 639294595206896146L, 909055797)]
        [TestCase("editor-20261105", "slot-1", "House", "house-entry", 639294418224171544L, -2052454917)]
        public void UnitySaveFixture_MetadataAndWorldProgressAreReadAndContinueThere(string folder, string slot, string scene, string spawn, long created, int worldSeed)
        {
            var store = new FileSaveStore(Path.Combine(FixtureSaves, folder));
            Assert.That(store.TryRead(slot, SaveFileNames.Metadata, out SaveSlotMetadata metadata), Is.True);
            Assert.That(metadata.Version, Is.EqualTo(SaveVersion.Unity), "Unity 판 파일에는 Version이 없다");
            Assert.That(metadata.SlotId, Is.EqualTo(slot));
            Assert.That(metadata.DisplayName, Is.EqualTo(slot));
            Assert.That(metadata.CreatedAtUtcTicks, Is.EqualTo(created));
            Assert.That(metadata.WorldSeed, Is.EqualTo(worldSeed));
            Assert.That(metadata.Character._defaultFacing, Is.EqualTo(0));
            Assert.That(metadata.Appearance._parts, Is.Empty);
            Assert.That(metadata.CharacterAppearanceSnapshot.visualKind, Is.EqualTo("spum"));
            Assert.That(metadata.CharacterAppearanceSnapshot.selectedParts.Length, Is.EqualTo(7));
            Assert.That(metadata.CharacterAppearanceSnapshot.selectedParts[3].categoryId, Is.EqualTo("hair"));
            Assert.That(metadata.CharacterAppearanceSnapshot.selectedParts[3].partId, Is.EqualTo("spum.hair.short"));

            Assert.That(store.TryRead(slot, SaveFileNames.WorldProgress, out WorldProgressSaveData progress), Is.True);
            Assert.That(progress.Version, Is.EqualTo(1));
            Assert.That(progress.SaveSlot, Is.EqualTo(slot));
            Assert.That(progress.CurrentScene, Is.EqualTo(scene));
            Assert.That(progress.CurrentEntryPointId, Is.EqualTo(spawn));
            Assert.That(progress.LastPortalDisplayName, Is.EqualTo(""));

            DateTime before = File.GetLastWriteTimeUtc(Path.Combine(FixtureSaves, folder, slot, SaveFileNames.Metadata));
            WorldSlotStart start = new WorldSlot(store, slot, WorldTravelData.From(Repo.Definitions)).Begin(DateTime.UtcNow.Ticks);
            Assert.That(start.Ok, Is.True, start.Problem);
            Assert.That(start.NewGame, Is.False);
            Assert.That(start.Entry, Is.EqualTo(new WorldEntryPoint(scene, spawn)));
            Assert.That(File.GetLastWriteTimeUtc(Path.Combine(FixtureSaves, folder, slot, SaveFileNames.Metadata)), Is.EqualTo(before), "이어 하기 시작은 쓰지 않는다");
        }
    }
}
