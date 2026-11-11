using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Core.DiscoveryClues;
using Rootborn.Core.Encyclopedia;
using Rootborn.Core.Quests;
using Rootborn.Core.StudentLife;
using Rootborn.Core.WorldState;

namespace Rootborn.Core.Tests
{
    // 옮겨 옴: Assets/Tests/EditMode/DiscoveryClues/DiscoveryClueLoopTests.cs (DiscoveryClueRegistryTests는 등록부 검사라 옮기지 않음)
    public sealed class DiscoveryClueTests
    {
        private sealed class MemoryStore : IDiscoveryClueSaveStore
        {
            public readonly Dictionary<string, DiscoveryClueProgressSaveData> Files = new Dictionary<string, DiscoveryClueProgressSaveData>();
            public DiscoveryClueProgressSaveData Read(string saveSlot, string fileName) => Files.TryGetValue(saveSlot + "/" + fileName, out var data) ? data : null;
            public void Write(string saveSlot, string fileName, DiscoveryClueProgressSaveData data) => Files[saveSlot + "/" + fileName] = data;
        }

        private static WorldStateFlag MakeFlag(string id)
            => new WorldStateFlag(id, id, id + ".desc", null, null, null, WorldStateScopeKind.Shared, WorldStateChangeKind.ObjectRevealed, "Use it",
                new[] { WorldStateSummarySurface.WorldLog }, new[] { WorldStateBadgeKind.Shared }, 0, 1);

        private static DiscoveryClue MakeClue(string id, WorldStateFlag relatedFlag, DiscoveryClueSource[] sources, IDiscoveryClueCondition[] conditions, IDiscoveryClueCompletion[] completions, IDiscoveryClueOutcome[] outcomes, string relatedLocationId = null, int sortPriority = 0)
            => new DiscoveryClue(id, id + ".name", id + ".desc", relatedFlag, null, relatedLocationId, 3, "Rumor", "Trace", sources, conditions, completions, outcomes,
                new[] { DiscoveryClueSummarySurface.NpcDialogue, DiscoveryClueSummarySurface.Board, DiscoveryClueSummarySurface.WorldLog, DiscoveryClueSummarySurface.Encyclopedia }, sortPriority);

        private static QuestDefinition MakeQuest(string id)
            => new QuestDefinition(id, id, id + ".desc", new QuestObjective[] { new TalkQuestObjective("talk", 2, "npc.a") });

        private static readonly DiscoveryClueCompletionEvent ArchiveVisit = new DiscoveryClueCompletionEvent(DiscoveryClueCompletionKind.LocationVisited, "location.library.archive-room");

        [Test]
        public void DISCOVERY_CLUE_EDIT_001_005_DefinitionSourcesConditionsCompletionsOutcomesAndSummaryAreDataDriven()
        {
            var flag = MakeFlag("world.library.archive-open");
            var npcSource = new DiscoveryClueSource("source.librarian", DiscoveryClueSourceKind.NpcDialogue, "Librarian", "Something old moved under the archive.", null, flag, 2);
            var boardSource = new DiscoveryClueSource("source.board", DiscoveryClueSourceKind.BoardPost, "Town Board", "A torn map points toward the archive room.", null, flag, 1);
            var clue = MakeClue("clue.library.archive-rumor", flag, new[] { npcSource, boardSource }, new IDiscoveryClueCondition[] { new DiscoveryClueWorldStateActiveCondition() },
                new IDiscoveryClueCompletion[] { new DiscoveryClueLocationVisitCompletion(null) }, Array.Empty<IDiscoveryClueOutcome>());
            var world = new WorldStateProgress("slot-a", "player-a");
            world.TryActivate(flag, new WorldStateActivationSource("quest.library", "step.reward", "event.reward", 1));
            var progress = new DiscoveryClueProgress("slot-a", "player-a");
            var context = new DiscoveryClueContext(world, progress, new StudentLifeProgress("slot-a", "player-a", 10, 10), null, null, 1);
            var cache = new DiscoveryClueLookupCache(new[] { clue });

            var summaries = DiscoveryClueSummaryBuilder.BuildForSource(cache, DiscoveryClueSourceKind.NpcDialogue, "source.librarian", context, DiscoveryClueSummarySurface.NpcDialogue);

            Assert.IsTrue(cache.TryGetById(clue.Id, out var loaded));
            Assert.AreSame(clue, loaded);
            Assert.AreEqual(1, summaries.Length);
            Assert.AreEqual(clue.Id, summaries[0].ClueId);
            Assert.AreEqual(DiscoveryClueSourceKind.NpcDialogue, summaries[0].SourceKind);
            Assert.AreEqual(2, DiscoveryClueSummaryBuilder.BuildForClue(cache, clue.Id, context, DiscoveryClueSummarySurface.WorldLog).Length, "A single clue must expose multiple source summaries without clue-id branching.");
        }

