using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Core.Encyclopedia;
using Rootborn.Core.Quests;
using Rootborn.Core.StudentLife;
using Rootborn.Core.WorldState;

namespace Rootborn.Core.Tests
{
    // 옮겨 옴: Assets/Tests/EditMode/WorldState/WorldStateUsageLoopTests.cs
    public sealed class WorldStateUsageTests
    {
        private static WorldStateFlag MakeFlag(string id)
            => new WorldStateFlag(id, id, id + ".desc", null, null, null, WorldStateScopeKind.Shared, WorldStateChangeKind.ObjectRevealed, "Use it",
                new[] { WorldStateSummarySurface.WorldLog }, new[] { WorldStateBadgeKind.Shared }, 0, 1);

        private static WorldStateUsage MakeUsage(string id, WorldStateFlag flag, IWorldStateUsageCondition[] conditions, IWorldStateUsageOutcome[] outcomes, WorldStateUsageRepeatPolicy policy = null, int sortPriority = 0)
            => new WorldStateUsage(id, id + ".name", id + ".desc", flag, null, "Interact", "Next", conditions, outcomes,
                policy ?? new WorldStateUsageRepeatPolicy(WorldStateUsageRepeatMode.Once, 0, 1, false),
                new[] { WorldStateUsageSummarySurface.LocationPanel, WorldStateUsageSummarySurface.WorldLog, WorldStateUsageSummarySurface.DayResult }, sortPriority);

        [Test]
        public void WORLD_USAGE_EDIT_001_002_DefinitionConditionsOutcomesAndRepeatPolicyAreDataLoaded()
        {
            var flag = MakeFlag("world.library.archive-open");
            var usage = MakeUsage("usage.library.archive-table", flag, new IWorldStateUsageCondition[] { new WorldStateUsageFlagActiveCondition() }, Array.Empty<IWorldStateUsageOutcome>());
            var index = new WorldStateUsageLookupCache(new[] { usage });

            Assert.IsTrue(index.TryGetById("usage.library.archive-table", out var loaded));
            Assert.AreSame(usage, loaded);
            Assert.AreEqual(1, index.GetByFlag(flag.Id).Length);
            Assert.AreEqual(0, index.GetByFlag(null).Length);
            Assert.IsNotNull(usage.RepeatPolicy);
            Assert.AreSame(WorldStateUsageRepeatPolicy.OncePolicy, MakeUsageWithoutPolicy().RepeatPolicy);
        }

        private static QuestDefinition Quest(string id)
            => new QuestDefinition(id, id, id, new QuestObjective[] { new TalkQuestObjective("talk", 2, "npc.a") });

        private static WorldStateUsage MakeUsageWithoutPolicy()
            => new WorldStateUsage("usage.no-policy", null, null, null, null, null, null, null, null, null, null, 0);

        [Test]
        public void WORLD_USAGE_CORE_TextKeysFallBackInOrder()
        {
            var usage = MakeUsageWithoutPolicy();
            Assert.AreEqual("usage.no-policy", usage.DisplayNameKey);
            Assert.AreEqual("usage.no-policy", usage.DescriptionKey);
            Assert.AreEqual("usage.no-policy", usage.InteractionPromptKey);
            Assert.AreEqual("usage.no-policy", usage.NextActionKey);
            Assert.AreEqual(string.Empty, usage.SourceFlagId);
        }

        [Test]
        public void WORLD_USAGE_EDIT_003_004_ActiveWorldFlagOpensUsagesThroughConditionStrategies()
        {
            var flag = MakeFlag("world.workshop.bench-open");
            var inactive = MakeFlag("world.shop.stock-open");
            var condition = new WorldStateUsageFlagActiveCondition();
            var usage = MakeUsage("usage.workshop.bench", flag, new IWorldStateUsageCondition[] { condition }, Array.Empty<IWorldStateUsageOutcome>());
            var hiddenUntilActive = MakeUsage("usage.shop.stock", inactive, new IWorldStateUsageCondition[] { condition }, Array.Empty<IWorldStateUsageOutcome>());
            var world = new WorldStateProgress("slot-a", "player-a");
            world.TryActivate(flag, new WorldStateActivationSource("quest.workshop", "step.reward", "event.reward", 1));
            var progress = new WorldStateUsageProgress("slot-a", "player-a");
            var context = new WorldStateUsageContext(world, progress, new StudentLifeProgress("slot-a", "player-a", 10, 10), null, null, 1);
            var cache = new WorldStateUsageLookupCache(new[] { usage, hiddenUntilActive });

            var summaries = WorldStateUsageSummaryBuilder.BuildForFlag(cache, flag.Id, context, WorldStateUsageSummarySurface.LocationPanel);

            Assert.AreEqual(1, summaries.Length);
            Assert.AreEqual("usage.workshop.bench", summaries[0].UsageId);
            Assert.IsTrue(summaries[0].IsAvailable);
            Assert.IsFalse(condition.Evaluate(new WorldStateUsageContext(new WorldStateProgress("slot-a", "player-a"), progress, context.StudentLifeProgress, null, null, 1), usage));
            Assert.AreEqual(0, WorldStateUsageSummaryBuilder.BuildForFlag(cache, inactive.Id, context, WorldStateUsageSummarySurface.LocationPanel).Length, "Unavailable usages are hidden on the location panel.");
            var log = WorldStateUsageSummaryBuilder.BuildForFlag(cache, inactive.Id, context, WorldStateUsageSummarySurface.WorldLog);
            Assert.AreEqual(1, log.Length, "Other surfaces still list unavailable usages.");
            Assert.IsFalse(log[0].IsAvailable);
        }

