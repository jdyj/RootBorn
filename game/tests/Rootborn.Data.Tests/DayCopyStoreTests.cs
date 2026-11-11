using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.StudentLife;
using Rootborn.Data.Save;

namespace Rootborn.Data.Tests
{
    /// <summary>FileSaveStore의 날마다 슬롯 사본(backups/&lt;날 번호&gt;/): 복사 대상, 개수 줄이기, 남은 임시 폴더. 자동 테스트다.</summary>
    public sealed class DayCopyStoreTests
    {
        private const string Slot = "slot-0";
        private TempDir _dir;
        private FileSaveStore _store;

        [SetUp]
        public void SetUp()
        {
            _dir = new TempDir();
            _store = new FileSaveStore(_dir.Path);
        }

        [TearDown]
        public void TearDown() => _dir.Dispose();

        private string Copies => Path.Combine(_dir.Path, Slot, FileSaveStore.DayCopiesFolder);

        private string[] CopyNames() => Directory.GetDirectories(Copies).Select(Path.GetFileName).OrderBy(n => n).ToArray();

        [Test]
        public void ACopyTakesTheSlotFiles_ButNotTempFilesOrFolders()
        {
            _store.Write(Slot, "a.json", new StudentLifeProgressSaveData { CurrentDay = 3 });
            _dir.Write(Path.Combine(Slot, ".a.json.1234.tmp"), "{");
            _dir.Write(Path.Combine(Slot, "other-folder", "x.json"), "{}");

            _store.KeepDayCopy(Slot, 3, 3);

            Assert.That(Directory.GetFiles(Path.Combine(Copies, "3")).Select(Path.GetFileName), Is.EqualTo(new[] { "a.json" }));
            Assert.That(Directory.GetDirectories(Path.Combine(Copies, "3")), Is.Empty);
            Assert.That(new FileSaveStore(Path.Combine(_dir.Path, Slot, FileSaveStore.DayCopiesFolder)).TryRead<StudentLifeProgressSaveData>("3", "a.json", out var read), Is.True);
            Assert.That(read.CurrentDay, Is.EqualTo(3));
        }

        [Test]
        public void OnlyTheLatestDaysAreKept_ByDayNumberNotByName()
        {
            _store.Write(Slot, "a.json", new StudentLifeProgressSaveData());
            foreach (var day in new[] { 9, 10, 2, 11 }) _store.KeepDayCopy(Slot, day, 2);
            Assert.That(CopyNames(), Is.EquivalentTo(new[] { "10", "11" }));
        }

        [Test]
        public void TheSameDayAgain_ReplacesThatCopy()
        {
            _store.Write(Slot, "a.json", new StudentLifeProgressSaveData { CurrentDay = 1 });
            _store.KeepDayCopy(Slot, 4, 3);
            _store.Write(Slot, "a.json", new StudentLifeProgressSaveData { CurrentDay = 2 });
            _store.KeepDayCopy(Slot, 4, 3);

            Assert.That(CopyNames(), Is.EqualTo(new[] { "4" }));
            Assert.That(File.ReadAllText(Path.Combine(Copies, "4", "a.json")), Does.Contain("\"CurrentDay\": 2"));
        }

        [Test]
        public void LeftoverTempCopyFolders_AreRemoved_AndOtherFoldersAreLeftAlone()
        {
            _store.Write(Slot, "a.json", new StudentLifeProgressSaveData());
            _dir.Write(Path.Combine(Slot, FileSaveStore.DayCopiesFolder, ".5.abc.tmp", "a.json"), "{");
            _dir.Write(Path.Combine(Slot, FileSaveStore.DayCopiesFolder, "mine", "note.txt"), "keep");
            _dir.Write(Path.Combine(Slot, FileSaveStore.DayCopiesFolder, "007", "a.json"), "{}");

            _store.KeepDayCopy(Slot, 6, 1);

            Assert.That(CopyNames(), Is.EquivalentTo(new[] { "6", "007", "mine" }));
        }

        [Test]
        public void ZeroKeepsNothing_AndAMissingSlotIsNotCreated()
        {
            _store.Write(Slot, "a.json", new StudentLifeProgressSaveData());
            _store.KeepDayCopy(Slot, 1, 0);
            Assert.That(Directory.Exists(Copies), Is.False);

            _store.KeepDayCopy("no-such-slot", 1, 3);
            Assert.That(Directory.Exists(Path.Combine(_dir.Path, "no-such-slot")), Is.False);
        }
    }
}