        [Test]
        public void DISCOVERY_CLUE_EDIT_006_008_OutcomesCompleteAndRewardOnlyOnceAcrossSaveLoad()
        {
            var flag = MakeFlag("world.library.archive-open");
            var quest = MakeQuest("quest.library.follow-up");
            var entry = new EncyclopediaEntry("encyclopedia.archive-fragment", null, "Archive Fragment", "???", "Archive desc", "Find it", null, null);
            var source = new DiscoveryClueSource("source.board", DiscoveryClueSourceKind.BoardPost, "Board", "Archive clue", null, flag, 1);
            var clue = MakeClue("clue.library.archive-rumor", flag, new[] { source }, Array.Empty<IDiscoveryClueCondition>(),
                new IDiscoveryClueCompletion[] { new DiscoveryClueLocationVisitCompletion("location.library.archive-room") },
                new IDiscoveryClueOutcome[]
                {
                    new DiscoveryClueQuestUnlockOutcome(new[] { quest }),
                    new DiscoveryClueCareerHintOutcome(new[] { "career.research" }),
                    new DiscoveryClueEncyclopediaOutcome(new[] { entry }),
                    new DiscoveryClueWorldStateRevealOutcome(new[] { flag }),
                });
            var world = new WorldStateProgress("slot-a", "player-a");
            var clueProgress = new DiscoveryClueProgress("slot-a", "player-a");
            var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            var questLog = new QuestLog(Array.Empty<QuestDefinition>());
            var encyclopedia = new EncyclopediaProgress("slot-a", "player-a");
            var context = new DiscoveryClueContext(world, clueProgress, student, questLog, encyclopedia, 1, 77L);
            var runner = new DiscoveryClueRunner();

            Assert.IsTrue(runner.TryRevealSource(clue, source, context, out var reveal));
            Assert.AreEqual(DiscoveryClueResultKind.Revealed, reveal.Kind);
            Assert.IsTrue(runner.TryComplete(clue, ArchiveVisit, context, out var completed));
            Assert.AreEqual(DiscoveryClueResultKind.Completed, completed.Kind);
            CollectionAssert.AreEqual(new[] { "quest", "career-hint", "encyclopedia", "world-state" }, completed.OutcomeIds);
            Assert.AreEqual(QuestStage.Active, questLog.GetState(quest));
            Assert.IsTrue(student.IsCareerHintUnlocked("career.research"));
            Assert.IsTrue(encyclopedia.IsUnlocked(entry.Id));
            Assert.AreEqual(77L, encyclopedia.ToSaveData().Records[0].FirstDiscoveredUtcTicks);
            Assert.IsTrue(world.IsActive(flag));
            Assert.AreEqual("clue.library.archive-rumor", world.GetRecord(flag).SourceEventId, "The clue id is the activation event.");

            var restored = DiscoveryClueProgress.FromSaveData(clueProgress.ToSaveData());
            Assert.IsTrue(restored.GetRecord(clue.Id).Seen);
            Assert.IsTrue(restored.GetRecord(clue.Id).Completed);
            Assert.IsTrue(restored.GetRecord(clue.Id).RewardClaimed);
            Assert.AreEqual(1, restored.TodayClueSummary.Length);
            var duplicateContext = new DiscoveryClueContext(world, restored, student, questLog, encyclopedia, 2);
            Assert.IsFalse(runner.TryComplete(clue, ArchiveVisit, duplicateContext, out var duplicate));
            Assert.AreEqual(DiscoveryClueResultKind.AlreadyCompleted, duplicate.Kind);
            Assert.AreEqual(1, restored.GetRecord(clue.Id).CompletedCount);
        }

