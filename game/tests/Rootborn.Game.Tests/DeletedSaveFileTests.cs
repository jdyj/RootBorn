using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 세이브 견고성 2(마): 파일 하나가 없을 때. 스냅샷과 영역 파일은 명령마다 한 묶음으로 쓰이므로 하나만 없으면 슬롯이 손상된 것이다:
    /// 빠진 파일을 알리고 시작을 거절하며 아무것도 쓰지 않는다(새 게임으로 남은 파일을 덮지 않는다, SlotDamageTests).
    /// 전에는 영역 파일이 없으면 그 영역만 처음 상태로 시작했다 — 퀘스트·마일스톤·캠페인·탐색·하루 이벤트의 "받음" 기록이 사라져
    /// 받은 보상을 다시 받을 수 있었다. Unity 판 스냅샷(지갑 없음)만 있는 옛 슬롯은 영역 파일 없이도 이어 간다(OlderSlotTests 등). 자동 테스트다.
    /// </summary>
    public sealed class DeletedSaveFileTests
    {
        private const string Checkpoint = "day-3";

        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true };

        // 파일 이름 앞부분 → 그 영역의 저장 모양. 영역 파일을 하나씩 지워 본다.
        private static readonly (string Prefix, Func<GameSession, object> Area)[] Areas =
        {
            ("campaign-progress", g => g.Campaign.ToSaveData().CompletedObjectiveIds), // 날짜는 학생에서 다시 맞춘다
            ("milestone-progress", g => g.Milestones.ToSaveData()),
            ("career-interest-progress", g => g.CareerInterests.ToSaveData()),
            ("location-state-progress", g => g.LocationStates.ToSaveData()),
            ("quest-log", g => g.QuestLog.ToSaveData()),
            ("exploration-progress", g => g.Exploration.ToSaveData()),
            ("discovery-clues", g => g.DiscoveryClues.ToSaveData()),
            ("clue-interpretations", g => g.ClueInterpretations.ToSaveData()),
            ("encyclopedia", g => g.Encyclopedia.ToSaveData()),
            ("daily-event-progress", g => g.DailyEvents.ToSaveData()),
        };

        public static IEnumerable<TestCaseData> AreaFiles()
        {
            foreach (var area in Areas)
                yield return new TestCaseData(SlotCheckpoints.FileNames(Checkpoint).Single(f => f.StartsWith(area.Prefix, StringComparison.Ordinal)), area.Prefix)
                    .SetName("Deleting_" + area.Prefix + "_RefusesToStart");
        }

        private static string Dump(object area) => JsonSerializer.Serialize(area, Json);

        [TestCaseSource(nameof(AreaFiles))]
        public void DeletingAnAreaFile_RefusesToStart_NamesIt_AndKeepsTheRest(string file, string prefix)
        {
            using var saves = SlotCheckpoints.Restore(Checkpoint);
            File.Delete(Path.Combine(saves.Path, SlotCheckpoints.Slot, file));
            var others = SlotCheckpoints.Read(saves);

            for (int attempt = 0; attempt < 2; attempt++)
            {
                var error = Assert.Throws<SaveSlotDamagedException>(() => TestWorld.Start(saves.Store, SlotCheckpoints.Slot).VisitLocation("location.town-square"), prefix);
                Assert.That(error.Problems.Single().FileName, Is.EqualTo(file), "빠진 영역 파일을 알린다");
                Assert.That(error.Problems.Single().Kind, Is.EqualTo(SaveSlotProblemKind.Missing));
            }

            Assert.That(SlotCheckpoints.Same(others, SlotCheckpoints.Read(saves)), Is.True, "거절한 시작은 아무것도 쓰지 않는다(빠진 파일을 새로 쓰지 않는다)");
        }

        [TestCaseSource(nameof(AreaFiles))]
        public void PuttingTheAreaFileBack_ContinuesAsBefore(string file, string prefix)
        {
            using var saves = SlotCheckpoints.Restore(Checkpoint);
            var original = TestWorld.Start(saves.Store, SlotCheckpoints.Slot);
            string path = Path.Combine(saves.Path, SlotCheckpoints.Slot, file);
            byte[] kept = File.ReadAllBytes(path);
            File.Delete(path);
            Assert.Throws<SaveSlotDamagedException>(() => TestWorld.Start(saves.Store, SlotCheckpoints.Slot));
            File.WriteAllBytes(path, kept);

            var game = TestWorld.Start(saves.Store, SlotCheckpoints.Slot);

            foreach (var area in Areas) Assert.That(Dump(area.Area(game)), Is.EqualTo(Dump(area.Area(original))), area.Prefix);
        }

        [TestCase("first-command")]
        [TestCase("day-3")]
        [TestCase("adult-chosen")]
        public void DeletingTheSnapshot_RefusesToStart_NamesIt_AndKeepsTheOtherAreaFiles(string checkpoint)
        {
            using var saves = SlotCheckpoints.Restore(checkpoint);
            var snapshot = SlotCheckpoints.FileNames(checkpoint).Single(f => f.StartsWith("adult-career-snapshot", StringComparison.Ordinal));
            File.Delete(Path.Combine(saves.Path, SlotCheckpoints.Slot, snapshot));
            var others = SlotCheckpoints.Read(saves);

            var error = Assert.Throws<SaveSlotDamagedException>(() => TestWorld.Start(saves.Store, SlotCheckpoints.Slot));

            Assert.That(error.Problems.Single().FileName, Is.EqualTo(snapshot), "빠진 스냅샷을 알린다");
            Assert.That(error.Problems.Single().Kind, Is.EqualTo(SaveSlotProblemKind.Missing));
            Assert.That(SlotCheckpoints.Same(others, SlotCheckpoints.Read(saves)), Is.True, "거절한 시작은 아무것도 쓰지 않는다");
        }

        [Test]
        public void DeletingEveryFileOfTheSlot_StartsANewGame()
        {
            using var saves = SlotCheckpoints.Restore(Checkpoint);
            foreach (var file in Directory.GetFiles(Path.Combine(saves.Path, SlotCheckpoints.Slot))) File.Delete(file);

            Assert.That(TestWorld.Start(saves.Store, SlotCheckpoints.Slot).Student.CurrentDay, Is.EqualTo(1));
        }
    }
}
