using System;
using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.StudentLife;
using UnityEditor;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class StatusActivityChainTests
    {
        private const string RegistryPath = "Assets/Data/Registry/GameDataRegistry.asset";
        private const string WorkshopActivityId = "location.activity.workshop-shift";
        private const string StatusId = "status.fatigue";

        [Test]
        public void STATUS_ACTIVITY_EDIT_001_WorkshopStatusActivityIsRegisteredAndDataDriven()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(RegistryPath);
            Assert.IsNotNull(registry, RegistryPath + " must exist.");
            Assert.IsTrue(registry.StudentConditionStatuses.Any(status => status != null && status.Id == StatusId), "Focused status activity chain must use a registered StatusDefinition.");

            var cache = new GameDataLookupCache(registry);
            Assert.IsTrue(cache.TryGetLocationActivity(WorkshopActivityId, out var activity), "Workshop status activity must be available through registry lookup cache.");
            Assert.IsNotNull(activity);
            Assert.AreEqual(LocationGrowthRoute.Work, activity.GrowthRoute);
            Assert.IsTrue(activity.Outcomes.Any(outcome => outcome is LocationActivityProgressDeltaOutcome progressOutcome && progressOutcome.Target == LocationActivityProgressTarget.Status && progressOutcome.Delta > 0), "Workshop activity must express status progression through a LocationActivityOutcomeBase strategy asset.");
        }

        [Test]
        public void STATUS_ACTIVITY_EDIT_002_WorkshopStatusActivityAppliesPersistsAndDedupesByRequestId()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(RegistryPath);
            var cache = new GameDataLookupCache(registry);
            Assert.IsTrue(cache.TryGetLocationActivity(WorkshopActivityId, out var activity));
            var status = registry.StudentConditionStatuses.First(definition => definition != null && definition.Id == StatusId);
            var progress = new StudentLifeProgress("slot-status-chain", "player-status-chain", 100, 100);
            progress.ConfigureCalendar(registry.StudentLifeCalendar); // activities spend a daily time point; with no calendar the budget is 0
            var runner = new LocationActivityRunner();

            Assert.IsTrue(runner.TryPerform(activity, progress, "request-status-chain", out var result), "Status activity should apply through LocationActivityRunner.");
            Assert.AreEqual(LifeActivityResultKind.Applied, result.Kind);
            Assert.AreEqual(1, progress.GetStatusValue(status), "Status activity must apply the configured status delta.");
            CollectionAssert.Contains(progress.GetTodayResultLogIds(), LocationActivityLogCodec.EncodeDelta(WorkshopActivityId, StatusId, 1), "Status activity must produce a stable day-result log.");

            Assert.IsFalse(runner.TryPerform(activity, progress, "request-status-chain", out var duplicate), "Duplicate request id must not apply the status delta twice.");
            Assert.AreEqual(LifeActivityResultKind.DuplicateRequest, duplicate.Kind);
            Assert.AreEqual(1, progress.GetStatusValue(status), "Duplicate request changed the status value.");

            var restored = StudentLifeProgress.FromSaveData(progress.ToSaveData(), registry.StudentLifeTraits, registry.StudentLifeSkills, registry.Careers, registry.Relationships, registry.StudentConditionStatuses);
            Assert.AreEqual(1, restored.GetStatusValue(status), "Status activity save data must restore the status value exactly once.");
            Assert.AreEqual(1, restored.GetTodayResultLogIds().Count(id => string.Equals(id, LocationActivityLogCodec.EncodeDelta(WorkshopActivityId, StatusId, 1), StringComparison.Ordinal)), "Status day-result log must restore exactly once.");
        }
    }
}
