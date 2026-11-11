using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Core.WorldState;

namespace Rootborn.Core.Tests
{
    // 옮겨 옴: Assets/Tests/EditMode/WorldState/WorldStateCoreTests.cs, WorldStatePersistenceTests.cs
    // (퀘스트 보상 WorldStateChangeQuestReward는 Quests 영역이라, 001·008은 그 보상이 부르는 진행 상태 호출로 옮김. 004는 소스 검사라 옮기지 않음)
    public sealed class WorldStateCoreTests
    {
        private sealed class Names : IWorldStateDisplayNames
        {
            public string LocationName(string locationId) => locationId + ".display";
            public string NpcName(string npcId) => npcId + ".display";
        }

        private sealed class Target : IWorldStateEffectTarget
        {
            public int Calls;
            public bool Active = true;
            public void SetActive(bool active) { Calls++; Active = active; }
        }

        internal static WorldStateFlag MakeFlag(string id, string locationId, WorldStateScopeKind scope, int effectVersion = 1, string[] npcIds = null, WorldStateSummarySurface[] surfaces = null)
        {
            return new WorldStateFlag(id, id + ".name", id + ".desc", locationId, npcIds, null, scope, WorldStateChangeKind.LocationUnlocked, "Next: " + id,
                surfaces ?? new[] { WorldStateSummarySurface.DayResult, WorldStateSummarySurface.WorldLog, WorldStateSummarySurface.LocationPanel },
                new[] { WorldStateBadgeKind.New, WorldStateBadgeKind.Shared }, 10, effectVersion);
        }

        // WorldStateChangeQuestReward.Apply가 하는 일: 켜지지 않았으면 켜고, 효과 버전을 적용됨으로 표시.
        private static void ApplyQuestReward(WorldStateProgress progress, WorldStateFlag flag, WorldStateActivationSource source)
        {
            if (progress.TryActivate(flag, source)) progress.MarkEffectVersionApplied(flag, flag.EffectVersion);
        }

        [Test]
        public void WORLD_STATE_EDIT_001_003_005_ActivationIsIdempotentAndBuildsSharedSummaries()
        {
            var flag = MakeFlag("world.library.archive-open", "location.library", WorldStateScopeKind.Shared, npcIds: new[] { "npc.librarian", "npc.other" });
            var progress = new WorldStateProgress("slot-a", "player-a");
            var source = new WorldStateActivationSource("quest.library", "step.archive", "event.archive-open", 7);

            ApplyQuestReward(progress, flag, source);
            ApplyQuestReward(progress, flag, source);

            Assert.IsTrue(progress.IsActive(flag));
            Assert.AreEqual(1, progress.ActiveFlagCount);
            Assert.AreEqual(1, progress.TodayWorldChangeSummary.Length);
            Assert.AreEqual("world.library.archive-open", progress.TodayWorldChangeSummary[0].FlagId);
            Assert.AreEqual("quest.library", progress.GetRecord(flag).SourceQuestChainId);
            Assert.AreEqual("step.archive", progress.GetRecord(flag).SourceQuestStepId);
            Assert.AreEqual("event.archive-open", progress.GetRecord(flag).SourceEventId);
            Assert.AreEqual(7, progress.GetRecord(flag).ActivatedDay);
            Assert.AreEqual(1, progress.GetRecord(flag).AppliedEffectVersion);

            var names = new Names();
            var locationSummary = WorldStateSummaryBuilder.BuildForLocation(new[] { flag }, progress, "location.library", WorldStateSummarySurface.LocationPanel, names);
            var otherLocation = WorldStateSummaryBuilder.BuildForLocation(new[] { flag }, progress, "location.cafe", WorldStateSummarySurface.LocationPanel, names);
            var resultSummary = WorldStateSummaryBuilder.BuildForSurface(new[] { flag }, progress, WorldStateSummarySurface.DayResult, names);
            var logSummary = WorldStateSummaryBuilder.BuildForSurface(new[] { flag }, progress, WorldStateSummarySurface.WorldLog, names);

            Assert.AreEqual(1, locationSummary.Length);
            Assert.AreEqual(0, otherLocation.Length);
            Assert.AreEqual(1, resultSummary.Length);
            Assert.AreEqual(1, logSummary.Length);
            Assert.AreEqual(resultSummary[0].FlagId, logSummary[0].FlagId);
            Assert.AreEqual("world.library.archive-open.name", logSummary[0].DisplayName);
            Assert.AreEqual("location.library.display", logSummary[0].LocationName);
            Assert.AreEqual("npc.librarian.display", logSummary[0].NpcName, "The first related NPC names the summary.");
            Assert.AreEqual(WorldStateScopeKind.Shared, logSummary[0].Scope);
            Assert.IsFalse(logSummary[0].SeenNotification);
        }

