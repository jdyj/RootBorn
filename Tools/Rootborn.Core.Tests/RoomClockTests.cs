using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;
using Rootborn.Core.Time;

namespace Rootborn.Core.Tests
{
    /// <summary>
    /// 방의 시계(멀티플레이 설계 2.1절): (날짜, 몇 번째 때)와 참가자별 "이 때를 끝냈는지". 모두 끝내면 다음 때, 마지막 때가 끝나면
    /// 모두 하루 마감 뒤 다음 날. 혼자 하기는 참가자 하나인 방이다. 하루의 때 수는 달력(평일 2, 주말 3, 1일차가 평일)에서 온다.
    /// </summary>
    public sealed class RoomClockTests
    {
        private static RoomClock Room(params string[] present)
        {
            var clock = new RoomClock(new LifeCalendar());
            foreach (var id in present) clock.Join(id);
            return clock;
        }

        [Test]
        public void New_StartsDayOneFirstPart_PartsFromTheCalendar()
        {
            var clock = new RoomClock(new LifeCalendar(4, 6, 96));
            Assert.That(clock.Day, Is.EqualTo(1));
            Assert.That(clock.PartIndex, Is.EqualTo(0));
            Assert.That(clock.PartsToday, Is.EqualTo(4), "1일차는 평일이라 달력의 평일 때 수");
            Assert.That(clock.IsDayOver, Is.False);
            Assert.That(clock.PresentCount, Is.EqualTo(0));
        }

        [Test]
        public void Alone_SpendingAPart_MovesToTheNextPartAtOnce()
        {
            var clock = Room("a");
            Assert.That(clock.CanSpend("a", 1), Is.True);
            Assert.That(clock.Spend("a", 1), Is.True);
            Assert.That(clock.PartIndex, Is.EqualTo(1), "혼자면 내가 끝내는 순간 모두가 끝낸 것");
            Assert.That(clock.HasFinishedPart("a"), Is.False, "새 때에서는 아직 안 끝냈다");
            Assert.That(clock.CanSpend("a", 1), Is.True);
        }

        [Test]
        public void Two_OnlyWhenBothFinish_ThePartAdvances()
        {
            var clock = Room("a", "b");
            Assert.That(clock.Spend("a", 1), Is.True);
            Assert.That(clock.PartIndex, Is.EqualTo(0), "한 명만 끝내면 그대로");
            Assert.That(clock.HasFinishedPart("a"), Is.True);
            Assert.That(clock.HasFinishedPart("b"), Is.False);
            Assert.That(clock.CanSpend("a", 1), Is.False, "끝낸 사람은 다음 때가 올 때까지 시간 포인트를 쓰지 못한다");
            Assert.That(clock.Spend("a", 1), Is.False);

            Assert.That(clock.Skip("b"), Is.True, "넘기기도 끝낸 것");
            Assert.That(clock.PartIndex, Is.EqualTo(1));
            Assert.That(clock.HasFinishedPart("a"), Is.False);
            Assert.That(clock.CanSpend("a", 1), Is.True);
        }

        [Test]
        public void ZeroPointActions_AreAlwaysAllowed_AndDoNotFinishThePart()
        {
            var clock = Room("a", "b");
            clock.Spend("a", 1);
            Assert.That(clock.CanSpend("a", 0), Is.True, "시간 포인트를 안 쓰는 일은 때 안에서 얼마든지");
            Assert.That(clock.Spend("b", 0), Is.True);
            Assert.That(clock.HasFinishedPart("b"), Is.False);
            Assert.That(clock.PartIndex, Is.EqualTo(0));
        }

        [Test]
        public void SpendingMorePointsThanThePartsLeft_IsRefused_WithoutChange()
        {
            var clock = Room("a");
            Assert.That(clock.CanSpend("a", 3), Is.False, "평일은 때가 둘");
            Assert.That(clock.Spend("a", 3), Is.False);
            Assert.That(clock.PartIndex, Is.EqualTo(0));
            Assert.That(clock.Spend("a", 2), Is.True, "두 포인트를 쓰는 일은 두 때를 끝낸다");
            Assert.That(clock.IsDayOver, Is.True);
        }

