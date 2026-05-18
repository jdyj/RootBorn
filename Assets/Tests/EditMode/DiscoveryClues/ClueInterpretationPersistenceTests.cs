using System;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.DiscoveryClues;
using Rootborn.Game.Save;
using UnityEngine;

namespace Rootborn.Tests.EditMode.DiscoveryClues
{
    public sealed class ClueInterpretationPersistenceTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Application.temporaryCachePath, "rootborn-clue-interpretation-persistence", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveService.SetRootDirectoryForTests(_root);
        }

        [TearDown]
        public void TearDown()
        {
            SaveService.SetRootDirectoryForTests(null);
            if (!string.IsNullOrEmpty(_root) && Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void CLUE_INTERPRET_EDIT_009_ProgressPersistenceRestoresSelectedCompletedRewardAndExpansionState()
        {
            var clue = ScriptableObject.CreateInstance<DiscoveryClueDefinition>();
            clue.ConfigureForTests("clue.discovery.play-loop", "Play clue", "desc", null, null, null, 1, "public", "hidden", Array.Empty<DiscoveryClueSourceDefinition>(), Array.Empty<DiscoveryClueConditionBase>(), Array.Empty<DiscoveryClueCompletionBase>(), Array.Empty<DiscoveryClueOutcomeBase>(), Array.Empty<DiscoveryClueSummarySurface>(), 0);
            var policy = ScriptableObject.CreateInstance<ClueInterpretationPolicyDefinition>();
            policy.ConfigureForTests("policy.interpret.nonexclusive", ClueInterpretationPolicyKind.NonExclusive, "policy.group.play-loop", 0, 0, false);
            var interpretation = ScriptableObject.CreateInstance<ClueInterpretationDefinition>();
            interpretation.ConfigureForTests("interpret.ask-librarian", "Ask Librarian", "desc", clue, "Ask", "public", "hidden", Array.Empty<ClueInterpretationSourceDefinition>(), Array.Empty<ClueInterpretationConditionBase>(), Array.Empty<ClueInterpretationOutcomeBase>(), policy, 0);
            var progress = new ClueInterpretationProgress("slot-a", "player-a");
            progress.MarkCompleted(
                interpretation,
                2,
                new[] { "encyclopedia", "career-hint" },
                new[] { "entry.archive" },
                new[] { "career.researcher" },
                new[] { "quest.followup" },
                new[] { "world.archive" },
                new[] { "rel.librarian" },
                new[] { "status.curiosity" });

            ClueInterpretationProgressPersistence.Save(progress);
            var restored = ClueInterpretationProgressPersistence.LoadOrCreate("slot-a", "player-a");
            var record = restored.GetRecord("interpret.ask-librarian");

            Assert.AreEqual("slot-a", restored.SaveSlot);
            Assert.AreEqual("player-a", restored.PlayerId);
            Assert.IsTrue(record.Selected);
            Assert.IsTrue(record.Completed);
            Assert.IsTrue(record.RewardClaimed);
            Assert.AreEqual(1, record.CompletedCount);
            CollectionAssert.Contains(record.EncyclopediaExpansionIds, "entry.archive");
            CollectionAssert.Contains(record.CareerHintGrantHistory, "career.researcher");
            CollectionAssert.Contains(record.FollowUpQuestIds, "quest.followup");
            CollectionAssert.Contains(record.RelatedWorldStateChanges, "world.archive");
            CollectionAssert.Contains(record.RelationshipChangeIds, "rel.librarian");
            CollectionAssert.Contains(record.StatusChangeIds, "status.curiosity");
            Assert.AreEqual(1, restored.TodayInterpretationSummary.Length);

            UnityEngine.Object.DestroyImmediate(clue);
            UnityEngine.Object.DestroyImmediate(policy);
            UnityEngine.Object.DestroyImmediate(interpretation);
        }
    }
}