        [Test]
        public void WORLD_STATE_CORE_SummarySkipsInactiveOrHiddenFlagsAndSortsByPriority()
        {
            var progress = new WorldStateProgress("slot-a", "player-a");
            var low = new WorldStateFlag("world.low", null, null, null, null, null, WorldStateScopeKind.Shared, WorldStateChangeKind.ObjectRevealed, null, null, null, 1, 1);
            var high = new WorldStateFlag("world.high", null, null, null, null, null, WorldStateScopeKind.Shared, WorldStateChangeKind.ObjectRevealed, null, null, null, 9, 1);
            var hidden = MakeFlag("world.hidden", null, WorldStateScopeKind.Shared, surfaces: new[] { WorldStateSummarySurface.DayResult });
            var inactive = MakeFlag("world.inactive", null, WorldStateScopeKind.Shared);
            progress.TryActivate(low, default);
            progress.TryActivate(high, default);
            progress.TryActivate(hidden, default);

            var summary = WorldStateSummaryBuilder.BuildForSurface(new[] { low, null, high, hidden, inactive }, progress, WorldStateSummarySurface.WorldLog, null);

            Assert.AreEqual(2, summary.Length);
            Assert.AreEqual("world.high", summary[0].FlagId);
            Assert.AreEqual("world.low", summary[1].DisplayName, "Display key falls back to the id.");
            Assert.AreEqual(string.Empty, summary[1].LocationName);
            Assert.AreEqual(0, WorldStateSummaryBuilder.BuildForSurface(null, progress, WorldStateSummarySurface.WorldLog, null).Length);
        }

        [Test]
        public void WORLD_STATE_EDIT_006_007_SaveLoadRestoresPersonalAndSharedScopesSeparately()
        {
            var shared = MakeFlag("world.shop.new-stock", "location.shop", WorldStateScopeKind.Shared);
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
            Assert.AreEqual(2, loaded.TodayWorldChangeSummary.Length);
        }

        [Test]
        public void WORLD_STATE_EDIT_008_RepeatedActivationDoesNotDuplicateNotificationOrEffect()
        {
            var flag = MakeFlag("world.workshop.repair-bench", null, WorldStateScopeKind.Shared);
            var progress = new WorldStateProgress("slot-c", "player-a");
            var source = new WorldStateActivationSource("chain.workshop", "step.repair", "event.repair", 4);

            Assert.IsTrue(progress.TryActivate(flag, source));
            Assert.IsFalse(progress.TryActivate(flag, source));
            Assert.IsTrue(progress.MarkEffectVersionApplied(flag, 1));
            Assert.IsFalse(progress.MarkEffectVersionApplied(flag, 1));

            Assert.AreEqual(1, progress.ActiveFlagCount);
            Assert.AreEqual(1, progress.TodayWorldChangeSummary.Length);
            Assert.AreEqual(1, progress.NewNotificationCount);
            Assert.IsTrue(progress.MarkNotificationSeen(flag));
            Assert.IsFalse(progress.MarkNotificationSeen(flag));
            Assert.AreEqual(0, progress.NewNotificationCount);
        }

        [Test]
        public void WORLD_STATE_CORE_RejectsNullOrEmptyFlagsWithoutChangingState()
        {
            var progress = new WorldStateProgress(null, null);
            var empty = MakeFlag(string.Empty, null, WorldStateScopeKind.Shared);

            Assert.AreEqual("default", progress.SaveSlot);
            Assert.AreEqual("player", progress.PlayerId);
            Assert.IsFalse(progress.TryActivate(null, default));
            Assert.IsFalse(progress.TryActivate(empty, default));
            Assert.IsFalse(progress.IsActive((WorldStateFlag)null));
            Assert.IsFalse(progress.IsActive(string.Empty));
            Assert.IsFalse(progress.MarkNotificationSeen(MakeFlag("world.none", null, WorldStateScopeKind.Shared)));
            Assert.IsFalse(progress.MarkEffectVersionApplied(null, 3));
            Assert.AreEqual(string.Empty, progress.GetRecord(null).FlagId ?? string.Empty);
            Assert.AreEqual(0, progress.ActiveFlagCount);
            Assert.AreEqual(0, progress.TodayWorldChangeSummary.Length);
        }

