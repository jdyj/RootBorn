using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Quests;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 세이브 견고성 6: 일부 파일만 새것인 슬롯을 흉내 낸다(명령 앞뒤 슬롯에서 파일 하나만 앞쪽 것으로 되돌린다).
    /// FileSaveStore는 이제 한 명령의 저장을 묶음으로 써서 이런 슬롯을 만들지 않는다(EndDayInterruptedTests). 이 시험은 묶음 전에 쓴 세이브,
    /// 묶음을 모르는 저장소, 손으로 고친 슬롯을 위해 남긴다.
    /// 어긋난 슬롯도 예외 없이 시작해 이어 갈 수 있어야 하고, 같은 명령을 다시 해도 보상이 두 번 나가지 않아야 한다.
    /// 어긋남이 무엇인지는 TestContext 출력에 남긴다(자동 테스트).
    /// </summary>
    public sealed class PartialWriteTests
    {
        private const string Slot = "s";

        private static GameSession Prepare(TempSaves saves)
        {
            var game = TestWorld.Start(saves.Store, Slot);
            game.TalkTo("npc.first-guide");
            game.SelectDailyPlanRoute(game.AvailableDailyPlanRoutes().First().Id);
            game.TalkTo("npc.teacher");
            return game;
        }

        private static string Describe(GameSession g)
        {
            var chain = g.Chain(MainChainTests.ChainId);
            return $"day={g.Student.CurrentDay} state={g.Student.DayState} allowance={g.Inventory.CountOf(g.Item("item.allowance"))} " +
                   $"step={g.QuestChains.GetCurrentStep(chain)?.Id} campaignDone={g.Campaign.ToSaveData().CompletedObjectiveIds.Length}";
        }

        /// <summary>하루 끝내기가 쓰는 파일 중 스냅샷 말고 바뀌는 파일 이름.</summary>
        private static (SortedDictionary<string, byte[]> Before, SortedDictionary<string, byte[]> After, string[] Changed) EndDayDiff()
        {
            using var saves = new TempSaves();
            Prepare(saves);
            var before = SlotCheckpoints.Read(saves, Slot);
            Assert.That(TestWorld.Start(saves.Store, Slot).EndDay().Ended, Is.True);
            var after = SlotCheckpoints.Read(saves, Slot);
            var changed = after.Keys.Where(k => !k.StartsWith("adult-career-snapshot", StringComparison.Ordinal) && (!before.ContainsKey(k) || !before[k].AsSpan().SequenceEqual(after[k]))).ToArray();
            return (before, after, changed);
        }

        public static IEnumerable<string> EndDayFiles() => EndDayDiff().Changed;

        [Test]
        public void EndingTheDayWritesMoreThanTheSnapshot()
        {
            Assert.That(EndDayDiff().Changed, Is.Not.Empty, "시험이 성립하려면 스냅샷 말고도 바뀌는 파일이 있어야 한다");
        }

        [TestCaseSource(nameof(EndDayFiles))]
        public void EndDay_SnapshotWrittenButAnotherFileStillOld_StartsAndRepeatingTheCommandIsSafe(string file)
        {
            var (before, after, _) = EndDayDiff();
            var mix = new SortedDictionary<string, byte[]>(after);
            if (before.TryGetValue(file, out var old)) mix[file] = old; else mix.Remove(file);
            using var saves = new TempSaves();
            SlotCheckpoints.WriteAll(saves, mix, Slot);

            var game = TestWorld.Start(saves.Store, Slot);
            TestContext.Out.WriteLine($"[스냅샷 새것 + {file} 옛것] 시작: {Describe(game)}");
            int allowance = game.Inventory.CountOf(game.Item("item.allowance"));
            Assert.DoesNotThrow(() => game.EndDay());
            Assert.That(game.Inventory.CountOf(game.Item("item.allowance")), Is.EqualTo(allowance), "다시 해도 용돈이 늘지 않는다");
            Assert.DoesNotThrow(() => game.StartNextDay());
            TestContext.Out.WriteLine($"  다음 날까지 간 뒤: {Describe(game)}");
            Assert.That(TestWorld.Start(saves.Store, Slot).Student.CurrentDay, Is.EqualTo(game.Student.CurrentDay), "이어서 저장한 것이 다시 읽힌다");
        }

        [TestCaseSource(nameof(EndDayFiles))]
        public void EndDay_AnotherFileWrittenButTheSnapshotStillOld_StartsAndTheDayEndsOnce(string file)
        {
            var (before, after, _) = EndDayDiff();
            var mix = new SortedDictionary<string, byte[]>(before) { [file] = after[file] };
            using var saves = new TempSaves();
            SlotCheckpoints.WriteAll(saves, mix, Slot);

            var game = TestWorld.Start(saves.Store, Slot);
            TestContext.Out.WriteLine($"[스냅샷 옛것 + {file} 새것] 시작: {Describe(game)}");
            Assert.DoesNotThrow(() => game.EndDay());
            TestContext.Out.WriteLine($"  하루 끝내기를 다시 한 뒤: {Describe(game)}");
            Assert.That(game.Student.DayState, Is.EqualTo(Rootborn.Core.StudentLife.StudentDayState.ResultReady));
            Assert.That(game.Campaign.ToSaveData().CompletedObjectiveIds.Distinct().Count(), Is.EqualTo(game.Campaign.ToSaveData().CompletedObjectiveIds.Length), "목표가 두 번 기록되지 않는다");
        }

        [Test]
        public void PartTimeWorkReward_LivesInOneFile_SoItCannotBeHalfApplied()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store, Slot);
            game.TalkTo("npc.first-guide");
            Assert.That(game.BeginPartTimeWork("work.shift.corner-store", "w1", 7).Applied, Is.True);
            var before = SlotCheckpoints.Read(saves, Slot);
            Assert.That(game.SettleMinigame("w1", true).Applied, Is.True);
            var after = SlotCheckpoints.Read(saves, Slot);

            var changed = after.Keys.Where(k => !before.ContainsKey(k) || !before[k].AsSpan().SequenceEqual(after[k])).ToArray();
            Assert.That(changed.Length, Is.EqualTo(1), "알바 정산이 바꾼 파일: " + string.Join(", ", changed));
            Assert.That(changed[0], Does.StartWith("adult-career-snapshot"), "용돈·시간·정산 기록이 한 파일에 같이 들어간다");
        }
    }
}
