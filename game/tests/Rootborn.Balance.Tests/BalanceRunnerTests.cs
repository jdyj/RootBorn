using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Careers;
using Rootborn.Game;
using Rootborn.Game.Tests;

namespace Rootborn.Balance.Tests
{
    /// <summary>한 판 돌리기: 실제 data/, 메모리 저장소, GameSession 명령. 자동 테스트다(화면 없음).</summary>
    public sealed class BalanceRunnerTests
    {
        private const int ShortDays = 6;

        // 여러 테스트가 같은 짧은 판들을 본다. 한 번만 돌린다.
        private static readonly Lazy<BalanceConfig> Config = new Lazy<BalanceConfig>(() =>
        {
            var config = BalanceConfig.Read();
            config.Validate();
            return config;
        });

        private static readonly Lazy<List<RunResult>> ShortRuns = new Lazy<List<RunResult>>(() => BalanceRunner.RunAll(Config.Value, ShortDays).ToList());

        [Test]
        public void WalletChangeEqualsEarnedMinusSpent_ForEveryRun()
        {
            Assert.That(ShortRuns.Value, Is.Not.Empty);
            foreach (var run in ShortRuns.Value)
            {
                TestContext.Out.WriteLine(Name(run) + " | 시작 " + run.StartDay + "일째 지갑 " + run.StartWallet + " | " + string.Join(" / ", run.Days.Select(Line)));
                Assert.That(run.Days.Count, Is.EqualTo(ShortDays), Name(run));
                long net = run.Days.Sum(d => (long)d.Earned - d.Spent);
                Assert.That(net, Is.EqualTo(run.Days[run.Days.Count - 1].WalletAtEnd - run.StartWallet), "번 것 − 쓴 것 = 지갑 변화: " + Name(run));
            }
        }

        [Test]
        public void StartingMoneyIsNotCountedAsEarned()
        {
            foreach (var run in ShortRuns.Value)
            {
                var first = run.Days[0];
                Assert.That(first.Earned, Is.EqualTo(first.WalletAtEnd - run.StartWallet + first.Spent), "첫날 번 것에 시작 지갑이 들어갔다: " + Name(run));
            }
        }

        [Test]
        public void SameInputGivesTheSameRunTwice()
        {
            var config = Config.Value;
            var facility = (Facility)RunTargets.All("facility", TestWorld.Definitions)[0];
            var strategy = config.Strategies.First(s => s.Each == "facility");
            var tier = config.Tiers[0];
            var a = BalanceRunner.Run(strategy, facility, tier, config, 3);
            var b = BalanceRunner.Run(strategy, facility, tier, config, 3);
            Assert.That(Lines(b), Is.EqualTo(Lines(a)));
        }

        [Test]
        public void FacilityWorkEarnsMoreAtAHigherScore()
        {
            var runs = ShortRuns.Value.Where(r => r.StrategyId == "facility-work-only").ToList();
            var facilities = runs.Select(r => r.TargetId).Distinct().ToList();
            Assert.That(facilities, Is.Not.Empty);
            foreach (var facility in facilities)
            {
                long low = Earned(runs.Single(r => r.TargetId == facility && r.TierId == "low"));
                long max = Earned(runs.Single(r => r.TargetId == facility && r.TierId == "max"));
                Assert.That(low, Is.GreaterThan(0), "낮은 점수 판도 번다: " + facility);
                Assert.That(low, Is.LessThan(max), "낮은 점수 판이 만점 판보다 적게 번다: " + facility);
            }
        }

        [Test]
        public void NothingStartsThatWouldEndAfterBedtime()
        {
            Assert.That(Config.Value.Sleep, Is.EqualTo("finish-by-bedtime"));
            int bedtime = TestWorld.Start(new MemorySaveStore()).TimeRules.BedtimeMinutes;
            foreach (var run in ShortRuns.Value)
                foreach (var day in run.Days.Where(d => d.Stop == StopReasons.Bedtime))
                    Assert.That(day.EndedAtMinutes, Is.LessThanOrEqualTo(bedtime), Name(run) + " " + day.Day + "일째");
        }

