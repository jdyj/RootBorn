using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Careers;
using Rootborn.Core.Inventory;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 스냅샷 없이 영역 파일만 남긴 Unity 판 슬롯을 이어 간다(자동 테스트). 고정 세이브에는 그런 슬롯이 없어,
    /// Unity 판의 파일 이름 규칙(Core SaveFileNames.Unity*)으로 만든 파일을 쓴다.
    /// </summary>
    public sealed class OlderSlotTests
    {
        private const string Slot = "slot-before-snapshots";
        private const string Player = TestWorld.Player;

        private static void WriteOlderSlot(TempSaves saves, string routeId)
        {
            var student = new StudentLifeProgress("slot-elsewhere", Player, 9, 8, 0, 600).ToSaveData();
            student.CurrentDay = 3;
            // 일과가 학생 진행에 들어오기 전의 파일이다(들어 있으면 따로 있던 일과 파일은 가져오지 않는다).
            student.DailyPlan = null;
            student.CareerCandidates = null;
            saves.Store.Write(Slot, SaveFileNames.UnityStudentLife(Player), student);
            saves.Store.Write(Slot, SaveFileNames.UnityInventory(Player), new PlayerInventorySaveData
            {
                Items = new[] { new PlayerInventoryItemSaveData { ItemId = "item.allowance", Count = 7 } },
            });
            saves.Store.Write(Slot, SaveFileNames.UnityDailyPlan(Player), new DailyPlanProgressSaveData { SaveSlot = Slot, PlayerId = Player, CurrentDay = 3, SelectedRouteId = routeId });
            saves.Store.Write(Slot, SaveFileNames.UnityCareerCandidates(Player), new CareerCandidateProgressSaveData
            {
                SaveSlot = Slot, PlayerId = Player,
                Candidates = new[] { new CareerCandidateProgressSaveData.CandidateEntry { CandidateId = "candidate.chef", UnderstandingScore = 3 } },
            });
        }

        private static string RouteId() => TestWorld.Definitions.All<DailyPlanRouteSet>().SelectMany(s => s.Routes).First().Id;

        [Test]
        public void AnOlderSlot_ContinuesFromItsAreaFiles()
        {
            using var saves = new TempSaves();
            WriteOlderSlot(saves, RouteId());

            var game = TestWorld.Start(saves.Store, Slot);

            Assert.That((game.Student.CurrentDay, game.Student.Energy, game.Student.Focus, game.Student.TimeMinutes), Is.EqualTo((3, 9, 8, 600)));
            Assert.That(game.Student.SaveSlot, Is.EqualTo(Slot), "복사한 슬롯도 지금 슬롯으로 읽는다");
            Assert.That(game.Inventory.CountOf(game.Item("item.allowance")), Is.EqualTo(7));
            Assert.That(game.DailyPlan.SelectedRouteId, Is.EqualTo(RouteId()));
            Assert.That(game.CareerCandidates.ToSaveData().Candidates.Single().UnderstandingScore, Is.EqualTo(3));
            Assert.That(saves.Store.TryRead<AdultCareerSnapshotSaveData>(Slot, SaveFileNames.AdultCareerSnapshot(Player), out _), Is.False, "읽기만 해서는 쓰지 않는다");
        }

        [Test]
        public void AfterTheFirstCommand_TheSnapshotTakesOver_AndTheOlderFilesStay()
        {
            using var saves = new TempSaves();
            WriteOlderSlot(saves, RouteId());
            var game = TestWorld.Start(saves.Store, Slot);
            game.VisitLocation("location.town-square");

            Assert.That(saves.Store.TryRead<AdultCareerSnapshotSaveData>(Slot, SaveFileNames.AdultCareerSnapshot(Player), out _), Is.True);
            foreach (var older in new[] { SaveFileNames.UnityStudentLife(Player), SaveFileNames.UnityInventory(Player), SaveFileNames.UnityDailyPlan(Player), SaveFileNames.UnityCareerCandidates(Player) })
                Assert.That(File.Exists(Path.Combine(saves.Path, Slot, older)), Is.True, older + "를 지우지 않는다");

            // 옛 파일을 바꿔도 이제 스냅샷이 원본이다.
            saves.Store.Write(Slot, SaveFileNames.UnityInventory(Player), new PlayerInventorySaveData { Items = new PlayerInventoryItemSaveData[0] });
            var again = TestWorld.Start(saves.Store, Slot);
            Assert.That(again.Student.CurrentDay, Is.EqualTo(3));
            Assert.That(again.Inventory.CountOf(again.Item("item.allowance")), Is.EqualTo(7));
            Assert.That(again.DailyPlan.SelectedRouteId, Is.EqualTo(RouteId()));
            Assert.That(again.CareerCandidates.ToSaveData().Candidates.Single().CandidateId, Is.EqualTo("candidate.chef"));
        }

        [Test]
        public void AnOlderSlot_PlaysOnToTheNextDay()
        {
            using var saves = new TempSaves();
            WriteOlderSlot(saves, RouteId());
            var game = TestWorld.Start(saves.Store, Slot);

            Assert.That(game.EndDay().Ended, Is.True);
            Assert.That(game.StartNextDay(), Is.True);
            Assert.That(TestWorld.Start(saves.Store, Slot).Student.CurrentDay, Is.EqualTo(4));
        }

        [Test]
        public void AnOlderInventoryFileWithoutItems_RefusesToStart()
        {
            using var saves = new TempSaves();
            saves.Store.Write(Slot, SaveFileNames.UnityInventory(Player), new PlayerInventorySaveData { Items = null });
            Assert.Throws<System.InvalidOperationException>(() => TestWorld.Start(saves.Store, Slot));
        }
    }
}
