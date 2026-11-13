using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Simulation.Pace;

namespace Rootborn.Simulation.Tests
{
    /// <summary>자동 테스트: 속도 기록의 시간 계산(걸음 목록을 손으로 만든다 — 게임을 돌리지 않는다).</summary>
    public sealed class PaceTimeTests
    {
        private static PaceStep Step(string category, long frames, int chars = 0, int choices = 0, int challenge = -1, double limit = 0)
            => new PaceStep { Category = category, Frames = frames, NewChars = chars, Choices = choices, Challenge = challenge, MinigameLimitSeconds = limit, Day = 1, Key = "k" };

        [Test]
        public void GameSeconds_AreFramesOverTheFixedFrameRate()
        {
            PaceStep s = Step(PaceCategories.Walk, 90);
            Assert.That(s.GameSeconds(1.0 / 60.0), Is.EqualTo(1.5).Within(1e-9));
        }

        [Test]
        public void HumanSeconds_ReadingAndChoosing_FollowTheAxisValues()
        {
            PaceStep s = Step(PaceCategories.ReadChoose, 0, chars: 400, choices: 3);
            var normal = new PaceAxis(new PaceReading("보통", 400), new PaceChoice("보통", 2), 0.5);
            var slow = new PaceAxis(new PaceReading("느림", 200), new PaceChoice("오래", 5), 0.5);
            var skip = new PaceAxis(new PaceReading("넘김", 0), new PaceChoice("짧게", 1), 0.5);
            Assert.That(normal.ReadingSeconds(s) + normal.ChoiceSeconds(s), Is.EqualTo(60 + 6).Within(1e-9));
            Assert.That(slow.ReadingSeconds(s) + slow.ChoiceSeconds(s), Is.EqualTo(120 + 15).Within(1e-9));
            Assert.That(skip.ReadingSeconds(s), Is.EqualTo(0), "넘기는 사람은 읽는 데 시간을 쓰지 않는다");
        }

        [Test]
        public void RealSeconds_AddGameTimeAndHumanTime_PerStep()
        {
            var steps = new List<PaceStep> { Step(PaceCategories.Walk, 120), Step(PaceCategories.ReadChoose, 6, chars: 100, choices: 1) };
            var axis = new PaceAxis(new PaceReading("보통", 400), new PaceChoice("보통", 2), 0.5);
            double[] real = PaceTime.RealSeconds(steps, axis, 1.0 / 60.0);
            Assert.That(real[0], Is.EqualTo(2).Within(1e-9));
            Assert.That(real[1], Is.EqualTo(0.1 + 15 + 2).Within(1e-9));
        }

        /// <summary>미니게임은 도전 하나마다 자동 풀이가 쓴 시간과 제한 시간의 몫 중 큰 쪽(모자란 만큼 그 도전의 마지막 걸음에 더한다).</summary>
        [Test]
        public void Minigame_TakesTheLargerOfSolverTimeAndTheShareOfTheLimit_PerChallenge()
        {
            var steps = new List<PaceStep>
            {
                Step(PaceCategories.Minigame, 60, challenge: 0, limit: 30),
                Step(PaceCategories.Minigame, 60, challenge: 0),
                Step(PaceCategories.Minigame, 600, challenge: 1, limit: 10),
            };
            double fps = 1.0 / 60.0;
            double[] half = PaceTime.RealSeconds(steps, new PaceAxis(new PaceReading("보통", 400), new PaceChoice("보통", 2), 0.5), fps);
            Assert.That(half[0] + half[1], Is.EqualTo(15).Within(1e-9), "풀이 2초 < 30초의 절반 15초");
            Assert.That(half[1], Is.EqualTo(1 + 13).Within(1e-9), "모자란 13초는 도전의 마지막 걸음에");
            Assert.That(half[2], Is.EqualTo(10).Within(1e-9), "풀이 10초 > 10초의 절반 5초 — 풀이 시간 그대로");
            double[] full = PaceTime.RealSeconds(steps, new PaceAxis(new PaceReading("보통", 400), new PaceChoice("보통", 2), 1.0), fps);
            Assert.That(full[0] + full[1] + full[2], Is.EqualTo(30 + 10).Within(1e-9));
        }

        [Test]
        public void Axes_HaveTheDesignBaseline_AndEveryCombination()
        {
            PaceAxis b = PaceAssumptions.Baseline;
            Assert.That(b.Reading.CharsPerMinute, Is.EqualTo(400));
            Assert.That(b.Choice.Seconds, Is.EqualTo(2));
            Assert.That(b.MinigameShare, Is.EqualTo(0.5));
            Assert.That(PaceAssumptions.All(), Has.Count.EqualTo(PaceAssumptions.Reading.Count * PaceAssumptions.Choice.Count * PaceAssumptions.MinigameShares.Count));
            Assert.That(PaceAssumptions.MinigameShares, Is.EqualTo(new[] { 0.25, 0.5, 1.0 }));
        }
    }
}