        [Test]
        public void WORLD_STATE_CORE_RestoreAndMergeRequireMatchingOwner()
        {
            var flagA = MakeFlag("world.a", null, WorldStateScopeKind.Shared);
            var flagB = MakeFlag("world.b", null, WorldStateScopeKind.Shared);
            var source = new WorldStateProgress("slot-a", "player-a");
            source.TryActivate(flagA, default);
            source.MarkNotificationSeen(flagA);
            source.MarkEffectVersionApplied(flagA, 3);
            var data = source.ToSaveData();

            var other = new WorldStateProgress("slot-a", "player-b");
            Assert.IsFalse(other.RestoreFromSaveData(data));
            Assert.IsFalse(other.MergeFromSaveData(data));
            Assert.IsFalse(other.IsActive(flagA));

            var target = new WorldStateProgress("slot-a", "player-a");
            target.TryActivate(flagA, default);
            target.TryActivate(flagB, default);
            Assert.IsTrue(target.MergeFromSaveData(data));
            Assert.IsTrue(target.GetRecord(flagA).SeenNotification);
            Assert.AreEqual(3, target.GetRecord(flagA).AppliedEffectVersion);
            Assert.IsTrue(target.IsActive(flagB), "Merge keeps flags that only this copy has.");
            Assert.AreEqual(2, target.TodayWorldChangeSummary.Length);

            Assert.IsTrue(target.RestoreFromSaveData(data));
            Assert.IsFalse(target.IsActive(flagB), "Restore replaces everything.");
        }

        [Test]
        public void WORLD_STATE_CORE_FromSaveDataSkipsBadRecordsAndDefaultsUnknownScopeToShared()
        {
            var data = new WorldStateProgressSaveData
            {
                SaveSlot = string.Empty,
                PlayerId = null,
                Records = new[]
                {
                    new WorldStateFlagRecordSaveData { FlagId = "world.a", Scope = "Nonsense", ActivatedDay = 2 },
                    new WorldStateFlagRecordSaveData { FlagId = string.Empty, Scope = "Personal" },
                    null,
                },
                TodayWorldChangeFlagIds = new[] { "world.a", "world.a", "world.missing", null },
            };

            var loaded = WorldStateProgress.FromSaveData(data, "slot-x", "player-x");

            Assert.AreEqual("slot-x", loaded.SaveSlot);
            Assert.AreEqual("player-x", loaded.PlayerId);
            Assert.AreEqual(1, loaded.ActiveFlagCount);
            Assert.AreEqual(WorldStateScopeKind.Shared, loaded.TodayWorldChangeSummary[0].Scope);
            Assert.AreEqual(1, loaded.TodayWorldChangeSummary.Length);
            Assert.AreEqual(0, WorldStateProgress.FromSaveData(null, "s", "p").ActiveFlagCount);
        }

        [Test]
        public void WORLD_STATE_EDIT_009_LookupCacheResolvesFlagsWithoutRepeatedRegistryScans()
        {
            var flags = new[] { MakeFlag("world.library.archive-open", null, WorldStateScopeKind.Shared), MakeFlag("world.alley.hidden-path", null, WorldStateScopeKind.Shared) };
            var cache = new WorldStateFlagLookupCache(flags);

            Assert.IsTrue(cache.TryGetFlag("world.alley.hidden-path", out var found));
            Assert.AreSame(flags[1], found);
            Assert.IsTrue(cache.TryGetFlag("world.library.archive-open", out _));
            Assert.IsFalse(cache.TryGetFlag(null, out _));
            Assert.AreEqual(1, cache.BuildCount);
        }