        [Test]
        public void DISCOVERY_CLUE_EDIT_009_SummaryBuilderUsesLookupCacheInsteadOfRegistryScan()
        {
            var flag = MakeFlag("world.shop.stock-open");
            var source = new DiscoveryClueSource("source.shop-board", DiscoveryClueSourceKind.BoardPost, "Shop Board", "Stock changed.", null, flag, 1);
            var clue = MakeClue("clue.shop.stock", flag, new[] { source }, null, null, null);
            var cache = new DiscoveryClueLookupCache(new[] { clue });
            var context = new DiscoveryClueContext(null, new DiscoveryClueProgress("slot-a", "player-a"), null, null, null, 1);

            DiscoveryClueSummaryBuilder.BuildForSource(cache, DiscoveryClueSourceKind.BoardPost, "source.shop-board", context, DiscoveryClueSummarySurface.Board);
            DiscoveryClueSummaryBuilder.BuildForSource(cache, DiscoveryClueSourceKind.BoardPost, "source.shop-board", context, DiscoveryClueSummarySurface.Board);

            Assert.AreEqual(1, cache.BuildCount);
            Assert.GreaterOrEqual(cache.LookupCount, 2);
        }

        [Test]
        public void DISCOVERY_CLUE_CORE_RejectedRequestsChangeNothing()
        {
            var flag = MakeFlag("world.a");
            var source = new DiscoveryClueSource("source.a", DiscoveryClueSourceKind.MapHint, null, null, null, null, 0);
            var clue = MakeClue("clue.a", flag, new[] { source }, new IDiscoveryClueCondition[] { new DiscoveryClueWorldStateActiveCondition() },
                new IDiscoveryClueCompletion[] { new DiscoveryClueLocationVisitCompletion("location.a") }, new IDiscoveryClueOutcome[] { new DiscoveryClueCareerHintOutcome(new[] { "career.x" }) });
            var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            var progress = new DiscoveryClueProgress("slot-a", "player-a");
            var runner = new DiscoveryClueRunner();
            var inactive = new DiscoveryClueContext(new WorldStateProgress("slot-a", "player-a"), progress, student, null, null, 1);

            Assert.IsFalse(runner.TryRevealSource(clue, source, inactive, out var condition));
            Assert.AreEqual(DiscoveryClueResultKind.ConditionFailed, condition.Kind);
            Assert.IsFalse(runner.TryComplete(clue, new DiscoveryClueCompletionEvent(DiscoveryClueCompletionKind.LocationVisited, "location.a"), inactive, out var completeCondition));
            Assert.AreEqual(DiscoveryClueResultKind.ConditionFailed, completeCondition.Kind);
            Assert.IsFalse(runner.TryRevealSource(clue, null, inactive, out var noSource));
            Assert.AreEqual(DiscoveryClueResultKind.InvalidRequest, noSource.Kind);
            Assert.IsFalse(runner.TryRevealSource(null, source, inactive, out var noClue));
            Assert.AreEqual(string.Empty, noClue.ClueId);
            Assert.IsFalse(runner.TryComplete(clue, default, new DiscoveryClueContext(null, null, student, null, null, 1), out var noProgress));
            Assert.AreEqual(DiscoveryClueResultKind.InvalidRequest, noProgress.Kind);

            var world = new WorldStateProgress("slot-a", "player-a");
            world.TryActivate(flag, default);
            var active = new DiscoveryClueContext(world, progress, student, null, null, 1);
            Assert.IsFalse(runner.TryComplete(clue, new DiscoveryClueCompletionEvent(DiscoveryClueCompletionKind.LocationVisited, "location.b"), active, out var wrongPlace));
            Assert.AreEqual(DiscoveryClueResultKind.CompletionNotMatched, wrongPlace.Kind);
            Assert.IsFalse(runner.TryComplete(clue, new DiscoveryClueCompletionEvent(DiscoveryClueCompletionKind.NpcTalked, "location.a"), active, out var wrongKind));
            Assert.AreEqual(DiscoveryClueResultKind.CompletionNotMatched, wrongKind.Kind);

            Assert.IsFalse(student.IsCareerHintUnlocked("career.x"));
            Assert.AreEqual(0, progress.RecordCount, "Rejected requests and lookups do not leave records.");
            Assert.AreEqual(0, progress.TodayClueSummary.Length);
        }

