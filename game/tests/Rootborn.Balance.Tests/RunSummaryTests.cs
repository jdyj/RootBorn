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
            int autoSkips = 0, string stop = StopReasons.Bedtime, int? minigameEarned = null)
            => new DayRecord
            {
                Day = day, Earned = earned, MinigameEarned = minigameEarned ?? earned, Spent = spent, FacilityLevel = level, Upgraded = upgraded,
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
            Assert.That(s.PaybackGaps, Is.EqualTo(new string[] { null }));
            Assert.That(s.RecoverDays, Is.EqualTo(new int?[] { 2 }), "올린 날 100 + 3일째 200 = 300 ≥ 150 → 2일");
        }

        // 두 번 올린 판: 두 번째 올리기의 앞 구간은 첫 올리기 다음 날부터다.
        [Test] public void SecondUpgradeMeasuresFromAfterTheFirstUpgrade()
        {
            var run = Run(
                Day(1, earned: 0),
                Day(2, earned: 50, spent: 100, level: 2, upgraded: true),
                Day(3, earned: 200, level: 2),
                Day(4, earned: 200, level: 2),
                Day(5, earned: 200, spent: 400, level: 3, upgraded: true),
                Day(6, earned: 300, level: 3));
            var s = RunSummary.From(run);
            // 첫째: 앞 1일 평균 0, 뒤 3~4일 평균 200 → 100 ÷ 200 = 0.5 → 1일
            // 둘째: 앞 3~4일 평균 200(1일부터 셌다면 112.5), 뒤 6일 300 → 400 ÷ 100 = 4일(1일부터 셌다면 3일)
            Assert.That(s.PaybackDays, Is.EqualTo(new int?[] { 1, 4 }));
            Assert.That(s.PaybackGaps, Is.EqualTo(new string[] { null, null }));
            // 쓴 값만큼 버는 날: 첫째 50 + 200 ≥ 100 → 2일, 둘째 200 + 300 ≥ 400 → 2일
            Assert.That(s.RecoverDays, Is.EqualTo(new int?[] { 2, 2 }));
        }

        // 이틀 연속 올린 판: 늘어난 수입으로는 셀 구간이 없고, 쓴 값만큼 버는 날은 있다.
        [Test] public void BackToBackUpgradesHaveNoSpanButStillRecover()
        {
            var run = Run(
                Day(1, earned: 100),
                Day(2, earned: 100, spent: 50, level: 2, upgraded: true),
                Day(3, earned: 100, spent: 50, level: 3, upgraded: true),
                Day(4, earned: 300, level: 3));
            var s = RunSummary.From(run);
            Assert.That(s.PaybackDays, Is.EqualTo(new int?[] { null, null }));
            Assert.That(s.PaybackGaps, Is.EqualTo(new[] { PaybackGap.NoSpan, PaybackGap.NoSpan }), "첫째는 뒤 구간이, 둘째는 앞 구간이 없다");
            Assert.That(s.RecoverDays, Is.EqualTo(new int?[] { 1, 1 }), "올린 날 번 100 ≥ 50");
        }

        // 자동 넘기기 수입이 섞인 판: 미니게임만 센 분당 수입은 미니게임을 한 행동이 번 것만 나눈다.
        [Test] public void MinigameOnlyRateCountsOnlyMinigameIncome()
        {
            // 번 것 1,000 중 미니게임 행동이 300, 미니게임 60초, 걷기 60초.
            var s = RunSummary.From(Run(Day(1, earned: 1000, minigameEarned: 300, minigameSeconds: 60f, travelSeconds: 60f)));
            Assert.That(s.IncomePerRealMinuteMinigameOnly, Is.EqualTo(300f).Within(0.001f));   // 300 ÷ (60/60)
            Assert.That(s.IncomePerRealMinuteWithTravel, Is.EqualTo(500f).Within(0.001f));     // 1,000 ÷ (120/60) — 판 전체 수입 그대로
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
            Assert.That(RunSummary.From(same).PaybackGaps, Is.EqualTo(new[] { PaybackGap.NoGain }));
            Assert.That(RunSummary.From(same).RecoverDays, Is.EqualTo(new int?[] { 2 }), "올린 날 0 + 4일째 100 ≥ 50");
            // 마지막 날에 올리면 뒤 날이 없다 → null, 판이 끝날 때까지 쓴 값만큼 벌지 못했다 → null
            var last = Run(Day(1, earned: 100), Day(2, earned: 0, spent: 50, level: 2, upgraded: true));
            Assert.That(RunSummary.From(last).PaybackDays, Is.EqualTo(new int?[] { null }));
            Assert.That(RunSummary.From(last).PaybackGaps, Is.EqualTo(new[] { PaybackGap.NoSpan }));
            Assert.That(RunSummary.From(last).RecoverDays, Is.EqualTo(new int?[] { null }));
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
