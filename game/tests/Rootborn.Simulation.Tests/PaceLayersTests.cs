using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Simulation.Pace;

namespace Rootborn.Simulation.Tests
{
    /// <summary>
    /// 자동 테스트(실제 플레이 아님): 가까운 층이 먼 층에 넘기는 잰 값 파일, 범위 검사의 틀, 덮어쓰기 전후 비교 보고서.
    /// 손으로 만든 걸음 목록으로 계산을 보고, 실제 데이터로는 유형 넷 하루를 덮어쓰기 전후로 돌린다.
    /// </summary>
    [TestFixture, NonParallelizable]
    public sealed class PaceLayersTests
    {
        private const double Fps = 1.0 / 60.0;
        private string _dir;

        [SetUp] public void MakeDir() => _dir = Path.Combine(Path.GetTempPath(), "rootborn-layers-" + Guid.NewGuid().ToString("N"));
        [TearDown] public void RemoveDir() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        private static PaceStep Walk(int day, double seconds, string kind = null, string subject = null, bool arrives = false)
            => new PaceStep { Day = day, Category = PaceCategories.Walk, Frames = (long)(seconds * 60), InteractionKind = kind, Subject = subject, Key = arrives ? "walk:" + kind + "/" + subject : "door:" + subject };

        private static PaceStep Screen(int day, int chars, int choices, string category = PaceCategories.ReadChoose)
            => new PaceStep { Day = day, Category = category, NewChars = chars, Choices = choices, Key = "button:x" };

        [Test]
        public void Measured_WalksFromSpotToSpot_AndScreenTextPerKind()
        {
            var steps = new List<PaceStep>
            {
                Walk(1, 4, "door", "town"), Walk(1, 6, "work", "w1", arrives: true),     // 그날 처음 → w1: 10초
                Screen(1, 100, 2), Screen(1, 0, 1, PaceCategories.Minigame), Screen(1, 20, 1),  // w1 화면: 120자, 고르기 3(미니게임 빼고)
                Walk(1, 8, "work", "w2", arrives: true),                                  // w1 → w2: 8초
                Screen(1, 60, 1),
                Walk(2, 3, "work", "w1", arrives: true),                                  // 다음 날: 그날 처음 → w1
            };
            PaceMeasured m = PaceMeasured.FromRuns(new[] { ("t", (IReadOnlyList<PaceStep>)steps) }, Fps);
            Assert.That(m.WalkSeconds("work"), Is.EqualTo((10 + 8 + 3) / 3.0).Within(1e-6));
            Assert.That(m.Walks.Single(w => w.From == PaceMeasured.DayStart && w.To == "w1").Seconds, Is.EqualTo((10 + 3) / 2.0).Within(1e-6));
            Assert.That(m.Walks.Single(w => w.From == "w1" && w.To == "w2").Seconds, Is.EqualTo(8).Within(1e-6));
            PaceMeasured.Screen s = m.ScreenFor("work");
            Assert.That(s.Count, Is.EqualTo(3));
            Assert.That(s.Chars, Is.EqualTo((120 + 60 + 0) / 3.0).Within(1e-6));
            Assert.That(s.Choices, Is.EqualTo((3 + 1 + 0) / 3.0).Within(1e-6));
            Assert.That(m.WalkSeconds("none"), Is.Null);

            string file = Path.Combine(_dir, PaceMeasured.FileName);
            m.Write(file);
            PaceMeasured back = PaceMeasured.Read(file);
            Assert.That(back.WalkSeconds("work"), Is.EqualTo(m.WalkSeconds("work")).Within(1e-9), "파일로 쓰고 다시 읽는다");
            Assert.That(PaceMeasured.Read(Path.Combine(_dir, "none.json")), Is.Null);
        }

        private static PaceCompareEntry Entry(string type, double walkSeconds, bool milestone)
        {
            var steps = new List<PaceStep> { Walk(1, walkSeconds), new PaceStep { Day = 2, Category = PaceCategories.NothingToDo, Frames = 600, Key = "idle" } };
            steps[0].Novelties.Add(new PaceNovelty("새것", "a", false));
            if (milestone) steps[1].Novelties.Add(new PaceNovelty("체인 끝", "c", true));
            return new PaceCompareEntry { Type = type, Label = type, Info = new PaceRunInfo { Name = type }, Steps = steps, DailyIncome = new[] { 10, 20 } };
        }

