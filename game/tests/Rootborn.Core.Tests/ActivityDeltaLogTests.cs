using System;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Careers;
using Rootborn.Core.Minigames;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Tests
{
    /// <summary>
    /// 생활 활동 실행기와 예약 활동(진로 연습) 정산은 같은 변화 기록("활동:+id=변화량", "활동:unlock=진로")을 남긴다.
    /// 두 곳이 같은 기록 만들기를 쓰는지 고정한다.
    /// </summary>
    public sealed class ActivityDeltaLogTests
    {
        private const string ActivityId = "activity.mixed";

        private static LifeActivity MixedActivity(ActivityTimePolicy policy) => new LifeActivity(ActivityId, LifeActivityCategory.School, policy, 30, 1, 0, 0, null,
            new ILifeActivityEffect[]
            {
                new TraitDeltaEffect("trait.a", 2), new TraitDeltaEffect("trait.b", -1), new SkillProgressEffect("skill.a", 3),
                new RelationshipDeltaEffect("rel.a", 1), new StatusDeltaEffect("status.a", 4), new CareerHintUnlockEffect("career.a"),
                new TraitDeltaEffect("trait.zero", 0),
            }, null);

        private static StudentLifeProgress NewStudent()
        {
            var student = new StudentLifeProgress("slot", "player", 12, 10, 0, 480);
            student.ConfigureCalendar(new LifeCalendar(3, 3));
            student.AddTrait("trait.b", 5); // 이미 있던 값에서 줄어드는 변화도 기록된다.
            return student;
        }

        private static string[] LogsOf(StudentLifeProgress student)
            => student.GetTodayResultLogIds().Where(log => log.StartsWith(ActivityId + ":", StringComparison.Ordinal)).OrderBy(log => log, StringComparer.Ordinal).ToArray();

        [Test]
        public void RunnerAndPracticeSettlement_WriteTheSameDeltaLogs()
        {
            var policy = new ActivityTimePolicy("time", 1, 127, false);

            var byRunner = NewStudent();
            Assert.That(new LifeActivityRunner().TryPerform(MixedActivity(policy), byRunner, "run-1", out _), Is.True);

            var bySettlement = NewStudent();
            var game = new Minigame("minigame.mixed", null, new IChallenge[] { new ChoiceChallenge("c", new[] { 0 }, new[] { "prompt" }, new[] { "a", "b" }) }, null);
            var practice = new CareerPractice("practice.mixed", null, MixedActivity(policy), null, null, game);
            var service = new StudentMinigameService(new[] { practice }, Array.Empty<IMinigamePartTimeWork>(), Array.Empty<CareerHint>(), Array.Empty<CareerCandidate>());
            Assert.That(service.TryBeginPractice(practice, bySettlement, new AdultCareerProgress("slot", "player"), "practice-1", 1).Applied, Is.True);
            Assert.That(service.TrySettlePractice(practice, bySettlement, "practice-1", true).Applied, Is.True);

            var expected = new[]
            {
                ActivityId + ":+rel.a=1", ActivityId + ":+skill.a=3", ActivityId + ":+status.a=4", ActivityId + ":+trait.a=2", ActivityId + ":+trait.b=-1",
                ActivityId + ":unlock=career.a",
            }.OrderBy(log => log, StringComparer.Ordinal).ToArray();
            Assert.That(LogsOf(byRunner), Is.EqualTo(expected));
            Assert.That(LogsOf(bySettlement), Is.EqualTo(expected));
        }
    }
}
