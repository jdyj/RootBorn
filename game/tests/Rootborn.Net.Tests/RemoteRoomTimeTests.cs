using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Core.StudentLife;
using Rootborn.Core.Time;
using Rootborn.Net.Session;

namespace Rootborn.Net.Tests
{
    /// <summary>
    /// 클라이언트 쪽 방의 시계(<see cref="RemoteRoomTime"/>): 서버의 시계를 비추고, 쓴 시간 포인트·하루 마감을 서버에 보낸다.
    /// 개인 진행(Core StudentLifeProgress)을 Core RoomTimePointGate로 붙인다. 같은 프로세스 안 전송의 자동 테스트다(GameSession 없음).
    /// </summary>
    public sealed class RemoteRoomTimeTests
    {
        [Test]
        public void BeforeJoining_NothingCanBeSpent()
        {
            using var rig = new SessionRig();
            var a = rig.Join("a");
            var room = new RemoteRoomTime(a, "a", new LifeCalendar());
            Assert.That(room.Known, Is.False);
            Assert.That(room.CanSpend("a", 1), Is.False, "서버의 시계를 받기 전에는 허락이 없다");
            Assert.That(room.Spend("a", 1), Is.False);
            rig.Run(0.3);
            Assert.That(room.Known, Is.True);
            Assert.That((room.Day, room.PartIndex, room.PartsToday), Is.EqualTo((1, 0, 2)));
            Assert.That(room.CanSpend("a", 1), Is.True);
            Assert.That(room.CanSpend("b", 1), Is.False, "자기 신원만 쓴다");
        }

        [Test]
        public void SpendingMirrorsTheServer_WaitsForTheOther_AndBothSeeTheSameClock()
        {
            using var rig = new SessionRig();
            var (_, aRoom, aStudent) = Player(rig, "a");
            var (_, bRoom, bStudent) = Player(rig, "b");
            rig.Run(0.3);

            Assert.That(aStudent.TrySpendTimePoints(1), Is.True);
            Assert.That(aRoom.HasFinishedPart("a"), Is.True, "보낸 즉시 끝낸 것으로 본다");
            Assert.That(aStudent.CanSpendTimePoints(1), Is.False, "답이 오기 전에 또 쓰지 못한다");
            rig.Run(0.3);
            Assert.That(rig.Server.Clock.CompletedParts("a"), Is.EqualTo(1));
            Assert.That(rig.Server.Clock.PartIndex, Is.EqualTo(0), "b를 기다린다");
            Assert.That(aStudent.CanSpendTimePoints(1), Is.False);
            Assert.That(bRoom.HasFinishedPart("b"), Is.False);

            Assert.That(bStudent.TrySpendTimePoints(1), Is.True);
            rig.Run(0.3);
            Assert.That((aRoom.Day, aRoom.PartIndex), Is.EqualTo((bRoom.Day, bRoom.PartIndex)));
            Assert.That(aRoom.PartIndex, Is.EqualTo(1));
            Assert.That(aRoom.RepresentativeMinutes, Is.EqualTo(bRoom.RepresentativeMinutes));
            Assert.That(aRoom.RepresentativeMinutes, Is.EqualTo(new LifeCalendar().PartTimes.MinutesFor(1, 2)));
            Assert.That(aStudent.CanSpendTimePoints(1), Is.True);
            Assert.That(aRoom.CompletedParts("a"), Is.EqualTo(1));
        }

        [Test]
        public void ChangedIsRaised_WhenTheServersClockArrives()
        {
            using var rig = new SessionRig();
            var (_, aRoom, _) = Player(rig, "a");
            int changed = 0;
            aRoom.Changed += () => changed++;
            rig.Run(0.3);
            Assert.That(changed, Is.GreaterThan(0));
        }

        [Test]
        public void ALateSpend_ForAPartTheServerAlreadySkipped_IsCountedOnce_AndNothingIsUndone()
        {
            using var rig = new SessionRig(serverSettings: new SessionServerSettings { Clock = new RoomClockSettings(autoSkipAfterTicks: 10) });
            var (_, _, aStudent) = Player(rig, "a");
            var (b, bRoom, bStudent) = Player(rig, "b");
            rig.Run(0.3);

            rig.Freeze(b);
            Assert.That(aStudent.TrySpendTimePoints(1), Is.True);
            rig.Run(0.5);
            Assert.That(rig.Server.Clock.PartIndex, Is.EqualTo(1), "b를 자동으로 넘겼다");
            Assert.That(bRoom.PartIndex, Is.EqualTo(0), "b는 아직 모른다(옛 허락)");

            Assert.That(bStudent.TrySpendTimePoints(1), Is.True, "b는 옛 허락으로 지난 때에 썼다");
            rig.Thaw(b);
            rig.Run(0.3);
            Assert.That(rig.Server.Clock.CompletedParts("b"), Is.EqualTo(1), "넘겨진 때에 늦게 온 요청은 이미 센 것 — 두 번 세지 않는다");
            Assert.That(rig.Server.Clock.PartIndex, Is.EqualTo(1));
            Assert.That(bStudent.TimePointsSpentToday, Is.EqualTo(1), "개인 진행은 되돌리지 않는다");
            Assert.That(bRoom.PartIndex, Is.EqualTo(1));
            Assert.That(bRoom.HasFinishedPart("b"), Is.False, "새 때에서는 다시 쓸 수 있다");
            Assert.That(bStudent.CanSpendTimePoints(1), Is.True);
        }