        [Test]
        public void WORLD_USAGE_EDIT_005_008_OutcomesApplyMultiplePersonalRewardsOnce()
        {
            var flag = MakeFlag("world.library.archive-open");
            var entry = new EncyclopediaEntry("encyclopedia.archive", null, "Archive", "???", "Archive desc", "Find it", null, null, 2);
            var usage = MakeUsage("usage.library.archive-table", flag, Array.Empty<IWorldStateUsageCondition>(), new IWorldStateUsageOutcome[]
            {
                new WorldStateUsageQuestUnlockOutcome(new[] { Quest("quest.library.follow-up") }),
                new WorldStateUsageCareerHintOutcome(new[] { "career.research" }),
                new WorldStateUsageEncyclopediaOutcome(new[] { entry }),
                new WorldStateUsageActivityUnlockOutcome(new[] { "activity.archive-reading", null, string.Empty }),
            });
            var world = new WorldStateProgress("slot-a", "player-a");
            world.TryActivate(flag, new WorldStateActivationSource("quest.library", "step.reward", "event.reward", 1));
            var usageProgress = new WorldStateUsageProgress("slot-a", "player-a");
            var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            var questLog = new QuestLog(Array.Empty<QuestDefinition>());
            var encyclopedia = new EncyclopediaProgress("slot-a", "player-a");
            var context = new WorldStateUsageContext(world, usageProgress, student, questLog, encyclopedia, 1, 99L);
            var runner = new WorldStateUsageRunner();

            Assert.IsTrue(runner.TryUse(usage, context, out var first));
            Assert.AreEqual(WorldStateUsageResultKind.Applied, first.Kind);
            CollectionAssert.AreEqual(new[] { "quest", "career-hint", "encyclopedia", "activity" }, first.OutcomeIds);
            Assert.AreEqual(QuestStage.Active, questLog.GetState("quest.library.follow-up"));
            Assert.IsTrue(student.IsCareerHintUnlocked("career.research"));
            Assert.IsTrue(encyclopedia.IsUnlocked(entry.Id));
            Assert.AreEqual(2, encyclopedia.GetRevealStage(entry.Id));
            Assert.AreEqual(99L, encyclopedia.ToSaveData().Records[0].FirstDiscoveredUtcTicks);
            var record = usageProgress.GetRecord(usage.Id);
            CollectionAssert.AreEqual(new[] { "activity.archive-reading" }, record.UnlockedActivityIds);
            Assert.AreEqual("world.library.archive-open", record.SourceWorldStateFlagId);
            Assert.IsTrue(usageProgress.HasClaimedReward(usage.Id, "quest"));

            Assert.IsFalse(runner.TryUse(usage, context, out var duplicate));
            Assert.AreEqual(WorldStateUsageResultKind.RepeatBlocked, duplicate.Kind);
            Assert.AreEqual("already used", duplicate.Reason);
            Assert.AreEqual(1, usageProgress.GetRecord(usage.Id).UsedCount);
        }

