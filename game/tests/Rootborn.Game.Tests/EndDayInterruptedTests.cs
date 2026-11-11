using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;
using Rootborn.Data.Save;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 세이브 안전 3: 하루 끝내기의 저장(여러 영역 파일 한 묶음)이 어느 단계에서 끊겨도, 다시 시작하면 슬롯이 하루 끝내기 전 그대로이거나
    /// 끝낸 뒤 그대로다. 캠페인 목표만 빠지거나 스냅샷만 앞서는 슬롯이 생기지 않는다. 끊김은 FileSaveStore의 시험용 단계 고리에서 던져 흉내 낸다. 자동 테스트다.
    /// </summary>
    public sealed class EndDayInterruptedTests
    {
        private const string Slot = "s";

        private sealed class SimulatedCrash : Exception
        {
        }

        private static readonly Lazy<(SortedDictionary<string, byte[]> Before, SortedDictionary<string, byte[]> After, List<string> Steps)> Reference =
            new Lazy<(SortedDictionary<string, byte[]>, SortedDictionary<string, byte[]>, List<string>)>(() =>
            {
                using var saves = new TempSaves();
                var game = TestWorld.Start(saves.Store, Slot);
                game.TalkTo("npc.first-guide");
                game.SelectDailyPlanRoute(game.AvailableDailyPlanRoutes().First().Id);
                game.TalkTo("npc.teacher");
                var before = SlotCheckpoints.Read(saves, Slot);

                var store = new FileSaveStore(saves.Path);
                var steps = new List<string>();
                var again = TestWorld.Start(store, Slot);
                store.BatchStepForTests = steps.Add;
                Assert.That(again.EndDay().Ended, Is.True);
                return (before, SlotCheckpoints.Read(saves, Slot), steps);
            });

        public static IEnumerable<TestCaseData> StopPoints()
        {
            var steps = Reference.Value.Steps;
            for (int k = 0; k < steps.Count; k++) yield return new TestCaseData(k).SetName($"EndDay_StoppedAfterStep{k:D2}_{steps[k].Replace(':', '_')}");
        }

        [Test]
        public void EndingTheDay_ChangesTheCampaignFileAndTheSnapshot_InOneBatch()
        {
            var (before, after, steps) = Reference.Value;
            var changed = after.Keys.Where(k => !before.ContainsKey(k) || !before[k].AsSpan().SequenceEqual(after[k])).ToArray();
            Assert.That(changed, Does.Contain(SaveFileNames.Campaign(TestWorld.Player)), "시험이 성립하려면 캠페인 파일이 바뀌어야 한다");
            Assert.That(changed, Does.Contain(SaveFileNames.AdultCareerSnapshot(TestWorld.Player)));
            Assert.That(steps.Count(s => s == "mark"), Is.EqualTo(1), "저장 한 번이 묶음 하나다: " + string.Join(", ", steps));
        }

        [TestCaseSource(nameof(StopPoints))]
        public void EndDay_StoppedAtAnyStep_RestartsAllBeforeOrAllAfter(int k)
        {
            var (before, after, steps) = Reference.Value;
            using var saves = new TempSaves();
            SlotCheckpoints.WriteAll(saves, before, Slot);
            var store = new FileSaveStore(saves.Path);
            var game = TestWorld.Start(store, Slot);
            int seen = 0;
            store.BatchStepForTests = _ => { if (seen++ == k) throw new SimulatedCrash(); };
            Assert.Throws<SimulatedCrash>(() => game.EndDay());

            var restarted = TestWorld.Start(new FileSaveStore(saves.Path), Slot);
            var files = SlotCheckpoints.Read(saves, Slot);
            bool isBefore = SlotCheckpoints.Same(files, before);
            bool isAfter = SlotCheckpoints.Same(files, after);
            TestContext.Out.WriteLine($"단계 {k}({steps[k]}) 뒤에 멈춤 → {(isBefore ? "전부 옛것" : isAfter ? "전부 새것" : "섞임")}");
            Assert.That(isBefore || isAfter, Is.True, "슬롯이 전부 옛것이거나 전부 새것이다");
            Assert.That(Directory.GetDirectories(Path.Combine(saves.Path, Slot)), Is.Empty, "다시 시작하면 남은 묶음을 치운다");

            var campaignAfter = TestWorld.Start(new FileSaveStore(saves.Path), Slot).Campaign.ToSaveData().CompletedObjectiveIds;
            if (isAfter)
            {
                Assert.That(restarted.Student.DayState, Is.EqualTo(StudentDayState.ResultReady));
            }
            else
            {
                Assert.That(restarted.Student.DayState, Is.Not.EqualTo(StudentDayState.ResultReady));
                Assert.That(restarted.EndDay().Ended, Is.True, "옛것이면 하루를 다시 끝낼 수 있다");
                Assert.That(SlotCheckpoints.Same(SlotCheckpoints.Read(saves, Slot), after), Is.True, "다시 끝내면 끊기지 않은 것과 같은 슬롯이 된다");
            }

            Assert.That(campaignAfter.Distinct().Count(), Is.EqualTo(campaignAfter.Length));
        }
    }
}
