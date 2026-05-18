using System;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Quests;
using Rootborn.Game.Quests.Rewards;
using Rootborn.Game.StudentLife;
using Rootborn.Game.WorldState;
using UnityEngine;

namespace Rootborn.Tests.EditMode.WorldState
{
    public sealed class WorldStateCoreTests
    {
        [Test]
        public void WORLD_STATE_EDIT_001_003_005_QuestRewardActivatesFlagAndBuildsSharedSummariesIdempotently()
        {
            var location = MakeLocation("location.library", "Library");
            var flag = MakeFlag("world.library.archive-open", location, WorldStateScopeKind.Shared);
            var progress = new WorldStateProgress("slot-a", "player-a");
            var reward = ScriptableObject.CreateInstance<WorldStateChangeQuestReward>();
            reward.ConfigureForTests(new[] { flag }, "quest.library", "step.archive", "event.archive-open", 7);
            var context = new RewardRuntimeContext(null, null, null, null, null, progress);

            Assert.IsTrue(reward.CanApply(in context));
            reward.Apply(in context);
            reward.Apply(in context);

            Assert.IsTrue(progress.IsActive(flag));
            Assert.AreEqual(1, progress.ActiveFlagCount);
            Assert.AreEqual(1, progress.TodayWorldChangeSummary.Length);
            Assert.AreEqual("world.library.archive-open", progress.TodayWorldChangeSummary[0].FlagId);
            Assert.AreEqual("quest.library", progress.GetRecord(flag).SourceQuestChainId);
            Assert.AreEqual("step.archive", progress.GetRecord(flag).SourceQuestStepId);
            Assert.AreEqual("event.archive-open", progress.GetRecord(flag).SourceEventId);
            Assert.AreEqual(7, progress.GetRecord(flag).ActivatedDay);

            var locationSummary = WorldStateSummaryBuilder.BuildForLocation(new[] { flag }, progress, location, WorldStateSummarySurface.LocationPanel);
            var resultSummary = WorldStateSummaryBuilder.BuildForSurface(new[] { flag }, progress, WorldStateSummarySurface.DayResult);
            var logSummary = WorldStateSummaryBuilder.BuildForSurface(new[] { flag }, progress, WorldStateSummarySurface.WorldLog);

            Assert.AreEqual(1, locationSummary.Length, "WORLD-STATE-EDIT-005 failed: location UI summary should be generated from the common model.");
            Assert.AreEqual(1, resultSummary.Length, "WORLD-STATE-EDIT-005 failed: day result UI summary should be generated from the common model.");
            Assert.AreEqual(1, logSummary.Length, "WORLD-STATE-EDIT-005 failed: world log UI summary should be generated from the common model.");
            Assert.AreEqual(resultSummary[0].FlagId, logSummary[0].FlagId);
            Assert.AreEqual(resultSummary[0].DisplayName, logSummary[0].DisplayName);
        }

        [Test]
        public void WORLD_STATE_EDIT_006_007_SaveLoadRestoresPersonalAndSharedScopesSeparately()
        {
            var shared = MakeFlag("world.shop.new-stock", MakeLocation("location.shop", "Shop"), WorldStateScopeKind.Shared);
            var personal = MakeFlag("world.mentor.notice", null, WorldStateScopeKind.Personal);
            var progress = new WorldStateProgress("slot-b", "player-a");

            progress.TryActivate(shared, new WorldStateActivationSource("chain.shop", "step.stock", "event.stock", 2));
            progress.TryActivate(personal, new WorldStateActivationSource("chain.mentor", "step.notice", "event.notice", 3));
            progress.MarkNotificationSeen(personal);
            progress.MarkEffectVersionApplied(shared, 4);

            var loaded = WorldStateProgress.FromSaveData(progress.ToSaveData(), "slot-b", "player-a");

            Assert.IsTrue(loaded.IsActive(shared));
            Assert.IsTrue(loaded.IsActive(personal));
            Assert.AreEqual(WorldStateScopeKind.Shared, loaded.GetRecord(shared).Scope);
            Assert.AreEqual(WorldStateScopeKind.Personal, loaded.GetRecord(personal).Scope);
            Assert.IsFalse(loaded.GetRecord(shared).SeenNotification);
            Assert.IsTrue(loaded.GetRecord(personal).SeenNotification);
            Assert.AreEqual(4, loaded.GetRecord(shared).AppliedEffectVersion);
        }

