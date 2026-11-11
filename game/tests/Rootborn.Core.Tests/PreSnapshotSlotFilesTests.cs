// 옮겨 옴: Assets/Scripts/UI/Careers/AdultCareerRuntimeComponent.cs(스냅샷이 없을 때의 옛 영역 파일 읽기: LoadLegacyInventory,
//          DailyPlanProgressPersistence.MigrateInto, CareerCandidateProgressPersistence.TryLoad), StudentLifeProgressPersistence.FileNameFor
using System;
using NUnit.Framework;
using Rootborn.Core.Careers;
using Rootborn.Core.Inventory;
using Rootborn.Core.StudentLife;
using Names = Rootborn.Core.Save.SaveFileNames;

namespace Rootborn.Core.Tests
{
    public sealed class PreSnapshotSlotFilesTests
    {
        private const string Slot = "slot-old";
        private const string Player = Names.DefaultPlayerId;

        [Test]
        public void FileNames_MatchUnity()
        {
            Assert.That(Names.UnityStudentLife(Player), Is.EqualTo("student-life-progress.json"));
            Assert.That(Names.UnityStudentLife("steam:7"), Is.EqualTo("student-life-progress-steam_7.json"));
            Assert.That(Names.UnityInventory(Player), Is.EqualTo("inventory.json"));
            Assert.That(Names.UnityInventory("player"), Is.EqualTo("inventory-player.json"));
            Assert.That(Names.UnityCareerCandidates(Player), Is.EqualTo("career-candidate-progress.json"));
            Assert.That(Names.UnityCareerCandidates("steam:7"), Is.EqualTo("career-candidate-progress-steam_7.json"));
            // 일과 파일은 "player"와 빈 id만 공용 이름이고, 그 밖은 UTF-16 글자마다 4자리 16진(옛 이름은 정리한 id).
            Assert.That(Names.UnityDailyPlan("player"), Is.EqualTo("daily-plan-progress.json"));
            Assert.That(Names.UnityDailyPlan("ab"), Is.EqualTo("daily-plan-progress-00610062.json"));
            Assert.That(Names.UnityDailyPlanSanitized("a:b"), Is.EqualTo("daily-plan-progress-a_b.json"));
            Assert.That(Names.UnityDailyPlanSanitized(""), Is.EqualTo("daily-plan-progress.json"));
        }

        [Test]
        public void WithoutFiles_NothingIsRead()
        {
            var store = new InMemorySaveStore();
            Assert.That(PreSnapshotSlotFiles.ReadStudent(store, Slot, Player), Is.Null);
            Assert.That(PreSnapshotSlotFiles.ReadInventory(store, Slot, Player), Is.Null);
            var student = new StudentLifeProgress(Slot, Player, 10, 10);
            Assert.That(PreSnapshotSlotFiles.ImportInto(student, store), Is.False);
        }

        [Test]
        public void TheStudentFile_IsReadAsTheSlotAndPlayerItIsIn()
        {
            var store = new InMemorySaveStore();
            var saved = new StudentLifeProgress("another-slot", "someone", 7, 6).ToSaveData();
            saved.CurrentDay = 4;
            store.Put(Slot, Names.UnityStudentLife(Player), saved);

            var read = PreSnapshotSlotFiles.ReadStudent(store, Slot, Player);

            Assert.That((read.SaveSlot, read.PlayerId, read.CurrentDay, read.Energy), Is.EqualTo((Slot, Player, 4, 7)));
        }

        [Test]
        public void AnInventoryFileWithoutItems_CannotBeRead()
        {
            var store = new InMemorySaveStore();
            store.Put(Slot, Names.UnityInventory(Player), new PlayerInventorySaveData { Items = null });
            Assert.Throws<InvalidOperationException>(() => PreSnapshotSlotFiles.ReadInventory(store, Slot, Player));

            store.Put(Slot, Names.UnityInventory(Player), new PlayerInventorySaveData { Items = new[] { new PlayerInventoryItemSaveData { ItemId = "item.coin", Count = 3 } } });
            Assert.That(PreSnapshotSlotFiles.ReadInventory(store, Slot, Player).Items[0].Count, Is.EqualTo(3));
        }

        [Test]
        public void TheDailyPlanFile_IsImported_FromTheEncodedName_OrTheOlderName()
        {
            var store = new InMemorySaveStore();
            // 칸이 생기기 전(버전 0)의 일과 파일: 고른 길 하나만 있다.
            var plan = new DailyPlanProgressSaveData { SaveSlot = Slot, PlayerId = Player, CurrentDay = 1, SelectedRouteId = "daily.library" };
            store.Put(Slot, Names.UnityDailyPlanSanitized(Player), plan);
            var student = new StudentLifeProgress(Slot, Player, 10, 10);

            Assert.That(PreSnapshotSlotFiles.ImportInto(student, store), Is.True);
            Assert.That(student.DailyPlan.SelectedRouteId, Is.EqualTo("daily.library"));

            var newer = new InMemorySaveStore();
            newer.Put(Slot, Names.UnityDailyPlan(Player), new DailyPlanProgressSaveData { SaveSlot = Slot, PlayerId = Player, CurrentDay = 1, SelectedRouteId = "daily.square" });
            newer.Put(Slot, Names.UnityDailyPlanSanitized(Player), plan);
            var other = new StudentLifeProgress(Slot, Player, 10, 10);
            PreSnapshotSlotFiles.ImportInto(other, newer);
            Assert.That(other.DailyPlan.SelectedRouteId, Is.EqualTo("daily.square"), "4자리 16진 이름이 먼저다");
        }

        [Test]
        public void TheCareerCandidateFile_ReplacesTheStudentsCandidates()
        {
            var store = new InMemorySaveStore();
            store.Put(Slot, Names.UnityCareerCandidates(Player), new CareerCandidateProgressSaveData
            {
                SaveSlot = "copied-from", PlayerId = Player,
                Candidates = new[] { new CareerCandidateProgressSaveData.CandidateEntry { CandidateId = "candidate.chef", UnderstandingScore = 2 } },
            });
            var student = new StudentLifeProgress(Slot, Player, 10, 10);

            Assert.That(PreSnapshotSlotFiles.ImportInto(student, store), Is.True);
            Assert.That(student.CareerCandidates.ToSaveData().Candidates[0].CandidateId, Is.EqualTo("candidate.chef"));
        }

        [Test]
        public void ChangedBehavior_WithoutACandidateFile_TheStudentKeepsItsOwnCandidates()
        {
            // Unity 판은 후보 파일이 없으면 학생 파일 안의 후보를 빈 것으로 바꿨다.
            var store = new InMemorySaveStore();
            var data = new StudentLifeProgress(Slot, Player, 10, 10).ToSaveData();
            data.CareerCandidates = new CareerCandidateProgressSaveData
            {
                SaveSlot = Slot, PlayerId = Player,
                Candidates = new[] { new CareerCandidateProgressSaveData.CandidateEntry { CandidateId = "candidate.nurse", UnderstandingScore = 1 } },
            };
            var student = StudentLifeProgress.FromSaveData(data);

            PreSnapshotSlotFiles.ImportInto(student, store);

            Assert.That(student.CareerCandidates.ToSaveData().Candidates[0].CandidateId, Is.EqualTo("candidate.nurse"));
        }
    }
}
