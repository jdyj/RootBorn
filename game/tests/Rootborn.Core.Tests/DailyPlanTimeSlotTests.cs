using NUnit.Framework;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Tests
{
    // 흐르는 시간(설계 4절): 일과 계획판의 칸을 시간 포인트가 아니라 하루의 시간대(TimeSlotDefinition id)에 묶는다. 칸 수·문구 키(화면)는 그대로다.
    public sealed class DailyPlanTimeSlotTests
    {
        private static StudentLifeProgress Student(int day)
        {
            var p = new StudentLifeProgress("slot", "player", 12, 10);
            p.ConfigureCalendar(new LifeCalendar(2, 3));
            for (int i = 1; i < day; i++)
            {
                Assert.That(p.TryEndDay(null, out _), Is.True);
                Assert.That(p.TryStartNextDay(), Is.True);
            }

            p.DailyPlan.SynchronizeDay(p);
            return p;
        }

        [Test]
        public void WeekdaySlots_AreAfternoonAndEvening()
        {
            var plan = Student(1).DailyPlan;
            Assert.That(plan.SlotCount, Is.EqualTo(2), "칸 수는 그대로(화면 구성 유지)");
            Assert.That(plan.Slots[0].TimeSlotId, Is.EqualTo(LocationStateTimeSlots.Afternoon), "방과 후");
            Assert.That(plan.Slots[1].TimeSlotId, Is.EqualTo(LocationStateTimeSlots.Evening));
        }

        [Test]
        public void WeekendSlots_AreMorningAfternoonEvening()
        {
            var plan = Student(6).DailyPlan;
            Assert.That(plan.SlotCount, Is.EqualTo(3));
            Assert.That(new[] { plan.Slots[0].TimeSlotId, plan.Slots[1].TimeSlotId, plan.Slots[2].TimeSlotId },
                Is.EqualTo(new[] { LocationStateTimeSlots.Morning, LocationStateTimeSlots.Afternoon, LocationStateTimeSlots.Evening }));
        }

        [TestCase(7 * 60, -1, "평일 오전에는 칸이 없다")]
        [TestCase(13 * 60, 0, "")]
        [TestCase(18 * 60, 1, "")]
        [TestCase(23 * 60, -1, "밤에는 칸이 없다")]
        [TestCase(26 * 60, -1, "자정 넘은 새벽(26:00)도 밤")]
        public void TheSlotForTheTime_FollowsTheTimeSlots(int minutes, int expected, string why)
        {
            Assert.That(Student(1).DailyPlan.SlotIndexAt(minutes), Is.EqualTo(expected), why);
        }

        [Test]
        public void ASavedPlan_KeepsItsTimeSlots_FromThePeriodKey()
        {
            var p = Student(6);
            var copy = DailyPlanProgress.FromSaveData(p.DailyPlan.ToSaveData(), "slot", "player", p.CurrentDay);
            Assert.That(copy.Slots[0].TimeSlotId, Is.EqualTo(LocationStateTimeSlots.Morning), "세이브 모양은 그대로(문구 키에서 시간대를 얻는다)");
        }
    }
}
