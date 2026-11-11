using NUnit.Framework;
using Rootborn.Core.Network;
using Rootborn.Core.World;

namespace Rootborn.Core.Tests
{
    /// <summary>남의 몸 사이 메우기: 주인이 보낸 위치를 받아 늦게(지연만큼) 따라 그린다.</summary>
    public sealed class PoseBufferTests
    {
        private const double Delay = 0.1;

        private static PoseSample At(uint seq, double time, float x, float y = 0f, string map = "Town", Facing facing = Facing.Right, bool walking = true)
            => new PoseSample(seq, time, map, x, y, facing, walking);

        [Test]
        public void Empty_HasNoPose()
        {
            var buffer = new PoseBuffer(Delay);
            Assert.That(buffer.Sample(5.0).HasPose, Is.False);
            Assert.That(buffer.MapId, Is.Null);
        }

        [Test]
        public void SingleSample_IsShownAsIs()
        {
            var buffer = new PoseBuffer(Delay);
            Assert.That(buffer.Add(At(1, 10.0, 32f, 64f), localArrival: 100.0), Is.True);
            var view = buffer.Sample(100.0);
            Assert.That(view.HasPose, Is.True);
            Assert.That(view.MapId, Is.EqualTo("Town"));
            Assert.That(view.X, Is.EqualTo(32f));
            Assert.That(view.Y, Is.EqualTo(64f));
        }

        [Test]
        public void BetweenTwoSamples_InterpolatesByDelayedTime()
        {
            var buffer = new PoseBuffer(Delay);
            buffer.Add(At(1, 10.0, 0f), 100.0);
            buffer.Add(At(2, 10.2, 20f), 100.2);
            // 지금 100.2 → 보낸 쪽 시각으로 10.2, 지연 0.1을 빼면 10.1: 두 점의 가운데.
            var view = buffer.Sample(100.2);
            Assert.That(view.X, Is.EqualTo(10f).Within(0.001f));
            // 지연이 다 지나면 마지막 점에서 멈춘다(앞질러 짐작하지 않는다).
            Assert.That(buffer.Sample(105.0).X, Is.EqualTo(20f));
            // 첫 점보다 이른 시각은 첫 점.
            Assert.That(buffer.Sample(99.0).X, Is.EqualTo(0f));
        }

        [Test]
        public void LateOrDuplicateSample_IsDroppedAndDoesNotMoveBack()
        {
            var buffer = new PoseBuffer(Delay);
            buffer.Add(At(1, 10.0, 0f), 100.0);
            buffer.Add(At(3, 10.2, 20f), 100.2);
            Assert.That(buffer.Add(At(2, 10.1, -500f), 100.25), Is.False, "순서가 바뀌어 늦게 온 것");
            Assert.That(buffer.Add(At(3, 10.2, -500f), 100.25), Is.False, "같은 번호");
            Assert.That(buffer.Sample(105.0).X, Is.EqualTo(20f));
            Assert.That(buffer.Dropped, Is.EqualTo(2));
        }

        [Test]
        public void MapChange_SnapsToNewMapWithoutBlendingAcrossMaps()
        {
            var buffer = new PoseBuffer(Delay);
            buffer.Add(At(1, 10.0, 0f), 100.0);
            buffer.Add(At(2, 10.2, 20f), 100.2);
            Assert.That(buffer.Add(At(3, 10.3, 500f, 40f, map: "House"), 100.3), Is.True);
            var view = buffer.Sample(100.3);
            Assert.That(view.MapId, Is.EqualTo("House"));
            Assert.That(view.X, Is.EqualTo(500f));
            Assert.That(view.Y, Is.EqualTo(40f));
            Assert.That(buffer.MapId, Is.EqualTo("House"));
        }

        [Test]
        public void OlderSampleFromPreviousMap_DoesNotBringPlayerBack()
        {
            var buffer = new PoseBuffer(Delay);
            buffer.Add(At(5, 10.5, 500f, map: "House"), 100.5);
            Assert.That(buffer.Add(At(4, 10.4, 20f, map: "Town"), 100.6), Is.False);
            Assert.That(buffer.Sample(101.0).MapId, Is.EqualTo("House"));
        }

        [Test]
        public void FacingAndWalking_FollowTheSampleBeingApproached()
        {
            var buffer = new PoseBuffer(Delay);
            buffer.Add(At(1, 10.0, 0f, facing: Facing.Left, walking: true), 100.0);
            buffer.Add(At(2, 10.2, 0f, facing: Facing.Up, walking: false), 100.2);
            var view = buffer.Sample(100.2);
            Assert.That(view.Facing, Is.EqualTo(Facing.Up));
            Assert.That(view.Walking, Is.False);
        }

        [Test]
        public void Jitter_UsesSmallestSeenDelayAsClockOffset()
        {
            var buffer = new PoseBuffer(Delay);
            buffer.Add(At(1, 10.0, 0f), 100.05);  // 늦게 옴(0.05 지연)
            buffer.Add(At(2, 10.2, 20f), 100.2);  // 빨리 옴 → 시계 차이 90.0
            Assert.That(buffer.Sample(100.2).X, Is.EqualTo(10f).Within(0.001f));
        }

        [Test]
        public void Capacity_KeepsOnlyNewestSamples()
        {
            var buffer = new PoseBuffer(Delay, capacity: 3);
            for (uint i = 1; i <= 10; i++) buffer.Add(At(i, 10.0 + i * 0.1, i), 100.0 + i * 0.1);
            Assert.That(buffer.Count, Is.EqualTo(3));
            Assert.That(buffer.Sample(200.0).X, Is.EqualTo(10f));
        }

        [Test]
        public void Clear_ForgetsEverything()
        {
            var buffer = new PoseBuffer(Delay);
            buffer.Add(At(7, 10.0, 1f), 100.0);
            buffer.Clear();
            Assert.That(buffer.Sample(100.0).HasPose, Is.False);
            Assert.That(buffer.Add(At(1, 1.0, 2f), 101.0), Is.True, "다시 들어온 사람은 번호가 처음부터 다시 시작한다");
        }
    }
}
