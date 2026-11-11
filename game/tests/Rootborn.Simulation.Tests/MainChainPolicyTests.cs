using System;
using NUnit.Framework;

namespace Rootborn.Simulation.Tests
{
    /// <summary>자동 테스트: 메인 체인 우선 정책의 고르기(관찰을 손으로 만든다 — 판단 코드만 본다).</summary>
    public sealed class MainChainPolicyTests
    {
        private static PlayView TwoWorkSpots()
        {
            var view = new PlayView { InWorld = true, HasTracked = true, TrackedStep = "step.work", MapId = "map" };
            view.TrackedTargets.Add("work.a");
            view.TrackedTargets.Add("work.b");
            view.Actions.Add(new PlayAction { Kind = PlayActionKind.WalkInteract, Key = "walk:part-time-work/work.a", Category = "interaction", InteractionKind = "part-time-work", Subject = "work.a" });
            view.Actions.Add(new PlayAction { Kind = PlayActionKind.WalkInteract, Key = "walk:part-time-work/work.b", Category = "interaction", InteractionKind = "part-time-work", Subject = "work.b" });
            return view;
        }

        /// <summary>같은 종류의 목표 지점이 여럿이면 그 단계에서 아직 안 가 본 곳을 먼저 고른다(어느 씨앗이든).</summary>
        [Test]
        public void AmongSeveralTargetSpots_PicksOneNotYetTriedInThisStep()
        {
            for (int seed = 0; seed < 32; seed++)
            {
                var memory = new PlayMemory();
                PlayView view = TwoWorkSpots();
                memory.Note(view, view.Find("walk:part-time-work/work.a"));
                PlayAction chosen = new MainChainPolicy(null).Choose(view, memory, new Random(seed));
                Assert.That(chosen?.Key, Is.EqualTo("walk:part-time-work/work.b"), "씨앗 " + seed);
            }
        }

        /// <summary>단계가 바뀌면 다시 처음부터 센다(새 단계에서는 전에 간 곳도 안 가 본 곳이다).</summary>
        [Test]
        public void ANewStep_ForgetsWhichSpotsWereTried()
        {
            var memory = new PlayMemory();
            PlayView old = TwoWorkSpots();
            memory.Note(old, old.Find("walk:part-time-work/work.a"));
            PlayView next = TwoWorkSpots();
            next.TrackedStep = "step.next";
            memory.Note(next, next.Find("walk:part-time-work/work.b"));
            Assert.That(memory.TriedThisStep("walk:part-time-work/work.a"), Is.EqualTo(0));
            Assert.That(memory.TriedThisStep("walk:part-time-work/work.b"), Is.EqualTo(1));
        }
    }
}
