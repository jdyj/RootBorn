using System;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Time;
using Rootborn.Game.Tests;

namespace Rootborn.Balance.Tests
{
    /// <summary>잠 규칙(설계 13.1절): 시각은 그날 0시부터 센 분이다(자정 넘긴 02:00 = 26:00). 손 계산으로 본다.</summary>
    public sealed class SleepRulesTests
    {
        private static TimeRules Rules => TestWorld.Definitions.TimeRules;

        private static int At(int hour, int minute = 0) => hour * 60 + minute;

        private static SleepMoment Moment(int now, int walk, int action, int runDay = 1)
            => new SleepMoment { RunDay = runDay, NowMinutes = now, WalkMinutes = walk, ActionMinutes = action, DayEndMinutes = Rules.DayEndMinutes };

        [Test]
        public void FinishBy_2200_AllowsAnActionEndingAt2200_AndRefusesOneEndingAt2201()
        {
            var rule = SleepRules.Parse("finish-by 22:00", Rules);
            Assert.That(rule.Id, Is.EqualTo("finish-by 22:00"));
            Assert.That(rule.SleepsTonight(1), Is.True);
            Assert.That(rule.MayStart(Moment(At(18, 30), 30, 180)), Is.True, "18:30 + 30 + 180 = 22:00");
            Assert.That(rule.MayStart(Moment(At(18, 31), 30, 180)), Is.False, "18:31 + 30 + 180 = 22:01");
        }

        [Test]
        public void FinishBy_0200_IsAfterMidnightOfTheSameDay()
        {
            var rule = SleepRules.Parse("finish-by 02:00", Rules);
            Assert.That(rule.MayStart(Moment(At(23), 30, 120)), Is.True, "23:00 + 150분 = 25:30 ≤ 26:00");
            Assert.That(rule.MayStart(Moment(At(25), 30, 120)), Is.False, "01:00(25:00) + 150분 = 27:30 > 26:00");
            Assert.That(rule.MayStart(Moment(At(9), 30, 180)), Is.True, "아침 9시에 시작하는 일을 그날 아침 02:00으로 읽어 막지 않는다");
        }

        [Test]
        public void StartBefore_2200_AllowsStartingAt2159_WhateverTheLength()
        {
            var rule = SleepRules.Parse("start-before 22:00", Rules);
            Assert.That(rule.SleepsTonight(1), Is.True);
            Assert.That(rule.MayStart(Moment(At(21, 59), 30, 180)), Is.True);
            Assert.That(rule.MayStart(Moment(At(22), 0, 10)), Is.False);
        }

        [Test]
        public void Never_NeverSleeps_AndOnlyRefusesWhatCrossesDayEnd()
        {
            var rule = SleepRules.Parse("never", Rules);
            for (int day = 1; day <= 30; day++) Assert.That(rule.SleepsTonight(day), Is.False, day + "일째");
            int end = Rules.DayEndMinutes;
            Assert.That(rule.MayStart(Moment(end - 210, 30, 180)), Is.True, "날 넘김에 딱 끝난다");
            Assert.That(rule.MayStart(Moment(end - 209, 30, 180)), Is.False, "날 넘김을 1분 넘긴다");
            Assert.That(rule.MayStart(Moment(At(23), 30, 180)), Is.True, "취침 시각 뒤라도 시작한다");
        }

        [Test]
        public void StayUpEvery3_StaysUpOnDays3_6_9_AndSleepsBy2200Otherwise()
        {
            var rule = SleepRules.Parse("stay-up-every 3", Rules);
            var stayUp = Enumerable.Range(1, 9).Where(d => !rule.SleepsTonight(d)).ToArray();
            Assert.That(stayUp, Is.EqualTo(new[] { 3, 6, 9 }));
            Assert.That(rule.MayStart(Moment(At(18, 31), 30, 180, runDay: 1)), Is.False, "자는 날은 22:00까지 끝낸다");
            Assert.That(rule.MayStart(Moment(At(18, 30), 30, 180, runDay: 1)), Is.True);
            Assert.That(rule.MayStart(Moment(At(23), 30, 180, runDay: 3)), Is.True, "밤새는 날은 날 넘김 전까지");
            Assert.That(rule.MayStart(Moment(Rules.DayEndMinutes - 209, 30, 180, runDay: 3)), Is.False);
        }