        [Test]
        public void AfterTheLastPart_TheDayIsOver_ThenEveryoneEndsTheDay_ThenTheNextDay()
        {
            var clock = Room("a", "b");
            clock.Spend("a", 1); clock.Spend("b", 1);
            clock.Spend("a", 1); clock.Spend("b", 1);
            Assert.That(clock.IsDayOver, Is.True);
            Assert.That(clock.PartIndex, Is.EqualTo(2));
            Assert.That(clock.Day, Is.EqualTo(1), "마지막 때가 끝나도 하루 마감 전에는 같은 날");
            Assert.That(clock.CanSpend("a", 1), Is.False);

            Assert.That(clock.EndDay("a"), Is.True);
            Assert.That(clock.Day, Is.EqualTo(1));
            Assert.That(clock.IsDayEnded("a"), Is.True);
            Assert.That(clock.EndDay("a"), Is.False, "두 번 마감해도 둘이 되지 않는다");

            Assert.That(clock.EndDay("b"), Is.True);
            Assert.That(clock.Day, Is.EqualTo(2));
            Assert.That(clock.PartIndex, Is.EqualTo(0));
            Assert.That(clock.IsDayEnded("a"), Is.False, "다음 날에는 비워진다");
            Assert.That(clock.CompletedParts("a"), Is.EqualTo(0));
        }

        [Test]
        public void EndingTheDayEarly_CountsAsFinishingEveryPartLeft()
        {
            var clock = Room("a", "b");
            Assert.That(clock.EndDay("a"), Is.True, "때가 남아도 먼저 자러 갈 수 있다");
            Assert.That(clock.HasFinishedPart("a"), Is.True);
            Assert.That(clock.CanSpend("a", 1), Is.False);
            Assert.That(clock.PartIndex, Is.EqualTo(0));

            clock.Spend("b", 1);
            Assert.That(clock.PartIndex, Is.EqualTo(1), "먼저 마감한 사람은 남은 때를 모두 끝낸 것으로 센다");
            clock.Spend("b", 1);
            Assert.That(clock.IsDayOver, Is.True);
            Assert.That(clock.Day, Is.EqualTo(1));
            clock.EndDay("b");
            Assert.That(clock.Day, Is.EqualTo(2));
        }

        [Test]
        public void Alone_EndingTheDay_GoesToTheNextDayAtOnce()
        {
            var clock = Room("a");
            clock.Spend("a", 1);
            Assert.That(clock.EndDay("a"), Is.True);
            Assert.That(clock.Day, Is.EqualTo(2));
            Assert.That(clock.PartIndex, Is.EqualTo(0));
        }

        [Test]
        public void Leaving_DoesNotKeepOthersWaiting()
        {
            var clock = Room("a", "b");
            clock.Spend("a", 1);
            Assert.That(clock.PartIndex, Is.EqualTo(0));
            Assert.That(clock.Leave("b"), Is.True);
            Assert.That(clock.PartIndex, Is.EqualTo(1), "나간 사람은 기다리지 않는다");
            Assert.That(clock.PresentCount, Is.EqualTo(1));

            clock.EndDay("a");
            Assert.That(clock.Day, Is.EqualTo(2), "남은 사람만 마감하면 다음 날");
        }

        [Test]
        public void JoiningMidway_StartsAtTheCurrentPart_AndIsWaitedFor()
        {
            var clock = Room("a");
            clock.Spend("a", 1);
            Assert.That(clock.PartIndex, Is.EqualTo(1));
            Assert.That(clock.Join("b"), Is.True);
            Assert.That(clock.CompletedParts("b"), Is.EqualTo(1), "도중에 들어오면 지금 때부터");
            Assert.That(clock.HasFinishedPart("b"), Is.False);

            clock.Spend("a", 1);
            Assert.That(clock.PartIndex, Is.EqualTo(1), "들어온 사람도 기다린다");
            clock.Spend("b", 1);
            Assert.That(clock.IsDayOver, Is.True);
            Assert.That(clock.Join("b"), Is.False, "이미 있는 사람");
        }

        [Test]
        public void Rejoining_TheSameDay_KeepsWhatWasAlreadyFinished()
        {
            var clock = Room("a", "b");
            clock.Spend("b", 1);
            clock.Leave("b");
            Assert.That(clock.PartIndex, Is.EqualTo(0), "a는 아직 안 끝냈다");
            clock.Join("b");
            Assert.That(clock.HasFinishedPart("b"), Is.True, "같은 날 다시 들어와도 끝낸 때를 다시 하지 않는다");
            Assert.That(clock.CanSpend("b", 1), Is.False);
            clock.Spend("a", 1);
            Assert.That(clock.PartIndex, Is.EqualTo(1));
        }

