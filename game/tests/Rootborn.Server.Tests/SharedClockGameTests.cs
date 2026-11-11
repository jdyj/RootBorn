using System;
using System.IO;
using NUnit.Framework;
using Rootborn.Core.StudentLife;
using Rootborn.Data;
using Rootborn.Data.Builders;
using Rootborn.Data.Save;
using Rootborn.Game;
using Rootborn.Net.Session;

namespace Rootborn.Server.Tests
{
    /// <summary>
    /// 방 하나(RoomServer, 실제 data/의 달력) + 게임 한 판 둘(GameSession, 실제 data/)을 같은 프로세스 안 전송으로 잇는다.
    /// 각 판은 RemoteRoomTime으로 방의 시계를 따르고, 시계가 바뀌면 SyncWithRoom을 부른다(Host가 할 일을 테스트가 한다).
    /// GameSession 명령만 부르는 자동 테스트다(화면·입력 근거 아님). 손님 저장을 서버로 보내는 일은 아직 없어 판마다 자기 저장소를 쓴다.
    /// </summary>
    public sealed class SharedClockGameTests
    {
        private const string LibraryStudy = "outside.self-study.library";
        private static GameDefinitions _definitions;
        private static GameStartSettings _settings;
        private string _root;

        [OneTimeSetUp]
        public void LoadData()
        {
            var load = GameDataLoader.Load(Path.Combine(ServerProcessTests.RepoRoot(), "data"), RootbornBuilders.Register);
            load.ThrowIfFailed();
            _definitions = load.Definitions;
            _settings = GameStartSettings.FromDefinitions(_definitions);
        }

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "rootborn-shared-clock", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void OneActing_DoesNotMoveThePart_UntilTheOtherFinishes_ThenBothSeeTheSameHud()
        {
            using var rig = NewRoom();
            var one = Play(rig, "one");
            var two = Play(rig, "two");
            rig.Run(0.5);
            Assert.That(one.Game.Room.PartsToday, Is.EqualTo(one.Game.Student.DailyTimePointBudget), "서버의 달력과 게임의 달력이 같다");

            Assert.That(one.Game.PerformOutsideSchoolActivity(LibraryStudy, "one:1").Kind, Is.EqualTo(LifeActivityResultKind.Applied));
            rig.Run(0.5);
            Assert.That(rig.Room.Clock.PartIndex, Is.EqualTo(0), "two가 끝내기 전에는 때가 안 넘어간다");
            Assert.That(two.Game.Room.PartIndex, Is.EqualTo(0));
            Assert.That(one.Game.PerformOutsideSchoolActivity(LibraryStudy, "one:2").Kind, Is.Not.EqualTo(LifeActivityResultKind.Applied),
                "끝낸 사람은 기다린다");
            Assert.That(one.Game.Student.TimePointsSpentToday, Is.EqualTo(1));

            Assert.That(two.Game.SkipPart(two.Game.Room.PartIndex), Is.True, "two는 넘긴다");
            rig.Run(0.5);
            Assert.That(rig.Room.Clock.PartIndex, Is.EqualTo(1));
            AssertSameHud(one.Game, two.Game);
            Assert.That(one.Game.RoomTimeMinutes, Is.EqualTo(one.Game.Student.Calendar.PartTimes.MinutesFor(1, one.Game.Room.PartsToday)));
            Assert.That(one.Game.Student.TimePointsRemaining, Is.EqualTo(two.Game.Student.TimePointsRemaining));
        }

        [Test]
        public void WhenOneLeaves_ThePartMovesOnWithoutThem()
        {
            using var rig = NewRoom();
            var one = Play(rig, "one");
            var two = Play(rig, "two");
            rig.Run(0.5);
            one.Game.PerformOutsideSchoolActivity(LibraryStudy, "one:leave");
            rig.Run(0.5);
            Assert.That(rig.Room.Clock.PartIndex, Is.EqualTo(0));

            rig.Quit(two.Client);
            rig.Run(0.5);
            Assert.That(rig.Room.Clock.PartIndex, Is.EqualTo(1), "나간 사람은 기다리지 않는다");
            Assert.That(one.Game.Room.PartIndex, Is.EqualTo(1));
            Assert.That(one.Game.PerformOutsideSchoolActivity(LibraryStudy, "one:after").Kind, Is.EqualTo(LifeActivityResultKind.Applied));
        }