        [Test]
        public void DISCOVERY_CLUE_CORE_CompletionFallsBackToRelatedLocationAndEmptyCompletionsAlwaysMatch()
        {
            var byLocation = MakeClue("clue.loc", null, null, null, new IDiscoveryClueCompletion[] { new DiscoveryClueLocationVisitCompletion(string.Empty) }, null, "location.park");
            var nowhere = MakeClue("clue.nowhere", null, null, null, new IDiscoveryClueCompletion[] { new DiscoveryClueLocationVisitCompletion(null) }, null);
            var any = MakeClue("clue.any", null, null, null, null, null);
            var context = new DiscoveryClueContext(null, new DiscoveryClueProgress("slot-a", "player-a"), null, null, null, 1);

            Assert.IsTrue(byLocation.MatchesCompletion(new DiscoveryClueCompletionEvent(DiscoveryClueCompletionKind.LocationVisited, "location.park"), context));
            Assert.IsFalse(nowhere.MatchesCompletion(new DiscoveryClueCompletionEvent(DiscoveryClueCompletionKind.LocationVisited, string.Empty), context), "No expected location never matches.");
            Assert.IsTrue(any.MatchesCompletion(new DiscoveryClueCompletionEvent(DiscoveryClueCompletionKind.ActivityCompleted, null), context));
        }

        [Test]
        public void DISCOVERY_CLUE_CORE_OutcomesThatChangeNothingAreNotReportedButRecorded()
        {
            var flag = MakeFlag("world.a");
            var world = new WorldStateProgress("slot-a", "player-a");
            world.TryActivate(flag, default);
            var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            student.UnlockCareerHint("career.a");
            var quest = MakeQuest("quest.a");
            var questLog = new QuestLog(new[] { quest });
            questLog.Accept(quest);
            questLog.RecordEvent(new QuestEvent(QuestEventKind.Talk, "talk-1", "npc.a"));
            var clue = MakeClue("clue.a", null, null, null, null, new IDiscoveryClueOutcome[]
            {
                new DiscoveryClueWorldStateRevealOutcome(new[] { flag, null }),
                new DiscoveryClueCareerHintOutcome(new[] { "career.a", null }),
                new DiscoveryClueQuestUnlockOutcome(new[] { quest }),
                null,
            });
            var progress = new DiscoveryClueProgress("slot-a", "player-a");

            Assert.IsTrue(new DiscoveryClueRunner().TryComplete(clue, default, new DiscoveryClueContext(world, progress, student, questLog, null, 0), out var result));

            Assert.AreEqual(0, result.OutcomeIds.Length);
            Assert.AreEqual(1, questLog.GetObjectiveCount(quest, 0), "An accepted quest is not added again (Unity reset its progress).");
            var record = progress.GetRecord("clue.a");
            CollectionAssert.AreEqual(new[] { "world.a" }, record.RelatedWorldStateFlagIds);
            CollectionAssert.AreEqual(new[] { "career.a" }, record.RelatedCareerHintGrantHistory);
            CollectionAssert.AreEqual(new[] { "quest.a" }, record.RelatedFollowUpQuestIds);
            Assert.AreEqual(1, record.CompletedDay, "Day clamps to at least 1.");
        }

