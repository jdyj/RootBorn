using NUnit.Framework;

namespace Rootborn.Game.Tests
{
    /// <summary>화면과 월드가 쓸 조회: 학생 시각·날, 성장 단계(자동 테스트).</summary>
    public sealed class StudentClockAndGrowthTests
    {
        [Test]
        public void ANewGame_StartsOnDayOneAtEight()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);

            Assert.That(game.StudentDay, Is.EqualTo(1));
            Assert.That(game.StudentTimeMinutes, Is.EqualTo(GameStartSettings.UnityStartTimeMinutes));
            Assert.That(game.StudentHour, Is.EqualTo(8f));
        }

        [Test]
        public void TheStudentClock_MovesWithActivities_AndIsSaved()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            int before = game.StudentTimeMinutes;
            Assert.That(game.PerformLocationActivity("location.activity.square-help-request", "clock:1").Kind, Is.EqualTo(Rootborn.Core.StudentLife.LifeActivityResultKind.Applied));

            Assert.That(game.StudentTimeMinutes, Is.GreaterThan(before));
            Assert.That(game.StudentHour, Is.EqualTo(game.StudentTimeMinutes / 60f));
            Assert.That(TestWorld.Start(saves.Store).StudentTimeMinutes, Is.EqualTo(game.StudentTimeMinutes));
        }

        [Test]
        public void TheGrowthStage_FollowsTheStudentDay_AndAdulthood()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            Assert.That(game.GrowthStageId(), Is.EqualTo("stage.playground"), "1일째");

            Assert.That(game.EndDay().Ended, Is.True);
            Assert.That(game.StartNextDay(), Is.True);
            Assert.That(game.GrowthStageId(), Is.EqualTo("stage.school"), "2일째부터 학교 단계");

            SaveEdits.EditSnapshot(saves.Store, snapshot =>
            {
                snapshot.Adult.FacilityId = "facility.restaurant";
                snapshot.Adult.FacilityLevel = 1;
            });
            Assert.That(TestWorld.Start(saves.Store).GrowthStageId(), Is.EqualTo("stage.adult"));
        }
    }
}
