using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.StudentLife;
using Rootborn.Data.Builders;

namespace Rootborn.Data.Tests
{
    /// <summary>데이터 덮어쓰기(시뮬레이션 테스트용): 실제 data/의 엔티티를 임시 폴더의 덮어쓰기로 바꿔 읽는다. 원본 파일은 건드리지 않는다.</summary>
    public sealed class DataOverlayTests
    {
        private const string Work = "work.clinic-helper";
        private const string WorkFile = "studentlife/PartTimeWorkDefinition.json";

        private static LoadResult Load(string overlay) => GameDataLoader.Load(RepoPaths.DataDirectory, RootbornBuilders.Register, overlay);

        private static string OverlayOf(string body)
            => @"[{""type"":""PartTimeWorkDefinition"",""id"":""" + Work + @""",""timeCostMinutes"":" + body + "}]";

        [Test]
        public void NoOverlay_IsSameAsBefore()
        {
            var plain = GameDataLoader.Load(RepoPaths.DataDirectory, RootbornBuilders.Register);
            var withNull = Load(null);

            Assert.That(withNull.OverlayChanges, Is.Empty);
            Assert.That(withNull.Errors.Count, Is.EqualTo(plain.Errors.Count));
            Assert.That(withNull.Definitions.Count, Is.EqualTo(plain.Definitions.Count));
            Assert.That(withNull.Definitions.Get<PartTimeWork>(Work).TimeCostMinutes, Is.EqualTo(plain.Definitions.Get<PartTimeWork>(Work).TimeCostMinutes));
        }

        [Test]
        public void OneField_ReplacesOnlyThatField_AndListsTheChange()
        {
            using var dir = new TempDir();
            dir.Write(WorkFile, OverlayOf("45"));
            var original = Load(null).Definitions.Get<PartTimeWork>(Work);

            var result = Load(dir.Path);

            Assert.That(result.Errors, Is.Empty);
            var changed = result.Definitions.Get<PartTimeWork>(Work);
            Assert.That(changed.TimeCostMinutes, Is.EqualTo(45));
            Assert.That(changed.TimeCostMinutes, Is.Not.EqualTo(original.TimeCostMinutes));
            Assert.That(changed.EnergyCost, Is.EqualTo(original.EnergyCost), "적지 않은 필드는 원본 그대로");
            Assert.That(changed.Rewards.Count, Is.EqualTo(original.Rewards.Count));
            var change = result.OverlayChanges.Single();
            Assert.That((change.Type, change.Id, change.Field, change.Before, change.After), Is.EqualTo(("PartTimeWorkDefinition", Work, "timeCostMinutes", "120", "45")));
        }

        [Test]
        public void SameValueAsOriginal_IsNotAChange()
        {
            using var dir = new TempDir();
            dir.Write(WorkFile, OverlayOf("120"));

            var result = Load(dir.Path);

            Assert.That(result.Errors, Is.Empty);
            Assert.That(result.OverlayChanges, Is.Empty);
        }

        [Test]
        public void UnknownId_IsAnError_NotIgnored()
        {
            using var dir = new TempDir();
            dir.Write(WorkFile, @"[{""type"":""PartTimeWorkDefinition"",""id"":""work.no-such"",""timeCostMinutes"":1}]");

            var result = Load(dir.Path);

            var error = result.Errors.Single(e => e.Kind == DataOverlay.UnknownEntityKind);
            Assert.That(error.Id, Is.EqualTo("work.no-such"));
            Assert.That(result.OverlayChanges, Is.Empty);
        }

        [Test]
        public void BrokenJson_IsInTheErrorList()
        {
            using var dir = new TempDir();
            dir.Write(WorkFile, @"[{""type"":""PartTimeWorkDefinition"",""id"":");

            var result = Load(dir.Path);

            Assert.That(result.Errors.Any(e => e.Kind == "parse"), Is.True);
        }

        [Test]
        public void MissingFolder_AndRegistryFile_AreErrors()
        {
            Assert.That(Load(Path.Combine(Path.GetTempPath(), "rootborn-no-such-overlay")).Errors.Any(e => e.Kind == DataOverlay.FolderKind), Is.True);

            using var dir = new TempDir();
            dir.Write("registry.json", "{}");
            Assert.That(Load(dir.Path).Errors.Any(e => e.Kind == DataOverlay.UnsupportedKind), Is.True);
        }

        [Test]
        public void ArrayField_IsReplacedWhole()
        {
            using var dir = new TempDir();
            dir.Write(WorkFile, @"[{""type"":""PartTimeWorkDefinition"",""id"":""" + Work + @""",""outcomes"":[]}]");

            var result = Load(dir.Path);

            Assert.That(result.Errors, Is.Empty);
            Assert.That(result.OverlayChanges.Single().Field, Is.EqualTo("outcomes"));
            Assert.That(result.Definitions.Get<PartTimeWork>(Work).Outcomes, Is.Empty);
        }

        [Test]
        public void OriginalFiles_AreNotModified()
        {
            string file = Path.Combine(RepoPaths.DataDirectory, WorkFile);
            byte[] before = File.ReadAllBytes(file);
            using var dir = new TempDir();
            dir.Write(WorkFile, OverlayOf("45"));

            Load(dir.Path);

            Assert.That(File.ReadAllBytes(file), Is.EqualTo(before));
        }

        [Test]
        public void Fingerprint_DiffersWithOverlay_AndIsUnchangedWithout()
        {
            string plain = DataFingerprint.Compute(RepoPaths.DataDirectory);
            using var dir = new TempDir();
            dir.Write(WorkFile, OverlayOf("45"));

            Assert.That(DataFingerprint.Compute(RepoPaths.DataDirectory, null), Is.EqualTo(plain));
            string withOverlay = DataFingerprint.Compute(RepoPaths.DataDirectory, dir.Path);
            Assert.That(withOverlay, Is.Not.EqualTo(plain));
            dir.Write(WorkFile, OverlayOf("46"));
            Assert.That(DataFingerprint.Compute(RepoPaths.DataDirectory, dir.Path), Is.Not.EqualTo(withOverlay));
        }
    }
}
