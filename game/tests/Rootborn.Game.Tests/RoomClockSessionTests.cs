using System.IO;
using NUnit.Framework;
using Rootborn.Core.StudentLife;
using Rootborn.Core.Time;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 게임 한 판이 방의 시계(멀티플레이 설계 2.1절)로 돈다. 혼자 하기는 혼자인 방(기본값)이고, 같은 프로세스에서 시계 하나를
    /// 두 판이 같이 쓰면 둘 다 끝내야 다음 때로 간다. GameSession 명령만 부르는 자동 테스트다(화면 근거 아님).
    /// </summary>
    public sealed class RoomClockSessionTests
    {
        private const string LibraryStudy = "outside.self-study.library";

        [Test]
        public void Alone_TheRoomFollowsTheDay_AndAnActivityMovesToTheNextPartAtOnce()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            Assert.That(game.Room, Is.Not.Null, "기본값은 혼자인 방");
            Assert.That(game.Room.Day, Is.EqualTo(game.Student.CurrentDay));
            Assert.That(game.Room.PartIndex, Is.EqualTo(0));
            Assert.That(game.Room.PartsToday, Is.EqualTo(game.Student.DailyTimePointBudget), "때 수는 달력 데이터의 하루 시간 포인트");

            Assert.That(game.PerformOutsideSchoolActivity(LibraryStudy, "outside:room:1").Kind, Is.EqualTo(LifeActivityResultKind.Applied));
            Assert.That(game.Room.PartIndex, Is.EqualTo(game.Student.TimePointsSpentToday), "혼자면 활동하는 순간 다음 때");
            Assert.That(game.Room.PartIndex, Is.EqualTo(1));
            Assert.That(game.Room.HasFinishedPart(game.PlayerId), Is.False);
        }

        [Test]
        public void Alone_EndingTheDay_IsTheRoomsNextDay_AndTheNextDayCatchesUp()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            game.TalkTo("npc.first-guide");
            int day = game.Student.CurrentDay;

            Assert.That(game.EndDay().Ended, Is.True);
            Assert.That(game.Room.Day, Is.EqualTo(day + 1), "혼자인 방은 마감하는 순간 다음 날");
            Assert.That(game.Room.PartIndex, Is.EqualTo(0));

            game = TestWorld.Start(saves.Store);
            Assert.That(game.Room.Day, Is.EqualTo(day + 1), "다시 읽어도 개인 진행에서 같은 방이 나온다");
            Assert.That(game.StartNextDay(), Is.True);
            Assert.That(game.Student.CurrentDay, Is.EqualTo(game.Room.Day));
        }

        [Test]
        public void Alone_RepresentativeTime_ComesFromThePart()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            var times = game.Student.Calendar.PartTimes;
            Assert.That(game.RoomTimeMinutes, Is.EqualTo(times.MinutesFor(0, game.Room.PartsToday)));
            game.PerformOutsideSchoolActivity(LibraryStudy, "outside:room:time");
            Assert.That(game.RoomTimeMinutes, Is.EqualTo(times.MinutesFor(1, game.Room.PartsToday)));
            Assert.That(game.Student.TimeMinutes, Is.GreaterThan(0), "분 단위 개인 시각은 그대로 남는다(지우는 일은 다음 단계)");
        }

        [Test]
        public void TwoSessions_SharingOneClock_WaitForEachOther()
        {
            using var saves = new TempSaves();
            var clock = new RoomClock(TestWorld.Definitions.Get<LifeCalendar>(TestWorld.Settings.CalendarId));
            clock.Join("player-1");
            clock.Join("player-2");
            var one = GameSession.Start(TestWorld.Definitions, TestWorld.Settings, saves.Store, "slot-room", "player-1", room: clock);
            var two = GameSession.Start(TestWorld.Definitions, TestWorld.Settings, saves.Store, "slot-room", "player-2", room: clock);

            Assert.That(one.PerformOutsideSchoolActivity(LibraryStudy, "outside:one:1").Kind, Is.EqualTo(LifeActivityResultKind.Applied));
            Assert.That(clock.PartIndex, Is.EqualTo(0), "한 명만 끝내면 그대로");
            int focus = one.Student.GetTraitValue("trait.focus");
            Assert.That(one.PerformOutsideSchoolActivity(LibraryStudy, "outside:one:2").Kind, Is.Not.EqualTo(LifeActivityResultKind.Applied),
                "끝낸 사람은 다음 때까지 시간 포인트를 쓰지 못한다");
            Assert.That(one.Student.GetTraitValue("trait.focus"), Is.EqualTo(focus), "거절되면 아무것도 바뀌지 않는다");
            Assert.That(one.Student.TimePointsSpentToday, Is.EqualTo(1));

            Assert.That(two.PerformOutsideSchoolActivity(LibraryStudy, "outside:two:1").Kind, Is.EqualTo(LifeActivityResultKind.Applied));
            Assert.That(clock.PartIndex, Is.EqualTo(1), "둘 다 끝내면 다음 때");
            Assert.That(one.PerformOutsideSchoolActivity(LibraryStudy, "outside:one:3").Kind, Is.EqualTo(LifeActivityResultKind.Applied));

            Assert.That(one.EndDay().Ended, Is.True);
            Assert.That(clock.Day, Is.EqualTo(1), "한 명만 마감하면 같은 날");
            Assert.That(two.EndDay().Ended, Is.True);
            Assert.That(clock.Day, Is.EqualTo(2));
        }

        [TestCaseSource(typeof(UnityFixtureSlotsTests), nameof(UnityFixtureSlotsTests.Slots))]
        public void OldSaves_WithoutAClock_MakeTheRoomFromPersonalProgress(string fixture, string slot)
        {
            using var saves = new TempSaves();
            saves.CopyFixture(fixture, slot);
            var game = TestWorld.Start(saves.Store, slot);
            bool ended = game.Student.DayState != StudentDayState.InProgress;
            Assert.That(game.Room.Day, Is.EqualTo(ended ? game.Student.CurrentDay + 1 : game.Student.CurrentDay));
            Assert.That(game.Room.PartIndex, Is.EqualTo(ended ? 0 : System.Math.Min(game.Student.TimePointsSpentToday, game.Room.PartsToday)));
            Assert.That(File.Exists(Path.Combine(saves.Path, slot, RoomClockSaveFiles.FileName)), Is.False, "혼자 하는 판은 시계 파일을 쓰지 않는다");
        }
    }
}
