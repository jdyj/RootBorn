using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Screens;
using Rootborn.Simulation.Headless;

namespace Rootborn.Simulation.Tests
{
    /// <summary>
    /// 자동 테스트: 같은 씨앗은 같은 플레이가 된다(행동 이름과 끝 상태가 같다), 남긴 기록을 그대로 다시 재생하면 같은 상태가 된다.
    /// ROOTBORN_SIM_REPLAY에 play.txt 경로를 주고 ReplayFromEnvironment를 돌리면 실패한 판을 다시 돌려 볼 수 있다.
    /// </summary>
    [TestFixture, NonParallelizable]
    public sealed class ReplayAndDeterminismTests
    {
        private static PlaySettings Short() => new PlaySettings { Days = 2, StepsPerDay = 40, MaxSteps = 400 };

        private static PlayResult Run(string policy, int seed, string name)
        {
            PlayAtlas atlas = FastPlayTests.Atlas;
            using var body = new HeadlessBody(atlas);
            PlayResult result = new PlayRunner(body, PlayPolicyCatalog.Default().Create(policy, atlas), seed, Short(), atlas, name).Run();
            PlayRecord.Write(result, atlas.RepoRoot, Short(), ScreenCatalog.Create().Screens.Select(s => s.Id), atlas);
            return result;
        }

        [TestCase(PlayPolicyCatalog.Random, 7)]
        [TestCase(PlayPolicyCatalog.MainChain, 3)]
        public void SameSeed_PlaysTheSameGame(string policy, int seed)
        {
            PlayResult a = Run(policy, seed, "determinism-" + policy + "-a");
            PlayResult b = Run(policy, seed, "determinism-" + policy + "-b");
            Assert.That(a.Failure, Is.Null, a.Failure?.Message);
            Assert.That(a.Steps.Count, Is.GreaterThan(20), "판다운 판");
            Assert.That(b.Keys.ToArray(), Is.EqualTo(a.Keys.ToArray()), "같은 씨앗 → 같은 행동");
            Assert.That(b.FinalState, Is.EqualTo(a.FinalState), "같은 씨앗 → 같은 끝 상태");
            Assert.That(b.Frames, Is.EqualTo(a.Frames), "같은 프레임 수");
        }

        [Test]
        public void DifferentSeeds_PlayDifferently()
        {
            PlayResult a = Run(PlayPolicyCatalog.Random, 1, "determinism-seed-1");
            PlayResult b = Run(PlayPolicyCatalog.Random, 2, "determinism-seed-2");
            Assert.That(b.Keys.ToArray(), Is.Not.EqualTo(a.Keys.ToArray()), "씨앗이 정책의 고르기를 바꾼다");
        }

        [Test]
        public void RecordedPlay_ReplaysToTheSameState()
        {
            PlayResult original = Run(PlayPolicyCatalog.Novelty, 5, "replay-source");
            Assert.That(original.Failure, Is.Null, original.Failure?.Message);
            string playFile = Path.Combine(original.OutputDir, "play.txt");

            PlayAtlas atlas = FastPlayTests.Atlas;
            using var body = new HeadlessBody(atlas);
            PlayResult replayed = PlayReplay.Run(body, playFile, atlas, "replay-copy");
            Assert.That(replayed.Failure, Is.Null, replayed.Failure?.Message);
            Assert.That(replayed.Keys.ToArray(), Is.EqualTo(original.Keys.ToArray()), "기록의 행동을 그대로 다시 했다");
            Assert.That(replayed.FinalState, Is.EqualTo(original.FinalState), "다시 한 판의 끝 상태가 같다");
        }

        [Test]
        public void ReplayingAnActionThatIsNotThere_ReportsWhereItDiverged()
        {
            PlayAtlas atlas = FastPlayTests.Atlas;
            string dir = Path.Combine(Path.GetTempPath(), "rootborn-sim-replay-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string file = Path.Combine(dir, "play.txt");
                File.WriteAllText(file, "# policy random\n# seed 1\n0\tstart\tbutton:save-slot-select/save-slots.card.0.new\n1\trandom\tbutton:nowhere/no-such-button\n");
                using var body = new HeadlessBody(atlas);
                PlayResult r = PlayReplay.Run(body, file, atlas, "replay-diverged");
                Assert.That(r.Failure, Is.Not.Null);
                Assert.That(r.Failure.What, Does.Contain("재생이 어긋났다"));
                Assert.That(r.Failure.Report, Does.Contain("1번째 행동 'button:nowhere/no-such-button'"));
            }
            finally { Directory.Delete(dir, true); }
        }

        /// <summary>실패한 판 다시 보기: ROOTBORN_SIM_REPLAY=output/simulation/&lt;이름&gt;/play.txt. 평소에는 돌지 않는다.</summary>
        [Test, Explicit("ROOTBORN_SIM_REPLAY에 기록 경로를 주고 손으로 돌린다")]
        public void ReplayFromEnvironment()
        {
            string file = Environment.GetEnvironmentVariable("ROOTBORN_SIM_REPLAY");
            Assert.That(file, Is.Not.Null.And.Not.Empty, "ROOTBORN_SIM_REPLAY에 play.txt 경로를 준다");
            PlayAtlas atlas = FastPlayTests.Atlas;
            using var body = new HeadlessBody(atlas);
            PlayResult r = PlayReplay.Run(body, file, atlas, "replay-" + Path.GetFileName(Path.GetDirectoryName(Path.GetFullPath(file))));
            PlayRecord.Write(r, atlas.RepoRoot, new PlaySettings(), ScreenCatalog.Create().Screens.Select(s => s.Id), atlas);
            TestContext.WriteLine(PlayRecord.Summary(r, ScreenCatalog.Create().Screens.Select(s => s.Id), atlas));
            if (r.Failure != null) Assert.Fail(r.Failure.Message);
        }
    }
}
