using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;
using Rootborn.Core.Time;
using Rootborn.Data;
using Rootborn.Data.Builders;
using Rootborn.Data.Save;
using Rootborn.Game;
using Rootborn.Net.Session;

namespace Rootborn.Server.Tests
{
    /// <summary>
    /// 흐르는 시간 4단계(설계 3.1~3.4절): 방 하나(RoomServer, 실제 data/의 시간 규칙) + 게임 한 판 둘(GameSession, 실제 data/)을 같은 프로세스 안 전송으로 잇는다.
    /// 각 판은 RemoteRoomTime으로 방의 시계를 따르고, 틱마다 AdvanceTime(Host가 프레임마다 하는 일)을, 시계가 바뀌면 SyncWithRoom을 부른다.
    /// 서버 틱은 손으로 돌리는 시계로 돈다(실시간을 기다리지 않는다). GameSession 명령만 부르는 자동 테스트다(화면·입력 근거 아님).
    /// 손님 저장은 판마다 자기 저장소를 쓴다.
    /// </summary>
    public sealed class SharedClockGameTests
    {
        private const string LibraryStudy = "outside.self-study.library";
        private const string HelpNeighbor = "outside.help-neighbor";
        private static GameDefinitions _definitions;
        private static GameStartSettings _settings;
        private static TimeRules _rules;
        private string _root;
        private readonly List<GameSession> _games = new List<GameSession>();

        [OneTimeSetUp]
        public void LoadData()
        {
            var load = GameDataLoader.Load(Path.Combine(ServerProcessTests.RepoRoot(), "data"), RootbornBuilders.Register);
            load.ThrowIfFailed();
            _definitions = load.Definitions;
            _settings = GameStartSettings.FromDefinitions(_definitions);
            _rules = _definitions.TimeRules;
            Assert.That(_rules, Is.Not.Null, "실제 데이터에 흐르는 시간 규칙이 있다");
        }

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "rootborn-shared-clock", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _games.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void BothGames_SeeTheSameTime_AndItKeepsFlowing_EvenWithAMenuOpen()
        {
            using var rig = NewRoom();
            var one = Play(rig, "one");
            var two = Play(rig, "two");
            Run(rig, 1);
            int start = rig.Room.Clock.Minutes;
            Assert.That(start, Is.EqualTo(_rules.DayStartMinutes).Within(5), "새 방은 1일째 07:00");

            Run(rig, 50, menuOpen: true); // 실제 50초 = 게임 2시간. 같이 하기는 메뉴가 열려 있어도 멈추지 않는다.
            int room = rig.Room.Clock.Minutes;
            Assert.That(room - start, Is.InRange(118, 122));
            foreach (var game in new[] { one.Game, two.Game })
            {
                Assert.That(game.StudentTimeMinutes, Is.InRange(room - 2 * RemoteRoomTime.ResyncMinutes, room), "개인 시계가 방의 시각을 따른다");
                Assert.That(game.RoomShownMinutes, Is.EqualTo(TimeRules.ClockMinutes(game.Room.Minutes)));
            }

            Assert.That(Math.Abs(one.Game.StudentTimeMinutes - two.Game.StudentTimeMinutes), Is.LessThanOrEqualTo(1), "둘이 같은 시각");
            Assert.That(one.Game.Student.CurrentDay, Is.EqualTo(two.Game.Student.CurrentDay));
        }

