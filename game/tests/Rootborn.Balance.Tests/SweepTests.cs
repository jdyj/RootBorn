using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Tests;

namespace Rootborn.Balance.Tests
{
    /// <summary>
    /// 모든 경우 훑기(설계 13절)를 작은 설정으로: 실제 설정을 읽어 축만 줄인다(잠 3, 점수 2, 알바 성공 1, 걷기 2, 몫 1, 12일, 시점 7·12, 전략 2).
    /// 자동 테스트다(가정을 넣은 계산, 화면 없음).
    /// </summary>
    public sealed class SweepTests
    {
        private const int SmallDays = 12;
        private static readonly int[] SmallCheckpoints = { 7, 12 };
        private const int Parallel4 = 4;

        private static readonly Lazy<BalanceConfig> Small = new Lazy<BalanceConfig>(() =>
        {
            var config = BalanceConfig.Read();
            config.Validate();
            var b = config.BaselineSetup;
            config.Axes.Sleep = new[] { b.Sleep, "finish-by 02:00", "never" };
            config.Axes.Score = new[] { b.Score, "fixed 100" };
            config.Axes.PartTime = new[] { b.PartTime };
            config.Axes.TravelMinutes = new[] { b.TravelMinutes, 0 };
            config.Axes.MinigameShare = new[] { b.MinigameShare };
            config.Axes.Days = SmallDays;
            config.Axes.Checkpoints = SmallCheckpoints;
            // 대상이 하나인 알바 전략 하나, 대상이 시설마다인 점수 전략 하나(설정 순서 그대로).
            config.Strategies = config.Strategies.Where(s => s.Id == "rotate-part-time-work" || s.Id == "facility-work-only").ToList();
            config.Validate();
            return config;
        });

        private static readonly Lazy<IReadOnlyList<SweepRow>> Rows = new Lazy<IReadOnlyList<SweepRow>>(() => SweepRunner.RunAll(Small.Value, Parallel4));

        private string _temp;

        [SetUp]
        public void MakeTemp() => _temp = Path.Combine(Path.GetTempPath(), "rootborn-balance-sweep-" + Guid.NewGuid().ToString("N"));

        [TearDown]
        public void RemoveTemp()
        {
            if (Directory.Exists(_temp)) Directory.Delete(_temp, true);
        }

        [Test]
        public void Plan_IsTheCrossProduct_InConfigOrder()
        {
            var config = Small.Value;
            var a = config.Axes;
            var plan = SweepRunner.Plan(config);
            int expected = config.Strategies.Sum(s => RunTargets.All(s.Each, TestWorld.Definitions).Count
                * a.Sleep.Length * (s.Scored ? a.Score.Length : a.PartTime.Length) * a.TravelMinutes.Length * a.MinigameShare.Length);
            Assert.That(plan.Count, Is.EqualTo(expected));

            var first = plan[0];
            var firstStrategy = config.Strategies[0];
            Assert.That(first.Strategy.Id, Is.EqualTo(firstStrategy.Id));
            Assert.That(RunTargets.IdOf(first.Target), Is.EqualTo(RunTargets.IdOf(RunTargets.All(firstStrategy.Each, TestWorld.Definitions)[0])));
            Assert.That(first.Setup.Sleep, Is.EqualTo(a.Sleep[0]));
            Assert.That(first.Setup.Score, Is.EqualTo(firstStrategy.Scored ? a.Score[0] : config.BaselineSetup.Score), "쓰지 않는 축은 기준 값");
            Assert.That(first.Setup.PartTime, Is.EqualTo(firstStrategy.Scored ? config.BaselineSetup.PartTime : a.PartTime[0]));
            Assert.That(first.Setup.TravelMinutes, Is.EqualTo(a.TravelMinutes[0]));
            Assert.That(first.Setup.MinigameShare, Is.EqualTo(a.MinigameShare[0]));

            var last = plan[plan.Count - 1];
            var lastStrategy = config.Strategies[config.Strategies.Count - 1];
            var lastTargets = RunTargets.All(lastStrategy.Each, TestWorld.Definitions);
            Assert.That(last.Strategy.Id, Is.EqualTo(lastStrategy.Id));
            Assert.That(RunTargets.IdOf(last.Target), Is.EqualTo(RunTargets.IdOf(lastTargets[lastTargets.Count - 1])));
            Assert.That(last.Setup.Sleep, Is.EqualTo(a.Sleep[a.Sleep.Length - 1]));
            Assert.That(last.Setup.Score, Is.EqualTo(lastStrategy.Scored ? a.Score[a.Score.Length - 1] : config.BaselineSetup.Score));
            Assert.That(last.Setup.TravelMinutes, Is.EqualTo(a.TravelMinutes[a.TravelMinutes.Length - 1]));
            Assert.That(last.Setup.MinigameShare, Is.EqualTo(a.MinigameShare[a.MinigameShare.Length - 1]));

            // 둘째 항목은 마지막 축(몫)이 아니라면 걷기가 바뀐다: 몫이 하나라 걷기가 가장 빨리 바뀐다.
            Assert.That(plan[1].Setup.TravelMinutes, Is.EqualTo(a.TravelMinutes[1]));
            Assert.That(plan[1].Setup.Sleep, Is.EqualTo(a.Sleep[0]));
        }

