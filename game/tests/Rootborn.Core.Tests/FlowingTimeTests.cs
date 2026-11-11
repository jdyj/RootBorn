using System;
using System.Text.Json;
using NUnit.Framework;
using Rootborn.Core.StudentLife;
using Rootborn.Core.Time;

namespace Rootborn.Core.Tests
{
    // 흐르는 시간(설계 docs/superpowers/specs/2026-11-12-flowing-time-design.md 3절): 시계, 밤샘, 지침, 잠과 회복, 활동이 쓰는 시간.
    // 수치는 테스트가 정한 값이다(실제 값은 game/time.json, Data·Game 테스트가 본다).
    public sealed class FlowingTimeTests
    {
        private const int Hour = 60;
        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true };

        // 하루 600초, 07:00 시작, 22:00 취침, 밤 1시간마다 체력 1, 늦게 자면 1시간마다 회복 2.
        private static TimeRules Rules() => new TimeRules(600f, 7 * Hour, 22 * Hour, 1, 2);

        private static StudentLifeProgress Progress(int energy = 12, int minutes = 7 * Hour)
        {
            var p = new StudentLifeProgress("flow", "p", energy, 10, 0, minutes);
            p.ConfigureCalendar(new LifeCalendar(2, 3));
            p.ConfigureTimeRules(Rules());
            return p;
        }

        private static DayEndRuleSet Restore12() => new DayEndRuleSet("rules", "", new IDayEndRule[] { new EnergyRestoreRule(12), new FocusRestoreRule(10) });

        private static LifeActivity Activity(int minutes, int energy, ActivityTimePolicy policy = null)
            => new LifeActivity(Guid.NewGuid().ToString("N"), LifeActivityCategory.SelfStudy, policy, minutes, energy, 0, 0, null, null, null);

        // ---------- 시계 ----------

        [Test]
        public void FlowingClock_TurnsRealSecondsIntoGameMinutes_AndKeepsTheFraction()
        {
            var clock = new FlowingClock(600f);
            Assert.That(clock.Advance(25f, false), Is.EqualTo(60), "하루 600초면 게임 1시간이 25초");
            Assert.That(clock.Advance(0.2f, false), Is.EqualTo(0), "1분(약 0.42초)이 안 되면 0");
            Assert.That(clock.Advance(0.3f, false), Is.EqualTo(1), "남은 조각이 모여 1분이 된다");
            Assert.That(clock.Advance(600f, true), Is.EqualTo(0), "멈춤이면 흐르지 않는다");
            Assert.That(clock.Advance(-5f, false), Is.EqualTo(0), "음수는 무시");
            Assert.That(clock.Advance(float.NaN, false), Is.EqualTo(0));
            Assert.That(clock.Advance(600f, false), Is.EqualTo(24 * Hour));
        }

        [Test]
        public void TimeRules_DayRunsFromSevenToSevenNextMorning()
        {
            var rules = Rules();
            Assert.That(rules.DayStartMinutes, Is.EqualTo(7 * Hour));
            Assert.That(rules.DayEndMinutes, Is.EqualTo(31 * Hour), "다음 날 07:00은 그날 0시부터 31시간");
            Assert.That(rules.HoursPastBedtime(22 * Hour), Is.EqualTo(0));
            Assert.That(rules.HoursPastBedtime(22 * Hour + 59), Is.EqualTo(0), "다 채운 시간만 센다");
            Assert.That(rules.HoursPastBedtime(26 * Hour), Is.EqualTo(4), "02:00은 취침 시각 뒤 4시간");
            Assert.That(rules.HoursPastBedtime(40 * Hour), Is.EqualTo(9), "다음 날 07:00을 넘겨 세지 않는다");
            Assert.That(TimeRules.ClockMinutes(26 * Hour + 5), Is.EqualTo(2 * Hour + 5), "보이는 시각은 24시간으로 감는다");
        }

        [Test]
        public void AdvanceClock_MovesTheTime_AndStopsOnlyWhenTheDayIsSettled()
        {
            var p = Progress();
            Assert.That(p.AdvanceClock(90), Is.EqualTo(90));
            Assert.That(p.TimeMinutes, Is.EqualTo(8 * Hour + 30));
            Assert.That(p.MinutesUntilDayEnd, Is.EqualTo(31 * Hour - (8 * Hour + 30)));

            Assert.That(p.TryEndDay(Restore12(), out _), Is.True);
            Assert.That(p.AdvanceClock(60), Is.EqualTo(0), "결과를 보는 동안(ResultReady)은 흐르지 않는다");
        }

        // ---------- 밤샘과 지침 ----------

