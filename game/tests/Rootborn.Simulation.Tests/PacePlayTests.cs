using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Simulation.Pace;

namespace Rootborn.Simulation.Tests
{
    /// <summary>
    /// 자동 테스트(창 없음, 실제 플레이 아님): 자동 플레이어에 속도 기록을 붙여 돌리고 output/simulation/pace/&lt;판 이름&gt;/에 보고서를 쓴다.
    /// 짧은 판(메인 체인 앞부분, 하루)은 평소 테스트로 돈다. 메인 체인 끝까지와 N일(ROOTBORN_PACE_DAYS, 기본 7)은 손으로만(<see cref="WritePaceReports"/>).
    /// </summary>
    [TestFixture, NonParallelizable]
    public sealed class PacePlayTests
    {
        private static PlaySettings Short(int maxSteps) => new PlaySettings { Days = 1, StepsPerDay = 5000, WindDownSteps = 120, MaxSteps = maxSteps };

        [Test]
        public void AShortRun_RecordsEveryStepsFrames_AndWritesTheReport()
        {
            PacePlayResult r = PacePlay.Run(PlayPolicyCatalog.MainChain, 1, Short(150), "pace-short-main-chain");
            Assert.That(r.Play.Failure, Is.Null, r.Play.Failure?.What);
            Assert.That(r.Steps.Count, Is.GreaterThanOrEqualTo(r.Play.Steps.Count), "정책의 걸음마다 속도 기록");
            Assert.That(r.Steps.Sum(s => s.Frames), Is.EqualTo(r.Play.Frames - r.FramesBeforeFirstStep), "돈 프레임을 빠짐없이 걸음에 넣는다");
            Assert.That(r.Steps.Where(s => s.Category == PaceCategories.Walk).Sum(s => s.Frames), Is.GreaterThan(0), "걸었다");
            Assert.That(r.Steps.Sum(s => s.NewChars), Is.GreaterThan(0), "화면의 글자를 관찰에서 셀 수 있다");
            Assert.That(r.Steps.Sum(s => s.Choices), Is.GreaterThan(0));
            Assert.That(r.Steps.Where(s => s.Day == 0).Sum(s => s.GameMinutes), Is.EqualTo(0), "게임 한 판 전(슬롯 선택)에는 학생 시계가 없다");
            Assert.That(r.Steps.SelectMany(s => s.Novelties).Select(n => n.Kind), Has.Some.EqualTo(PaceNoveltyCatalog.FirstScreen).And.Some.EqualTo(PaceNoveltyCatalog.FirstMap));
            foreach (string file in new[] { "report.md", "timeline.csv", "novelty.csv" })
                Assert.That(File.Exists(Path.Combine(r.OutputDir, file)), file);
            TestContext.WriteLine(r.OutputDir);
        }

        [Test]
        public void TheSameSeed_GivesTheSameTimeline()
        {
            PacePlayResult a = PacePlay.Run(PlayPolicyCatalog.OffSchool, 3, Short(80), "pace-determinism-a");
            PacePlayResult b = PacePlay.Run(PlayPolicyCatalog.OffSchool, 3, Short(80), "pace-determinism-b");
            Assert.That(File.ReadAllText(Path.Combine(b.OutputDir, "timeline.csv")), Is.EqualTo(File.ReadAllText(Path.Combine(a.OutputDir, "timeline.csv"))));
        }

        /// <summary>실제 10분 하루를 끝까지(잘 시간·지침이 하루를 끝낸다) — 정책마다 하루 보고서.</summary>
        [TestCase(PlayPolicyCatalog.MainChain)]
        [TestCase(PlayPolicyCatalog.OffSchool)]
        public void OneFullDay_PerPolicy_HasADayTable(string policy)
        {
            PacePlayResult r = PacePlay.Run(policy, 1, new PlaySettings { Days = 1, StepsPerDay = 5000, WindDownSteps = 120, MaxSteps = 5000 }, "pace-day1-" + policy);
            KnownDefects.AssertNoNewFailure(r.Play);
            Assert.That(r.Play.DaysCompleted, Is.GreaterThanOrEqualTo(1), "하루를 넘겼다");
            Assert.That(File.ReadAllText(Path.Combine(r.OutputDir, "report.md")), Does.Contain("| 1 |"), "1일째 줄");
        }

