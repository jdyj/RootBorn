using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Screens;
using Rootborn.Simulation.Headless;

namespace Rootborn.Simulation.Tests
{
    /// <summary>
    /// 자동 테스트(창 없음): 대본 없이 정책이 화면에 보이는 것만 보고 골라 여러 날을 플레이한다. 입력은 화면 포인터 판정과 키 배정을 거친다.
    /// 한 걸음마다 불변식(예외·막힘·수치 범위·보상 두 번·화면 쌓임·빠진 문구 키·벽 뚫기·걸음 길이), 날이 바뀔 때마다 저장 뒤 다시 읽기를 본다.
    /// 씨앗 수와 날 수는 ROOTBORN_SIM_SEEDS·ROOTBORN_SIM_DAYS로 늘린다(기본: 씨앗 2개 × 2일, CI 1분 안).
    /// 기록은 output/simulation/fast-&lt;정책&gt;-&lt;씨앗&gt;/(play.txt, summary.md, 실패하면 failure.md).
    /// 사람의 실제 플레이를 대신하지 않는다.
    /// </summary>
    [TestFixture, NonParallelizable]
    public sealed class FastPlayTests
    {
        public static PlayAtlas Atlas => PlayAtlas.Load(PlayAtlas.FindRepoRoot());

        public static PlaySettings Settings() => new PlaySettings
        {
            Days = PlaySettings.EnvInt("ROOTBORN_SIM_DAYS", 2),
            StepsPerDay = PlaySettings.EnvInt("ROOTBORN_SIM_STEPS_PER_DAY", 80),
        };

        public static IEnumerable<TestCaseData> Plays()
        {
            int seeds = PlaySettings.EnvInt("ROOTBORN_SIM_SEEDS", 2);
            foreach (string policy in PlayPolicyCatalog.Default().Names)
                for (int seed = 1; seed <= seeds; seed++)
                    yield return new TestCaseData(policy, seed).SetName("Plays_" + policy + "_seed" + seed);
        }

        /// <summary>한 판을 돌리고 기록을 남긴다. 실패해도 기록은 남는다.</summary>
        public static PlayResult Play(string policy, int seed, PlaySettings settings, string name)
        {
            PlayAtlas atlas = Atlas;
            using var body = new HeadlessBody(atlas);
            PlayResult result = new PlayRunner(body, PlayPolicyCatalog.Default().Create(policy, atlas), seed, settings, atlas, name).Run();
            PlayRecord.Write(result, atlas.RepoRoot, settings, ScreenCatalog.Create().Screens.Select(s => s.Id), atlas);
            TestContext.WriteLine(PlayRecord.Summary(result, ScreenCatalog.Create().Screens.Select(s => s.Id), atlas));
            return result;
        }

        [TestCaseSource(nameof(Plays))]
        public void PolicyPlaysSeveralDays_KeepingEveryInvariant(string policy, int seed)
        {
            PlaySettings settings = Settings();
            PlayResult result = Play(policy, seed, settings, "fast-" + policy + "-" + seed);
            KnownDefects.AssertNoNewFailure(result);
            if (KnownDefects.Matches(result.Failure) != null) return;
            Assert.That(result.DaysCompleted, Is.GreaterThanOrEqualTo(settings.Days), "날을 넘기며 플레이했다");
            Assert.That(result.Reloads, Is.GreaterThanOrEqualTo(settings.Days), "날마다 다시 읽기 검사");
        }

        /// <summary>메인 체인(실제 데이터의 첫 이야기). 정책은 이 id를 모른다 — 테스트만 끝났는지 본다.</summary>
        public const string OpeningChain = "main-story.school-opening";

        /// <summary>
        /// 메인 체인 우선 정책이 대본 없이(추적 목표와 지도만 보고) 메인 체인을 끝까지 간다. 못 가면 실패 메시지의 체인 위치와 기록이 막힌 곳이다.
        /// </summary>
        [Test]
        public void MainChainPolicy_FinishesTheOpeningChain_WithoutAScript()
        {
            var settings = new PlaySettings { Days = 10, StepsPerDay = 120, MaxSteps = 3000, StopWhenChainCompleted = OpeningChain };
            PlayResult result = Play(PlayPolicyCatalog.MainChain, 1, settings, "fast-main-chain-to-end");
            KnownDefects.AssertNoNewFailure(result);
            if (KnownDefects.Matches(result.Failure) != null) return;
            Assert.That(result.ChainsCompleted, Is.GreaterThanOrEqualTo(1), "체인 위치: " + result.LastChainStep + ", 기록: " + result.OutputDir);
            Assert.That(result.FinalState, Has.Some.StartsWith("chain " + OpeningChain + " Completed"), "메인 체인을 끝냈다. 위치: " + result.LastChainStep);
        }
    }
}
