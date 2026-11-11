using System;
using NUnit.Framework;
using Rootborn.Core.DiscoveryClues;
using Rootborn.Core.Encyclopedia;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;
using Rootborn.Core.WorldState;
using Names = Rootborn.Core.Save.SaveFileNames;

namespace Rootborn.Core.Tests
{
    /// <summary>
    /// Unity 판은 도감·발견 단서·단서 해석·탐색 파일에 플레이어 id를 정리하지 않고 붙였다. 새로 쓸 때는 정리한 이름을 쓰고,
    /// 읽을 때 새 이름이 없으면 옛 이름을 찾는다. 기본 플레이어는 두 이름이 같아 바뀌는 것이 없다.
    /// </summary>
    public sealed class LegacySaveFileNameTests
    {
        private const string Slot = "slot-a";
        private const string SteamPlayer = "steam:7";

        [Test]
        public void TryReadWithLegacyName_PrefersTheNewName_ThenFallsBack()
        {
            var store = new InMemorySaveStore();
            store.Put(Slot, "old.json", new EncyclopediaProgressSaveData { PlayerId = "old" });

            Assert.That(store.TryReadWithLegacyName<EncyclopediaProgressSaveData>(Slot, "new.json", "old.json", out var fromOld), Is.True);
            Assert.That(fromOld.PlayerId, Is.EqualTo("old"));

            store.Put(Slot, "new.json", new EncyclopediaProgressSaveData { PlayerId = "new" });
            Assert.That(store.TryReadWithLegacyName<EncyclopediaProgressSaveData>(Slot, "new.json", "old.json", out var fromNew), Is.True);
            Assert.That(fromNew.PlayerId, Is.EqualTo("new"));
        }

        [Test]
        public void TryReadWithLegacyName_SameNames_ReadsOnce()
        {
            var store = new InMemorySaveStore();

            Assert.That(store.TryReadWithLegacyName<EncyclopediaProgressSaveData>(Slot, "same.json", "same.json", out _), Is.False);
            Assert.That(store.Reads, Is.EqualTo(1));
        }

        [Test]
        public void TryReadWithLegacyName_ALegacyNameTheStoreCannotAddress_MeansNoLegacyFile()
        {
            // 파일 저장소는 ':'·구분자가 든 이름을 거절한다(ArgumentException). 그런 옛 이름에는 읽을 파일이 있을 수 없다.
            var store = new PathCheckingStore();

            Assert.That(store.TryReadWithLegacyName<EncyclopediaProgressSaveData>(Slot, "encyclopedia-steam_7.json", "encyclopedia-steam:7.json", out var data), Is.False);
            Assert.That(data, Is.Null);
            Assert.That(EncyclopediaSaveFiles.LoadOrCreate(store, Slot, SteamPlayer).PlayerId, Is.EqualTo(SteamPlayer));
            Assert.Throws<ArgumentException>(() => store.TryRead<EncyclopediaProgressSaveData>(Slot, "bad:new.json", out _), "새 이름의 오류는 그대로 낸다");
        }

        [Test]
        public void Encyclopedia_ReadsTheUnsanitizedUnityFile_AndWritesTheNewName()
        {
            var store = new InMemorySaveStore();
            store.Put(Slot, "encyclopedia-steam:7.json", new EncyclopediaProgressSaveData
            {
                SaveSlot = Slot, PlayerId = SteamPlayer,
                Records = new[] { new EncyclopediaProgressRecord { EntryId = "entry-a", RevealStage = 1 } },
            });

            var progress = EncyclopediaSaveFiles.LoadOrCreate(store, Slot, SteamPlayer);
            Assert.That(progress.IsUnlocked("entry-a"), Is.True);

            EncyclopediaSaveFiles.Save(store, progress);
            Assert.That(store.Contains(Slot, "encyclopedia-steam_7.json"), Is.True);
            Assert.That(EncyclopediaSaveFiles.LoadOrCreate(store, Slot, SteamPlayer).IsUnlocked("entry-a"), Is.True);
        }

        [Test]
        public void DiscoveryClues_ReadsTheUnsanitizedUnityFile_AndWritesTheNewName()
        {
            var store = new InMemorySaveStore();
            store.Put(Slot, "discovery-clues-steam:7.json", new DiscoveryClueProgressSaveData
            {
                SaveSlot = Slot, PlayerId = SteamPlayer,
                Records = new[] { new DiscoveryClueRecordSaveData { ClueId = "clue.a", Seen = true } },
            });

            var progress = DiscoveryClueSaveFiles.LoadOrCreate(store, Slot, SteamPlayer);
            Assert.That(progress.GetRecord("clue.a").Seen, Is.True);

            DiscoveryClueSaveFiles.Save(store, progress);
            Assert.That(store.Contains(Slot, "discovery-clues-steam_7.json"), Is.True);
        }

