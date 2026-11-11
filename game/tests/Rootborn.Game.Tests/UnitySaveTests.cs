using NUnit.Framework;
using Rootborn.Core.StudentLife;

namespace Rootborn.Game.Tests
{
    /// <summary>Unity 판 QA 세이브(Data 테스트의 고정 파일, 임시 폴더로 복사)로 GameSession을 시작하고 하루를 더 진행한다. 자동 테스트다.</summary>
    public sealed class UnitySaveTests
    {
        [TestCase("exe-20261105/slot-0", "slot-0")]
        [TestCase("editor-20261105/slot-1", "slot-1")]
        public void UnityQaSave_StartsAndPlaysAnotherDay(string fixture, string slot)
        {
            using var saves = new TempSaves();
            saves.CopyFixture(fixture, slot);
            var game = TestWorld.Start(saves.Store, slot);
            int day = game.Student.CurrentDay;
            Assert.That(day, Is.GreaterThanOrEqualTo(2), "세이브의 날짜에서 이어 간다");
            Assert.That(game.Student.DailyTimePointBudget, Is.GreaterThan(0));

            if (game.Student.DayState == StudentDayState.InProgress)
            {
                game.VisitLocation("location.town-square");
                Assert.That(game.EndDay().Ended, Is.True);
            }

            Assert.That(game.StartNextDay(), Is.True);
            Assert.That(game.Student.CurrentDay, Is.EqualTo(day + 1));

            var again = TestWorld.Start(saves.Store, slot);
            Assert.That(again.Student.CurrentDay, Is.EqualTo(day + 1));
            Assert.That(again.Student.DayState, Is.EqualTo(StudentDayState.InProgress));
        }

        [Test]
        public void UnityQaSave_KeepsTheSavedStudentValues()
        {
            using var saves = new TempSaves();
            saves.CopyFixture("exe-20261105/slot-0", "slot-0");
            var game = TestWorld.Start(saves.Store, "slot-0");

            // Rootborn.Data.Tests OldSaveTests.Snapshot_StudentSurvivesTheProgressObject와 같은 값.
            Assert.That(game.Student.Energy, Is.EqualTo(11));
            Assert.That(game.Student.Focus, Is.EqualTo(9));
            Assert.That(game.Student.CurrentDay, Is.EqualTo(2));
            Assert.That(game.Student.TimeMinutes, Is.EqualTo(510));
            Assert.That(game.Student.SaveSlot, Is.EqualTo("slot-0"));
        }
    }
}
