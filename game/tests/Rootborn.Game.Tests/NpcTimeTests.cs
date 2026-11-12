using System;
using NUnit.Framework;
using Rootborn.Core.StudentLife;
using Rootborn.Core.Time;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// NPC 걷기가 쓰는 분 아래까지 든 시각(<see cref="GameSession.DayMinutesExact"/>). 정수부는 늘 개인 시계의 분이고, 조각은 다음 분까지 흐른 비율이다.
    /// 혼자 하기는 흐르는 시계의 남은 조각, 같이 하는 방은 방의 분이 바뀐 뒤 들어온 실제 초에서 얻는다. GameSession 명령만 부르는 자동 테스트다(화면 근거 아님).
    /// </summary>
    public sealed class NpcTimeTests
    {
        private const string LibraryStudy = "outside.self-study.library";
        private static TimeRules Rules => TestWorld.Definitions.TimeRules;
        private static float SecondsPerMinute => Rules.RealSecondsPerGameDay / 1440f;

        [Test]
        public void DayMinutesExact_는_분_사이를_메운다()
        {
            using var saves = new TempSaves();
            var session = TestWorld.Start(saves.Store);
            double before = session.DayMinutesExact;
            Assert.That(Math.Floor(before), Is.EqualTo(session.Student.TimeMinutes));

            session.AdvanceTime(SecondsPerMinute * 0.5f, false);
            Assert.That(session.DayMinutesExact - before, Is.InRange(0.45, 0.55));
            Assert.That((int)Math.Floor(session.DayMinutesExact), Is.EqualTo(session.Student.TimeMinutes));

            session.AdvanceTime(SecondsPerMinute * 0.75f, false);
            Assert.That(session.DayMinutesExact - before, Is.InRange(1.2, 1.3), "분을 넘겨도 이어진다");
            Assert.That((int)Math.Floor(session.DayMinutesExact), Is.EqualTo(session.Student.TimeMinutes));
        }

        [Test]
        public void DayMinutesExact_는_패널이_열려_멈추면_그대로다()
        {
            using var saves = new TempSaves();
            var session = TestWorld.Start(saves.Store);
            session.AdvanceTime(SecondsPerMinute * 0.3f, false);
            double before = session.DayMinutesExact;

            session.AdvanceTime(SecondsPerMinute * 5f, true);
            Assert.That(session.DayMinutesExact, Is.EqualTo(before));
        }

        [Test]
        public void 혼자_하기에서_활동이_시계를_건너뛰어도_정수부는_개인_시계의_분이다()
        {
            using var saves = new TempSaves();
            var session = TestWorld.Start(saves.Store);
            session.AdvanceTime(SecondsPerMinute * 0.6f, false);

            Assert.That(session.PerformOutsideSchoolActivity(LibraryStudy, "outside:npc-time:1").Kind, Is.EqualTo(LifeActivityResultKind.Applied));
            Assert.That((int)Math.Floor(session.DayMinutesExact), Is.EqualTo(session.Student.TimeMinutes));
            Assert.That(session.DayMinutesExact - session.Student.TimeMinutes, Is.LessThan(1d));
        }

        [Test]
        public void 같이_하는_방에서는_방의_분을_따르고_조각은_1분을_넘지_않는다()
        {
            using var saves = new TempSaves();
            var clock = new RoomClock(Rules, 1, -1);
            clock.Join("player-1");
            var session = GameSession.Start(TestWorld.Definitions, TestWorld.Settings, saves.Store, "slot-npc-time", "player-1", room: clock);
            Assert.That(session.IsSoloRoom, Is.False);
            session.AdvanceTime(0.01f, false);
            int minute = session.Student.TimeMinutes;
            Assert.That(minute, Is.EqualTo(clock.Minutes));

            session.AdvanceTime(SecondsPerMinute * 0.5f, false);
            Assert.That(session.DayMinutesExact - minute, Is.InRange(0.45, 0.55), "방의 분 안에서 실제 초로 메운다");
            for (int i = 0; i < 10; i++) session.AdvanceTime(SecondsPerMinute, false);
            Assert.That(session.Student.TimeMinutes, Is.EqualTo(minute), "같이 하는 방은 스스로 흐르지 않는다");
            Assert.That(session.DayMinutesExact, Is.LessThan(minute + 1d), "조각은 1분을 넘지 않는다");
            Assert.That(session.DayMinutesExact, Is.GreaterThanOrEqualTo(minute + 0.99));

            clock.Advance(1);
            session.AdvanceTime(SecondsPerMinute * 0.2f, false);
            Assert.That((int)Math.Floor(session.DayMinutesExact), Is.EqualTo(minute + 1), "방의 분이 바뀌면 새 분");
            Assert.That(session.DayMinutesExact - (minute + 1), Is.LessThan(0.1), "조각은 0에서 다시");

            session.AdvanceTime(SecondsPerMinute * 0.5f, false);
            Assert.That(session.DayMinutesExact - (minute + 1), Is.InRange(0.45, 0.55));
        }

        /// <summary>
        /// NPC 자리는 (데이터, 날, 그날의 분)의 함수라, 같은 방의 두 사람이 같은 (날, 분)을 가지면 같은 자리를 계산한다(설계 4절).
        /// 한 사람은 방이 한참 흐른 뒤에 들어오고, 그 사이 다른 사람은 프레임 간격이 다르게(잘게) 시간을 받는다 — 개인 진행·들어온 때·프레임 간격이 정수 분에 들어가지 않는다.
        /// </summary>
        [Test]
        public void 같은_방의_두_사람은_같은_분에_있다()
        {
            using var saves = new TempSaves();
            var clock = new RoomClock(Rules, 1, -1);
            clock.Join("player-a");
            var a = GameSession.Start(TestWorld.Definitions, TestWorld.Settings, saves.Store, "slot-npc-room-a", "player-a", room: clock);
            for (int i = 0; i < 37; i++)
            {
                clock.Advance(1);
                for (int k = 0; k < 3; k++) a.AdvanceTime(SecondsPerMinute / 3f, false);
            }
            Assert.That(a.PerformOutsideSchoolActivity(LibraryStudy, "outside:npc-room:a").Kind, Is.EqualTo(LifeActivityResultKind.Applied),
                "앞서 들어온 사람만 활동을 했다(개인 진행이 다르다)");

            clock.Join("player-b");
            var b = GameSession.Start(TestWorld.Definitions, TestWorld.Settings, saves.Store, "slot-npc-room-b", "player-b", room: clock);
            for (int step = 0; step < 50; step++)
            {
                if (step % 5 == 0) clock.Advance(1);
                a.AdvanceTime(SecondsPerMinute * 0.07f, false);
                b.AdvanceTime(SecondsPerMinute * 0.3f, true); // 들어간 사람은 메뉴를 연 채, 프레임 간격도 다르다(분이 바뀌기 전에 1분어치를 넘게 받는다)
                Assert.That((int)Math.Floor(a.DayMinutesExact), Is.EqualTo(clock.Minutes), step + "번째: 앞서 들어온 사람");
                Assert.That((int)Math.Floor(b.DayMinutesExact), Is.EqualTo(clock.Minutes), step + "번째: 늦게 들어온 사람");
                Assert.That(b.Student.CurrentDay, Is.EqualTo(a.Student.CurrentDay));
                Assert.That(a.Student.CurrentDay, Is.EqualTo(clock.Day));
                Assert.That(Math.Abs(a.DayMinutesExact - b.DayMinutesExact), Is.LessThan(1d), step + "번째: 분 아래 조각의 차이만");
            }
        }
    }
}
