using System.IO;
using NUnit.Framework;
using Rootborn.Core.Save;
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

        [Test]
        public void StartNextDay_WaitsUntilTheRoomHasMovedToTheNextDay()
        {
            using var saves = new TempSaves();
            var (clock, one, two) = SharedRoom(saves);
            Assert.That(one.EndDay().Ended, Is.True);
            Assert.That(one.CanStartNextDay, Is.False, "방이 아직 같은 날");
            Assert.That(one.StartNextDay(), Is.False);
            Assert.That(one.Student.CurrentDay, Is.EqualTo(1));
            Assert.That(one.Student.DayState, Is.EqualTo(StudentDayState.ResultReady), "기다리는 동안 결과를 그대로 본다");

            Assert.That(two.EndDay().Ended, Is.True);
            Assert.That(clock.Day, Is.EqualTo(2));
            Assert.That(one.CanStartNextDay, Is.True);
            Assert.That(one.StartNextDay(), Is.True);
            Assert.That(two.StartNextDay(), Is.True);
            Assert.That(one.Student.CurrentDay, Is.EqualTo(2));
            Assert.That(two.Student.CurrentDay, Is.EqualTo(2));
        }

        [Test]
        public void Alone_CanStartNextDay_RightAfterEndingTheDay()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            Assert.That(game.CanStartNextDay, Is.False, "하루가 진행 중");
            game.EndDay();
            Assert.That(game.CanStartNextDay, Is.True);
        }

        [Test]
        public void SkipPart_Alone_SpendsOnePointWithoutAnActivity_AndIsSaved()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            int energy = game.Student.Energy, focus = game.Student.GetTraitValue("trait.focus");
            var topics = new System.Collections.Generic.List<string>();
            game.Changed += topics.Add;

            Assert.That(game.CanSkipPart, Is.True);
            Assert.That(game.SkipPart(0), Is.True);
            Assert.That(game.Student.TimePointsSpentToday, Is.EqualTo(1), "개인 시간 포인트도 하나 쓴다");
            Assert.That(game.Room.PartIndex, Is.EqualTo(1), "혼자면 바로 다음 때");
            Assert.That(game.Student.GetTodayActivityIds(), Is.Empty, "활동 기록은 없다");
            Assert.That(game.Student.Energy, Is.EqualTo(energy));
            Assert.That(game.Student.GetTraitValue("trait.focus"), Is.EqualTo(focus));
            Assert.That(topics, Does.Contain(GameChangeTopics.Student).And.Contain(GameChangeTopics.Room));

            Assert.That(game.SkipPart(0), Is.False, "이미 넘긴 때를 다시 누르면(두 번 누름) 다음 때를 넘기지 않는다");
            Assert.That(game.Student.TimePointsSpentToday, Is.EqualTo(1));

            game = TestWorld.Start(saves.Store);
            Assert.That(game.Student.TimePointsSpentToday, Is.EqualTo(1), "저장된다");
            Assert.That(game.Room.PartIndex, Is.EqualTo(1));
            Assert.That(game.SkipPart(1), Is.True);
            Assert.That(game.Room.IsDayOver, Is.True);
            Assert.That(game.CanSkipPart, Is.False, "때를 다 쓰면 넘길 것이 없다");
            Assert.That(game.SkipPart(2), Is.False);
            Assert.That(game.EndDay().Ended, Is.True, "하루 마감은 그대로");
        }

        [Test]
        public void EndingTheDay_WithoutSpendingAnyPoint_StillWorks_AndSkipIsNotNeeded()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            Assert.That(game.Student.TimePointsSpentToday, Is.EqualTo(0));
            Assert.That(game.EndDay().Ended, Is.True, "지금도 시간 포인트를 안 쓰고 하루를 끝낼 수 있다");
            Assert.That(game.CanSkipPart, Is.False, "마감한 날은 넘기지 않는다");
            Assert.That(game.SkipPart(0), Is.False);
        }

        [Test]
        public void SkipPart_Shared_FinishesMyPart_AndWaitsForTheOther()
        {
            using var saves = new TempSaves();
            var (clock, one, two) = SharedRoom(saves);
            Assert.That(one.SkipPart(0), Is.True);
            Assert.That(clock.PartIndex, Is.EqualTo(0));
            Assert.That(clock.HasFinishedPart("player-1"), Is.True);
            Assert.That(one.CanSkipPart, Is.False, "기다리는 동안은 넘길 수 없다");
            Assert.That(two.PerformOutsideSchoolActivity(LibraryStudy, "outside:two:skip").Kind, Is.EqualTo(LifeActivityResultKind.Applied));
            Assert.That(clock.PartIndex, Is.EqualTo(1));
            Assert.That(one.Student.TimePointsSpentToday, Is.EqualTo(clock.CompletedParts("player-1")));
        }

        [Test]
        public void SyncWithRoom_AfterTheRoomSkippedMe_CatchesUpPersonalPoints_AndSavesIt()
        {
            using var saves = new TempSaves();
            var (clock, one, two) = SharedRoom(saves, new RoomClockSettings(autoSkipAfterTicks: 1));
            Assert.That(one.PerformOutsideSchoolActivity(LibraryStudy, "outside:one:sync").Kind, Is.EqualTo(LifeActivityResultKind.Applied));
            Assert.That(clock.Tick(1), Is.True, "player-2를 넘겼다");
            Assert.That(two.Student.TimePointsSpentToday, Is.EqualTo(0));

            var topics = new System.Collections.Generic.List<string>();
            two.Changed += topics.Add;
            Assert.That(two.SyncWithRoom(), Is.True);
            Assert.That(two.Student.TimePointsSpentToday, Is.EqualTo(1));
            Assert.That(topics, Does.Contain(GameChangeTopics.Room).And.Contain(GameChangeTopics.Student));
            Assert.That(clock.PartIndex, Is.EqualTo(1), "방에 두 번 알리지 않는다");

            topics.Clear();
            Assert.That(two.SyncWithRoom(), Is.False, "맞춘 뒤에는 바뀌는 것이 없다");
            Assert.That(topics, Is.EqualTo(new[] { GameChangeTopics.Room }), "방이 바뀌었다는 알림만(HUD가 다시 읽는다)");

            var again = GameSession.Start(TestWorld.Definitions, TestWorld.Settings, saves.Store, "slot-room", "player-2", room: clock);
            Assert.That(again.Student.TimePointsSpentToday, Is.EqualTo(1), "맞춘 값이 저장된다");
        }

        private static (RoomClock Clock, GameSession One, GameSession Two) SharedRoom(TempSaves saves, RoomClockSettings settings = null)
        {
            var clock = new RoomClock(TestWorld.Definitions.Get<LifeCalendar>(TestWorld.Settings.CalendarId), settings);
            clock.Join("player-1");
            clock.Join("player-2");
            var one = GameSession.Start(TestWorld.Definitions, TestWorld.Settings, saves.Store, "slot-room", "player-1", room: clock);
            var two = GameSession.Start(TestWorld.Definitions, TestWorld.Settings, saves.Store, "slot-room", "player-2", room: clock);
            return (clock, one, two);
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
            Assert.That(File.Exists(Path.Combine(saves.Path, slot, SaveFileNames.RoomClock)), Is.False, "혼자 하는 판은 시계 파일을 쓰지 않는다");
        }
    }
}
