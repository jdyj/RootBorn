using NUnit.Framework;
using Rootborn.Core.Minigames;
using Rootborn.Core.Quests;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Tests
{
    // 사용자 결정(2026-11-11)으로 Unity 판과 달라진 동작. 성인 직업 쪽은 AdultCareerTests에 있다.
    public sealed class UserDecisions20261111Tests
    {
        private static QuestDefinition Quest(string id, QuestCondition[] provider, QuestCondition[] prerequisites)
            => new QuestDefinition(id, id, id, new QuestObjective[] { new TalkQuestObjective("talk", 1, "npc") }, null, null, provider, prerequisites);

        private static StudentLifeProgress StudentOnDay(int day)
        {
            var student = new StudentLifeProgress("slot", "player", 10, 10);
            for (int i = 1; i < day; i++) { student.TryEndDay(null, out _); student.TryStartNextDay(); }
            return student;
        }

        [Test]
        public void ChangedBehavior_QuestIsAcceptedOnlyWhenItsPrerequisitesHold()
        {
            var quest = Quest("q", null, new QuestCondition[] { new DayAtLeastCondition(3) });
            var log = new QuestLog(null);
            log.AddQuest(quest);

            Assert.That(log.Accept(quest, new QuestRuntimeContext(log, quest, null, null, StudentOnDay(2))), Is.False);
            Assert.That(log.GetState(quest), Is.EqualTo(QuestStage.NotStarted));
            Assert.That(log.Accept(quest), Is.False, "조건이 있는 퀘스트는 판정할 문맥 없이 받을 수 없다.");
            Assert.That(log.Accept(quest, new QuestRuntimeContext(log, quest, null, null, StudentOnDay(3))), Is.True);
        }

        [Test]
        public void ChangedBehavior_QuestIsAcceptedOnlyWhenItsProviderConditionsHold()
        {
            var quest = Quest("q", new QuestCondition[] { new DayAtLeastCondition(2) }, null);
            var log = new QuestLog(null);
            log.AddQuest(quest);

            Assert.That(log.Accept(quest, new QuestRuntimeContext(log, quest, null, null, StudentOnDay(1))), Is.False);
            Assert.That(log.Accept(quest, new QuestRuntimeContext(log, quest, null, null, StudentOnDay(2))), Is.True);
        }

        [Test]
        public void QuestWithoutConditions_IsStillAcceptedWithoutContext()
        {
            var quest = Quest("q", null, null);
            var log = new QuestLog(null);
            log.AddQuest(quest);
            Assert.That(log.Accept(quest), Is.True);
        }

        [Test]
        public void ChangedBehavior_FocusRewardStopsAtTheDailyFocus()
        {
            var student = new StudentLifeProgress("slot", "player", 10, 9);
            student.ConfigureDailyFocus(10);
            new CareerInterestFocusReward("r", 3).Apply(new CareerInterestEvaluationContext(null, null, null, student, 1));
            Assert.That(student.Focus, Is.EqualTo(10));

            student.RestoreFocusForDay(12);
            new CareerInterestFocusReward("r2", 1).Apply(new CareerInterestEvaluationContext(null, null, null, student, 1));
            Assert.That(student.Focus, Is.EqualTo(12), "이미 넘어 있는 집중을 깎지는 않는다.");
        }

        [Test]
        public void FocusReward_WithoutAConfiguredDailyFocus_IsNotCapped()
        {
            var student = new StudentLifeProgress("slot", "player", 10, 9);
            new CareerInterestFocusReward("r", 3).Apply(new CareerInterestEvaluationContext(null, null, null, student, 1));
            Assert.That(student.Focus, Is.EqualTo(12));
        }

        [Test]
        public void DayEndRules_TellTheDailyFocus()
        {
            Assert.That(new DayEndRuleSet("d", null, new IDayEndRule[] { new EnergyRestoreRule(12), new FocusRestoreRule(10) }).DailyFocus, Is.EqualTo(10));
            Assert.That(new DayEndRuleSet("d", null, new IDayEndRule[] { new EnergyRestoreRule(12) }).DailyFocus, Is.Zero);
        }

        [Test]
        public void ChangedBehavior_ChoiceChallengeFailsWhenMistakesPassTheAllowance()
        {
            var challenge = new ChoiceChallenge("c", new[] { 0, 0, 0, 0 }, new[] { "p", "p", "p", "p" }, new[] { "a", "b" }, 30f, 1);
            var session = challenge.Create(default, 1);

            Assert.That(session.Choose(1), Is.True);
            Assert.That(session.IsComplete, Is.False, "허용한 실수 안에서는 계속한다.");
            Assert.That(session.Choose(1), Is.True);

            Assert.That(session.IsComplete, Is.True);
            Assert.That(session.Score, Is.Zero);
        }

        [Test]
        public void ChangedBehavior_EarlyHoursAreMorning_FromTheDayStart()
        {
            Assert.That(LocationStateTimeSlots.FromMinutes(7 * 60), Is.EqualTo(LocationStateTimeSlots.Morning));
            // 흐르는 시간(2026-11-12): 하루 시작(07:00) 전의 새벽은 밤이다(아래 NightBeforeTheDayStart).
            Assert.That(LocationStateTimeSlots.FromMinutes(1), Is.EqualTo(LocationStateTimeSlots.Night));
            Assert.That(LocationStateTimeSlots.FromMinutes(8 * 60), Is.EqualTo(LocationStateTimeSlots.Morning));
            Assert.That(LocationStateTimeSlots.FromMinutes(11 * 60 + 59), Is.EqualTo(LocationStateTimeSlots.Morning));
            Assert.That(LocationStateTimeSlots.FromMinutes(12 * 60), Is.EqualTo(LocationStateTimeSlots.Afternoon));
        }

        [Test]
        public void ChangedBehavior_NightBeforeTheDayStart_IsNight_EvenPastMidnightOfTheSameDay()
        {
            // 흐르는 시각은 하루 시작(07:00) 전에도 흐른다. 그 새벽(00:00~06:59)을 아침으로 보면 마을이 아침처럼 밝고 NPC가 아침 자리에 선다.
            Assert.That(LocationStateTimeSlots.FromMinutes(0), Is.EqualTo(LocationStateTimeSlots.Night));
            Assert.That(LocationStateTimeSlots.FromMinutes(6 * 60 + 59), Is.EqualTo(LocationStateTimeSlots.Night));
            Assert.That(LocationStateTimeSlots.FromMinutes(26 * 60), Is.EqualTo(LocationStateTimeSlots.Night), "같은 날의 26:00(= 02:00)");
            Assert.That(LocationStateTimeSlots.FromMinutes(31 * 60), Is.EqualTo(LocationStateTimeSlots.Morning), "31:00 = 다음 날 07:00");
            Assert.That(LocationStateTimeSlots.FromMinutes(5 * 60, dayStartMinutes: 5 * 60), Is.EqualTo(LocationStateTimeSlots.Morning), "하루 시작은 시간 규칙에서 온다");
            Assert.That(LocationStateTimeSlots.FromMinutes(5 * 60), Is.EqualTo(LocationStateTimeSlots.Night));

            var rules = new Rootborn.Core.Time.TimeRules(600f, 6 * 60, 22 * 60, 1, 2);
            var student = new StudentLifeProgress("slot", "player", 10, 10, 0, 6 * 60 + 30);
            Assert.That(LocationStateTimeSlots.FromProgress(student), Is.EqualTo(LocationStateTimeSlots.Night), "규칙이 없으면 기본 하루 시작(07:00)");
            student.ConfigureTimeRules(rules);
            Assert.That(LocationStateTimeSlots.FromProgress(student), Is.EqualTo(LocationStateTimeSlots.Morning), "진행의 시간 규칙의 하루 시작(06:00)");
        }

        [Test]
        public void ChangedBehavior_UnsettledStudentMinigameIsForfeitedWhenTheNextDayStarts()
        {
            var student = new StudentLifeProgress("slot", "player", 10, 10);
            student.ReserveMinigame(new PendingStudentMinigameSaveData { Kind = StudentMinigameKind.Practice, RequestId = "attempt-1", DefinitionId = "practice", MinigameId = "game", Day = 1, ReservedTimePoints = 1, Seed = 7 });

            student.TryEndDay(null, out _);
            student.TryStartNextDay();

            Assert.That(student.HasPendingMinigame, Is.False);
            Assert.That(student.HasAppliedRequest("attempt-1"), Is.True, "포기한 요청은 쓴 것으로 남아 다시 정산하지 못한다.");
        }

        private static DailyPlanRoute PlanRoute(string id) => new DailyPlanRoute(id, id, null, null, null, null, null, null, null, null, null, null);

        private static StudentLifeProgress StudentWithThreeSlots()
        {
            var student = new StudentLifeProgress("slot", "player", 10, 10);
            student.ConfigureCalendar(new LifeCalendar(3, 3));
            student.DailyPlan.SynchronizeDay(student);
            return student;
        }

        [Test]
        public void ChangedBehavior_TheSameRouteFillsOnlyAsManySlotsADayAsItsSetAllows()
        {
            var once = PlanRoute("route.once");
            var other = PlanRoute("route.other");
            new DailyPlanRouteSet("set", null, new[] { once, other }, 1);
            var student = StudentWithThreeSlots();

            Assert.That(student.DailyPlan.TryAssignSlot(0, once, student), Is.True);
            Assert.That(student.DailyPlan.TryAssignSlot(1, once, student), Is.False, "같은 길은 하루 한 번까지.");
            Assert.That(student.DailyPlan.Slots[1].RouteId, Is.Empty);
            Assert.That(student.DailyPlan.TryAssignSlot(1, other, student), Is.True);
            Assert.That(student.DailyPlan.TryAssignSlot(0, other, student), Is.False);
        }

        [Test]
        public void TheDailyLimitOfARoute_ComesFromData()
        {
            var twice = PlanRoute("route.twice");
            var set = new DailyPlanRouteSet("set", null, new[] { twice }, 2);
            var student = StudentWithThreeSlots();

            Assert.That(set.MaxSelectionsPerDay, Is.EqualTo(2));
            Assert.That(student.DailyPlan.TryAssignSlot(0, twice, student), Is.True);
            Assert.That(student.DailyPlan.TryAssignSlot(1, twice, student), Is.True);
            Assert.That(student.DailyPlan.TryAssignSlot(2, twice, student), Is.False);
        }

        [Test]
        public void ARouteOutsideAnySet_HasNoDailyLimit()
        {
            var free = PlanRoute("route.free");
            var student = StudentWithThreeSlots();
            for (int i = 0; i < 3; i++) Assert.That(student.DailyPlan.TryAssignSlot(i, free, student), Is.True);
        }
    }
}
