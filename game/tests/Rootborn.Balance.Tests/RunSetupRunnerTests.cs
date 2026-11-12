using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Careers;
using Rootborn.Game.Tests;

namespace Rootborn.Balance.Tests
{
    /// <summary>
    /// 한 판의 가정(RunSetup)을 바꿔 돌린다: 실제 data/, 메모리 저장소, GameSession 명령. 자동 테스트다(가정을 넣은 계산, 화면 없음).
    /// 시설은 정의의 첫째, 전략은 facility-work-only가 기본.
    /// </summary>
    public sealed class RunSetupRunnerTests
    {
        private const int TenDays = 10, ThirtyDays = 30;

        private static readonly Lazy<BalanceConfig> Config = new Lazy<BalanceConfig>(() =>
        {
            var config = BalanceConfig.Read();
            config.Validate();
            return config;
        });

        private static Facility FirstFacility => TestWorld.Definitions.All<Facility>().First();

        private static readonly Lazy<RunResult> NeverRun = new Lazy<RunResult>(() => Run("facility-work-only", s => s.Sleep = "never", TenDays));
        private static readonly Lazy<RunResult> BaselineRun = new Lazy<RunResult>(() => Run("facility-work-only", _ => { }, TenDays));

        private static RunResult Run(string strategyId, Action<RunSetup> edit, int days)
        {
            var config = Config.Value;
            var setup = RunSetup.Baseline(config);
            edit(setup);
            return BalanceRunner.Run(config.Strategies.Single(s => s.Id == strategyId), FirstFacility, setup, config, days);
        }

        [Test]
        public void NeverSleeping_RunsAllDays_WithoutHanging_AndNeverRecordsSlept()
        {
            var run = NeverRun.Value;
            TestContext.Out.WriteLine(Lines(run));
            Assert.That(run.Days.Count, Is.EqualTo(TenDays));
            Assert.That(run.Days.All(d => !d.Slept && d.SleptAtMinutes == -1), Is.True, "자지 않는 판인데 잔 날이 있다");
            for (int i = 1; i < run.Days.Count; i++)
                Assert.That(run.Days[i].Day, Is.EqualTo(run.Days[i - 1].Day + 1), "날 번호가 하나씩 늘지 않는다");
        }

        [Test]
        public void NeverSleeping_GetsExhausted_AndThoseDaysStillHaveARow()
        {
            var run = NeverRun.Value;
            var exhausted = run.Days.Where(d => d.Exhausted).ToList();
            Assert.That(exhausted, Is.Not.Empty, "자지 않았는데 지친 날이 없다. 날마다 체력: " + string.Join(", ", run.Days.Select(d => d.Day + "일 " + d.EnergyAtEnd)));
            foreach (var day in exhausted)
                Assert.That(day.Stop, Is.EqualTo(StopReasons.DayRanOut), day.Day + "일째: 자지 않는 밤에 지치면 날이 넘어가기를 기다린다");
        }

        [Test]
        public void SleepingAt2200_NeverRecordsExhausted()
        {
            var run = BaselineRun.Value;
            int bedtime = TestWorld.Definitions.TimeRules.BedtimeMinutes;
            Assert.That(RunSetup.Baseline(Config.Value).Sleep, Is.EqualTo("finish-by 22:00"));
            Assert.That(run.Days.Count(d => d.Exhausted), Is.Zero, Lines(run));
            foreach (var day in run.Days)
            {
                Assert.That(day.Slept, Is.True, day.Day + "일째");
                Assert.That(day.SleptAtMinutes, Is.InRange(0, bedtime), day.Day + "일째");
            }
        }

        [Test]
        public void FinishBy0200_DoesMoreActionsPerDayThanFinishBy2200_OnDayOne()
        {
            var late = Run("facility-work-only", s => s.Sleep = "finish-by 02:00", 1);
            Assert.That(late.Days[0].Actions, Is.GreaterThan(BaselineRun.Value.Days[0].Actions), Lines(late) + " | " + Lines(BaselineRun.Value));
        }

        [Test]
        public void NoActionCrossesDayEnd_WhenStayingUp()
        {
            int dayEnd = TestWorld.Definitions.TimeRules.DayEndMinutes;
            foreach (var day in NeverRun.Value.Days)
                Assert.That(day.EndedAtMinutes, Is.LessThanOrEqualTo(dayEnd), day.Day + "일째");
        }

