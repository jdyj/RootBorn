// 옮겨 옴: Assets/Scripts/Game/World/TownAutoDoorPortal.cs (_openingDuration: 실내 문 0.35초, Town.unity 문 0.8초),
// TownLevelTransitionTrigger.cs (경계 0.25초)
using NUnit.Framework;
using Rootborn.Core.World;

namespace Rootborn.Core.Tests
{
    public sealed class DoorOpenSecondsTests
    {
        private static readonly AreaRect Body = new AreaRect(0, 0, 32, 32);

        private static int FramesUntilOpen(DoorArea door)
        {
            var watch = new DoorWatch(new[] { door });
            watch.Reset(new AreaRect(200, 200, 32, 32));
            for (int frame = 0; frame < 600; frame++)
                if (watch.Update(Body, 1f / 60f) != null) return frame;
            return -1;
        }

        [Test]
        public void DOOR_TIME_001_DoorWithItsOwnTimeOpensAfterThatTime()
        {
            var target = new TravelTarget("Town", "x");
            int interior = FramesUntilOpen(new DoorArea("a", new AreaRect(0, 0, 32, 32), target));
            int town = FramesUntilOpen(new DoorArea("a", new AreaRect(0, 0, 32, 32), target, 0.8f));
            int edge = FramesUntilOpen(new DoorArea("a", new AreaRect(0, 0, 32, 32), target, 0.25f));
            Assert.That(interior, Is.EqualTo(21), "기본 0.35초: 들어선 프레임 + 21프레임");
            Assert.That(town, Is.EqualTo(48), "0.8초");
            Assert.That(edge, Is.EqualTo(15), "0.25초");
        }
    }
}