        [Test]
        public void RunAll_GivesOneRowPerPlanAndCheckpoint()
        {
            var config = Small.Value;
            var plan = SweepRunner.Plan(config);
            var rows = Rows.Value;
            Assert.That(rows.Count, Is.EqualTo(plan.Count * SmallCheckpoints.Length));
            Assert.That(rows.Where(r => r.Failure != null).Select(r => r.StrategyId + "/" + r.TargetId + "/" + r.Setup.Id + ": " + r.Failure), Is.Empty, "실패한 판");
            for (int i = 0; i < plan.Count; i++)
                for (int k = 0; k < SmallCheckpoints.Length; k++)
                {
                    var row = rows[i * SmallCheckpoints.Length + k];
                    Assert.That(row.StrategyId, Is.EqualTo(plan[i].Strategy.Id));
                    Assert.That(row.TargetId, Is.EqualTo(RunTargets.IdOf(plan[i].Target)));
                    Assert.That(row.Setup.Id, Is.EqualTo(plan[i].Setup.Id));
                    Assert.That(row.Checkpoint, Is.EqualTo(SmallCheckpoints[k]));
                    Assert.That(row.Summary, Is.Not.Null);
                }
        }

        [Test]
        public void RunAll_TwiceInParallel_GivesIdenticalCsvBytes()
        {
            string a = Path.Combine(_temp, "a"), b = Path.Combine(_temp, "b");
            SweepReport.Write(a, Small.Value, Rows.Value);
            SweepReport.Write(b, Small.Value, SweepRunner.RunAll(Small.Value, Parallel4));
            foreach (var file in new[] { SweepReport.CsvFile, SweepReport.ReportFile })
                Assert.That(File.ReadAllBytes(Path.Combine(b, file)), Is.EqualTo(File.ReadAllBytes(Path.Combine(a, file))), file);
        }

        [Test]
        public void RunAll_ParallelEqualsSerial()
        {
            var watch = Stopwatch.StartNew();
            var serial = SweepRunner.RunAll(Small.Value, 1);
            watch.Stop();
            int runs = SweepRunner.Plan(Small.Value).Count;
            TestContext.Out.WriteLine("직렬: 판 " + runs + "개 × " + SmallDays + "일, " + watch.Elapsed.TotalSeconds.ToString("0.00") + "초, 판-날 하나 "
                + (watch.Elapsed.TotalMilliseconds / (runs * SmallDays)).ToString("0.00") + "ms");
            string p = Path.Combine(_temp, "parallel"), s = Path.Combine(_temp, "serial");
            SweepReport.Write(p, Small.Value, Rows.Value);
            SweepReport.Write(s, Small.Value, serial);
            Assert.That(File.ReadAllBytes(Path.Combine(s, SweepReport.CsvFile)), Is.EqualTo(File.ReadAllBytes(Path.Combine(p, SweepReport.CsvFile))));
        }

