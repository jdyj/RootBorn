// 원래 파일: Assets/Tests/PlayMode/EndToEnd/StudentDayResultLoopE2EScenarioTests.cs (DAY_END_001)
using NUnit.Framework;
using Rootborn.Core.StudentLife;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 하루를 끝내면 결과를 기다리는 상태가 되고(날은 넘어가지 않음), 다시 읽어도 그 상태이며 하루를 다시 정산하지 않는다. 다음 날 시작으로만 넘어간다.
    /// 자동 테스트다.
    /// </summary>
    public sealed class DayEndLoopTests
    {
        [Test]
        public void DayEnd001_EndingTheDay_WaitsForTheResult_SurvivesReading_ThenTheNextDayStarts()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            game.TalkTo("npc.first-guide"); // 첫 저장
            int day = game.Student.CurrentDay;
            Assert.That(game.Student.DayState, Is.EqualTo(StudentDayState.InProgress));

            Assert.That(game.EndDay().Ended, Is.True);
            Assert.That(game.Student.DayState, Is.EqualTo(StudentDayState.ResultReady));
            Assert.That(game.Student.CurrentDay, Is.EqualTo(day), "하루를 끝내도 날은 그대로다");

            game = TestWorld.Start(saves.Store);
            Assert.That(game.Student.DayState, Is.EqualTo(StudentDayState.ResultReady), "끝낸 날이 저장된다");
            Assert.That(game.Student.CurrentDay, Is.EqualTo(day));
            Assert.That(game.EndDay().Ended, Is.False, "결과를 다시 열어도 다시 정산하지 않는다");
            Assert.That(game.Student.CurrentDay, Is.EqualTo(day));

            game = TestWorld.Start(saves.Store);
            Assert.That(game.StartNextDay(), Is.True);
            game = TestWorld.Start(saves.Store);
            Assert.That(game.Student.CurrentDay, Is.EqualTo(day + 1));
            Assert.That(game.Student.DayState, Is.EqualTo(StudentDayState.InProgress));
            Assert.That(game.StartNextDay(), Is.False, "진행 중인 날은 넘기지 않는다");
        }

        [Test]
        public void DayEnd_ActivitiesAreRefusedWhileTheResultIsWaiting()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            game.TalkTo("npc.first-guide");
            game.EndDay();
            game = TestWorld.Start(saves.Store);
            int focus = game.Student.GetTraitValue("trait.focus");

            Assert.That(game.PerformOutsideSchoolActivity("outside.self-study.library", "outside:after-end").Kind, Is.Not.EqualTo(LifeActivityResultKind.Applied));
            Assert.That(game.BeginPartTimeWork("work.shift.corner-store", "work:after-end", 1).Applied, Is.False);
            Assert.That(TestWorld.Start(saves.Store).Student.GetTraitValue("trait.focus"), Is.EqualTo(focus));
        }
    }
}