        [Test]
        public void WORLD_USAGE_CORE_OutcomesThatChangeNothingAreNotReportedButStillRecorded()
        {
            var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            student.UnlockCareerHint("career.research");
            var encyclopedia = new EncyclopediaProgress("slot-a", "player-a");
            var entry = new EncyclopediaEntry("encyclopedia.archive", null, null, null, null, null, null, null);
            encyclopedia.TryUnlock(entry.Id, 1, 5L);
            var usage = MakeUsage("usage.repeat", null, null, new IWorldStateUsageOutcome[]
            {
                new WorldStateUsageCareerHintOutcome(new[] { "career.research" }),
                new WorldStateUsageEncyclopediaOutcome(new[] { entry, null }),
                new WorldStateUsageActivityUnlockOutcome(null),
                new WorldStateUsageQuestUnlockOutcome(new QuestDefinition[0]),
                null,
            });
            var progress = new WorldStateUsageProgress("slot-a", "player-a");
            var context = new WorldStateUsageContext(null, progress, student, null, encyclopedia, 0);

            Assert.IsTrue(new WorldStateUsageRunner().TryUse(usage, context, out var result));

            Assert.AreEqual(0, result.OutcomeIds.Length);
            var record = progress.GetRecord(usage.Id);
            CollectionAssert.AreEqual(new[] { "career.research" }, record.CareerHintGrantHistory);
            CollectionAssert.AreEqual(new[] { "encyclopedia.archive" }, record.DiscoveredEncyclopediaEntryIds);
            Assert.AreEqual(1, record.FirstUsedDay, "Day clamps to at least 1.");
            Assert.AreEqual(1, context.CurrentDay);
        }

        [Test]
        public void WORLD_USAGE_CORE_FailedConditionOrMissingProgressChangesNothing()
        {
            var flag = MakeFlag("world.a");
            var usage = MakeUsage("usage.a", flag, new IWorldStateUsageCondition[] { new WorldStateUsageFlagActiveCondition() }, new IWorldStateUsageOutcome[] { new WorldStateUsageCareerHintOutcome(new[] { "career.x" }) });
            var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            var progress = new WorldStateUsageProgress("slot-a", "player-a");
            var runner = new WorldStateUsageRunner();

            Assert.IsFalse(runner.TryUse(usage, new WorldStateUsageContext(new WorldStateProgress("slot-a", "player-a"), progress, student, null, null, 1), out var failed));
            Assert.AreEqual(WorldStateUsageResultKind.ConditionFailed, failed.Kind);
            Assert.IsFalse(runner.TryUse(usage, new WorldStateUsageContext(null, null, student, null, null, 1), out var missing));
            Assert.AreEqual(WorldStateUsageResultKind.InvalidRequest, missing.Kind);
            Assert.IsFalse(runner.TryUse(null, new WorldStateUsageContext(null, progress, student, null, null, 1), out var nullUsage));
            Assert.AreEqual(string.Empty, nullUsage.UsageId);

            Assert.IsFalse(student.IsCareerHintUnlocked("career.x"));
            Assert.AreEqual(0, progress.GetRecord(usage.Id).UsedCount);
            Assert.AreEqual(0, progress.TodayUsageSummary.Length);
        }

        [Test]
        public void WORLD_USAGE_EDIT_006_007_RepeatPolicyAndProgressPersistUsageState()
        {
            var progress = new WorldStateUsageProgress("slot-a", "player-a");
            var policy = new WorldStateUsageRepeatPolicy(WorldStateUsageRepeatMode.DailyCooldown, 2, 3, false);
            var usage = MakeUsage("usage.alley.hidden-path", MakeFlag("world.alley.open"), Array.Empty<IWorldStateUsageCondition>(), Array.Empty<IWorldStateUsageOutcome>(), policy);

            Assert.IsTrue(policy.CanUse(usage, progress, 1, out _));
            progress.MarkUsed(usage, 1, policy, new[] { "reward.a" }, new[] { "activity.a" }, new[] { "quest.a" }, new[] { "encyclopedia.a" }, new[] { "career.a" });
            Assert.IsFalse(policy.CanUse(usage, progress, 2, out var reason));
            StringAssert.Contains("cooldown", reason);
            Assert.IsTrue(policy.CanUse(usage, progress, 3, out _));

            var restored = WorldStateUsageProgress.FromSaveData(progress.ToSaveData());
            Assert.AreEqual(1, restored.GetRecord(usage.Id).UsedCount);
            Assert.AreEqual(3, restored.GetRecord(usage.Id).CooldownUntilDay);
            Assert.IsTrue(restored.HasClaimedReward(usage.Id, "reward.a"));
            Assert.AreEqual(1, restored.TodayUsageSummary.Length);
            Assert.AreEqual("world.alley.open", restored.TodayUsageSummary[0].SourceWorldStateFlagId);
        }