        [Test]
        public void Rejoining_OnALaterDay_StartsFresh()
        {
            var clock = Room("a", "b");
            clock.EndDay("b");
            clock.Leave("b");
            clock.EndDay("a");
            Assert.That(clock.Day, Is.EqualTo(2));
            clock.Join("b");
            Assert.That(clock.IsDayEnded("b"), Is.False);
            Assert.That(clock.CompletedParts("b"), Is.EqualTo(0));
        }

        [Test]
        public void Nobody_TheClockStops()
        {
            var clock = Room();
            Assert.That(clock.CanSpend("a", 1), Is.False, "방에 없는 사람은 쓰지 못한다");
            Assert.That(clock.Spend("a", 1), Is.False);
            Assert.That(clock.EndDay("a"), Is.False);
            Assert.That(clock.Tick(1000), Is.False);
            Assert.That(clock.PartIndex, Is.EqualTo(0));
            Assert.That(clock.Day, Is.EqualTo(1));

            var two = Room("a");
            two.Leave("a");
            Assert.That(two.PartIndex, Is.EqualTo(0), "모두 나가도 저절로 넘어가지 않는다");
            Assert.That(two.Day, Is.EqualTo(1));
        }

        [Test]
        public void AutoSkip_IsOffByDefault()
        {
            var clock = Room("a", "b");
            clock.Spend("a", 1);
            Assert.That(clock.Tick(1_000_000), Is.False);
            Assert.That(clock.PartIndex, Is.EqualTo(0));
            Assert.That(RoomClockSettings.Default.AutoSkipAfterTicks, Is.EqualTo(0));
        }

        [Test]
        public void AutoSkip_WhenOn_SkipsThoseNotFinished_AfterTheTicksWhileSomeoneWaits()
        {
            var clock = new RoomClock(new LifeCalendar(), new RoomClockSettings(autoSkipAfterTicks: 10));
            clock.Join("a"); clock.Join("b"); clock.Join("c");
            Assert.That(clock.Tick(50), Is.False, "아무도 기다리지 않으면 세지 않는다");

            clock.Spend("a", 1);
            Assert.That(clock.Tick(9), Is.False);
            Assert.That(clock.PartIndex, Is.EqualTo(0));
            Assert.That(clock.Tick(1), Is.True);
            Assert.That(clock.PartIndex, Is.EqualTo(1), "안 끝낸 b, c는 넘긴 것으로 본다");

            clock.Spend("b", 1);
            Assert.That(clock.Tick(9), Is.False, "새 때는 새로 센다");
            clock.Skip("c");
            clock.Spend("a", 1);
            Assert.That(clock.IsDayOver, Is.True);

            clock.EndDay("a");
            Assert.That(clock.Tick(100), Is.False, "하루 마감은 자동으로 넘기지 않는다");
            Assert.That(clock.Day, Is.EqualTo(1));
        }

        [Test]
        public void Weekend_HasThreeParts_FromTheCalendar()
        {
            var clock = new RoomClock(new LifeCalendar(), day: 6);
            clock.Join("a");
            Assert.That(clock.PartsToday, Is.EqualTo(3));
            clock.Spend("a", 1); clock.Spend("a", 1);
            Assert.That(clock.IsDayOver, Is.False);
            clock.Spend("a", 1);
            Assert.That(clock.IsDayOver, Is.True);
        }

        [Test]
        public void Participants_ListsWhoIsPresentAndWhoFinished()
        {
            var clock = Room("a", "b");
            clock.Spend("a", 1);
            clock.Join("c");
            clock.Leave("c");
            var list = new List<RoomParticipantState>();
            clock.CopyPresent(list);
            Assert.That(list.Count, Is.EqualTo(2));
            Assert.That(list.Find(p => p.ParticipantId == "a").FinishedPart, Is.True);
            Assert.That(list.Find(p => p.ParticipantId == "b").FinishedPart, Is.False);
        }

        // ---------- 때의 대표 시각 ----------

        [Test]
        public void RepresentativeMinutes_ComeFromTheCalendarsPartTimes_LastPartsOfTheDay()
        {
            var calendar = new LifeCalendar(2, 3, 96, new PartOfDayTimes(new[] { 9 * 60, 14 * 60, 18 * 60 + 30 }, 22 * 60));
            var clock = new RoomClock(calendar);
            clock.Join("a");
            Assert.That(clock.RepresentativeMinutes, Is.EqualTo(14 * 60), "평일 첫 때(방과 후)");
            clock.Spend("a", 1);
            Assert.That(clock.RepresentativeMinutes, Is.EqualTo(18 * 60 + 30), "평일 둘째 때(저녁)");
            clock.Spend("a", 1);
            Assert.That(clock.RepresentativeMinutes, Is.EqualTo(22 * 60), "때를 다 쓰면 밤");

            var weekend = new RoomClock(calendar, day: 6);
            Assert.That(weekend.RepresentativeMinutes, Is.EqualTo(9 * 60), "주말 첫 때(오전)");
        }

