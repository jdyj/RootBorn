using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Simulation.Pace;

namespace Rootborn.Simulation.Tests
{
    /// <summary>자동 테스트: 지루함 재기(새로운 것 사이 간격, 되풀이, 빈 날, 할 것 없음의 몫, 지점) — 손으로 만든 걸음 목록으로.</summary>
    public sealed class PaceAnalysisTests
    {
        private const double Fps = 1.0 / 60.0;
        private static readonly PaceAxis Axis = PaceAssumptions.Baseline;

        private static PaceStep S(int day, string category, double seconds, string key, params PaceNovelty[] novel)
        {
            var s = new PaceStep { Day = day, Minutes = 7 * 60, Category = category, Frames = (long)(seconds * 60), Key = key };
            s.Novelties.AddRange(novel);
            return s;
        }

        private static PaceNovelty N(string what, bool milestone = false) => new PaceNovelty("종류", what, milestone);

        private static List<PaceStep> Indexed(params PaceStep[] steps)
        {
            for (int i = 0; i < steps.Length; i++) steps[i].Index = i;
            return steps.ToList();
        }

        [Test]
        public void Gaps_AreTheTimeBetweenNovelties_WithWhatWasDoneMeanwhile()
        {
            List<PaceStep> steps = Indexed(
                S(1, PaceCategories.Walk, 1, "w", N("a")),
                S(1, PaceCategories.Walk, 60, "walk:x"),
                S(1, PaceCategories.Walk, 60, "walk:x"),
                S(1, PaceCategories.ReadChoose, 5, "button:y", N("b")),
                S(1, PaceCategories.NothingToDo, 10, "idle:600", N("c")),
                S(1, PaceCategories.NothingToDo, 30, "idle:600"));
            var a = new PaceAnalysis(steps, Fps, Axis);
            List<PaceGap> gaps = a.LongestGaps(10);
            Assert.That(gaps[0].Seconds, Is.EqualTo(125).Within(1e-6), "a 뒤부터 b까지");
            Assert.That(gaps[0].FromStep, Is.EqualTo(0));
            Assert.That(gaps[0].ToStep, Is.EqualTo(3));
            Assert.That(gaps[0].ByCategory[PaceCategories.Walk], Is.EqualTo(120).Within(1e-6));
            Assert.That(gaps[0].TopKeys[0], Is.EqualTo(("walk:x", 2)));
            Assert.That(gaps[1].Seconds, Is.EqualTo(30).Within(1e-6), "마지막 새것 뒤 끝까지도 간격(열린 간격)");
            Assert.That(gaps[1].Open, Is.True);
            Assert.That(gaps.Select(g => g.Seconds), Is.Ordered.Descending);
        }

        [Test]
        public void Repetition_CountsRunsOfTheSameAction_AndTheMostRepeatedPerDay()
        {
            List<PaceStep> steps = Indexed(
                S(1, PaceCategories.Walk, 1, "a"), S(1, PaceCategories.Walk, 1, "a"), S(1, PaceCategories.Walk, 1, "a"),
                S(1, PaceCategories.Walk, 1, "b"), S(1, PaceCategories.Walk, 1, "a"), S(2, PaceCategories.Walk, 1, "b"));
            var a = new PaceAnalysis(steps, Fps, Axis);
            PaceRun run = a.LongestRuns(5)[0];
            Assert.That((run.Key, run.Count, run.FromStep), Is.EqualTo(("a", 3, 0)));
            var perDay = a.MostRepeatedPerDay();
            Assert.That(perDay[1], Is.EqualTo(("a", 4)));
            Assert.That(perDay[2], Is.EqualTo(("b", 1)));
        }

        [Test]
        public void EmptyDays_AndNothingToDoShare_ArePerDay()
        {
            List<PaceStep> steps = Indexed(
                S(1, PaceCategories.Walk, 30, "a", N("x")), S(1, PaceCategories.NothingToDo, 10, "idle"),
                S(2, PaceCategories.Walk, 40, "a"),
                S(3, PaceCategories.Walk, 20, "a", N("y")));
            var a = new PaceAnalysis(steps, Fps, Axis);
            Assert.That(a.EmptyDays(), Is.EqualTo(new[] { 2 }));
            Assert.That(a.NothingToDoShare(1), Is.EqualTo(0.25).Within(1e-9));
            Assert.That(a.NothingToDoShare(2), Is.EqualTo(0));
            Assert.That(a.ByDay[1][PaceCategories.Walk], Is.EqualTo(30).Within(1e-6));
            Assert.That(a.ByCategory[PaceCategories.Walk], Is.EqualTo(90).Within(1e-6));
            Assert.That(a.TotalSeconds, Is.EqualTo(100).Within(1e-6));
        }

        [Test]
        public void Milestones_CarryTheDayAndTheCumulativeTimeAtEveryAxis()
        {
            List<PaceStep> steps = Indexed(
                S(1, PaceCategories.Walk, 30, "a"),
                S(1, PaceCategories.ReadChoose, 0, "b"),
                S(2, PaceCategories.Walk, 20, "c", N("chain-end", milestone: true), N("not-a-milestone")));
            steps[1].NewChars = 400; // 보통이면 60초, 넘기면 0초
            var a = new PaceAnalysis(steps, Fps, Axis);
            PaceMilestone m = a.Milestones().Single();
            Assert.That(m.What, Is.EqualTo("chain-end"));
            Assert.That(m.Day, Is.EqualTo(2));
            Assert.That(m.Seconds, Is.EqualTo(30 + 60 + 20).Within(1e-6));
            var fast = new PaceAnalysis(steps, Fps, PaceAssumptions.Fastest);
            Assert.That(fast.Milestones().Single().Seconds, Is.EqualTo(50).Within(1e-6));
        }
    }
}
