using System;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Careers;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;
using Rootborn.Data.Save;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 테스트 준비용: 저장된 스냅샷을 고쳐 쓴다(Unity E2E의 Prepare와 같은 준비 단계). 확인 대상이 아니라 시작 상태를 만드는 데만 쓴다.
    /// </summary>
    internal static class SaveEdits
    {
        public static void EditSnapshot(FileSaveStore store, Action<AdultCareerSnapshotSaveData> edit, string slot = TestWorld.Slot, string player = TestWorld.Player)
        {
            var name = SaveFileNames.AdultCareerSnapshot(player);
            Assert.That(store.TryRead<AdultCareerSnapshotSaveData>(slot, name, out var snapshot), Is.True, "먼저 한 번 저장해야 한다");
            edit(snapshot);
            store.Write(slot, name, snapshot);
        }

        /// <summary>Unity AdultCareerE2EScenarioTests.MakeCandidateReady: 4일째, 진행 중, 시간 포인트 0, career.chef 힌트, candidate.chef 이해도 3.</summary>
        public static void MakeChefCandidateReady(AdultCareerSnapshotSaveData snapshot)
        {
            var student = snapshot.Student;
            student.CurrentDay = Math.Max(4, student.CurrentDay);
            student.DayState = StudentDayState.InProgress.ToString();
            student.TimePointsSpentToday = 0;
            student.CareerHintIds = student.CareerHintIds.Concat(new[] { "career.chef" }).Distinct().ToArray();
            student.CareerCandidates ??= new CareerCandidateProgressSaveData { SaveSlot = student.SaveSlot, PlayerId = student.PlayerId };
            student.CareerCandidates.Candidates = new[]
            {
                new CareerCandidateProgressSaveData.CandidateEntry
                {
                    CandidateId = "candidate.chef", UnderstandingScore = 3, HintIds = new[] { "hint.chef.practice" }, LastHintSourceId = "hint.chef.practice",
                },
            };
        }

        public static void Fill(AdultCareerSnapshotSaveData snapshot, string itemId, int count)
        {
            var items = (snapshot.Inventory?.Items ?? Array.Empty<Rootborn.Core.Inventory.PlayerInventoryItemSaveData>()).ToList();
            items.Add(new Rootborn.Core.Inventory.PlayerInventoryItemSaveData { ItemId = itemId, Count = count });
            snapshot.Inventory ??= new Rootborn.Core.Inventory.PlayerInventorySaveData();
            snapshot.Inventory.Items = items.ToArray();
        }
    }
}
