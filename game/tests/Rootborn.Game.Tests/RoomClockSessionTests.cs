using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;
using Rootborn.Core.Time;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 게임 한 판이 방의 시계(흐르는 시간 4단계)로 돈다. 혼자 하기는 혼자인 방(기본값, 개인 시계가 흐르고 활동은 시계를 건너뛴다)이고,
    /// 같은 프로세스에서 Core 방의 시계 하나를 두 판이 같이 쓰면 개인 시계가 방의 시각을 따라가고 활동은 "하는 중"이다.
    /// 방의 시계는 테스트가 손으로 돌린다(서버의 틱 대신). GameSession 명령만 부르는 자동 테스트다(화면 근거 아님).
    /// </summary>
    public sealed class RoomClockSessionTests
    {
        private const string LibraryStudy = "outside.self-study.library";
        private const string HelpNeighbor = "outside.help-neighbor";
        private static TimeRules Rules => TestWorld.Definitions.TimeRules;

        [Test]
        public void Alone_TheRoomFollowsTheDay_AndAnActivitySkipsTheClock()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            Assert.That(game.IsSoloRoom, Is.True, "기본값은 혼자인 방");
            Assert.That(game.Room.Day, Is.EqualTo(game.Student.CurrentDay));
            int before = game.StudentTimeMinutes;
            int cost = TestWorld.Definitions.Get<OutsideSchoolActivity>(LibraryStudy).TimeCostMinutes;

            Assert.That(game.PerformOutsideSchoolActivity(LibraryStudy, "outside:room:1").Kind, Is.EqualTo(LifeActivityResultKind.Applied));
            Assert.That(game.StudentTimeMinutes, Is.EqualTo(before + cost), "혼자면 시계가 끝나는 시각으로 건너뛴다");
            Assert.That(game.IsBusy, Is.False);
            Assert.That(game.RoomShownMinutes, Is.EqualTo(game.ClockMinutes), "HUD는 개인 시계");
        }

        [Test]
        public void Alone_Sleeping_IsTheRoomsNextDay_AndTheNextDayStarts()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            game.TalkTo("npc.first-guide");
            int day = game.Student.CurrentDay;

            Assert.That(game.Sleep().Ended, Is.True);
            Assert.That(game.Room.Day, Is.EqualTo(day + 1), "혼자인 방은 자는 순간 다음 날");
            Assert.That(game.IsSleepingInRoom, Is.False, "혼자면 기다리지 않는다");

            game = TestWorld.Start(saves.Store);
            Assert.That(game.Room.Day, Is.EqualTo(day + 1), "다시 읽어도 개인 진행에서 같은 방이 나온다");
            Assert.That(game.StartNextDay(), Is.True);
            Assert.That(game.Student.CurrentDay, Is.EqualTo(game.Room.Day));
        }

        [Test]
        public void Shared_BothFollowTheRoomsTime_JoiningLateDoesNotChargeThePastNight()
        {
            using var saves = new TempSaves();
            var (clock, one, two) = SharedRoom(saves);
            clock.Advance(90);
            Tick(one, two);
            Assert.That(one.StudentTimeMinutes, Is.EqualTo(clock.Minutes));
            Assert.That(two.StudentTimeMinutes, Is.EqualTo(clock.Minutes));
            Assert.That(one.RoomShownMinutes, Is.EqualTo(TimeRules.ClockMinutes(clock.Minutes)));

            var (lateClock, late, _) = SharedRoom(saves, "slot-late", startMinutes: 25 * 60);
            int energy = late.Student.Energy;
            Tick(late);
            Assert.That(late.StudentTimeMinutes, Is.EqualTo(25 * 60), "01:00의 방에 들어오면 그 시각에서");
            Assert.That(late.Student.Energy, Is.EqualTo(energy), "방에 없던 밤 시간은 거슬러 깎지 않는다");
            lateClock.Advance(60);
            Tick(late);
            Assert.That(late.Student.Energy, Is.EqualTo(energy - Rules.NightEnergyLossPerHour), "들어온 뒤 깨어 있는 밤은 깎는다");
        }

        [Test]
        public void Shared_AnActivityKeepsThatPlayerBusy_WithoutSkippingTheClock()
        {
            using var saves = new TempSaves();
            var (clock, one, two) = SharedRoom(saves);
            Tick(one, two);
            int start = one.StudentTimeMinutes;
            int cost = TestWorld.Definitions.Get<OutsideSchoolActivity>(LibraryStudy).TimeCostMinutes;
            Assert.That(one.PerformOutsideSchoolActivity(LibraryStudy, "one:study").Kind, Is.EqualTo(LifeActivityResultKind.Applied));
            Assert.That(one.StudentTimeMinutes, Is.EqualTo(start), "같이 하면 시계를 건너뛰지 않는다");
            Assert.That((one.IsBusy, one.ActivityEndsAtMinutes), Is.EqualTo((true, start + cost)));
            Assert.That(one.PerformOutsideSchoolActivity(HelpNeighbor, "one:help").Kind, Is.Not.EqualTo(LifeActivityResultKind.Applied), "하는 중");
            Assert.That(two.PerformOutsideSchoolActivity(HelpNeighbor, "two:help").Kind, Is.EqualTo(LifeActivityResultKind.Applied), "다른 사람은 된다");

            clock.Advance(cost);
            Tick(one, two);
            Assert.That(one.IsBusy, Is.False);
            Assert.That(one.PerformOutsideSchoolActivity(HelpNeighbor, "one:help").Kind, Is.EqualTo(LifeActivityResultKind.Applied));
        }

        [Test]
        public void Shared_StartNextDay_WaitsUntilEveryoneSleeps()
        {
            using var saves = new TempSaves();
            var (clock, one, two) = SharedRoom(saves);
            Tick(one, two);
            Assert.That(one.Sleep().Ended, Is.True);
            Assert.That(one.IsSleepingInRoom, Is.True);
            Assert.That(one.StartNextDay(), Is.False, "다른 사람이 깨어 있으면 07:00까지 깨어날 수 없다");
            var awake = new List<string>();
            one.RoomAwakeOthers(awake);
            Assert.That(awake, Is.EqualTo(new[] { "player-2" }));

            Assert.That(two.Sleep().Ended, Is.True);
            Assert.That(clock.Day, Is.EqualTo(2));
            Assert.That(clock.Minutes, Is.EqualTo(Rules.DayStartMinutes));
            Assert.That(one.StartNextDay(), Is.True);
            Assert.That(two.StartNextDay(), Is.True);
            Tick(one, two);
            Assert.That(one.StudentTimeMinutes, Is.EqualTo(Rules.DayStartMinutes));
        }

        [Test]
        public void Shared_AwakeAtTheDayEnd_RollsOverWithoutRecovery_TheSleeperRecovers()
        {
            using var saves = new TempSaves();
            var (clock, one, two) = SharedRoom(saves, startMinutes: 21 * 60);
            Tick(one, two);
            int full = one.Student.Energy;
            Assert.That(one.Sleep().Ended, Is.True);

            for (int i = 0; i < 10 * 60 && clock.Day == 1; i += 10)
            {
                clock.Advance(10);
                Tick(one, two);
            }

            Assert.That((clock.Day, clock.Minutes), Is.EqualTo((2, Rules.DayStartMinutes)), "07:00에 다음 날");
            Assert.That(two.Student.CurrentDay, Is.EqualTo(2), "깨어 있던 사람은 곧바로 다음 날");
            Assert.That(two.Student.LastNight.HasSlept, Is.False);
            Assert.That(two.Student.Energy, Is.EqualTo(full - 9 * Rules.NightEnergyLossPerHour), "회복 없음(22:00~07:00)");
            Assert.That(one.StartNextDay(), Is.True);
            Assert.That(one.Student.Energy, Is.EqualTo(full));

            var reread = GameSession.Start(TestWorld.Definitions, TestWorld.Settings, saves.Store, "slot-room", "player-2", room: clock);
            Assert.That((reread.Student.CurrentDay, reread.Student.Energy), Is.EqualTo((2, two.Student.Energy)), "새 판으로 다시 읽어도 같다");
        }

        [TestCaseSource(typeof(UnityFixtureSlotsTests), nameof(UnityFixtureSlotsTests.Slots))]
        public void OldSaves_WithoutAClock_MakeTheRoomFromPersonalProgress(string fixture, string slot)
        {
            using var saves = new TempSaves();
            saves.CopyFixture(fixture, slot);
            var game = TestWorld.Start(saves.Store, slot);
            bool ended = game.Student.DayState != StudentDayState.InProgress;
            Assert.That(game.Room.Day, Is.EqualTo(ended ? game.Student.CurrentDay + 1 : game.Student.CurrentDay));
            Assert.That(File.Exists(Path.Combine(saves.Path, slot, SaveFileNames.RoomClock)), Is.False, "혼자 하는 판은 시계 파일을 쓰지 않는다");
        }

        private static (RoomClock Clock, GameSession One, GameSession Two) SharedRoom(TempSaves saves, string slot = "slot-room", int startMinutes = -1)
        {
            var clock = new RoomClock(Rules, 1, startMinutes);
            clock.Join("player-1");
            clock.Join("player-2");
            var one = GameSession.Start(TestWorld.Definitions, TestWorld.Settings, saves.Store, slot, "player-1", room: clock);
            var two = GameSession.Start(TestWorld.Definitions, TestWorld.Settings, saves.Store, slot, "player-2", room: clock);
            return (clock, one, two);
        }

        // Host가 프레임마다 하는 일: 같이 하는 판은 방의 시각을 따라간다(실제 초는 쓰지 않는다).
        private static void Tick(params GameSession[] games)
        {
            foreach (var game in games) game.AdvanceTime(0.1f, false);
        }
    }
}