        // S1: 넘겨 잇기 — 자지 않는 밤에는 07:00 전에 시작하기만 하면 된다(시계 조각 1분을 남긴다). 끝 제한이 없다.
        [Test]
        public void NeverCarry_StartsBeforeDayEnd_EvenWhenItEndsAfter()
        {
            var rule = SleepRules.Parse("never carry", Rules);
            int end = Rules.DayEndMinutes;
            Assert.That(rule.Id, Is.EqualTo("never carry"));
            Assert.That(rule.SleepsTonight(1), Is.False);
            Assert.That(rule.MayStart(Moment(end - 32, 30, 180)), Is.True, "걷고 나면 07:00 2분 전 — 시작한다(끝은 07:00 뒤)");
            Assert.That(rule.MayStart(Moment(end - 31, 30, 180)), Is.False, "걷고 나면 07:00 1분 전 — 걷기가 1분 더 걸리면 걷다가 날이 넘어간다");
            Assert.That(rule.LatestEnd(1, end), Is.Null, "넘겨 잇는 밤에는 끝 제한이 없다");
            Assert.That(SleepRules.Parse("never", Rules).LatestEnd(1, end), Is.EqualTo(end));
        }

        [Test]
        public void StayUpEvery2Carry_CarriesOnlyOnItsAwakeNights()
        {
            var rule = SleepRules.Parse("stay-up-every 2 carry", Rules);
            int end = Rules.DayEndMinutes;
            Assert.That(rule.MayStart(Moment(end - 32, 30, 180, runDay: 2)), Is.True, "밤새는 날");
            Assert.That(rule.MayStart(Moment(At(18, 31), 30, 180, runDay: 1)), Is.False, "자는 날은 그대로 22:00까지");
            Assert.That(rule.LatestEnd(1, end), Is.EqualTo(Rules.BedtimeMinutes));
            Assert.That(rule.LatestEnd(2, end), Is.Null);
        }

        // S2: 가끔 밤새고 다른 밤은 그 시각까지 끝내고 잔다.
        [Test]
        public void StayUpEvery2Until0100_SleepsBy0100OnOtherNights()
        {
            var rule = SleepRules.Parse("stay-up-every 2 until 01:00", Rules);
            Assert.That(rule.SleepsTonight(1), Is.True);
            Assert.That(rule.SleepsTonight(2), Is.False);
            Assert.That(rule.MayStart(Moment(At(21, 30), 30, 180, runDay: 1)), Is.True, "21:30 + 210분 = 25:00 ≤ 01:00(25:00)");
            Assert.That(rule.MayStart(Moment(At(21, 31), 30, 180, runDay: 1)), Is.False);
            Assert.That(rule.LatestEnd(1, Rules.DayEndMinutes), Is.EqualTo(At(25)));
            Assert.That(rule.LatestEnd(2, Rules.DayEndMinutes), Is.EqualTo(Rules.DayEndMinutes));

            var both = SleepRules.Parse("stay-up-every 2 until 01:00 carry", Rules);
            Assert.That(both.MayStart(Moment(Rules.DayEndMinutes - 32, 30, 180, runDay: 2)), Is.True);
            Assert.That(both.MayStart(Moment(At(21, 31), 30, 180, runDay: 1)), Is.False);
        }

        // S6: 행동 뒤 검사에 쓰는 끝 — start-before는 끝 제한이 없다.
        [Test]
        public void LatestEnd_IsTheRuleTimeOnSleepingNights_AndNoneForStartBefore()
        {
            int end = Rules.DayEndMinutes;
            Assert.That(SleepRules.Parse("finish-by 02:00", Rules).LatestEnd(1, end), Is.EqualTo(At(26)));
            Assert.That(SleepRules.Parse("start-before 22:00", Rules).LatestEnd(1, end), Is.Null);
            Assert.That(SleepRules.Parse("stay-up-every 3", Rules).LatestEnd(3, end), Is.EqualTo(end));
        }

        [Test]
        public void UnknownOrMalformedRuleSaysItsText()
        {
            foreach (var bad in new[] { "nope", "finish-by 25:99", "stay-up-every 0", "finish-by", "never 3", "start-before 7",
                "stay-up-every 2 carry until 01:00", "stay-up-every 2 until", "stay-up-every 2 until 25:00", "never carry carry", "carry", "stay-up-every 2 later 01:00" })
            {
                Assert.That(SleepRules.Knows(bad), Is.False, bad);
                var e = Assert.Throws<ArgumentException>(() => SleepRules.Parse(bad, Rules), bad);
                Assert.That(e.Message, Does.Contain(bad));
            }
            Assert.That(SleepRules.Knows(null), Is.False);
            Assert.That(SleepRules.Knows("finish-by 06:00"), Is.True);
        }
    }
}