        [Test]
        public void WORLD_STATE_EDIT_006_007_PersistenceSavesLoadsPersonalAndSharedWorldStatePerPlayer()
        {
            var store = new InMemorySaveStore();
            var shared = MakeFlag("world.library.archive-open", null, WorldStateScopeKind.Shared);
            var personal = MakeFlag("world.mentor.notice", null, WorldStateScopeKind.Personal);
            var slotWide = MakeFlag("world.slot.notice", null, WorldStateScopeKind.SaveSlot);
            var progress = WorldStateSaveFiles.LoadOrCreate(store, "slot-0", "player-a");

            progress.TryActivate(shared, new WorldStateActivationSource("chain.library", "step.archive", "event.archive", 2));
            progress.TryActivate(personal, new WorldStateActivationSource("chain.mentor", "step.notice", "event.notice", 3));
            progress.TryActivate(slotWide, default);
            progress.MarkNotificationSeen(personal);
            WorldStateSaveFiles.Save(store, progress);

            var loadedA = WorldStateSaveFiles.LoadOrCreate(store, "slot-0", "player-a");
            var loadedB = WorldStateSaveFiles.LoadOrCreate(store, "slot-0", "player-b");

            Assert.IsTrue(loadedA.IsActive(shared));
            Assert.IsTrue(loadedA.IsActive(personal));
            Assert.AreEqual(WorldStateScopeKind.Personal, loadedA.GetRecord(personal).Scope);
            Assert.IsTrue(loadedA.GetRecord(personal).SeenNotification);
            Assert.IsTrue(loadedB.IsActive(shared), "Shared flags are visible to another player in the same slot.");
            Assert.IsTrue(loadedB.IsActive(slotWide), "Save-slot flags are visible to another player in the same slot.");
            Assert.IsFalse(loadedB.IsActive(personal), "Personal flags must not leak between players.");
            Assert.AreEqual("player-b", loadedB.PlayerId);
            Assert.IsTrue(store.TryRead("slot-0", "world-state-progress-shared.json", out WorldStateProgressSaveData sharedFile));
            Assert.AreEqual(string.Empty, sharedFile.PlayerId);
            Assert.IsFalse(WorldStateSaveFiles.LoadOrCreate(store, "slot-1", "player-a").IsActive(shared));
        }

        [Test]
        public void WORLD_STATE_CORE_FlagActiveConditionComparesWithExpectedValue()
        {
            var flag = MakeFlag("world.a", null, WorldStateScopeKind.Shared);
            var progress = new WorldStateProgress("slot-a", "player-a");
            var context = new WorldStateConditionContext(progress, "location.a", "npc.a", null, null);

            Assert.IsFalse(new WorldStateFlagActiveCondition("world.a", true).IsSatisfied(context));
            Assert.IsTrue(new WorldStateFlagActiveCondition("world.a", false).IsSatisfied(context));
            progress.TryActivate(flag, default);
            Assert.IsTrue(new WorldStateFlagActiveCondition("world.a", true).IsSatisfied(context));
            Assert.IsFalse(new WorldStateFlagActiveCondition(null, true).IsSatisfied(context), "No flag means 'not expected active'.");
            Assert.IsTrue(new WorldStateFlagActiveCondition("world.a", false).IsSatisfied(new WorldStateConditionContext(null, null, null, null, null)));
        }

        [Test]
        public void WORLD_STATE_CORE_SetObjectActiveEffectAppliesOncePerEffectVersion()
        {
            var flag = MakeFlag("world.a", null, WorldStateScopeKind.Shared, effectVersion: 2);
            var progress = new WorldStateProgress("slot-a", "player-a");
            var target = new Target();
            var effect = new WorldStateSetObjectActiveEffect(false);
            var context = new WorldStateEffectContext(progress, target);

            Assert.IsFalse(effect.Apply(context, flag), "Inactive flags do nothing.");
            Assert.AreEqual(0, target.Calls);
            progress.TryActivate(flag, default);
            Assert.IsTrue(effect.Apply(context, flag));
            Assert.IsFalse(target.Active);
            Assert.AreEqual(2, progress.GetRecord(flag).AppliedEffectVersion);
            Assert.IsFalse(effect.Apply(context, flag));
            Assert.AreEqual(1, target.Calls);
            Assert.IsFalse(effect.CanApply(new WorldStateEffectContext(progress, null), flag));
        }

        [Test]
        public void WORLD_STATE_CORE_ScopeDefinitionOverridesScopeKindAndVersionIsAtLeastOne()
        {
            var scope = new WorldStateScope("scope.personal", WorldStateScopeKind.Personal, null);
            var flag = new WorldStateFlag("world.a", null, null, null, null, null, WorldStateScopeKind.Shared, WorldStateChangeKind.EventAvailable, null, null, null, 0, 0, scope);

            Assert.AreEqual(WorldStateScopeKind.Personal, flag.Scope);
            Assert.AreEqual(1, flag.EffectVersion);
            Assert.AreEqual("scope.personal", scope.DisplayNameKey);
            Assert.AreEqual("world.a", flag.DescriptionKey);
            Assert.IsTrue(flag.IsVisibleOn(WorldStateSummarySurface.LocationPanel), "No surfaces listed means every surface.");
        }
    }
}