        [Test]
        public void StayingUpPastBedtime_LosesOneEnergyPerFullHour_OnlyOnce()
        {
            var p = Progress(energy: 5, minutes: 21 * Hour);
            p.AdvanceClock(59);
            Assert.That(p.Energy, Is.EqualTo(5), "취침 시각 전에는 줄지 않는다");
            p.AdvanceClock(60);
            Assert.That(p.Energy, Is.EqualTo(5), "22:59 — 아직 한 시간을 다 채우지 않았다");
            p.AdvanceClock(1);
            Assert.That(p.Energy, Is.EqualTo(4), "23:00");
            p.AdvanceClock(30);
            Assert.That(p.Energy, Is.EqualTo(4), "같은 시간을 두 번 깎지 않는다");
            p.AdvanceClock(90);
            Assert.That(p.Energy, Is.EqualTo(2), "01:00 — 23:00, 00:00, 01:00 세 번");
            Assert.That(p.IsExhausted, Is.False);
        }

        [Test]
        public void EnergyAtZero_IsExhausted_AndNoActivityCanStart_EvenAFreeOne()
        {
            var p = Progress(energy: 1, minutes: 22 * Hour);
            p.AdvanceClock(60);
            Assert.That(p.Energy, Is.EqualTo(0));
            Assert.That(p.IsExhausted, Is.True);

            string before = JsonSerializer.Serialize(p.ToSaveData(), Json);
            Assert.That(new LifeActivityRunner().TryPerform(Activity(15, 0), p, "free", out var result), Is.False, "체력 0짜리도 못 한다");
            Assert.That(result.Kind, Is.EqualTo(LifeActivityResultKind.InsufficientResources));
            Assert.That(JsonSerializer.Serialize(p.ToSaveData(), Json), Is.EqualTo(before));
            Assert.That(StudentActivityRefusal.Explain(p, null, 0, 0), Is.EqualTo(StudentActivityRefusalReason.Exhausted));
        }

        [Test]
        public void SpendingTheLastEnergyInTheDay_IsAlsoExhausted()
        {
            var p = Progress(energy: 2);
            Assert.That(new LifeActivityRunner().TryPerform(Activity(60, 2), p, "last", out _), Is.True);
            Assert.That(p.Energy, Is.EqualTo(0));
            Assert.That(p.IsExhausted, Is.True);
            Assert.That(new LifeActivityRunner().TryPerform(Activity(15, 0), p, "free", out _), Is.False);
        }

        [Test]
        public void WithoutTimeRules_ThereIsNoNightLossAndNoExhaustion()
        {
            var p = new StudentLifeProgress("old", "p", 1, 10, 0, 22 * Hour);
            p.AdvanceClock(10 * Hour);
            Assert.That(p.Energy, Is.EqualTo(1));
            Assert.That(new LifeActivityRunner().TryPerform(Activity(15, 1), p, "a", out _), Is.True);
            Assert.That(p.IsExhausted, Is.False, "시간 규칙이 없으면 지침도 없다(옛 동작)");
        }

        // ---------- 잠 ----------

        [Test]
        public void SleepingBeforeBedtime_RestoresEverything_AndWakesAtSeven()
        {
            var p = Progress(energy: 3, minutes: 21 * Hour);
            Assert.That(p.TrySleep(Restore12(), out _), Is.True);
            Assert.That(p.Energy, Is.EqualTo(12));
            Assert.That(p.TryStartNextDay(), Is.True);
            Assert.That((p.CurrentDay, p.TimeMinutes), Is.EqualTo((2, 7 * Hour)));
        }

        [Test]
        public void SleepingLate_RestoresTwoLessPerHourLate()
        {
            var p = Progress(energy: 12, minutes: 22 * Hour);
            p.AdvanceClock(4 * Hour); // 02:00, 밤에 4 잃어 8
            Assert.That(p.Energy, Is.EqualTo(8));
            // 체력이 낮은 사람이 02:00에 자면 12 - 2*4 = 4까지만 돌아온다.
            var low = new StudentLifeProgress("flow", "p", 2, 10, 0, 26 * Hour);
            low.ConfigureTimeRules(Rules());
            Assert.That(low.TrySleep(Restore12(), out _), Is.True);
            Assert.That(low.Energy, Is.EqualTo(4));

            Assert.That(p.TrySleep(Restore12(), out _), Is.True);
            Assert.That(p.Energy, Is.EqualTo(8), "자서 체력이 줄지는 않는다(회복 목표 4보다 남은 8이 크다)");
        }

        [Test]
        public void SleepingClearsExhaustion()
        {
            var p = Progress(energy: 1, minutes: 22 * Hour);
            p.AdvanceClock(60);
            Assert.That(p.IsExhausted, Is.True);
            Assert.That(p.TrySleep(Restore12(), out _), Is.True);
            Assert.That(p.TryStartNextDay(), Is.True);
            Assert.That(p.IsExhausted, Is.False);
            Assert.That(p.Energy, Is.EqualTo(10), "23:00에 자면 12 - 2");
        }

