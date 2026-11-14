using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Careers;
using Rootborn.Core.Encyclopedia;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;
using Rootborn.Core.WorldState;
using Rootborn.Data.Save;
using Names = Rootborn.Core.Save.SaveFileNames;

namespace Rootborn.Data.Tests
{
    /// <summary>
    /// Core가 이름을 만드는 세이브 파일은 모두 모양이 등록돼 있고, 맨 위 모양은 형식 버전을 가진다.
    /// 고정 파일(Unity 판 QA 세이브)의 이름은 기본 플레이어에 대해 SaveFileNames가 만드는 이름과 같아야 한다.
    /// </summary>
    public sealed class SaveShapeRegistrationTests
    {
        private static readonly string FixtureRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures", "saves");
        private const string DefaultPlayer = Names.DefaultPlayerId;

        /// <summary>기본 플레이어 하나가 쓰는 Core 세이브 파일 이름 전부.</summary>
        private static readonly string[] DefaultPlayerFiles =
        {
            Names.QuestLog,
            Names.QuestChain(DefaultPlayer),
            Names.AdultCareerSnapshot(DefaultPlayer),
            Names.Campaign(DefaultPlayer),
            Names.LocationState(DefaultPlayer),
            Names.Milestone(DefaultPlayer),
            Names.CareerInterest(DefaultPlayer),
            Names.Exploration(DefaultPlayer),
            Names.WorldState(DefaultPlayer),
            Names.WorldStateShared,
            Names.WorldStateUsage(DefaultPlayer),
            Names.Encyclopedia(DefaultPlayer),
            Names.DiscoveryClues(DefaultPlayer),
            Names.ClueInterpretations(DefaultPlayer),
            Names.HouseFurnitureLayout(0),
            Names.HouseFurnitureLayoutBeforeStages,
            Names.FurnitureUnlocks(DefaultPlayer),
            Names.HouseState,
            Names.DailyEvent(DefaultPlayer),
            Names.UnityStudentLife(DefaultPlayer),
            Names.UnityInventory(DefaultPlayer),
            Names.UnityDailyPlan(DefaultPlayer),
            Names.UnityDailyPlanSanitized(DefaultPlayer),
            Names.UnityCareerCandidates(DefaultPlayer),
            Names.ResourceRecipe(DefaultPlayer),
            Names.ResourceRecipe(Names.LegacyPlayerId),
            Names.ObjectiveJournalTracking,
            Names.Settings,
            Names.RoomClock,
            Names.RoomSettings,
            Names.RoomRequestLedger,
            Names.RoomPiece(Rootborn.Core.Room.RoomWallet.PieceName),
            Names.RoomPiece(Rootborn.Core.Room.RoomWorldFlags.PieceName),
            Names.RoomPiece(Rootborn.Core.Room.RoomChests.PieceName),
            Names.RoomOutbox(DefaultPlayer),
            Names.RoomPiece(Rootborn.Core.Room.RoomHouse.PieceName),
            Names.StudentClock(DefaultPlayer),
            Names.Metadata,
            Names.WorldProgress,
            Names.Wearables(DefaultPlayer),
            Names.WorkspaceProgress(DefaultPlayer),
            Names.WorkspaceLayout("workspace.chef", DefaultPlayer),
            Names.HouseBuild,
        };

        [TestCaseSource(nameof(DefaultPlayerFiles))]
        public void EveryCoreSaveFile_HasARegisteredShape(string file)
        {
            Assert.That(SaveShapeCoverage.ShapeFor(file), Is.Not.Null, $"{file}: SaveShapeCoverage.Shapes에 모양이 없다");
            Assert.That(SaveShapeCoverage.IsListedAsNotShaped(file), Is.False, $"{file}: 모양이 있는데 '아직 모양 없음'에도 있다");
        }

        [Test]
        public void EveryRegisteredShape_IsVersioned()
        {
            var unversioned = SaveShapeCoverage.Shapes.Where(s => !typeof(IVersionedSave).IsAssignableFrom(s.Shape)).Select(s => s.Pattern).ToArray();
            Assert.That(unversioned, Is.Empty, "파일 맨 위 모양은 IVersionedSave를 구현해야 한다");
        }

