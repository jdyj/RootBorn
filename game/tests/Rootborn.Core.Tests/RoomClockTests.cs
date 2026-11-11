using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Core.Network;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;
using Rootborn.Core.Time;

namespace Rootborn.Core.Tests
{
    /// <summary>
    /// 방의 시계(흐르는 시간 설계 3.1·3.3절, 6절 4단계): (날짜, 분)이 서버에서 흐르고 멈추지 않는다. 방에 아무도 없으면 멈춘다.
    /// 잠든 사람은 "자는 중"이다. 지금 있는 모두가 잠들면 곧바로 다음 날 하루 시작(07:00), 아니면 시계가 흘러 07:00이 되면 모두에게 다음 날.
    /// </summary>
    public sealed class RoomClockTests
    {
        private const int DayStart = 7 * 60;
        private static readonly TimeRules Rules = new TimeRules(600f, DayStart, 22 * 60, 1, 2);

        [Test]
        public void New_StartsDayOneAtTheDayStart()
        {
            var clock = new RoomClock(Rules);
            Assert.That((clock.Day, clock.Minutes), Is.EqualTo((1, DayStart)));
            Assert.That(clock.PresentCount, Is.Zero);
        }

        [Test]
        public void Nobody_TheClockStops()
        {
            var clock = new RoomClock(Rules);
            Assert.That(clock.Advance(60), Is.Zero);
            Assert.That(clock.Minutes, Is.EqualTo(DayStart));
            clock.Join("a");
            Assert.That(clock.Advance(60), Is.EqualTo(60));
            clock.Leave("a");
            Assert.That(clock.Advance(60), Is.Zero, "아무도 없으면 다시 멈춘다");
            Assert.That(clock.Minutes, Is.EqualTo(DayStart + 60));
        }

        [Test]
        public void TheClockFlows_AndDoesNotWaitForAnyone()
        {
            var clock = new RoomClock(Rules);
            clock.Join("a");
            clock.Join("b");
            clock.Advance(90);
            Assert.That(clock.Minutes, Is.EqualTo(DayStart + 90), "모두 같은 시각 하나");
        }

        [Test]
        public void AtTheDayEnd_EveryoneGetsTheNextDay_AtTheDayStart()
        {
            var clock = new RoomClock(Rules, 3, Rules.DayEndMinutes - 10);
            clock.Join("a");
            clock.Join("b");
            clock.Sleep("a");
            Assert.That(clock.Advance(10), Is.EqualTo(10));
            Assert.That((clock.Day, clock.Minutes), Is.EqualTo((4, DayStart)));
            Assert.That(clock.IsAsleep("a"), Is.False, "자던 사람은 일어난다");
            Assert.That(clock.IsAsleep("b"), Is.False);
        }

        [Test]
        public void Alone_Sleeping_GoesToTheNextDayAtOnce()
        {
            var clock = new RoomClock(Rules, 1, 23 * 60);
            clock.Join("a");
            Assert.That(clock.Sleep("a"), Is.True);
            Assert.That((clock.Day, clock.Minutes), Is.EqualTo((2, DayStart)));
        }

        [Test]
        public void Two_OnlyWhenBothSleep_TheNightIsSkipped()
        {
            var clock = new RoomClock(Rules, 1, 23 * 60);
            clock.Join("a");
            clock.Join("b");
            clock.Sleep("a");
            Assert.That((clock.Day, clock.Minutes), Is.EqualTo((1, 23 * 60)), "깨어 있는 사람이 있으면 그대로 흐른다");
            Assert.That(clock.IsAsleep("a"), Is.True);
            Assert.That(clock.Sleep("a"), Is.False, "이미 자는 중");
            clock.Advance(30);
            Assert.That(clock.Minutes, Is.EqualTo(23 * 60 + 30));
            clock.Sleep("b");
            Assert.That((clock.Day, clock.Minutes), Is.EqualTo((2, DayStart)));
        }

        [Test]
        public void SleepOn_OnlyCountsForThatDay()
        {
            var clock = new RoomClock(Rules, 5, 23 * 60);
            clock.Join("a");
            clock.Join("b");
            Assert.That(clock.SleepOn("a", 4), Is.False, "지난 날의 잠은 오늘을 재우지 않는다");
            Assert.That(clock.IsAsleep("a"), Is.False);
            Assert.That(clock.SleepOn("a", 5), Is.True);
        }

