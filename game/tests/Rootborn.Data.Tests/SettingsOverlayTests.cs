using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Data.Builders;

namespace Rootborn.Data.Tests
{
    /// <summary>
    /// 규칙 설정 덮어쓰기(시뮬레이션 테스트용): 덮어쓰기 폴더 맨 위의 economy.json·time.json은 game/의 같은 이름 파일 위에 최상위 필드 단위로 덮인다.
    /// 바뀐 필드는 덮어쓰기 목록에 "settings/<파일>"로 든다. 원본 파일은 건드리지 않는다.
    /// </summary>
    public sealed class SettingsOverlayTests
    {
        private static LoadResult Load(string overlay) => GameDataLoader.Load(RepoPaths.DataDirectory, RootbornBuilders.Register, overlay);

        [Test]
        public void TimeJson_OneField_ReplacesOnlyThatField_AndListsTheChange()
        {
            using var dir = new TempDir();
            dir.Write("time.json", @"{""bedtime"":""23:00""}");
            var original = Load(null).Definitions.TimeRules;

            var result = Load(dir.Path);

            Assert.That(result.Errors, Is.Empty);
            var rules = result.Definitions.TimeRules;
            Assert.That(rules.BedtimeMinutes, Is.EqualTo(23 * 60));
            Assert.That(rules.DayStartMinutes, Is.EqualTo(original.DayStartMinutes), "적지 않은 필드는 원본 그대로");
            var change = result.OverlayChanges.Single();
            Assert.That((change.Type, change.Id, change.Field, change.Before, change.After), Is.EqualTo((DataOverlay.SettingsType, "time.json", "bedtime", "\"22:00\"", "\"23:00\"")));
        }

        [Test]
        public void EconomyJson_SameValue_IsNotAChange_AndEntityOverlayStillWorks()
        {
            using var dir = new TempDir();
            dir.Write("economy.json", @"{""currencyItem"":""item.allowance""}");
            dir.Write("studentlife/PartTimeWorkDefinition.json", @"[{""type"":""PartTimeWorkDefinition"",""id"":""work.clinic-helper"",""timeCostMinutes"":45}]");

            var result = Load(dir.Path);

            Assert.That(result.Errors, Is.Empty);
            Assert.That(result.OverlayChanges.Select(c => c.Field), Is.EqualTo(new[] { "timeCostMinutes" }));
            Assert.That(result.Definitions.Currency.Id, Is.EqualTo("item.allowance"));
        }

        [Test]
        public void BadValuesInTheSettingsOverlay_AreLoadErrors()
        {
            using var dir = new TempDir();
            dir.Write("time.json", @"{""bedtime"":""25:00""}");
            Assert.That(Load(dir.Path).Errors, Is.Not.Empty, "규칙 파일 검사를 그대로 거친다");

            using var notObject = new TempDir();
            notObject.Write("economy.json", "[1,2]");
            Assert.That(Load(notObject.Path).Errors.Any(e => e.Kind == DataOverlay.UnsupportedKind), "맨 위가 객체가 아니면 오류");
        }

        [Test]
        public void OriginalSettingsFiles_AreNotModified_AndTheFingerprintDiffers()
        {
            string time = Path.Combine(RepoPaths.DataDirectory, "..", "game", "time.json");
            byte[] before = File.ReadAllBytes(time);
            string plain = DataFingerprint.Compute(RepoPaths.DataDirectory);
            using var dir = new TempDir();
            dir.Write("time.json", @"{""bedtime"":""23:00""}");

            Load(dir.Path);

            Assert.That(File.ReadAllBytes(time), Is.EqualTo(before));
            Assert.That(DataFingerprint.Compute(RepoPaths.DataDirectory, dir.Path), Is.Not.EqualTo(plain));
        }
    }
}