        [Test]
        public void CheckpointSummary_EqualsSummaryOfAShorterRun()
        {
            var config = Small.Value;
            var plan = SweepRunner.Plan(config);
            int k = Array.IndexOf(SmallCheckpoints, 7);
            // 자지 않는 시설 판(지친 날이 있다)과 기준 판.
            var picks = new[]
            {
                Enumerable.Range(0, plan.Count).First(i => plan[i].Strategy.Scored && plan[i].Setup.Sleep == "never"),
                Enumerable.Range(0, plan.Count).First(i => plan[i].Strategy.Scored && SweepRunner.IsBaseline(config, plan[i].Setup)),
            };
            foreach (int i in picks)
            {
                var row = Rows.Value[i * SmallCheckpoints.Length + k];
                var shorter = RunSummary.From(BalanceRunner.Run(plan[i].Strategy, plan[i].Target, plan[i].Setup, config, 7));
                string name = row.StrategyId + "/" + row.TargetId + "/" + row.Setup.Id;
                Assert.That(row.Summary.IncomePerDay, Is.EqualTo(shorter.IncomePerDay), name);
                Assert.That(row.Summary.Level2Day, Is.EqualTo(shorter.Level2Day), name);
                Assert.That(row.Summary.Level3Day, Is.EqualTo(shorter.Level3Day), name);
                Assert.That(row.Summary.ExhaustionSeenDays, Is.EqualTo(shorter.ExhaustionSeenDays), name);
                Assert.That(row.Summary.NightsAwake, Is.EqualTo(shorter.NightsAwake), name);
                Assert.That(row.Summary.ZeroActionDays, Is.EqualTo(shorter.ZeroActionDays), name);
                Assert.That(row.Summary.NetIncomePerDay, Is.EqualTo(shorter.NetIncomePerDay), name);
            }
            Assert.That(Rows.Value[picks[0] * SmallCheckpoints.Length + k].Summary.ExhaustionSeenDays, Is.GreaterThan(0), "자지 않는 판에 지친 날이 없으면 이 비교는 약하다");
        }

        [Test]
        public void BaselineRowsAreMarked_AndThereIsOnePerStrategyTargetCheckpoint()
        {
            var config = Small.Value;
            var baseline = Rows.Value.Where(r => r.IsBaseline).ToList();
            int pairs = config.Strategies.Sum(s => RunTargets.All(s.Each, TestWorld.Definitions).Count);
            Assert.That(baseline.Count, Is.EqualTo(pairs * SmallCheckpoints.Length));
            foreach (var group in baseline.GroupBy(r => r.StrategyId + "/" + r.TargetId + "/" + r.Checkpoint))
                Assert.That(group.Count(), Is.EqualTo(1), group.Key);
            foreach (var row in baseline)
            {
                Assert.That(row.Setup.Sleep, Is.EqualTo(config.BaselineSetup.Sleep));
                Assert.That(row.Setup.TravelMinutes, Is.EqualTo(config.BaselineSetup.TravelMinutes));
            }
        }

        [Test]
        public void Report_HasASleepTablePerStrategy_WithARowPerSleepRule_AndMarksTheBest()
        {
            var config = Small.Value;
            SweepReport.Write(_temp, config, Rows.Value);
            var lines = File.ReadAllLines(Path.Combine(_temp, SweepReport.ReportFile));
            int section = Array.IndexOf(lines, SweepReport.SleepHeading);
            Assert.That(section, Is.GreaterThanOrEqualTo(0), "자는 때 절이 없다");
            int sectionEnd = Array.FindIndex(lines, section + 1, l => l.StartsWith("## "));
            if (sectionEnd < 0) sectionEnd = lines.Length;
            foreach (var strategy in config.Strategies)
            {
                int heading = Array.FindIndex(lines, section, sectionEnd - section, l => l == "### " + strategy.Id);
                Assert.That(heading, Is.GreaterThan(section), "자는 때 표가 없다: " + strategy.Id);
                var table = lines.Skip(heading + 1).SkipWhile(l => !l.StartsWith("|")).TakeWhile(l => l.StartsWith("|")).ToList();
                var body = table.Skip(2).ToList();
                Assert.That(body.Count, Is.EqualTo(config.Axes.Sleep.Length), strategy.Id + ": 줄 수 = 잠 규칙 수");
                for (int i = 0; i < config.Axes.Sleep.Length; i++)
                    Assert.That(body[i], Does.Contain(config.Axes.Sleep[i]), strategy.Id + ": 설정 순서");
                Assert.That(body.Count(l => l.Contains("**")), Is.EqualTo(1), strategy.Id + ": 가장 많이 버는 줄 하나만 굵게");
                Assert.That(body.Count(l => l.Contains(SweepReport.BaselineMark)), Is.EqualTo(1), strategy.Id + ": 기준 줄 표시");
                Assert.That(body.Single(l => l.Contains(SweepReport.BaselineMark)), Does.Contain(config.BaselineSetup.Sleep));
            }
        }