        [Test]
        public void EndingTheDay_TheNextDayStartsOnlyWhenBothEnded()
        {
            using var rig = NewRoom();
            var one = Play(rig, "one");
            var two = Play(rig, "two");
            rig.Run(0.5);

            Assert.That(one.Game.EndDay().Ended, Is.True);
            rig.Run(0.5);
            Assert.That(one.Game.CanStartNextDay, Is.False);
            Assert.That(one.Game.StartNextDay(), Is.False, "two가 마감하기 전에는 다음 날을 시작하지 않는다");
            Assert.That(rig.Room.Clock.Day, Is.EqualTo(1));

            Assert.That(two.Game.EndDay().Ended, Is.True);
            rig.Run(0.5);
            Assert.That(rig.Room.Clock.Day, Is.EqualTo(2));
            Assert.That(one.Game.StartNextDay(), Is.True);
            Assert.That(two.Game.StartNextDay(), Is.True);
            Assert.That(one.Game.Student.CurrentDay, Is.EqualTo(2));
            AssertSameHud(one.Game, two.Game);
            Assert.That(one.Game.PerformOutsideSchoolActivity(LibraryStudy, "one:day2").Kind, Is.EqualTo(LifeActivityResultKind.Applied));
        }

        [Test]
        public void AfterSavingAndRestartingTheServer_TheClockAndBothGamesContinue()
        {
            var roomStore = new FileSaveStore(Path.Combine(_root, "room"));
            using (var rig = NewRoom(roomStore))
            {
                var one = Play(rig, "one");
                var two = Play(rig, "two");
                rig.Run(0.5);
                one.Game.PerformOutsideSchoolActivity(LibraryStudy, "one:a");
                two.Game.PerformOutsideSchoolActivity(LibraryStudy, "two:a");
                rig.Run(0.5);
                Assert.That(rig.Room.Clock.PartIndex, Is.EqualTo(1));
                rig.Room.Shutdown();
            }

            using var again = NewRoom(roomStore);
            Assert.That(again.Room.IsNewRoom, Is.False);
            Assert.That((again.Room.Clock.Day, again.Room.Clock.PartIndex), Is.EqualTo((1, 1)), "저장한 방의 시계에서 이어진다");
            var oneAgain = Play(again, "one");
            var twoAgain = Play(again, "two");
            again.Run(0.5);
            Assert.That(oneAgain.Game.Student.TimePointsSpentToday, Is.EqualTo(1), "개인 진행도 이어진다");
            Assert.That(oneAgain.Game.Room.PartIndex, Is.EqualTo(1));
            Assert.That(oneAgain.Game.PerformOutsideSchoolActivity(LibraryStudy, "one:b").Kind, Is.EqualTo(LifeActivityResultKind.Applied));
            Assert.That(twoAgain.Game.SkipPart(1), Is.True);
            again.Run(0.5);
            Assert.That(again.Room.Clock.IsDayOver, Is.True);
            AssertSameHud(oneAgain.Game, twoAgain.Game);
        }

        private RoomRig NewRoom(FileSaveStore store = null) =>
            new RoomRig(store ?? new FileSaveStore(Path.Combine(_root, "room")), calendar: Program.RoomCalendar(_definitions));

        private (SessionClient Client, GameSession Game) Play(RoomRig rig, string playerId)
        {
            var client = rig.Join(playerId);
            var room = new RemoteRoomTime(client, playerId, _definitions.Get<LifeCalendar>(_settings.CalendarId));
            var store = new FileSaveStore(Path.Combine(_root, "player-" + playerId));
            var game = GameSession.Start(_definitions, _settings, store, "slot-shared", playerId, room: room);
            room.Changed += () => game.SyncWithRoom();
            return (client, game);
        }

        private static void AssertSameHud(GameSession a, GameSession b)
        {
            Assert.That((a.Room.Day, a.Room.PartIndex, a.Room.PartsToday, a.RoomTimeMinutes),
                Is.EqualTo((b.Room.Day, b.Room.PartIndex, b.Room.PartsToday, b.RoomTimeMinutes)), "둘의 HUD 값(날, 때, 대표 시각)이 같다");
        }
    }
}