        [Test]
        public void SleepingInTheDay_SkipsToSevenNextMorning_WithFullRest()
        {
            var p = Progress(energy: 4, minutes: 13 * Hour);
            Assert.That(p.TrySleep(Restore12(), out _), Is.True);
            Assert.That(p.TryStartNextDay(), Is.True);
            Assert.That((p.CurrentDay, p.TimeMinutes, p.Energy), Is.EqualTo((2, 7 * Hour, 12)));
        }

        [Test]
        public void StayingUpUntilSeven_TheDayEndsWithoutRest_AndStaysExhausted()
        {
            var p = Progress(energy: 6, minutes: 22 * Hour);
            Assert.That(p.HasDayRunOut, Is.False);
            p.AdvanceClock(9 * Hour);
            Assert.That(p.TimeMinutes, Is.EqualTo(31 * Hour));
            Assert.That(p.HasDayRunOut, Is.True);
            Assert.That(p.Energy, Is.EqualTo(0));
            Assert.That(p.IsExhausted, Is.True);
            Assert.That(new LifeActivityRunner().TryPerform(Activity(15, 0), p, "late", out _), Is.False, "날이 다 지난 뒤에는 시작하지 못한다");

            Assert.That(p.TryEndDayAwake(Restore12(), out _), Is.True);
            Assert.That(p.Energy, Is.EqualTo(0), "자지 않았으면 회복이 없다");
            Assert.That(p.Focus, Is.EqualTo(10), "집중도 돌아오지 않는다(그대로)");
            Assert.That(p.TryStartNextDay(), Is.True);
            Assert.That((p.CurrentDay, p.TimeMinutes), Is.EqualTo((2, 7 * Hour)));
            Assert.That(p.IsExhausted, Is.True, "자야만 풀린다");
            p.AdvanceClock(15 * Hour);
            Assert.That(p.Energy, Is.EqualTo(0));
        }

        [Test]
        public void AnActivityRunningPastSeven_CarriesTheRestIntoTheNextMorning()
        {
            var p = Progress(energy: 12, minutes: 30 * Hour + 30);
            Assert.That(new LifeActivityRunner().TryPerform(Activity(60, 0), p, "dawn", out _), Is.True);
            Assert.That(p.TimeMinutes, Is.EqualTo(31 * Hour + 30));
            Assert.That(p.HasDayRunOut, Is.True);
            Assert.That(p.TryEndDayAwake(Restore12(), out _), Is.True);
            Assert.That(p.TryStartNextDay(), Is.True);
            Assert.That(p.TimeMinutes, Is.EqualTo(7 * Hour + 30), "07:00을 넘긴 30분은 다음 날 아침에 이어진다");
        }

        // ---------- 활동과 시간 ----------

        [Test]
        public void AnActivity_SkipsTheClockAhead_ByItsMinutes_WhenAlone()
        {
            var p = Progress();
            Assert.That(new LifeActivityRunner().TryPerform(Activity(45, 1), p, "a", out _), Is.True);
            Assert.That(p.TimeMinutes, Is.EqualTo(7 * Hour + 45));
            Assert.That(p.BusyUntilMinutes, Is.EqualTo(7 * Hour + 45), "언제까지 바쁜가 — 혼자면 이미 그 시각이다");
            Assert.That(p.IsBusy, Is.False);
        }

        [Test]
        public void WhenTheClockCannotSkip_TheActivityKeepsYouBusyUntilItsEnd()
        {
            var p = Progress();
            p.ConfigureActivityClock(ActivityClockMode.StayBusy);
            Assert.That(new LifeActivityRunner().TryPerform(Activity(60, 1), p, "a", out _), Is.True);
            Assert.That(p.TimeMinutes, Is.EqualTo(7 * Hour), "시계는 건너뛰지 않는다");
            Assert.That(p.BusyUntilMinutes, Is.EqualTo(8 * Hour));
            Assert.That(p.IsBusy, Is.True);
            Assert.That(new LifeActivityRunner().TryPerform(Activity(15, 0), p, "b", out _), Is.False, "하는 중에는 다른 활동을 시작하지 못한다");
            Assert.That(StudentActivityRefusal.Explain(p, null, 0, 0), Is.EqualTo(StudentActivityRefusalReason.Busy), "하는 중이라 못 한다(문구 ui.prompt.busy)");
            Assert.That(StudentActivityRefusal.LocalizationKey(StudentActivityRefusalReason.Busy), Is.EqualTo("ui.prompt.busy"));
            p.AdvanceClock(60);
            Assert.That(p.IsBusy, Is.False);
            Assert.That(new LifeActivityRunner().TryPerform(Activity(15, 0), p, "c", out _), Is.True);
        }

