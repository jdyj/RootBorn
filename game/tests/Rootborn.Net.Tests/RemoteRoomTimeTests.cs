using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Core.Time;
using Rootborn.Net.Session;

namespace Rootborn.Net.Tests
{
    /// <summary>
    /// 클라이언트 쪽 방의 시계(<see cref="RemoteRoomTime"/>, 흐르는 시간 4단계): 서버의 시계를 비추고(받은 시각 사이를 같은 속도로 메운다),
    /// 잠을 서버에 보낸다. 같은 프로세스 안 전송의 자동 테스트다(GameSession 없음 — 그것은 Server.Tests SharedClockGameTests).
    /// </summary>
    public sealed class RemoteRoomTimeTests
    {
        // 하루 240초: 게임 1분 = 실제 1/6초.
        private static readonly TimeRules Rules = new TimeRules(240f, 7 * 60, 22 * 60, 1, 2);

        [Test]
        public void BeforeJoining_TheTimeIsUnknown_AndNothingIsSent()
        {
            using var rig = new SessionRig(timeRules: Rules);
            var a = rig.Join("a");
            var room = new RemoteRoomTime(a, "a", Rules);
            Assert.That(room.Known, Is.False);
            Assert.That(room.SleepOn("a", 1), Is.False, "서버의 시계를 받기 전에는 보내지 않는다");
            rig.Run(0.3);
            Assert.That(room.Known, Is.True);
            Assert.That(room.Day, Is.EqualTo(1));
            Assert.That(room.SleepOn("b", 1), Is.False, "자기 신원만");
        }

        [Test]
        public void BetweenSyncs_TheTimeFlowsAtTheSameSpeed_AndMatchesTheServer()
        {
            using var rig = new SessionRig(timeRules: Rules, serverSettings: new SessionServerSettings { ClockSyncSeconds = 60 });
            var a = rig.Join("a");
            var room = new RemoteRoomTime(a, "a", Rules);
            rig.Run(0.5);
            int received = 0;
            a.RoomTimeChanged += () => received++;
            rig.Run(5); // 게임 30분, 맞춤 간격(60초)·게임 1시간 안
            Assert.That(received, Is.Zero, "그 사이 서버는 보내지 않았다");
            Assert.That(room.Minutes, Is.EqualTo(rig.Server.Clock.Minutes).Within(1), "받은 시각 + 흐른 시간으로 메운다");
        }

        [Test]
        public void TheEstimate_DoesNotPassTheDayEnd_TheServerAnnouncesTheNextDay()
        {
            var store = new MemorySaveStore();
            store.Write("slot-0", Rootborn.Core.Save.SaveFileNames.RoomClock, new RoomClock(Rules, 1, Rules.DayEndMinutes - 3).ToSaveData());
            using var rig = new SessionRig(store: store, saveSlot: "slot-0", timeRules: Rules, serverSettings: new SessionServerSettings { ClockSyncSeconds = 60 });
            var a = rig.Join("a");
            var room = new RemoteRoomTime(a, "a", Rules);
            rig.Run(0.2);
            a.Update(rig.Now + 5); // 서버가 아직 다음 시각을 보내지 않은 동안 클라이언트 시간만 흐른다(게임 30분, 끊김 시간 초과 안)
            Assert.That(room.Day, Is.EqualTo(1));
            Assert.That(room.Minutes, Is.EqualTo(Rules.DayEndMinutes - 1), "같은 날 안에서는 날 넘김 시각 앞까지만");
        }

        [Test]
        public void Sleeping_IsSent_AndShownAsAsleepUntilTheServerCountsIt()
        {
            using var rig = new SessionRig(timeRules: Rules);
            var a = rig.Join("a");
            var b = rig.Join("b");
            var aRoom = new RemoteRoomTime(a, "a", Rules);
            var bRoom = new RemoteRoomTime(b, "b", Rules);
            rig.Run(0.3);
            Assert.That(aRoom.SleepOn("a", 2), Is.False, "방의 날이 아니면 보내지 않는다");
            Assert.That(aRoom.SleepOn("a", 1), Is.True);
            Assert.That(aRoom.IsAsleep("a"), Is.True, "보낸 잠은 곧바로 자는 중으로 보인다(되돌리지 않는다)");
            Assert.That(aRoom.HasPendingSleep, Is.True);
            Assert.That(aRoom.SleepOn("a", 1), Is.False, "두 번 보내지 않는다");
            rig.Run(0.3);
            Assert.That(aRoom.HasPendingSleep, Is.False, "서버가 셌다");
            Assert.That(rig.Server.Clock.IsAsleep("a"), Is.True);

            var list = new List<RoomParticipantState>();
            bRoom.CopyPresent(list);
            Assert.That(list.Find(p => p.ParticipantId == "a").Asleep, Is.True);
            Assert.That(list.Find(p => p.ParticipantId == "b").Asleep, Is.False);

            bRoom.SleepOn("b", 1);
            rig.Run(0.3);
            Assert.That(aRoom.Day, Is.EqualTo(2), "모두 자면 다음 날");
            Assert.That(aRoom.IsAsleep("a"), Is.False);
        }

        [Test]
        public void ChangedIsRaised_WhenTheServersClockArrives()
        {
            using var rig = new SessionRig(timeRules: Rules);
            var a = rig.Join("a");
            var room = new RemoteRoomTime(a, "a", Rules);
            int changed = 0;
            room.Changed += () => changed++;
            rig.Run(0.3);
            Assert.That(changed, Is.GreaterThan(0));
            room.Dispose();
            int after = changed;
            rig.Join("b");
            rig.Run(0.3);
            Assert.That(changed, Is.EqualTo(after), "놓은 뒤에는 알리지 않는다");
        }
    }
}