        [Test]
        public void PartOfDayTimes_MoreParts_ThanTimes_ReuseTheFirstTime()
        {
            var times = new PartOfDayTimes(new[] { 600, 900 }, 1300);
            Assert.That(times.MinutesFor(0, 3), Is.EqualTo(600));
            Assert.That(times.MinutesFor(1, 3), Is.EqualTo(600));
            Assert.That(times.MinutesFor(2, 3), Is.EqualTo(900));
            Assert.That(times.MinutesFor(3, 3), Is.EqualTo(1300));
            Assert.That(new LifeCalendar().PartTimes, Is.Not.Null, "달력에는 기본 대표 시각이 있다");
        }

        // ---------- 세이브 ----------

        [Test]
        public void Save_RoundTrips_DayPartAndEachParticipant_NobodyPresentAfterLoading()
        {
            var clock = Room("a", "b");
            clock.Spend("a", 1); clock.Spend("b", 1);
            clock.Spend("a", 1);
            clock.EndDay("a");
            var data = clock.ToSaveData();
            Assert.That(data.Version, Is.EqualTo(RoomClockSaveData.KnownVersion));

            var loaded = RoomClock.FromSaveData(data, new LifeCalendar());
            Assert.That(loaded.Day, Is.EqualTo(1));
            Assert.That(loaded.PartIndex, Is.EqualTo(1));
            Assert.That(loaded.PresentCount, Is.EqualTo(0), "읽은 뒤에는 아무도 없다(들어오면 다시 센다)");
            // 먼저 마감한 a만 들어오면 a 혼자인 방이라 바로 넘어간다. 기다릴 b가 먼저 들어온다.
            loaded.Join("b"); loaded.Join("a");
            Assert.That(loaded.Day, Is.EqualTo(1));
            Assert.That(loaded.IsDayEnded("a"), Is.True);
            Assert.That(loaded.HasFinishedPart("b"), Is.False);
            loaded.Spend("b", 1);
            loaded.EndDay("b");
            Assert.That(loaded.Day, Is.EqualTo(2));
        }

        [Test]
        public void Save_ThroughTheStore_AndOldWorldTimeFile_BecomesTheClocksDay()
        {
            var store = new InMemorySaveStore();
            Assert.That(RoomClockSaveFiles.Load(store, "slot-1", new LifeCalendar()).Day, Is.EqualTo(1), "아무 파일도 없으면 첫날");

            // 시계 파일이 없고 옛 공용 시간 파일(world-time/<슬롯>.json)만 있으면 그 날짜로 시작한다.
            store.Write(SaveFileNames.WorldTimeSlot, SaveFileNames.WorldTime("slot-1"), new WorldTimeSaveData { Version = 1, CurrentDay = 9, TimeOfDay = 2 });
            var fromOld = RoomClockSaveFiles.Load(store, "slot-1", new LifeCalendar());
            Assert.That(fromOld.Day, Is.EqualTo(9));
            Assert.That(fromOld.PartIndex, Is.EqualTo(0), "옛 파일의 때(아침·낮·저녁·밤)는 시간 포인트 칸과 맞지 않아 첫 때로 둔다");

            fromOld.Join("a");
            fromOld.Spend("a", 1);
            RoomClockSaveFiles.Save(store, "slot-1", fromOld);
            Assert.That(store.TryRead<RoomClockSaveData>("slot-1", SaveFileNames.RoomClock, out _), Is.True);
            var again = RoomClockSaveFiles.Load(store, "slot-1", new LifeCalendar());
            Assert.That(again.Day, Is.EqualTo(9));
            Assert.That(again.PartIndex, Is.EqualTo(1), "시계 파일이 있으면 옛 파일보다 먼저");
        }

