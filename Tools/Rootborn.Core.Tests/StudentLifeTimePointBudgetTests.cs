using System;
using System.Text.Json;
using NUnit.Framework;
using Rootborn.Core.Inventory;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Tests
{
    // 옮겨 옴: Assets/Tests/EditMode/StudentLife/StudentLifeTimePointTests.cs
    // 흐르는 시간(2026-11-12)으로 바뀜: 하루 시간 포인트는 한도가 아니다. 하루에 할 수 있는 양은 깨어 있는 시간과 체력이 정하고,
    // "오늘은 더 못 한다"는 지침(체력 0)이 맡는다. 같이 지키는 것: 거절되면 무변경, 같은 요청은 한 번, 하루 한 번은 저장·다시 읽기로 다시 채울 수 없다.
    // 이미 있는 것(달력 평일 2·주말 3, 하루 한 번 정책, 달력 없으면 예산 0)은 StudentLifeActivityTests에 있다.
    public sealed class StudentLifeTimePointBudgetTests
    {
        private const int AllDays = 127, Weekdays = 31;
        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true };

        private static StudentLifeProgress Progress()
        {
            var p = new StudentLifeProgress("tp-test", "p", 100, 100);
            p.ConfigureCalendar(new LifeCalendar(2, 3));
            return p;
        }

        // 세이브 모양으로 되돌린 뒤 다시 만든다. 달력은 세이브에 없으므로 다시 묶는다(GameSession.Load와 같다).
        private static StudentLifeProgress Restore(StudentLifeProgress p)
        {
            var copy = JsonSerializer.Deserialize<StudentLifeProgressSaveData>(JsonSerializer.Serialize(p.ToSaveData(), Json), Json);
            var restored = StudentLifeProgress.FromSaveData(copy);
            restored.ConfigureCalendar(new LifeCalendar(2, 3));
            return restored;
        }

        private static string Saved(StudentLifeProgress p) => JsonSerializer.Serialize(p.ToSaveData(), Json);

        private static void NextDay(StudentLifeProgress p)
        {
            Assert.That(p.TryEndDay(null, out _), Is.True);
            Assert.That(p.TryStartNextDay(), Is.True);
        }

        private static ActivityTimePolicy Policy(string id, int cost, int mask, bool once) => new ActivityTimePolicy(id, cost, mask, once);

        private static LifeActivity Activity(ActivityTimePolicy policy)
            => new LifeActivity(Guid.NewGuid().ToString("N"), LifeActivityCategory.SelfStudy, policy, 30, 1, 1, 0, null, null, null);

        [Test]
        public void Weekday_ThirdPaidActionIsAllowed_ButExhaustionRejectsWithoutChangingAnyProgress()
        {
            var p = Progress();
            var action = Activity(Policy("paid", 1, AllDays, false));
            var runner = new LifeActivityRunner();
            Assert.That(runner.TryPerform(action, p, "a", out _), Is.True);
            Assert.That(runner.TryPerform(action, p, "b", out _), Is.True);
            Assert.That(runner.TryPerform(action, p, "c", out _), Is.True, "평일 세 번째도 된다(시간 포인트 한도 없음)");
            FlowFixtures.Exhaust(p);
            string before = Saved(p);

            Assert.That(runner.TryPerform(action, p, "d", out var refused), Is.False);
            Assert.That(refused.Kind, Is.EqualTo(LifeActivityResultKind.InsufficientResources));
            Assert.That(Saved(p), Is.EqualTo(before));
        }

        [Test]
        public void DuplicateRequest_DoesNotSpendAnotherTimePoint()
        {
            var p = Progress();
            var action = Activity(Policy("paid", 1, AllDays, false));
            var runner = new LifeActivityRunner();
            Assert.That(runner.TryPerform(action, p, "a", out _), Is.True);
            Assert.That(runner.TryPerform(action, p, "a", out var again), Is.False);
            Assert.That(again.Kind, Is.EqualTo(LifeActivityResultKind.DuplicateRequest));
            Assert.That(p.TimePointsRemaining, Is.EqualTo(1));
        }

        [Test]
        public void SchoolPolicy_IsFreeOncePerWeekday_AndDoesNotGateOutsideGrowth()
        {
            var p = Progress();
            var school = Activity(Policy("morning", 0, Weekdays, true));
            var outside = Activity(Policy("paid", 1, AllDays, false));
            var runner = new LifeActivityRunner();

            Assert.That(runner.TryPerform(outside, p, "outside-first", out _), Is.True, "학교에 가지 않아도 학교 밖 활동을 할 수 있다");
            Assert.That(runner.TryPerform(school, p, "school", out _), Is.True);
            Assert.That(p.TimePointsRemaining, Is.EqualTo(1), "학교는 시간 포인트를 쓰지 않는다");
            Assert.That(runner.TryPerform(school, p, "school-again", out _), Is.False, "학교는 하루 한 번");

            var restored = Restore(p);
            Assert.That(runner.TryPerform(school, restored, "school-after-load", out _), Is.False, "저장·다시 읽기로 하루 한 번을 다시 채우지 않는다");
        }

        [Test]
        public void Weekend_DisallowsWeekdaySchool_AndHasNoActionCount()
        {
            var p = Progress();
            for (int i = 0; i < 5; i++) NextDay(p);
            Assert.That(p.CurrentDay, Is.EqualTo(6));
            var school = Activity(Policy("morning", 0, Weekdays, true));
            var paid = Activity(Policy("paid", 1, AllDays, false));
            var runner = new LifeActivityRunner();

            Assert.That(runner.TryPerform(school, p, "school", out _), Is.False);
            for (int i = 0; i < 3; i++) Assert.That(runner.TryPerform(paid, p, "work-" + i, out _), Is.True);
            Assert.That(runner.TryPerform(paid, p, "fourth", out _), Is.True, "주말 네 번째도 된다(시간 포인트 한도 없음)");
        }

        [Test]
        public void SaveRoundTrip_AndCalendarRebind_DoNotRefillBudget_NextDayDoes()
        {
            var p = Progress();
            Assert.That(p.TrySpendTimePoints(1), Is.True);

            var restored = Restore(p);
            restored.ConfigureCalendar(new LifeCalendar(2, 3));
            Assert.That(restored.TimePointsRemaining, Is.EqualTo(1));

            NextDay(restored);
            Assert.That(restored.TimePointsRemaining, Is.EqualTo(2));
            Assert.That(restored.TrySpendTimePoints(-1), Is.False, "음수로 예산을 늘리지 않는다");
            Assert.That(restored.TimePointsRemaining, Is.EqualTo(2));
        }

        [Test]
        public void SettledDay_RejectsPaidAndFreeActivitiesUntilNextDay()
        {
            var p = Progress();
            Assert.That(p.TryEndDay(null, out _), Is.True);
            string before = Saved(p);
            var runner = new LifeActivityRunner();

            Assert.That(runner.TryPerform(Activity(Policy("paid", 1, AllDays, false)), p, "paid", out _), Is.False);
            Assert.That(runner.TryPerform(Activity(Policy("free", 0, AllDays, false)), p, "free", out _), Is.False);
            Assert.That(runner.TryPerform(Activity(null), p, "no-policy", out _), Is.False, "정책이 없는 활동도 정산한 날에는 안 된다");
            Assert.That(Saved(p), Is.EqualTo(before));
        }

        [Test]
        public void LegacySaveWithoutTimePointFields_StartsWithAvailableBudget()
        {
            var dto = JsonSerializer.Deserialize<StudentLifeProgressSaveData>(
                "{\"SaveSlot\":\"old\",\"PlayerId\":\"p\",\"CurrentDay\":6,\"Energy\":20,\"Focus\":20}", Json);
            var p = StudentLifeProgress.FromSaveData(dto);
            p.ConfigureCalendar(new LifeCalendar(2, 3));
            Assert.That(p.TimePointsRemaining, Is.EqualTo(3));
            Assert.That(p.DayState, Is.EqualTo(StudentDayState.InProgress));
        }

        [Test]
        public void CostlyActionWithoutCalendar_IsNotBlockedByABudget_ButASettledDayStillIs()
        {
            var p = new StudentLifeProgress("test", "p", 100, 100);
            Assert.That(p.TrySpendTimePoints(1), Is.True, "달력이 없어도 예산으로 막지 않는다");
            Assert.That(p.TryEndDay(null, out _), Is.True);
            Assert.That(p.TrySpendTimePoints(1), Is.False, "정산한 날에는 닫혀 있다");
        }

        // Unity EveryPaidActivityEntryPoint_UsesTheSharedBudget을 바꿈: 진입점마다 세 번째도 되고, 지치면 무변경으로 거절된다.
        [TestCase("location")]
        [TestCase("outside")]
        [TestCase("help")]
        [TestCase("work")]
        public void EveryPaidActivityEntryPoint_IsLimitedByExhaustion_NotByACount(string entryPoint)
        {
            var p = Progress();
            var perform = EntryPoint(entryPoint, Policy("shared", 1, AllDays, false));
            for (int i = 0; i < 3; i++) Assert.That(perform(p, "step-" + i), Is.True, entryPoint + " " + i);
            Assert.That(p.TimePointsSpentToday, Is.EqualTo(3), "방에 알릴 몫은 센다");
            FlowFixtures.Exhaust(p);
            string before = Saved(p);
            Assert.That(perform(p, "after-exhausted"), Is.False, entryPoint);
            Assert.That(Saved(p), Is.EqualTo(before));
        }

        [Test]
        public void DifferentEntryPoints_ShareOneBody()
        {
            var p = Progress();
            var policy = Policy("shared", 1, AllDays, false);
            Assert.That(EntryPoint("location", policy)(p, "location"), Is.True);
            Assert.That(EntryPoint("work", policy)(p, "work"), Is.True);
            FlowFixtures.Exhaust(p);
            string before = Saved(p);
            Assert.That(EntryPoint("help", policy)(p, "help"), Is.False, "지치면 어느 진입점도 시작하지 못한다");
            Assert.That(EntryPoint("outside", policy)(p, "outside"), Is.False);
            Assert.That(new LifeActivityRunner().TryPerform(Activity(policy), p, "life", out _), Is.False);
            Assert.That(Saved(p), Is.EqualTo(before));
        }

        private static Func<StudentLifeProgress, string, bool> EntryPoint(string kind, ActivityTimePolicy policy)
        {
            switch (kind)
            {
                case "location":
                    var location = new LocationActivity("activity.location", "", "", "location.fixture", null, policy, 30, 0, 0, 0, null, null);
                    return (p, request) => new LocationActivityRunner().TryPerform(location, p, request, out _);
                case "outside":
                    var outside = new OutsideSchoolActivity("activity.outside", "", "category.fixture", policy, 30, 0, 0, 0, null, null);
                    return (p, request) => new OutsideSchoolActivityRunner().TryPerform(outside, p, request, out _);
                case "help":
                    var help = new TownHelpAction("help.fixture", "", "npc.fixture", "location.fixture", policy, 30, 0, 0, 0, null, null);
                    return (p, request) => new TownHelpActionRunner().TryPerform(help, p, request, out _);
                default:
                    var work = new PartTimeWork("work.fixture", "", null, null, policy, 30, 0, 0, 0, null, null, null);
                    var inventory = new ItemInventory();
                    return (p, request) => new PartTimeWorkRunner().TryPerform(work, p, inventory, request, out _);
            }
        }
    }
}
