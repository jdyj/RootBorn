using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Core.Encyclopedia;
using Rootborn.Core.Inventory;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Tests
{
    // 옮겨 옴: Assets/Tests/EditMode/Encyclopedia/EncyclopediaCoreTests.cs (ENCYCLOPEDIA_EDIT_002~006, 001은 등록부 검사라 옮기지 않음)
    public sealed class EncyclopediaCoreTests
    {
        private sealed class FakeKnowledge : IKnowledgeUnlockLookup
        {
            private readonly HashSet<string> _ids = new HashSet<string>();
            public FakeKnowledge(params string[] ids) { foreach (var id in ids) _ids.Add(id); }
            public bool IsUnlocked(string knowledgeId) => knowledgeId != null && _ids.Contains(knowledgeId);
        }

        private static readonly ItemDefinition Book = new ItemDefinition("item.book");
        private static readonly ItemDefinition Missing = new ItemDefinition("item.missing");

        private static ItemInventory FakeItems(ItemDefinition item, int count)
        {
            var inventory = new ItemInventory();
            inventory.Add(item, count);
            return inventory;
        }

        private sealed class MemoryStore : IEncyclopediaSaveStore
        {
            public readonly Dictionary<string, EncyclopediaProgressSaveData> Files = new Dictionary<string, EncyclopediaProgressSaveData>();
            public EncyclopediaProgressSaveData Read(string saveSlot, string fileName) => Files.TryGetValue(saveSlot + "/" + fileName, out var data) ? data : null;
            public void Write(string saveSlot, string fileName, EncyclopediaProgressSaveData data) => Files[saveSlot + "/" + fileName] = data;
        }

        private static EncyclopediaEntry Entry(string id, EncyclopediaCategory category, IEncyclopediaUnlockCondition[] conditions = null, int revealStage = 1)
            => new EncyclopediaEntry(id, category, id, "???", "Description " + id, "Find a clue", Array.Empty<string>(), conditions, revealStage);

        [Test]
        public void ENCYCLOPEDIA_EDIT_002_UnlockConditionsEvaluateStateWithoutEntityBranching()
        {
            var progress = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            progress.RecordActivityCompleted("location.library");
            progress.RecordActivityCompleted("activity.study");
            progress.RecordActivityCompleted("npc.librarian");
            progress.RecordActivityCompleted("discovery.old-map");
            progress.UnlockCareerHint("career.writer");
            var context = new EncyclopediaUnlockContext(progress, new FakeKnowledge("knowledge.soil"), FakeItems(Book, 1));

            Assert.IsTrue(new EncyclopediaLocationUnlockedCondition("location.library").IsSatisfied(context));
            Assert.IsTrue(new EncyclopediaActivityCompletedCondition("activity.study").IsSatisfied(context));
            Assert.IsTrue(new EncyclopediaNpcMetCondition("npc.librarian").IsSatisfied(context));
            Assert.IsTrue(new EncyclopediaDiscoveryCompletedCondition("discovery.old-map").IsSatisfied(context));
            Assert.IsTrue(new EncyclopediaCareerHintUnlockedCondition("career.writer").IsSatisfied(context));
            Assert.IsTrue(new EncyclopediaKnowledgeUnlockedCondition("knowledge.soil").IsSatisfied(context));
            Assert.IsTrue(new EncyclopediaItemPossessedCondition(Book).IsSatisfied(context));
        }

        [Test]
        public void ENCYCLOPEDIA_CORE_ConditionsRejectEmptyIdsAndMissingContext()
        {
            var progress = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            progress.RecordActivityCompleted("location.library");
            var full = new EncyclopediaUnlockContext(progress, new FakeKnowledge("knowledge.soil"), FakeItems(Book, 1));
            var empty = new EncyclopediaUnlockContext(null, null, null);

            Assert.IsFalse(new EncyclopediaLocationUnlockedCondition(null).IsSatisfied(full));
            Assert.IsFalse(new EncyclopediaLocationUnlockedCondition(string.Empty).IsSatisfied(full));
            Assert.IsFalse(new EncyclopediaLocationUnlockedCondition("location.library").IsSatisfied(empty));
            Assert.IsFalse(new EncyclopediaCareerHintUnlockedCondition("career.writer").IsSatisfied(full));
            Assert.IsFalse(new EncyclopediaKnowledgeUnlockedCondition("knowledge.soil").IsSatisfied(empty));
            Assert.IsFalse(new EncyclopediaItemPossessedCondition(Book).IsSatisfied(empty));
            Assert.IsFalse(new EncyclopediaItemPossessedCondition(Missing).IsSatisfied(full));
            Assert.IsFalse(new EncyclopediaItemPossessedCondition(null).IsSatisfied(full));
        }

        [Test]
        public void ENCYCLOPEDIA_CORE_EntryWithoutConditionsNeverUnlocksAndAllConditionsMustHold()
        {
            var progress = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            progress.RecordActivityCompleted("location.library");
            var context = new EncyclopediaUnlockContext(progress, null, null);

            Assert.IsFalse(Entry("entry.none", null).IsUnlocked(context));
            Assert.IsTrue(Entry("entry.one", null, new IEncyclopediaUnlockCondition[] { new EncyclopediaLocationUnlockedCondition("location.library") }).IsUnlocked(context));
            Assert.IsFalse(Entry("entry.two", null, new IEncyclopediaUnlockCondition[] { new EncyclopediaLocationUnlockedCondition("location.library"), new EncyclopediaNpcMetCondition("npc.x") }).IsUnlocked(context));
            Assert.IsFalse(Entry("entry.null", null, new IEncyclopediaUnlockCondition[] { null }).IsUnlocked(context), "A missing condition blocks the entry, as in Unity.");
            Assert.AreEqual(1, Entry("entry.stage", null, null, revealStage: 0).RevealStage);
        }

        [Test]
        public void ENCYCLOPEDIA_EDIT_003_ProgressPersistsUnlockStagesAndNewFlagsBySaveSlotAndPlayer()
        {
            var progress = new EncyclopediaProgress("slot-a", "player-a");
            Assert.IsTrue(progress.TryUnlock("entry-a", 2, 123L));
            Assert.IsTrue(progress.IsNew("entry-a"));
            progress.MarkSeen("entry-a");
            var store = new MemoryStore();

            EncyclopediaSaveFiles.Save(store, progress);
            var loaded = EncyclopediaSaveFiles.LoadOrCreate(store, "slot-a", "player-a");

            Assert.IsTrue(loaded.IsUnlocked("entry-a"));
            Assert.AreEqual(2, loaded.GetRevealStage("entry-a"));
            Assert.IsFalse(loaded.IsNew("entry-a"));
            Assert.IsFalse(EncyclopediaSaveFiles.LoadOrCreate(store, "slot-a", "player-b").IsUnlocked("entry-a"));
            Assert.IsTrue(store.Files.ContainsKey("slot-a/encyclopedia-player-a.json"));
        }

        [Test]
        public void ENCYCLOPEDIA_CORE_SaveRoundTripKeepsTicksAndDropsDuplicateOrEmptyRecords()
        {
            var progress = new EncyclopediaProgress("slot-a", "player-a");
            progress.TryUnlock("entry-a", 3, 555L);
            var data = progress.ToSaveData();
            Assert.AreEqual("slot-a", data.SaveSlot);
            Assert.AreEqual(555L, data.Records[0].FirstDiscoveredUtcTicks);

            data.Records = new[]
            {
                data.Records[0],
                new EncyclopediaProgressRecord { EntryId = "entry-a", RevealStage = 9, IsNew = false },
                new EncyclopediaProgressRecord { EntryId = string.Empty, RevealStage = 1 },
                null,
                new EncyclopediaProgressRecord { EntryId = "entry-b", RevealStage = 0, IsNew = true },
            };
            var restored = EncyclopediaProgress.FromSaveData(data);

            Assert.AreEqual(2, restored.UnlockedCount);
            Assert.AreEqual(3, restored.GetRevealStage("entry-a"), "The first saved record wins.");
            Assert.AreEqual(1, restored.GetRevealStage("entry-b"));
            Assert.AreEqual(0, restored.GetRevealStage("entry-missing"));
            Assert.AreEqual("default", EncyclopediaProgress.FromSaveData(null).SaveSlot);
        }

        [Test]
        public void ENCYCLOPEDIA_EDIT_004_ReapplyingUnlockIsIdempotent()
        {
            var progress = new EncyclopediaProgress("slot-a", "player-a");

            Assert.IsTrue(progress.TryUnlock("entry-a", 1, 1L));
            Assert.IsFalse(progress.TryUnlock("entry-a", 1, 2L));
            Assert.IsFalse(progress.TryUnlock(string.Empty, 1, 2L));
            Assert.IsFalse(progress.MarkSeen("entry-missing"));
            Assert.AreEqual(1, progress.UnlockedCount);
            Assert.AreEqual(1, progress.NewCount);
            Assert.IsTrue(progress.MarkSeen("entry-a"));
            Assert.IsFalse(progress.MarkSeen("entry-a"));
            Assert.AreEqual(0, progress.NewCount);
        }

        [Test]
        public void ENCYCLOPEDIA_EDIT_005_IndexCachesLookupWithoutRegistryRescanOnRefresh()
        {
            var category = new EncyclopediaCategory("cat", "Cat", 0);
            var entries = new[] { Entry("entry-b", category), Entry("entry-a", category) };
            var index = new EncyclopediaIndex(new[] { category }, entries);
            var progress = new EncyclopediaProgress("slot-a", "player-a");
            progress.TryUnlock("entry-a", 1, 1L);

            Assert.AreEqual(1, index.BuildVersion);
            Assert.AreEqual(2, index.GetEntries(category).Count);
            var rows = index.GetVisibleRows(category, progress, 0, 25);
            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual("entry-a", rows[0].Entry.Id, "Entries are sorted by id.");
            Assert.IsTrue(rows[0].IsUnlocked);
            Assert.AreEqual("entry-a", rows[0].Title);
            Assert.AreEqual("???", rows[1].Title);
            Assert.AreEqual("Find a clue", rows[1].Summary);
            Assert.IsTrue(index.TryGetEntry("entry-b", out _));
            Assert.IsFalse(index.TryGetEntry(null, out _));
            Assert.AreEqual(1, index.BuildVersion, "UI row refreshes must use the cached index rather than rebuilding registry lookup tables.");
        }

        [Test]
        public void ENCYCLOPEDIA_CORE_IndexAddsCategoriesFromEntriesAndSortsCategories()
        {
            var late = new EncyclopediaCategory("cat.late", "Late", 5);
            var early = new EncyclopediaCategory("cat.early", "Early", 1);
            var index = new EncyclopediaIndex(new[] { late }, new[] { Entry("entry-a", early), Entry("entry-orphan", null) });

            Assert.AreEqual(2, index.Categories.Count);
            Assert.AreSame(early, index.Categories[0]);
            Assert.AreEqual(0, index.GetEntries(null).Count);
            Assert.IsFalse(index.TryGetEntry("entry-orphan", out _), "Entries without a category are skipped.");
        }

        [Test]
        public void ENCYCLOPEDIA_EDIT_006_ListModelPagesMoreThanOneHundredEntries()
        {
            var category = new EncyclopediaCategory("cat", "Cat", 0);
            var entries = new EncyclopediaEntry[125];
            for (int i = 0; i < entries.Length; i++) entries[i] = Entry("entry-" + i.ToString("000"), category);

            var index = new EncyclopediaIndex(new[] { category }, entries);
            var page = index.GetVisibleRows(category, new EncyclopediaProgress("slot-a", "player-a"), 2, 50);

            Assert.AreEqual(25, page.Count);
            Assert.AreEqual("entry-100", page[0].Entry.Id);
            Assert.AreEqual("entry-124", page[24].Entry.Id);
            Assert.AreEqual(0, index.GetVisibleRows(category, null, 9, 50).Count);
        }

        [Test]
        public void ENCYCLOPEDIA_CORE_UnlockerUnlocksSatisfiedEntriesOnceAndReportsThem()
        {
            var progress = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            progress.RecordActivityCompleted("location.library");
            var context = new EncyclopediaUnlockContext(progress, null, null);
            var open = Entry("entry.library", null, new IEncyclopediaUnlockCondition[] { new EncyclopediaLocationUnlockedCondition("location.library") }, revealStage: 2);
            var closed = Entry("entry.cafe", null, new IEncyclopediaUnlockCondition[] { new EncyclopediaLocationUnlockedCondition("location.cafe") });
            var encyclopedia = new EncyclopediaProgress("slot-a", "player-a");
            var reported = new List<string>();

            Assert.AreEqual(1, EncyclopediaUnlocker.EvaluateUnlocks(new[] { open, closed, null }, context, encyclopedia, 77L, e => reported.Add(e.Id)));
            Assert.AreEqual(0, EncyclopediaUnlocker.EvaluateUnlocks(new[] { open, closed }, context, encyclopedia, 78L, e => reported.Add(e.Id)));

            CollectionAssert.AreEqual(new[] { "entry.library" }, reported);
            Assert.AreEqual(2, encyclopedia.GetRevealStage("entry.library"));
            Assert.AreEqual(77L, encyclopedia.ToSaveData().Records[0].FirstDiscoveredUtcTicks);
            Assert.AreEqual(0, EncyclopediaUnlocker.EvaluateUnlocks(new[] { open }, context, null, 1L, null));
        }
    }
}
