using System;
using NUnit.Framework;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Tests
{
    // 옮겨 옴: Assets/Tests/EditMode/StudentLife/LocationIdentityGameplayTests.cs, TownLocationNpcFoundationTests.cs
    // (등록부·소스 문자열·씬 상호작용을 보는 테스트와 LocationActivityVisitQuestEventTests는 에셋·씬 검사라 옮기지 않음)
    public sealed class LocationActivityTests
    {
        private static LocationActivity Activity(string id, string locationId, string route, int time, int energy, int focus, ILocationActivityRequirement[] requirements, params ILocationActivityOutcome[] outcomes)
            => new LocationActivity(id, id + ".name", null, locationId, route, null, time, energy, focus, 0, requirements, outcomes);

        [Test]
        public void LOCIDENT_EDIT_004_006_LocationActivityStrategiesApplyOutcomesAndDedupeByRequestId()
        {
            var activity = Activity("location.activity.library-hard-book", "location.library", LocationGrowthRoutes.SelfStudy, 40, 1, 2, null,
                new LocationActivityTraitDeltaOutcome("trait.curiosity", 2), new LocationActivitySkillDeltaOutcome("skill.research", 3), new LocationActivityRelationshipDeltaOutcome("relationship.librarian", 1));
            var progress = new StudentLifeProgress("slot", "player", 8, 8);
            var runner = new LocationActivityRunner();

            Assert.IsTrue(runner.TryPerform(activity, progress, "request-library-hard-book", out var result));
            Assert.AreEqual(LifeActivityResultKind.Applied, result.Kind);
            Assert.AreEqual("location.library", result.LocationId);
            Assert.AreEqual(LocationGrowthRoutes.SelfStudy, result.GrowthRoute);
            Assert.AreEqual(2, progress.GetTraitValue("trait.curiosity"));
            Assert.AreEqual(3, progress.GetSkillValue("skill.research"));
            Assert.AreEqual(1, progress.GetRelationshipValue("relationship.librarian"));
            CollectionAssert.Contains(progress.GetTodayActivityIds(), activity.Id);
            CollectionAssert.Contains(progress.GetTodayResultLogIds(), "location-activity:" + activity.Id + ":+trait.curiosity=2");
            CollectionAssert.Contains(progress.GetTodayResultLogIds(), "location-activity:" + activity.Id + ":+skill.research=3");
            CollectionAssert.Contains(progress.GetTodayResultLogIds(), "location-activity:" + activity.Id + ":+relationship.librarian=1");

            Assert.IsFalse(runner.TryPerform(activity, progress, "request-library-hard-book", out var duplicate));
            Assert.AreEqual(LifeActivityResultKind.DuplicateRequest, duplicate.Kind);
            Assert.AreEqual(2, progress.GetTraitValue("trait.curiosity"));
            Assert.AreEqual(3, progress.GetSkillValue("skill.research"));
            Assert.AreEqual(1, progress.GetRelationshipValue("relationship.librarian"));
        }

        [Test]
        public void LOCNPC_EDIT_004_LocationVisitRulesUseStrategyArrays()
        {
            var location = new Location("location.school", "location.school.name", "location.school.desc", "location.category.school", "anchor.school",
                new[] { "npc.role.teacher" }, null, null, null);

            Assert.AreEqual("location.category.school", location.CategoryId);
            CollectionAssert.Contains((System.Collections.ICollection)location.RelatedRoleIds, "npc.role.teacher");
            Assert.IsTrue(location.CanVisit(new StudentLifeProgress("slot", "player", 8, 8)));
            Assert.IsFalse(location.CanVisit(null));
            Assert.IsFalse(new Location("l", "n", null, null, null, null, null, null, new ILocationVisitRule[] { new NeverVisit() }).CanVisit(new StudentLifeProgress("s", "p", 1, 1)));
        }

        [Test]
        public void LOCNPC_EDIT_005_006_LocationAndNpcProgressPersistsPerSaveSlotPlayerAndDedupe()
        {
            var progress = new StudentLifeProgress("slot-a", "player-a", 8, 8);
            var visitProgress = new LocationVisitProgress(progress);

            Assert.IsTrue(visitProgress.TryRecordLocationVisit("location.square", out var firstLocation));
            Assert.IsTrue(visitProgress.TryRecordNpcMeeting("npc.guide", out var firstNpc));
            Assert.IsFalse(visitProgress.TryRecordLocationVisit("location.square", out var duplicateLocation));
            Assert.IsFalse(visitProgress.TryRecordNpcMeeting("npc.guide", out var duplicateNpc));

            var restored = StudentLifeProgress.FromSaveData(progress.ToSaveData());
            var restoredVisits = new LocationVisitProgress(restored);

            Assert.AreEqual(LocationVisitResultKind.FirstVisit, firstLocation.Kind);
            Assert.AreEqual(LocationVisitResultKind.FirstMeeting, firstNpc.Kind);
            Assert.AreEqual(LocationVisitResultKind.DuplicateVisit, duplicateLocation.Kind);
            Assert.AreEqual(LocationVisitResultKind.DuplicateMeeting, duplicateNpc.Kind);
            Assert.IsTrue(restoredVisits.HasVisited("location.square"));
            Assert.IsTrue(restoredVisits.HasMet("npc.guide"));
            Assert.AreEqual(1, restoredVisits.VisitedLocationIds.Length);
            Assert.AreEqual(1, restoredVisits.MetNpcIds.Length);
            CollectionAssert.Contains(restored.GetActivityLogIds(), "location.square");
            CollectionAssert.Contains(restored.GetActivityLogIds(), "npc.guide");
        }

        // 더한 경계

        private sealed class NeverVisit : ILocationVisitRule { public bool IsSatisfied(StudentLifeProgress progress) => false; }
        private sealed class Never : ILocationActivityRequirement { public bool IsSatisfied(StudentLifeProgress progress, LocationActivity activity) => false; }

        [Test]
        public void RejectionsChangeNothing()
        {
            var runner = new LocationActivityRunner();
            var progress = new StudentLifeProgress("slot", "player", 1, 1, 0, 8 * 60);
            var costly = Activity("a.costly", "l", LocationGrowthRoutes.Work, 30, 5, 0, null, new LocationActivityTraitDeltaOutcome("t", 1));
            var locked = Activity("a.locked", "l", LocationGrowthRoutes.Work, 30, 0, 0, new ILocationActivityRequirement[] { new Never() }, new LocationActivityTraitDeltaOutcome("t", 1));

            Assert.IsFalse(runner.TryPerform(costly, progress, "r1", out var poor));
            Assert.AreEqual(LifeActivityResultKind.InsufficientResources, poor.Kind);
            Assert.IsFalse(runner.TryPerform(locked, progress, "r1", out var blocked));
            Assert.AreEqual(LifeActivityResultKind.RequirementFailed, blocked.Kind);
            Assert.IsFalse(runner.TryPerform(costly, progress, "", out var empty));
            Assert.AreEqual(LifeActivityResultKind.InvalidRequest, empty.Kind);
            Assert.IsFalse(runner.TryPerform(null, progress, "r1", out var none));
            Assert.AreEqual(LocationGrowthRoutes.Exploration, none.GrowthRoute);

            Assert.AreEqual(1, progress.Energy);
            Assert.AreEqual(8 * 60, progress.TimeMinutes);
            Assert.AreEqual(0, progress.GetTraitValue("t"));
            Assert.IsFalse(progress.HasAppliedRequest("r1"));
            Assert.IsEmpty(progress.GetActivityLogIds());
        }

        [Test]
        public void DuplicateRequestSurvivesSaveRoundTrip()
        {
            var activity = Activity("a", "l", LocationGrowthRoutes.Rest, 0, 0, 0, null, new LocationActivityStatusDeltaOutcome("status.calm", -2));
            var progress = new StudentLifeProgress("slot", "player", 8, 8);
            Assert.IsTrue(new LocationActivityRunner().TryPerform(activity, progress, "r", out var applied));
            CollectionAssert.AreEqual(new[] { "location-activity:a:status.calm=-2" }, applied.OutcomeLogIds);

            var restored = StudentLifeProgress.FromSaveData(progress.ToSaveData());
            Assert.IsFalse(new LocationActivityRunner().TryPerform(activity, restored, "r", out var duplicate));
            Assert.AreEqual(LifeActivityResultKind.DuplicateRequest, duplicate.Kind);
            Assert.AreEqual(0, restored.GetStatusValue("status.calm")); // 진행 상태 값은 0 밑으로 내려가지 않는다.
        }

        [Test]
        public void CareerHintLogsFirstUnlockUnlessLoggingEveryTime()
        {
            var progress = new StudentLifeProgress("slot", "player", 8, 8);
            var activity = Activity("a", "l", LocationGrowthRoutes.Work, 0, 0, 0, null);
            var once = new LocationActivityCareerHintOutcome("career.cook", false);
            var every = new LocationActivityCareerHintOutcome("career.cook", true);

            Assert.AreEqual("location-activity:a:unlock=career.cook", once.Apply(progress, activity));
            Assert.AreEqual(string.Empty, once.Apply(progress, activity));
            Assert.AreEqual("location-activity:a:unlock=career.cook", every.Apply(progress, activity));
            Assert.AreEqual(string.Empty, new LocationActivityTraitDeltaOutcome("t", 0).Apply(progress, activity));
            Assert.AreEqual(string.Empty, new LocationActivityTraitDeltaOutcome("", 3).Apply(progress, activity));
        }

        [Test]
        public void RecipeRevealGoesThroughInterfaceAndLogsOnlyWhenChanged()
        {
            var reveals = new Reveals();
            var outcome = new LocationActivityResourceRecipeOutcome("resource.clay", "recipe.cup", null, reveals);
            var progress = new StudentLifeProgress("slot", "player", 8, 8);
            var activity = Activity("a", "l", LocationGrowthRoutes.Work, 0, 0, 0, null);

            Assert.AreEqual("objective.resourceRecipe.next", outcome.NextHintKey);
            Assert.AreEqual("location-activity:a:unlock=recipe.cup", outcome.Apply(progress, activity));
            Assert.AreEqual(string.Empty, outcome.Apply(progress, activity));
            Assert.AreEqual("slot/player:resource.clay:recipe.cup:a", reveals.Last);
            Assert.AreEqual(string.Empty, new LocationActivityResourceRecipeOutcome("r", "", null, reveals).Apply(progress, activity));
        }

        private sealed class Reveals : ILocationActivityRecipeReveal
        {
            private readonly System.Collections.Generic.HashSet<string> _done = new System.Collections.Generic.HashSet<string>();
            public string Last;
            public bool TryReveal(StudentLifeProgress progress, string resourceId, string recipeId, string sourceActivityId)
            {
                Last = progress.SaveSlot + "/" + progress.PlayerId + ":" + resourceId + ":" + recipeId + ":" + sourceActivityId;
                return _done.Add(recipeId);
            }
        }

        [Test]
        public void GrowthSummaryParsesDeltaAndUnlockLogsAndSkipsOthers()
        {
            var summary = LocationActivityGrowthSummary.FromResultLogs(new[]
            {
                "location-activity:a:+skill.x=3", "location-activity:a:status.y=-1", "location-activity:b:unlock=career.z", "other:a:+t=1", "location-activity:bad", null,
            });

            Assert.AreEqual(3, summary.Entries.Length);
            Assert.AreEqual("skill.x", summary.Entries[0].TargetId);
            Assert.AreEqual(3, summary.Entries[0].Delta);
            Assert.AreEqual(-1, summary.Entries[1].Delta);
            Assert.IsTrue(summary.Entries[2].IsUnlock);
            Assert.AreEqual("b", summary.Entries[2].ActivityId);
            Assert.AreEqual(string.Empty, LocationActivityLogCodec.EncodeDelta("a", "t", 0));
        }

        [Test]
        public void IdentityReportsGrowthRoutesFromActivitiesAndHintsAndClampsVisitWindow()
        {
            var work = Activity("a.work", "location.workshop", LocationGrowthRoutes.Work, 0, 0, 0, null);
            var identity = new LocationIdentity("identity.workshop", "identity.workshop.name", null, "location.workshop", new[] { work },
                new[] { new LocationHintEntry("hint.rest", LocationGrowthRoutes.Rest, "milestone.rest") }, 20 * 60, 9 * 60);

            Assert.IsTrue(identity.HasGrowthRoute(LocationGrowthRoutes.Work));
            Assert.IsTrue(identity.HasGrowthRoute(LocationGrowthRoutes.Rest));
            Assert.IsFalse(identity.HasGrowthRoute(LocationGrowthRoutes.Commerce));
            Assert.AreEqual("identity.workshop.name", identity.DescriptionKey);
            Assert.AreEqual(20 * 60, identity.RecommendedVisitStartMinute);
            Assert.AreEqual(20 * 60, identity.RecommendedVisitEndMinute);
            Assert.AreEqual("milestone.rest", identity.Hints[0].MilestoneId);

            IDailyPlanLocationActivity forPlan = work;
            Assert.AreEqual("location.workshop", forPlan.LocationId);
            Assert.AreEqual("Work", forPlan.GrowthRoute);
        }

        [Test]
        public void VisitWithEmptyIdIsInvalidAndVisitedListSkipsSuffixedIds()
        {
            var progress = new StudentLifeProgress("slot", "player", 8, 8);
            var visits = new LocationVisitProgress(progress);
            progress.RecordActivityCompleted("location.cafe@evening");

            Assert.IsFalse(visits.TryRecordLocationVisit("", out var invalid));
            Assert.AreEqual(LocationVisitResultKind.InvalidRequest, invalid.Kind);
            Assert.IsFalse(new LocationVisitProgress(null).TryRecordNpcMeeting("npc.a", out var noProgress));
            Assert.AreEqual("default", noProgress.SaveSlot);
            Assert.IsEmpty(visits.VisitedLocationIds);
            Assert.IsTrue(visits.TryRecordLocationVisit("location.square", out _));
            CollectionAssert.Contains(progress.GetTodayResultLogIds(), "location:location.square:visited");
            CollectionAssert.AreEqual(new[] { "location.square" }, visits.VisitedLocationIds);
        }
    }
}