        [Test]
        public void WORLD_USAGE_CORE_RepeatModesBlockAsInUnity()
        {
            var progress = new WorldStateUsageProgress("slot-a", "player-a");
            var perDay = new WorldStateUsageRepeatPolicy(WorldStateUsageRepeatMode.OncePerDay, 0, 0, false);
            var maxUses = new WorldStateUsageRepeatPolicy(WorldStateUsageRepeatMode.MaxUses, 0, 2, false);
            var repeatable = new WorldStateUsageRepeatPolicy(WorldStateUsageRepeatMode.Repeatable, 5, 0, true);
            var daily = MakeUsage("usage.daily", null, null, null, perDay);
            var limited = MakeUsage("usage.limited", null, null, null, maxUses);
            var free = MakeUsage("usage.free", null, null, null, repeatable);

            progress.MarkUsed(daily, 4, perDay, null, null, null, null, null);
            Assert.IsFalse(perDay.CanUse(daily, progress, 4, out string sameDay));
            Assert.AreEqual("already used today", sameDay);
            Assert.IsTrue(perDay.CanUse(daily, progress, 5, out _));

            progress.MarkUsed(limited, 1, maxUses, null, null, null, null, null);
            Assert.IsTrue(maxUses.CanUse(limited, progress, 1, out _));
            progress.MarkUsed(limited, 1, maxUses, null, null, null, null, null);
            Assert.IsFalse(maxUses.CanUse(limited, progress, 9, out string max));
            Assert.AreEqual("max uses reached", max);

            progress.MarkUsed(free, 1, repeatable, null, null, null, null, null);
            Assert.AreEqual(0, progress.GetRecord(free.Id).CooldownUntilDay, "Only DailyCooldown sets a cooldown.");
            Assert.IsTrue(repeatable.CanUse(free, progress, 1, out _));
            Assert.IsFalse(repeatable.CanUse(null, progress, 1, out string missing));
            Assert.AreEqual("missing usage progress", missing);
            Assert.AreEqual(3, progress.TodayUsageSummary.Length, "daily, limited and free are each listed once today.");
        }

        [Test]
        public void WORLD_USAGE_EDIT_009_SummaryBuilderUsesLookupCacheInsteadOfRegistryScan()
        {
            var flag = MakeFlag("world.shop.stock-open");
            var usage = MakeUsage("usage.shop.stock", flag, Array.Empty<IWorldStateUsageCondition>(), Array.Empty<IWorldStateUsageOutcome>());
            var cache = new WorldStateUsageLookupCache(new[] { usage });
            var world = new WorldStateProgress("slot-a", "player-a");
            world.TryActivate(flag, new WorldStateActivationSource("quest.shop", "step.reward", "event.reward", 1));
            var context = new WorldStateUsageContext(world, new WorldStateUsageProgress("slot-a", "player-a"), new StudentLifeProgress("slot-a", "player-a", 10, 10), null, null, 1);

            WorldStateUsageSummaryBuilder.BuildForSurface(cache, context, WorldStateUsageSummarySurface.WorldLog);
            WorldStateUsageSummaryBuilder.BuildForSurface(cache, context, WorldStateUsageSummarySurface.WorldLog);

            Assert.AreEqual(1, cache.BuildCount);
            Assert.GreaterOrEqual(cache.LookupCount, 2);
        }

        [Test]
        public void WORLD_USAGE_CORE_SummaryReportsUsedRepeatableAndRewardIds()
        {
            var policy = new WorldStateUsageRepeatPolicy(WorldStateUsageRepeatMode.Repeatable, 0, 0, false);
            var later = MakeUsage("usage.later", null, null, null, null, sortPriority: 5);
            var usage = MakeUsage("usage.repeat", null, null, new IWorldStateUsageOutcome[] { new WorldStateUsageActivityUnlockOutcome(new[] { "activity.a" }), new WorldStateUsageCareerHintOutcome(new[] { "career.a" }) }, policy, sortPriority: 1);
            var progress = new WorldStateUsageProgress("slot-a", "player-a");
            var context = new WorldStateUsageContext(null, progress, new StudentLifeProgress("slot-a", "player-a", 1, 1), null, null, 1);
            new WorldStateUsageRunner().TryUse(usage, context, out _);
            var cache = new WorldStateUsageLookupCache(new[] { later, usage, null });

            var summaries = WorldStateUsageSummaryBuilder.BuildForSurface(cache, context, WorldStateUsageSummarySurface.DayResult);

            Assert.AreEqual(2, summaries.Length);
            Assert.AreEqual("usage.repeat", summaries[0].UsageId, "Usages are ordered by ascending sort priority.");
            Assert.IsTrue(summaries[0].IsUsed);
            Assert.IsTrue(summaries[0].IsRepeatable);
            CollectionAssert.AreEqual(new[] { "activity.a", "career.a" }, summaries[0].RewardSummaryIds);
            Assert.IsFalse(summaries[1].IsUsed);
            Assert.IsFalse(summaries[1].IsRepeatable, "A usage that was never used is not marked repeatable.");
            Assert.AreEqual(0, WorldStateUsageSummaryBuilder.BuildForSurface(null, context, WorldStateUsageSummarySurface.DayResult).Length);
        }

