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
    // 옮겨 옴: Assets/Tests/EditMode/DiscoveryClues/ClueInterpretationLoopTests.cs, ClueInterpretationPersistenceTests.cs
    // (ClueInterpretationRegistryTests는 등록부 검사라 옮기지 않음)
    public sealed class ClueInterpretationTests
    {
        private const string ClueId = "clue.archive.shard";

        private static ClueInterpretation MakeInterpretation(string id, ClueInterpretationSource[] sources, IClueInterpretationCondition[] conditions, IClueInterpretationOutcome[] outcomes, ClueInterpretationPolicy policy, int sortPriority = 0)
            => new ClueInterpretation(id, id + ".name", id + ".desc", ClueId, "method." + id, "public." + id, "hidden." + id, sources, conditions, outcomes, policy, sortPriority);

        private static ClueInterpretationSource MakeSource(string id, ClueInterpretationSourceKind kind, string targetId)
            => new ClueInterpretationSource(id, kind, targetId, id + ".name", id + ".hint", 1);

        private static ClueInterpretationPolicy MakePolicy(string id, ClueInterpretationPolicyKind kind, string groupId, int sequenceOrder = 0, int cooldownDays = 0)
            => new ClueInterpretationPolicy(id, kind, groupId, sequenceOrder, cooldownDays, false);

        private static WorldStateFlag MakeFlag(string id)
            => new WorldStateFlag(id, id + ".name", id + ".desc", null, null, null, WorldStateScopeKind.Shared, WorldStateChangeKind.ObjectRevealed, "use",
                new[] { WorldStateSummarySurface.WorldLog }, new[] { WorldStateBadgeKind.Shared }, 0, 1);

        private static QuestDefinition MakeQuest(string id)
            => new QuestDefinition(id, id + ".name", id + ".desc", new QuestObjective[] { new TalkQuestObjective("talk", 2, "npc.a") });

        private static DiscoveryClueProgress SeenClue()
        {
            var progress = new DiscoveryClueProgress("slot-a", "player-a");
            progress.MarkSourceSeen(ClueId, ClueId + ".source", 1);
            return progress;
        }

        private static ClueInterpretationContext Context(ClueInterpretationProgress progress, ClueInterpretationLookupCache catalog = null, int day = 1, StudentLifeProgress student = null,
            QuestLog questLog = null, WorldStateProgress world = null, EncyclopediaProgress encyclopedia = null, DiscoveryClueProgress clues = null)
            => new ClueInterpretationContext(world ?? new WorldStateProgress("slot-a", "player-a"), clues ?? SeenClue(), progress, student ?? new StudentLifeProgress("slot-a", "player-a", 10, 10),
                questLog ?? new QuestLog(Array.Empty<QuestDefinition>()), encyclopedia ?? new EncyclopediaProgress("slot-a", "player-a"), day, catalog);

        [Test]
        public void CLUE_INTERPRET_EDIT_001_005_DefinitionSourcesConditionsOutcomesAndPolicyAreDataDriven()
        {
            var librarian = MakeSource("interpret.source.librarian", ClueInterpretationSourceKind.NpcDialogue, "npc.librarian");
            var workshop = MakeSource("interpret.source.workshop", ClueInterpretationSourceKind.ObjectInteraction, "object.workbench");
            var shop = MakeSource("interpret.source.shop", ClueInterpretationSourceKind.NpcDialogue, "npc.shopkeeper");
            var conditionA = new ClueInterpretationClueSeenCondition();
            var conditionB = new ClueInterpretationRelationshipThresholdCondition("rel.librarian", 1);
            var exclusive = MakePolicy("policy.archive.exclusive", ClueInterpretationPolicyKind.Exclusive, "group.archive");
            var sequential = MakePolicy("policy.archive.sequential", ClueInterpretationPolicyKind.Sequential, "group.archive.sequence");
            var npcInterpretation = MakeInterpretation("interpret.ask-librarian", new[] { librarian }, new IClueInterpretationCondition[] { conditionA, conditionB },
                new IClueInterpretationOutcome[] { new ClueInterpretationEncyclopediaOutcome(new[] { new EncyclopediaEntry("entry.archive-shard", null, null, null, null, null, null, null) }), new ClueInterpretationCareerHintOutcome(new[] { "career.researcher" }) }, exclusive);
            var objectInterpretation = MakeInterpretation("interpret.restore-workbench", new[] { workshop }, new IClueInterpretationCondition[] { conditionA },
                new IClueInterpretationOutcome[] { new ClueInterpretationQuestUnlockOutcome(new[] { MakeQuest("quest.restore-shard") }), new ClueInterpretationWorldStateOutcome(new[] { MakeFlag("world.archive-shard-restored") }), new ClueInterpretationRelationshipDeltaOutcome("rel.librarian", 2) }, exclusive);
            var shopInterpretation = MakeInterpretation("interpret.ask-shopkeeper", new[] { shop }, new IClueInterpretationCondition[] { conditionA }, new IClueInterpretationOutcome[] { new ClueInterpretationStatusDeltaOutcome("status.curiosity", 3) }, sequential);
            var cache = new ClueInterpretationLookupCache(new[] { npcInterpretation, objectInterpretation, shopInterpretation });
            var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            student.AddRelationship("rel.librarian", 1);
            var context = Context(new ClueInterpretationProgress("slot-a", "player-a"), cache, student: student);

            var summaries = ClueInterpretationSummaryBuilder.BuildForClue(cache, ClueId, context, ClueInterpretationSummarySurface.ClueDetail);
            var sourceSummaries = ClueInterpretationSummaryBuilder.BuildForSource(cache, ClueInterpretationSourceKind.NpcDialogue, "npc.librarian", context, ClueInterpretationSummarySurface.NpcDialogue);

            Assert.AreEqual(3, summaries.Length, "One clue must expose three interpretation paths from definitions only.");
            Assert.AreEqual(1, sourceSummaries.Length);
            Assert.AreEqual("interpret.ask-librarian", sourceSummaries[0].InterpretationId);
            Assert.IsTrue(sourceSummaries[0].Available);
            Assert.AreEqual(1, cache.BuildCount, "Summary lookup must build an index once instead of scanning every refresh.");
            Assert.GreaterOrEqual(cache.LookupCount, 2);
        }

        [Test]
        public void CLUE_INTERPRET_EDIT_006_010_PoliciesSaveLoadAndDuplicateOutcomesAreIdempotent()
        {
            var sourceA = MakeSource("interpret.source.a", ClueInterpretationSourceKind.NpcDialogue, "npc.a");
            var sourceB = MakeSource("interpret.source.b", ClueInterpretationSourceKind.LocationInvestigation, "location.archive");
            var entry = new EncyclopediaEntry("entry.archive-shard", null, null, null, null, null, null, null);
            var flag = MakeFlag("world.archive-open");
            var quest = MakeQuest("quest.archive-followup");
            var nonExclusive = MakePolicy("policy.archive.nonexclusive", ClueInterpretationPolicyKind.NonExclusive, "group.archive.multi");
            var exclusive = MakePolicy("policy.archive.exclusive", ClueInterpretationPolicyKind.Exclusive, "group.archive.single");
            var worldOutcome = new ClueInterpretationWorldStateOutcome(new[] { flag });
            var first = MakeInterpretation("interpret.first", new[] { sourceA }, null, new IClueInterpretationOutcome[] { new ClueInterpretationCareerHintOutcome(new[] { "career.researcher" }), new ClueInterpretationEncyclopediaOutcome(new[] { entry }) }, nonExclusive);
            var second = MakeInterpretation("interpret.second", new[] { sourceB }, null, new IClueInterpretationOutcome[] { worldOutcome, new ClueInterpretationQuestUnlockOutcome(new[] { quest }) }, nonExclusive);
            var locked = MakeInterpretation("interpret.locked", new[] { sourceB }, null, new IClueInterpretationOutcome[] { worldOutcome }, exclusive);
            var interpretationProgress = new ClueInterpretationProgress("slot-a", "player-a");
            var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            var questLog = new QuestLog(Array.Empty<QuestDefinition>());
            var encyclopedia = new EncyclopediaProgress("slot-a", "player-a");
            var world = new WorldStateProgress("slot-a", "player-a");
            var context = Context(interpretationProgress, null, 1, student, questLog, world, encyclopedia);
            var runner = new ClueInterpretationRunner();

            Assert.IsTrue(runner.TryComplete(first, context, out var firstResult));
            Assert.IsTrue(runner.TryComplete(second, context, out var secondResult));
            Assert.AreEqual(ClueInterpretationResultKind.Completed, firstResult.Kind);
            Assert.AreEqual(ClueInterpretationResultKind.Completed, secondResult.Kind);
            Assert.IsTrue(student.IsCareerHintUnlocked("career.researcher"));
            Assert.IsTrue(encyclopedia.IsUnlocked(entry.Id));
            Assert.IsTrue(world.IsActive(flag));
            Assert.AreEqual("interpret.second", world.GetRecord(flag).SourceEventId);
            Assert.AreEqual(QuestStage.Active, questLog.GetState(quest));

            var restored = ClueInterpretationProgress.FromSaveData(interpretationProgress.ToSaveData());
            Assert.IsTrue(restored.GetRecord(first.Id).Completed);
            Assert.IsTrue(restored.GetRecord(second.Id).Completed);
            Assert.IsTrue(restored.GetRecord(first.Id).RewardClaimed);
            Assert.AreEqual(2, restored.TodayInterpretationSummary.Length);
            var duplicateContext = Context(restored, null, 2, student, questLog, world, encyclopedia);
            Assert.IsFalse(runner.TryComplete(first, duplicateContext, out var duplicate));
            Assert.AreEqual(ClueInterpretationResultKind.AlreadyCompleted, duplicate.Kind);
            Assert.AreEqual(1, restored.GetRecord(first.Id).CompletedCount);
            Assert.IsTrue(runner.TryComplete(locked, duplicateContext, out var lockedResult));
            Assert.AreEqual(ClueInterpretationResultKind.Completed, lockedResult.Kind);
            var blocked = MakeInterpretation("interpret.blocked-by-exclusive", new[] { sourceB }, null, new IClueInterpretationOutcome[] { worldOutcome }, exclusive);
            Assert.IsFalse(runner.TryComplete(blocked, duplicateContext, out var blockedResult));
            Assert.AreEqual(ClueInterpretationResultKind.LockedByPolicy, blockedResult.Kind);
            Assert.AreEqual(0, restored.GetRecord(blocked.Id).CompletedCount);
        }

        [Test]
        public void CLUE_INTERPRET_EDIT_009_SaveFilesRestoreSelectedCompletedRewardAndExpansionState()
        {
            var store = new InMemorySaveStore();
            var interpretation = MakeInterpretation("interpret.ask-librarian", null, null, null, MakePolicy("policy.interpret.nonexclusive", ClueInterpretationPolicyKind.NonExclusive, "policy.group.play-loop"));
            var progress = new ClueInterpretationProgress("slot-a", "player-a");
            progress.MarkCompleted(interpretation, 2, new[] { "encyclopedia", "career-hint" }, new[] { "entry.archive" }, new[] { "career.researcher" }, new[] { "quest.followup" },
                new[] { "world.archive" }, new[] { "rel.librarian" }, new[] { "status.curiosity" });

            ClueInterpretationSaveFiles.Save(store, progress);
            var restored = ClueInterpretationSaveFiles.LoadOrCreate(store, "slot-a", "player-a");
            var record = restored.GetRecord("interpret.ask-librarian");

            Assert.IsTrue(store.Contains("slot-a", "clue-interpretations-player-a.json"));
            Assert.AreEqual("slot-a", restored.SaveSlot);
            Assert.AreEqual("player-a", restored.PlayerId);
            Assert.IsTrue(record.Selected);
            Assert.IsTrue(record.Completed);
            Assert.IsTrue(record.RewardClaimed);
            Assert.AreEqual(1, record.CompletedCount);
            Assert.AreEqual(ClueId, record.ClueId);
            Assert.AreEqual("policy.group.play-loop", record.PolicyGroupId);
            CollectionAssert.AreEqual(new[] { "encyclopedia", "career-hint" }, record.RewardClaimedIds);
            CollectionAssert.Contains(record.EncyclopediaExpansionIds, "entry.archive");
            CollectionAssert.Contains(record.CareerHintGrantHistory, "career.researcher");
            CollectionAssert.Contains(record.FollowUpQuestIds, "quest.followup");
            CollectionAssert.Contains(record.RelatedWorldStateChanges, "world.archive");
            CollectionAssert.Contains(record.RelationshipChangeIds, "rel.librarian");
            CollectionAssert.Contains(record.StatusChangeIds, "status.curiosity");
            Assert.AreEqual(1, restored.TodayInterpretationSummary.Length);
            Assert.AreEqual(0, ClueInterpretationSaveFiles.LoadOrCreate(store, "slot-a", "player-b").TodayInterpretationSummary.Length);
        }

        [Test]
        public void CLUE_INTERPRET_CORE_SequentialNeedsPreviousOrderCompleted()
        {
            // Unity 판과 달라진 동작: 원본은 같은 그룹에서 아무 해석이나 끝나 있으면(자기 자신 포함) 통과시켰다.
            var step0 = MakeInterpretation("interpret.step0", null, null, null, MakePolicy("p0", ClueInterpretationPolicyKind.Sequential, "group.seq", 0));
            var step1 = MakeInterpretation("interpret.step1", null, null, null, MakePolicy("p1", ClueInterpretationPolicyKind.Sequential, "group.seq", 1));
            var step2a = MakeInterpretation("interpret.step2a", null, null, null, MakePolicy("p2a", ClueInterpretationPolicyKind.Sequential, "group.seq", 2));
            var step2b = MakeInterpretation("interpret.step2b", null, null, null, MakePolicy("p2b", ClueInterpretationPolicyKind.Sequential, "group.seq", 2));
            var step4 = MakeInterpretation("interpret.step4", null, null, null, MakePolicy("p4", ClueInterpretationPolicyKind.Sequential, "group.seq", 4));
            var catalog = new ClueInterpretationLookupCache(new[] { step0, step1, step2a, step2b, step4 });
            var progress = new ClueInterpretationProgress("slot-a", "player-a");
            var runner = new ClueInterpretationRunner();

            Assert.IsFalse(runner.TryComplete(step2a, Context(progress, catalog), out var early));
            Assert.AreEqual(ClueInterpretationResultKind.LockedByPolicy, early.Kind);
            Assert.AreEqual("previous interpretation required", early.Reason);
            Assert.IsTrue(runner.TryComplete(step0, Context(progress, catalog), out _));
            Assert.IsFalse(runner.TryComplete(step2a, Context(progress, catalog), out _), "Step 0 alone does not open step 2.");
            Assert.IsTrue(runner.TryComplete(step1, Context(progress, catalog), out _));
            Assert.IsTrue(runner.TryComplete(step2b, Context(progress, catalog), out _));
            Assert.IsTrue(runner.TryComplete(step4, Context(progress, catalog), out _), "Order 3 is not defined, so order 2 is the previous step.");
            Assert.IsTrue(runner.TryComplete(step2a, Context(progress, catalog), out _), "Same-order steps are alternatives; either opens the next.");

            var fresh = new ClueInterpretationProgress("slot-a", "player-a");
            Assert.IsFalse(runner.TryComplete(step1, Context(fresh, null), out var noCatalog), "Without the interpretation list the previous step cannot be checked.");
            Assert.AreEqual(ClueInterpretationResultKind.LockedByPolicy, noCatalog.Kind);
            Assert.IsTrue(runner.TryComplete(step0, Context(fresh, null), out _), "Order 0 needs nothing.");
        }

        [Test]
        public void CLUE_INTERPRET_CORE_ConditionFailureReportsReasonAndChangesNothing()
        {
            var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            var flag = MakeFlag("world.archive");
            var interpretation = MakeInterpretation("interpret.a", null,
                new IClueInterpretationCondition[] { new ClueInterpretationClueSeenCondition(), new ClueInterpretationWorldStateActiveCondition(flag, "reason.world") },
                new IClueInterpretationOutcome[] { new ClueInterpretationStatusDeltaOutcome("status.a", 2) }, null);
            var progress = new ClueInterpretationProgress("slot-a", "player-a");
            var runner = new ClueInterpretationRunner();

            Assert.IsFalse(runner.TryComplete(interpretation, Context(progress, student: student), out var worldOff));
            Assert.AreEqual(ClueInterpretationResultKind.ConditionFailed, worldOff.Kind);
            Assert.AreEqual("reason.world", worldOff.Reason);
            Assert.IsFalse(runner.TryComplete(interpretation, Context(progress, student: student, clues: new DiscoveryClueProgress("slot-a", "player-a")), out var unseen));
            Assert.AreEqual("condition.missing", unseen.Reason);
            Assert.IsFalse(runner.TryComplete(null, Context(progress), out var missing));
            Assert.AreEqual(ClueInterpretationResultKind.InvalidRequest, missing.Kind);
            Assert.IsFalse(runner.TryComplete(interpretation, new ClueInterpretationContext(null, null, null, student, null, null, 1), out var noProgress));
            Assert.AreEqual(ClueInterpretationResultKind.InvalidRequest, noProgress.Kind);

            Assert.AreEqual(0, student.GetStatusValue("status.a"));
            Assert.AreEqual(0, progress.TodayInterpretationSummary.Length);
            Assert.AreEqual(0, progress.ToSaveData().Records.Length, "Rejected requests and lookups leave no records.");
            Assert.IsFalse(new ClueInterpretationRelationshipThresholdCondition(null, 0).Evaluate(Context(progress, student: student), interpretation));
            Assert.IsTrue(new ClueInterpretationRelationshipThresholdCondition("rel.a", -5).Evaluate(Context(progress, student: student), interpretation), "Minimum clamps to 0.");
            Assert.IsFalse(new ClueInterpretationClueSeenCondition().Evaluate(Context(progress), MakeInterpretationWithoutClue()));
        }

        private static ClueInterpretation MakeInterpretationWithoutClue()
            => new ClueInterpretation("interpret.no-clue", null, null, null, null, null, null, null, null, null, null, 0);

        [Test]
        public void CLUE_INTERPRET_CORE_RepeatableHonoursCooldownAndReappliesDeltas()
        {
            var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            var quest = MakeQuest("quest.repeat");
            var questLog = new QuestLog(Array.Empty<QuestDefinition>());
            var interpretation = MakeInterpretation("interpret.repeat", null, null, new IClueInterpretationOutcome[]
            {
                new ClueInterpretationRelationshipDeltaOutcome("rel.a", 2),
                new ClueInterpretationStatusDeltaOutcome("status.a", 0),
                new ClueInterpretationQuestUnlockOutcome(new[] { quest }),
            }, MakePolicy("p", ClueInterpretationPolicyKind.Repeatable, "group.r", 0, 2));
            var progress = new ClueInterpretationProgress("slot-a", "player-a");
            var runner = new ClueInterpretationRunner();

            Assert.IsTrue(runner.TryComplete(interpretation, Context(progress, day: 3, student: student, questLog: questLog), out var first));
            CollectionAssert.AreEqual(new[] { "relationship", "quest" }, first.OutcomeIds, "A zero delta is not an outcome.");
            questLog.RecordEvent(new QuestEvent(QuestEventKind.Talk, "talk-1", "npc.a"));
            Assert.IsFalse(runner.TryComplete(interpretation, Context(progress, day: 4, student: student, questLog: questLog), out var cooldown));
            Assert.AreEqual(ClueInterpretationResultKind.CooldownActive, cooldown.Kind);
            Assert.IsTrue(runner.TryComplete(interpretation, Context(progress, day: 5, student: student, questLog: questLog), out var second));
            CollectionAssert.AreEqual(new[] { "relationship" }, second.OutcomeIds);

            Assert.AreEqual(4, student.GetRelationshipValue("rel.a"));
            Assert.AreEqual(1, questLog.GetObjectiveCount(quest, 0), "The quest is not restarted (Unity reset its progress).");
            var record = progress.GetRecord(interpretation.Id);
            Assert.AreEqual(2, record.CompletedCount);
            Assert.AreEqual(3, record.CompletedDay, "First completion day is kept.");
            Assert.AreEqual(5, record.LastInterpretedDay);
            Assert.AreEqual(3, record.FirstSelectedDay);
        }

        [Test]
        public void CLUE_INTERPRET_CORE_SummaryReportsLockReasonsPolicyAndSortsBySourceStrength()
        {
            var strong = new ClueInterpretationSource("source.strong", ClueInterpretationSourceKind.BoardRecord, null, null, null, 5);
            var weak = new ClueInterpretationSource("source.weak", ClueInterpretationSourceKind.BoardRecord, "board.town", null, "hint.weak", 0);
            var exclusive = MakePolicy("p", ClueInterpretationPolicyKind.Exclusive, "group.x");
            var done = MakeInterpretation("interpret.done", new[] { weak }, null, null, exclusive, sortPriority: 1);
            var rival = MakeInterpretation("interpret.rival", new[] { strong, null }, null, null, exclusive, sortPriority: 1);
            var gated = MakeInterpretation("interpret.gated", new[] { weak }, new IClueInterpretationCondition[] { new ClueInterpretationWorldStateActiveCondition(MakeFlag("world.off"), null) }, null, null);
            var cache = new ClueInterpretationLookupCache(new[] { done, rival, gated, null });
            var progress = new ClueInterpretationProgress("slot-a", "player-a");
            new ClueInterpretationRunner().TryComplete(done, Context(progress, cache), out _);

            var rows = ClueInterpretationSummaryBuilder.BuildForSurface(cache, Context(progress, cache), ClueInterpretationSummarySurface.Journal);

            Assert.AreEqual(3, rows.Length);
            Assert.AreEqual("interpret.rival", rows[0].InterpretationId);
            Assert.AreEqual(6, rows[0].SortPriority);
            Assert.AreEqual("source.strong", rows[0].SourceTargetId, "Target id falls back to the source id.");
            Assert.IsFalse(rows[0].Available);
            Assert.AreEqual("exclusive group already completed", rows[0].LockedReason);
            Assert.AreEqual(ClueInterpretationPolicyKind.Exclusive, rows[0].PolicyKind);
            Assert.AreEqual("group.x", rows[0].PolicyGroupId);
            var doneRow = Array.Find(rows, r => r.InterpretationId == "interpret.done");
            Assert.IsTrue(doneRow.Completed);
            Assert.IsTrue(doneRow.Selected);
            Assert.AreEqual("already completed", doneRow.LockedReason);
            Assert.AreEqual("hint.weak", doneRow.HintText);
            var gatedRow = Array.Find(rows, r => r.InterpretationId == "interpret.gated");
            Assert.AreEqual("condition.missing", gatedRow.LockedReason);
            Assert.AreEqual(ClueInterpretationPolicyKind.NonExclusive, gatedRow.PolicyKind, "No policy reads as non-exclusive.");
            Assert.AreEqual(string.Empty, gatedRow.PolicyGroupId);
            Assert.AreEqual(2, ClueInterpretationSummaryBuilder.BuildForSource(cache, ClueInterpretationSourceKind.BoardRecord, "board.town", Context(progress, cache), ClueInterpretationSummarySurface.Journal).Length);
            Assert.AreEqual(0, ClueInterpretationSummaryBuilder.BuildForClue(cache, null, Context(progress, cache), ClueInterpretationSummarySurface.Journal).Length);
            Assert.AreEqual("p.solo", MakePolicy("p.solo", ClueInterpretationPolicyKind.Exclusive, null).GroupId, "An empty group falls back to the policy id.");
        }

        [Test]
        public void CLUE_INTERPRET_CORE_FromSaveDataClampsAndSkipsBadRecords()
        {
            var data = new ClueInterpretationProgressSaveData
            {
                SaveSlot = "slot-a",
                PlayerId = "player-a",
                Records = new[] { new ClueInterpretationRecordSaveData { InterpretationId = "interpret.a", CompletedCount = -4, LastInterpretedDay = -1, OutcomeIds = new[] { "x", null, "x" } }, null, new ClueInterpretationRecordSaveData() },
                TodayInterpretationIds = new[] { "interpret.a", "interpret.missing", "interpret.a" },
            };

            var progress = ClueInterpretationProgress.FromSaveData(data);

            Assert.AreEqual(0, progress.GetRecord("interpret.a").CompletedCount);
            Assert.AreEqual(0, progress.GetRecord("interpret.a").LastInterpretedDay);
            CollectionAssert.AreEqual(new[] { "x" }, progress.GetRecord("interpret.a").OutcomeIds);
            Assert.AreEqual(1, progress.TodayInterpretationSummary.Length);
            Assert.AreEqual(1, progress.ToSaveData().Records.Length);
            Assert.AreEqual("player", ClueInterpretationProgress.FromSaveData(null).PlayerId);
        }
    }
}
