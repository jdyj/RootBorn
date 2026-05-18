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
    public sealed class DiscoveryClueLoopTests
    {
        [Test]
        public void DISCOVERY_CLUE_EDIT_001_005_DefinitionSourcesConditionsCompletionsOutcomesAndSummaryAreDataDriven()
        {
            var flag = MakeFlag("world.library.archive-open");
            var npcSource = ScriptableObject.CreateInstance<DiscoveryClueSourceDefinition>();
            npcSource.ConfigureForTests("source.librarian", DiscoveryClueSourceKind.NpcDialogue, "Librarian", "Something old moved under the archive.", null, flag, 2);
            var boardSource = ScriptableObject.CreateInstance<DiscoveryClueSourceDefinition>();
            boardSource.ConfigureForTests("source.board", DiscoveryClueSourceKind.BoardPost, "Town Board", "A torn map points toward the archive room.", null, flag, 1);
            var condition = ScriptableObject.CreateInstance<DiscoveryClueWorldStateActiveCondition>();
            var completion = ScriptableObject.CreateInstance<DiscoveryClueLocationVisitCompletion>();
            var clue = MakeClue("clue.library.archive-rumor", flag, new[] { npcSource, boardSource }, new DiscoveryClueConditionBase[] { condition }, new DiscoveryClueCompletionBase[] { completion }, Array.Empty<DiscoveryClueOutcomeBase>());
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

            DestroyAll(flag, npcSource, boardSource, condition, completion, clue);
        }

        [Test]
        public void DISCOVERY_CLUE_EDIT_006_008_OutcomesCompleteAndRewardOnlyOnceAcrossSaveLoad()
        {
            var flag = MakeFlag("world.library.archive-open");
            var quest = MakeQuest("quest.library.follow-up");
            var career = ScriptableObject.CreateInstance<CareerDefinition>();
            career.ConfigureForTests("career.research", "Research");
            var entry = ScriptableObject.CreateInstance<EncyclopediaEntryDefinition>();
            entry.ConfigureForTests("encyclopedia.archive-fragment", null, "Archive Fragment", "???", "Archive desc", "Find it", null, null);
            var source = ScriptableObject.CreateInstance<DiscoveryClueSourceDefinition>();
            source.ConfigureForTests("source.board", DiscoveryClueSourceKind.BoardPost, "Board", "Archive clue", null, flag, 1);
            var completion = ScriptableObject.CreateInstance<DiscoveryClueLocationVisitCompletion>();
            completion.ConfigureForTests("location.library.archive-room");
            var questOutcome = ScriptableObject.CreateInstance<DiscoveryClueQuestUnlockOutcome>();
            questOutcome.ConfigureForTests(new[] { quest });
            var careerOutcome = ScriptableObject.CreateInstance<DiscoveryClueCareerHintOutcome>();
            careerOutcome.ConfigureForTests(new[] { career });
            var encyclopediaOutcome = ScriptableObject.CreateInstance<DiscoveryClueEncyclopediaOutcome>();
            encyclopediaOutcome.ConfigureForTests(new[] { entry });
            var worldOutcome = ScriptableObject.CreateInstance<DiscoveryClueWorldStateRevealOutcome>();
            worldOutcome.ConfigureForTests(new[] { flag });
            var clue = MakeClue("clue.library.archive-rumor", flag, new[] { source }, Array.Empty<DiscoveryClueConditionBase>(), new DiscoveryClueCompletionBase[] { completion }, new DiscoveryClueOutcomeBase[] { questOutcome, careerOutcome, encyclopediaOutcome, worldOutcome });
            var world = new WorldStateProgress("slot-a", "player-a");
            var clueProgress = new DiscoveryClueProgress("slot-a", "player-a");
            var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            var questLog = new QuestLog(Array.Empty<QuestDefinition>());
            var encyclopedia = new EncyclopediaProgress("slot-a", "player-a");
            var context = new DiscoveryClueContext(world, clueProgress, student, questLog, encyclopedia, 1);
            var runner = new DiscoveryClueRunner();

            Assert.IsTrue(runner.TryRevealSource(clue, source, context, out var reveal));
            Assert.AreEqual(DiscoveryClueResultKind.Revealed, reveal.Kind);
            Assert.IsTrue(runner.TryComplete(clue, new DiscoveryClueCompletionEvent(DiscoveryClueCompletionKind.LocationVisited, "location.library.archive-room"), context, out var completed));
            Assert.AreEqual(DiscoveryClueResultKind.Completed, completed.Kind);
            Assert.AreEqual(4, completed.OutcomeIds.Length);
            Assert.AreEqual(QuestState.Active, questLog.GetState(quest));
            Assert.IsTrue(student.IsCareerHintUnlocked(career));
            Assert.IsTrue(encyclopedia.IsUnlocked(entry.Id));
            Assert.IsTrue(world.IsActive(flag));

            var restored = DiscoveryClueProgress.FromSaveData(clueProgress.ToSaveData());
            Assert.IsTrue(restored.GetRecord(clue.Id).Seen);
            Assert.IsTrue(restored.GetRecord(clue.Id).Completed);
            Assert.IsTrue(restored.GetRecord(clue.Id).RewardClaimed);
            Assert.AreEqual(1, restored.TodayClueSummary.Length);
            var duplicateContext = new DiscoveryClueContext(world, restored, student, questLog, encyclopedia, 2);
            Assert.IsFalse(runner.TryComplete(clue, new DiscoveryClueCompletionEvent(DiscoveryClueCompletionKind.LocationVisited, "location.library.archive-room"), duplicateContext, out var duplicate));
            Assert.AreEqual(DiscoveryClueResultKind.AlreadyCompleted, duplicate.Kind);
            Assert.AreEqual(1, restored.GetRecord(clue.Id).CompletedCount);

            DestroyAll(flag, quest, career, entry, source, completion, questOutcome, careerOutcome, encyclopediaOutcome, worldOutcome, clue);
        }

        [Test]
        public void DISCOVERY_CLUE_EDIT_009_SummaryBuilderUsesLookupCacheInsteadOfRegistryScan()
        {
            var flag = MakeFlag("world.shop.stock-open");
            var source = ScriptableObject.CreateInstance<DiscoveryClueSourceDefinition>();
            source.ConfigureForTests("source.shop-board", DiscoveryClueSourceKind.BoardPost, "Shop Board", "Stock changed.", null, flag, 1);
            var clue = MakeClue("clue.shop.stock", flag, new[] { source }, Array.Empty<DiscoveryClueConditionBase>(), Array.Empty<DiscoveryClueCompletionBase>(), Array.Empty<DiscoveryClueOutcomeBase>());
            var cache = new DiscoveryClueLookupCache(new[] { clue });
            var world = new WorldStateProgress("slot-a", "player-a");
            world.TryActivate(flag, new WorldStateActivationSource("quest.shop", "step.reward", "event.reward", 1));
            var context = new DiscoveryClueContext(world, new DiscoveryClueProgress("slot-a", "player-a"), new StudentLifeProgress("slot-a", "player-a", 10, 10), null, null, 1);

            DiscoveryClueSummaryBuilder.BuildForSource(cache, DiscoveryClueSourceKind.BoardPost, "source.shop-board", context, DiscoveryClueSummarySurface.Board);
            DiscoveryClueSummaryBuilder.BuildForSource(cache, DiscoveryClueSourceKind.BoardPost, "source.shop-board", context, DiscoveryClueSummarySurface.Board);

            Assert.AreEqual(1, cache.BuildCount);
            Assert.GreaterOrEqual(cache.LookupCount, 2);

            DestroyAll(flag, source, clue);
        }

        private static DiscoveryClueDefinition MakeClue(string id, WorldStateFlagDefinition relatedFlag, DiscoveryClueSourceDefinition[] sources, DiscoveryClueConditionBase[] conditions, DiscoveryClueCompletionBase[] completions, DiscoveryClueOutcomeBase[] outcomes)
        {
            var clue = ScriptableObject.CreateInstance<DiscoveryClueDefinition>();
            clue.ConfigureForTests(id, id + ".name", id + ".desc", relatedFlag, null, null, 3, "Rumor", "Trace", sources, conditions, completions, outcomes, new[] { DiscoveryClueSummarySurface.NpcDialogue, DiscoveryClueSummarySurface.Board, DiscoveryClueSummarySurface.WorldLog, DiscoveryClueSummarySurface.Encyclopedia }, 0);
            return clue;
        }

        private static WorldStateFlagDefinition MakeFlag(string id)
        {
            var flag = ScriptableObject.CreateInstance<WorldStateFlagDefinition>();
            flag.ConfigureForTests(id, id, id + ".desc", null, null, null, WorldStateScopeKind.Shared, WorldStateChangeKind.ObjectRevealed, "Use it", new[] { WorldStateSummarySurface.WorldLog }, new[] { WorldStateBadgeKind.Shared }, 0, 1);
            return flag;
        }

        private static QuestDefinition MakeQuest(string id)
        {
            var quest = ScriptableObject.CreateInstance<QuestDefinition>();
            quest.ConfigureForRuntime(id, id, id + ".desc", Array.Empty<QuestObjectiveBase>(), Array.Empty<QuestRewardBase>(), Array.Empty<QuestCompletionEffectBase>());
            return quest;
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
