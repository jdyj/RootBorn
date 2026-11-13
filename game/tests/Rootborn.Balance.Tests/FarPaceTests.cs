using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Tests;
using Rootborn.Simulation.Pace;

namespace Rootborn.Balance.Tests
{
    /// <summary>
    /// 자동 테스트(실제 플레이 아님): 먼 층 — 균형 시뮬레이터가 GameSession 명령으로 도는 판에 속도 기록을 붙인다. 걷기는 가까운 층에서 잰 값
    /// (pace-measured.json, 없으면 균형 시뮬레이터의 가정), 읽기·고르기는 가까운 층의 상호작용 종류별 평균(어림). 100일은 손으로만(<see cref="WriteFarReport"/>).
    /// </summary>
    [NonParallelizable] // 덮어쓰기 테스트가 TestWorld의 정의를 바꾼다
    public sealed class FarPaceTests
    {
        private string _dir;

        [SetUp] public void MakeDir() { _dir = Path.Combine(Path.GetTempPath(), "rootborn-far-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
        [TearDown] public void RemoveDir() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

        private static PaceMeasured Measured()
        {
            var m = new PaceMeasured { Source = "시험" };
            m.WalkSecondsByKind[PlannedStep.PartTimeKind] = new PaceMeasured.Mean { Value = 30, Count = 1 };
            m.ScreenByKind[PlannedStep.PartTimeKind] = new PaceMeasured.Screen { Chars = 400, Choices = 2, Count = 1 };
            return m;
        }

        private static StrategySpec Strategy(string id) => BalanceConfig.Read().Strategies.First(s => s.Id == id);

        [Test]
        public void MeasuredWalksAndScreens_ReplaceTheAssumptions_ForTheirKind()
        {
            FarRun run = FarPace.Run(BalanceConfig.Read(), Strategy("one-part-time-work"), 2, Measured());
            var walks = run.Steps.Where(s => s.Category == PaceCategories.Walk && s.InteractionKind == PlannedStep.PartTimeKind).ToList();
            Assert.That(walks, Is.Not.Empty);
            Assert.That(walks.All(s => Math.Abs(s.GameSeconds(FarPace.SecondsPerFrame) - 30) < 0.05), "알바까지 걷기는 잰 30초");
            var acts = run.Steps.Where(s => s.Category == PaceCategories.ReadChoose && s.InteractionKind == PlannedStep.PartTimeKind).ToList();
            Assert.That(acts.All(s => s.NewChars == 400 && s.Choices == 2), "알바 화면은 잰 평균 글자·고르기");
            Assert.That(run.Steps.Any(s => s.Category == PaceCategories.Minigame && s.MinigameLimitSeconds > 0), "미니게임은 제한 시간으로");
            Assert.That(run.Steps.Count(s => s.Category == PaceCategories.Sleep), Is.EqualTo(2), "밤마다 잠");
            Assert.That(run.MeasuredKinds, Does.Contain(PlannedStep.PartTimeKind));
            Assert.That(run.Steps.SelectMany(s => s.Novelties), Is.Not.Empty, "새로운 것을 GameSession 조회로 센다");
            Assert.That(run.Result.Days, Has.Count.EqualTo(2));
        }

        [Test]
        public void WithoutAMeasuredFile_WalksUseTheBalanceAssumption_AndTheReportSaysSo()
        {
            var config = BalanceConfig.Read();
            FarRun run = FarPace.Run(config, Strategy("one-part-time-work"), 1, null);
            float assumed = DayAssumptions.RealSeconds(TestWorld.Definitions.TimeRules, RunSetup.Baseline(config).TravelMinutes);
            Assert.That(run.Steps.Where(s => s.Category == PaceCategories.Walk).All(s => Math.Abs(s.GameSeconds(FarPace.SecondsPerFrame) - assumed) < 0.05));
            Assert.That(run.AssumedKinds, Does.Contain(PlannedStep.PartTimeKind));
            string dir = FarPace.Write(_dir, "t", config, new[] { run }, null);
            string md = File.ReadAllText(Path.Combine(dir, "report.md"));
            Assert.That(md.Split('\n').Take(4).Any(l => l.Contains("자동 테스트(실제 플레이 아님)")));
            Assert.That(md, Does.Contain("가정").And.Contain(PlannedStep.PartTimeKind));
        }

        /// <summary>평소 테스트: 전략마다 3일 — far-short/report.md(전략마다 열, 날마다 새로운 것, 하루 평균 수입).</summary>
        [Test]
        public void EveryStrategy_ShortFarRun_WritesTheReport()
        {
            var config = BalanceConfig.Read();
            string dir = FarPace.RunAndWrite("far-short", config, 3, FarPace.ReadMeasured(), out var runs);
            string md = File.ReadAllText(Path.Combine(dir, "report.md"));
            string header = md.Split('\n').First(l => l.StartsWith("| 항목 |"));
            foreach (var s in config.Strategies) Assert.That(header, Does.Contain(s.Id), s.Id);
            Assert.That(md, Does.Contain("## 날마다 새로운 것").And.Contain("하루 평균 수입"));
            Assert.That(runs.Any(r => r.Steps.SelectMany(s => s.Novelties).Any(n => n.Kind == PaceNoveltyCatalog.Adult)), "성인으로 시작하는 전략은 성인이 됨 지점");
            Assert.That(PaceBands.Check(PaceBands.Read(PaceBands.FilePath(TestWorld.RepoRoot)), PaceBands.Far, FarPace.Entries(runs), FarPace.SecondsPerFrame), Is.Empty, "범위 밖(빈 범위 파일이면 없다)");
            TestContext.WriteLine(dir);
        }

        /// <summary>덮어쓰기 전후(먼 층): 알바 보상을 올리면 하루 평균 수입이 달라지고 맨 위에 바뀐 필드가 적힌다.</summary>
        [Test]
        public void AnOverlay_ChangesTheFarLayerIncome_AndTheReportListsTheField()
        {
            Directory.CreateDirectory(Path.Combine(_dir, "overlay", "studentlife"));
            File.WriteAllText(Path.Combine(_dir, "overlay", "studentlife", "PartTimeWorkDefinition.json"),
                @"[{""type"":""PartTimeWorkDefinition"",""id"":""work.clinic-helper"",""rewardCounts"":""0a000000""}]");
            var config = BalanceConfig.Read();
            config.Strategies = config.Strategies.Where(s => s.Id == "rotate-part-time-work").ToList();
            string dir = FarPace.CompareOverlay("far-overlay-test", config, 3, null, Path.Combine(_dir, "overlay"));
            string md = File.ReadAllText(Path.Combine(dir, "report.md"));
            Assert.That(md, Does.Contain("PartTimeWorkDefinition/work.clinic-helper.rewardCounts"));
            string income = md.Split('\n').SkipWhile(l => !l.StartsWith("### 하루 평균 수입")).First(l => l.StartsWith("| rotate"));
            Assert.That(income, Does.Not.EndWith("| +0.0 |"), income);
            Assert.That(TestWorld.OverlayChanges, Is.Empty, "덮어쓰기를 끝내면 원래 정의로 돌아온다");
        }

        /// <summary>
        /// 손으로만: 전략마다 ROOTBORN_PACE_FAR_DAYS일(기본 100) — output/simulation/pace/far-&lt;N&gt;days/report.md. 잰 값은 ROOTBORN_PACE_MEASURED 또는
        /// output/simulation/pace/measured/pace-measured.json(Simulation WriteCompareReport가 쓴다). ROOTBORN_DATA_OVERLAY가 있으면 전후 비교.
        /// </summary>
        [Test, Explicit("손으로만: 전략마다 100일 먼 층 속도 보고서를 쓴다"), Category("PaceReport")]
        public void WriteFarReport()
        {
            int days = FarPace.Days(Environment.GetEnvironmentVariable(FarPace.DaysVariable));
            var config = BalanceConfig.Read();
            string overlay = Environment.GetEnvironmentVariable(TestWorld.OverlayVariable);
            string dir;
            if (!string.IsNullOrWhiteSpace(overlay))
            {
                using (TestWorld.UseOverlay("")) dir = FarPace.CompareOverlay("far-" + days + "days-overlay", config, days, FarPace.ReadMeasured(), overlay);
            }
            else
            {
                dir = FarPace.RunAndWrite("far-" + days + "days", config, days, FarPace.ReadMeasured(), out var runs);
                Assert.That(PaceBands.Check(PaceBands.Read(PaceBands.FilePath(TestWorld.RepoRoot)), PaceBands.Far, FarPace.Entries(runs), FarPace.SecondsPerFrame), Is.Empty, "범위 밖");
            }
            TestContext.WriteLine(dir);
        }
    }
}