        /// <summary>
        /// 보고서 풀이의 근거(흐르는 시간 설계 2절): 혼자 하기에서 화면 없이 서 있으면 시계가 하루 = 실제 600초(data/time TimeDefinition)로 흐른다
        /// — 실제 25초에 게임 60분. 활동은 걸리는 분만큼 시계를 건너뛰고(SIM-004 재현과 시간이 드는 활동), 메뉴가 열리면 멈춘다(PlayRunner 불변식).
        /// </summary>
        [Test]
        public void StandingStillInTheWorld_TheClockRunsAtTheDataDayLength()
        {
            PlayAtlas atlas = FastPlayTests.Atlas;
            using var body = new Rootborn.Simulation.Headless.HeadlessBody(atlas);
            var runner = new PlayRunner(body, PlayPolicyCatalog.Default().Create(PlayPolicyCatalog.MainChain, atlas), 1, new PlaySettings { MaxSteps = 3 }, atlas, "pace-clock");
            runner.Run();
            for (int i = 0; i < 5 && body.Screens.FocusScreen != null; i++) body.PressKey(Rootborn.Screens.ScreenKey.Escape);
            Assume.That(body.Screens.FocusScreen, Is.Null, "화면을 다 닫았다");
            var day = body.Session.TimeRules;
            int before = body.Session.StudentTimeMinutes;
            body.Wait(1500); // 실제 25초
            double expected = 25.0 * Rootborn.Core.Time.TimeRules.MinutesPerDay / day.RealSecondsPerGameDay;
            TestContext.WriteLine("하루 실제 " + day.RealSecondsPerGameDay + "초, 25초 동안 게임 " + (body.Session.StudentTimeMinutes - before) + "분(기대 " + expected + ")");
            Assert.That(body.Session.StudentTimeMinutes - before, Is.EqualTo(expected).Within(1.0));
        }

        [Test]
        public void DaysSetting_DefaultsToSeven_AndRejectsBadValuesByName()
        {
            Assert.That(PacePlay.ParseDays(null), Is.EqualTo(7));
            Assert.That(PacePlay.ParseDays(""), Is.EqualTo(7));
            Assert.That(PacePlay.ParseDays("3"), Is.EqualTo(3));
            var e = Assert.Throws<ArgumentException>(() => PacePlay.ParseDays("0"));
            Assert.That(e.Message, Does.Contain(PacePlay.DaysVariable).And.Contain("0"));
            Assert.Throws<ArgumentException>(() => PacePlay.ParseDays("x"));
        }

        /// <summary>
        /// 손으로만: 두 정책(메인 체인 우선, 학교 밖)으로 ① 메인 체인 끝까지(그 체인을 끝내거나 N일), ② N일을 돌려 보고서를 쓴다.
        /// N은 ROOTBORN_PACE_DAYS(기본 7). 실패해도 보고서는 남고, 알려진 결함이 아닌 실패면 마지막에 실패한다.
        /// </summary>
        [Test, Explicit("손으로만: 메인 체인 끝까지와 N일을 돌려 output/simulation/pace 에 보고서를 쓴다"), Category("PaceReport")]
        public void WritePaceReports()
        {
            int days = PacePlay.ParseDays(Environment.GetEnvironmentVariable(PacePlay.DaysVariable));
            var failures = new System.Collections.Generic.List<string>();
            foreach (string policy in new[] { PlayPolicyCatalog.MainChain, PlayPolicyCatalog.OffSchool })
            {
                var toChainEnd = new PlaySettings { Days = days, StepsPerDay = 5000, WindDownSteps = 120, MaxSteps = 5000 * days, StopWhenChainCompleted = FastPlayTests.OpeningChain };
                var nDays = new PlaySettings { Days = days, StepsPerDay = 5000, WindDownSteps = 120, MaxSteps = 5000 * days };
                foreach (var (name, settings) in new[] { ("pace-" + policy + "-main-chain", toChainEnd), ("pace-" + policy + "-" + days + "days", nDays) })
                {
                    PacePlayResult r = PacePlay.Run(policy, 1, settings, name);
                    TestContext.WriteLine(name + ": " + r.Info.End + " → " + r.OutputDir);
                    if (r.Play.Failure != null && KnownDefects.Matches(r.Play.Failure) == null) failures.Add(name + ": " + r.Play.Failure.What);
                }
            }
            Assert.That(failures, Is.Empty);
        }
    }
}
