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

        // "\n"으로만 끊는다. 끝의 빈 조각은 줄이 아니다.
        private static string[] Lines(string path)
        {
            var text = File.ReadAllText(path);
            Assert.That(text, Does.Not.Contain("\r"), "줄 끝은 \\n이다: " + path);
            return text.TrimEnd('\n').Split('\n');
        }
    }
}
