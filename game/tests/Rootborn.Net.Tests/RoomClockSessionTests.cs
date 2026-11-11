using NUnit.Framework;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;
using Rootborn.Core.Time;
using Rootborn.Net.Session;

namespace Rootborn.Net.Tests
{
    /// <summary>
    /// 서버가 방의 시계(Core RoomClock)를 갖고 모두에게 그 상태를 보낸다(멀티플레이 설계 2.1절). 같은 프로세스 안 전송,
    /// 서버 하나 + 클라이언트 둘의 자동 테스트다. 게임 판(GameSession)은 붙이지 않았다.
    /// </summary>
    public sealed class RoomClockSessionTests
    {
        [Test]
        public void BothFinishing_MovesToTheNextPart_ForEveryone()
        {
            using var rig = new SessionRig();
            var a = rig.Join("a");
            var b = rig.Join("b");
            rig.Run(0.3);
            Assert.That(a.RoomClock.PartsToday, Is.EqualTo(2), "기본 달력의 1일차(평일)");

            a.RequestFinishPart(1);
            b.RequestFinishPart(1);
            rig.Run(0.3);
            Assert.That(rig.Server.Clock.PartIndex, Is.EqualTo(1));
            Assert.That(a.RoomClock.PartIndex, Is.EqualTo(1));
            Assert.That(b.RoomClock.PartIndex, Is.EqualTo(1));
            Assert.That(a.RoomClock.FinishedCount, Is.EqualTo(0), "새 때에서는 아무도 안 끝냈다");
        }

        [Test]
        public void OnlyOneFinishing_DoesNotMoveOn_AndOthersSeeWhoFinished()
        {
            using var rig = new SessionRig();
            var a = rig.Join("a");
            var b = rig.Join("b");
            rig.Run(0.3);

            a.RequestFinishPart(1);
            rig.Run(0.3);
            Assert.That(rig.Server.Clock.PartIndex, Is.EqualTo(0));
            Assert.That(b.RoomClock.PartIndex, Is.EqualTo(0));
            Assert.That(b.RoomClock.HasFinished(a.NetId), Is.True);
            Assert.That(b.RoomClock.HasFinished(b.NetId), Is.False);
            Assert.That(b.RoomClock.FinishedCount, Is.EqualTo(1));

            a.RequestFinishPart(1);
            rig.Run(0.3);
            Assert.That(rig.Server.Clock.CompletedParts("a"), Is.EqualTo(1), "기다리는 동안 다시 끝내도 하나로 센다");
            Assert.That(rig.Server.Clock.PartIndex, Is.EqualTo(0));
        }

        [Test]
        public void Leaving_MovesOnWithoutTheOneWhoLeft()
        {
            using var rig = new SessionRig();
            var a = rig.Join("a");
            var b = rig.Join("b");
            rig.Run(0.3);
            a.RequestFinishPart(1);
            rig.Run(0.3);
            Assert.That(a.RoomClock.PartIndex, Is.EqualTo(0));

            rig.Quit(b);
            rig.Run(0.3);
            Assert.That(rig.Server.Clock.PartIndex, Is.EqualTo(1), "나간 사람은 기다리지 않는다");
            Assert.That(a.RoomClock.PartIndex, Is.EqualTo(1));
            Assert.That(a.RoomClock.PlayerCount, Is.EqualTo(1));
        }

        [Test]
        public void ReconnectingWithTheSameIdentity_IsNotLeaving()
        {
            using var rig = new SessionRig();
            var a = rig.Join("a");
            rig.Join("b");
            rig.Run(0.3);
            a.RequestFinishPart(1);
            rig.Run(0.3);

            var bAgain = rig.Join("b");
            rig.Run(0.3);
            Assert.That(bAgain.Status, Is.EqualTo(ClientStatus.Joined));
            Assert.That(rig.Server.Clock.PartIndex, Is.EqualTo(0), "옛 연결을 바꾸는 동안 a 혼자인 방으로 넘어가지 않는다");
            bAgain.RequestSkipPart();
            rig.Run(0.3);
            Assert.That(rig.Server.Clock.PartIndex, Is.EqualTo(1));
        }

        [Test]
        public void AutoSkip_IsARoomSetting_CountedInServerUpdates()
        {
            var settings = new SessionServerSettings { MaxPlayers = 4, Clock = new RoomClockSettings(autoSkipAfterTicks: 30) };
            using var rig = new SessionRig(serverSettings: settings);
            var a = rig.Join("a");
            var b = rig.Join("b");
            rig.Run(0.3);
            a.RequestFinishPart(1);
            rig.Run(0.2);
            Assert.That(rig.Server.Clock.PartIndex, Is.EqualTo(0), "30번의 서버 Update가 지나기 전");
            rig.Run(0.5);
            Assert.That(rig.Server.Clock.PartIndex, Is.EqualTo(1), "안 끝낸 b를 넘긴 것으로 본다");
            Assert.That(b.RoomClock.PartIndex, Is.EqualTo(1));
        }

        [Test]
        public void AutoSkip_IsOffByDefault()
        {
            using var rig = new SessionRig();
            var a = rig.Join("a");
            rig.Join("b");
            rig.Run(0.3);
            a.RequestFinishPart(1);
            rig.Run(5.0);
            Assert.That(rig.Server.Clock.PartIndex, Is.EqualTo(0));
        }

        [Test]
        public void TheServersCalendar_DecidesThePartsOfTheDay()
        {
            var store = new MemorySaveStore();
            using var rig = new SessionRig(store: store, saveSlot: "slot-0", calendar: new LifeCalendar(4, 5, 96));
            var a = rig.Join("a");
            rig.Run(0.3);
            Assert.That(a.RoomClock.PartsToday, Is.EqualTo(4));
            a.RequestFinishPart(3);
            rig.Run(0.3);
            Assert.That(a.RoomClock.PartIndex, Is.EqualTo(3), "혼자면 바로 넘어간다");
        }

        [Test]
        public void OldWorldTimeFile_BecomesTheRoomsDay()
        {
            var store = new MemorySaveStore();
            store.Write(SaveFileNames.WorldTimeSlot, SaveFileNames.WorldTime("slot-0"), new WorldTimeSaveData { Version = 1, CurrentDay = 5, TimeOfDay = 1 });
            using var rig = new SessionRig(store: store, saveSlot: "slot-0");
            var a = rig.Join("a");
            rig.Run(0.3);
            Assert.That(rig.Server.Clock.Day, Is.EqualTo(5));
            Assert.That(a.RoomClock.Day, Is.EqualTo(5));
        }
    }
}
