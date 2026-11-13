using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Simulation.Pace;

namespace Rootborn.Simulation.Tests
{
    /// <summary>자동 테스트: 속도 보고서 세 파일(report.md, timeline.csv, novelty.csv)이 생기고 절·열이 있다(손으로 만든 걸음 목록).</summary>
    public sealed class PaceReportTests
    {
        private string _dir;

        [SetUp] public void MakeDir() => _dir = Path.Combine(Path.GetTempPath(), "rootborn-pace-" + Guid.NewGuid().ToString("N"));
        [TearDown] public void RemoveDir() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        private static List<PaceStep> Steps()
        {
            var steps = new List<PaceStep>
            {
                new PaceStep { Index = 0, Day = 1, Minutes = 420, Category = PaceCategories.ReadChoose, Frames = 5, NewChars = 40, Choices = 1, Key = "button:a/b", By = "start", Top = "a" },
                new PaceStep { Index = 1, Day = 1, Minutes = 421, Category = PaceCategories.Walk, Frames = 600, Key = "walk:npc/x,y", By = "main-chain", Map = "m" },
                new PaceStep { Index = 2, Day = 2, Minutes = 425, Category = PaceCategories.Minigame, Frames = 120, Challenge = 0, MinigameLimitSeconds = 30, Key = "wait", By = "main-chain" },
            };
            steps[0].Novelties.Add(new PaceNovelty("처음 연 화면", "a", false));
            steps[2].Novelties.Add(new PaceNovelty("체인 끝", "chain \"q\"", true));
            return steps;
        }

        [Test]
        public void WritesThreeFiles_WithTheEvidenceLineFirst_AndTheSections()
        {
            var info = new PaceRunInfo { Name = "t", Policy = "main-chain", Seed = 1, End = "끝" };
            PaceReport.Write(_dir, info, Steps(), 1.0 / 60.0);
            string md = File.ReadAllText(Path.Combine(_dir, "report.md"));
            string[] lines = md.Split('\n');
            Assert.That(lines.Take(3).Any(l => l.Contains("자동 테스트(실제 플레이 아님)") && l.Contains("사람 쪽 시간은 가정")), "맨 위에 근거 종류");
            foreach (string section in new[] { "## 실제 시간 범위", "## 지점까지 걸린 시간", "## 하루 시간 쓰임", "## 가장 긴 지루한 구간", "## 되풀이", "## 빈 날", "## 가정 값" })
                Assert.That(md, Does.Contain(section), section);
            Assert.That(md, Does.Contain("chain \"q\""), "지점 이름이 표에");
        }

        [Test]
        public void TimelineAndNoveltyCsv_HaveAHeaderAndARowPerStepOrEvent_QuotingCommas()
        {
            PaceReport.Write(_dir, new PaceRunInfo { Name = "t", Policy = "p" }, Steps(), 1.0 / 60.0);
            string[] timeline = File.ReadAllLines(Path.Combine(_dir, "timeline.csv"));
            Assert.That(timeline[0], Is.EqualTo("step,day,clock,category,game_seconds,game_minutes,new_chars,choices,minigame_limit_seconds,human_seconds_baseline,real_seconds_baseline,by,top,key,novelties"));
            Assert.That(timeline, Has.Length.EqualTo(1 + 3));
            Assert.That(timeline[2], Does.Contain("\"walk:npc/x,y\""), "쉼표가 든 칸은 따옴표");
            Assert.That(timeline[1].Split(',')[2], Is.EqualTo("07:00"));
            string[] novelty = File.ReadAllLines(Path.Combine(_dir, "novelty.csv"));
            Assert.That(novelty[0], Is.EqualTo("step,day,clock,kind,what,milestone,real_seconds_baseline"));
            Assert.That(novelty, Has.Length.EqualTo(1 + 2));
            Assert.That(novelty[2], Does.Contain("\"chain \"\"q\"\"\""), "따옴표는 두 번");
        }
    }
}
