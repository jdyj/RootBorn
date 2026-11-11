using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Rootborn.Core.Save;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 세이브 안전 1·2: 시작할 때 읽기 문제를 모아 SaveSlotDamagedException 하나로 알린다. 스냅샷이 없는데 이 플레이어의 다른 영역 파일이
    /// 있으면 손상(빠진 파일)이고, 월드 상태 사본 파일이 깨져도 다른 영역처럼 알린다. 진짜 빈 슬롯과 다른 플레이어만 논 슬롯은 새 게임이다. 자동 테스트다.
    /// </summary>
    public sealed class SlotDamageTests
    {
        private static string Folder(TempSaves saves, string slot = SlotCheckpoints.Slot) => Path.Combine(saves.Path, slot);

        private static string SnapshotOf(string checkpoint)
            => SlotCheckpoints.FileNames(checkpoint).Single(f => f.StartsWith("adult-career-snapshot", StringComparison.Ordinal));

        [Test]
        public void MissingSnapshot_IsReportedAsMissing_WithTheFilesThatAreStillThere()
        {
            using var saves = SlotCheckpoints.Restore("day-3");
            var snapshot = SnapshotOf("day-3");
            File.Delete(Path.Combine(Folder(saves), snapshot));
            var before = SlotCheckpoints.Read(saves);

            var error = Assert.Throws<SaveSlotDamagedException>(() => TestWorld.Start(saves.Store, SlotCheckpoints.Slot));

            Assert.That(error.SaveSlot, Is.EqualTo(SlotCheckpoints.Slot));
            var missing = error.Problems.Single();
            Assert.That(missing.Kind, Is.EqualTo(SaveSlotProblemKind.Missing));
            Assert.That(missing.FileName, Is.EqualTo(snapshot));
            Assert.That(error.FoundFiles, Does.Contain(SaveFileNames.Campaign(TestWorld.Player)));
            Assert.That(error.Message, Does.Contain(snapshot));
            Assert.That(SlotCheckpoints.Same(before, SlotCheckpoints.Read(saves)), Is.True, "거절한 시작은 아무것도 쓰지 않는다");
        }

        [Test]
        public void MissingSnapshot_IsNotOverwrittenByANewGame_EvenAfterTryingAgain()
        {
            using var saves = SlotCheckpoints.Restore("day-3");
            File.Delete(Path.Combine(Folder(saves), SnapshotOf("day-3")));
            var before = SlotCheckpoints.Read(saves);

            for (int attempt = 0; attempt < 3; attempt++)
                Assert.Throws<SaveSlotDamagedException>(() => TestWorld.Start(saves.Store, SlotCheckpoints.Slot).VisitLocation("location.town-square"));

            Assert.That(SlotCheckpoints.Same(before, SlotCheckpoints.Read(saves)), Is.True);
        }

        [Test]
        public void SeveralDamagedFiles_AreAllListedInOneReport()
        {
            using var saves = SlotCheckpoints.Restore("day-3");
            var broken = new[] { SaveFileNames.Campaign(TestWorld.Player), SaveFileNames.Milestone(TestWorld.Player), SaveFileNames.WorldStateShared };
            foreach (var file in broken) File.WriteAllText(Path.Combine(Folder(saves), file), "{ \"Records\": ");

            var error = Assert.Throws<SaveSlotDamagedException>(() => TestWorld.Start(saves.Store, SlotCheckpoints.Slot));

            Assert.That(error.Problems.Select(p => p.FileName), Is.EquivalentTo(broken));
            Assert.That(error.Problems.All(p => p.Kind == SaveSlotProblemKind.Unreadable && p.Error != null), Is.True);
            foreach (var file in broken) Assert.That(error.Message, Does.Contain(file));
        }

        [TestCase("world-state-progress.json")]
        [TestCase("world-state-progress-shared.json")]
        public void ADamagedWorldStateCopy_IsReported_AndNotSilentlyReplaced(string file)
        {
            using var saves = SlotCheckpoints.Restore("day-3");
            Assert.That(File.Exists(Path.Combine(Folder(saves), file)), Is.True, "시험 슬롯에 그 사본이 있어야 한다");
            File.WriteAllText(Path.Combine(Folder(saves), file), "[1]");
            var before = SlotCheckpoints.Read(saves);

            var error = Assert.Throws<SaveSlotDamagedException>(() => TestWorld.Start(saves.Store, SlotCheckpoints.Slot));

            Assert.That(error.Problems.Single().FileName, Is.EqualTo(file));
            Assert.That(SlotCheckpoints.Same(before, SlotCheckpoints.Read(saves)), Is.True);
        }

        [Test]
        public void ATooNewFile_IsReportedAsUnreadable()
        {
            using var saves = SlotCheckpoints.Restore("day-3");
            var file = SaveFileNames.Milestone(TestWorld.Player);
            var text = File.ReadAllText(Path.Combine(Folder(saves), file));
            int version = SlotCheckpoints.TopVersion(Encoding.UTF8.GetBytes(text)).Value;
            File.WriteAllText(Path.Combine(Folder(saves), file), text.Replace("\"Version\": " + version, "\"Version\": 999999"));

            var error = Assert.Throws<SaveSlotDamagedException>(() => TestWorld.Start(saves.Store, SlotCheckpoints.Slot));

            Assert.That(error.Problems.Single().FileName, Is.EqualTo(file));
            Assert.That(error.Problems.Single().Error, Is.InstanceOf<Rootborn.Data.Save.SaveFileTooNewException>());
        }

        [Test]
        public void AnEmptySlotFolder_StartsANewGame()
        {
            using var saves = new TempSaves();
            Directory.CreateDirectory(Folder(saves));
            Assert.That(TestWorld.Start(saves.Store, SlotCheckpoints.Slot).Student.CurrentDay, Is.EqualTo(1));
        }

        [Test]
        public void ASlotWithOnlySlotInfoFiles_StartsANewGame()
        {
            // 캐릭터를 만든 뒤 첫 명령 전: 슬롯 정보와 옷 파일만 있다(GameSession이 읽지 않는 파일).
            using var saves = new TempSaves();
            Directory.CreateDirectory(Folder(saves));
            File.WriteAllText(Path.Combine(Folder(saves), SaveFileNames.Metadata), "{}");
            File.WriteAllText(Path.Combine(Folder(saves), SaveFileNames.Wearables(TestWorld.Player)), "{}");
            Assert.That(TestWorld.Start(saves.Store, SlotCheckpoints.Slot).Student.CurrentDay, Is.EqualTo(1));
        }

        [Test]
        public void ASecondPlayerJoiningAPlayedSlot_StartsFresh_BecauseSlotSharedFilesAreNotTheirs()
        {
            using var saves = SlotCheckpoints.Restore("furniture-placed");
            Assert.That(File.Exists(Path.Combine(Folder(saves), SaveFileNames.WorldStateShared)), Is.True);
            File.WriteAllText(Path.Combine(Folder(saves), SaveFileNames.HouseState), "{\"CurrentStageIndex\": 0}");
            Assert.That(TestWorld.Start(saves.Store, SlotCheckpoints.Slot).Student.CurrentDay, Is.EqualTo(3));
            File.WriteAllText(Path.Combine(Folder(saves), SaveFileNames.ObjectiveJournalTracking), "{\"CategoryId\": \"quests\", \"StableKey\": \"k\"}");

            var guest = TestWorld.Start(saves.Store, SlotCheckpoints.Slot, "player-guest");

            Assert.That(guest.Student.CurrentDay, Is.EqualTo(1));
        }
    }
}
