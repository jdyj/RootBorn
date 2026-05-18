using System;
using NUnit.Framework;
using Rootborn.Game.Encyclopedia;
using Rootborn.Game.Quests;
using Rootborn.Game.StudentLife;
using Rootborn.Game.WorldState;
using UnityEngine;

namespace Rootborn.Tests.EditMode.WorldState
{
    public sealed class WorldStateUsageLoopTests
    {
        [Test]
        public void WORLD_USAGE_EDIT_001_002_DefinitionConditionsOutcomesAndRepeatPolicyAreDataLoaded()
        {
            var flag = MakeFlag("world.library.archive-open");
            var usage = MakeUsage("usage.library.archive-table", flag, new WorldStateUsageConditionBase[] { ScriptableObject.CreateInstance<WorldStateUsageFlagActiveCondition>() }, Array.Empty<WorldStateUsageOutcomeBase>());
            var index = new WorldStateUsageLookupCache(new[] { usage });

            Assert.IsTrue(index.TryGetById("usage.library.archive-table", out var loaded));
            Assert.AreSame(usage, loaded);
            Assert.AreEqual(1, index.GetByFlag(flag.Id).Length);
            Assert.IsNotNull(usage.RepeatPolicy);

            UnityEngine.Object.DestroyImmediate(flag);
            UnityEngine.Object.DestroyImmediate(usage);
        }

        [Test]
        public void WORLD_USAGE_EDIT_003_004_ActiveWorldFlagOpensUsagesThroughConditionStrategies()
        {
            var flag = MakeFlag("world.workshop.bench-open");
            var inactive = MakeFlag("world.shop.stock-open");
            var condition = ScriptableObject.CreateInstance<WorldStateUsageFlagActiveCondition>();
            var usage = MakeUsage("usage.workshop.bench", flag, new WorldStateUsageConditionBase[] { condition }, Array.Empty<WorldStateUsageOutcomeBase>());
            var world = new WorldStateProgress("slot-a", "player-a");
            world.TryActivate(flag, new WorldStateActivationSource("quest.workshop", "step.reward", "event.reward", 1));
            var progress = new WorldStateUsageProgress("slot-a", "player-a");
            var context = new WorldStateUsageContext(world, progress, new StudentLifeProgress("slot-a", "player-a", 10, 10), null, null, null, 1);

            var summaries = WorldStateUsageSummaryBuilder.BuildForFlag(new WorldStateUsageLookupCache(new[] { usage }), flag.Id, context, WorldStateUsageSummarySurface.LocationPanel);

            Assert.AreEqual(1, summaries.Length);
            Assert.AreEqual("usage.workshop.bench", summaries[0].UsageId);
            Assert.IsFalse(condition.Evaluate(new WorldStateUsageContext(new WorldStateProgress("slot-a", "player-a"), progress, context.StudentLifeProgress, null, null, null, 1), usage));
            Assert.AreEqual(0, WorldStateUsageSummaryBuilder.BuildForFlag(new WorldStateUsageLookupCache(new[] { usage }), inactive.Id, context, WorldStateUsageSummarySurface.LocationPanel).Length);

            UnityEngine.Object.DestroyImmediate(flag);
            UnityEngine.Object.DestroyImmediate(inactive);
            UnityEngine.Object.DestroyImmediate(condition);
            UnityEngine.Object.DestroyImmediate(usage);
        }

        [Test]
        public void WORLD_USAGE_EDIT_005_008_OutcomesApplyMultiplePersonalRewardsOnce()
        {
            var flag = MakeFlag("world.library.archive-open");
            var quest = MakeQuest("quest.library.follow-up");
            var career = ScriptableObject.CreateInstance<CareerDefinition>();
            career.ConfigureForTests("career.research", "Research");
            var entry = ScriptableObject.CreateInstance<EncyclopediaEntryDefinition>();
            entry.ConfigureForTests("encyclopedia.archive", null, "Archive", "???", "Archive desc", "Find it", null, null);
            var questOutcome = ScriptableObject.CreateInstance<WorldStateUsageQuestUnlockOutcome>();
            questOutcome.ConfigureForTests(new[] { quest });
            var careerOutcome = ScriptableObject.CreateInstance<WorldStateUsageCareerHintOutcome>();
            careerOutcome.ConfigureForTests(new[] { career });
            var encyclopediaOutcome = ScriptableObject.CreateInstance<WorldStateUsageEncyclopediaOutcome>();
            encyclopediaOutcome.ConfigureForTests(new[] { entry });
            var usage = MakeUsage("usage.library.archive-table", flag, Array.Empty<WorldStateUsageConditionBase>(), new WorldStateUsageOutcomeBase[] { questOutcome, careerOutcome, encyclopediaOutcome });
            var world = new WorldStateProgress("slot-a", "player-a");
            world.TryActivate(flag, new WorldStateActivationSource("quest.library", "step.reward", "event.reward", 1));
            var usageProgress = new WorldStateUsageProgress("slot-a", "player-a");
            var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            var questLog = new QuestLog(Array.Empty<QuestDefinition>());
            var encyclopedia = new EncyclopediaProgress("slot-a", "player-a");
            var context = new WorldStateUsageContext(world, usageProgress, student, questLog, encyclopedia, null, 1);
            var runner = new WorldStateUsageRunner();

            Assert.IsTrue(runner.TryUse(usage, context, out var first));
            Assert.AreEqual(WorldStateUsageResultKind.Applied, first.Kind);
            Assert.AreEqual(3, first.OutcomeIds.Length);
            Assert.AreEqual(QuestState.Active, questLog.GetState(quest));
            Assert.IsTrue(student.IsCareerHintUnlocked(career));
            Assert.IsTrue(encyclopedia.IsUnlocked(entry.Id));

            Assert.IsFalse(runner.TryUse(usage, context, out var duplicate));
            Assert.AreEqual(WorldStateUsageResultKind.RepeatBlocked, duplicate.Kind);
            Assert.AreEqual(1, usageProgress.GetRecord(usage.Id).UsedCount);

            UnityEngine.Object.DestroyImmediate(flag);
            UnityEngine.Object.DestroyImmediate(quest);
            UnityEngine.Object.DestroyImmediate(career);
            UnityEngine.Object.DestroyImmediate(entry);
            UnityEngine.Object.DestroyImmediate(questOutcome);
            UnityEngine.Object.DestroyImmediate(careerOutcome);
            UnityEngine.Object.DestroyImmediate(encyclopediaOutcome);
            UnityEngine.Object.DestroyImmediate(usage);
        }

