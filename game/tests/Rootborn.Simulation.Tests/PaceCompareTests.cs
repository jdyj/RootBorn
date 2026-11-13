using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Simulation.Pace;

namespace Rootborn.Simulation.Tests
{
    /// <summary>
    /// 자동 테스트(실제 플레이 아님): 플레이어 유형을 나란히 보는 보고서 — 유형마다 열 하나. 손으로 만든 걸음 목록으로 열·절을 보고,
    /// 유형 넷을 하루씩 실제로 돌려 output/simulation/pace/compare-day1/report.md를 쓴다. 7일은 손으로만(<see cref="WriteCompareReport"/>).
    /// </summary>
    [TestFixture, NonParallelizable]
    public sealed class PaceCompareTests
    {
        private string _dir;

        [SetUp] public void MakeDir() => _dir = Path.Combine(Path.GetTempPath(), "rootborn-compare-" + Guid.NewGuid().ToString("N"));
        [TearDown] public void RemoveDir() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        private static PaceCompareEntry Entry(string type, int days, bool milestone)
        {
            var steps = new List<PaceStep>();
            for (int d = 1; d <= days; d++)
            {
                var s = new PaceStep { Index = steps.Count, Day = d, Minutes = 420, Category = PaceCategories.Walk, Frames = 600, Key = "walk:x" };
                if (d == 1) s.Novelties.Add(new PaceNovelty("처음 들어간 맵", "m", false));
                if (milestone && d == 2) s.Novelties.Add(new PaceNovelty("체인 끝", "chain.a", true));
                steps.Add(s);
                steps.Add(new PaceStep { Index = steps.Count, Day = d, Minutes = 500, Category = PaceCategories.NothingToDo, Frames = 300, Key = "idle:600" });
            }
            return new PaceCompareEntry { Type = type, Info = new PaceRunInfo { Name = type, Policy = type, DaysCompleted = days, End = "끝" }, Steps = steps };
        }

        [Test]
        public void TheCompareReport_HasAColumnPerType_AndTheSections()
        {
            PaceCompare.Write(_dir, "t", new[] { Entry(PlayerTypeCatalog.Story, 3, true), Entry(PlayerTypeCatalog.OneThing, 3, false) }, 1.0 / 60.0);
            string md = File.ReadAllText(Path.Combine(_dir, "report.md"));
            Assert.That(md.Split('\n').Take(3).Any(l => l.Contains("자동 테스트(실제 플레이 아님)")));
            string header = md.Split('\n').First(l => l.StartsWith("| 항목 |"));
            Assert.That(header, Does.Contain(PlayerTypeCatalog.Label(PlayerTypeCatalog.Story)).And.Contain(PlayerTypeCatalog.Label(PlayerTypeCatalog.OneThing)));
            foreach (string section in new[] { "## 한눈에", "## 하루 평균 시간 쓰임", "## 지점까지 걸린 시간", "## 되풀이 상위" })
                Assert.That(md, Does.Contain(section), section);
            Assert.That(md, Does.Contain("chain.a"));
            string milestoneRow = md.Split('\n').First(l => l.StartsWith("| chain.a"));
            Assert.That(milestoneRow, Does.Contain("2일째").And.Contain("닿지 못함"), "닿은 유형과 못 닿은 유형");
            string emptyRow = md.Split('\n').First(l => l.StartsWith("| 빈 날"));
            Assert.That(emptyRow, Does.Contain("2일(처음 2일째)"));
        }

        [Test]
        public void TheFourTypes_PlayOneDayEach_AndTheCompareReportIsWritten()
        {
            var settings = new PlaySettings { Days = 1, StepsPerDay = 5000, WindDownSteps = 120, MaxSteps = 5000 };
            string dir = PacePlay.Compare("compare-day1", settings, 1);
            string md = File.ReadAllText(Path.Combine(dir, "report.md"));
            foreach (string type in PlayerTypeCatalog.Names) Assert.That(md, Does.Contain(PlayerTypeCatalog.Label(type)), type);
            foreach (string type in PlayerTypeCatalog.Names) Assert.That(File.Exists(Path.Combine(PaceReport.Directory(PlayAtlas.FindRepoRoot(), "compare-day1-" + type), "report.md")), "유형마다 보고서도");
            TestContext.WriteLine(dir);
        }

        /// <summary>손으로만: 유형 넷을 ROOTBORN_PACE_DAYS일(기본 7) 돌려 compare-&lt;N&gt;days/report.md. 알려진 결함이 아닌 실패가 있으면 보고서를 쓴 뒤 실패한다.</summary>
        [Test, Explicit("손으로만: 플레이어 유형 넷을 N일 돌려 output/simulation/pace/compare-<N>days 에 비교 보고서를 쓴다"), Category("PaceReport")]
        public void WriteCompareReport()
        {
            int days = PacePlay.ParseDays(Environment.GetEnvironmentVariable(PacePlay.DaysVariable));
            var settings = new PlaySettings { Days = days, StepsPerDay = 5000, WindDownSteps = 120, MaxSteps = 5000 * days };
            string dir = PacePlay.Compare("compare-" + days + "days", settings, 1, out List<string> failures);
            TestContext.WriteLine(dir);
            Assert.That(failures, Is.Empty);
        }
    }
}
