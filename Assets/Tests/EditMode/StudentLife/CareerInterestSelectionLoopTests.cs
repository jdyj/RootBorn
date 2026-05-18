using System;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class CareerInterestSelectionLoopTests
    {
        [Test]
        public void CAREER_INTEREST_EDIT_001_RegistryExposesInterestConditionRecommendationAndRewardDefinitions()
        {
            AssertTypeExists("Rootborn.Game.StudentLife.CareerInterestDefinition, Rootborn.Game");
            AssertTypeExists("Rootborn.Game.StudentLife.CareerInterestUnlockRequirementBase, Rootborn.Game");
            AssertTypeExists("Rootborn.Game.StudentLife.CareerInterestSelectionRuleBase, Rootborn.Game");
            AssertTypeExists("Rootborn.Game.StudentLife.CareerInterestRecommendationBase, Rootborn.Game");
            AssertTypeExists("Rootborn.Game.StudentLife.CareerInterestRewardBase, Rootborn.Game");
            AssertTypeExists("Rootborn.Game.StudentLife.CareerInterestProgress, Rootborn.Game");
            AssertRegistryProperty("CareerInterests", typeof(CareerInterestDefinition[]));
        }

        [Test]
        public void CAREER_INTEREST_EDIT_002_OnlyRevealedCandidatesCanBeSelectedByDefault()
        {
            var set = CareerInterestTestSet.Create();
            var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            var candidates = new CareerCandidateProgress(student.SaveSlot, student.PlayerId);
            var interests = new CareerInterestProgress(student.SaveSlot, student.PlayerId);

            var locked = interests.TrySelect(set.LearningInterest, candidates, student, 1, "select.learning.locked");
            candidates.ApplyHint(set.LibraryHint, student, "source.library.1", new[] { "library" });
            candidates.ApplyHint(set.ClassHint, student, "source.class.1", new[] { "class" });
            var revealed = interests.TrySelect(set.LearningInterest, candidates, student, 1, "select.learning.revealed");

            Assert.IsFalse(locked.Applied, "CAREER-INTEREST-EDIT-002 failed: locked or hinted candidates must not be selectable.");
            Assert.IsTrue(revealed.Applied, "CAREER-INTEREST-EDIT-002 failed: revealed candidate should be selectable.");
            Assert.AreEqual("interest.learning", interests.CurrentInterestId);
        }

        [Test]
        public void CAREER_INTEREST_EDIT_004_FirstSelectionPersistsCurrentInterestHistoryAndRewardClaimFlags()
        {
            var set = CareerInterestTestSet.Create();
            var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            var candidates = Revealed(set, student);
            var interests = new CareerInterestProgress(student.SaveSlot, student.PlayerId);

            var first = interests.TrySelect(set.LearningInterest, candidates, student, 1, "select.learning.day1");
            var duplicate = interests.TrySelect(set.LearningInterest, candidates, student, 1, "select.learning.day1.again");
            var restored = CareerInterestProgress.FromSaveData(interests.ToSaveData());

            Assert.IsTrue(first.Applied);
            Assert.IsFalse(duplicate.Applied, "CAREER-INTEREST-EDIT-005 failed: same interest selection must not duplicate rewards/logs.");
            Assert.AreEqual("interest.learning", restored.CurrentInterestId);
            CollectionAssert.Contains(restored.SelectionHistoryIds, "interest.learning");
            Assert.IsTrue(restored.HasClaimedReward("interest.learning", "reward.learning.focus"));
        }

        [Test]
        public void CAREER_INTEREST_EDIT_006_ChangePolicyCanRequireOneChangePerDay()
        {
            var set = CareerInterestTestSet.Create();
            var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            var candidates = Revealed(set, student);
            RevealTechnical(set, candidates, student);
            var interests = new CareerInterestProgress(student.SaveSlot, student.PlayerId);

            Assert.IsTrue(interests.TrySelect(set.LearningInterest, candidates, student, 1, "select.learning.day1").Applied);
            var changed = interests.TrySelect(set.TechnicalInterest, candidates, student, 2, "select.technical.day2");
            var blocked = interests.TrySelect(set.LearningInterest, candidates, student, 2, "select.learning.day2.return");

            Assert.IsTrue(changed.Applied);
            Assert.IsFalse(blocked.Applied, "CAREER-INTEREST-EDIT-006 failed: data-driven change rule should block a second same-day change.");
            Assert.AreEqual(CareerInterestSelectionState.ChangedToday, blocked.State);
            Assert.IsNotEmpty(blocked.Reason);
        }

        [Test]
        public void CAREER_INTEREST_EDIT_008_RecommendationsAndSummaryComeFromSelectedInterestData()
        {
            var set = CareerInterestTestSet.Create();
            var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            var candidates = Revealed(set, student);
            var interests = new CareerInterestProgress(student.SaveSlot, student.PlayerId);
            interests.TrySelect(set.LearningInterest, candidates, student, 1, "select.learning.day1");

            var summary = CareerInterestSummaryBuilder.Build(set.LearningInterest, interests, candidates, student, 1);

            Assert.AreEqual(CareerInterestSelectionState.Selected, summary.State);
            CollectionAssert.Contains(summary.RecommendedActionKeys, "action.visit-library");
            CollectionAssert.Contains(summary.RelatedLocationKeys, set.Library.DisplayNameKey);
            CollectionAssert.Contains(summary.RelatedActivityKeys, set.LibraryActivity.DisplayNameKey);
        }

        [Test]
        public void CAREER_INTEREST_EDIT_010_LookupCacheIndexesInterestsWithoutRebuild()
        {
            var set = CareerInterestTestSet.Create();
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();
            RegistryTestBinder.Set(registry, "_careerInterests", set.Interests);

            var cache = new GameDataLookupCache(registry);

            Assert.AreEqual(1, cache.BuildCount);
            Assert.IsTrue(cache.TryGetCareerInterest("interest.learning", out var interest));
            Assert.AreSame(set.LearningInterest, interest);
            Assert.AreEqual(1, cache.BuildCount);
            UnityEngine.Object.DestroyImmediate(registry);
        }

        private static void AssertTypeExists(string assemblyQualifiedName)
        {
            Assert.IsNotNull(Type.GetType(assemblyQualifiedName), assemblyQualifiedName + " is missing");
        }

        private static void AssertRegistryProperty(string name, Type expectedType)
        {
            var property = typeof(GameDataRegistry).GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(property, name + " property is missing from GameDataRegistry");
            Assert.AreEqual(expectedType, property.PropertyType);
        }

        private static CareerCandidateProgress Revealed(CareerInterestTestSet set, StudentLifeProgress student)
        {
            var candidates = new CareerCandidateProgress(student.SaveSlot, student.PlayerId);
            candidates.ApplyHint(set.LibraryHint, student, "source.library.1", new[] { "library" });
            candidates.ApplyHint(set.ClassHint, student, "source.class.1", new[] { "class" });
            return candidates;
        }

        private static void RevealTechnical(CareerInterestTestSet set, CareerCandidateProgress candidates, StudentLifeProgress student)
        {
            candidates.ApplyHint(set.WorkHint, student, "source.work.1", new[] { "work" });
            candidates.ApplyHint(set.ShopHint, student, "source.shop.1", new[] { "shop" });
        }

        private sealed class CareerInterestTestSet
        {
            public CareerCandidateDefinition Learning;
            public CareerCandidateDefinition Technical;
            public CareerInterestDefinition LearningInterest;
            public CareerInterestDefinition TechnicalInterest;
            public CareerInterestDefinition[] Interests;
            public CareerHintDefinition LibraryHint;
            public CareerHintDefinition ClassHint;
            public CareerHintDefinition WorkHint;
            public CareerHintDefinition ShopHint;
            public LocationDefinition Library;
            public LocationDefinition Workshop;
            public LifeActivityDefinition LibraryActivity;
            public LifeActivityDefinition WorkActivity;

            public static CareerInterestTestSet Create()
            {
                var set = new CareerInterestTestSet();
                set.Library = CreateLocation("location.library");
                set.Workshop = CreateLocation("location.workshop");
                set.LibraryActivity = CreateActivity("activity.library.study");
                set.WorkActivity = CreateActivity("activity.workshop.practice");
                var libraryRoute = CreateRoute("route.library", set.Library, set.LibraryActivity, "action.visit-library");
                var workRoute = CreateRoute("route.workshop", set.Workshop, set.WorkActivity, "action.practice-workshop");
                set.Learning = CreateCandidate("candidate.learning", libraryRoute, set.Library, set.LibraryActivity, "action.visit-library");
                set.Technical = CreateCandidate("candidate.technical", workRoute, set.Workshop, set.WorkActivity, "action.practice-workshop");
                set.LibraryHint = CreateHint("hint.learning.library", set.Learning, libraryRoute);
                set.ClassHint = CreateHint("hint.learning.class", set.Learning, libraryRoute);
                set.WorkHint = CreateHint("hint.technical.work", set.Technical, workRoute);
                set.ShopHint = CreateHint("hint.technical.shop", set.Technical, workRoute);
                set.LearningInterest = CreateInterest("interest.learning", set.Learning, new[] { set.Library }, new[] { set.LibraryActivity }, new[] { "action.visit-library" }, "reward.learning.focus");
                set.TechnicalInterest = CreateInterest("interest.technical", set.Technical, new[] { set.Workshop }, new[] { set.WorkActivity }, new[] { "action.practice-workshop" }, "reward.technical.focus");
                set.Interests = new[] { set.LearningInterest, set.TechnicalInterest };
                return set;
            }

            private static LocationDefinition CreateLocation(string id)
            {
                var location = ScriptableObject.CreateInstance<LocationDefinition>();
                location.ConfigureForTests(id, id, Vector2.zero, null);
                return location;
            }

            private static LifeActivityDefinition CreateActivity(string id)
            {
                var activity = ScriptableObject.CreateInstance<LifeActivityDefinition>();
                activity.ConfigureForTests(id, id, LifeActivityCategory.Hobby, 0, 0, 0, 0, null, null, null);
                return activity;
            }

            private static CareerCandidateRouteDefinition CreateRoute(string id, LocationDefinition location, LifeActivityDefinition activity, string recommended)
            {
                var route = ScriptableObject.CreateInstance<CareerCandidateRouteDefinition>();
                route.ConfigureForTests(id, id, "desc." + id, new[] { location }, new[] { activity }, new[] { recommended });
                return route;
            }

            private static CareerCandidateDefinition CreateCandidate(string id, CareerCandidateRouteDefinition route, LocationDefinition location, LifeActivityDefinition activity, string recommended)
            {
                var candidate = ScriptableObject.CreateInstance<CareerCandidateDefinition>();
                candidate.ConfigureForTests(id, id, "desc." + id, new[] { route }, Array.Empty<CareerCandidateRequirementBase>(), new[] { recommended }, new[] { location }, new[] { activity });
                return candidate;
            }

            private static CareerHintDefinition CreateHint(string id, CareerCandidateDefinition candidate, CareerCandidateRouteDefinition route)
            {
                var source = ScriptableObject.CreateInstance<CareerResultLogHintSource>();
                source.ConfigureForTests(string.Empty);
                var hint = ScriptableObject.CreateInstance<CareerHintDefinition>();
                hint.ConfigureForTests(id, id, candidate, route, id + ".insight", 1, new CareerHintSourceBase[] { new AlwaysMatchingCareerHintSource() });
                return hint;
            }

            private static CareerInterestDefinition CreateInterest(string id, CareerCandidateDefinition candidate, LocationDefinition[] locations, LifeActivityDefinition[] activities, string[] recommended, string rewardId)
            {
                var requirement = ScriptableObject.CreateInstance<CareerInterestCandidateStateRequirement>();
                requirement.ConfigureForTests(CareerCandidateState.Revealed);
                var changeRule = ScriptableObject.CreateInstance<CareerInterestDailyChangeLimitRule>();
                changeRule.ConfigureForTests(1, true);
                var recommendation = ScriptableObject.CreateInstance<CareerInterestStaticRecommendation>();
                recommendation.ConfigureForTests(recommended);
                var reward = ScriptableObject.CreateInstance<CareerInterestFocusReward>();
                reward.ConfigureForTests(rewardId, 1);
                var interest = ScriptableObject.CreateInstance<CareerInterestDefinition>();
                interest.ConfigureForTests(id, id, "desc." + id, candidate, locations, activities, new CareerInterestUnlockRequirementBase[] { requirement }, new CareerInterestSelectionRuleBase[] { changeRule }, new CareerInterestRecommendationBase[] { recommendation }, new CareerInterestRewardBase[] { reward });
                return interest;
            }
        }

        private sealed class AlwaysMatchingCareerHintSource : CareerHintSourceBase
        {
            public override bool Matches(CareerCandidateEvaluationContext context) => true;
        }
    }
}
