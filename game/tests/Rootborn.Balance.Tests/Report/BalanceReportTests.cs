using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Careers;
using Rootborn.Game.Tests;

namespace Rootborn.Balance.Tests
{
    /// <summary>보고서 파일 셋(days.csv, summary.csv, report.md). 자동 테스트다 — 표의 숫자는 가정을 넣은 계산이다.</summary>
    [NonParallelizable] // 환경 변수를 바꾸는 테스트가 있다
    public sealed class BalanceReportTests
    {
        private const int ShortDays = 3;

        private static readonly Lazy<BalanceConfig> Config = new Lazy<BalanceConfig>(() =>
        {
            var config = BalanceConfig.Read();
            config.Validate();
            return config;
        });

        // 여러 테스트가 같은 짧은 판들을 본다. 한 번만 돌린다.
        private static readonly Lazy<List<RunResult>> ShortRuns = new Lazy<List<RunResult>>(() => BalanceRunner.RunAll(Config.Value, ShortDays).ToList());

        private string _temp;

        [SetUp]
        public void MakeTemp()
        {
            _temp = Path.Combine(Path.GetTempPath(), "rootborn-balance-report-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void RemoveTemp()
        {
            if (Directory.Exists(_temp)) Directory.Delete(_temp, true);
        }

        [Test]
        public void WritesThreeFilesWithOneRowPerDayAndPerRun()
        {
            var runs = ShortRuns.Value;
            BalanceReport.Write(_temp, Config.Value, ShortDays, runs);

            Assert.That(Lines(Path.Combine(_temp, BalanceReport.DaysFile)).Length, Is.EqualTo(1 + runs.Sum(r => r.Days.Count)), "머리 줄 + 날마다 한 줄");
            Assert.That(Lines(Path.Combine(_temp, BalanceReport.SummaryFile)).Length, Is.EqualTo(1 + runs.Count), "머리 줄 + 판마다 한 줄");
            Assert.That(File.ReadAllText(Path.Combine(_temp, BalanceReport.ReportFile)), Does.Contain("실제 플레이가 아니다"));
        }

        [Test]
        public void WritingTwiceGivesIdenticalFiles()
        {
            var runs = ShortRuns.Value;
            string a = Path.Combine(_temp, "a"), b = Path.Combine(_temp, "b");
            BalanceReport.Write(a, Config.Value, ShortDays, runs);
            BalanceReport.Write(b, Config.Value, ShortDays, BalanceRunner.RunAll(Config.Value, ShortDays).ToList());
            foreach (var file in new[] { BalanceReport.DaysFile, BalanceReport.SummaryFile, BalanceReport.ReportFile })
                Assert.That(File.ReadAllBytes(Path.Combine(b, file)), Is.EqualTo(File.ReadAllBytes(Path.Combine(a, file))), file);
        }

        [Test]
        public void ReportHasATablePerStrategyAndARowPerTarget()
        {
            var config = Config.Value;
            BalanceReport.Write(_temp, config, ShortDays, ShortRuns.Value);
            var lines = Lines(Path.Combine(_temp, BalanceReport.ReportFile));

            int facilities = TestWorld.Definitions.All<Facility>().Count();
            Assert.That(facilities, Is.GreaterThan(0));
            foreach (var strategy in config.Strategies)
            {
                int heading = Array.IndexOf(lines, "## " + strategy.Id);
                Assert.That(heading, Is.GreaterThanOrEqualTo(0), "전략 제목이 없다: " + strategy.Id);
                var table = lines.Skip(heading + 1).SkipWhile(l => !l.StartsWith("|")).TakeWhile(l => l.StartsWith("|")).ToList();
                int rows = table.Count - 2; // 머리 줄과 구분 줄
                int expected = RunTargets.All(strategy.Each, TestWorld.Definitions).Count;
                Assert.That(rows, Is.EqualTo(expected), "표의 줄 수가 대상 수와 다르다: " + strategy.Id);
                if (strategy.Each == "facility") Assert.That(rows, Is.EqualTo(facilities), strategy.Id);
            }
        }

        [Test]
        public void EmptyRatesAreBlankNotZero()
        {
            var config = Config.Value;
            var run = new RunResult { StrategyId = config.Strategies[0].Id, TargetId = "", TierId = "", StartDay = 1, StartWallet = 0 };
            run.Days.Add(new DayRecord { Day = 1, Stop = StopReasons.NothingToDo });
            BalanceReport.Write(_temp, config, 1, new[] { run });

            var lines = Lines(Path.Combine(_temp, BalanceReport.SummaryFile));
            var header = lines[0].Split(',');
            var row = lines[1].Split(',');
            foreach (var column in new[] { "income_per_game_hour", "income_per_real_minute_with_travel", "income_per_real_minute_minigame_only", "level2_day", "level3_day", "first_auto_skip_day", "first_bag_full_day" })
            {
                int at = Array.IndexOf(header, column);
                Assert.That(at, Is.GreaterThanOrEqualTo(0), "칸이 없다: " + column);
                Assert.That(row[at], Is.EqualTo(""), "아무것도 못 한 판의 " + column + "은 빈칸이다");
            }
            Assert.That(row[Array.IndexOf(header, "income_per_day")], Is.EqualTo("0"), "하루 수입은 0이다(빈칸이 아니다)");
        }

        [Test]
        public void CsvColumnsSayWhatTheyHold()
        {
            BalanceReport.Write(_temp, Config.Value, ShortDays, ShortRuns.Value);
            var days = Lines(Path.Combine(_temp, BalanceReport.DaysFile))[0].Split(',');
            Assert.That(days, Does.Contain("minigame_earned"));
            Assert.That(days, Does.Contain("other_items_held"));
            Assert.That(days, Does.Not.Contain("other_items"));
            var summary = Lines(Path.Combine(_temp, BalanceReport.SummaryFile))[0].Split(',');
            Assert.That(summary, Does.Contain("payback_days").And.Contain("payback_gaps").And.Contain("recover_days"));
            Assert.That(File.ReadAllText(Path.Combine(_temp, BalanceReport.ReportFile)), Does.Contain("other_items_held"), "범례에 그날 끝 개수라고 적는다");
        }

        // 이틀 연속 올린 판: 두 칸이 나란히, 셀 수 없는 것은 "구간 없음"으로.
        [Test]
        public void UpgradeColumnsShowRecoverDaysAndWhyPaybackIsMissing()
        {
            var strategy = Spec("s-up");
            var run = Hand(strategy, "x", Day(1, 100), Day(2, 100, spent: 50, upgraded: true), Day(3, 100, spent: 50, upgraded: true), Day(4, 300));
            BalanceReport.Write(_temp, With(strategy), 4, new[] { run });

            var text = File.ReadAllText(Path.Combine(_temp, BalanceReport.ReportFile));
            var table = Table(text, strategy.Id);
            Assert.That(Cell(table, "x", BalanceReport.RecoverColumn), Is.EqualTo("1·1"));
            Assert.That(Cell(table, "x", BalanceReport.PaybackColumn), Is.EqualTo(BalanceReport.NoSpanLabel + "·" + BalanceReport.NoSpanLabel));
            Assert.That(text, Does.Contain(BalanceReport.NoSpanLabel + "`").And.Contain(BalanceReport.NoGainLabel + "`"), "범례에 두 글자를 적는다");
        }

        [Test]
        public void RatioLineSaysAllEqual_AndIsLeftOutForASingleTarget()
        {
            var equal = Spec("s-equal");
            var single = Spec("s-single");
            var runs = new[]
            {
                Hand(equal, "a", Day(1, 50)), Hand(equal, "b", Day(1, 50)),
                Hand(single, "only", Day(1, 70)),
            };
            BalanceReport.Write(_temp, With(equal, single), 1, runs);
            var text = File.ReadAllText(Path.Combine(_temp, BalanceReport.ReportFile));
            Assert.That(Section(text, equal.Id), Does.Contain("모두 같음 (50.0)").And.Not.Contain("÷"));
            Assert.That(Section(text, single.Id), Does.Not.Contain("하루 수입 가장").And.Not.Contain("모두 같음"));
        }

        [Test]
        public void StrategyNoteIsPrintedUnderItsDescription()
        {
            var noted = Spec("s-noted");
            noted.Note = "설명 한 줄이다.";
            BalanceReport.Write(_temp, With(noted), 1, new[] { Hand(noted, "a", Day(1, 10)) });
            Assert.That(Section(File.ReadAllText(Path.Combine(_temp, BalanceReport.ReportFile)), noted.Id), Does.Contain("설명 한 줄이다."));
        }

        // 표의 모든 줄에서 값이 없는 칸은 뺀다(대상 종류와 무관하게).
        [Test]
        public void ColumnsWithNoValueInAnyRowAreLeftOut()
        {
            var config = Config.Value;
            BalanceReport.Write(_temp, config, ShortDays, ShortRuns.Value);
            var text = File.ReadAllText(Path.Combine(_temp, BalanceReport.ReportFile));
            foreach (var strategy in config.Strategies)
            {
                var table = Table(text, strategy.Id);
                for (int c = 2; c < table[0].Length; c++)
                    Assert.That(table.Skip(2).Any(row => row[c] != "-"), Is.True, strategy.Id + ": 값이 하나도 없는 칸이 남았다: " + table[0][c]);
            }
            Assert.That(Table(text, "one-part-time-work")[0].Any(h => h.Contains("레벨")), Is.False, "알바 표에 레벨 칸이 없다");
            Assert.That(text, Does.Contain(BalanceReport.DroppedColumnsLine), "뺀 칸을 적는다");
        }

        [Test]
        public void BadDaysVariableFailsAndNamesIt()
        {
            var before = Environment.GetEnvironmentVariable(BalanceReport.DaysVariable);
            try
            {
                foreach (var bad in new[] { "abc", "0" })
                {
                    Environment.SetEnvironmentVariable(BalanceReport.DaysVariable, bad);
                    var e = Assert.Throws<InvalidOperationException>(() => BalanceReport.Days(Config.Value));
                    Assert.That(e.Message, Does.Contain(BalanceReport.DaysVariable).And.Contain(bad), bad);
                }
                Environment.SetEnvironmentVariable(BalanceReport.DaysVariable, "7");
                Assert.That(BalanceReport.Days(Config.Value), Is.EqualTo(7));
            }
            finally
            {
                Environment.SetEnvironmentVariable(BalanceReport.DaysVariable, before);
            }
        }

        [Test]
        public void DaysAndOutputFallBackToConfigAndRepoOutput()
        {
            var days = Environment.GetEnvironmentVariable(BalanceReport.DaysVariable);
            var output = Environment.GetEnvironmentVariable(BalanceReport.OutVariable);
            try
            {
                Environment.SetEnvironmentVariable(BalanceReport.DaysVariable, null);
                Environment.SetEnvironmentVariable(BalanceReport.OutVariable, null);
                Assert.That(BalanceReport.Days(Config.Value), Is.EqualTo(Config.Value.Days));
                Assert.That(BalanceReport.OutputDirectory(), Is.EqualTo(Path.Combine(TestWorld.RepoRoot, "output", "balance")));

                Environment.SetEnvironmentVariable(BalanceReport.OutVariable, _temp);
                Assert.That(BalanceReport.OutputDirectory(), Is.EqualTo(_temp));
            }
            finally
            {
                Environment.SetEnvironmentVariable(BalanceReport.DaysVariable, days);
                Environment.SetEnvironmentVariable(BalanceReport.OutVariable, output);
            }
        }

        // ---- 손으로 만든 판 ----

        private static StrategySpec Spec(string id)
            => new StrategySpec { Id = id, Each = "facility", Start = "adult", Scored = false, Do = new[] { "best-facility-work" } };

        private static BalanceConfig With(params StrategySpec[] strategies)
            => new BalanceConfig { Days = 1, Sleep = Config.Value.Sleep, Tiers = Config.Value.Tiers, Strategies = strategies };

        private static DayRecord Day(int day, int earned, int spent = 0, bool upgraded = false)
            => new DayRecord { Day = day, Earned = earned, MinigameEarned = earned, Spent = spent, Upgraded = upgraded, Actions = 1, Stop = StopReasons.Bedtime };

        private static RunResult Hand(StrategySpec strategy, string target, params DayRecord[] days)
        {
            var run = new RunResult { StrategyId = strategy.Id, TargetId = target, TierId = "", StartDay = 1 };
            run.Days.AddRange(days);
            return run;
        }

        // 그 전략 제목부터 다음 전략 제목 앞까지.
        private static string Section(string text, string strategyId)
        {
            int at = text.IndexOf("\n## " + strategyId + "\n", StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThanOrEqualTo(0), "전략 제목이 없다: " + strategyId);
            int next = text.IndexOf("\n## ", at + 1, StringComparison.Ordinal);
            return next < 0 ? text.Substring(at) : text.Substring(at, next - at);
        }

        // 그 전략 표의 칸들(머리 줄, 구분 줄, 줄들).
        private static List<string[]> Table(string text, string strategyId)
            => Section(text, strategyId).Split('\n').Where(l => l.StartsWith("|"))
                .Select(l => l.Trim('|').Split(" | ").Select(c => c.Trim()).ToArray()).ToList();

        private static string Cell(List<string[]> table, string target, string column)
        {
            int c = Array.IndexOf(table[0], column);
            Assert.That(c, Is.GreaterThanOrEqualTo(0), "칸이 없다: " + column + " | " + string.Join(", ", table[0]));
            return table.Skip(2).Single(row => row[0] == target)[c];
        }

        // "\n"으로만 끊는다. 끝의 빈 조각은 줄이 아니다.
        private static string[] Lines(string path)
        {
            var text = File.ReadAllText(path);
            Assert.That(text, Does.Not.Contain("\r"), "줄 끝은 \\n이다: " + path);
            return text.TrimEnd('\n').Split('\n');
        }
    }
}