        [Test]
        public void Report_SaysItIsNotRealPlay_AndListsTheAxes()
        {
            var config = Small.Value;
            SweepReport.Write(_temp, config, Rows.Value);
            var text = File.ReadAllText(Path.Combine(_temp, SweepReport.ReportFile));
            Assert.That(text, Does.Contain(BalanceReport.NotPlayLine));
            foreach (var value in config.Axes.Sleep.Concat(config.Axes.Score).Concat(config.Axes.PartTime))
                Assert.That(text, Does.Contain(value), value);
            Assert.That(text, Does.Contain("판 " + SweepRunner.Plan(config).Count + "개"));
            Assert.That(text, Does.Not.Contain(SweepReport.FailuresHeading), "실패한 판이 없으면 실패 절도 없다");
            var csv = File.ReadAllLines(Path.Combine(_temp, SweepReport.CsvFile));
            Assert.That(csv[0], Does.StartWith("strategy,target,sleep,score,part_time,travel_minutes,minigame_share,checkpoint_day,is_baseline,failed,"));
            Assert.That(csv.Length, Is.EqualTo(1 + Rows.Value.Count));
        }

        [Test]
        public void AFailedRunIsListedAtTheTop_AndInTheFailedColumn()
        {
            var config = Small.Value;
            var rows = Rows.Value.ToList();
            var broken = new SweepRow
            {
                StrategyId = rows[0].StrategyId, TargetId = rows[0].TargetId, Setup = rows[0].Setup, Checkpoint = rows[0].Checkpoint,
                Summary = new RunSummary(), Failure = "시험: 행동이 날을 넘겼다",
            };
            rows[0] = broken;
            SweepReport.Write(_temp, config, rows);
            var report = File.ReadAllLines(Path.Combine(_temp, SweepReport.ReportFile));
            int failures = Array.IndexOf(report, SweepReport.FailuresHeading);
            Assert.That(failures, Is.GreaterThan(0).And.LessThan(Array.FindIndex(report, l => l.StartsWith("가정"))), "실패 절은 맨 위에");
            Assert.That(report.Skip(failures).Take(5).Any(l => l.Contains("시험: 행동이 날을 넘겼다")), Is.True);
            Assert.That(File.ReadAllLines(Path.Combine(_temp, SweepReport.CsvFile))[1], Does.Contain("시험: 행동이 날을 넘겼다"));
        }

        // ---- S3·S8: 손으로 값을 넣은 줄(실제 판의 줄을 복사해 요약만 바꾼다) ----

        private static SweepRow With(SweepRow r, float income)
            => new SweepRow { StrategyId = r.StrategyId, TargetId = r.TargetId, Setup = r.Setup, Checkpoint = r.Checkpoint, IsBaseline = r.IsBaseline, Summary = new RunSummary { IncomePerDay = income, NetIncomePerDay = income } };

        private static bool OthersBaseline(BalanceConfig config, RunSetup s)
        {
            var b = config.BaselineSetup;
            return s.Score == b.Score && s.PartTime == b.PartTime && s.TravelMinutes == b.TravelMinutes && s.MinigameShare == b.MinigameShare;
        }

        private static string[] WriteReport(BalanceConfig config, IReadOnlyList<SweepRow> rows, string temp)
        {
            SweepReport.Write(temp, config, rows);
            return File.ReadAllLines(Path.Combine(temp, SweepReport.ReportFile));
        }

        private static List<string> SleepTable(string[] lines, string heading)
        {
            int section = Array.IndexOf(lines, SweepReport.SleepHeading);
            int at = Array.FindIndex(lines, section, l => l == heading);
            Assert.That(at, Is.GreaterThan(section), "표 제목이 없다: " + heading);
            return lines.Skip(at + 1).SkipWhile(l => !l.StartsWith("|")).TakeWhile(l => l.StartsWith("|")).Skip(2).ToList();
        }

