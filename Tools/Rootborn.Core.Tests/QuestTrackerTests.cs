using System;
using NUnit.Framework;
using Rootborn.Core.Quests;

namespace Rootborn.Core.Tests
{
    public sealed class QuestTrackerTests
    {
        private static QuestTracker Active(int objectives)
        {
            var tracker = new QuestTracker(objectives);
            Assert.That(tracker.TryAccept(), Is.True);
            return tracker;
        }

        [Test]
        public void Accept_OnlyFromNotStarted()
        {
            var tracker = new QuestTracker(1);
            Assert.That(tracker.Stage, Is.EqualTo(QuestStage.NotStarted));
            Assert.That(tracker.TryAccept(), Is.True);
            Assert.That(tracker.Stage, Is.EqualTo(QuestStage.Active));
            Assert.That(tracker.TryAccept(), Is.False);
        }

        [Test]
        public void Progress_IsIgnoredUntilAccepted()
        {
            var tracker = new QuestTracker(1);
            Assert.That(tracker.TryAddObjectiveCount(0, 1, 1, "e1"), Is.False);
            Assert.That(tracker.GetObjectiveCount(0), Is.Zero);
        }

        [Test]
        public void Progress_ClampsToTheRequiredCountAndCompletesWhenEveryObjectiveIsDone()
        {
            var tracker = Active(2);
            Assert.That(tracker.TryAddObjectiveCount(0, 5, 3, "a"), Is.True);
            Assert.That(tracker.GetObjectiveCount(0), Is.EqualTo(3));
            Assert.That(tracker.Stage, Is.EqualTo(QuestStage.Active), "the second objective is not done yet");

            Assert.That(tracker.TryAddObjectiveCount(1, 1, 1, "b"), Is.True);
            Assert.That(tracker.Stage, Is.EqualTo(QuestStage.Completed));
        }

        [Test]
        public void Progress_TheSameEventKeyCountsOnce()
        {
            var tracker = Active(1);
            Assert.That(tracker.TryAddObjectiveCount(0, 1, 3, "tree-1"), Is.True);
            Assert.That(tracker.TryAddObjectiveCount(0, 1, 3, "tree-1"), Is.False);
            Assert.That(tracker.GetObjectiveCount(0), Is.EqualTo(1));
            Assert.That(tracker.TryAddObjectiveCount(0, 1, 3, null), Is.True, "events without a key are not deduplicated");
        }

        [Test]
        public void Progress_RejectsNonPositiveDeltaOrRequirementAndBadIndexes()
        {
            var tracker = Active(1);
            Assert.That(tracker.TryAddObjectiveCount(0, 0, 1, "a"), Is.False);
            Assert.That(tracker.TryAddObjectiveCount(0, 1, 0, "b"), Is.False);
            Assert.That(tracker.GetObjectiveCount(0), Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => tracker.TryAddObjectiveCount(1, 1, 1, "c"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new QuestTracker(-1));
        }

        [Test]
        public void AQuestWithoutObjectivesNeverCompletesByItself()
        {
            var tracker = Active(0);
            Assert.That(tracker.Stage, Is.EqualTo(QuestStage.Active));
            Assert.That(tracker.TryMarkRewardClaimed(), Is.False);
        }

        [Test]
        public void RewardClaim_OnlyOnceAndOnlyAfterCompletion()
        {
            var tracker = Active(1);
            Assert.That(tracker.TryMarkRewardClaimed(), Is.False);
            tracker.TryAddObjectiveCount(0, 1, 1, "a");
            Assert.That(tracker.TryMarkRewardClaimed(), Is.True);
            Assert.That(tracker.Stage, Is.EqualTo(QuestStage.RewardClaimed));
            Assert.That(tracker.TryMarkRewardClaimed(), Is.False);
            Assert.That(tracker.TryAddObjectiveCount(0, 1, 1, "b"), Is.False);
        }

        [Test]
        public void Snapshot_RestoresProgressAndProcessedKeys()
        {
            var tracker = Active(2);
            tracker.TryAddObjectiveCount(0, 2, 3, "a");
            var snapshot = tracker.ToSnapshot();

            var copy = new QuestTracker(2);
            copy.Restore(snapshot.Stage, null, snapshot.ObjectiveCounts, snapshot.ProcessedEventKeys);
            Assert.That(copy.Stage, Is.EqualTo(QuestStage.Active));
            Assert.That(copy.GetObjectiveCount(0), Is.EqualTo(2));
            Assert.That(copy.TryAddObjectiveCount(0, 1, 3, "a"), Is.False, "a key seen before saving still counts once");
            Assert.That(copy.TryAddObjectiveCount(0, 1, 3, "c"), Is.True);
        }

        [Test]
        public void Snapshot_IsACopyThatLaterProgressDoesNotChange()
        {
            var tracker = Active(1);
            var snapshot = tracker.ToSnapshot();
            tracker.TryAddObjectiveCount(0, 1, 2, "a");
            Assert.That(snapshot.ObjectiveCounts[0], Is.Zero);
            Assert.That(snapshot.ProcessedEventKeys, Is.Empty);
        }

        [Test]
        public void Restore_ANamedStageWinsOverTheNumberSoReorderedStagesStillLoad()
        {
            var tracker = new QuestTracker(1);
            tracker.Restore(QuestStage.Active, "RewardClaimed", new[] { 1 }, null);
            Assert.That(tracker.Stage, Is.EqualTo(QuestStage.RewardClaimed));
            Assert.That(tracker.TryMarkRewardClaimed(), Is.False, "a claimed reward is not claimable again after loading");

            tracker.Restore(QuestStage.Completed, "not-a-stage", null, null);
            Assert.That(tracker.Stage, Is.EqualTo(QuestStage.Completed));
        }

        [Test]
        public void Restore_FinishedQuestsStayFinishedAndBadCountsAreCleaned()
        {
            var tracker = new QuestTracker(2);
            tracker.Restore(QuestStage.Completed, null, new[] { -4 }, new[] { "", null, "k" });
            Assert.That(tracker.GetObjectiveCount(0), Is.Zero, "negative counts load as zero");
            Assert.That(tracker.GetObjectiveCount(1), Is.Zero, "missing counts load as zero");
            Assert.That(tracker.ToSnapshot().ProcessedEventKeys, Is.EqualTo(new[] { "k" }));
            Assert.That(tracker.TryMarkRewardClaimed(), Is.True);
        }

        [Test]
        public void Restore_ReplacesEarlierProgress()
        {
            var tracker = Active(1);
            tracker.TryAddObjectiveCount(0, 1, 3, "old");
            tracker.Restore(QuestStage.Active, null, new[] { 0 }, null);
            Assert.That(tracker.GetObjectiveCount(0), Is.Zero);
            Assert.That(tracker.TryAddObjectiveCount(0, 1, 3, "old"), Is.True, "keys from before the load are forgotten");
        }
    }
}
