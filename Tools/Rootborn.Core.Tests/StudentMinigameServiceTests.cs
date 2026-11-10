using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Core.Careers;
using Rootborn.Core.Minigames;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Tests
{
    // 옮겨 옴: Assets/Tests/EditMode/Careers/StudentMinigameReservationTests.cs (규칙 부분: 001~004, 007, 009, 010, JOB_HINT)
    public sealed class StudentMinigameServiceTests
    {
        [Test]
        public void STUDENT_GAME_001_PracticeBeginReservesAllCostsBeforeGrowth()
        {
            var f = new Fixture();
            Assert.That(f.BeginPractice("practice-1").Applied, Is.True);
            Assert.That(f.Student.TimePointsRemaining, Is.EqualTo(2));
            Assert.That(f.Student.Energy, Is.EqualTo(11));
            Assert.That(f.Student.TimeMinutes, Is.EqualTo(480 + 30));
            Assert.That(f.Student.GetTraitValue("trait.fixture"), Is.Zero);
            var pending = f.Student.PendingMinigame;
            Assert.That(pending.Kind, Is.EqualTo(StudentMinigameKind.Practice));
            Assert.That(pending.DefinitionId, Is.EqualTo("practice.fixture"));
            Assert.That(pending.MinigameId, Is.EqualTo("minigame.fixture"));
            Assert.That(pending.ReservedTimePoints, Is.EqualTo(1));
            Assert.That(pending.Seed, Is.EqualTo(123));
            Assert.That(StudentLifeProgress.FromSaveData(f.Student.ToSaveData()).PendingMinigame.RequestId, Is.EqualTo("practice-1"));
        }

        [Test]
        public void STUDENT_GAME_002_PracticeSuccessCommitsGrowthCandidateUnderstandingAndReceiptTogether()
        {
            var f = new Fixture(); f.BeginPractice("practice-1");
            Assert.That(f.SettlePractice("practice-1", true).Applied, Is.True);
            Assert.That(f.Student.GetTraitValue("trait.fixture"), Is.EqualTo(2));
            Assert.That(f.Student.TimePointsRemaining, Is.EqualTo(2));
            Assert.That(f.Student.Energy, Is.EqualTo(11));
            Assert.That(f.Student.HasAppliedRequest("practice-1"), Is.True);
            Assert.That(f.Student.HasPendingMinigame, Is.False);
            Assert.That(f.Student.CareerCandidates.GetUnderstandingScore(f.Candidate), Is.EqualTo(1));
            Assert.That(f.Student.GetTodayResultLogIds(), Does.Contain("activity.fixture:+trait.fixture=2"));
            Assert.That(CareerCandidateProgress.FromSaveData(f.Student.ToSaveData().CareerCandidates).GetUnderstandingScore(f.Candidate), Is.EqualTo(1));
            Assert.That(f.SettlePractice("practice-1", true).Applied, Is.False);
        }

        [Test]
        public void STUDENT_GAME_003_PartTimeSuccessPaysOnceWithGrowthAndDailyWorkLogs()
        {
            var f = new Fixture();
            Assert.That(f.BeginWork("job-1").Applied, Is.True);
            Assert.That(f.Payroll.Coins, Is.Zero);
            Assert.That(f.SettleWork("job-1", true).Applied, Is.True);
            Assert.That(f.Payroll.Coins, Is.EqualTo(5));
            Assert.That(f.Student.GetTraitValue("trait.fixture"), Is.EqualTo(1));
            Assert.That(f.Student.TimePointsRemaining, Is.EqualTo(2));
            Assert.That(f.Student.GetTodayActivityIds(), Does.Contain("job.fixture"));
            Assert.That(f.SettleWork("job-1", true).Applied, Is.False);
            Assert.That(f.Payroll.Coins, Is.EqualTo(5));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void STUDENT_GAME_004_FailedOrCancelledGameRetainsCostsWithoutIncomeOrGrowth(bool forfeitDirectly)
        {
            var f = new Fixture(); f.BeginWork("job-1");
            var result = forfeitDirectly ? f.Service.Forfeit(f.Student, "job-1") : f.SettleWork("job-1", false);
            Assert.That(result.Applied, Is.True);
            Assert.That(f.Student.TimePointsRemaining, Is.EqualTo(2));
            Assert.That(f.Payroll.Coins, Is.Zero);
            Assert.That(f.Student.GetTraitValue("trait.fixture"), Is.Zero);
            Assert.That(f.Student.HasAppliedRequest("job-1"), Is.True);
            Assert.That(f.BeginWork("job-1").Kind, Is.EqualTo(AdultCareerResultKind.DuplicateRequest));
        }

        [Test]
        public void STUDENT_GAME_007_OneSharedAttemptBlocksOtherPracticeAndPartTimeWork()
        {
            var f = new Fixture(); f.BeginPractice("practice-1");
            Assert.That(f.BeginWork("job-1").Applied, Is.False);
            Assert.That(f.BeginPractice("practice-2").Applied, Is.False);
            Assert.That(f.Student.TimePointsRemaining, Is.EqualTo(2));
        }

        [Test]
        public void STUDENT_GAME_009_UnregisteredPracticeCannotEnterPaidSession()
        {
            var f = new Fixture();
            var unregistered = new CareerPractice("other", "other", f.Practice.Activity, null, null, f.Practice.Minigame);
            Assert.That(f.Service.TryBeginPractice(unregistered, f.Student, f.Adult, "unregistered", 123).Kind, Is.EqualTo(AdultCareerResultKind.InvalidDefinition));
            Assert.That(f.Student.TimePointsRemaining, Is.EqualTo(3));
        }

        [Test]
        public void STUDENT_GAME_010_CurrentPracticeSourceDoesNotMatchOnlyHistoricalActivity()
        {
            var f = new Fixture();
            f.Student.RecordActivityCompleted("activity.fixture", new string[0]);
            var context = new CareerCandidateEvaluationContext(f.Candidate, f.Student.CareerCandidates, f.Student, "new-attempt", new[] { "other-activity" });
            Assert.That(new CareerActivityLogHintSource("activity.fixture").Matches(context), Is.False);
        }

        [Test]
        public void STUDENT_GAME_JOB_HINT_SuccessfulJobCountsTowardCareerOnce_AndFailureCountsNothing()
        {
            var f = new Fixture(jobHint: true);
            Assert.That(f.BeginWork("job-fail").Applied, Is.True);
            Assert.That(f.SettleWork("job-fail", false).Applied, Is.True);
            Assert.That(f.Student.CareerCandidates.GetUnderstandingScore(f.Candidate), Is.Zero);
            Assert.That(f.BeginWork("job-1").Applied, Is.True);
            Assert.That(f.SettleWork("job-1", true).Applied, Is.True);
            Assert.That(f.Student.CareerCandidates.GetUnderstandingScore(f.Candidate), Is.EqualTo(1));
            Assert.That(f.BeginWork("job-2").Applied, Is.True);
            Assert.That(f.SettleWork("job-2", true).Applied, Is.True);
            Assert.That(f.Student.CareerCandidates.GetUnderstandingScore(f.Candidate), Is.EqualTo(1));
        }

        // --- 원래 테스트가 안 다루던 경계 ---

        [Test]
        public void Begin_RejectsAdultsForeignIdentityEmptyRequestAndEndedDay()
        {
            var f = new Fixture();
            Assert.That(f.Service.TryBeginPractice(f.Practice, f.Student, new AdultCareerProgress("slot", "someone"), "r", 1).Kind, Is.EqualTo(AdultCareerResultKind.InvalidRequest));
            Assert.That(f.BeginPractice("").Kind, Is.EqualTo(AdultCareerResultKind.InvalidRequest));
            var adult = AdultCareerProgress.FromSaveData(new AdultCareerProgressSaveData { SaveSlot = "slot", PlayerId = "player", FacilityId = "facility.x", FacilityLevel = 1 });
            Assert.That(f.Service.TryBeginPractice(f.Practice, f.Student, adult, "r", 1).Kind, Is.EqualTo(AdultCareerResultKind.RequirementFailed));
            f.Student.TryEndDay(null, out _);
            Assert.That(f.BeginPractice("r").Kind, Is.EqualTo(AdultCareerResultKind.RequirementFailed));
            Assert.That(f.Student.Energy, Is.EqualTo(12));
            Assert.That(f.Student.HasPendingMinigame, Is.False);
        }

        [Test]
        public void BeginWork_ChecksRewardRoomAndRequirementsBeforeSpending()
        {
            var f = new Fixture();
            f.Payroll.HasRoom = false;
            Assert.That(f.BeginWork("full").Kind, Is.EqualTo(AdultCareerResultKind.InsufficientCapacity));
            f.Payroll.HasRoom = true; f.Work.RequirementsMet = false;
            Assert.That(f.BeginWork("blocked").Kind, Is.EqualTo(AdultCareerResultKind.RequirementFailed));
            Assert.That(f.Service.TryBeginPartTimeWork(f.Work, f.Student, f.Adult, null, "no-payroll", 1).Kind, Is.EqualTo(AdultCareerResultKind.InvalidDefinition));
            Assert.That(f.Student.TimePointsRemaining, Is.EqualTo(3));
            Assert.That(f.Student.HasPendingMinigame, Is.False);
        }

        [Test]
        public void Settle_RejectsWrongKindWrongRequestAndNextDay()
        {
            var f = new Fixture(); f.BeginPractice("practice-1");
            Assert.That(f.SettleWork("practice-1", true).Kind, Is.EqualTo(AdultCareerResultKind.InvalidRequest), "a practice reservation is not a job");
            Assert.That(f.SettlePractice("other", true).Kind, Is.EqualTo(AdultCareerResultKind.InvalidRequest));
            f.Student.TryEndDay(null, out _); f.Student.TryStartNextDay();
            Assert.That(f.SettlePractice("practice-1", true).Kind, Is.EqualTo(AdultCareerResultKind.InvalidRequest), "a reservation from yesterday cannot pay out today");
            Assert.That(f.Student.GetTraitValue("trait.fixture"), Is.Zero);
        }

        [Test]
        public void FullInventoryAtSettlement_KeepsReservationForRetry()
        {
            var f = new Fixture(); f.BeginWork("job-1");
            f.Payroll.HasRoom = false;
            Assert.That(f.SettleWork("job-1", true).Kind, Is.EqualTo(AdultCareerResultKind.InsufficientCapacity));
            Assert.That(f.Student.HasPendingMinigame, Is.True);
            f.Payroll.HasRoom = true;
            Assert.That(f.SettleWork("job-1", true).Applied, Is.True);
            Assert.That(f.Payroll.Coins, Is.EqualTo(5));
        }

        [Test]
        public void ChangedBehavior_SettledJobIsMarkedEvenIfPayrollForgetsTheReceipt()
        {
            var f = new Fixture(); f.Payroll.MarksRequest = false; f.BeginWork("job-1");
            Assert.That(f.SettleWork("job-1", true).Applied, Is.True);
            Assert.That(f.Student.HasAppliedRequest("job-1"), Is.True);
            Assert.That(f.BeginWork("job-1").Kind, Is.EqualTo(AdultCareerResultKind.DuplicateRequest));
        }

        [Test]
        public void Forfeit_WithoutReservationOrTwice_IsRejected()
        {
            var f = new Fixture();
            Assert.That(f.Service.Forfeit(f.Student, "nothing").Kind, Is.EqualTo(AdultCareerResultKind.InvalidRequest));
            f.BeginPractice("p");
            Assert.That(f.Service.Forfeit(f.Student, "p").Applied, Is.True);
            Assert.That(f.Service.Forfeit(f.Student, "p").Kind, Is.EqualTo(AdultCareerResultKind.DuplicateRequest));
        }

        private sealed class FakePayroll : IPartTimeWorkPayroll
        {
            public bool HasRoom = true;
            public bool MarksRequest = true;
            public int Coins;

            public bool CanReceiveRewards(IMinigamePartTimeWork work) => HasRoom;

            public bool TryPerformReserved(IMinigamePartTimeWork work, StudentLifeProgress student, string requestId)
            {
                if (!HasRoom) return false;
                Coins += 5;
                student.AddTrait("trait.fixture", 1);
                if (MarksRequest) student.MarkRequestApplied(requestId);
                student.RecordActivityCompleted(work.Id, new[] { "pay:" + work.Id + "=5" }, true);
                return true;
            }
        }

        private sealed class Fixture
        {
            public readonly StudentLifeProgress Student;
            public readonly AdultCareerProgress Adult = new AdultCareerProgress("slot", "player");
            public readonly CareerPractice Practice;
            public readonly FakePartTimeWork Work;
            public readonly CareerCandidate Candidate;
            public readonly FakePayroll Payroll = new FakePayroll();
            public readonly StudentMinigameService Service;

            public Fixture(bool jobHint = false)
            {
                Student = new StudentLifeProgress("slot", "player", 12, 10, 0, 480);
                Student.ConfigureCalendar(new LifeCalendar(3, 3));
                var policy = new ActivityTimePolicy("practice-time", 1, 127, false);
                var game = new Minigame("minigame.fixture", null, new IChallenge[] { new ChoiceChallenge("c", new[] { 0 }, new[] { "prompt" }, new[] { "a", "b" }) }, null);
                var activity = new LifeActivity("activity.fixture", LifeActivityCategory.School, policy, 30, 1, 0, 0, null, new ILifeActivityEffect[] { new TraitDeltaEffect("trait.fixture", 2) }, null);
                Practice = new CareerPractice("practice.fixture", null, activity, null, null, game);
                Work = new FakePartTimeWork("job.fixture", game, policy, 30, 1, 0, 0);
                Candidate = new CareerCandidate("candidate.fixture", null, "desc", null, null, null, null, null, null, null, revealedHintThreshold: 1, deepeningHintThreshold: 2, readyHintThreshold: 3);
                var hint = jobHint
                    ? new CareerHint("hint.job", null, Candidate, null, null, 1, true, new ICareerHintSource[] { new CareerResultLogHintSource("job.fixture") })
                    : new CareerHint("hint.fixture", null, Candidate, null, "hint", 1, false, new ICareerHintSource[] { new CareerActivityLogHintSource("activity.fixture") });
                Service = new StudentMinigameService(new[] { Practice }, new IMinigamePartTimeWork[] { Work }, new[] { hint }, new[] { Candidate });
            }

            public AdultCareerResult BeginPractice(string request) => Service.TryBeginPractice(Practice, Student, Adult, request, 123);
            public AdultCareerResult BeginWork(string request) => Service.TryBeginPartTimeWork(Work, Student, Adult, Payroll, request, 123);
            public AdultCareerResult SettlePractice(string request, bool succeeded) => Service.TrySettlePractice(Practice, Student, request, succeeded);
            public AdultCareerResult SettleWork(string request, bool succeeded) => Service.TrySettlePartTimeWork(Work, Student, Payroll, request, succeeded);
        }
    }
}
