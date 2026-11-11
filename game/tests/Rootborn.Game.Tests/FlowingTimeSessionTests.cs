using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 흐르는 시간을 GameSession으로(설계 2026-11-12-flowing-time-design.md 6절 2단계): 시간 흐르기·멈춤, 밤샘·지침, 자지 않은 날 넘김, 잠, 저장·다시 읽기.
    /// 실제 data/와 game/time.json, 임시 폴더 저장소. 시간은 손으로 돌린다(AdvanceTime에 실제 초를 넘긴다). 자동 테스트다.
    /// </summary>
    public sealed class FlowingTimeSessionTests
    {
        private const string LibraryStudy = "outside.self-study.library";
        private const float GameHour = 25f; // 하루 600초 → 게임 1시간이 실제 25초

        private static void PassHours(GameSession game, int hours)
        {
            for (int i = 0; i < hours; i++) game.AdvanceTime(GameHour, false);
        }

        private static void PassUntil(GameSession game, int minutes)
        {
            while (game.Student.TimeMinutes < minutes && game.Student.DayState == StudentDayState.InProgress) game.AdvanceTime(GameHour / 60f, false);
        }

        [Test]
        public void TimeFlows_WithRealSeconds_AndStopsWhileAMenuIsOpen_WhenAlone()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            var topics = new List<string>();
            game.Changed += topics.Add;
            Assert.That(game.IsSoloRoom, Is.True);
            Assert.That(game.ClockMinutes, Is.EqualTo(7 * 60));

            Assert.That(game.AdvanceTime(GameHour, false), Is.EqualTo(60));
            Assert.That(game.ClockMinutes, Is.EqualTo(8 * 60));
            Assert.That(topics, Does.Contain(GameChangeTopics.Time));

            Assert.That(game.AdvanceTime(GameHour * 4, true), Is.EqualTo(0), "혼자 할 때 패널·모달·대화가 열려 있으면 멈춘다");
            Assert.That(game.ClockMinutes, Is.EqualTo(8 * 60));
            Assert.That(game.AdvanceTime(0.2f, false), Is.EqualTo(0), "1분이 안 되는 초는 모아 둔다");
            Assert.That(game.AdvanceTime(0.3f, false), Is.EqualTo(1));
        }

        [Test]
        public void TheHourIsSaved_WithoutACommand()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            Assert.That(game.PerformOutsideSchoolActivity(LibraryStudy, "flow:first").Kind, Is.EqualTo(LifeActivityResultKind.Applied), "준비: 첫 저장");
            PassHours(game, 3);
            Assert.That(TestWorld.Start(saves.Store).Student.TimeMinutes, Is.EqualTo(game.Student.TimeMinutes), "게임 1시간이 바뀔 때 저장한다");
        }

        [Test]
        public void AnActivity_TakesItsMinutes_AndThereIsNoDailyCount()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            int start = game.Student.TimeMinutes;
            for (int i = 0; i < 3; i++)
                Assert.That(game.PerformOutsideSchoolActivity(LibraryStudy, "flow:study:" + i).Kind, Is.EqualTo(LifeActivityResultKind.Applied),
                    "평일 세 번째 큰 활동도 된다(시간 포인트 없음)");
            int minutes = TestWorld.Definitions.Get<OutsideSchoolActivity>(LibraryStudy).TimeCostMinutes;
            Assert.That(minutes, Is.GreaterThan(0), "준비: 자습은 시간이 든다");
            Assert.That(game.Student.TimeMinutes, Is.EqualTo(start + 3 * minutes), "자습 시간(data/) × 3");
            Assert.That(game.ActivityEndsAtMinutes, Is.EqualTo(game.Student.TimeMinutes));
            Assert.That(game.IsBusy, Is.False, "혼자면 끝나는 시각으로 곧바로 건너뛴다");
        }

        [Test]
        public void StayingUp_LosesEnergy_ThenExhausted_NothingStarts_AndItIsSaved()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            // 낮에 체력을 쓴다(이웃 돕기는 체력 1·집중 0). 활동이 얼마나 걸리든(data/의 값) 취침 시각 전에 끝나게 오후 5시 전에만 시작한다.
            int n = 0;
            while (game.Student.Energy > 2 && game.Student.TimeMinutes < 17 * 60)
                Assert.That(game.PerformOutsideSchoolActivity("outside.help-neighbor", "flow:help:" + n++).Kind, Is.EqualTo(LifeActivityResultKind.Applied));
            Assert.That(game.Student.TimeMinutes, Is.LessThan(22 * 60), "준비: 낮 활동이 취침 시각 전에 끝났다");
            int atBedtime = game.Student.Energy;
            Assert.That(atBedtime, Is.InRange(1, 11), "준비: 체력을 썼고 남아 있다");
            PassUntil(game, 22 * 60);
            Assert.That(game.Student.Energy, Is.EqualTo(atBedtime), "취침 시각까지는 줄지 않는다");
            Assert.That(game.MinutesUntilBedtime, Is.EqualTo(0));

            for (int late = 1; late <= atBedtime; late++)
            {
                PassHours(game, 1);
                Assert.That(game.Student.Energy, Is.EqualTo(atBedtime - late), "취침 시각 뒤 " + late + "시간");
            }
            Assert.That(game.IsExhausted, Is.True);
            Assert.That(game.PerformOutsideSchoolActivity(LibraryStudy, "flow:exhausted").Kind, Is.Not.EqualTo(LifeActivityResultKind.Applied));
            Assert.That(game.PerformLocationActivity("opening.introduce.restaurant", "flow:free").Kind, Is.Not.EqualTo(LifeActivityResultKind.Applied),
                "체력 0짜리도 시작하지 못한다");

            var again = TestWorld.Start(saves.Store);
            Assert.That(again.IsExhausted, Is.True, "지침은 저장된다");
            Assert.That(again.ClockMinutes, Is.EqualTo((22 + atBedtime) % 24 * 60), "체력이 0이 된 시각");
            PassHours(again, 1);
            Assert.That(again.Student.Energy, Is.EqualTo(0));
        }

        [Test]
        public void NotSleepingUntilSeven_StartsTheNextDayWithoutRest_AndStillExhausted()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            var topics = new List<string>();
            game.Changed += topics.Add;
            int n = 0;
            while (game.Student.Energy > 3 && game.Student.TimeMinutes < 17 * 60) // 활동 길이(data/)와 무관하게 낮에만 한다
                game.PerformOutsideSchoolActivity("outside.help-neighbor", "flow:help:" + n++);
            int focus = game.Student.Focus;

            for (int hour = 0; hour < 30 && game.Student.CurrentDay == 1; hour++) PassHours(game, 1); // 낮을 지나 밤을 새고 07:00을 넘긴다
            Assert.That(game.Student.CurrentDay, Is.EqualTo(2), "07:00에 날이 넘어간다");
            Assert.That(game.Student.DayState, Is.EqualTo(StudentDayState.InProgress), "자지 않은 날은 곧바로 다음 날이 열린다");
            Assert.That(game.LastDayResult.Ended, Is.True, "어제의 결과는 남는다");
            Assert.That(game.Student.Energy, Is.EqualTo(0), "회복이 없다");
            Assert.That(game.Student.Focus, Is.EqualTo(focus), "집중도 돌아오지 않는다");
            Assert.That(game.IsExhausted, Is.True, "지친 채로 시작한다");
            Assert.That(topics, Does.Contain(GameChangeTopics.Day));

            var again = TestWorld.Start(saves.Store);
            Assert.That((again.Student.CurrentDay, again.IsExhausted), Is.EqualTo((2, true)), "다시 읽어도 지쳐 있다");
            Assert.That(again.Sleep().Ended, Is.True, "지쳐도 잘 수 있다");
            Assert.That(again.StartNextDay(), Is.True);
            Assert.That((again.Student.CurrentDay, again.ClockMinutes, again.IsExhausted), Is.EqualTo((3, 7 * 60, false)), "낮에 자도 다음 날 07:00, 자야 풀린다");
            Assert.That(again.Student.Energy, Is.EqualTo(12));
        }

        [Test]
        public void Sleeping_OnTime_RestoresAll_Late_RestoresLess()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            game.PerformOutsideSchoolActivity(LibraryStudy, "flow:study");
            PassUntil(game, 21 * 60);
            Assert.That(game.Sleep().Ended, Is.True);
            Assert.That(game.Student.Energy, Is.EqualTo(12), "취침 시각 전에 자면 다 돌아온다");
            Assert.That(game.AdvanceTime(GameHour, false), Is.EqualTo(0), "결과를 보는 동안은 흐르지 않는다");
            Assert.That(game.StartNextDay(), Is.True);
            Assert.That(game.ClockMinutes, Is.EqualTo(7 * 60));

            // 둘째 날: 체력을 2까지 쓰고 02:00에 잔다 → 밤에 2를 잃어 0, 회복은 12 - 2×4 = 4.
            int n = 0;
            while (game.Student.Energy > 2) game.PerformOutsideSchoolActivity("outside.help-neighbor", "flow:day2:" + n++);
            PassUntil(game, 26 * 60);
            Assert.That(game.ClockMinutes, Is.EqualTo(2 * 60));
            Assert.That(game.Sleep().Ended, Is.True);
            Assert.That(game.Student.Energy, Is.EqualTo(4));
            Assert.That(game.StartNextDay(), Is.True);
            Assert.That((game.Student.CurrentDay, game.Student.Energy, game.IsExhausted), Is.EqualTo((3, 4, false)));
        }

        [Test]
        public void TheDailyPlanSlot_FollowsTheTimeOfDay()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            Assert.That(game.SelectDailyPlanRoute(game.AvailableDailyPlanRoutes()[0].Id), Is.True, "준비: 일과를 고른다(칸이 맞춰진다)");
            Assert.That(game.CurrentDailyPlanSlotIndex, Is.EqualTo(-1), "평일 07:00 — 칸 없음");
            PassUntil(game, 13 * 60);
            Assert.That(game.CurrentDailyPlanSlotIndex, Is.EqualTo(0), "방과 후 칸(낮)");
            PassUntil(game, 18 * 60);
            Assert.That(game.CurrentDailyPlanSlotIndex, Is.EqualTo(1), "저녁 칸");
        }

        [Test]
        public void EndDay_IsSleepingNow()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            PassUntil(game, 23 * 60);
            Assert.That(game.EndDay().Ended, Is.True);
            Assert.That(game.Student.Energy, Is.EqualTo(11), "23:00에 잔 것과 같다: 밤에 1을 잃어 11, 회복 목표는 12 - 2 = 10이라 자서 줄지 않고 11");
            Assert.That(game.IsExhausted, Is.False);
        }

        [Test]
        public void AnOldSave_ContinuesFromItsSavedTime_ReadsTheOldField_AndRoundTrips()
        {
            using var saves = new TempSaves();
            saves.CopyFixture("exe-20261105/slot-0", "slot-0");
            Assert.That(File.Exists(Path.Combine(saves.Path, "slot-0", SaveFileNames.StudentClock(TestWorld.Player))), Is.False, "옛 세이브에는 시계 파일이 없다");

            var game = TestWorld.Start(saves.Store, "slot-0");
            Assert.That((game.Student.CurrentDay, game.Student.TimeMinutes), Is.EqualTo((2, 510)), "저장된 시각(08:30)에서 이어진다");
            Assert.That(game.Student.TimePointsSpentToday, Is.EqualTo(1), "옛 필드는 읽는다(한도로 쓰지 않는다)");
            Assert.That(game.IsExhausted, Is.False);

            PassHours(game, 2);
            Assert.That(File.Exists(Path.Combine(saves.Path, "slot-0", SaveFileNames.StudentClock(TestWorld.Player))), Is.True, "저장하면 시계 파일이 생긴다");
            var again = TestWorld.Start(saves.Store, "slot-0");
            Assert.That((again.Student.CurrentDay, again.Student.TimeMinutes, again.Student.Energy), Is.EqualTo((2, 630, game.Student.Energy)));
            Assert.That(again.Student.TimePointsSpentToday, Is.EqualTo(1), "옛 필드는 이름·형 그대로 다시 써진다");
        }
    }
}