        [Test]
        public void EveryRegisteredShape_ReadsAFileWithoutVersionAsUnity()
        {
            foreach (var (_, shape) in SaveShapeCoverage.Shapes)
            {
                // Unity 판에도 Version이 있던 모양은 그 값과 뜻을 그대로 둔다(스냅샷은 처음부터 2).
                if (shape == typeof(AdultCareerSnapshotSaveData)) continue;
                // 옷 파일도 Unity 판부터 소문자 version 필드가 있고 기본값이 1이다.
                if (shape == typeof(Rootborn.Core.Characters.WearableSaveData)) continue;
                var instance = (IVersionedSave)Activator.CreateInstance(shape);
                Assert.That(instance.Version, Is.EqualTo(SaveVersion.Unity), $"{shape.Name}: 새로 만든 모양(=버전 없는 파일)은 0이어야 한다");
                Assert.That(instance.KnownVersion, Is.GreaterThanOrEqualTo(SaveVersion.Current), shape.Name);
            }
        }

        [Test]
        public void ShapedFixtureFiles_UseTheDefaultPlayerNames()
        {
            // 고정 파일은 모두 기본 플레이어("local-player")가 쓴 것이다. 기본 플레이어 이름이 바뀌면 옛 세이브를 못 찾는다.
            var shaped = Directory.GetFiles(FixtureRoot, "*.json", SearchOption.AllDirectories)
                .Select(Path.GetFileName)
                .Where(name => SaveShapeCoverage.ShapeFor(name) != null)
                .Distinct()
                .ToArray();

            Assert.That(shaped, Is.Not.Empty);
            Assert.That(shaped.Except(DefaultPlayerFiles), Is.Empty, "고정 파일 이름이 SaveFileNames의 기본 플레이어 이름과 다르다");
        }

        [Test]
        public void WorldStateFiles_RoundTripThroughTheFileStore()
        {
            using var dir = new TempDir();
            var store = new FileSaveStore(dir.Path);
            var shared = new WorldStateFlag("world.a", null, null, "location.a", null, null, WorldStateScopeKind.Shared, WorldStateChangeKind.ObjectRevealed,
                null, null, null, 0, 1);
            var progress = WorldStateSaveFiles.LoadOrCreate(store, "slot-0", DefaultPlayer);
            progress.TryActivate(shared, new WorldStateActivationSource("chain", "step", "event", 2));

            WorldStateSaveFiles.Save(store, progress);

            Assert.That(File.Exists(Path.Combine(dir.Path, "slot-0", "world-state-progress.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(dir.Path, "slot-0", "world-state-progress-shared.json")), Is.True);
            Assert.That(WorldStateSaveFiles.LoadOrCreate(store, "slot-0", "player-b").IsActive(shared), Is.True, "공유 상태는 같은 슬롯의 다른 플레이어도 본다");
            Assert.That(store.TryRead("slot-0", Names.WorldStateShared, out WorldStateProgressSaveData written), Is.True);
            Assert.That(written.Version, Is.EqualTo(WorldStateProgressSaveData.KnownVersion));
        }

        [Test]
        public void AnUnsanitizedUnityFileOnDisk_IsReadThenWrittenUnderTheNewName()
        {
            using var dir = new TempDir();
            var store = new FileSaveStore(dir.Path);
            const string player = "steam 7"; // 공백은 파일 이름에 쓸 수 있다. Unity 판은 그대로 붙였다.
            dir.Write(Path.Combine("slot-0", "encyclopedia-steam 7.json"),
                "{\"SaveSlot\":\"slot-0\",\"PlayerId\":\"steam 7\",\"Records\":[{\"EntryId\":\"entry-a\",\"RevealStage\":1,\"FirstDiscoveredUtcTicks\":0,\"IsNew\":true}]}");

            var progress = EncyclopediaSaveFiles.LoadOrCreate(store, "slot-0", player);
            Assert.That(progress.IsUnlocked("entry-a"), Is.True);

            EncyclopediaSaveFiles.Save(store, progress);
            Assert.That(File.Exists(Path.Combine(dir.Path, "slot-0", "encyclopedia-steam_7.json")), Is.True);
        }

        [Test]
        public void ALegacyNameWithAColon_DoesNotBreakLoading()
        {
            // Unity 판 이름 "exploration-progress-steam:7.json"은 파일 저장소가 다룰 수 없다. 새 게임으로 시작한다.
            using var dir = new TempDir();
            var store = new FileSaveStore(dir.Path);

            var progress = ExplorationProgressPersistence.LoadOrCreate(store, "slot-0", "steam:7");

            Assert.That(progress.PlayerId, Is.EqualTo("steam:7"));
            ExplorationProgressPersistence.Save(store, progress);
            Assert.That(File.Exists(Path.Combine(dir.Path, "slot-0", "exploration-progress-steam_7.json")), Is.True);
        }
    }
}