        [Test]
        public void Report_BoldsEveryRowEqualToTheBest_AndCountsThem()
        {
            var config = Small.Value;
            var strategy = config.Strategies.First(s => s.Scored).Id;
            var tied = new[] { config.BaselineSetup.Sleep, "never" };
            var rows = Rows.Value.Select(r => r.StrategyId == strategy && OthersBaseline(config, r.Setup) && r.Checkpoint == SmallDays
                ? With(r, tied.Contains(r.Setup.Sleep) ? 500f : 100f) : r).ToList();
            var body = SleepTable(WriteReport(config, rows, _temp), "### " + strategy);
            Assert.That(body.Count(l => l.StartsWith("| **")), Is.EqualTo(2), "같은 값인 줄은 모두 굵게");
            var lines = File.ReadAllLines(Path.Combine(_temp, SweepReport.ReportFile));
            int heading = Array.IndexOf(lines, "### " + strategy, Array.IndexOf(lines, SweepReport.SleepHeading));
            Assert.That(lines.Skip(heading).Take(body.Count + 12).Any(l => l.Contains(SweepReport.TiedLine + " 2")), Is.True, "표 아래에 같은 값 줄 수");
        }

        [Test]
        public void Report_HasTheBestSleepRulePerStrategyAndTravel()
        {
            var config = Small.Value;
            var lines = WriteReport(config, Rows.Value, _temp);
            int at = Array.IndexOf(lines, SweepReport.BestPerTravelHeading);
            Assert.That(at, Is.GreaterThan(Array.IndexOf(lines, SweepReport.SleepHeading)), "자는 때 절 안에 있다");
            var body = lines.Skip(at + 1).SkipWhile(l => !l.StartsWith("|")).TakeWhile(l => l.StartsWith("|")).Skip(2).ToList();
            Assert.That(body.Count, Is.EqualTo(config.Strategies.Count * config.Axes.TravelMinutes.Length), "줄 = 전략 × 걷는 분");
        }

        [Test]
        public void Report_ListsTargetsWhoseBestDiffers_AndGivesPerTargetTablesWhenTargetsDiffer()
        {
            var config = Small.Value;
            var strategy = config.Strategies.First(s => s.Scored);
            var targets = RunTargets.All(strategy.Each, TestWorld.Definitions).Select(RunTargets.IdOf).ToList();
            Assert.That(targets.Count, Is.GreaterThan(1));
            string odd = targets[1];
            // 둘째 대상만 never가 아주 크다 → 평균 1등은 never. 나머지 대상의 1등은 기준(22:00)이라 평균과 다르다.
            var rows = Rows.Value.Select(r =>
            {
                if (r.StrategyId != strategy.Id || !OthersBaseline(config, r.Setup) || r.Checkpoint != SmallDays) return r;
                float v = r.Setup.Sleep == config.BaselineSetup.Sleep ? 100f : r.Setup.Sleep == "never" ? (r.TargetId == odd ? 1000f : 0f) : 50f;
                return With(r, v);
            }).ToList();
            var lines = WriteReport(config, rows, _temp);
            Assert.That(lines.Any(l => l.Contains(SweepReport.DifferentBestLine) && l.Contains(targets[0]) && l.Contains(config.BaselineSetup.Sleep)), Is.True, "다른 1등인 대상");
            Assert.That(lines.Any(l => l.Contains(SweepReport.DifferentBestLine) && l.Contains(odd + " ")), Is.False, "평균과 같은 1등인 대상은 적지 않는다");
            Assert.That(SleepTable(lines, "#### " + strategy.Id + " / " + targets[0]).Count, Is.EqualTo(config.Axes.Sleep.Length), "대상끼리 다르면 대상별 표");
        }

