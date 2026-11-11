using System;
using NUnit.Framework;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Tests
{
    // 옮겨 옴: Assets/Tests/EditMode/StudentLife/CampaignLoopTests.cs (CAMPAIGN_EDIT_004_008; 001과 등록부 캐시 검사는 에셋 검사라 옮기지 않음)
    public sealed class CampaignTests
    {
        private sealed class Fixture
        {
            public readonly CampaignLocationVisitObjective VisitTown = new CampaignLocationVisitObjective("campaign.objective.visit-town", "location.town-square", 1);
            public readonly CampaignLocationVisitObjective VisitLibrary = new CampaignLocationVisitObjective("campaign.objective.visit-library", "location.library", 1);
            public readonly CampaignActivityLogObjective SchoolFreeGrowth = new CampaignActivityLogObjective("campaign.objective.school-free-growth", "location-activity:self-study", 1, false);
            public readonly CampaignCareerHintObjective CareerHint = new CampaignCareerHintObjective("campaign.objective.career-hint", "career.writer");
            public readonly CampaignRoute RouteExplore, RouteLibrary, RouteSchool;
            public readonly CampaignDay DayOne, DayTwo;
            public readonly Campaign Campaign;

            public Fixture()
            {
                RouteExplore = new CampaignRoute("campaign.route.explore", "Explore town", "Visit a town place", new CampaignObjective[] { VisitTown });
                RouteLibrary = new CampaignRoute("campaign.route.library", "Library path", "Study outside class", new CampaignObjective[] { VisitLibrary, SchoolFreeGrowth });
                RouteSchool = new CampaignRoute("campaign.route.school", "School path", "Attend class", new CampaignObjective[] { CareerHint });
                DayOne = new CampaignDay("campaign.day.town", 1, "Town adaptation", "Meet the town", "Try one of several routes", new[] { RouteExplore, RouteLibrary });
                DayTwo = new CampaignDay("campaign.day.growth", 2, "Find growth", "School and outside-school both work", "Choose a growth route", new[] { RouteLibrary, RouteSchool });
                Campaign = new Campaign("campaign.first-three-days", "Open campaign", new[] { DayOne, DayTwo },
                    new CampaignReward[] { new CampaignCareerHintReward("campaign.reward.writer-hint", "career.writer") });
            }
        }

        [Test]
        public void CAMPAIGN_EDIT_004_008_CampaignProgressSummaryAndCacheAreDataDriven()
        {
            var f = new Fixture();
            Assert.That(f.DayOne.Routes.Count, Is.EqualTo(2));
            Assert.That(f.DayTwo.Routes.Count, Is.EqualTo(2));
            Assert.That(f.DayTwo.HasSchoolFreeCompletionRoute(), Is.True);

            var progress = new StudentLifeProgress("slot", "player", 10, 10);
            var campaignProgress = new CampaignProgress("slot", "player", f.Campaign);
            progress.RecordActivityCompleted("location-activity:self-study", new[] { "location-activity:self-study:+skill.focus=1" });
            progress.UnlockCareerHint("career.writer");
            var context = new CampaignEvaluationContext(progress, campaignProgress, new[] { "location.town-square", "location.library" }, Array.Empty<string>());
            var runner = new CampaignRunner();

            Assert.That(runner.TryApplyProgress(f.Campaign, context, "campaign-result-day-1", out var result), Is.True);
            Assert.That(result.CompletedObjectiveIds, Does.Contain(f.VisitTown.Id));
            Assert.That(result.CompletedObjectiveIds, Does.Contain(f.VisitLibrary.Id));
            Assert.That(result.CompletedObjectiveIds, Does.Contain(f.SchoolFreeGrowth.Id));
            Assert.That(result.CompletedObjectiveIds, Does.Contain(f.CareerHint.Id));
            Assert.That(campaignProgress.SelectRoute(f.DayOne, f.RouteLibrary), Is.True);
            Assert.That(campaignProgress.TryClaimRewards(f.Campaign, progress, "campaign-reward-request-1"), Is.True);
            Assert.That(progress.IsCareerHintUnlocked("career.writer"), Is.True);

            var restored = CampaignProgress.FromSaveData(campaignProgress.ToSaveData(), f.Campaign);
            Assert.That(restored.ActiveCampaignId, Is.EqualTo(f.Campaign.Id));
            Assert.That(restored.IsObjectiveCompleted(f.VisitTown.Id), Is.True);
            Assert.That(restored.GetSelectedRouteId(f.DayOne.Id), Is.EqualTo(f.RouteLibrary.Id));
            Assert.That(restored.TryClaimRewards(f.Campaign, progress, "campaign-reward-request-1"), Is.False);

            var duplicateContext = new CampaignEvaluationContext(progress, restored, new[] { "location.town-square", "location.library" }, Array.Empty<string>());
            Assert.That(runner.TryApplyProgress(f.Campaign, duplicateContext, "campaign-result-day-1", out var duplicate), Is.False);
            Assert.That(duplicate.CompletedObjectiveIds, Is.Empty);

            var hud = CampaignSummaryBuilder.BuildHudSummary(f.Campaign, restored);
            var dayResult = CampaignSummaryBuilder.BuildDayResultSummary(f.Campaign, restored, result);
            Assert.That(hud.TodayDirection, Does.Contain("Town adaptation"));
            Assert.That(hud.AvailableRoutesText, Does.Contain("Library path"));
            Assert.That(dayResult.SelectedRouteText, Does.Contain(f.RouteLibrary.Id));
            Assert.That(dayResult.ProgressText, Does.Contain(f.VisitTown.Id));
        }

        // 더한 경계

        [Test]
        public void RewardWithProgressAppliesOnceAndRejectedRewardChangesNothing()
        {
            var f = new Fixture();
            var progress = new StudentLifeProgress("slot", "player", 10, 10);
            var campaignProgress = new CampaignProgress("slot", "player", f.Campaign);

            Assert.That(campaignProgress.TryClaimRewards(f.Campaign, progress, "req"), Is.True);
            Assert.That(progress.IsCareerHintUnlocked("career.writer"), Is.True);
            Assert.That(campaignProgress.TryClaimRewards(f.Campaign, progress, "req"), Is.False);
            Assert.That(campaignProgress.TryClaimRewards(f.Campaign, progress, ""), Is.False);

            var blocked = new Campaign("campaign.blocked", null, null, new CampaignReward[] { new BlockedReward(), new CampaignCareerHintReward("reward.hint", "career.cook") });
            Assert.That(campaignProgress.TryClaimRewards(blocked, progress, "req-2"), Is.False);
            Assert.That(progress.IsCareerHintUnlocked("career.cook"), Is.False);
            Assert.That(campaignProgress.ToSaveData().RewardClaimedIds, Does.Not.Contain("req-2"));
        }

        [Test]
        public void RewardWithoutStudentProgressIsRejectedAndNotRecorded()
        {
            // Unity 판과 달라진 동작: 진행 상태 없이 받으면 받은 것으로 기록만 되던 것을 거절한다.
            var f = new Fixture();
            var campaignProgress = new CampaignProgress("slot", "player", f.Campaign);

            Assert.That(campaignProgress.TryClaimRewards(f.Campaign, null, "req"), Is.False);
            Assert.That(campaignProgress.ToSaveData().RewardClaimedIds, Is.Empty);
            Assert.That(campaignProgress.TryClaimRewards(f.Campaign, new StudentLifeProgress("slot", "player", 10, 10), "req"), Is.True);
        }

        [Test]
        public void ResultIdWithNothingNewIsNotConsumedAndEmptyResultIdIsRejected()
        {
            var f = new Fixture();
            var progress = new StudentLifeProgress("slot", "player", 10, 10);
            var campaignProgress = new CampaignProgress("slot", "player", f.Campaign);
            var runner = new CampaignRunner();
            var empty = new CampaignEvaluationContext(progress, campaignProgress, null, null);

            Assert.That(runner.TryApplyProgress(f.Campaign, empty, "", out _), Is.False);
            Assert.That(runner.TryApplyProgress(f.Campaign, empty, "r1", out var nothing), Is.False);
            Assert.That(nothing.CompletedObjectiveIds, Is.Empty);
            // Unity 판과 달라진 동작: 새로 끝난 목표가 없으면 결과 id를 남기지 않는다.
            Assert.That(campaignProgress.ToSaveData().AppliedResultIds, Is.Empty);
            Assert.That(campaignProgress.MarkResultApplied("r1"), Is.True);
        }

        [Test]
        public void ActivityLogObjectiveCountsAllLogSourcesAndSchoolFlagDecidesSchoolFreeRoute()
        {
            var objective = new CampaignActivityLogObjective("o", "help:", 3, false);
            var progress = new StudentLifeProgress("slot", "player", 10, 10);
            progress.RecordActivityCompleted("help:a", new[] { "help:a:+trait=1" });

            var evaluation = objective.Evaluate(new CampaignEvaluationContext(progress, null, null, null));
            Assert.That(evaluation.Current, Is.EqualTo(2));
            Assert.That(evaluation.IsComplete, Is.False);

            var schoolOnly = new CampaignRoute("r.school", "School", null, new CampaignObjective[] { new CampaignActivityLogObjective("o2", "class:", 1, true) });
            var noObjectives = new CampaignRoute("r.none", "None", null, null);
            Assert.That(new CampaignDay("d", 1, null, null, null, new[] { schoolOnly, noObjectives }).HasSchoolFreeCompletionRoute(), Is.False);
            Assert.That(noObjectives.DescriptionKey, Is.EqualTo("None"));
        }

        [Test]
        public void DayLookupFallsBackToClampedIndexAndDefaultsTextKeys()
        {
            var dayA = new CampaignDay("day.a", 1, null, null, null, null);
            var dayB = new CampaignDay("day.b", 5, "theme.b", null, null, null);
            var campaign = new Campaign("c", null, new[] { dayA, dayB }, null);

            Assert.That(campaign.GetDayByNumber(5), Is.SameAs(dayB));
            Assert.That(campaign.GetDayByNumber(2), Is.SameAs(dayB));
            Assert.That(campaign.GetDayByNumber(9), Is.SameAs(dayB));
            Assert.That(campaign.GetDayByNumber(0), Is.SameAs(dayA));
            Assert.That(dayA.ThemeKey, Is.EqualTo("day.a"));
            Assert.That(dayB.DirectionText, Is.EqualTo("theme.b"));
            Assert.That(campaign.DescriptionKey, Is.EqualTo("c"));
            Assert.That(new Campaign("e", null, null, null).GetDayByNumber(1), Is.Null);
        }

        [Test]
        public void SaveDataRoundTripAndFileNames()
        {
            var f = new Fixture();
            var progress = new CampaignProgress("slot", "p-1", f.Campaign);
            progress.SetCurrentDayNumber(2);
            progress.CompleteObjective("o.1");
            progress.SelectRoute(f.DayTwo, f.RouteSchool);
            progress.MarkResultApplied("res-1");

            var restored = CampaignProgress.FromSaveData(progress.ToSaveData(), null);
            Assert.That(restored.SaveSlot, Is.EqualTo("slot"));
            Assert.That(restored.PlayerId, Is.EqualTo("p-1"));
            Assert.That(restored.ActiveCampaignId, Is.EqualTo(f.Campaign.Id));
            Assert.That(restored.CurrentDayNumber, Is.EqualTo(2));
            Assert.That(restored.LastProgressLogIds, Is.EqualTo(new[] { "o.1" }));
            Assert.That(restored.GetSelectedRouteId(f.DayTwo.Id), Is.EqualTo(f.RouteSchool.Id));
            Assert.That(restored.MarkResultApplied("res-1"), Is.False);
            Assert.That(restored.CompleteObjective("o.1"), Is.False);
            Assert.That(CampaignProgress.FromSaveData(null, f.Campaign).ActiveCampaignId, Is.EqualTo(f.Campaign.Id));

            Assert.That(CampaignProgressPersistence.FileNameFor("player"), Is.EqualTo("campaign-progress.json"));
            Assert.That(CampaignProgressPersistence.FileNameFor("local-player"), Is.EqualTo("campaign-progress.json"));
            Assert.That(CampaignProgressPersistence.FileNameFor(""), Is.EqualTo("campaign-progress.json"));
            Assert.That(CampaignProgressPersistence.FileNameFor("steam:12 a"), Is.EqualTo("campaign-progress-steam_12_a.json"));
        }

        private sealed class BlockedReward : CampaignReward
        {
            public BlockedReward() : base("reward.blocked") { }
            public override bool CanApply(StudentLifeProgress progress) => false;
            public override void Apply(StudentLifeProgress progress) => throw new InvalidOperationException();
        }
    }
}
