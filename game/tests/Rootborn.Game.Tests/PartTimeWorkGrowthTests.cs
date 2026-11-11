// 원래 파일: Assets/Tests/PlayMode/EndToEnd/PartTimeWorkGrowthE2EScenarioTests.cs (JOB_001, JOB_002)
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.StudentLife;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 마을 알바를 미니게임으로 예약하고 통과로 정산하면 성장이 한 번, 오늘 기록이 한 번 남고, 진로와 이어진 알바는 그 진로 후보 이해도를
    /// 힌트만큼 한 번 올린다. 명령마다 새 GameSession으로 다시 읽는다. 자동 테스트다.
    /// </summary>
    public sealed class PartTimeWorkGrowthTests
    {
        private static readonly string[] Works =
        {
            "work.shift.corner-store", "work.workshop-helper", "work.restaurant-helper", "work.studio-helper", "work.supply-helper",
            "work.training-helper", "work.clinic-helper", "work.fitness-helper", "work.errand-helper",
        };

        private static WorkTraitDeltaOutcome TraitOutcome(PartTimeWork work)
        {
            var outcome = work.Outcomes.OfType<WorkTraitDeltaOutcome>().FirstOrDefault();
            Assert.That(outcome, Is.Not.Null, work.Id + "에 볼 수 있는 특성 결과가 없다");
            return outcome;
        }

        [Test]
        public void Job001_CornerStoreShift_GrowsOnce_LogsOnce_AndSurvivesReading()
        {
            const string workId = "work.shift.corner-store";
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            var outcome = TraitOutcome(game.Definitions.Get<PartTimeWork>(workId));
            int traitBefore = game.Student.GetTraitValue(outcome.TraitId);
            int spentBefore = game.Student.TimePointsSpentToday;

            Assert.That(game.BeginPartTimeWork(workId, "work:corner-store:1", 3).Applied, Is.True);
            game = TestWorld.Start(saves.Store);
            Assert.That(game.Student.HasPendingMinigame, Is.True, "예약은 저장된다");
            Assert.That(game.SettleMinigame("work:corner-store:1", true).Applied, Is.True);
            game = TestWorld.Start(saves.Store);

            Assert.That(game.Student.GetTraitValue(outcome.TraitId), Is.EqualTo(traitBefore + outcome.Delta));
            Assert.That(game.Student.TimePointsSpentToday, Is.GreaterThan(spentBefore));
            Assert.That(game.Student.GetTodayActivityIds().Count(id => id == workId), Is.EqualTo(1));
            Assert.That(game.Student.HasPendingMinigame, Is.False);
        }

        [TestCaseSource(nameof(Works))]
        public void Job002_EveryTownJob_GrowsOnce_AndRaisesOnlyItsCareerCandidate(string workId)
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            var work = game.Definitions.Get<PartTimeWork>(workId);
            var outcome = TraitOutcome(work);
            int traitBefore = game.Student.GetTraitValue(outcome.TraitId);
            var candidates = game.Definitions.All<CareerCandidate>();
            var hint = game.Definitions.All<CareerHint>().FirstOrDefault(h =>
                h.Matches(new CareerCandidateEvaluationContext(h.Candidate, game.CareerCandidates, game.Student, string.Empty, new[] { workId })));
            var before = candidates.ToDictionary(c => c.Id, c => game.CareerCandidates.GetUnderstandingScore(c));

            // 알바를 할 수 있게 둔다(요구 조건이 있는 알바는 그 조건이 데이터에 있다). 막히면 이유를 보인다.
            var begin = game.BeginPartTimeWork(workId, "work:" + workId, 5);
            Assert.That(begin.Applied, Is.True, workId + " 예약: " + begin.Kind);
            game = TestWorld.Start(saves.Store);
            Assert.That(game.SettleMinigame("work:" + workId, true).Applied, Is.True);
            game = TestWorld.Start(saves.Store);

            Assert.That(game.Student.GetTraitValue(outcome.TraitId), Is.EqualTo(traitBefore + outcome.Delta));
            Assert.That(game.Student.HasPendingMinigame, Is.False);
            foreach (var candidate in candidates)
            {
                int expected = before[candidate.Id] + (hint != null && hint.Candidate == candidate ? hint.UnderstandingDelta : 0);
                Assert.That(game.CareerCandidates.GetUnderstandingScore(candidate), Is.EqualTo(expected),
                    workId + " 뒤 " + candidate.Id + " 이해도(힌트 " + (hint?.Id ?? "없음") + ")");
            }
        }

        [Test]
        public void Job_AFailedMinigame_IsForfeited_GrowsNothing_AndKeepsTheSpentTime()
        {
            const string workId = "work.shift.corner-store";
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            var outcome = TraitOutcome(game.Definitions.Get<PartTimeWork>(workId));
            int traitBefore = game.Student.GetTraitValue(outcome.TraitId);
            Assert.That(game.BeginPartTimeWork(workId, "work:fail", 3).Applied, Is.True);
            int spent = game.Student.TimePointsSpentToday;

            game = TestWorld.Start(saves.Store);
            game.SettleMinigame("work:fail", false);
            game = TestWorld.Start(saves.Store);

            Assert.That(game.Student.GetTraitValue(outcome.TraitId), Is.EqualTo(traitBefore));
            Assert.That(game.Student.HasPendingMinigame, Is.False);
            Assert.That(game.Student.TimePointsSpentToday, Is.EqualTo(spent), "쓴 시간은 돌아오지 않는다");
            Assert.That(game.Inventory.CountOf(game.Item("item.allowance")), Is.EqualTo(0));
        }
    }
}
