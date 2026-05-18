using System;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.StudentLife;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class CampaignLoopTests
    {
        [Test]
        public void CAMPAIGN_EDIT_001_RegisteredFirstThreeDaysCampaignAssetLoadsFromGameDataRegistry()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            Assert.IsNotNull(registry, "CAMPAIGN-EDIT-001 failed: GameDataRegistry asset was not found.");
            Assert.IsNotNull(registry.Campaigns, "CAMPAIGN-EDIT-001 failed: campaign registry array is null.");
            Assert.GreaterOrEqual(registry.Campaigns.Length, 1, "CAMPAIGN-EDIT-001 failed: no CampaignDefinition is registered.");

            var cache = new GameDataLookupCache(registry);
            Assert.IsTrue(cache.TryGetCampaign("campaign.first-three-days", out var campaign), "CAMPAIGN-EDIT-001 failed: first three days campaign was not loaded from registered data.");
            Assert.AreEqual(3, campaign.Days.Length, "CAMPAIGN-EDIT-001 failed: first three days campaign should expose three day definitions.");
            for (int i = 0; i < campaign.Days.Length; i++) Assert.GreaterOrEqual(campaign.Days[i].Routes.Length, 2, "CAMPAIGN-EDIT-002 failed: each campaign day should expose at least two route choices.");
            Assert.IsTrue(campaign.GetDayByNumber(2).HasSchoolFreeCompletionRoute(), "CAMPAIGN-EDIT-003 failed: day two should be completable without a school class route.");
            Assert.IsTrue(cache.TryGetCampaignDay("campaign.day.future", out _), "CAMPAIGN-EDIT-001 failed: registered campaign day was not cached.");
            Assert.IsTrue(cache.TryGetCampaignRoute("campaign.route.library", out _), "CAMPAIGN-EDIT-001 failed: registered campaign route was not cached.");
            Assert.AreEqual(1, cache.BuildCount, "CAMPAIGN-EDIT-008 failed: lookup cache should be built once for registered data.");
        }

        [Test]
        public void CAMPAIGN_EDIT_004_008_CampaignProgressSummaryAndCacheAreDataDriven()
        {
            var town = ScriptableObject.CreateInstance<LocationDefinition>();
            var library = ScriptableObject.CreateInstance<LocationDefinition>();
            var school = ScriptableObject.CreateInstance<LocationDefinition>();
            var career = ScriptableObject.CreateInstance<CareerDefinition>();
            var visitTown = ScriptableObject.CreateInstance<CampaignLocationVisitObjective>();
            var visitLibrary = ScriptableObject.CreateInstance<CampaignLocationVisitObjective>();
            var schoolFreeGrowth = ScriptableObject.CreateInstance<CampaignActivityLogObjective>();
            var careerHint = ScriptableObject.CreateInstance<CampaignCareerHintObjective>();
            var reward = ScriptableObject.CreateInstance<CampaignCareerHintReward>();
            var routeExplore = ScriptableObject.CreateInstance<CampaignRouteDefinition>();
            var routeLibrary = ScriptableObject.CreateInstance<CampaignRouteDefinition>();
            var routeSchool = ScriptableObject.CreateInstance<CampaignRouteDefinition>();
            var dayOne = ScriptableObject.CreateInstance<CampaignDayDefinition>();
            var dayTwo = ScriptableObject.CreateInstance<CampaignDayDefinition>();
            var campaign = ScriptableObject.CreateInstance<CampaignDefinition>();
            try
            {
                town.ConfigureForTests("location.town-square", "Town Square", Vector2.zero, Array.Empty<DiscoveryDefinition>());
                library.ConfigureForTests("location.library", "Library", Vector2.zero, Array.Empty<DiscoveryDefinition>());
                school.ConfigureForTests("location.school", "School", Vector2.zero, Array.Empty<DiscoveryDefinition>());
                career.ConfigureForTests("career.writer", "Writer");
                visitTown.ConfigureForTests("campaign.objective.visit-town", town, 1);
                visitLibrary.ConfigureForTests("campaign.objective.visit-library", library, 1);
                schoolFreeGrowth.ConfigureForTests("campaign.objective.school-free-growth", "location-activity:self-study", 1, false);
                careerHint.ConfigureForTests("campaign.objective.career-hint", career);
                reward.ConfigureForTests("campaign.reward.writer-hint", career);
                routeExplore.ConfigureForTests("campaign.route.explore", "Explore town", "Visit a town place", new CampaignObjectiveBase[] { visitTown });
                routeLibrary.ConfigureForTests("campaign.route.library", "Library path", "Study outside class", new CampaignObjectiveBase[] { visitLibrary, schoolFreeGrowth });
                routeSchool.ConfigureForTests("campaign.route.school", "School path", "Attend class", new CampaignObjectiveBase[] { careerHint });
                dayOne.ConfigureForTests("campaign.day.town", 1, "Town adaptation", "Meet the town", "Try one of several routes", new[] { routeExplore, routeLibrary });
                dayTwo.ConfigureForTests("campaign.day.growth", 2, "Find growth", "School and outside-school both work", "Choose a growth route", new[] { routeLibrary, routeSchool });
                campaign.ConfigureForTests("campaign.first-three-days", "First Three Days", "Open campaign", new[] { dayOne, dayTwo }, new CampaignRewardBase[] { reward });

                var registry = ScriptableObject.CreateInstance<GameDataRegistry>();
                registry.ConfigureCampaignsForTests(new[] { campaign });
                var cache = new GameDataLookupCache(registry);

                Assert.IsTrue(cache.TryGetCampaign("campaign.first-three-days", out var loaded), "CAMPAIGN-EDIT-001 failed: campaign was not loaded from registry cache.");
                Assert.AreSame(campaign, loaded);
                Assert.IsTrue(cache.TryGetCampaignDay("campaign.day.town", out var loadedDay), "CAMPAIGN-EDIT-001 failed: campaign day was not cached.");
                Assert.IsTrue(cache.TryGetCampaignRoute("campaign.route.library", out var loadedRoute), "CAMPAIGN-EDIT-001 failed: campaign route was not cached.");
                Assert.AreSame(dayOne, loadedDay);
                Assert.AreSame(routeLibrary, loadedRoute);
                Assert.AreEqual(2, dayOne.Routes.Length, "CAMPAIGN-EDIT-002 failed: day one should expose at least two alternate routes.");
                Assert.AreEqual(2, dayTwo.Routes.Length, "CAMPAIGN-EDIT-002 failed: day two should expose at least two alternate routes.");
                Assert.IsTrue(dayTwo.HasSchoolFreeCompletionRoute(), "CAMPAIGN-EDIT-003 failed: at least one day should complete without school class.");
                Assert.AreEqual(1, cache.BuildCount, "CAMPAIGN-EDIT-008 failed: lookup cache should be built once.");

                var progress = new StudentLifeProgress("slot", "player", 10, 10);
                var campaignProgress = new CampaignProgress("slot", "player", campaign);
                progress.RecordActivityCompleted("location-activity:self-study", new[] { "location-activity:self-study:+skill.focus=1" });
                progress.UnlockCareerHint(career);
                var context = new CampaignEvaluationContext(progress, campaignProgress, new[] { town, library }, Array.Empty<string>());
                var runner = new CampaignRunner();

                Assert.IsTrue(runner.TryApplyProgress(campaign, context, "campaign-result-day-1", out var result), "CAMPAIGN-EDIT-004 failed: campaign objectives should evaluate data-driven progress.");
                CollectionAssert.Contains(result.CompletedObjectiveIds, visitTown.Id);
                CollectionAssert.Contains(result.CompletedObjectiveIds, visitLibrary.Id);
                CollectionAssert.Contains(result.CompletedObjectiveIds, schoolFreeGrowth.Id);
                CollectionAssert.Contains(result.CompletedObjectiveIds, careerHint.Id);
                Assert.IsTrue(campaignProgress.SelectRoute(dayOne, routeLibrary), "CAMPAIGN-EDIT-005 failed: selected route was not recorded.");
                Assert.IsTrue(campaignProgress.TryClaimRewards(campaign, "campaign-reward-request-1"), "CAMPAIGN-EDIT-006 failed: reward should claim once.");
                Assert.IsTrue(progress.IsCareerHintUnlocked(career));

                var restored = CampaignProgress.FromSaveData(campaignProgress.ToSaveData(), campaign);
                Assert.AreEqual(campaign.Id, restored.ActiveCampaignId, "CAMPAIGN-EDIT-005 failed: active campaign did not survive save/load.");
                Assert.IsTrue(restored.IsObjectiveCompleted(visitTown.Id), "CAMPAIGN-EDIT-005 failed: completed objective did not survive save/load.");
                Assert.AreEqual(routeLibrary.Id, restored.GetSelectedRouteId(dayOne.Id), "CAMPAIGN-EDIT-005 failed: selected route did not survive save/load.");
                Assert.IsFalse(restored.TryClaimRewards(campaign, "campaign-reward-request-1"), "CAMPAIGN-EDIT-006 failed: duplicate reward request was applied.");

                var duplicateContext = new CampaignEvaluationContext(progress, restored, new[] { town, library }, Array.Empty<string>());
                Assert.IsFalse(runner.TryApplyProgress(campaign, duplicateContext, "campaign-result-day-1", out var duplicate), "CAMPAIGN-EDIT-006 failed: duplicate result id changed campaign progress.");
                Assert.AreEqual(0, duplicate.CompletedObjectiveIds.Length);

                var hud = CampaignSummaryBuilder.BuildHudSummary(campaign, restored);
                var dayResult = CampaignSummaryBuilder.BuildDayResultSummary(campaign, restored, result);
                StringAssert.Contains("Town adaptation", hud.TodayDirection, "CAMPAIGN-EDIT-007 failed: HUD omitted today's direction.");
                StringAssert.Contains("Library path", hud.AvailableRoutesText, "CAMPAIGN-EDIT-007 failed: HUD omitted alternate route.");
                StringAssert.Contains(routeLibrary.Id, dayResult.SelectedRouteText, "CAMPAIGN-EDIT-007 failed: result omitted selected route.");
                StringAssert.Contains(visitTown.Id, dayResult.ProgressText, "CAMPAIGN-EDIT-007 failed: result omitted campaign progress change.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(campaign);
                UnityEngine.Object.DestroyImmediate(dayTwo);
                UnityEngine.Object.DestroyImmediate(dayOne);
                UnityEngine.Object.DestroyImmediate(routeSchool);
                UnityEngine.Object.DestroyImmediate(routeLibrary);
                UnityEngine.Object.DestroyImmediate(routeExplore);
                UnityEngine.Object.DestroyImmediate(reward);
                UnityEngine.Object.DestroyImmediate(careerHint);
                UnityEngine.Object.DestroyImmediate(schoolFreeGrowth);
                UnityEngine.Object.DestroyImmediate(visitLibrary);
                UnityEngine.Object.DestroyImmediate(visitTown);
                UnityEngine.Object.DestroyImmediate(career);
                UnityEngine.Object.DestroyImmediate(school);
                UnityEngine.Object.DestroyImmediate(library);
                UnityEngine.Object.DestroyImmediate(town);
            }
        }
    }
}