        [Test]
        public void ThereIsNoDailyCountLimit_OnlyTimeAndEnergy()
        {
            var p = Progress(energy: 12);
            var policy = new ActivityTimePolicy("paid", 1);
            var runner = new LifeActivityRunner();
            for (int i = 0; i < 6; i++) Assert.That(runner.TryPerform(Activity(60, 2, policy), p, "a" + i, out _), Is.True, "평일 " + (i + 1) + "번째");
            Assert.That(p.Energy, Is.EqualTo(0));
            Assert.That(runner.TryPerform(Activity(60, 0, policy), p, "seventh", out _), Is.False, "체력이 먼저 막는다");
        }

        // ---------- 세이브 ----------

        [Test]
        public void ClockState_RoundTripsThroughItsOwnSaveShape()
        {
            var p = Progress(energy: 1, minutes: 22 * Hour);
            p.ConfigureActivityClock(ActivityClockMode.StayBusy);
            p.AdvanceClock(70);
            new LifeActivityRunner().TryPerform(Activity(30, 0), p, "x", out _); // 지쳐서 거절된다
            Assert.That(p.IsExhausted, Is.True);

            var clockJson = JsonSerializer.Serialize(p.ToClockSaveData(), Json);
            var studentJson = JsonSerializer.Serialize(p.ToSaveData(), Json);
            var again = StudentLifeProgress.FromSaveData(JsonSerializer.Deserialize<StudentLifeProgressSaveData>(studentJson, Json));
            again.ConfigureTimeRules(Rules());
            again.RestoreClock(JsonSerializer.Deserialize<StudentClockSaveData>(clockJson, Json));
            Assert.That(again.IsExhausted, Is.True);
            again.AdvanceClock(30);
            Assert.That(again.Energy, Is.EqualTo(0));
            Assert.That(again.TimeMinutes, Is.EqualTo(23 * Hour + 40));
            Assert.That(JsonSerializer.Serialize(again.ToClockSaveData(), Json), Does.Contain("\"Version\":" + StudentClockSaveData.KnownVersion));
        }

        [Test]
        public void ClockSave_FromAnotherDay_IsIgnored_AndNightLossIsNotChargedTwiceAfterLoading()
        {
            var p = Progress(energy: 6, minutes: 22 * Hour);
            p.AdvanceClock(2 * Hour); // 00:00, 2 잃음
            var saved = p.ToClockSaveData();
            Assert.That(p.Energy, Is.EqualTo(4));

            var again = StudentLifeProgress.FromSaveData(p.ToSaveData());
            again.ConfigureTimeRules(Rules());
            again.RestoreClock(saved);
            again.AdvanceClock(1);
            Assert.That(again.Energy, Is.EqualTo(4), "읽은 뒤 이미 깎은 밤 시간을 다시 깎지 않는다");

            var stale = new StudentClockSaveData { Day = 99, Exhausted = true, NightHoursCharged = 0 };
            var other = StudentLifeProgress.FromSaveData(p.ToSaveData());
            other.ConfigureTimeRules(Rules());
            other.RestoreClock(stale);
            Assert.That(other.IsExhausted, Is.False, "다른 날의 시계 상태는 버린다");
        }

        [Test]
        public void OldSaveWithoutClockFile_ContinuesFromItsSavedTime_AndCountsNightFromThere()
        {
            var dto = JsonSerializer.Deserialize<StudentLifeProgressSaveData>(
                "{\"SaveSlot\":\"old\",\"PlayerId\":\"p\",\"CurrentDay\":3,\"Energy\":7,\"Focus\":5,\"TimeMinutes\":1410,\"TimePointsSpentToday\":2}", Json);
            var p = StudentLifeProgress.FromSaveData(dto);
            p.ConfigureTimeRules(Rules());
            p.RestoreClock(null);
            Assert.That(p.TimeMinutes, Is.EqualTo(1410));
            Assert.That(p.TimePointsSpentToday, Is.EqualTo(2), "옛 필드는 그대로 읽는다");
            Assert.That(p.IsExhausted, Is.False);
            p.AdvanceClock(1);
            Assert.That(p.Energy, Is.EqualTo(7), "옛 세이브에서 이미 지난 밤 시간(23:30까지 1시간)은 거슬러 깎지 않는다");
            p.AdvanceClock(30);
            Assert.That(p.Energy, Is.EqualTo(6), "00:01 — 그 뒤로 채운 시간부터 센다");
        }
    }
}
