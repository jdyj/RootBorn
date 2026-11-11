using System;
using System.IO;
using NUnit.Framework;
using Rootborn.Core.Time;
using Rootborn.Data;
using Rootborn.Data.Builders;
using Rootborn.Data.Save;

namespace Rootborn.Server.Tests
{
    /// <summary>방의 시계는 게임 데이터의 시간 규칙(하루 길이, 하루 시작)을 따른다. 자동 테스트.</summary>
    public sealed class RoomTimeRulesTests
    {
        private string _saveRoot;

        [SetUp]
        public void SetUp()
        {
            _saveRoot = Path.Combine(Path.GetTempPath(), "rootborn-room-time", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_saveRoot)) Directory.Delete(_saveRoot, true);
        }

        [Test]
        public void TheRoomClock_UsesTheRulesItIsGiven_AndFlowsOnlyWithSomeoneIn()
        {
            var rules = new TimeRules(240f, 6 * 60, 22 * 60, 1, 2); // 하루 240초 = 게임 1분이 실제 1/6초
            using var rig = new RoomRig(new FileSaveStore(_saveRoot), timeRules: rules);
            Assert.That((rig.Room.Clock.Day, rig.Room.Clock.Minutes), Is.EqualTo((1, 6 * 60)), "주어진 규칙의 하루 시작");
            rig.Run(5);
            Assert.That(rig.Room.Clock.Minutes, Is.EqualTo(6 * 60), "아무도 없으면 멈춘다");
            rig.Join("a");
            rig.Run(10);
            Assert.That(rig.Room.Clock.Minutes - 6 * 60, Is.InRange(58, 60), "실제 10초 = 게임 60분");
        }

        [Test]
        public void WithoutTimeRules_TheClockDoesNotFlow()
        {
            using var rig = new RoomRig(new FileSaveStore(_saveRoot));
            rig.Join("a");
            rig.Run(5);
            Assert.That(rig.Room.Clock.Minutes, Is.EqualTo(TimeRules.Legacy.DayStartMinutes), "시간 규칙이 없는 시험 데이터: 날만 센다");
        }

        [Test]
        public void TheServer_TakesTheTimeRulesFromTheGameData()
        {
            var load = GameDataLoader.Load(Path.Combine(ServerProcessTests.RepoRoot(), "data"), RootbornBuilders.Register);
            load.ThrowIfFailed();
            Assert.That(Program.RoomTimeRules(load.Definitions), Is.SameAs(load.Definitions.TimeRules), "게임 한 판과 같은 시간 규칙");
            Assert.That(Program.RoomTimeRules(load.Definitions).DayStartMinutes, Is.EqualTo(7 * 60));
        }
    }
}
