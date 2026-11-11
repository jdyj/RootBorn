using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 세이브 견고성 2(마): 파일 하나가 없을 때. 스냅샷 말고 영역 파일이 없으면 "그 영역의 세이브 없음"이라 거절하지 않고
    /// 그 영역만 처음 상태로 시작하며, 다음 명령이 그 파일을 새로 쓴다. 스냅샷 파일만 없으면 슬롯이 손상된 것으로 보고 시작을 거절한다
    /// (새 게임으로 남은 파일을 덮지 않는다, SlotDamageTests). 자동 테스트다.
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
                    .SetName("Deleting_" + area.Prefix + "_ResetsOnlyThatArea");
        }

        private static string Dump(object area) => JsonSerializer.Serialize(area, Json);

        [TestCaseSource(nameof(AreaFiles))]
        public void DeletingAnAreaFile_StartsWithThatAreaFresh_AndKeepsTheRest(string file, string prefix)
        {
            using var saves = SlotCheckpoints.Restore(Checkpoint);
            var original = TestWorld.Start(saves.Store, SlotCheckpoints.Slot);
            using var empty = new TempSaves();
            var fresh = TestWorld.Start(empty.Store, SlotCheckpoints.Slot);
            File.Delete(Path.Combine(saves.Path, SlotCheckpoints.Slot, file));

            var game = TestWorld.Start(saves.Store, SlotCheckpoints.Slot);

            foreach (var area in Areas)
            {
                var expected = area.Prefix == prefix ? fresh : original;
                Assert.That(Dump(area.Area(game)), Is.EqualTo(Dump(area.Area(expected))), area.Prefix);
            }

            Assert.That(game.Student.CurrentDay, Is.EqualTo(original.Student.CurrentDay), "학생 진행은 스냅샷에서 그대로");
            Assert.That(Dump(game.QuestChains.ToSaveData()), Is.EqualTo(Dump(original.QuestChains.ToSaveData())), "체인도 스냅샷에서 그대로");

            game.VisitLocation("location.town-square");
            Assert.That(File.Exists(Path.Combine(saves.Path, SlotCheckpoints.Slot, file)), Is.True, "다음 명령이 그 파일을 새로 쓴다");
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
