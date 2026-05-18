using System;
using NUnit.Framework;
using Rootborn.Game.DiscoveryClues;
using Rootborn.Game.Encyclopedia;
using Rootborn.Game.Quests;
using Rootborn.Game.StudentLife;
using Rootborn.Game.WorldState;
using UnityEngine;

namespace Rootborn.Tests.EditMode.DiscoveryClues
{
    public sealed class ClueInterpretationLoopTests
    {
        [Test]
        public void CLUE_INTERPRET_EDIT_001_005_DefinitionSourcesConditionsOutcomesAndPolicyAreDataDriven()
        {
            var clue = MakeDiscoveryClue("clue.archive.shard");
            var librarian = MakeSource("interpret.source.librarian", ClueInterpretationSourceKind.NpcDialogue, "npc.librarian");
            var workshop = MakeSource("interpret.source.workshop", ClueInterpretationSourceKind.ObjectInteraction, "object.workbench");
            var shop = MakeSource("interpret.source.shop", ClueInterpretationSourceKind.NpcDialogue, "npc.shopkeeper");
            var relationship = MakeRelationship("rel.librarian");
            var status = MakeStatus("status.curiosity");
            var career = MakeCareer("career.researcher");
            var entry = MakeEntry("entry.archive-shard");
            var quest = MakeQuest("quest.restore-shard");
            var flag = MakeFlag("world.archive-shard-restored");
            var conditionA = ScriptableObject.CreateInstance<ClueInterpretationClueSeenCondition>();
            var conditionB = ScriptableObject.CreateInstance<ClueInterpretationRelationshipThresholdCondition>();
            conditionB.ConfigureForTests(relationship, 1);
            var encyclopediaOutcome = ScriptableObject.CreateInstance<ClueInterpretationEncyclopediaOutcome>();
            encyclopediaOutcome.ConfigureForTests(new[] { entry });
            var careerOutcome = ScriptableObject.CreateInstance<ClueInterpretationCareerHintOutcome>();
            careerOutcome.ConfigureForTests(new[] { career });
            var questOutcome = ScriptableObject.CreateInstance<ClueInterpretationQuestUnlockOutcome>();
            questOutcome.ConfigureForTests(new[] { quest });
            var worldOutcome = ScriptableObject.CreateInstance<ClueInterpretationWorldStateOutcome>();
            worldOutcome.ConfigureForTests(new[] { flag });
            var relationshipOutcome = ScriptableObject.CreateInstance<ClueInterpretationRelationshipDeltaOutcome>();
            relationshipOutcome.ConfigureForTests(relationship, 2);
            var statusOutcome = ScriptableObject.CreateInstance<ClueInterpretationStatusDeltaOutcome>();
            statusOutcome.ConfigureForTests(status, 3);
            var exclusive = MakePolicy("policy.archive.exclusive", ClueInterpretationPolicyKind.Exclusive, "group.archive", 0, 0);
            var sequential = MakePolicy("policy.archive.sequential", ClueInterpretationPolicyKind.Sequential, "group.archive.sequence", 0, 0);
            var npcInterpretation = MakeInterpretation("interpret.ask-librarian", clue, new[] { librarian }, new ClueInterpretationConditionBase[] { conditionA, conditionB }, new ClueInterpretationOutcomeBase[] { encyclopediaOutcome, careerOutcome }, exclusive);
            var objectInterpretation = MakeInterpretation("interpret.restore-workbench", clue, new[] { workshop }, new ClueInterpretationConditionBase[] { conditionA }, new ClueInterpretationOutcomeBase[] { questOutcome, worldOutcome, relationshipOutcome }, exclusive);
            var shopInterpretation = MakeInterpretation("interpret.ask-shopkeeper", clue, new[] { shop }, new ClueInterpretationConditionBase[] { conditionA }, new ClueInterpretationOutcomeBase[] { statusOutcome }, sequential);
            var cache = new ClueInterpretationLookupCache(new[] { npcInterpretation, objectInterpretation, shopInterpretation });
            var clueProgress = new DiscoveryClueProgress("slot-a", "player-a");
            clueProgress.MarkSourceSeen(clue, clue.Sources[0], 1);
            var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            student.AddRelationshipForTests(relationship, 1);
            var context = new ClueInterpretationContext(new WorldStateProgress("slot-a", "player-a"), clueProgress, new ClueInterpretationProgress("slot-a", "player-a"), student, new QuestLog(Array.Empty<QuestDefinition>()), new EncyclopediaProgress("slot-a", "player-a"), 1);

            var summaries = ClueInterpretationSummaryBuilder.BuildForClue(cache, clue.Id, context, ClueInterpretationSummarySurface.ClueDetail);
            var sourceSummaries = ClueInterpretationSummaryBuilder.BuildForSource(cache, ClueInterpretationSourceKind.NpcDialogue, "npc.librarian", context, ClueInterpretationSummarySurface.NpcDialogue);

            Assert.AreEqual(3, summaries.Length, "One clue must expose three interpretation paths from SO definitions only.");
            Assert.AreEqual(1, sourceSummaries.Length);
            Assert.AreEqual("interpret.ask-librarian", sourceSummaries[0].InterpretationId);
            Assert.IsTrue(sourceSummaries[0].Available);
            Assert.AreEqual(1, cache.BuildCount, "Summary lookup must build an index once instead of scanning every refresh.");
            Assert.GreaterOrEqual(cache.LookupCount, 2);

            DestroyAll(clue, librarian, workshop, shop, relationship, status, career, entry, quest, flag, conditionA, conditionB, encyclopediaOutcome, careerOutcome, questOutcome, worldOutcome, relationshipOutcome, statusOutcome, exclusive, sequential, npcInterpretation, objectInterpretation, shopInterpretation);
        }