        [Test]
        public void TravelZero_FitsMoreActionsThanTravelSixty()
        {
            var zero = Run("facility-work-only", s => s.TravelMinutes = 0, 1);
            var sixty = Run("facility-work-only", s => s.TravelMinutes = 60, 1);
            Assert.That(zero.Days[0].Actions, Is.GreaterThan(sixty.Days[0].Actions), Lines(zero) + " | " + Lines(sixty));
        }

        [Test]
        public void MinigameShare_ChangesMinigameRealSeconds_NotIncome()
        {
            var quarter = Run("facility-work-only", s => s.MinigameShare = 0.25f, 1).Days[0];
            var full = Run("facility-work-only", s => s.MinigameShare = 1.0f, 1).Days[0];
            Assert.That(quarter.MinigameRealSeconds, Is.GreaterThan(0f));
            Assert.That(full.Earned, Is.EqualTo(quarter.Earned), "몫은 시간만 바꾼다");
            Assert.That(full.MinigameRealSeconds, Is.EqualTo(quarter.MinigameRealSeconds * 4f).Within(0.01f));
        }

        [Test]
        public void FailEvery2_EarnsLessThanFixed80()
        {
            var failing = Run("facility-work-only", s => s.Score = "fail-every 2", TenDays);
            Assert.That(Earned(failing), Is.LessThan(Earned(BaselineRun.Value)), Lines(failing));
        }

        [Test]
        public void FacilityWorkLevel1_OnlyEverDoesLevelOneWork_EvenAfterUpgrading()
        {
            var run = Run("facility-level1-work-upgrade", s => s.Score = "fixed 100", ThirtyDays);
            TestContext.Out.WriteLine(Lines(run));
            Assert.That(run.Days.Any(d => d.Upgraded), Is.True, "올린 날이 없으면 이 검사는 빈 통과다");
            var levels = Works();
            var done = run.Days.SelectMany(d => d.ActionCounts.Keys).Where(levels.ContainsKey).Distinct().ToList();
            Assert.That(done, Is.Not.Empty);
            foreach (var id in done) Assert.That(levels[id], Is.EqualTo(1), "레벨 1 일만 해야 한다: " + id);
        }

        [Test]
        public void MasterThenAutoSkip_EventuallyAutoSkipsTheHighestWork()
        {
            var run = Run("facility-master-then-autoskip", s => s.Score = "fixed 100", ThirtyDays);
            TestContext.Out.WriteLine(Lines(run));
            var last = run.Days[run.Days.Count - 1];
            Assert.That(last.AutoSkips, Is.GreaterThan(0), "마지막 날 자동 넘기기가 없다");
            var levels = Works();
            int top = TestWorld.Definitions.All<FacilityWork>()
                .Where(w => w.FacilityId == FirstFacility.Id && w.MinimumFacilityLevel <= last.FacilityLevel)
                .Max(w => w.MinimumFacilityLevel);
            foreach (var id in last.ActionCounts.Keys.Where(levels.ContainsKey))
                Assert.That(levels[id], Is.EqualTo(top), "마지막 날 한 일이 그 레벨에서 가장 높은 일이 아니다: " + id);
        }

        private static Dictionary<string, int> Works()
            => TestWorld.Definitions.All<FacilityWork>().ToDictionary(w => w.Id, w => w.MinimumFacilityLevel, StringComparer.Ordinal);

        private static long Earned(RunResult run) => run.Days.Sum(d => (long)d.Earned);

        private static string Lines(RunResult run)
            => run.StrategyId + "/" + run.TargetId + "/" + run.Setup.Id + " | " + string.Join(" / ", run.Days.Select(d => string.Join(" ", new object[]
            {
                d.Day, "번" + d.Earned, "행동" + d.Actions, string.Join(",", d.ActionCounts.Select(p => p.Key + "×" + p.Value)), "끝" + d.EndedAtMinutes,
                "잠" + (d.Slept ? d.SleptAtMinutes.ToString() : "-"), "지침" + d.Exhausted, "체력" + d.EnergyAtEnd, "레벨" + d.FacilityLevel, "넘김" + d.AutoSkips, d.Stop,
            })));
    }
}