        [Test]
        public void Leaving_WhenEveryoneLeftIsAsleep_GoesToTheNextDay()
        {
            var clock = new RoomClock(Rules, 1, 23 * 60);
            clock.Join("a");
            clock.Join("b");
            clock.Sleep("a");
            clock.Leave("b");
            Assert.That((clock.Day, clock.Minutes), Is.EqualTo((2, DayStart)), "남은 모두가 자는 중");
        }

        [Test]
        public void AbsentPeople_AreNotWaitedFor_AndForgottenAtTheNextDay()
        {
            var clock = new RoomClock(Rules, 1, 23 * 60);
            clock.Join("a");
            clock.Join("b");
            clock.Leave("b");
            clock.Sleep("a");
            Assert.That(clock.Day, Is.EqualTo(2));
            var list = new List<RoomParticipantState>();
            clock.CopyPresent(list);
            Assert.That(list.ConvertAll(p => p.ParticipantId), Is.EqualTo(new[] { "a" }));
            Assert.That(clock.IsPresent("b"), Is.False);
        }

        [Test]
        public void Rejoining_TheSameDay_KeepsTheSleep()
        {
            var clock = new RoomClock(Rules, 1, 23 * 60);
            clock.Join("a");
            clock.Join("b");
            clock.Sleep("a");
            clock.Leave("a");
            Assert.That(clock.Join("a"), Is.True);
            Assert.That(clock.IsAsleep("a"), Is.True, "같은 날 다시 들어오면 자는 중 그대로");
        }

        [Test]
        public void JoiningMidday_StartsAtTheRoomsTime()
        {
            var clock = new RoomClock(Rules);
            clock.Join("a");
            clock.Advance(300);
            clock.Join("b");
            Assert.That(clock.Minutes, Is.EqualTo(DayStart + 300), "들어와도 시계는 그대로");
            Assert.That(clock.IsAsleep("b"), Is.False);
        }

        [Test]
        public void Participants_ListsWhoIsPresentAndWhoIsAsleep()
        {
            var clock = new RoomClock(Rules);
            clock.Join("a");
            clock.Join("b");
            clock.Sleep("b");
            var list = new List<RoomParticipantState>();
            clock.CopyPresent(list);
            Assert.That(list.Count, Is.EqualTo(2));
            Assert.That(list.Find(p => p.ParticipantId == "a").Asleep, Is.False);
            Assert.That(list.Find(p => p.ParticipantId == "b").Asleep, Is.True);
        }

        [Test]
        public void Advance_ManyDays_StopsAtEachDayStart()
        {
            var clock = new RoomClock(Rules);
            clock.Join("a");
            int moved = clock.Advance(TimeRules.MinutesPerDay * 3 + 5);
            Assert.That(clock.Day, Is.EqualTo(4));
            Assert.That(clock.Minutes, Is.EqualTo(DayStart + 5));
            Assert.That(moved, Is.EqualTo(TimeRules.MinutesPerDay * 3 + 5));
        }

        [Test]
        public void Save_RoundTrips_DayMinutesAndSleep_NobodyPresentAfterLoading()
        {
            var clock = new RoomClock(Rules, 4, 20 * 60);
            clock.Join("a");
            clock.Join("b");
            clock.Sleep("b");
            var data = clock.ToSaveData();
            Assert.That(data.Version, Is.EqualTo(RoomClockSaveData.KnownVersion));
            Assert.That(RoomClockSaveData.KnownVersion, Is.EqualTo(2));

            var again = RoomClock.FromSaveData(data, Rules);
            Assert.That((again.Day, again.Minutes), Is.EqualTo((4, 20 * 60)));
            Assert.That(again.PresentCount, Is.Zero);
            again.Join("b");
            Assert.That(again.IsAsleep("b"), Is.True);
            again.Join("a");
            Assert.That(again.IsAsleep("a"), Is.False);
        }

        [Test]
        public void OldSave_WithParts_IsReadAsThatDaysDayStart()
        {
            // 형식 1(때 단위): 날, 몇 번째 때, 참가자별 끝낸 때·마감. 그 날의 07:00으로 읽고 마감은 잠으로 보지 않는다(들어오면 다시 알린다).
            var old = new RoomClockSaveData
            {
                Version = 1, Day = 6, PartIndex = 2,
                Participants = new[] { new RoomClockParticipantSaveData { ParticipantId = "a", CompletedParts = 2, DayEnded = true } },
            };
            var clock = RoomClock.FromSaveData(old, Rules);
            Assert.That((clock.Day, clock.Minutes), Is.EqualTo((6, DayStart)));
            clock.Join("a");
            Assert.That(clock.IsAsleep("a"), Is.False);
        }

