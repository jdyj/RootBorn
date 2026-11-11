using NUnit.Framework;
using Rootborn.Core.StudentLife;

namespace Rootborn.Game.Tests
{
    /// <summary>
    /// 학교 밖 경로로도 성장·진로 힌트가 진행된다(자유 경로 성장 원칙). Unity CareerCandidateDeepeningLoopE2EScenarioTests의 흐름을 따른다.
    /// 자동 테스트다.
    /// </summary>
    public sealed class FreePathTests
    {
        private const string Candidate = "career.candidate.service";

        [Test]
        public void SquareHelpRequest_DeepensTheServiceCandidateWithoutSchool_OnceAcrossSaveAndLoad()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            var candidate = game.Definitions.Get<CareerCandidate>(Candidate);
            Assert.That(game.CareerCandidates.GetHintCount(candidate), Is.EqualTo(0));

            game.VisitLocation("location.town-square");
            var result = game.PerformLocationActivity("location.activity.square-help-request", "square:help:1");

            Assert.That(result.Kind, Is.EqualTo(LifeActivityResultKind.Applied));
            Assert.That(game.CareerCandidates.GetHintCount(candidate), Is.EqualTo(1));
            int understanding = game.CareerCandidates.GetUnderstandingScore(candidate);
            Assert.That(understanding, Is.GreaterThan(0));

            var again = TestWorld.Start(saves.Store);
            Assert.That(again.CareerCandidates.GetHintCount(candidate), Is.EqualTo(1));
            Assert.That(again.CareerCandidates.GetUnderstandingScore(candidate), Is.EqualTo(understanding));
            Assert.That(again.PerformLocationActivity("location.activity.square-help-request", "square:help:1").Kind,
                Is.EqualTo(LifeActivityResultKind.DuplicateRequest), "같은 요청은 한 번만");
            Assert.That(again.CareerCandidates.GetUnderstandingScore(candidate), Is.EqualTo(understanding));
        }

        [Test]
        public void OutsideSchoolSelfStudy_GrowsTheStudentOutsideClass()
        {
            using var saves = new TempSaves();
            var game = TestWorld.Start(saves.Store);
            var before = Snapshot(game.Student);

            var result = game.PerformOutsideSchoolActivity("outside.self-study.library", "outside:self-study:1");

            Assert.That(result.Kind, Is.EqualTo(LifeActivityResultKind.Applied));
            Assert.That(Snapshot(game.Student), Is.Not.EqualTo(before), "특성이나 기술이 바뀐다");
            Assert.That(Snapshot(TestWorld.Start(saves.Store).Student), Is.EqualTo(Snapshot(game.Student)), "저장된다");
        }

        private static string Snapshot(StudentLifeProgress student)
        {
            var save = student.ToSaveData();
            return string.Join(",", System.Array.ConvertAll(save.Traits, t => t.Id + "=" + t.Value)) + "|"
                + string.Join(",", System.Array.ConvertAll(save.Skills, s => s.Id + "=" + s.Value));
        }
    }
}
