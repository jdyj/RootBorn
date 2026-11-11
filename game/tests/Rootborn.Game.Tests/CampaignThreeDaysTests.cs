// 원래 흐름: Assets/Tests/PlayMode/EndToEnd/MainChainOpeningE2EScenarioTests.cs와 SelfStudyLibraryGrowthE2EScenarioTests.cs가 지나가는
//            기본 캠페인(campaign.school-opening-three-days) 사흘. Unity 판에는 캠페인만 따로 확인하는 PlayMode 테스트가 없었다.
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.StudentLife;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 기본 캠페인 사흘을 학교 밖 길로만 지난다: 1일째 도서관 어려운 책(SelfStudy), 2일째 가게 알바(StoreWork), 3일째 기록실 문 단서(Archive).
    /// 선생님을 만나지 않아도 날마다 목표 하나가 끝난다(자유 경로 성장 원칙). 단계마다 새 GameSession으로 다시 읽는다. 자동 테스트다.
    /// </summary>
    public sealed class CampaignThreeDaysTests
    {
        private const string SelfStudy = "opening.campaign.objective.SelfStudy";
        private const string StoreWork = "opening.campaign.objective.StoreWork";
        private const string Archive = "opening.campaign.objective.Archive";
        private const string Teacher = "opening.campaign.objective.Teacher";

        [Test]
        public void Campaign_ThreeDaysOutsideSchool_CompleteOneObjectivePerDay_AndSurviveReading()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            Assert.That(game.Campaign, Is.Not.Null);
            Assert.That(game.Campaign.ActiveCampaignId, Is.EqualTo("campaign.school-opening-three-days"));

            // 1일째: 도서관에서 어려운 책.
            game.VisitLocation("location.library");
            Assert.That(game.PerformLocationActivity("location.activity.library-hard-book", "location-activity:library-hard-book:day1").Kind,
                Is.EqualTo(LifeActivityResultKind.Applied));
            game = TestWorld.Start(saves.Store);
            var day1 = game.EndDay();
            Assert.That(day1.Ended, Is.True);
            Assert.That(day1.Campaign.CompletedObjectiveIds, Does.Contain(SelfStudy), "하루 결과가 자습 목표를 끝낸다");
            game = TestWorld.Start(saves.Store);
            Assert.That(game.Campaign.IsObjectiveCompleted(SelfStudy), Is.True);
            Assert.That(game.Campaign.IsObjectiveCompleted(Teacher), Is.False, "선생님을 만나지 않았다");
            Assert.That(game.StartNextDay(), Is.True);

            // 2일째: 가게 알바 한 번.
            game = TestWorld.Start(saves.Store);
            Assert.That(game.Campaign.CurrentDayNumber, Is.EqualTo(2));
            Assert.That(game.BeginPartTimeWork("work.shift.corner-store", "work:corner-store:day2", 7).Applied, Is.True);
            Assert.That(game.SettleMinigame("work:corner-store:day2", true).Applied, Is.True);
            game = TestWorld.Start(saves.Store);
            var day2 = game.EndDay();
            Assert.That(day2.Campaign.CompletedObjectiveIds, Does.Contain(StoreWork));
            game = TestWorld.Start(saves.Store);
            Assert.That(game.StartNextDay(), Is.True);

            // 3일째: 도서관 기록실 문.
            game = TestWorld.Start(saves.Store);
            game.VisitLocation("location.library");
            Assert.That(game.PerformLocationActivity("opening.clue.archive-door", "location-activity:archive-door:day3").Kind, Is.EqualTo(LifeActivityResultKind.Applied));
            game = TestWorld.Start(saves.Store);
            var day3 = game.EndDay();
            Assert.That(day3.Campaign.CompletedObjectiveIds, Does.Contain(Archive));

            var final = TestWorld.Start(saves.Store);
            Assert.That(new[] { SelfStudy, StoreWork, Archive }.All(final.Campaign.IsObjectiveCompleted), Is.True);
            Assert.That(final.Campaign.IsObjectiveCompleted(Teacher), Is.False);
            Assert.That(final.EndDay().Ended, Is.False, "끝낸 날의 결과는 다시 적용되지 않는다");
            Assert.That(TestWorld.Start(saves.Store).Campaign.ToSaveData().CompletedObjectiveIds.Count(id => id == Archive), Is.EqualTo(1));
        }
    }
}