        [Test]
        public void OneActs_OnlyThatPlayerIsBusy_AndTheClockKeepsFlowing()
        {
            using var rig = NewRoom();
            var one = Play(rig, "one");
            var two = Play(rig, "two");
            Run(rig, 1);
            int before = rig.Room.Clock.Minutes;
            int cost = _definitions.Get<OutsideSchoolActivity>(LibraryStudy).TimeCostMinutes;

            Assert.That(one.Game.PerformOutsideSchoolActivity(LibraryStudy, "one:1").Kind, Is.EqualTo(LifeActivityResultKind.Applied));
            Assert.That(rig.Room.Clock.Minutes, Is.EqualTo(before), "같이 할 때 활동은 방의 시계를 건너뛰지 않는다");
            Assert.That(one.Game.StudentTimeMinutes, Is.LessThan(before + cost), "개인 시계도 건너뛰지 않는다");
            Assert.That(one.Game.IsBusy, Is.True, "그 사람만 하는 중");
            Assert.That(one.Game.ActivityEndsAtMinutes, Is.EqualTo(one.Game.StudentTimeMinutes + cost));
            Assert.That(two.Game.IsBusy, Is.False);

            Assert.That(one.Game.PerformOutsideSchoolActivity(HelpNeighbor, "one:2").Kind, Is.Not.EqualTo(LifeActivityResultKind.Applied), "하는 중에는 다른 활동을 시작하지 못한다");
            Assert.That(StudentActivityRefusal.Explain(one.Game.Student, null, 0, 0), Is.EqualTo(StudentActivityRefusalReason.Busy));
            Assert.That(two.Game.PerformOutsideSchoolActivity(HelpNeighbor, "two:1").Kind, Is.EqualTo(LifeActivityResultKind.Applied), "다른 사람은 할 수 있다");

            Run(rig, cost * SecondsPerMinute + 3);
            Assert.That(rig.Room.Clock.Minutes - before, Is.GreaterThanOrEqualTo(cost), "시계는 그대로 흘렀다");
            Assert.That(one.Game.IsBusy, Is.False, "끝나는 시각이 지나면 다시 할 수 있다");
            Assert.That(one.Game.PerformOutsideSchoolActivity(HelpNeighbor, "one:3").Kind, Is.EqualTo(LifeActivityResultKind.Applied));
        }

        [Test]
        public void BothSleep_TheRoomGoesToTheNextDay_0700_AtOnce()
        {
            using var rig = NewRoom(startMinutes: 21 * 60);
            var one = Play(rig, "one");
            var two = Play(rig, "two");
            Run(rig, 1);
            Assert.That(one.Game.Sleep().Ended, Is.True);
            Run(rig, 1);
            Assert.That(rig.Room.Clock.Day, Is.EqualTo(1), "한 사람만 자면 그대로");
            Assert.That(one.Game.IsSleepingInRoom, Is.True);
            Assert.That(one.Game.CanStartNextDay, Is.False);

            Assert.That(two.Game.Sleep().Ended, Is.True);
            Assert.That(two.Game.Room.Day, Is.EqualTo(1), "보낸 잠은 방이 셀 때까지 그 날");
            Run(rig, 0.1);
            Assert.That(rig.Room.Clock.Day, Is.EqualTo(2), "모두 자면 곧바로 다음 날");
            Assert.That(rig.Room.Clock.Minutes, Is.EqualTo(_rules.DayStartMinutes).Within(1), "07:00(방금 흐른 틱만큼)");
            Assert.That(one.Game.StartNextDay(), Is.True);
            Assert.That(two.Game.StartNextDay(), Is.True);
            Assert.That(one.Game.Student.CurrentDay, Is.EqualTo(2));
            Assert.That(one.Game.Student.Energy, Is.EqualTo(two.Game.Student.Energy));
            Assert.That(one.Game.StudentTimeMinutes, Is.EqualTo(_rules.DayStartMinutes).Within(2));
        }