        [Test]
        public void DISCOVERY_CLUE_CORE_SourcesSeenAndTrackingAccumulate()
        {
            var a = new DiscoveryClueSource("source.a", DiscoveryClueSourceKind.NpcDialogue, null, null, "npc.a", null, 0);
            var b = new DiscoveryClueSource("source.b", DiscoveryClueSourceKind.BoardPost, null, null, null, null, 0);
            var clue = MakeClue("clue.a", null, new[] { a, b }, null, null, null);
            var progress = new DiscoveryClueProgress("slot-a", "player-a");

            progress.MarkSourceSeen(clue, a, 2);
            progress.MarkSourceSeen(clue, b, 4);
            progress.MarkSourceSeen(clue, a, 5);
            progress.MarkTracked(clue, true);
            progress.MarkTracked(null, true);

            var record = progress.GetRecord("clue.a");
            CollectionAssert.AreEqual(new[] { "source.a", "source.b" }, record.SourceIdsSeen);
            Assert.AreEqual(2, record.FirstSeenDay);
            Assert.AreEqual(5, record.LastSeenDay);
            Assert.IsTrue(record.Tracked);
            Assert.IsTrue(record.HasSeenSource("source.b"));
            Assert.IsFalse(record.HasSeenSource(null));
            Assert.AreEqual(1, progress.TodayClueSummary.Length);
            Assert.AreEqual("npc.a", a.RelatedNpcId);
            Assert.AreEqual("source.a", a.DisplayNameKey);
            Assert.AreEqual("source.a", a.HintTextKey);
        }

        [Test]
        public void DISCOVERY_CLUE_CORE_SummarySortsByPriorityPlusSourceStrengthAndHidesUnmetConditions()
        {
            var weak = new DiscoveryClueSource("source.weak", DiscoveryClueSourceKind.BoardPost, null, "weak hint", null, null, 1);
            var strong = new DiscoveryClueSource("source.strong", DiscoveryClueSourceKind.BoardPost, null, null, null, null, 9);
            var flag = MakeFlag("world.locked");
            var clue = MakeClue("clue.a", null, new[] { weak, strong, null }, null, null, null, sortPriority: 2);
            var hidden = MakeClue("clue.hidden", flag, new[] { weak }, new IDiscoveryClueCondition[] { new DiscoveryClueWorldStateActiveCondition() }, null, null);
            var progress = new DiscoveryClueProgress("slot-a", "player-a");
            var context = new DiscoveryClueContext(new WorldStateProgress("slot-a", "player-a"), progress, null, null, null, 1);
            var cache = new DiscoveryClueLookupCache(new[] { clue, hidden, null });

            var rows = DiscoveryClueSummaryBuilder.BuildForSurface(cache, context, DiscoveryClueSummarySurface.Board);

            Assert.AreEqual(2, rows.Length);
            Assert.AreEqual("source.strong", rows[0].SourceId);
            Assert.AreEqual(11, rows[0].SortPriority);
            Assert.AreEqual(9, rows[0].Strength, "Strength is the larger of clue and source.");
            Assert.AreEqual(3, rows[1].Strength);
            Assert.AreEqual("weak hint", rows[1].HintText);
            Assert.AreEqual("Rumor", rows[1].PublicText);
            Assert.AreEqual(0, DiscoveryClueSummaryBuilder.BuildForSurface(cache, context, DiscoveryClueSummarySurface.Hud).Length, "Surface filter applies.");
            Assert.AreEqual(0, DiscoveryClueSummaryBuilder.BuildForClue(cache, "clue.missing", context, DiscoveryClueSummarySurface.Board).Length);
            Assert.AreEqual(0, DiscoveryClueSummaryBuilder.BuildForSource(cache, DiscoveryClueSourceKind.MapHint, "source.weak", context, DiscoveryClueSummarySurface.Board).Length, "Kind must match too.");
            Assert.AreEqual(0, progress.RecordCount);
            Assert.AreEqual("clue.b.desc", MakeClue("clue.b", null, null, null, null, null).DescriptionKey);
        }

