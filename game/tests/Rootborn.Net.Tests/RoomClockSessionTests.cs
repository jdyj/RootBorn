using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Core.Save;
using Rootborn.Core.Time;
using Rootborn.Net.Session;

namespace Rootborn.Net.Tests
{
    /// <summary>
    /// 서버가 방의 시계(Core RoomClock, 흐르는 시간 4단계)를 갖고 흘리며 모두에게 그 상태를 보낸다. 같은 프로세스 안 전송,
    /// 서버 하나 + 클라이언트 둘의 자동 테스트다. 게임 판(GameSession)은 붙이지 않았다(그것은 Server.Tests SharedClockGameTests).
    /// </summary>
    public sealed class RoomClockSessionTests
    {
        // 하루 240초: 게임 1분 = 실제 1/6초, 실제 1초 = 게임 6분.
        private static readonly TimeRules Rules = new TimeRules(240f, 7 * 60, 22 * 60, 1, 2);

        [Test]
        public void TheServersClock_Flows_AndEveryoneGetsTheSameDayAndMinutes()
        {
            using var rig = new SessionRig(timeRules: Rules);
            var a = rig.Join("a");
            var b = rig.Join("b");
            rig.Run(0.3);
            Assert.That(a.RoomTime.Known && b.RoomTime.Known, Is.True);
            Assert.That(a.RoomTime.Day, Is.EqualTo(1));

            rig.Run(10);
            int server = rig.Server.Clock.Minutes;
            Assert.That(server - 7 * 60, Is.InRange(59, 62), "실제 10초 = 게임 60분, 멈추지 않는다");
            Assert.That(a.RoomTime.Minutes, Is.EqualTo(b.RoomTime.Minutes), "모두 같은 시각을 받는다");
            Assert.That(server - a.RoomTime.Minutes, Is.InRange(0, 31), "가끔(5초마다) 맞춘다 — 매 프레임 보내지 않는다");
        }

        [Test]
        public void TheClock_IsNotSentEveryFrame()
        {
            using var rig = new SessionRig(timeRules: Rules);
            var a = rig.Join("a");
            rig.Run(0.5);
            int received = 0;
            a.RoomTimeChanged += () => received++;
            rig.Run(20);
            Assert.That(received, Is.InRange(3, 8), "20초에 맞춤 4번 + 게임 1시간마다 한 번(2번) 안팎");
        }

        [Test]
        public void Sleeping_IsShownToEveryone_AndWhenEveryoneSleeps_TheNextDayStartsAt0700()
        {
            using var rig = new SessionRig(timeRules: Rules);
            var a = rig.Join("a");
            var b = rig.Join("b");
            rig.Run(0.3);
            a.RequestSleep(1);
            rig.Run(0.3);
            Assert.That(b.RoomTime.IsAsleep(a.NetId), Is.True);
            Assert.That(b.RoomTime.AsleepCount, Is.EqualTo(1));
            Assert.That(rig.Server.Clock.Day, Is.EqualTo(1), "깨어 있는 사람이 있으면 그대로");

            b.RequestSleep(1);
            rig.Run(0.3);
            Assert.That(rig.Server.Clock.Day, Is.EqualTo(2));
            Assert.That(a.RoomTime.Day, Is.EqualTo(2));
            Assert.That(a.RoomTime.Minutes, Is.EqualTo(7 * 60).Within(2));
            Assert.That(a.RoomTime.AsleepCount, Is.Zero, "다음 날에는 모두 깨어 있다");
        }

        [Test]
        public void ASleepForAnotherDay_IsNotCounted()
        {
            using var rig = new SessionRig(timeRules: Rules);
            var a = rig.Join("a");
            rig.Run(0.3);
            a.RequestSleep(5);
            rig.Run(0.3);
            Assert.That(rig.Server.Clock.Day, Is.EqualTo(1), "지난·다른 날의 잠은 받지 않는다");
        }

