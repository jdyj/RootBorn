// 옮겨 옴: Assets/Scripts/Game/Player/PlayerInteractionRouter.cs (반경 1.75 안의 후보, 중심 대 중심 거리, InteractionTargetRule로 고르기)
using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Core.Interaction;

namespace Rootborn.Core.Tests
{
    public sealed class InteractionTargetsTests
    {
        private const int Cell = 32;

        private static InteractionCandidate Npc(string id, float x, float y) => new InteractionCandidate("npc", id, x, y, 30, true);

        [Test]
        public void INTERACT_001_OnlyCandidatesWithinTheUnityRadiusCanBeTargeted()
        {
            var targets = new InteractionTargets(InteractionTargets.UnityRadiusCells * Cell);
            targets.SetCandidates(new[] { Npc("npc.a", 100f, 100f) });
            Assert.That(targets.Update(100f + 56f, 100f), Is.Not.Null, "1.75칸 = 56px 안");
            Assert.That(targets.Update(100f + 57f, 100f), Is.Null, "밖");
            Assert.That(targets.Target, Is.Null);
        }

        [Test]
        public void INTERACT_002_ClearlyCloserWinsAndPrioritySettlesNearTies()
        {
            var targets = new InteractionTargets(56f);
            var board = new InteractionCandidate("daily-plan-board", "board", 120f, 100f, 80, true);
            var npc = Npc("npc.a", 104f, 100f);
            targets.SetCandidates(new[] { board, npc });
            Assert.That(targets.Update(100f, 100f).Id, Is.EqualTo("npc.a"), "16px 더 가까우면 우선순위가 낮아도 이긴다");
            var near = new InteractionCandidate("daily-plan-board", "board", 100.3f, 100f, 80, true);
            targets.SetCandidates(new[] { npc, near });
            Assert.That(targets.Update(102f, 100f).Id, Is.EqualTo("board"), "0.5px 안의 차이는 같은 거리 → 우선순위");
        }

        [Test]
        public void INTERACT_003_PressingReportsWhatWasInteractedWithOnce()
        {
            var targets = new InteractionTargets(56f) { MapId = "House" };
            targets.SetCandidates(new[] { Npc("npc.first-guide", 50f, 50f) });
            var events = new List<InteractionEvent>();
            targets.Interacted += e => events.Add(e);
            Assert.That(targets.Press(), Is.Null, "대상이 없으면 아무 일도 없다");
            targets.Update(60f, 50f);
            InteractionEvent? fired = targets.Press();
            Assert.That(fired, Is.Not.Null);
            Assert.That(events.Count, Is.EqualTo(1));
            Assert.That(events[0].Kind, Is.EqualTo("npc"));
            Assert.That(events[0].TargetId, Is.EqualTo("npc.first-guide"));
            Assert.That(events[0].MapId, Is.EqualTo("House"));
        }

        [Test]
        public void INTERACT_004_AutoDoorWinsTheTargetButPressingDoesNothing()
        {
            // Unity TownAutoDoorPortal: 우선순위 110, 빈 안내, E는 false. 문 앞에서는 다른 것을 고를 수 없다.
            var targets = new InteractionTargets(56f);
            var door = new InteractionCandidate("door", "town", 100f, 100f, 110, false);
            targets.SetCandidates(new[] { door, Npc("npc.a", 100.2f, 100f) });
            Assert.That(targets.Update(100f, 120f).Kind, Is.EqualTo("door"));
            Assert.That(targets.Target.Acts, Is.False);
            Assert.That(targets.Press(), Is.Null);
        }

        [Test]
        public void INTERACT_005_ReplacingCandidatesDropsAStaleTarget()
        {
            var targets = new InteractionTargets(56f);
            targets.SetCandidates(new[] { Npc("npc.a", 0f, 0f) });
            targets.Update(0f, 0f);
            targets.SetCandidates(new InteractionCandidate[0]);
            Assert.That(targets.Target, Is.Null);
            Assert.That(targets.Press(), Is.Null);
        }
    }
}