        [Test]
        public void FromSave_ClampsBrokenValues()
        {
            Assert.That(new RoomClockSaveData().Version, Is.EqualTo(SaveVersion.Unity));
            var clock = RoomClock.FromSaveData(new RoomClockSaveData { Version = 2, Day = -3, Minutes = 99999 }, Rules);
            Assert.That(clock.Day, Is.EqualTo(1));
            Assert.That(clock.Minutes, Is.EqualTo(Rules.DayEndMinutes - 1));
            var early = RoomClock.FromSaveData(new RoomClockSaveData { Version = 2, Day = 2, Minutes = 3 }, Rules);
            Assert.That(early.Minutes, Is.EqualTo(DayStart), "하루 시작보다 이르면 하루 시작");
            Assert.That(RoomClock.FromSaveData(null, Rules).Day, Is.EqualTo(1));
        }

        [Test]
        public void Save_ThroughTheStore_AndOldWorldTimeFile_BecomesTheClocksDay()
        {
            var store = new InMemorySaveStore();
            Assert.That(RoomClockSaveFiles.Load(store, "slot", Rules).Day, Is.EqualTo(1));
            store.Write(SaveFileNames.WorldTimeSlot, SaveFileNames.WorldTime("slot"), new WorldTimeSaveData { Version = 1, CurrentDay = 5, TimeOfDay = 2 });
            var fromOld = RoomClockSaveFiles.Load(store, "slot", Rules);
            Assert.That((fromOld.Day, fromOld.Minutes), Is.EqualTo((5, DayStart)));

            var clock = new RoomClock(Rules, 9, 12 * 60);
            RoomClockSaveFiles.Save(store, "slot", clock);
            var read = RoomClockSaveFiles.Load(store, "slot", Rules);
            Assert.That((read.Day, read.Minutes), Is.EqualTo((9, 12 * 60)));
        }

        [Test]
        public void FromProgress_IsTheSoloRoom_AtThePersonalDayAndTime()
        {
            var progress = new StudentLifeProgress("slot", "me", 10, 10, 0, 15 * 60);
            var clock = RoomClock.FromProgress(progress, "me", Rules);
            Assert.That((clock.Day, clock.Minutes), Is.EqualTo((progress.CurrentDay, 15 * 60)));
            Assert.That(clock.IsPresent("me"), Is.True);
        }

        [Test]
        public void FromProgress_AnEndedDay_IsAlreadyTheNextDayOfTheRoom()
        {
            var progress = StudentLifeProgress.FromSaveData(new StudentLifeProgressSaveData
            {
                SaveSlot = "s", PlayerId = "me", Energy = 10, Focus = 10, CurrentDay = 3, TimeMinutes = 23 * 60,
                DayState = StudentDayState.ResultReady.ToString(), LastSettledDay = 3,
            });
            var clock = RoomClock.FromProgress(progress, "me", Rules);
            Assert.That((clock.Day, clock.Minutes), Is.EqualTo((progress.CurrentDay + 1, DayStart)));
        }

        [Test]
        public void JumpClockTo_JoiningAtTheRoomsTime_MovesForwardOnly_WithoutChargingThePastNight()
        {
            var p = new StudentLifeProgress("slot", "me", 12, 10, 0, DayStart);
            p.ConfigureTimeRules(Rules);
            Assert.That(p.JumpClockTo(25 * 60), Is.EqualTo(25 * 60 - DayStart), "01:00의 방에 들어왔다");
            Assert.That(p.TimeMinutes, Is.EqualTo(25 * 60));
            Assert.That(p.Energy, Is.EqualTo(12), "방에 없던 밤 시간은 거슬러 깎지 않는다");
            Assert.That(p.JumpClockTo(24 * 60), Is.Zero, "뒤로는 가지 않는다");
            p.AdvanceClock(60);
            Assert.That(p.Energy, Is.EqualTo(11), "들어온 뒤로 깨어 있는 시간은 깎는다");
        }
    }
}
