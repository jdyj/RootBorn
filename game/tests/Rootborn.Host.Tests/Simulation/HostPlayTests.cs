using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Screens;
using Rootborn.Simulation;

namespace Rootborn.Host.Tests.Simulation
{
    /// <summary>
    /// 장치 이벤트 주입(OS 입력 계층 미확인): 창 없는 층과 같은 정책·관찰·불변식으로 실제 창과 게임 루프에서 플레이한다. 정책마다 씨앗 하나,
    /// 기본 2일(ROOTBORN_SIM_HOST_DAYS·ROOTBORN_SIM_HOST_SEEDS로 늘린다). 화면이 처음 맨 위에 뜰 때 한 장씩 찍는다
    /// (output/host-sim-evidence/&lt;정책&gt;-&lt;씨앗&gt;-&lt;걸음&gt;-&lt;화면&gt;.png). 기록은 output/simulation/host-&lt;정책&gt;-&lt;씨앗&gt;/.
    /// 사람의 실제 플레이를 대신하지 않는다.
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class HostPlayTests
    {
        private const int ShotsPerPlay = 8;

        private static string Root => HostOptions.Resolve(new[] { "--sample" }).RepoRoot;

        public static IEnumerable<TestCaseData> Plays()
        {
            int seeds = PlaySettings.EnvInt("ROOTBORN_SIM_HOST_SEEDS", 1);
            foreach (string policy in PlayPolicyCatalog.Default().Names)
                for (int seed = 1; seed <= seeds; seed++)
                    yield return new TestCaseData(policy, seed).SetName("HostPlays_" + policy + "_seed" + seed);
        }

        /// <summary>밤샘 정책은 날이 넘어갈 때까지 서서 기다리므로 학생 시계를 빨리 돌린다(--time-scale — 하루 60초).</summary>
        private const float StayUpTimeScale = 10f;

        private static float TimeScaleFor(string policy) => policy == PlayPolicyCatalog.StayUp ? StayUpTimeScale : 1f;

        private static PlayResult Play(string policy, int seed, PlaySettings settings, string name, int shots, float timeScale = 1f)
        {
            PlayAtlas atlas = PlayAtlas.Load(Root);
            string evidence = Path.Combine(Root, "output", "host-sim-evidence");
            using var body = new DriverBody(Root, evidence, timeScale);
            var runner = new PlayRunner(body, PlayPolicyCatalog.Default().Create(policy, atlas), seed, settings, atlas, name);
            var shot = new HashSet<string>(StringComparer.Ordinal);
            runner.AfterStep += (step, action) =>
            {
                string top = body.Screens.FocusScreen?.Id ?? (body.InWorld ? "world-" + body.Walk.Map.Id : null);
                if (top == null || shot.Count >= shots || !shot.Add(top)) return;
                body.Player.MovePointer(-50, -50);
                body.Player.Frames(2);
                body.Screenshot(name + "-" + step.ToString("000") + "-" + top);
            };
            PlayResult result = runner.Run();
            if (result.Failure != null) body.Screenshot(name + "-failure");
            PlayRecord.Write(result, Root, settings, ScreenCatalog.Create().Screens.Select(s => s.Id), atlas);
            TestContext.WriteLine(PlayRecord.Summary(result, ScreenCatalog.Create().Screens.Select(s => s.Id), atlas));
            TestContext.WriteLine("스크린샷: " + evidence + " (" + string.Join(", ", shot) + ")");
            Assert.That(body.Player.Game.Renderer.MissingImages, Is.Empty, "못 찾은 그림");
            return result;
        }

        [TestCaseSource(nameof(Plays))]
        public void PolicyPlaysInTheRealWindow_KeepingEveryInvariant(string policy, int seed)
        {
            var settings = new PlaySettings
            {
                Days = PlaySettings.EnvInt("ROOTBORN_SIM_HOST_DAYS", 2),
                StepsPerDay = PlaySettings.EnvInt("ROOTBORN_SIM_HOST_STEPS_PER_DAY", 30),
            };
            PlayResult result = Play(policy, seed, settings, "host-" + policy + "-" + seed, ShotsPerPlay, TimeScaleFor(policy));
            if (result.Failure != null) Assert.Fail(result.Failure.What + "\n기록: " + result.OutputDir + "\n" + result.Failure.Report);
            Assert.That(result.DaysCompleted, Is.GreaterThanOrEqualTo(settings.Days), "날을 넘기며 플레이했다");
        }

        /// <summary>
        /// 밤샘이 이득이 아니다(흐르는 시간 설계 3.4)를 실제 창에서: 같은 씨앗·같은 배속으로 밤샘 정책과 학교 밖 정책(같은 선호, 잔다)을 이틀 돌려
        /// 특성·기술 합을 비교한다. 배속은 둘 다 같다(하루 60초).
        /// </summary>
        [Test]
        public void StayingUp_IsNotBetterThanSleeping_InTheRealWindow()
        {
            var settings = new PlaySettings { Days = 2, StepsPerDay = 40 };
            PlayResult up = Play(PlayPolicyCatalog.StayUp, 1, settings, "host-stay-up-vs-sleep-up", 4, StayUpTimeScale);
            PlayResult slept = Play(PlayPolicyCatalog.OffSchool, 1, settings, "host-stay-up-vs-sleep-slept", 4, StayUpTimeScale);
            if (up.Failure != null) Assert.Fail(up.Failure.What + "\n" + up.Failure.Report);
            if (slept.Failure != null) Assert.Fail(slept.Failure.What + "\n" + slept.Failure.Report);
            int grownUp = Growth(up), grownSlept = Growth(slept);
            TestContext.WriteLine("성장 합(특성+기술): 밤샘 " + grownUp + ", 잠 " + grownSlept);
            Assert.That(up.Notes.Any(n => n.Contains("자지 않고 넘어왔다")), Is.True, "밤샘 쪽은 자지 않고 날을 넘겼다");
            Assert.That(grownUp, Is.LessThanOrEqualTo(grownSlept), "밤샘이 자는 것보다 이득이면 안 된다");
        }

        private static int Growth(PlayResult result) => result.FinalState
            .Where(l => l.StartsWith("trait ", StringComparison.Ordinal) || l.StartsWith("skill ", StringComparison.Ordinal))
            .Sum(l => int.Parse(l.Substring(l.LastIndexOf('=') + 1)));

        /// <summary>
        /// 메인 체인 우선 정책으로 실제 창에서 메인 체인을 끝까지(대본 없음, 40초쯤). 창 없는 층의
        /// MainChainPolicy_FinishesTheOpeningChain_WithoutAScript와 같은 판단이다.
        /// </summary>
        [Test]
        public void MainChainPolicy_FinishesTheOpeningChainInTheRealWindow()
        {
            var settings = new PlaySettings { Days = 10, StepsPerDay = 120, MaxSteps = 3000, StopWhenChainCompleted = "main-story.school-opening" };
            PlayResult result = Play(PlayPolicyCatalog.MainChain, 1, settings, "host-main-chain-to-end", 20);
            if (result.Failure != null) Assert.Fail(result.Failure.What + "\n기록: " + result.OutputDir + "\n" + result.Failure.Report);
            Assert.That(result.ChainsCompleted, Is.GreaterThanOrEqualTo(1), "체인 위치: " + result.LastChainStep + ", 기록: " + result.OutputDir);
        }
    }
}