        [Test]
        public void CLUE_INTERPRET_EDIT_006_010_PoliciesSaveLoadAndDuplicateOutcomesAreIdempotent()
        {
            var clue = MakeDiscoveryClue("clue.archive.shard");
            var sourceA = MakeSource("interpret.source.a", ClueInterpretationSourceKind.NpcDialogue, "npc.a");
            var sourceB = MakeSource("interpret.source.b", ClueInterpretationSourceKind.LocationInvestigation, "location.archive");
            var career = MakeCareer("career.researcher");
            var entry = MakeEntry("entry.archive-shard");
            var flag = MakeFlag("world.archive-open");
            var quest = MakeQuest("quest.archive-followup");
            var nonExclusive = MakePolicy("policy.archive.nonexclusive", ClueInterpretationPolicyKind.NonExclusive, "group.archive.multi", 0, 0);
            var exclusive = MakePolicy("policy.archive.exclusive", ClueInterpretationPolicyKind.Exclusive, "group.archive.single", 0, 0);
            var careerOutcome = ScriptableObject.CreateInstance<ClueInterpretationCareerHintOutcome>();
            careerOutcome.ConfigureForTests(new[] { career });
            var entryOutcome = ScriptableObject.CreateInstance<ClueInterpretationEncyclopediaOutcome>();
            entryOutcome.ConfigureForTests(new[] { entry });
            var worldOutcome = ScriptableObject.CreateInstance<ClueInterpretationWorldStateOutcome>();
            worldOutcome.ConfigureForTests(new[] { flag });
            var questOutcome = ScriptableObject.CreateInstance<ClueInterpretationQuestUnlockOutcome>();
            questOutcome.ConfigureForTests(new[] { quest });
            var first = MakeInterpretation("interpret.first", clue, new[] { sourceA }, Array.Empty<ClueInterpretationConditionBase>(), new ClueInterpretationOutcomeBase[] { careerOutcome, entryOutcome }, nonExclusive);
            var second = MakeInterpretation("interpret.second", clue, new[] { sourceB }, Array.Empty<ClueInterpretationConditionBase>(), new ClueInterpretationOutcomeBase[] { worldOutcome, questOutcome }, nonExclusive);
            var locked = MakeInterpretation("interpret.locked", clue, new[] { sourceB }, Array.Empty<ClueInterpretationConditionBase>(), new ClueInterpretationOutcomeBase[] { worldOutcome }, exclusive);
            var clueProgress = new DiscoveryClueProgress("slot-a", "player-a");
            clueProgress.MarkSourceSeen(clue, clue.Sources[0], 1);
            var interpretationProgress = new ClueInterpretationProgress("slot-a", "player-a");
            var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            var questLog = new QuestLog(Array.Empty<QuestDefinition>());
            var encyclopedia = new EncyclopediaProgress("slot-a", "player-a");
            var world = new WorldStateProgress("slot-a", "player-a");
            var context = new ClueInterpretationContext(world, clueProgress, interpretationProgress, student, questLog, encyclopedia, 1);
            var runner = new ClueInterpretationRunner();

            Assert.IsTrue(runner.TryComplete(first, context, out var firstResult));
            Assert.IsTrue(runner.TryComplete(second, context, out var secondResult));
            Assert.AreEqual(ClueInterpretationResultKind.Completed, firstResult.Kind);
            Assert.AreEqual(ClueInterpretationResultKind.Completed, secondResult.Kind);
            Assert.IsTrue(student.IsCareerHintUnlocked(career));
            Assert.IsTrue(encyclopedia.IsUnlocked(entry.Id));
            Assert.IsTrue(world.IsActive(flag));
            Assert.AreEqual(QuestState.Active, questLog.GetState(quest));
            var restored = ClueInterpretationProgress.FromSaveData(interpretationProgress.ToSaveData());
            Assert.IsTrue(restored.GetRecord(first.Id).Completed);
            Assert.IsTrue(restored.GetRecord(second.Id).Completed);
            Assert.IsTrue(restored.GetRecord(first.Id).RewardClaimed);
            Assert.AreEqual(2, restored.TodayInterpretationSummary.Length);

            var duplicateContext = new ClueInterpretationContext(world, clueProgress, restored, student, questLog, encyclopedia, 2);
            Assert.IsFalse(runner.TryComplete(first, duplicateContext, out var duplicate));
            Assert.AreEqual(ClueInterpretationResultKind.AlreadyCompleted, duplicate.Kind);
            Assert.AreEqual(1, restored.GetRecord(first.Id).CompletedCount);

            Assert.IsTrue(runner.TryComplete(locked, duplicateContext, out var lockedResult));
            Assert.AreEqual(ClueInterpretationResultKind.Completed, lockedResult.Kind);
            var blocked = MakeInterpretation("interpret.blocked-by-exclusive", clue, new[] { sourceB }, Array.Empty<ClueInterpretationConditionBase>(), new ClueInterpretationOutcomeBase[] { worldOutcome }, exclusive);
            Assert.IsFalse(runner.TryComplete(blocked, duplicateContext, out var blockedResult));
            Assert.AreEqual(ClueInterpretationResultKind.LockedByPolicy, blockedResult.Kind);

            DestroyAll(clue, sourceA, sourceB, career, entry, flag, quest, nonExclusive, exclusive, careerOutcome, entryOutcome, worldOutcome, questOutcome, first, second, locked, blocked);
        }