        [Test]
        public void UpgradeStrategyReachesLevelTwo_AndRecordsWhatItSpent()
        {
            var config = Config.Value;
            var strategy = config.Strategies.Single(s => s.Id == "facility-work-upgrade");
            var max = config.Tiers.Single(t => t.Id == "max");
            foreach (var facility in RunTargets.All("facility", TestWorld.Definitions))
            {
                var run = BalanceRunner.Run(strategy, facility, max, config, config.Days);
                TestContext.Out.WriteLine(Name(run) + " | " + string.Join(" / ", run.Days.Select(Line)));
                var upgraded = run.Days.Where(d => d.Upgraded).ToList();
                Assert.That(upgraded, Is.Not.Empty, "30일 안에 올리지 못했다: " + Name(run) + " | 마지막 날 " + Line(run.Days[run.Days.Count - 1]));
                foreach (var day in upgraded) Assert.That(day.Spent, Is.GreaterThan(0), "올린 날 쓴 것이 기록되지 않았다: " + Name(run) + " " + day.Day + "일째");
                Assert.That(run.Days.Max(d => d.FacilityLevel), Is.GreaterThanOrEqualTo(2), Name(run));
                for (int i = 1; i < run.Days.Count; i++)
                    Assert.That(run.Days[i].FacilityLevel, Is.GreaterThanOrEqualTo(run.Days[i - 1].FacilityLevel), "시설 레벨이 줄었다: " + Name(run));
            }
        }

        [Test]
        public void AutoSkipStrategyRecordsAutoSkips()
        {
            var config = Config.Value;
            var strategy = config.Strategies.Single(s => s.Id == "facility-autoskip-upgrade");
            var max = config.Tiers.Single(t => t.Id == "max");
            foreach (var facility in RunTargets.All("facility", TestWorld.Definitions))
            {
                var run = BalanceRunner.Run(strategy, facility, max, config, config.Days);
                TestContext.Out.WriteLine(Name(run) + " | " + string.Join(" / ", run.Days.Select(Line)));
                Assert.That(run.Days.Any(d => d.AutoSkips > 0), Is.True,
                    "30일 안에 자동 넘기기가 한 번도 없었다: " + Name(run) + " | 마지막 날 " + Line(run.Days[run.Days.Count - 1]));
            }
        }

        [Test]
        public void ARefusedStartForAFullBagStopsTheDayAsBagFull()
        {
            var config = Config.Value;
            var strategy = new StrategySpec { Id = "fake-bag-full", Each = "none", Start = "new-game", Scored = false, Do = new[] { BagFullAction.KindName } };
            RunResult run = null;
            Assert.That(() => run = BalanceRunner.Run(strategy, null, null, config, 2, new IBalanceAction[] { new BagFullAction() }), Throws.Nothing);
            Assert.That(run.Days.Count, Is.EqualTo(2), "다음 날로 넘어간다");
            foreach (var day in run.Days)
            {
                Assert.That(day.Stop, Is.EqualTo(StopReasons.BagFull), day.Day + "일째");
                Assert.That(day.Actions, Is.EqualTo(0), day.Day + "일째");
            }
            Assert.That(run.Days[1].Day, Is.EqualTo(run.Days[0].Day + 1));
        }

        [Test]
        public void EveryPartTimeWorkInTheDataGetsItsOwnRun()
        {
            var expected = RunTargets.All("part-time-work", TestWorld.Definitions).Select(RunTargets.IdOf).ToList();
            Assert.That(expected, Is.Not.Empty);
            var runs = ShortRuns.Value.Where(r => r.StrategyId == "one-part-time-work").ToList();
            Assert.That(runs.Select(r => r.TargetId), Is.EquivalentTo(expected));
            foreach (var run in runs)
                Assert.That(Earned(run), Is.GreaterThan(0), "알바 판이 벌지 못했다: " + Name(run) + " | " + string.Join(" / ", run.Days.Select(Line)));
        }

        // 가방이 찼다고 거절하는 가짜 행동.
        private sealed class BagFullAction : IBalanceAction
        {
            public const string KindName = "fake-bag-full";
            public string Kind => KindName;

            public PlannedStep Plan(GameSession game, RunContext context)
                => new PlannedStep { Id = KindName, Minutes = 0, Walks = false, Do = (g, request) => new StepOutcome { Done = false, BagFull = true } };
        }

        private static long Earned(RunResult run) => run.Days.Sum(d => (long)d.Earned);

        private static string Name(RunResult run) => run.StrategyId + "/" + run.TargetId + "/" + run.TierId;

        private static List<string> Lines(RunResult run) => run.Days.Select(Line).ToList();

        private static string Line(DayRecord d)
            => string.Join(" ", new object[]
            {
                d.Day, d.Weekend, d.WalletAtEnd, d.Earned, d.Spent, d.Actions,
                string.Join(",", d.ActionCounts.Select(p => p.Key + "×" + p.Value)),
                string.Join(",", d.OtherItems.Select(p => p.Key + "×" + p.Value)),
                d.WorkMinutes, d.TravelMinutes, d.TravelRealSeconds, d.MinigameRealSeconds, d.EndedAtMinutes,
                "레벨" + d.FacilityLevel, d.Upgraded, "넘김" + d.AutoSkips, "체력" + d.EnergyAtEnd, "집중" + d.FocusAtEnd, d.Stop,
            });
    }
}
