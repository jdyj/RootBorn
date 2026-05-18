using System;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class CareerCandidateDeepeningLoopTests
    {
        [Test]
        public void CAREER_CANDIDATE_EDIT_001_RegistryExposesCandidateHintAndRouteDefinitions()
        {
            AssertTypeExists("Rootborn.Game.StudentLife.CareerCandidateDefinition, Rootborn.Game");
            AssertTypeExists("Rootborn.Game.StudentLife.CareerHintDefinition, Rootborn.Game");
            AssertTypeExists("Rootborn.Game.StudentLife.CareerCandidateRouteDefinition, Rootborn.Game");
            AssertTypeExists("Rootborn.Game.StudentLife.CareerCandidateRequirementBase, Rootborn.Game");
            AssertTypeExists("Rootborn.Game.StudentLife.CareerHintSourceBase, Rootborn.Game");
            AssertTypeExists("Rootborn.Game.StudentLife.CareerCandidateRewardBase, Rootborn.Game");
            AssertTypeExists("Rootborn.Game.StudentLife.CareerCandidateProgress, Rootborn.Game");
            AssertRegistryProperty("CareerCandidates", typeof(CareerCandidateDefinition[]));
            AssertRegistryProperty("CareerHints", typeof(CareerHintDefinition[]));
            AssertRegistryProperty("CareerCandidateRoutes", typeof(CareerCandidateRouteDefinition[]));
        }

        [Test]
        public void CAREER_CANDIDATE_EDIT_002_FourCandidatesCanExistAsDataDrivenScriptableObjects()
        {
            var set = CareerCandidateTestSet.Create();
            Assert.AreEqual(4, set.Candidates.Length);
            CollectionAssert.AreEquivalent(new[] { "candidate.learning", "candidate.technical", "candidate.service", "candidate.investigation" }, Array.ConvertAll(set.Candidates, candidate => candidate.Id));
            for (int i = 0; i < set.Candidates.Length; i++)
            {
                Assert.IsInstanceOf<ScriptableObject>(set.Candidates[i]);
                Assert.GreaterOrEqual(set.Candidates[i].Routes.Count, 1);
                Assert.GreaterOrEqual(set.Candidates[i].RecommendedActionKeys.Count, 1);
            }
        }

        [Test]
        public void CAREER_CANDIDATE_EDIT_004_AtLeastOneCandidateAcceptsTwoAlternativeRoutes()
        {
            var set = CareerCandidateTestSet.Create();
            Assert.GreaterOrEqual(set.Learning.Routes.Count, 2);
            Assert.AreNotSame(set.Learning.Routes[0], set.Learning.Routes[1]);
        }

        [Test]
        public void CAREER_CANDIDATE_EDIT_005_OutsideSchoolActivityLogCanIncreaseCandidateHintWithoutClassRoute()
        {
            var set = CareerCandidateTestSet.Create();
            var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            var candidates = new CareerCandidateProgress(student.SaveSlot, student.PlayerId);
            student.RecordActivityCompleted(set.LibraryActivity.Id, new[] { set.LibraryActivity.Id + ":unlock=career.researcher" });
            var result = candidates.ApplyHint(set.LibraryHint, student, "source.library.day1", student.GetTodayResultLogIds());

            Assert.IsTrue(result.Applied);
            Assert.AreEqual("candidate.learning", result.CandidateId);
            Assert.AreEqual(1, candidates.GetHintCount(set.Learning));
            Assert.AreEqual(CareerCandidateState.Hinted, set.Learning.GetState(candidates));
        }

        [Test]
        public void CAREER_CANDIDATE_EDIT_006_CandidateProgressPersistsBySaveSlotAndPlayerIdentity()
        {
            var set = CareerCandidateTestSet.Create();
            var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            var candidates = new CareerCandidateProgress(student.SaveSlot, student.PlayerId);
            student.RecordActivityCompleted(set.LibraryActivity.Id, new[] { "library unlock" });
            candidates.ApplyHint(set.LibraryHint, student, "source.library.day1", student.GetTodayResultLogIds());

            var save = candidates.ToSaveData();
            var restored = CareerCandidateProgress.FromSaveData(save);
            var otherPlayer = new CareerCandidateProgress("slot-a", "player-b");

            Assert.AreEqual("slot-a", restored.SaveSlot);
            Assert.AreEqual("player-a", restored.PlayerId);
            Assert.AreEqual(1, restored.GetHintCount(set.Learning));
            Assert.AreEqual(0, otherPlayer.GetHintCount(set.Learning));
        }

        [Test]
        public void CAREER_CANDIDATE_EDIT_007_SameHintSourceDoesNotApplyProgressOrRewardTwice()
        {
            var set = CareerCandidateTestSet.Create();
            var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            var candidates = new CareerCandidateProgress(student.SaveSlot, student.PlayerId);
            student.RecordActivityCompleted(set.LibraryActivity.Id, new[] { "library unlock" });

            var first = candidates.ApplyHint(set.LibraryHint, student, "source.library.day1", student.GetTodayResultLogIds());
            var duplicate = candidates.ApplyHint(set.LibraryHint, student, "source.library.day1", student.GetTodayResultLogIds());

            Assert.IsTrue(first.Applied);
            Assert.IsFalse(duplicate.Applied);
            Assert.AreEqual(1, candidates.GetHintCount(set.Learning));
            Assert.AreEqual(1, candidates.GetUnderstandingScore(set.Learning));
        }

        [Test]
        public void CAREER_CANDIDATE_EDIT_008_SummaryShowsLockedPartialUnlockedAndRecommendedActions()
        {
            var set = CareerCandidateTestSet.Create();
            var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            var candidates = new CareerCandidateProgress(student.SaveSlot, student.PlayerId);
            var locked = CareerCandidateSummaryBuilder.Build(set.Technical, candidates, student);
            student.RecordActivityCompleted(set.LibraryActivity.Id, new[] { "library unlock" });
            candidates.ApplyHint(set.LibraryHint, student, "source.library.day1", student.GetTodayResultLogIds());
            var hinted = CareerCandidateSummaryBuilder.Build(set.Learning, candidates, student);
            candidates.ApplyHint(set.ClassHint, student, "source.class.day1", new[] { "class unlock" });
            var revealed = CareerCandidateSummaryBuilder.Build(set.Learning, candidates, student);

            Assert.AreEqual("???", locked.DisplayName);
            Assert.AreEqual(CareerCandidateState.Hinted, hinted.State);
            Assert.AreEqual(CareerCandidateState.Revealed, revealed.State);
            CollectionAssert.Contains(revealed.RelatedLocationKeys, set.Library.DisplayNameKey);
            CollectionAssert.Contains(revealed.RelatedActivityKeys, set.LibraryActivity.DisplayNameKey);
            CollectionAssert.Contains(revealed.RecommendedActionKeys, "action.visit-library");
        }

        [Test]
        public void CAREER_CANDIDATE_EDIT_009_LookupCacheIndexesCandidateHintAndRouteWithoutRebuild()
        {
            var set = CareerCandidateTestSet.Create();
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();
            RegistryTestBinder.Set(registry, "_careerCandidates", set.Candidates);
            RegistryTestBinder.Set(registry, "_careerHints", set.Hints);
            RegistryTestBinder.Set(registry, "_careerCandidateRoutes", set.Routes);

            var cache = new GameDataLookupCache(registry);

            Assert.AreEqual(1, cache.BuildCount);
            Assert.IsTrue(cache.TryGetCareerCandidate("candidate.learning", out var candidate));
            Assert.AreSame(set.Learning, candidate);
            Assert.IsTrue(cache.TryGetCareerHint("hint.learning.library", out var hint));
            Assert.AreSame(set.LibraryHint, hint);
            Assert.IsTrue(cache.TryGetCareerCandidateRoute("route.library", out var route));
            Assert.AreSame(set.LibraryRoute, route);
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

        private static LifeActivityDefinition CreateActivity(string id)
        {
            var activity = ScriptableObject.CreateInstance<LifeActivityDefinition>();
            activity.ConfigureForTests(id, id, LifeActivityCategory.Hobby, 0, 0, 0, 0, null, null, null);
            return activity;
        }

        private static LocationDefinition CreateLocation(string id)
        {
            var location = ScriptableObject.CreateInstance<LocationDefinition>();
            location.ConfigureForTests(id, id, Vector2.zero, null);
            return location;
        }

        private static CareerCandidateRouteDefinition CreateRoute(string id, LocationDefinition location, LifeActivityDefinition activity, string recommended)
        {
            var route = ScriptableObject.CreateInstance<CareerCandidateRouteDefinition>();
            route.ConfigureForTests(id, id, "desc." + id, new[] { location }, new[] { activity }, new[] { recommended });
            return route;
        }

        private static CareerActivityLogHintSource CreateActivitySource(LifeActivityDefinition activity)
        {
            var source = ScriptableObject.CreateInstance<CareerActivityLogHintSource>();
            source.ConfigureForTests(activity);
            return source;
        }

        private static CareerResultLogHintSource CreateResultSource(string text)
        {
            var source = ScriptableObject.CreateInstance<CareerResultLogHintSource>();
            source.ConfigureForTests(text);
            return source;
        }

        private sealed class CareerCandidateTestSet
        {
            public CareerCandidateDefinition Learning;
            public CareerCandidateDefinition Technical;
            public CareerCandidateDefinition Service;
            public CareerCandidateDefinition Investigation;
            public CareerCandidateDefinition[] Candidates;
            public CareerHintDefinition LibraryHint;
            public CareerHintDefinition ClassHint;
            public CareerHintDefinition WorkHint;
            public CareerHintDefinition ServiceHint;
            public CareerHintDefinition InvestigationHint;
            public CareerHintDefinition[] Hints;
            public CareerCandidateRouteDefinition LibraryRoute;
            public CareerCandidateRouteDefinition ClassRoute;
            public CareerCandidateRouteDefinition WorkRoute;
            public CareerCandidateRouteDefinition ServiceRoute;
            public CareerCandidateRouteDefinition InvestigationRoute;
            public CareerCandidateRouteDefinition[] Routes;
            public LocationDefinition Library;
            public LocationDefinition School;
            public LocationDefinition Workshop;
            public LocationDefinition Plaza;
            public LocationDefinition Alley;
            public LifeActivityDefinition LibraryActivity;
            public LifeActivityDefinition ClassActivity;
            public LifeActivityDefinition WorkActivity;
            public LifeActivityDefinition ServiceActivity;
            public LifeActivityDefinition InvestigationActivity;

            public static CareerCandidateTestSet Create()
            {
                var set = new CareerCandidateTestSet();
                set.Library = CreateLocation("location.library");
                set.School = CreateLocation("location.school");
                set.Workshop = CreateLocation("location.workshop");
                set.Plaza = CreateLocation("location.plaza");
                set.Alley = CreateLocation("location.alley");
                set.LibraryActivity = CreateActivity("activity.library.self-study");
                set.ClassActivity = CreateActivity("activity.school.class");
                set.WorkActivity = CreateActivity("activity.workshop.part-time");
                set.ServiceActivity = CreateActivity("activity.plaza.help");
                set.InvestigationActivity = CreateActivity("activity.alley.investigate");
                set.LibraryRoute = CreateRoute("route.library", set.Library, set.LibraryActivity, "action.visit-library");
                set.ClassRoute = CreateRoute("route.class", set.School, set.ClassActivity, "action.attend-class");
                set.WorkRoute = CreateRoute("route.work", set.Workshop, set.WorkActivity, "action.try-workshop");
                set.ServiceRoute = CreateRoute("route.service", set.Plaza, set.ServiceActivity, "action.help-npc");
                set.InvestigationRoute = CreateRoute("route.investigation", set.Alley, set.InvestigationActivity, "action.investigate-alley");
                set.Learning = CreateCandidate("candidate.learning", new[] { set.ClassRoute, set.LibraryRoute }, new[] { set.School, set.Library }, new[] { set.ClassActivity, set.LibraryActivity }, "action.visit-library");
                set.Technical = CreateCandidate("candidate.technical", new[] { set.WorkRoute }, new[] { set.Workshop }, new[] { set.WorkActivity }, "action.try-workshop");
                set.Service = CreateCandidate("candidate.service", new[] { set.ServiceRoute }, new[] { set.Plaza }, new[] { set.ServiceActivity }, "action.help-npc");
                set.Investigation = CreateCandidate("candidate.investigation", new[] { set.InvestigationRoute }, new[] { set.Alley }, new[] { set.InvestigationActivity }, "action.investigate-alley");
                set.LibraryHint = CreateHint("hint.learning.library", set.Learning, set.LibraryRoute, new CareerHintSourceBase[] { CreateActivitySource(set.LibraryActivity), CreateResultSource("library") });
                set.ClassHint = CreateHint("hint.learning.class", set.Learning, set.ClassRoute, new CareerHintSourceBase[] { CreateResultSource("class") });
                set.WorkHint = CreateHint("hint.technical.work", set.Technical, set.WorkRoute, new CareerHintSourceBase[] { CreateActivitySource(set.WorkActivity) });
                set.ServiceHint = CreateHint("hint.service.help", set.Service, set.ServiceRoute, new CareerHintSourceBase[] { CreateActivitySource(set.ServiceActivity) });
                set.InvestigationHint = CreateHint("hint.investigation.alley", set.Investigation, set.InvestigationRoute, new CareerHintSourceBase[] { CreateActivitySource(set.InvestigationActivity) });
                set.Candidates = new[] { set.Learning, set.Technical, set.Service, set.Investigation };
                set.Hints = new[] { set.LibraryHint, set.ClassHint, set.WorkHint, set.ServiceHint, set.InvestigationHint };
                set.Routes = new[] { set.LibraryRoute, set.ClassRoute, set.WorkRoute, set.ServiceRoute, set.InvestigationRoute };
                return set;
            }

            private static CareerCandidateDefinition CreateCandidate(string id, CareerCandidateRouteDefinition[] routes, LocationDefinition[] locations, LifeActivityDefinition[] activities, string recommended)
            {
                var candidate = ScriptableObject.CreateInstance<CareerCandidateDefinition>();
                candidate.ConfigureForTests(id, id, "desc." + id, routes, Array.Empty<CareerCandidateRequirementBase>(), new[] { recommended }, locations, activities);
                return candidate;
            }

            private static CareerHintDefinition CreateHint(string id, CareerCandidateDefinition candidate, CareerCandidateRouteDefinition route, CareerHintSourceBase[] sources)
            {
                var hint = ScriptableObject.CreateInstance<CareerHintDefinition>();
                hint.ConfigureForTests(id, id, candidate, route, "insight." + id, 1, sources);
                return hint;
            }
        }

        private static class RegistryTestBinder
        {
            public static void Set<T>(GameDataRegistry registry, string fieldName, T value)
            {
                var field = typeof(GameDataRegistry).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(field, fieldName + " field is missing from GameDataRegistry");
                field.SetValue(registry, value);
            }
        }
    }
}
