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
        public void ChangedBehavior_EarlyHoursAreMorning()
        {
            Assert.That(LocationStateTimeSlots.FromMinutes(7 * 60), Is.EqualTo(LocationStateTimeSlots.Morning));
            Assert.That(LocationStateTimeSlots.FromMinutes(1), Is.EqualTo(LocationStateTimeSlots.Morning));
            Assert.That(LocationStateTimeSlots.FromMinutes(8 * 60), Is.EqualTo(LocationStateTimeSlots.Morning));
            Assert.That(LocationStateTimeSlots.FromMinutes(11 * 60 + 59), Is.EqualTo(LocationStateTimeSlots.Morning));
            Assert.That(LocationStateTimeSlots.FromMinutes(12 * 60), Is.EqualTo(LocationStateTimeSlots.Afternoon));
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
    }
}