        [Test]
        public void TheShippedBandsFile_IsEmpty_SoNothingIsBlocked()
        {
            List<PaceBand> bands = PaceBands.Read(PaceBands.FilePath(PlayAtlas.FindRepoRoot()));
            Assert.That(bands, Is.Empty, "값은 사용자가 정한다");
            Assert.That(PaceBands.Check(bands, PaceBands.Near, new[] { Entry("a", 60, false) }, Fps), Is.Empty);
        }

        [Test]
        public void Bands_CatchTooLong_TooShort_Unreached_AndUnknownMetrics_ForTheRightWhoAndLayer()
        {
            string file = Path.Combine(_dir, PaceBands.FileName);
            Directory.CreateDirectory(_dir);
            File.WriteAllText(file, @"{""bands"":[
                {""metric"":""total-minutes"",""max"":1},
                {""metric"":""total-minutes"",""who"":""b"",""min"":100},
                {""metric"":""milestone-minutes:체인 끝"",""layer"":""near""},
                {""metric"":""total-minutes"",""layer"":""far"",""max"":0},
                {""metric"":""typo""}]}");
            List<PaceBand> bands = PaceBands.Read(file);
            List<string> outside = PaceBands.Check(bands, PaceBands.Near, new[] { Entry("a", 120, true), Entry("b", 10, false) }, Fps);
            Assert.That(outside.Count(o => o.Contains("total-minutes") && o.EndsWith("a: 2.167")), Is.EqualTo(1), "a: 2분 10초 > 1분");
            Assert.That(outside.Count(o => o.Contains("/b/total-minutes")), Is.EqualTo(1), "b만 보는 범위");
            Assert.That(outside, Has.Some.Contains("닿지 못함"), "b는 지점에 못 닿았다");
            Assert.That(outside.Count(o => o.Contains("모르는 지표")), Is.EqualTo(2), "오타는 둘 다 범위 밖");
            Assert.That(outside, Has.None.Contains("far/"), "먼 층 범위는 가까운 층 숫자에 쓰지 않는다");
        }

        [Test]
        public void TheDiffReport_ListsChangesFirst_AndBeforeAfterDifference()
        {
            string md = PaceDiff.Markdown("t", new[] { "settings/time.json.bedtime: \"22:00\" -> \"20:00\"" },
                new[] { Entry("a", 60, false) }, new[] { Entry("a", 30, true) }, Fps, new[] { "> 자동 테스트(실제 플레이 아님)" });
            Assert.That(md.IndexOf(PaceDiff.ChangesTitle, StringComparison.Ordinal), Is.LessThan(md.IndexOf("### 판 전체", StringComparison.Ordinal)), "바뀐 필드가 맨 위");
            Assert.That(md, Does.Contain("settings/time.json.bedtime"));
            string total = md.Split('\n').SkipWhile(l => !l.StartsWith("### 판 전체")).First(l => l.StartsWith("| a |"));
            Assert.That(total, Does.Contain("1분 10초").And.Contain("40초").And.Contain("-30초"));
            Assert.That(md, Does.Contain("새로 닿음"));
            Assert.That(md, Does.Contain("### 하루 평균 수입"));
        }

        /// <summary>실제 데이터: 취침 시각을 덮어쓰기로 20:00으로 당기면 유형 넷의 하루가 달라지고, 전후 보고서 맨 위에 그 필드가 적힌다.</summary>
        [Test]
        public void ARealOverlay_ChangesTheNearLayerNumbers_AndTheReportSaysSo()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(Path.Combine(_dir, "time.json"), @"{""bedtime"":""20:00""}");
            var settings = new PlaySettings { Days = 1, StepsPerDay = 5000, WindDownSteps = 120, MaxSteps = 5000 };
            string dir = PacePlay.CompareOverlay("overlay-day1", settings, 1, _dir, out List<string> failures);
            Assert.That(failures, Is.Empty);
            string md = File.ReadAllText(Path.Combine(dir, "report.md"));
            Assert.That(md, Does.Contain("settings/time.json.bedtime"));
            var rows = md.Split('\n').SkipWhile(l => !l.StartsWith("### 판 전체")).Skip(4).TakeWhile(l => l.StartsWith("|")).ToList();
            Assert.That(rows, Has.Count.EqualTo(PlayerTypeCatalog.Names.Count));
            Assert.That(rows, Has.Some.Not.EndsWith("| 0초 |"), "적어도 한 유형의 하루가 달라졌다: " + string.Join(" / ", rows));
            TestContext.WriteLine(dir);
        }
    }
}