        [Test]
        public void OneSleeps_TheClockFlowsUntil0700_ThenBothGetTheNextDay_TheAwakeOneWithoutRecovery()
        {
            using var rig = NewRoom(startMinutes: 21 * 60);
            var one = Play(rig, "one");
            var two = Play(rig, "two");
            Run(rig, 1);
            int full = one.Game.Student.Energy;
            Assert.That(one.Game.Sleep().Ended, Is.True, "취침 시각 전에 잔다");
            var awake = new List<string>();
            one.Game.RoomAwakeOthers(awake);
            Assert.That(awake, Is.EqualTo(new[] { "two" }), "자는 중 화면: 아직 깨어 있는 사람");

            int twoBefore = two.Game.Student.Energy;
            Run(rig, 4 * 60 * SecondsPerMinute); // 01:00까지
            Assert.That(rig.Room.Clock.Day, Is.EqualTo(1));
            Assert.That(TimeRules.ClockMinutes(two.Game.StudentTimeMinutes), Is.EqualTo(60).Within(RemoteRoomTime.ResyncMinutes));
            Assert.That(two.Game.Student.Energy, Is.LessThan(twoBefore), "취침 시각 뒤 깨어 있으면 체력이 준다");
            Assert.That(one.Game.CanStartNextDay, Is.False, "자는 중에는 깨어날 수 없다(07:00까지)");

            Run(rig, 6 * 60 * SecondsPerMinute + 5); // 07:00을 넘긴다
            Assert.That((rig.Room.Clock.Day, rig.Room.Clock.Minutes / 60), Is.EqualTo((2, 7)), "깨어 있는 사람이 있으면 07:00에 다음 날");
            Assert.That(two.Game.Student.CurrentDay, Is.EqualTo(2), "깨어 있던 사람은 자지 않은 날로 넘어간다");
            Assert.That(two.Game.Student.DayState, Is.EqualTo(StudentDayState.InProgress));
            Assert.That(two.Game.Student.LastNight.HasSlept, Is.False);
            Assert.That(two.Game.Student.Energy, Is.LessThanOrEqualTo(twoBefore - 9), "회복 없음(22:00~07:00 밤 감소 9)");

            Assert.That(one.Game.CanStartNextDay, Is.True);
            Assert.That(one.Game.StartNextDay(), Is.True);
            Assert.That(one.Game.Student.Energy, Is.EqualTo(full), "제때 잔 사람은 다 회복");
            Assert.That(one.Game.Student.Energy, Is.GreaterThan(two.Game.Student.Energy), "밤샘이 이득이 아니다");
        }

        [Test]
        public void LeavingAndComingBackMidday_StartsAtTheRoomsTime_AndALaterDayIsARestDay()
        {
            var roomStore = new FileSaveStore(Path.Combine(_root, "room"));
            using var rig = NewRoom(store: roomStore);
            var one = Play(rig, "one");
            var two = Play(rig, "two");
            Run(rig, 1);
            Assert.That(two.Game.PerformOutsideSchoolActivity(HelpNeighbor, "two:before").Kind, Is.EqualTo(LifeActivityResultKind.Applied));
            int left = two.Game.StudentTimeMinutes;
            Leave(rig, two);

            Run(rig, 3 * 60 * SecondsPerMinute);
            var back = Play(rig, "two");
            Run(rig, 1);
            Assert.That(back.Game.Student.CurrentDay, Is.EqualTo(1));
            Assert.That(back.Game.StudentTimeMinutes, Is.EqualTo(rig.Room.Clock.Minutes).Within(RemoteRoomTime.ResyncMinutes), "낮 도중에 들어오면 방의 시각에서");
            Assert.That(back.Game.StudentTimeMinutes - left, Is.GreaterThanOrEqualTo(170));
            Assert.That(back.Game.Student.GetTodayActivityIds(), Does.Contain(HelpNeighbor), "그날 한 것은 그대로");

            Leave(rig, back);
            Assert.That(one.Game.Sleep().Ended, Is.True);
            Run(rig, 0.5);
            Assert.That(rig.Room.Clock.Day, Is.EqualTo(2), "혼자 남은 사람이 자면 곧바로 다음 날");
            var later = Play(rig, "two");
            Run(rig, 1);
            Assert.That(later.Game.Student.CurrentDay, Is.EqualTo(2), "방에 없던 날은 쉰 날");
            Assert.That(later.Game.Student.DayState, Is.EqualTo(StudentDayState.InProgress));
        }

