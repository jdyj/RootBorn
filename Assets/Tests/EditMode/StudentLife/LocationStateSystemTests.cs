using System;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Dialogue;
using Rootborn.Game.DiscoveryClues;
using Rootborn.Game.Quests;
using Rootborn.Game.StudentLife;
using Rootborn.Game.WorldState;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class LocationStateSystemTests
    {
        [Test]
        public void LOCATION_STATE_EDIT_001_003_RegistryDataPathLoadsAndTimeConditionSelectsWithoutEntityBranches()
        {
            var location = MakeLocation("location.library");
            var morning = MakeTimeSlot("time.morning", 1);
            var evening = MakeTimeSlot("time.evening", 2);
            var morningCondition = ScriptableObject.CreateInstance<LocationStateTimeSlotCondition>();
            var eveningCondition = ScriptableObject.CreateInstance<LocationStateTimeSlotCondition>();
            var morningState = ScriptableObject.CreateInstance<LocationStateDefinition>();
            var eveningState = ScriptableObject.CreateInstance<LocationStateDefinition>();
            try
            {
                morningCondition.ConfigureForTests(new[] { morning });
                eveningCondition.ConfigureForTests(new[] { evening });
                morningState.ConfigureForTests("location.state.library-morning", "Library Morning", "Morning study", location, 10, new[] { morningCondition }, Array.Empty<LocationStateEffectBase>());
                eveningState.ConfigureForTests("location.state.library-evening", "Library Evening", "Quiet evening", location, 20, new[] { eveningCondition }, Array.Empty<LocationStateEffectBase>());

                var cache = new LocationStateLookupCache(new[] { morningState, eveningState });
                var resolver = new LocationStateResolver(cache, LocationStateConflictPolicyDefinition.HighestPriority());
                var progress = new StudentLifeProgress("slot", "player", 10, 10, 0, 8 * 60);

                var morningSummary = resolver.Resolve(new LocationStateContext(location, morning, progress, null, null, null, null));
                var eveningSummary = resolver.Resolve(new LocationStateContext(location, evening, progress, null, null, null, null));

                Assert.AreSame(morningState, morningSummary.PrimaryState, "LOCATION-STATE-EDIT-001 failed: state must load from location state data path/cache.");
                Assert.AreSame(eveningState, eveningSummary.PrimaryState, "LOCATION-STATE-EDIT-003 failed: time slot condition should select matching state.");
                Assert.AreEqual(1, cache.BuildCount, "LOCATION-STATE-EDIT-009 failed: cache should be built once, not per summary.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(eveningState);
                UnityEngine.Object.DestroyImmediate(morningState);
                UnityEngine.Object.DestroyImmediate(eveningCondition);
                UnityEngine.Object.DestroyImmediate(morningCondition);
                UnityEngine.Object.DestroyImmediate(evening);
                UnityEngine.Object.DestroyImmediate(morning);
                UnityEngine.Object.DestroyImmediate(location);
            }
        }

        [Test]
        public void LOCATION_STATE_EDIT_004_006_StrategyConditionsAndEffectsExposeActivitiesNpcsCluesObjectsPortalsAndShops()
        {
            var location = MakeLocation("location.library");
            var timeSlot = MakeTimeSlot("time.evening", 2);
            var worldFlag = ScriptableObject.CreateInstance<WorldStateFlagDefinition>();
            var relationship = ScriptableObject.CreateInstance<RelationshipDefinition>();
            var npc = MakeNpc("npc.librarian", location);
            var activity = MakeActivity("location.activity.library-archive", location);
            var clue = ScriptableObject.CreateInstance<DiscoveryClueDefinition>();
            var worldCondition = ScriptableObject.CreateInstance<LocationStateWorldStateCondition>();
            var affinityCondition = ScriptableObject.CreateInstance<LocationStateRelationshipCondition>();
            var effect = ScriptableObject.CreateInstance<LocationStateAvailabilityEffect>();
            var state = ScriptableObject.CreateInstance<LocationStateDefinition>();
            try
            {
                worldFlag.ConfigureForTests("world.library.archive-open", "Archive Open", "Archive opened", location, Array.Empty<ScriptableObject>(), Array.Empty<ScriptableObject>(), WorldStateScopeKind.Shared, WorldStateChangeKind.LocationUnlocked, "next", Array.Empty<WorldStateSummarySurface>(), Array.Empty<WorldStateBadgeKind>(), 1, 1);
                relationship.ConfigureForTests("relationship.librarian", "Librarian");
                clue.ConfigureForTests("clue.old-ledger", "Old Ledger", "Old ledger clue", worldFlag, null, location, 1, "clue.public", "clue.hidden", Array.Empty<DiscoveryClueSourceDefinition>(), Array.Empty<DiscoveryClueConditionBase>(), Array.Empty<DiscoveryClueCompletionBase>(), Array.Empty<DiscoveryClueOutcomeBase>(), Array.Empty<DiscoveryClueSummarySurface>(), 1);
                worldCondition.ConfigureForTests(worldFlag, true);
                affinityCondition.ConfigureForTests(relationship, 2);
                effect.ConfigureForTests(new[] { activity }, new[] { npc }, new[] { clue }, new[] { "object.archive-desk" }, new[] { "portal.backroom" }, new[] { "shop.library-special" });
                state.ConfigureForTests("location.state.library-archive", "Archive Open", "Research archive", location, 50, new LocationStateConditionBase[] { worldCondition, affinityCondition }, new LocationStateEffectBase[] { effect });

                var student = new StudentLifeProgress("slot", "player", 10, 10, 0, 18 * 60);
                student.AddRelationshipForTests(relationship, 2);
                var world = new WorldStateProgress("slot", "player");
                Assert.IsTrue(world.TryActivate(worldFlag, new WorldStateActivationSource(string.Empty, string.Empty, "test", 1)));

                var resolver = new LocationStateResolver(new LocationStateLookupCache(new[] { state }), LocationStateConflictPolicyDefinition.HighestPriority());
                var summary = resolver.Resolve(new LocationStateContext(location, timeSlot, student, world, null, null, null));

                Assert.AreSame(state, summary.PrimaryState, "LOCATION-STATE-EDIT-004 failed: strategy conditions should all be evaluated.");
                CollectionAssert.Contains(summary.AvailableActivityIds, activity.Id, "LOCATION-STATE-EDIT-006 failed: state effect must expose activities.");
                CollectionAssert.Contains(summary.PresentNpcIds, npc.Id, "LOCATION-STATE-EDIT-006 failed: state effect must expose NPCs.");
                CollectionAssert.Contains(summary.ClueIds, clue.Id, "LOCATION-STATE-EDIT-006 failed: state effect must expose clues.");
                CollectionAssert.Contains(summary.InteractableObjectIds, "object.archive-desk", "LOCATION-STATE-EDIT-006 failed: state effect must expose objects.");
                CollectionAssert.Contains(summary.PortalIds, "portal.backroom", "LOCATION-STATE-EDIT-006 failed: state effect must expose portals.");
                CollectionAssert.Contains(summary.ShopItemIds, "shop.library-special", "LOCATION-STATE-EDIT-006 failed: state effect must expose shop items.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(state);
                UnityEngine.Object.DestroyImmediate(effect);
                UnityEngine.Object.DestroyImmediate(affinityCondition);
                UnityEngine.Object.DestroyImmediate(worldCondition);
                UnityEngine.Object.DestroyImmediate(clue);
                UnityEngine.Object.DestroyImmediate(activity);
                UnityEngine.Object.DestroyImmediate(npc);
                UnityEngine.Object.DestroyImmediate(relationship);
                UnityEngine.Object.DestroyImmediate(worldFlag);
                UnityEngine.Object.DestroyImmediate(timeSlot);
                UnityEngine.Object.DestroyImmediate(location);
            }
        }

        [Test]
        public void LOCATION_STATE_EDIT_005_007_OneLocationSupportsMultipleTimeStatesAndConflictPolicies()
        {
            var location = MakeLocation("location.workshop");
            var slot = MakeTimeSlot("time.evening", 2);
            var low = MakeState("location.state.workshop-cleanup", location, 10);
            var high = MakeState("location.state.workshop-shipping", location, 30);
            try
            {
                var cache = new LocationStateLookupCache(new[] { low, high });
                var highest = new LocationStateResolver(cache, LocationStateConflictPolicyDefinition.HighestPriority());
                var merged = new LocationStateResolver(cache, LocationStateConflictPolicyDefinition.MergeAll());

                var highestSummary = highest.Resolve(new LocationStateContext(location, slot, new StudentLifeProgress("slot", "player", 10, 10, 0, 18 * 60), null, null, null, null));
                var mergedSummary = merged.Resolve(new LocationStateContext(location, slot, new StudentLifeProgress("slot", "player", 10, 10, 0, 18 * 60), null, null, null, null));

                Assert.AreSame(high, highestSummary.PrimaryState, "LOCATION-STATE-EDIT-007 failed: priority policy should select highest priority.");
                Assert.AreEqual(1, highestSummary.StateIds.Length);
                CollectionAssert.AreEquivalent(new[] { low.Id, high.Id }, mergedSummary.StateIds, "LOCATION-STATE-EDIT-007 failed: merge policy should keep all satisfied states.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(high);
                UnityEngine.Object.DestroyImmediate(low);
                UnityEngine.Object.DestroyImmediate(slot);
                UnityEngine.Object.DestroyImmediate(location);
            }
        }

        [Test]
        public void LOCATION_STATE_EDIT_008_ProgressPersistsDiscoveryRewardsLastVisitTodaySummaryAndHiddenHints()
        {
            var progress = new LocationStateProgress("slot-a", "player-a");
            progress.MarkDiscovered("location.library", "location.state.library-evening", 2, "time.evening", "Quiet Evening", false);
            progress.MarkRewardClaimed("location.state.library-evening", "reward.note");
            progress.MarkHiddenHintVisible("location.state.library-archive");

            var restored = LocationStateProgress.FromSaveData(progress.ToSaveData(), "fallback", "fallback-player");

            Assert.IsTrue(restored.HasDiscovered("location.state.library-evening"), "LOCATION-STATE-EDIT-008 failed: discovery flag must persist.");
            Assert.IsTrue(restored.HasRewardClaimed("location.state.library-evening", "reward.note"), "LOCATION-STATE-EDIT-008 failed: reward claimed flag must persist.");
            Assert.AreEqual("location.state.library-evening", restored.GetLastVisitedStateId("location.library"), "LOCATION-STATE-EDIT-008 failed: last visited state must persist.");
            Assert.IsTrue(restored.IsHiddenHintVisible("location.state.library-archive"), "LOCATION-STATE-EDIT-008 failed: hidden hint visibility must persist.");
            Assert.AreEqual(1, restored.TodaySummary.Length, "LOCATION-STATE-EDIT-008 failed: today location state summary must persist.");
            Assert.AreEqual("slot-a", restored.SaveSlot);
            Assert.AreEqual("player-a", restored.PlayerId);
        }

        private static LocationDefinition MakeLocation(string id)
        {
            var location = ScriptableObject.CreateInstance<LocationDefinition>();
            location.ConfigureForTests(id, id + ".name", Vector2.zero, Array.Empty<DiscoveryDefinition>());
            return location;
        }

        private static TimeSlotDefinition MakeTimeSlot(string id, int order)
        {
            var timeSlot = ScriptableObject.CreateInstance<TimeSlotDefinition>();
            timeSlot.ConfigureForTests(id, id + ".name", order);
            return timeSlot;
        }

        private static NpcDefinition MakeNpc(string id, LocationDefinition location)
        {
            var npc = ScriptableObject.CreateInstance<NpcDefinition>();
            npc.ConfigureForTests(id, id + ".name", id + ".intro", location, Array.Empty<NpcRoleDefinition>(), null, Array.Empty<NpcDialogueConditionBase>());
            return npc;
        }

        private static LocationActivityDefinition MakeActivity(string id, LocationDefinition location)
        {
            var activity = ScriptableObject.CreateInstance<LocationActivityDefinition>();
            activity.ConfigureForTests(id, id + ".name", location, LocationGrowthRoute.SelfStudy, 15, 0, 0, 0, Array.Empty<LocationActivityRequirementBase>(), Array.Empty<LocationActivityOutcomeBase>());
            return activity;
        }

        private static LocationStateDefinition MakeState(string id, LocationDefinition location, int priority)
        {
            var state = ScriptableObject.CreateInstance<LocationStateDefinition>();
            state.ConfigureForTests(id, id + ".name", id + ".description", location, priority, Array.Empty<LocationStateConditionBase>(), Array.Empty<LocationStateEffectBase>());
            return state;
        }
    }
}
