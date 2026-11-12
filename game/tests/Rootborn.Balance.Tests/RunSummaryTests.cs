using NUnit.Framework;

namespace Rootborn.Balance.Tests
{
    [TestFixture]
    public sealed class RunSummaryTests
    {
        private static RunResult Run(params DayRecord[] days)
        {
            var r = new RunResult { StrategyId = "t", TargetId = "", TierId = "" };
            r.Days.AddRange(days);
            return r;
        }

        private static DayRecord Day(int day, int earned = 0, int spent = 0, int level = 1, bool upgraded = false,
            int actions = 1, int workMinutes = 180, float minigameSeconds = 20f, float travelSeconds = 12.5f,
            int autoSkips = 0, string stop = StopReasons.Bedtime)
            => new DayRecord
            {
                Day = day, Earned = earned, Spent = spent, FacilityLevel = level, Upgraded = upgraded,
                Actions = actions, WorkMinutes = workMinutes, MinigameRealSeconds = minigameSeconds,
                TravelRealSeconds = travelSeconds, AutoSkips = autoSkips, Stop = stop,
            };

        // 3일: 하루 100씩 벌고, 2일째에 150을 써서 올린 뒤 3일째에 200을 번다.
        [Test] public void SummaryOfAHandMadeRun()
        {
            var run = Run(Day(1, earned: 100, level: 1), Day(2, earned: 100, spent: 150, level: 2, upgraded: true), Day(3, earned: 200, level: 2));
            var s = RunSummary.From(run);
            Assert.That(s.IncomePerDay, Is.EqualTo(400f / 3).Within(0.001f));
            Assert.That(s.Level2Day, Is.EqualTo(2));
            Assert.That(s.Level3Day, Is.Null);
            Assert.That(s.PaybackDays, Is.EqualTo(new int?[] { 2 }), "150 ÷ (200 − 100) = 1.5 → 2일");
        }

        [Test] public void ADayWithNothingDoneKeepsItsRowAndLeavesRatesEmpty()
        {
            var s = RunSummary.From(Run(Day(1, earned: 0, actions: 0, workMinutes: 0, minigameSeconds: 0)));
            Assert.That(s.IncomePerDay, Is.EqualTo(0f));
            Assert.That(s.IncomePerGameHour, Is.Null);
            Assert.That(s.IncomePerRealMinuteMinigameOnly, Is.Null);
        }

        // 올린 뒤 수입이 같거나, 마지막 날에 올림 → null
        [Test] public void UpgradeThatDoesNotRaiseIncomeHasNoPayback()
        {
            // 앞 1~2일 평균 100, 3일째 올림, 뒤 4일 평균 100 → 차 0 → null
            var same = Run(Day(1, earned: 100), Day(2, earned: 100), Day(3, earned: 0, spent: 50, level: 2, upgraded: true), Day(4, earned: 100, level: 2));
            Assert.That(RunSummary.From(same).PaybackDays, Is.EqualTo(new int?[] { null }), "100 − 100 = 0");
            // 마지막 날에 올리면 뒤 날이 없다 → null
            var last = Run(Day(1, earned: 100), Day(2, earned: 0, spent: 50, level: 2, upgraded: true));
            Assert.That(RunSummary.From(last).PaybackDays, Is.EqualTo(new int?[] { null }));
        }

        [Test] public void FirstAutoSkipAndBagFullDaysAreTheFirstMatchingDay()
        {
            var run = Run(Day(1), Day(2, autoSkips: 1), Day(3, autoSkips: 2, stop: StopReasons.BagFull), Day(4, stop: StopReasons.BagFull));
            var s = RunSummary.From(run);
            Assert.That(s.FirstAutoSkipDay, Is.EqualTo(2));   // 2일째가 처음 자동 넘기기 있는 날
            Assert.That(s.FirstBagFullDay, Is.EqualTo(3));    // 3일째가 처음 가방이 차서 멈춘 날
            var none = RunSummary.From(Run(Day(1)));
            Assert.That(none.FirstAutoSkipDay, Is.Null);
            Assert.That(none.FirstBagFullDay, Is.Null);
        }

        // 걷기 포함 값 < 미니게임만 센 값
        [Test] public void MinigameOnlyRateIgnoresTravelSeconds()
        {
            // 번 것 600, 미니게임 120초, 걷기 60초.
            var s = RunSummary.From(Run(Day(1, earned: 600, minigameSeconds: 120f, travelSeconds: 60f)));
            Assert.That(s.IncomePerRealMinuteMinigameOnly, Is.EqualTo(300f).Within(0.001f));   // 600 ÷ (120/60) = 300
            Assert.That(s.IncomePerRealMinuteWithTravel, Is.EqualTo(200f).Within(0.001f));     // 600 ÷ (180/60) = 200
            Assert.That(s.IncomePerRealMinuteWithTravel, Is.LessThan(s.IncomePerRealMinuteMinigameOnly));
            Assert.That(s.IncomePerGameHour, Is.EqualTo(200f).Within(0.001f));                 // 600 ÷ (180/60) = 200
        }
    }
}