        [Test]
        public void WORLD_USAGE_EDIT_006_007_RepeatPolicyAndProgressPersistUsageState()
        {
            var progress = new WorldStateUsageProgress("slot-a", "player-a");
            var policy = ScriptableObject.CreateInstance<WorldStateUsageRepeatPolicyDefinition>();
            policy.ConfigureForTests(WorldStateUsageRepeatMode.DailyCooldown, 2, 3, false);
            var usage = MakeUsage("usage.alley.hidden-path", MakeFlag("world.alley.open"), Array.Empty<WorldStateUsageConditionBase>(), Array.Empty<WorldStateUsageOutcomeBase>());
            usage.ConfigureRepeatPolicyForTests(policy);

            Assert.IsTrue(policy.CanUse(usage, progress, 1, out _));
            progress.MarkUsed(usage, 1, policy, new[] { "reward.a" }, new[] { "activity.a" }, new[] { "quest.a" }, new[] { "encyclopedia.a" }, new[] { "career.a" });
            Assert.IsFalse(policy.CanUse(usage, progress, 2, out var reason));
            StringAssert.Contains("cooldown", reason);

            var restored = WorldStateUsageProgress.FromSaveData(progress.ToSaveData());
            Assert.AreEqual(1, restored.GetRecord(usage.Id).UsedCount);
            Assert.AreEqual(3, restored.GetRecord(usage.Id).CooldownUntilDay);
            Assert.IsTrue(restored.HasClaimedReward(usage.Id, "reward.a"));
            Assert.AreEqual(1, restored.TodayUsageSummary.Length);

            UnityEngine.Object.DestroyImmediate(policy);
            UnityEngine.Object.DestroyImmediate(usage.SourceFlag);
            UnityEngine.Object.DestroyImmediate(usage);
        }

        [Test]
        public void WORLD_USAGE_EDIT_009_SummaryBuilderUsesLookupCacheInsteadOfRegistryScan()
        {
            var flag = MakeFlag("world.shop.stock-open");
            var usage = MakeUsage("usage.shop.stock", flag, Array.Empty<WorldStateUsageConditionBase>(), Array.Empty<WorldStateUsageOutcomeBase>());
            var cache = new WorldStateUsageLookupCache(new[] { usage });
            var world = new WorldStateProgress("slot-a", "player-a");
            world.TryActivate(flag, new WorldStateActivationSource("quest.shop", "step.reward", "event.reward", 1));
            var context = new WorldStateUsageContext(world, new WorldStateUsageProgress("slot-a", "player-a"), new StudentLifeProgress("slot-a", "player-a", 10, 10), null, null, null, 1);

            WorldStateUsageSummaryBuilder.BuildForSurface(cache, context, WorldStateUsageSummarySurface.WorldLog);
            WorldStateUsageSummaryBuilder.BuildForSurface(cache, context, WorldStateUsageSummarySurface.WorldLog);

            Assert.AreEqual(1, cache.BuildCount);
            Assert.GreaterOrEqual(cache.LookupCount, 2);

            UnityEngine.Object.DestroyImmediate(flag);
            UnityEngine.Object.DestroyImmediate(usage);
        }

        private static WorldStateFlagDefinition MakeFlag(string id)
        {
            var flag = ScriptableObject.CreateInstance<WorldStateFlagDefinition>();
            flag.ConfigureForTests(id, id, id + ".desc", null, null, null, WorldStateScopeKind.Shared, WorldStateChangeKind.ObjectRevealed, "Use it", new[] { WorldStateSummarySurface.WorldLog }, new[] { WorldStateBadgeKind.Shared }, 0, 1);
            return flag;
        }

        private static WorldStateUsageDefinition MakeUsage(string id, WorldStateFlagDefinition flag, WorldStateUsageConditionBase[] conditions, WorldStateUsageOutcomeBase[] outcomes)
        {
            var usage = ScriptableObject.CreateInstance<WorldStateUsageDefinition>();
            var policy = ScriptableObject.CreateInstance<WorldStateUsageRepeatPolicyDefinition>();
            policy.ConfigureForTests(WorldStateUsageRepeatMode.Once, 0, 1, false);
            usage.ConfigureForTests(id, id + ".name", id + ".desc", flag, null, "Interact", "Next", conditions, outcomes, policy, new[] { WorldStateUsageSummarySurface.LocationPanel, WorldStateUsageSummarySurface.WorldLog, WorldStateUsageSummarySurface.DayResult }, 0);
            return usage;
        }

        private static QuestDefinition MakeQuest(string id)
        {
            var quest = ScriptableObject.CreateInstance<QuestDefinition>();
            quest.ConfigureForRuntime(id, id, id + ".desc", Array.Empty<QuestObjectiveBase>(), Array.Empty<QuestRewardBase>(), Array.Empty<QuestCompletionEffectBase>());
            return quest;
        }
    }
}