        [Test]
        public void AutoSkipped_PersonalPointsCatchUp_ThroughTheGate()
        {
            using var rig = new SessionRig(serverSettings: new SessionServerSettings { Clock = new RoomClockSettings(autoSkipAfterTicks: 10) });
            var (_, _, aStudent) = Player(rig, "a");
            var (_, bRoom, bStudent, bGate) = PlayerWithGate(rig, "b");
            rig.Run(0.3);
            aStudent.TrySpendTimePoints(1);
            rig.Run(0.5);
            Assert.That(bRoom.PartIndex, Is.EqualTo(1));
            Assert.That(bRoom.CompletedParts("b"), Is.EqualTo(1));
            Assert.That(bStudent.TimePointsSpentToday, Is.EqualTo(0));
            Assert.That(bGate.CatchUp(bStudent), Is.EqualTo(1));
            Assert.That(bStudent.TimePointsSpentToday, Is.EqualTo(1));
            rig.Run(0.3);
            Assert.That(rig.Server.Clock.CompletedParts("b"), Is.EqualTo(1), "따라잡기는 서버에 보내지 않는다");
        }

        [Test]
        public void EndingTheDay_IsSentToTheServer_TheNextDayComesWhenBothEnd()
        {
            using var rig = new SessionRig();
            var (a, aRoom, _) = Player(rig, "a");
            var (_, bRoom, _) = Player(rig, "b");
            rig.Run(0.3);

            Assert.That(aRoom.EndDay("a"), Is.True);
            rig.Run(0.3);
            Assert.That(rig.Server.Clock.IsDayEnded("a"), Is.True);
            Assert.That(bRoom.Day, Is.EqualTo(1));
            Assert.That(aRoom.CanSpend("a", 1), Is.False, "마감한 날은 쓰지 못한다");
            Assert.That(aRoom.EndDay("a"), Is.False, "두 번 마감하지 않는다");

            Assert.That(bRoom.EndDay("b"), Is.True);
            rig.Run(0.3);
            Assert.That(aRoom.Day, Is.EqualTo(2));
            Assert.That(bRoom.Day, Is.EqualTo(2));
            Assert.That(aRoom.CanSpend("a", 1), Is.True);

            a.RequestDayEndReady(1);
            rig.Run(0.3);
            Assert.That(rig.Server.Clock.IsDayEnded("a"), Is.False, "지난 날의 마감 요청은 새 날을 마감하지 않는다");
        }

        [Test]
        public void Participants_ListWhoHasNotFinished_ByPlayerId()
        {
            using var rig = new SessionRig();
            var (_, _, aStudent) = Player(rig, "a");
            var (_, bRoom, _) = Player(rig, "b");
            rig.Run(0.3);
            aStudent.TrySpendTimePoints(1);
            rig.Run(0.3);

            var list = new List<RoomParticipantState>();
            bRoom.CopyPresent(list);
            Assert.That(list.Count, Is.EqualTo(2));
            Assert.That(list.Find(p => p.ParticipantId == "a").FinishedPart, Is.True);
            Assert.That(list.Find(p => p.ParticipantId == "b").FinishedPart, Is.False);
        }

        private static (SessionClient, RemoteRoomTime, StudentLifeProgress) Player(SessionRig rig, string id)
        {
            var (client, room, student, _) = PlayerWithGate(rig, id);
            return (client, room, student);
        }

        private static (SessionClient, RemoteRoomTime, StudentLifeProgress, RoomTimePointGate) PlayerWithGate(SessionRig rig, string id)
        {
            var client = rig.Join(id);
            var room = new RemoteRoomTime(client, id, new LifeCalendar());
            var student = new StudentLifeProgress("slot", id, 10, 10);
            student.ConfigureCalendar(new LifeCalendar());
            var gate = new RoomTimePointGate(room, id);
            student.ConfigureTimeGate(gate);
            return (client, room, student, gate);
        }
    }
}
