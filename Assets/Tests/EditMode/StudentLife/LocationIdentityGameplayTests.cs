using System;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.StudentLife;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class LocationIdentityGameplayTests
    {
        private const string RegistryPath = "Assets/Data/Registry/GameDataRegistry.asset";

        [Test]
        public void LOCIDENT_EDIT_001_003_RegistryLoadsLocationIdentitiesWithDistinctActivityRoutes()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(RegistryPath);

            Assert.IsNotNull(registry, RegistryPath + " must exist.");
            Assert.GreaterOrEqual(registry.LocationIdentities.Length, 4, "LOCIDENT-EDIT-001 failed: location identity definitions must be registered.");

            int locationsWithActivitiesOrHints = 0;
            int distinctGrowthRoutes = 0;
            bool hasSchool = false;
            bool hasLibrary = false;
            bool hasWorkshop = false;
            bool hasSquare = false;

            for (int i = 0; i < registry.LocationIdentities.Length; i++)
            {
                var identity = registry.LocationIdentities[i];
                Assert.IsNotNull(identity, "LOCIDENT-EDIT-001 failed: registry contains null location identity.");
                Assert.IsNotNull(identity.Location, identity.Id + " must reference a location.");
                Assert.IsFalse(string.IsNullOrEmpty(identity.DescriptionKey), identity.Id + " must expose a location identity description.");
                Assert.Greater(identity.AvailableActivities.Count + identity.Hints.Count, 0, identity.Id + " must expose activities or hints.");
                StringAssert.StartsWith("Assets/Data/StudentLife/LocationIdentities/", AssetDatabase.GetAssetPath(identity));

                if (identity.AvailableActivities.Count + identity.Hints.Count > 0) locationsWithActivitiesOrHints++;
                if (identity.HasGrowthRoute(LocationGrowthRoute.FormalStudy)) distinctGrowthRoutes++;
                if (identity.HasGrowthRoute(LocationGrowthRoute.SelfStudy)) distinctGrowthRoutes++;
                if (identity.HasGrowthRoute(LocationGrowthRoute.Work)) distinctGrowthRoutes++;
                if (identity.HasGrowthRoute(LocationGrowthRoute.SocialHelp)) distinctGrowthRoutes++;

                hasSchool |= identity.Location.Id == "location.school" && identity.HasGrowthRoute(LocationGrowthRoute.FormalStudy);
                hasLibrary |= identity.Location.Id == "location.library" && identity.HasGrowthRoute(LocationGrowthRoute.SelfStudy);
                hasWorkshop |= identity.Location.Id == "location.workshop" && identity.HasGrowthRoute(LocationGrowthRoute.Work);
                hasSquare |= identity.Location.Id == "location.town-square" && identity.HasGrowthRoute(LocationGrowthRoute.SocialHelp);
            }

            Assert.GreaterOrEqual(locationsWithActivitiesOrHints, 4, "LOCIDENT-EDIT-002 failed: at least four locations need unique actions or hints.");
            Assert.GreaterOrEqual(distinctGrowthRoutes, 3, "LOCIDENT-EDIT-003 failed: at least three different growth routes must be represented.");
            Assert.IsTrue(hasSchool, "LOCIDENT-EDIT-003 failed: school must provide formal study growth.");
            Assert.IsTrue(hasLibrary, "LOCIDENT-EDIT-003 failed: library must provide self-study growth.");
            Assert.IsTrue(hasWorkshop, "LOCIDENT-EDIT-003 failed: workshop must provide work growth.");
            Assert.IsTrue(hasSquare, "LOCIDENT-EDIT-003 failed: town square must provide social/help growth.");
        }

        [Test]
        public void LOCIDENT_EDIT_004_006_LocationActivityStrategiesApplyOutcomesAndDedupeByRequestId()
        {
            var location = ScriptableObject.CreateInstance<LocationDefinition>();
            var trait = ScriptableObject.CreateInstance<TraitDefinition>();
            var skill = ScriptableObject.CreateInstance<SkillDefinition>();
            var relationship = ScriptableObject.CreateInstance<RelationshipDefinition>();
            var traitOutcome = ScriptableObject.CreateInstance<LocationActivityTraitDeltaOutcome>();
            var skillOutcome = ScriptableObject.CreateInstance<LocationActivitySkillDeltaOutcome>();
            var relationshipOutcome = ScriptableObject.CreateInstance<LocationActivityRelationshipDeltaOutcome>();
            var activity = ScriptableObject.CreateInstance<LocationActivityDefinition>();
            try
            {
                location.ConfigureForTests("location.library", "location.library.name", Vector2.zero, Array.Empty<DiscoveryDefinition>());
                trait.ConfigureForTests("trait.curiosity", "trait.curiosity.name");
                skill.ConfigureForTests("skill.research", "skill.research.name");
                relationship.ConfigureForTests("relationship.librarian", "relationship.librarian.name");
                traitOutcome.ConfigureForTests(trait, 2);
                skillOutcome.ConfigureForTests(skill, 3);
                relationshipOutcome.ConfigureForTests(relationship, 1);
                activity.ConfigureForTests(
                    "location.activity.library-hard-book",
                    "location.activity.library-hard-book.name",
                    location,
                    LocationGrowthRoute.SelfStudy,
                    40,
                    1,
                    2,
                    0,
                    Array.Empty<LocationActivityRequirementBase>(),
                    new LocationActivityOutcomeBase[] { traitOutcome, skillOutcome, relationshipOutcome });

                var progress = new StudentLifeProgress("slot", "player", 8, 8);
                var runner = new LocationActivityRunner();

                Assert.IsTrue(runner.TryPerform(activity, progress, "request-library-hard-book", out var result), "LOCIDENT-EDIT-005 failed: location activity should apply.");
                Assert.AreEqual(LifeActivityResultKind.Applied, result.Kind);
                Assert.AreEqual("location.library", result.LocationId);
                Assert.AreEqual(LocationGrowthRoute.SelfStudy, result.GrowthRoute);
                Assert.AreEqual(2, progress.GetTraitValue(trait));
                Assert.AreEqual(3, progress.GetSkillValue(skill));
                Assert.AreEqual(1, progress.GetRelationshipValue(relationship));
                CollectionAssert.Contains(progress.GetTodayActivityIds(), activity.Id);
                CollectionAssert.Contains(progress.GetTodayResultLogIds(), "location-activity:" + activity.Id + ":+trait.curiosity=2");
                CollectionAssert.Contains(progress.GetTodayResultLogIds(), "location-activity:" + activity.Id + ":+skill.research=3");
                CollectionAssert.Contains(progress.GetTodayResultLogIds(), "location-activity:" + activity.Id + ":+relationship.librarian=1");

                Assert.IsFalse(runner.TryPerform(activity, progress, "request-library-hard-book", out var duplicate), "LOCIDENT-EDIT-006 failed: same request id must not apply twice.");
                Assert.AreEqual(LifeActivityResultKind.DuplicateRequest, duplicate.Kind);
                Assert.AreEqual(2, progress.GetTraitValue(trait), "LOCIDENT-EDIT-006 failed: duplicate request changed trait again.");
                Assert.AreEqual(3, progress.GetSkillValue(skill), "LOCIDENT-EDIT-006 failed: duplicate request changed skill again.");
                Assert.AreEqual(1, progress.GetRelationshipValue(relationship), "LOCIDENT-EDIT-006 failed: duplicate request changed relationship again.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(activity);
                UnityEngine.Object.DestroyImmediate(relationshipOutcome);
                UnityEngine.Object.DestroyImmediate(skillOutcome);
                UnityEngine.Object.DestroyImmediate(traitOutcome);
                UnityEngine.Object.DestroyImmediate(relationship);
                UnityEngine.Object.DestroyImmediate(skill);
                UnityEngine.Object.DestroyImmediate(trait);
                UnityEngine.Object.DestroyImmediate(location);
            }
        }

        [Test]
        public void LOCIDENT_EDIT_007_LocationActivityLookupCacheAvoidsRepeatedRegistryScans()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(RegistryPath);
            var cache = new GameDataLookupCache(registry);

            Assert.IsTrue(cache.TryGetLocationIdentity("location.town-square", out var squareIdentity), "LOCIDENT-EDIT-007 failed: cache cannot resolve town square identity.");
            Assert.IsNotNull(squareIdentity);
            Assert.IsTrue(cache.TryGetLocationActivity("location.activity.square-help-request", out var squareActivity), "LOCIDENT-EDIT-007 failed: cache cannot resolve square location activity.");
            Assert.IsNotNull(squareActivity);
            Assert.AreEqual(1, cache.BuildCount, "LOCIDENT-EDIT-007 failed: cache rebuilt during location identity/activity lookup.");
        }

        [Test]
        public void LOCIDENT_EDIT_004_SourceDoesNotBranchByEntityIdsForLocationActivities()
        {
            string[] files =
            {
                "Assets/Scripts/Game/StudentLife/LocationIdentityDefinition.cs",
                "Assets/Scripts/Game/StudentLife/LocationActivityDefinition.cs",
                "Assets/Scripts/Game/StudentLife/LocationActivityRunner.cs",
                "Assets/Scripts/Game/StudentLife/LocationActivityOutcomes.cs",
                "Assets/Scripts/UI/StudentLife/LocationIdentityPanel.cs",
                "Assets/Scripts/UI/StudentLife/LocationNpcRuntimeInstaller.cs"
            };

            for (int i = 0; i < files.Length; i++)
            {
                Assert.IsTrue(File.Exists(files[i]), files[i] + " must exist.");
                string source = File.ReadAllText(files[i]);
                StringAssert.DoesNotContain("locationId ==", source, files[i] + " must not branch by location id.");
                StringAssert.DoesNotContain("activityId ==", source, files[i] + " must not branch by activity id.");
                StringAssert.DoesNotContain("switch (locationId", source, files[i] + " must not switch by location id.");
                StringAssert.DoesNotContain("switch (activityId", source, files[i] + " must not switch by activity id.");
            }
        }
    }
}
