using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Data.Builders;

namespace Rootborn.Data.Tests
{
    /// <summary>
    /// 흐르는 시간의 수치(하루 시작, 취침 시각, 밤 체력 감소, 늦잠 회복 감소)는 game/time.json, 하루 길이는 data/time의 TimeDefinition이다.
    /// data/는 변환기 결과라 고치지 않는다. 하루 길이가 있는 데이터(실제 게임)에 이 파일이 없거나 깨졌으면 읽기 오류다.
    /// </summary>
    public sealed class TimeSettingsTests
    {
        private const string TimeDefinition =
            "[{\"type\":\"TimeDefinition\",\"id\":\"Time_Default\",\"realSecondsPerGameDay\":300,\"startDay\":1,\"initialTimeScale\":1}]";

        private const string GoodFile =
            "{\"dayStart\":\"06:30\",\"bedtime\":\"23:00\",\"nightEnergyLossPerHour\":3,\"lateSleepRecoveryLossPerHour\":4}";

        [Test]
        public void RealData_HasTimeRules_WithTheDecidedFirstValues()
        {
            var result = GameDataLoader.Load(RepoPaths.DataDirectory, RootbornBuilders.Register);
            result.ThrowIfFailed();
            var rules = result.Definitions.TimeRules;
            Assert.That(rules, Is.Not.Null, "game/time.json");
            // 사용자 결정(2026-11-12, 설계 8절 2번 추천안): 하루 10분, 07:00 시작, 22:00 취침, 밤 1시간마다 1, 늦게 자면 1시간마다 2.
            Assert.That(rules.RealSecondsPerGameDay, Is.EqualTo(600f), "하루 길이는 data/time의 TimeDefinition");
            Assert.That((rules.DayStartMinutes, rules.BedtimeMinutes), Is.EqualTo((7 * 60, 22 * 60)));
            Assert.That((rules.NightEnergyLossPerHour, rules.LateSleepRecoveryLossPerHour), Is.EqualTo((1, 2)));
        }

        [Test]
        public void TheFile_AndTheDayLength_MakeTheRules()
        {
            using var dir = WithTime(TimeDefinition, GoodFile);
            var result = GameDataLoader.Load(Path.Combine(dir.Path, "data"), RootbornBuilders.Register);
            Assert.That(result.Errors, Is.Empty);
            var rules = result.Definitions.TimeRules;
            Assert.That(rules.RealSecondsPerGameDay, Is.EqualTo(300f));
            Assert.That((rules.DayStartMinutes, rules.BedtimeMinutes), Is.EqualTo((6 * 60 + 30, 23 * 60)));
            Assert.That((rules.NightEnergyLossPerHour, rules.LateSleepRecoveryLossPerHour), Is.EqualTo((3, 4)));
        }

        [Test]
        public void ABedtimeAfterMidnight_CountsAsTheSameNight()
        {
            using var dir = WithTime(TimeDefinition, GoodFile.Replace("23:00", "01:00"));
            var rules = GameDataLoader.Load(Path.Combine(dir.Path, "data"), RootbornBuilders.Register).Definitions.TimeRules;
            Assert.That(rules.BedtimeMinutes, Is.EqualTo(25 * 60));
        }

        [Test]
        public void DataWithADayLength_WithoutTheFile_IsALoadError()
        {
            using var dir = WithTime(TimeDefinition, null);
            var result = GameDataLoader.Load(Path.Combine(dir.Path, "data"), RootbornBuilders.Register);
            Assert.That(result.Ok, Is.False);
            Assert.That(result.Errors.Select(e => e.Source), Has.Some.Contains("time.json"));
            Assert.That(result.Definitions.TimeRules, Is.Null);
        }

        [Test]
        public void DataWithoutADayLength_NeedsNoFile()
        {
            using var dir = WithTime(null, null);
            var result = GameDataLoader.Load(Path.Combine(dir.Path, "data"), RootbornBuilders.Register);
            Assert.That(result.Errors, Is.Empty);
            Assert.That(result.Definitions.TimeRules, Is.Null);
        }

        [TestCase("{ not json")]
        [TestCase("{}")]
        [TestCase("{\"dayStart\":\"7시\",\"bedtime\":\"22:00\",\"nightEnergyLossPerHour\":1,\"lateSleepRecoveryLossPerHour\":2}")]
        [TestCase("{\"dayStart\":\"25:00\",\"bedtime\":\"22:00\",\"nightEnergyLossPerHour\":1,\"lateSleepRecoveryLossPerHour\":2}")]
        [TestCase("{\"dayStart\":\"07:00\",\"bedtime\":\"22:00\",\"nightEnergyLossPerHour\":-1,\"lateSleepRecoveryLossPerHour\":2}")]
        [TestCase("{\"dayStart\":\"07:00\",\"bedtime\":\"22:00\",\"nightEnergyLossPerHour\":1}")]
        [TestCase("{\"dayStart\":\"07:00\",\"bedtime\":\"07:00\",\"nightEnergyLossPerHour\":1,\"lateSleepRecoveryLossPerHour\":2}")]
        public void ABrokenFile_IsALoadError(string json)
        {
            using var dir = WithTime(TimeDefinition, json);
            var result = GameDataLoader.Load(Path.Combine(dir.Path, "data"), RootbornBuilders.Register);
            Assert.That(result.Ok, Is.False, json);
            Assert.That(result.Errors.Select(e => e.Source), Has.Some.Contains("time.json"));
            Assert.That(result.Definitions.TimeRules, Is.Null);
        }

        private static TempDir WithTime(string timeDefinition, string file)
        {
            var dir = new TempDir();
            var data = Path.Combine(dir.Path, "data");
            Directory.CreateDirectory(data);
            Directory.CreateDirectory(Path.Combine(dir.Path, "game"));
            File.WriteAllText(Path.Combine(data, "registry.json"), "{}");
            if (timeDefinition != null) File.WriteAllText(Path.Combine(data, "time.json"), timeDefinition);
            if (file != null) File.WriteAllText(Path.Combine(dir.Path, "game", "time.json"), file);
            return dir;
        }
    }
}