        [Test]
        public void FromSave_ClampsBrokenValues()
        {
            var data = new RoomClockSaveData
            {
                Version = 1, Day = -4, PartIndex = 99,
                Participants = new[] { new RoomClockParticipantSaveData { ParticipantId = "a", CompletedParts = 50 }, null, new RoomClockParticipantSaveData { ParticipantId = "" } },
            };
            var clock = RoomClock.FromSaveData(data, new LifeCalendar());
            Assert.That(clock.Day, Is.EqualTo(1));
            Assert.That(clock.PartIndex, Is.EqualTo(2));
            clock.Join("a");
            Assert.That(clock.CompletedParts("a"), Is.EqualTo(2));
            Assert.That(RoomClock.FromSaveData(null, new LifeCalendar()).Day, Is.EqualTo(1));
            Assert.That(new RoomClockSaveData().Version, Is.EqualTo(SaveVersion.Unity));
        }

        // ---------- 개인 진행에서 만들기(시계가 없던 세이브) ----------

        [Test]
        public void FromProgress_UsesTheDayAndSpentPoints_OfTheOldSave()
        {
            var progress = StudentLifeProgress.FromSaveData(new StudentLifeProgressSaveData
            {
                SaveSlot = "s", PlayerId = "p", Energy = 10, Focus = 10, CurrentDay = 6, TimePointsSpentToday = 2,
            });
            progress.ConfigureCalendar(new LifeCalendar());
            var clock = RoomClock.FromProgress(progress, "p");
            Assert.That(clock.Day, Is.EqualTo(6));
            Assert.That(clock.PartIndex, Is.EqualTo(2));
            Assert.That(clock.PartsToday, Is.EqualTo(3));
            Assert.That(clock.IsPresent("p"), Is.True, "혼자인 방에 그 사람이 있다");
            Assert.That(clock.CanSpend("p", 1), Is.True);
        }

        [Test]
        public void FromProgress_AnEndedDay_IsAlreadyTheNextDayOfTheRoom()
        {
            var progress = StudentLifeProgress.FromSaveData(new StudentLifeProgressSaveData
            {
                SaveSlot = "s", PlayerId = "p", Energy = 10, Focus = 10, CurrentDay = 3, TimePointsSpentToday = 1,
                DayState = StudentDayState.ResultReady.ToString(), LastSettledDay = 3,
            });
            progress.ConfigureCalendar(new LifeCalendar());
            var clock = RoomClock.FromProgress(progress, "p");
            Assert.That(clock.Day, Is.EqualTo(4), "혼자인 방은 마감하는 순간 다음 날로 넘어갔다");
            Assert.That(clock.PartIndex, Is.EqualTo(0));
        }

        // ---------- 개인 시간 포인트와 잇기 ----------

        [Test]
        public void Gate_PersonalSpendingFinishesTheRoomsPart_AndWaitingBlocksIt()
        {
            var clock = Room("a", "b");
            var a = NewStudent("a", clock);
            var b = NewStudent("b", clock);

            Assert.That(a.TrySpendTimePoints(1), Is.True);
            Assert.That(clock.HasFinishedPart("a"), Is.True);
            Assert.That(a.CanSpendTimePoints(1), Is.False, "b를 기다리는 동안 a는 시간 포인트를 쓰지 못한다");
            Assert.That(a.TryUseTimePolicy(new ActivityTimePolicy("x")), Is.False);
            Assert.That(a.TimePointsSpentToday, Is.EqualTo(1));
            Assert.That(a.CanUseTimePolicy(new ActivityTimePolicy("talk", 0)), Is.True, "포인트를 안 쓰는 일은 된다");

            Assert.That(b.TryUseTimePolicy(new ActivityTimePolicy("y")), Is.True);
            Assert.That(clock.PartIndex, Is.EqualTo(1));
            Assert.That(a.CanSpendTimePoints(1), Is.True);
        }

        [Test]
        public void Gate_Alone_BehavesLikeThePersonalBudget()
        {
            var clock = Room("a");
            var a = NewStudent("a", clock);
            Assert.That(a.TrySpendTimePoints(1), Is.True);
            Assert.That(a.TrySpendTimePoints(1), Is.True);
            Assert.That(a.CanSpendTimePoints(1), Is.False);
            Assert.That(clock.IsDayOver, Is.True);
            Assert.That(clock.PartIndex, Is.EqualTo(a.TimePointsSpentToday));
        }

