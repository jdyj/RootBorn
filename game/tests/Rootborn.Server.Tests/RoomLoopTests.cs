using NUnit.Framework;
using Rootborn.Net.Room;
using Rootborn.Net.Transport;

namespace Rootborn.Server.Tests
{
    public sealed class RoomLoopTests
    {
        [Test]
        public void RunsFixedTicks_OnlyWhenTheClockHasPassedThem()
        {
            using var room = new RoomServer(new LoopbackNetwork().Listen(), new RoomServerSettings(), RoomRig.Versions, new MemoryStore(), "s", null);
            var clock = new ManualRoomClock();
            var loop = new RoomLoop(room, clock, 0.1);
            Assert.That(loop.RunDue(), Is.EqualTo(1), "시작 시각의 틱 하나");
            Assert.That(loop.RunDue(), Is.EqualTo(0), "시계가 안 갔으면 틱도 없다");
            clock.Now = 0.35;
            Assert.That(loop.RunDue(), Is.EqualTo(3));
            Assert.That(loop.Ticks, Is.EqualTo(4));
            Assert.That(loop.SecondsUntilNextTick, Is.EqualTo(0.05).Within(1e-9));
        }

        [Test]
        public void FarBehind_CatchesUpAtMostALimitThenSkips()
        {
            using var room = new RoomServer(new LoopbackNetwork().Listen(), new RoomServerSettings(), RoomRig.Versions, new MemoryStore(), "s", null);
            var clock = new ManualRoomClock();
            var loop = new RoomLoop(room, clock, 0.1);
            loop.RunDue();
            clock.Now = 100.0;
            Assert.That(loop.RunDue(), Is.EqualTo(RoomLoop.MaxCatchUpTicks));
            Assert.That(loop.RunDue(), Is.EqualTo(0), "밀린 것은 버리고 지금부터 다시 센다");
            clock.Now = 100.1 + 1e-6;
            Assert.That(loop.RunDue(), Is.EqualTo(1));
        }
    }
}
