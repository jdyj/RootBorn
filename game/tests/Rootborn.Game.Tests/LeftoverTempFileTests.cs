using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Rootborn.Core.Save;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 세이브 견고성 5: FileSaveStore는 같은 폴더의 ".파일.guid.tmp"에 쓰고 바꿔치기한다. 쓰다 꺼져 임시 파일이 남은 슬롯도 시작되고,
    /// 지금 동작은 그 임시 파일을 읽지도 지우지도 않는 것이다. 자동 테스트다.
    /// </summary>
    public sealed class LeftoverTempFileTests
    {
        private static string TempName(string file) => "." + file + "." + Guid.NewGuid().ToString("N") + ".tmp";

        private static string Snapshot => SaveFileNames.AdultCareerSnapshot(TestWorld.Player);

        [Test]
        public void ATempFileBesideTheRealFile_IsIgnored_AndLeftAlone()
        {
            using var saves = SlotCheckpoints.Restore("day-3");
            var folder = Path.Combine(saves.Path, SlotCheckpoints.Slot);
            var half = Encoding.UTF8.GetBytes("{ \"Student\": { \"CurrentDay\": 99");
            var tempFiles = SlotCheckpoints.FileNames("day-3").Select(TempName).ToArray();
            foreach (var temp in tempFiles) File.WriteAllBytes(Path.Combine(folder, temp), half);
            var before = SlotCheckpoints.Read(saves);

            var game = TestWorld.Start(saves.Store, SlotCheckpoints.Slot);
            Assert.That(game.Student.CurrentDay, Is.EqualTo(3), "임시 파일이 아니라 진짜 파일을 읽는다");
            Assert.That(SlotCheckpoints.Same(before, SlotCheckpoints.Read(saves)), Is.True, "읽기만 해서는 임시 파일도 건드리지 않는다");

            game.VisitLocation("location.town-square");
            var after = SlotCheckpoints.Read(saves);
            foreach (var temp in tempFiles) Assert.That(after[temp].AsSpan().SequenceEqual(half), Is.True, temp + "는 그대로 남는다(지금은 치우지 않는다)");
            Assert.That(TestWorld.Start(saves.Store, SlotCheckpoints.Slot).Student.CurrentDay, Is.EqualTo(3));
        }

        [Test]
        public void ATempFileThatIsACompleteNewerSnapshot_IsStillNotUsed()
        {
            using var saves = SlotCheckpoints.Restore("first-command");
            var folder = Path.Combine(saves.Path, SlotCheckpoints.Slot);
            File.WriteAllBytes(Path.Combine(folder, TempName(Snapshot)), SlotCheckpoints.Files("day-3")[Snapshot]);

            Assert.That(TestWorld.Start(saves.Store, SlotCheckpoints.Slot).Student.CurrentDay, Is.EqualTo(1), "바꿔치기가 끝나지 않은 쓰기는 없던 일이다");
        }

        [Test]
        public void ASlotWithOnlyTempFiles_StartsANewGame_AndTheFirstSaveWritesRealFiles()
        {
            using var saves = new TempSaves();
            var folder = Path.Combine(saves.Path, SlotCheckpoints.Slot);
            Directory.CreateDirectory(folder);
            var temp = Path.Combine(folder, TempName(Snapshot));
            File.WriteAllBytes(temp, SlotCheckpoints.Files("day-3")[Snapshot]);

            var game = TestWorld.Start(saves.Store, SlotCheckpoints.Slot);
            Assert.That(game.Student.CurrentDay, Is.EqualTo(1));
            game.TalkTo("npc.first-guide");

            Assert.That(File.Exists(Path.Combine(folder, Snapshot)), Is.True);
            Assert.That(File.Exists(temp), Is.True, "남은 임시 파일은 그대로다");
            Assert.That(TestWorld.Start(saves.Store, SlotCheckpoints.Slot).Student.CurrentDay, Is.EqualTo(1));
        }

        [Test]
        public void SavingNeverLeavesItsOwnTempFileBehind()
        {
            using var saves = SlotCheckpoints.Restore("day-3");
            TestWorld.Start(saves.Store, SlotCheckpoints.Slot).VisitLocation("location.town-square");
            Assert.That(SlotCheckpoints.Read(saves).Keys.Where(k => k.EndsWith(".tmp", StringComparison.Ordinal)), Is.Empty);
        }
    }
}