        [Test]
        public void WORLD_STATE_EDIT_008_RepeatedQuestResultDoesNotDuplicateNotificationRewardOrEffect()
        {
            var flag = MakeFlag("world.workshop.repair-bench", null, WorldStateScopeKind.Shared);
            var progress = new WorldStateProgress("slot-c", "player-a");
            var reward = ScriptableObject.CreateInstance<WorldStateChangeQuestReward>();
            reward.ConfigureForTests(new[] { flag }, "chain.workshop", "step.repair", "event.repair", 4);
            var context = new RewardRuntimeContext(null, null, null, null, null, progress);

            Assert.IsTrue(reward.CanApply(in context));
            reward.Apply(in context);
            Assert.IsFalse(reward.CanApply(in context));
            reward.Apply(in context);

            Assert.AreEqual(1, progress.ActiveFlagCount);
            Assert.AreEqual(1, progress.TodayWorldChangeSummary.Length);
            Assert.AreEqual(1, progress.NewNotificationCount);
        }

        [Test]
        public void WORLD_STATE_EDIT_009_LookupCacheResolvesFlagsWithoutRepeatedRegistryScans()
        {
            var flags = new[]
            {
                MakeFlag("world.library.archive-open", null, WorldStateScopeKind.Shared),
                MakeFlag("world.alley.hidden-path", null, WorldStateScopeKind.Shared),
            };
            var cache = new WorldStateFlagLookupCache(flags);

            Assert.IsTrue(cache.TryGetFlag("world.alley.hidden-path", out var found));
            Assert.AreSame(flags[1], found);
            Assert.IsTrue(cache.TryGetFlag("world.library.archive-open", out _));
            Assert.AreEqual(1, cache.BuildCount, "WORLD-STATE-EDIT-009 failed: world-state lookup should not scan the registry on every query.");
        }

        [Test]
        public void WORLD_STATE_EDIT_004_SourceDoesNotBranchByWorldStateEntityIds()
        {
            string[] files =
            {
                "Assets/Scripts/Game/WorldState/WorldStateConditionBase.cs",
                "Assets/Scripts/Game/WorldState/WorldStateEffectBase.cs",
                "Assets/Scripts/Game/WorldState/WorldStateTypes.cs",
                "Assets/Scripts/Game/WorldState/WorldStateProgress.cs",
                "Assets/Scripts/Game/Quests/Rewards/WorldStateChangeQuestReward.cs",
                "Assets/Scripts/UI/WorldState/WorldStateChangeCard.cs"
            };

            for (int i = 0; i < files.Length; i++)
            {
                Assert.IsTrue(File.Exists(files[i]), files[i] + " must exist.");
                string source = File.ReadAllText(files[i]);
                StringAssert.DoesNotContain("flagId ==", source, files[i] + " must not branch by flag id.");
                StringAssert.DoesNotContain("questId ==", source, files[i] + " must not branch by quest id.");
                StringAssert.DoesNotContain("chainId ==", source, files[i] + " must not branch by chain id.");
                StringAssert.DoesNotContain("locationId ==", source, files[i] + " must not branch by location id.");
                StringAssert.DoesNotContain("npcId ==", source, files[i] + " must not branch by npc id.");
                StringAssert.DoesNotContain("switch (flagId", source, files[i] + " must not switch by flag id.");
            }
        }

        private static WorldStateFlagDefinition MakeFlag(string id, LocationDefinition location, WorldStateScopeKind scope)
        {
            var flag = ScriptableObject.CreateInstance<WorldStateFlagDefinition>();
            flag.ConfigureForTests(
                id,
                id + ".name",
                id + ".desc",
                location,
                Array.Empty<ScriptableObject>(),
                Array.Empty<ScriptableObject>(),
                scope,
                WorldStateChangeKind.LocationUnlocked,
                "Next: " + id,
                new[] { WorldStateSummarySurface.DayResult, WorldStateSummarySurface.WorldLog, WorldStateSummarySurface.LocationPanel },
                new[] { WorldStateBadgeKind.New, WorldStateBadgeKind.Shared },
                10,
                1);
            return flag;
        }

        private static LocationDefinition MakeLocation(string id, string name)
        {
            var location = ScriptableObject.CreateInstance<LocationDefinition>();
            location.ConfigureForTests(id, name, Vector2.zero, Array.Empty<DiscoveryDefinition>());
            return location;
        }
    }
}
