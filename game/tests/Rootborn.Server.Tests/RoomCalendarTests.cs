using System;
using System.IO;
using NUnit.Framework;
using Rootborn.Core.StudentLife;
using Rootborn.Data;
using Rootborn.Data.Builders;
using Rootborn.Data.Save;
using Rootborn.Game;

namespace Rootborn.Server.Tests
{
    /// <summary>방의 시계는 게임 데이터의 달력(하루의 때 수)을 따른다. 자동 테스트.</summary>
    public sealed class RoomCalendarTests
    {
        private string _saveRoot;

        [SetUp]
        public void SetUp()
        {
            _saveRoot = Path.Combine(Path.GetTempPath(), "rootborn-room-calendar", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_saveRoot)) Directory.Delete(_saveRoot, true);
        }

        [Test]
        public void TheRoomClock_UsesTheCalendarItIsGiven()
        {
            var calendar = new LifeCalendar(weekdayBudget: 4, weekendBudget: 5);
            using var rig = new RoomRig(new FileSaveStore(_saveRoot), calendar: calendar);
            Assert.That(rig.Room.Clock.Day, Is.EqualTo(1));
            Assert.That(rig.Room.Clock.PartsToday, Is.EqualTo(4), "1일째는 평일 — 주어진 달력의 평일 때 수");
        }

        [Test]
        public void TheServer_TakesTheCalendarFromTheGameData()
        {
            var load = GameDataLoader.Load(Path.Combine(ServerProcessTests.RepoRoot(), "data"), RootbornBuilders.Register);
            load.ThrowIfFailed();
            var expected = load.Definitions.Get<LifeCalendar>(GameStartSettings.FromDefinitions(load.Definitions).CalendarId);
            Assert.That(Program.RoomCalendar(load.Definitions), Is.SameAs(expected), "게임 한 판과 같은 달력 정의");
        }
    }
}