        [Test]
        public void Leaving_WhenEveryoneLeftIsAsleep_GoesToTheNextDay()
        {
            using var rig = new SessionRig(timeRules: Rules);
            var a = rig.Join("a");
            var b = rig.Join("b");
            rig.Run(0.3);
            a.RequestSleep(1);
            rig.Run(0.3);
            rig.Quit(b);
            rig.Run(0.5);
            Assert.That(rig.Server.Clock.Day, Is.EqualTo(2), "나간 사람은 기다리지 않는다");
            Assert.That(a.RoomTime.Day, Is.EqualTo(2));
        }

        [Test]
        public void ReconnectingWithTheSameIdentity_IsNotLeaving()
        {
            using var rig = new SessionRig(timeRules: Rules);
            var a = rig.Join("a");
            var b = rig.Join("b");
            rig.Run(0.3);
            a.RequestSleep(1);
            rig.Run(0.3);
            var b2 = rig.Join("b"); // 같은 신원이 새 연결로
            rig.Run(0.3);
            Assert.That(rig.Server.Clock.Day, Is.EqualTo(1), "연결만 바뀐 것은 나간 것이 아니다");
            Assert.That(b2.RoomTime.IsAsleep(a.NetId), Is.True);
        }

        [Test]
        public void Nobody_TheServersClockStops()
        {
            using var rig = new SessionRig(timeRules: Rules);
            rig.Run(10);
            Assert.That(rig.Server.Clock.Minutes, Is.EqualTo(7 * 60));
            var a = rig.Join("a");
            rig.Run(5);
            rig.Quit(a);
            rig.Run(0.5);
            int left = rig.Server.Clock.Minutes;
            rig.Run(10);
            Assert.That(rig.Server.Clock.Minutes, Is.EqualTo(left), "다 나가면 다시 멈춘다(데디케이티드 서버)");
        }

        [Test]
        public void TheClock_IsSavedEachGameHour_AndReadBack()
        {
            var store = new MemorySaveStore();
            using (var rig = new SessionRig(store: store, saveSlot: "slot-0", timeRules: Rules))
            {
                rig.Join("a");
                rig.Run(11); // 1시간 넘게
            }

            Assert.That(store.TryRead("slot-0", SaveFileNames.RoomClock, out RoomClockSaveData saved), Is.True);
            Assert.That(saved.Version, Is.EqualTo(RoomClockSaveData.KnownVersion));
            Assert.That(saved.Minutes, Is.GreaterThanOrEqualTo(8 * 60), "게임 1시간이 바뀔 때 썼다");
            using var again = new SessionRig(store: store, saveSlot: "slot-0", timeRules: Rules);
            Assert.That(again.Server.Clock.Minutes, Is.EqualTo(saved.Minutes));
        }

        [Test]
        public void ABigPauseOfTheServer_DoesNotJumpTheClock()
        {
            using var rig = new SessionRig(timeRules: Rules);
            rig.Join("a");
            rig.Run(0.3);
            int before = rig.Server.Clock.Minutes;
            rig.Server.Update(rig.Now + 3600); // 서버가 한 시간 멈췄다 돌아옴
            Assert.That(rig.Server.Clock.Minutes - before, Is.LessThanOrEqualTo(7), "한 번에 넘기는 초는 상한(1초 = 게임 6분)까지");
        }

        [Test]
        public void OldWorldTimeFile_BecomesTheRoomsDay()
        {
            var store = new MemorySaveStore();
            store.Write(SaveFileNames.WorldTimeSlot, SaveFileNames.WorldTime("slot-0"), new WorldTimeSaveData { Version = 1, CurrentDay = 5, TimeOfDay = 1 });
            using var rig = new SessionRig(store: store, saveSlot: "slot-0", timeRules: Rules);
            var a = rig.Join("a");
            rig.Run(0.3);
            Assert.That(rig.Server.Clock.Day, Is.EqualTo(5));
            Assert.That(a.RoomTime.Day, Is.EqualTo(5));
        }
    }
}