        [Test]
        public void WORLD_USAGE_CORE_SaveFilesRoundTripPerPlayerWithSanitizedNames()
        {
            var store = new InMemorySaveStore();
            var progress = WorldStateUsageSaveFiles.LoadOrCreate(store, "slot-a", "steam:7");
            var usage = MakeUsage("usage.a", MakeFlag("world.a"), null, null);
            progress.MarkUsed(usage, 2, usage.RepeatPolicy, new[] { "activity" }, new[] { "activity.a" }, null, null, null);
            WorldStateUsageSaveFiles.Save(store, progress);

            Assert.IsTrue(store.Contains("slot-a", "world-state-usage-steam_7.json"));
            var loaded = WorldStateUsageSaveFiles.LoadOrCreate(store, "slot-a", "steam:7");
            Assert.AreEqual("steam:7", loaded.PlayerId);
            Assert.AreEqual(2, loaded.GetRecord("usage.a").LastUsedDay);
            Assert.AreEqual(0, WorldStateUsageSaveFiles.LoadOrCreate(store, "slot-a", "player-b").RecordCount);
        }

        [Test]
        public void WORLD_USAGE_CORE_FromSaveDataClampsNegativesAndSkipsUnknownToday()
        {
            var data = new WorldStateUsageProgressSaveData
            {
                SaveSlot = "slot-a",
                PlayerId = "player-a",
                Records = new[] { new WorldStateUsageRecordSaveData { UsageId = "usage.a", UsedCount = -3, FirstUsedDay = -1, LastUsedDay = -1, CooldownUntilDay = -9, RewardClaimedIds = new[] { "r", null, "" } }, null },
                TodayUsageIds = new[] { "usage.a", "usage.missing", "usage.a" },
            };

            var progress = WorldStateUsageProgress.FromSaveData(data);
            var record = progress.GetRecord("usage.a");

            Assert.AreEqual(0, record.UsedCount);
            Assert.AreEqual(0, record.CooldownUntilDay);
            CollectionAssert.AreEqual(new[] { "r" }, record.RewardClaimedIds);
            Assert.AreEqual(1, progress.TodayUsageSummary.Length);
            Assert.AreEqual(1, progress.RecordCount);
            Assert.AreEqual("default", WorldStateUsageProgress.FromSaveData(null).SaveSlot);
        }

        [Test]
        public void WORLD_USAGE_CORE_RepeatedQuestUnlockKeepsStartedQuestProgress()
        {
            // Unity 판과 달라진 동작: 원본은 반복 사용 때마다 QuestLog.AddQuest로 퀘스트를 새로 넣어 진행도를 지웠다.
            var quest = Quest("quest.repeat");
            var usage = MakeUsage("usage.repeat-quest", null, null, new IWorldStateUsageOutcome[] { new WorldStateUsageQuestUnlockOutcome(new[] { quest, null }) },
                new WorldStateUsageRepeatPolicy(WorldStateUsageRepeatMode.Repeatable, 0, 0, false));
            var questLog = new QuestLog(Array.Empty<QuestDefinition>());
            var progress = new WorldStateUsageProgress("slot-a", "player-a");
            var context = new WorldStateUsageContext(null, progress, null, questLog, null, 1);
            var runner = new WorldStateUsageRunner();

            Assert.IsTrue(runner.TryUse(usage, context, out var first));
            CollectionAssert.AreEqual(new[] { "quest" }, first.OutcomeIds);
            questLog.RecordEvent(new QuestEvent(QuestEventKind.Talk, "talk-1", "npc.a"));
            Assert.AreEqual(1, questLog.GetObjectiveCount(quest, 0));

            Assert.IsTrue(runner.TryUse(usage, context, out var second));
            Assert.AreEqual(0, second.OutcomeIds.Length, "An already started quest is not unlocked again.");
            Assert.AreEqual(QuestStage.Active, questLog.GetState(quest));
            Assert.AreEqual(1, questLog.GetObjectiveCount(quest, 0), "Progress is kept.");
            CollectionAssert.AreEqual(new[] { "quest.repeat" }, progress.GetRecord(usage.Id).UnlockedFollowUpQuestIds);
            Assert.AreEqual(2, progress.GetRecord(usage.Id).UsedCount);
        }
    }
}
