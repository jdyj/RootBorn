// 옮겨 옴: Assets/Scripts/UI/Quests/QuestChainRuntimeService.cs(ApplyRegisteredEventToSession, CreditPlanChosenBeforeStart)의 적용 순서.
// Unity 판에서는 PlayMode E2E(MainChainOpeningE2EScenarioTests 001)로만 확인하던 것을 규칙 단위로 고정한다.
using NUnit.Framework;
using Rootborn.Core.Quests;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Tests
{
    public sealed class QuestChainEventsTests
    {
        private static readonly DailyPlanRoute Library = new DailyPlanRoute("daily.library", "daily.library", "location.library", "SelfStudy",
            null, null, null, null, null, "t", "d", "n");
        private static readonly DailyPlanRouteSet[] Routes = { new DailyPlanRouteSet("daily.default", null, new[] { Library }, 1) };

        // 시작 NPC가 엄마인 체인: 첫 단계는 일과 고르기와 선생님과 대화.
        private static QuestChainDefinition Opening() => new QuestChainDefinition("chain.opening", "n", "d", "", new[]
        {
            new QuestStepDefinition("step.school", "n", "d", new QuestObjective[]
            {
                new DailyPlanSelectedQuestObjective("plan", 1),
                new TalkQuestObjective("teacher", 1, "npc.teacher"),
            }),
        }, startNpcId: "npc.mom");

        private static StudentLifeProgress Student() => new StudentLifeProgress("slot", "player", 100, 100);

        private static QuestRuntimeContext Context(StudentLifeProgress student) => new QuestRuntimeContext(null, null, null, null, student);

        [Test]
        public void TalkingToTheStartNpc_AcceptsAndTracksTheChain()
        {
            var chain = Opening();
            var log = new QuestChainLog(new[] { chain });

            Assert.That(QuestChainEvents.Apply(log, new[] { chain }, Routes, new QuestEvent(QuestEventKind.Talk, "talk-1", "npc.mom"), Context(Student()), default), Is.True);

            Assert.That(log.GetState(chain), Is.EqualTo(QuestChainState.Tracked));
            Assert.That(log.GetCurrentStep(chain).Id, Is.EqualTo("step.school"));
        }

        [Test]
        public void TalkingToSomeoneElse_DoesNotStartTheChain()
        {
            var chain = Opening();
            var log = new QuestChainLog(new[] { chain });

            QuestChainEvents.Apply(log, new[] { chain }, Routes, new QuestEvent(QuestEventKind.Talk, "talk-1", "npc.teacher"), Context(Student()), default);

            Assert.That(log.GetState(chain), Is.EqualTo(QuestChainState.Available));
        }

        [Test]
        public void APlanChosenBeforeTheChainStarts_IsCountedWhenItStarts()
        {
            var chain = Opening();
            var log = new QuestChainLog(new[] { chain });
            var student = Student();
            Assert.That(new DailyPlanRouteRunner().TrySelectRoute(student.DailyPlan, Library, student), Is.True);

            QuestChainEvents.Apply(log, new[] { chain }, Routes, new QuestEvent(QuestEventKind.Talk, "talk-1", "npc.mom"), Context(student), default);

            Assert.That(log.GetObjectiveProgress(chain, 0), Is.EqualTo(1), "일과");
            Assert.That(log.GetObjectiveProgress(chain, 1), Is.EqualTo(0), "선생님");
        }

        [Test]
        public void APlanWhoseRouteIsNotInTheRouteSets_IsNotCounted()
        {
            var chain = Opening();
            var log = new QuestChainLog(new[] { chain });
            var student = Student();
            new DailyPlanRouteRunner().TrySelectRoute(student.DailyPlan, Library, student);

            QuestChainEvents.Apply(log, new[] { chain }, new DailyPlanRouteSet[0], new QuestEvent(QuestEventKind.Talk, "talk-1", "npc.mom"), Context(student), default);

            Assert.That(log.GetState(chain), Is.EqualTo(QuestChainState.Tracked));
            Assert.That(log.GetObjectiveProgress(chain, 0), Is.EqualTo(0));
            Assert.That(QuestChainEvents.CreditPlanChosenBeforeStart(log, Routes, Context(null), default), Is.False, "학생 진행이 없으면");
        }

        [Test]
        public void AnEventIsRecordedAndEveryChainIsEvaluated()
        {
            var chain = Opening();
            var log = new QuestChainLog(new[] { chain });
            var chains = new[] { chain };
            QuestChainEvents.Apply(log, chains, Routes, new QuestEvent(QuestEventKind.Talk, "talk-1", "npc.mom"), Context(Student()), default);

            QuestChainEvents.Apply(log, chains, Routes, new QuestEvent(QuestEventKind.DailyPlanSelected, "plan-1", "daily.library"), Context(Student()), default);
            QuestChainEvents.Apply(log, chains, Routes, new QuestEvent(QuestEventKind.Talk, "talk-2", "npc.teacher"), Context(Student()), default);

            Assert.That(log.GetState(chain), Is.EqualTo(QuestChainState.Completed));
        }

        [Test]
        public void EventsWithoutAKeyOrACount_ChangeNothing()
        {
            var chain = Opening();
            var log = new QuestChainLog(new[] { chain });
            var before = log.ToSaveData().Chains[0].ProcessedEventKeys.Length;

            Assert.That(QuestChainEvents.Apply(log, new[] { chain }, Routes, new QuestEvent(QuestEventKind.Talk, "", "npc.mom"), Context(Student()), default), Is.False);
            Assert.That(QuestChainEvents.Apply(log, new[] { chain }, Routes, new QuestEvent(QuestEventKind.Talk, "talk-0", "npc.mom", 0), Context(Student()), default), Is.False);
            Assert.That(QuestChainEvents.Apply(null, new[] { chain }, Routes, new QuestEvent(QuestEventKind.Talk, "talk-1", "npc.mom"), Context(Student()), default), Is.False);

            Assert.That(log.GetState(chain), Is.EqualTo(QuestChainState.Available));
            Assert.That(log.ToSaveData().Chains[0].ProcessedEventKeys.Length, Is.EqualTo(before));
        }

        [Test]
        public void AnAlreadyStartedChain_IsNotAcceptedAgain()
        {
            var chain = Opening();
            var log = new QuestChainLog(new[] { chain });
            var chains = new[] { chain };
            QuestChainEvents.Apply(log, chains, Routes, new QuestEvent(QuestEventKind.Talk, "talk-1", "npc.mom"), Context(Student()), default);
            QuestChainEvents.Apply(log, chains, Routes, new QuestEvent(QuestEventKind.DailyPlanSelected, "plan-1", "daily.library"), Context(Student()), default);

            QuestChainEvents.Apply(log, chains, Routes, new QuestEvent(QuestEventKind.Talk, "talk-2", "npc.mom"), Context(Student()), default);

            Assert.That(log.GetObjectiveProgress(chain, 0), Is.EqualTo(1), "다시 받아 진행이 지워지지 않는다");
        }
    }
}