        private static ClueInterpretationDefinition MakeInterpretation(string id, DiscoveryClueDefinition clue, ClueInterpretationSourceDefinition[] sources, ClueInterpretationConditionBase[] conditions, ClueInterpretationOutcomeBase[] outcomes, ClueInterpretationPolicyDefinition policy)
        {
            var interpretation = ScriptableObject.CreateInstance<ClueInterpretationDefinition>();
            interpretation.ConfigureForTests(id, id + ".name", id + ".desc", clue, "method." + id, "public." + id, "hidden." + id, sources, conditions, outcomes, policy, 0);
            return interpretation;
        }

        private static ClueInterpretationSourceDefinition MakeSource(string id, ClueInterpretationSourceKind kind, string targetId)
        {
            var source = ScriptableObject.CreateInstance<ClueInterpretationSourceDefinition>();
            source.ConfigureForTests(id, kind, targetId, id + ".name", id + ".hint", 1);
            return source;
        }

        private static ClueInterpretationPolicyDefinition MakePolicy(string id, ClueInterpretationPolicyKind kind, string groupId, int sequenceOrder, int cooldownDays)
        {
            var policy = ScriptableObject.CreateInstance<ClueInterpretationPolicyDefinition>();
            policy.ConfigureForTests(id, kind, groupId, sequenceOrder, cooldownDays, false);
            return policy;
        }

        private static DiscoveryClueDefinition MakeDiscoveryClue(string id)
        {
            var source = ScriptableObject.CreateInstance<DiscoveryClueSourceDefinition>();
            source.ConfigureForTests(id + ".source", DiscoveryClueSourceKind.BoardPost, "Board", "hint", null, null, 1);
            var clue = ScriptableObject.CreateInstance<DiscoveryClueDefinition>();
            clue.ConfigureForTests(id, id + ".name", id + ".desc", null, null, null, 1, "public", "hidden", new[] { source }, Array.Empty<DiscoveryClueConditionBase>(), Array.Empty<DiscoveryClueCompletionBase>(), Array.Empty<DiscoveryClueOutcomeBase>(), Array.Empty<DiscoveryClueSummarySurface>(), 0);
            return clue;
        }

        private static RelationshipDefinition MakeRelationship(string id)
        {
            var relationship = ScriptableObject.CreateInstance<RelationshipDefinition>();
            relationship.ConfigureForTests(id, id + ".name", id + ".target");
            return relationship;
        }

        private static StatusDefinition MakeStatus(string id)
        {
            var status = ScriptableObject.CreateInstance<StatusDefinition>();
            status.ConfigureForTests(id, id + ".name", true);
            return status;
        }

        private static CareerDefinition MakeCareer(string id)
        {
            var career = ScriptableObject.CreateInstance<CareerDefinition>();
            career.ConfigureForTests(id, id + ".name");
            return career;
        }

        private static EncyclopediaEntryDefinition MakeEntry(string id)
        {
            var entry = ScriptableObject.CreateInstance<EncyclopediaEntryDefinition>();
            entry.ConfigureForTests(id, null, id + ".name", "???", id + ".desc", id + ".hint", null, null);
            return entry;
        }

        private static QuestDefinition MakeQuest(string id)
        {
            var quest = ScriptableObject.CreateInstance<QuestDefinition>();
            quest.ConfigureForRuntime(id, id + ".name", id + ".desc", Array.Empty<QuestObjectiveBase>(), Array.Empty<QuestRewardBase>(), Array.Empty<QuestCompletionEffectBase>());
            return quest;
        }

        private static WorldStateFlagDefinition MakeFlag(string id)
        {
            var flag = ScriptableObject.CreateInstance<WorldStateFlagDefinition>();
            flag.ConfigureForTests(id, id + ".name", id + ".desc", null, null, null, WorldStateScopeKind.Shared, WorldStateChangeKind.ObjectRevealed, "use", new[] { WorldStateSummarySurface.WorldLog }, new[] { WorldStateBadgeKind.Shared }, 0, 1);
            return flag;
        }

        private static void DestroyAll(params UnityEngine.Object[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] != null) UnityEngine.Object.DestroyImmediate(values[i]);
            }
        }
    }
}