        [Test]
        public void DISCOVERY_CLUE_CORE_ExplorationAccessMarksByIdAndFallsBackToFirstSource()
        {
            var first = new DiscoveryClueSource("source.first", DiscoveryClueSourceKind.LocationTrace, null, null, null, null, 0);
            var clue = MakeClue("clue.a", null, new[] { first }, null, null, null);
            var progress = new DiscoveryClueProgress("slot-a", "player-a");
            IExplorationClues access = new DiscoveryClueAccess(progress, new DiscoveryClueLookupCache(new[] { clue }));

            Assert.IsFalse(access.HasSeen("clue.a"));
            access.MarkSourceSeen("clue.a", string.Empty, 3);
            access.MarkSourceSeen("clue.unknown", "source.x", 3);
            access.MarkSourceSeen(null, "source.x", 3);

            Assert.IsTrue(access.HasSeen("clue.a"));
            CollectionAssert.AreEqual(new[] { "source.first" }, progress.GetRecord("clue.a").SourceIdsSeen);
            Assert.AreEqual(3, progress.GetRecord("clue.a").FirstSeenDay);
            Assert.IsFalse(access.HasSeen("clue.unknown"), "Clues not in the definitions are ignored.");
            Assert.AreEqual(1, progress.RecordCount);
        }

        [Test]
        public void DISCOVERY_CLUE_CORE_SaveFilesRoundTripAndClampNegatives()
        {
            var store = new MemoryStore();
            var progress = DiscoveryClueSaveFiles.LoadOrCreate(store, null, null);
            var clue = MakeClue("clue.a", null, null, null, null, null);
            progress.MarkSourceSeen(clue, null, 2);
            DiscoveryClueSaveFiles.Save(store, progress);

            Assert.IsTrue(store.Files.ContainsKey("default/discovery-clues-player.json"));
            Assert.AreEqual("discovery-clues-steam:7.json", DiscoveryClueSaveFiles.FileNameFor("steam:7"), "Unity did not sanitize this file name; kept as is.");
            var loaded = DiscoveryClueSaveFiles.LoadOrCreate(store, "default", "player");
            Assert.IsTrue(loaded.GetRecord("clue.a").Seen);

            var data = new DiscoveryClueProgressSaveData
            {
                SaveSlot = "slot-a",
                PlayerId = "player-a",
                Records = new[] { new DiscoveryClueRecordSaveData { ClueId = "clue.a", FirstSeenDay = -2, CompletedCount = -1, SourceIdsSeen = new[] { "s", null, "s" } }, null, new DiscoveryClueRecordSaveData() },
                TodayClueIds = new[] { "clue.a", "clue.missing", "clue.a", null },
            };
            var restored = DiscoveryClueProgress.FromSaveData(data);
            Assert.AreEqual(1, restored.RecordCount);
            Assert.AreEqual(0, restored.GetRecord("clue.a").FirstSeenDay);
            Assert.AreEqual(0, restored.GetRecord("clue.a").CompletedCount);
            CollectionAssert.AreEqual(new[] { "s" }, restored.GetRecord("clue.a").SourceIdsSeen);
            Assert.AreEqual(1, restored.TodayClueSummary.Length);
            Assert.AreEqual("default", DiscoveryClueProgress.FromSaveData(null).SaveSlot);
        }
    }
}