        [Test]
        public void Report_GroupsCombosWithTheSameResult_AndNamesTheAxesThatDiffer()
        {
            var config = Small.Value;
            var rows = Rows.Value.ToList();
            int a = rows.FindIndex(r => r.Checkpoint == SmallDays && r.Setup.TravelMinutes == config.BaselineSetup.TravelMinutes && r.StrategyId == config.Strategies[0].Id);
            int b = rows.FindIndex(r => r.Checkpoint == SmallDays && r.Setup.TravelMinutes != config.BaselineSetup.TravelMinutes && r.StrategyId == rows[a].StrategyId
                && r.TargetId == rows[a].TargetId && r.Setup.Sleep == rows[a].Setup.Sleep);
            rows[a] = With(rows[a], 99999f);
            rows[b] = With(rows[b], 99999f);
            var lines = WriteReport(config, rows, _temp);
            int top = Array.FindIndex(lines, l => l.StartsWith("### " + SweepReport.TopHeadingPrefix));
            var first = lines.Skip(top + 1).SkipWhile(l => !l.StartsWith("|")).Skip(2).First();
            Assert.That(first, Does.Contain("99999.0"));
            Assert.That(first, Does.Contain("| 2 |"), "같은 결과 2판");
            Assert.That(first, Does.Contain("걷는 분"), "달라진 축");
        }

        [Test]
        public void SummaryCountsExhaustedDaysAndNightsAwake()
        {
            var run = new RunResult { StrategyId = "t", TargetId = "" };
            run.Days.Add(new DayRecord { Day = 1, Slept = true, SleptAtMinutes = 1300, EnergyAtEnd = 10, Earned = 100, Spent = 40, Actions = 3, Stop = StopReasons.Bedtime });
            run.Days.Add(new DayRecord { Day = 2, Slept = false, Exhausted = true, EnergyAtEnd = 0, Earned = 50, Actions = 2, Stop = StopReasons.DayRanOut });
            run.Days.Add(new DayRecord { Day = 3, Slept = true, SleptAtMinutes = 900, Exhausted = true, EnergyAtEnd = 2, Actions = 0, Stop = StopReasons.Exhausted });
            run.Days.Add(new DayRecord { Day = 4, Slept = true, SleptAtMinutes = 1320, EnergyAtEnd = 8, Earned = 30, Actions = 1, Stop = StopReasons.Exhausted });

            var all = RunSummary.From(run);
            Assert.That(all.ExhaustionSeenDays, Is.EqualTo(2), "지침을 본 날");
            Assert.That(all.StoppedExhaustedDays, Is.EqualTo(2), "지쳐서 멈춘 날(Stop == exhausted)");
            Assert.That(all.ZeroActionDays, Is.EqualTo(1), "행동 0인 날");
            Assert.That(all.NightsAwake, Is.EqualTo(1));
            Assert.That(all.AverageEnergyAtEnd, Is.EqualTo(5f).Within(0.001f));
            Assert.That(all.AverageSleptAtMinutes, Is.EqualTo((1300f + 900f + 1320f) / 3).Within(0.01f));
            Assert.That(all.IncomePerDay, Is.EqualTo(180f / 4).Within(0.001f), "번 돈 합 ÷ 날 수(쓴 돈은 빼지 않는다)");
            Assert.That(all.NetIncomePerDay, Is.EqualTo(140f / 4).Within(0.001f), "(번 돈 − 쓴 돈) ÷ 날 수");

            var two = RunSummary.From(run, 2);
            Assert.That(two.ExhaustionSeenDays, Is.EqualTo(1));
            Assert.That(two.StoppedExhaustedDays, Is.EqualTo(0));
            Assert.That(two.ZeroActionDays, Is.EqualTo(0));
            Assert.That(two.NightsAwake, Is.EqualTo(1));
            Assert.That(two.AverageEnergyAtEnd, Is.EqualTo(5f).Within(0.001f));
            Assert.That(two.AverageSleptAtMinutes, Is.EqualTo(1300f).Within(0.01f));
            Assert.That(two.IncomePerDay, Is.EqualTo(75f).Within(0.001f));

            var awake = new RunResult { StrategyId = "t", TargetId = "" };
            awake.Days.Add(new DayRecord { Day = 1, Slept = false });
            Assert.That(RunSummary.From(awake).AverageSleptAtMinutes, Is.Null, "잔 날이 없으면 평균 잔 시각은 없다");
            Assert.Throws<ArgumentOutOfRangeException>(() => RunSummary.From(run, 5));
        }
    }
}