        [Test]
        public void Gate_CatchUp_AfterTheRoomSkippedMe_BringsPersonalPointsUpToTheRoom()
        {
            var clock = new RoomClock(new LifeCalendar(), new RoomClockSettings(autoSkipAfterTicks: 1));
            clock.Join("a"); clock.Join("b");
            var a = NewStudent("a", clock, out _);
            var b = NewStudent("b", clock, out var bGate);
            Assert.That(a.TrySpendTimePoints(1), Is.True);
            Assert.That(clock.Tick(1), Is.True, "b를 넘겼다");
            Assert.That(clock.CompletedParts("b"), Is.EqualTo(1));
            Assert.That(b.TimePointsSpentToday, Is.EqualTo(0), "개인 진행은 아직 뒤처져 있다");

            Assert.That(bGate.CatchUp(b), Is.EqualTo(1));
            Assert.That(b.TimePointsSpentToday, Is.EqualTo(1));
            Assert.That(clock.PartIndex, Is.EqualTo(1), "따라잡기는 방에 다시 알리지 않는다(두 번 세지 않는다)");
            Assert.That(clock.CompletedParts("b"), Is.EqualTo(1));
            Assert.That(bGate.CatchUp(b), Is.EqualTo(0), "맞춘 뒤에는 아무것도 하지 않는다");
            Assert.That(b.TrySpendTimePoints(1), Is.True, "다음 때는 그대로 쓴다");
            Assert.That(clock.CompletedParts("b"), Is.EqualTo(2));
        }

        [Test]
        public void Gate_CatchUp_OnlyOnTheSameDay_WhileTheDayIsInProgress()
        {
            var clock = Room("a");
            var a = NewStudent("a", clock, out var gate);
            Assert.That(gate.CatchUp(a), Is.EqualTo(0), "같으면 아무것도 하지 않는다");
            clock.EndDay("a");
            Assert.That(clock.Day, Is.EqualTo(2));
            Assert.That(gate.CatchUp(a), Is.EqualTo(0), "방이 다른 날이면 개인 시간 포인트를 건드리지 않는다");
            Assert.That(a.TimePointsSpentToday, Is.EqualTo(0));
        }

        [Test]
        public void SpendFrom_IsTheSpendForThatPart_AndALateOneForAPartAlreadyCounted_ChangesNothing()
        {
            var clock = new RoomClock(new LifeCalendar(), new RoomClockSettings(autoSkipAfterTicks: 1));
            clock.Join("a"); clock.Join("b");
            Assert.That(clock.SpendFrom("a", 1, 0, 1), Is.True);
            Assert.That(clock.SpendFrom("a", 1, 0, 1), Is.False, "같은 요청을 다시 해도 한 번만");
            Assert.That(clock.CompletedParts("a"), Is.EqualTo(1));

            Assert.That(clock.Tick(1), Is.True, "b는 넘겨졌다");
            Assert.That(clock.PartIndex, Is.EqualTo(1));
            Assert.That(clock.SpendFrom("b", 1, 0, 1), Is.False, "넘겨진 때에 늦게 온 요청은 이미 센 것");
            Assert.That(clock.CompletedParts("b"), Is.EqualTo(1));
            Assert.That(clock.PartIndex, Is.EqualTo(1), "다음 때까지 끝낸 것으로 세지 않는다");

            Assert.That(clock.SpendFrom("b", 1, 1, 1), Is.True);
            Assert.That(clock.SpendFrom("a", 2, 1, 1), Is.False, "다른 날의 요청은 받지 않는다");
            Assert.That(clock.CompletedParts("a"), Is.EqualTo(1));
        }

        [Test]
        public void SpendFrom_TwoPoints_WhenOneWasAlreadySkipped_FinishesOnlyTheRest()
        {
            var clock = new RoomClock(new LifeCalendar(weekdayBudget: 3), new RoomClockSettings(autoSkipAfterTicks: 1));
            clock.Join("a"); clock.Join("b");
            clock.Spend("a", 1);
            clock.Tick(1);
            Assert.That(clock.PartIndex, Is.EqualTo(1));
            Assert.That(clock.SpendFrom("b", 1, 0, 2), Is.True, "두 포인트 중 넘겨진 앞 때를 빼고 나머지만");
            Assert.That(clock.CompletedParts("b"), Is.EqualTo(2));
        }

        private static StudentLifeProgress NewStudent(string id, RoomClock clock) => NewStudent(id, clock, out _);

        private static StudentLifeProgress NewStudent(string id, RoomClock clock, out RoomTimePointGate gate)
        {
            var student = new StudentLifeProgress("s", id, 10, 10);
            student.ConfigureCalendar(new LifeCalendar());
            gate = new RoomTimePointGate(clock, id);
            student.ConfigureTimeGate(gate);
            return student;
        }
    }
}