        [Test]
        public void ClueInterpretations_ReadsTheUnsanitizedUnityFile_AndWritesTheNewName()
        {
            var store = new InMemorySaveStore();
            store.Put(Slot, "clue-interpretations-steam:7.json", new ClueInterpretationProgressSaveData
            {
                SaveSlot = Slot, PlayerId = SteamPlayer,
                Records = new[] { new ClueInterpretationRecordSaveData { InterpretationId = "interpret.a", Completed = true, CompletedCount = 1 } },
            });

            var progress = ClueInterpretationSaveFiles.LoadOrCreate(store, Slot, SteamPlayer);
            Assert.That(progress.GetRecord("interpret.a").Completed, Is.True);

            ClueInterpretationSaveFiles.Save(store, progress);
            Assert.That(store.Contains(Slot, "clue-interpretations-steam_7.json"), Is.True);
        }

        [Test]
        public void Exploration_ReadsTheUnsanitizedUnityFile_AndWritesTheNewName()
        {
            var store = new InMemorySaveStore();
            store.Put(Slot, "exploration-progress-steam:7.json", new ExplorationProgressSaveData
            {
                SaveSlot = Slot, PlayerId = SteamPlayer,
                Records = new[] { new ExplorationProgressRecordSaveData { InteractionId = "point.a", Completed = true } },
            });

            var progress = ExplorationProgressPersistence.LoadOrCreate(store, Slot, SteamPlayer);
            Assert.That(progress.TryGetRecord("point.a", out var record) && record.Completed, Is.True);

            ExplorationProgressPersistence.Save(store, progress);
            Assert.That(store.Contains(Slot, "exploration-progress-steam_7.json"), Is.True);
            Assert.That(ExplorationProgressPersistence.LoadOrCreate(store, Slot, SteamPlayer).TryGetRecord("point.a", out _), Is.True);
        }

        [Test]
        public void Exploration_DefaultPlayer_UsesTheSharedFile()
        {
            var store = new InMemorySaveStore();
            var progress = ExplorationProgressPersistence.LoadOrCreate(store, Slot, Names.DefaultPlayerId);
            progress.GetRecord("point.a");

            ExplorationProgressPersistence.Save(store, progress);

            Assert.That(store.Contains(Slot, "exploration-progress.json"), Is.True);
            Assert.That(ExplorationProgressPersistence.LoadOrCreate(store, null, null).SaveSlot, Is.EqualTo("default"));
        }

        [Test]
        public void UnreadableFiles_Throw_InsteadOfStartingOver()
        {
            // ISaveStore의 약속: 있는데 읽을 수 없는 파일은 예외. 새 게임으로 덮어쓰지 않는다.
            var store = new UnreadableStore();

            Assert.Throws<InvalidOperationException>(() => EncyclopediaSaveFiles.LoadOrCreate(store, Slot, SteamPlayer));
            Assert.Throws<InvalidOperationException>(() => DiscoveryClueSaveFiles.LoadOrCreate(store, Slot, SteamPlayer));
            Assert.Throws<InvalidOperationException>(() => ClueInterpretationSaveFiles.LoadOrCreate(store, Slot, SteamPlayer));
            Assert.Throws<InvalidOperationException>(() => WorldStateSaveFiles.LoadOrCreate(store, Slot, SteamPlayer));
            Assert.Throws<InvalidOperationException>(() => WorldStateUsageSaveFiles.LoadOrCreate(store, Slot, SteamPlayer));
            Assert.Throws<InvalidOperationException>(() => ExplorationProgressPersistence.LoadOrCreate(store, Slot, SteamPlayer));
        }

        private sealed class PathCheckingStore : ISaveStore
        {
            public bool TryRead<T>(string saveSlot, string fileName, out T data) where T : class, new()
            {
                if (fileName.IndexOf(':') >= 0) throw new ArgumentException("경로 한 칸이 아닌 이름", nameof(fileName));
                data = null;
                return false;
            }

            public void Write<T>(string saveSlot, string fileName, T data) where T : class { }
            public void Delete(string saveSlot, string fileName) { }
        }

        private sealed class UnreadableStore : ISaveStore
        {
            public bool TryRead<T>(string saveSlot, string fileName, out T data) where T : class, new() => throw new InvalidOperationException("깨진 파일");
            public void Write<T>(string saveSlot, string fileName, T data) where T : class => throw new InvalidOperationException("쓰면 안 된다");
            public void Delete(string saveSlot, string fileName) => throw new InvalidOperationException("지우면 안 된다");
        }
    }
}