        [Test]
        public void AfterSavingAndRestartingTheServer_TheTimeContinues()
        {
            var roomStore = new FileSaveStore(Path.Combine(_root, "room"));
            int savedDay, savedMinutes;
            using (var rig = NewRoom(store: roomStore))
            {
                var one = Play(rig, "one");
                Run(rig, 90 * SecondsPerMinute);
                Assert.That(one.Game.PerformOutsideSchoolActivity(LibraryStudy, "one:a").Kind, Is.EqualTo(LifeActivityResultKind.Applied));
                savedDay = rig.Room.Clock.Day;
                savedMinutes = rig.Room.Clock.Minutes;
                rig.Room.Shutdown();
            }

            using var again = NewRoom(store: roomStore);
            Assert.That(again.Room.IsNewRoom, Is.False);
            Assert.That((again.Room.Clock.Day, again.Room.Clock.Minutes), Is.EqualTo((savedDay, savedMinutes)), "저장한 방의 시각에서 이어진다");
            Run(again, 10);
            Assert.That(again.Room.Clock.Minutes, Is.EqualTo(savedMinutes), "아무도 없으면 멈춘다");
            var oneAgain = Play(again, "one");
            Run(again, 1);
            Assert.That(oneAgain.Game.Student.GetTodayActivityIds(), Does.Contain(LibraryStudy), "개인 진행도 이어진다");
            Assert.That(oneAgain.Game.IsBusy, Is.True, "하는 중도 이어진다(끝나는 시각은 개인 시계 파일에)");
            Run(again, 10);
            Assert.That(again.Room.Clock.Minutes, Is.GreaterThan(savedMinutes), "들어오면 다시 흐른다");
        }

        [Test]
        public void AnOldPartsRoomClockFile_IsReadAsThatDaysDayStart()
        {
            var roomStore = new FileSaveStore(Path.Combine(_root, "room"));
            const string fixture = "{\"Version\":1,\"Day\":3,\"PartIndex\":1,\"Participants\":[{\"ParticipantId\":\"one\",\"CompletedParts\":1,\"DayEnded\":false}]}";
            string slotFolder = Path.Combine(_root, "room", "room-a");
            Directory.CreateDirectory(slotFolder);
            File.WriteAllText(Path.Combine(slotFolder, SaveFileNames.RoomClock), fixture);

            using var rig = NewRoom(store: roomStore);
            Assert.That(rig.Room.IsNewRoom, Is.False);
            Assert.That((rig.Room.Clock.Day, rig.Room.Clock.Minutes), Is.EqualTo((3, _rules.DayStartMinutes)), "형식 1(때)의 방은 그 날의 07:00");
            var one = Play(rig, "one");
            Run(rig, 1);
            Assert.That(one.Game.Student.CurrentDay, Is.EqualTo(3), "새 손님은 쉰 날로 방의 날까지");
            rig.Room.Shutdown();
            Assert.That(roomStore.TryRead<RoomClockSaveData>("room-a", SaveFileNames.RoomClock, out var written), Is.True);
            Assert.That(written.Version, Is.EqualTo(RoomClockSaveData.KnownVersion), "다시 쓸 때는 새 형식");
        }

        // ---------- 돕는 것 ----------

        private static readonly float SecondsPerMinute = 600f / TimeRules.MinutesPerDay; // 바뀌면 LoadData가 알린다

        private RoomRig NewRoom(FileSaveStore store = null, int startMinutes = -1)
        {
            Assert.That(_rules.RealSecondsPerGameDay, Is.EqualTo(600f), "이 테스트의 시간 계산은 하루 600초를 가정한다");
            store ??= new FileSaveStore(Path.Combine(_root, "room"));
            if (startMinutes >= 0)
                store.Write("room-a", SaveFileNames.RoomClock, new RoomClock(_rules, 1, startMinutes).ToSaveData());
            return new RoomRig(store, timeRules: Program.RoomTimeRules(_definitions));
        }

        private (SessionClient Client, GameSession Game) Play(RoomRig rig, string playerId)
        {
            var client = rig.Join(playerId);
            rig.Run(0.2);
            var room = new RemoteRoomTime(client, playerId, _rules);
            var store = new FileSaveStore(Path.Combine(_root, "player-" + playerId));
            var game = GameSession.Start(_definitions, _settings, store, "slot-shared", playerId, room: room);
            room.Changed += () => game.SyncWithRoom();
            _games.Add(game);
            return (client, game);
        }

        private void Leave(RoomRig rig, (SessionClient Client, GameSession Game) player)
        {
            _games.Remove(player.Game);
            rig.Quit(player.Client);
            rig.Run(0.2);
        }

        private void Run(RoomRig rig, double seconds, bool menuOpen = false)
            => rig.Run(seconds, dt => { foreach (var game in _games.ToArray()) game.AdvanceTime(dt, menuOpen); });
    }
}
