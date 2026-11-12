using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Core.Careers;
using Rootborn.Core.Inventory;
using Rootborn.Core.Minigames;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Tests
{
    // 옮겨 옴: Assets/Tests/EditMode/Careers/AdultCareerFoundationTests.cs,
    //          Assets/Tests/EditMode/Careers/PendingFacilityWorkTests.cs(규칙 부분: 001~004, 007, 009, 010, 014, 016, 017)
    public sealed class AdultCareerTests
    {
        [Test]
        public void ADULT_001_TransitionRequiresEarnedHintAndReadyCandidate()
        {
            var f = new Fixture(earned: false);
            Assert.That(f.Transition("transition").Kind, Is.EqualTo(AdultCareerResultKind.RequirementFailed));
            Assert.That(f.Progress.IsAdult, Is.False);
            f.Student.UnlockCareerHint("career.fixture");
            Assert.That(f.Transition("transition").Kind, Is.EqualTo(AdultCareerResultKind.RequirementFailed));
            f.Candidates = Fixture.ReadyCandidates();
            Assert.That(f.Transition("transition").Kind, Is.EqualTo(AdultCareerResultKind.Applied));
            Assert.That(f.Progress.FacilityLevel, Is.EqualTo(1));
        }

        [Test]
        public void ChangedBehavior_TransitionIsRefusedAfterTheDayHasEnded()
        {
            var f = new Fixture();
            f.Student.TryEndDay(null, out _);
            Assert.That(f.Transition("transition").Kind, Is.EqualTo(AdultCareerResultKind.RequirementFailed));
            Assert.That(f.Progress.IsAdult, Is.False);
            f.Student.TryStartNextDay();
            Assert.That(f.Transition("transition").Kind, Is.EqualTo(AdultCareerResultKind.Applied), "거절한 요청은 소모되지 않는다.");
        }

        [Test]
        public void ChangedBehavior_UnsettledWorkIsForfeitedWhenTheNextDayStarts()
        {
            var f = new Fixture();
            f.Transition("transition");
            Assert.That(f.Reserve("attempt-1").Applied, Is.True);
            f.Student.TryEndDay(null, out _); f.Student.TryStartNextDay();

            Assert.That(f.Service.ForfeitWorkLeftFromEarlierDays(f.Student, f.Progress), Is.True);

            Assert.That(f.Progress.HasPendingWork, Is.False);
            Assert.That(f.Progress.HasAppliedRequest("attempt-1"), Is.True);
            Assert.That(f.Service.ForfeitWorkLeftFromEarlierDays(f.Student, f.Progress), Is.False);
        }

        [Test]
        public void ChangedBehavior_WorkLeftFromAnEarlierDayDoesNotBlockANewReservation()
        {
            var f = new Fixture();
            f.Transition("transition");
            f.Reserve("attempt-1");
            f.Student.TryEndDay(null, out _); f.Student.TryStartNextDay();

            Assert.That(f.Reserve("attempt-2").Kind, Is.EqualTo(AdultCareerResultKind.Applied));
            Assert.That(f.Progress.PendingWork.RequestId, Is.EqualTo("attempt-2"));
            Assert.That(f.Progress.HasAppliedRequest("attempt-1"), Is.True);
        }

        [Test]
        public void WorkReservedToday_IsNotForfeited()
        {
            var f = new Fixture();
            f.Transition("transition");
            f.Reserve("attempt-1");
            Assert.That(f.Service.ForfeitWorkLeftFromEarlierDays(f.Student, f.Progress), Is.False);
            Assert.That(f.Progress.HasPendingWork, Is.True);
        }

        [Test]
        public void ADULT_002_TransitionPreservesStudentGrowthAndRejectsSecondFacility()
        {
            var f = new Fixture();
            f.Student.AddTrait("trait.fixture", 17);
            Assert.That(f.Transition("first").Kind, Is.EqualTo(AdultCareerResultKind.Applied));
            Assert.That(f.Transition("first").Kind, Is.EqualTo(AdultCareerResultKind.DuplicateRequest));
            Assert.That(f.Transition("different-request").Kind, Is.EqualTo(AdultCareerResultKind.AlreadyAdult));
            Assert.That(f.Student.GetTraitValue("trait.fixture"), Is.EqualTo(17));
        }

        [Test]
        public void ADULT_003_EarliestDayRuleIsConfigurableAndDoesNotConsumeRequest()
        {
            var f = new Fixture(extraTransitionRule: new MinimumDayTransitionRequirement(2));
            Assert.That(f.Transition("same-request").Kind, Is.EqualTo(AdultCareerResultKind.RequirementFailed));
            f.Student.TryEndDay(null, out _); f.Student.TryStartNextDay();
            Assert.That(f.Transition("same-request").Kind, Is.EqualTo(AdultCareerResultKind.Applied));
        }

        [Test]
        public void ADULT_004_WorkConsumesSharedTimeAndAwardsDeclaredMaximumExactlyOnce()
        {
            var f = new Fixture(); f.Transition("transition");
            Assert.That(f.Work("job", 100, false).Kind, Is.EqualTo(AdultCareerResultKind.Applied));
            Assert.That(f.Inventory.CountOf(Fixture.Coin), Is.EqualTo(20));
            Assert.That(f.Student.TimePointsRemaining, Is.EqualTo(1));
            Assert.That(f.Progress.GetSuccessfulCompletions(f.FacilityWork), Is.EqualTo(1));
            Assert.That(f.Work("job", 100, false).Kind, Is.EqualTo(AdultCareerResultKind.DuplicateRequest));
            Assert.That(f.Inventory.CountOf(Fixture.Coin), Is.EqualTo(20));
            Assert.That(f.Student.TimePointsRemaining, Is.EqualTo(1));
        }

        [Test]
        public void ADULT_018_DirectWorkAppearsInDailyActivityExactlyOnce()
        {
            var f = new Fixture(); f.Transition("transition");
            Assert.That(f.Work("recorded-work", 100, false).Kind, Is.EqualTo(AdultCareerResultKind.Applied));
            Assert.That(f.Student.GetTodayActivityIds(), Is.EqualTo(new[] { "work.fixture" }));
            Assert.That(f.Work("recorded-work", 100, false).Kind, Is.EqualTo(AdultCareerResultKind.DuplicateRequest));
            Assert.That(f.Student.GetTodayActivityIds(), Has.Length.EqualTo(1));
        }

        [Test]
        public void ADULT_019_WorkAdvancesTheClockByItsMinutesOnlyWhenApplied()
        {
            var f = new Fixture(timeCostMinutes: 90); f.Transition("transition");
            int start = f.Student.TimeMinutes;
            Assert.That(f.Work("clock", 100, false).Kind, Is.EqualTo(AdultCareerResultKind.Applied));
            Assert.That(f.Student.TimeMinutes, Is.EqualTo(start + 90));
            Assert.That(f.Work("clock", 100, false).Kind, Is.EqualTo(AdultCareerResultKind.DuplicateRequest));
            Assert.That(f.Student.TimeMinutes, Is.EqualTo(start + 90));
            FlowFixtures.Exhaust(f.Student);
            int beforeRefusal = f.Student.TimeMinutes;
            Assert.That(f.Work("clock-no-time", 100, false).Kind, Is.EqualTo(AdultCareerResultKind.InsufficientTime));
            Assert.That(f.Student.TimeMinutes, Is.EqualTo(beforeRefusal));
        }

        [Test]
        public void ADULT_005_FullInventoryPreflightPreservesTimeMasteryAndRequest()
        {
            var f = new Fixture(); f.Transition("transition");
            for (int i = 0; i < Fixture.MaxSlots; i++) f.Slots.Add("full-" + i, 1);
            Assert.That(f.Work("retry", 100, false).Kind, Is.EqualTo(AdultCareerResultKind.InsufficientCapacity));
            Assert.That(f.Student.TimePointsRemaining, Is.EqualTo(2));
            Assert.That(f.Progress.GetMastery(f.FacilityWork), Is.Zero);
            f.Slots.Clear();
            Assert.That(f.Work("retry", 100, false).Kind, Is.EqualTo(AdultCareerResultKind.Applied));
        }

        [Test]
        public void ADULT_006_ExhaustionDoesNotPayOrAdvanceMastery()
        {
            // 흐르는 시간: 하루 시간 포인트 한도 대신 지침이 막는다.
            var f = new Fixture(); f.Transition("transition"); FlowFixtures.Exhaust(f.Student);
            Assert.That(f.Work("job", 100, false).Kind, Is.EqualTo(AdultCareerResultKind.InsufficientTime));
            Assert.That(f.Inventory.CountOf(Fixture.Coin), Is.Zero);
            Assert.That(f.Progress.GetMastery(f.FacilityWork), Is.Zero);
        }

        [TestCase(1, 2, 2, true)]
        [TestCase(2, 1, 2, true)]
        [TestCase(2, 2, 1, true)]
        [TestCase(2, 2, 2, false)]
        public void ADULT_007_AutoSkipRequiresMasterySuccessCountFacilityAndEquipmentTogether(int mastery, int successCount, int level, bool equipment)
        {
            var f = new Fixture(); f.Transition("transition"); f.Seed(level, mastery, successCount);
            if (equipment) f.Slots.Add(Fixture.Equipment);
            Assert.That(f.Work("skip", 0, true).Kind, Is.EqualTo(AdultCareerResultKind.RequirementFailed));
            Assert.That(f.Inventory.CountOf(Fixture.Coin), Is.Zero);
            Assert.That(f.Student.TimePointsRemaining, Is.EqualTo(2));
        }

        [Test]
        public void ADULT_008_AutoSkipAwardsMaximumRewardNotSeventyPercent()
        {
            var f = new Fixture(); f.Transition("transition"); f.Seed(2, 2, 2); f.Slots.Add(Fixture.Equipment);
            Assert.That(f.Work("skip", 0, true).Kind, Is.EqualTo(AdultCareerResultKind.Applied));
            Assert.That(f.Inventory.CountOf(Fixture.Coin), Is.EqualTo(20));
            Assert.That(f.Student.TimePointsRemaining, Is.EqualTo(1));
        }

        [Test]
        public void ADULT_009_UpgradeConsumesAggregatedCostsAndActivatesWorldChange()
        {
            var f = new Fixture(); f.Transition("transition"); f.Slots.Add(Fixture.Coin, 10);
            Assert.That(f.Upgrade("upgrade").Kind, Is.EqualTo(AdultCareerResultKind.Applied));
            Assert.That(f.Progress.FacilityLevel, Is.EqualTo(2));
            Assert.That(f.Inventory.CountOf(Fixture.Coin), Is.Zero);
            Assert.That(f.World.IsActive("world.fixture.level2"), Is.True);
            Assert.That(f.Upgrade("upgrade").Kind, Is.EqualTo(AdultCareerResultKind.DuplicateRequest));
        }

        [Test]
        public void ADULT_010_UpgradeWithInsufficientDuplicateItemCostsIsAtomic()
        {
            var f = new Fixture(); f.Transition("transition"); f.Slots.Add(Fixture.Coin, 9);
            Assert.That(f.Upgrade("upgrade").Kind, Is.EqualTo(AdultCareerResultKind.InsufficientResources));
            Assert.That(f.Inventory.CountOf(Fixture.Coin), Is.EqualTo(9));
            Assert.That(f.Progress.FacilityLevel, Is.EqualTo(1));
            Assert.That(f.World.IsActive("world.fixture.level2"), Is.False);
            f.Slots.Add(Fixture.Coin);
            Assert.That(f.Upgrade("upgrade").Kind, Is.EqualTo(AdultCareerResultKind.Applied));
        }

        [Test]
        public void ADULT_011_LevelThreeRecordsOnlySelectedRouteEndingOnce()
        {
            var f = new Fixture(); f.Transition("transition"); f.Slots.Add(Fixture.Coin, 30);
            Assert.That(f.Upgrade("upgrade-2").Kind, Is.EqualTo(AdultCareerResultKind.Applied));
            Assert.That(f.Upgrade("upgrade-3").Kind, Is.EqualTo(AdultCareerResultKind.Applied));
            Assert.That(f.Progress.FacilityLevel, Is.EqualTo(3));
            Assert.That(f.Progress.HasEnding(f.Facility), Is.True);
            Assert.That(f.Progress.EndingIds.Length, Is.EqualTo(1));
            Assert.That(f.Upgrade("upgrade-4").Kind, Is.EqualTo(AdultCareerResultKind.AtMaximumLevel));
        }

        [Test]
        public void ADULT_012_SaveRoundTripPreservesWorkClaimEndingAndStudentBudget()
        {
            var f = new Fixture(); f.Transition("transition"); f.Work("job", 100, false); f.Slots.Add(Fixture.Coin, 20);
            f.Upgrade("upgrade-2"); f.Upgrade("upgrade-3");
            f.Progress = AdultCareerProgress.FromSaveData(f.Progress.ToSaveData());
            f.Student = StudentLifeProgress.FromSaveData(f.Student.ToSaveData()); f.Student.ConfigureCalendar(new LifeCalendar(2, 3));
            Assert.That(f.Work("job", 100, false).Kind, Is.EqualTo(AdultCareerResultKind.DuplicateRequest));
            Assert.That(f.Student.TimePointsRemaining, Is.EqualTo(1));
            Assert.That(f.Progress.HasEnding(f.Facility), Is.True);
            Assert.That(f.Progress.GetSuccessfulCompletions(f.FacilityWork), Is.EqualTo(1));
        }

        [Test]
        public void ADULT_013_UnregisteredFacilityCannotTransition()
        {
            var f = new Fixture(); f.Service = new AdultCareerService(Array.Empty<Facility>());
            Assert.That(f.Transition("transition").Kind, Is.EqualTo(AdultCareerResultKind.InvalidDefinition));
        }

        [Test]
        public void ADULT_014_ForgedWorkCannotAwardRewardsEvenForSameFacility()
        {
            var f = new Fixture(); f.Transition("transition");
            f.FacilityWork = new FacilityWork("forged", null, "facility.fixture", null, new[] { new ItemAmount<string>(Fixture.Coin, 20) }, null);
            Assert.That(f.Work("forged", 100, false).Kind, Is.EqualTo(AdultCareerResultKind.InvalidDefinition));
        }

        [Test]
        public void ADULT_015_CrossPlayerGrowthCannotAuthorizeTransition()
        {
            var f = new Fixture(); f.Progress = new AdultCareerProgress("slot", "other-player");
            Assert.That(f.Transition("transition").Kind, Is.EqualTo(AdultCareerResultKind.InvalidRequest));
        }

        // --- PendingFacilityWorkTests의 규칙 부분 ---

        [Test]
        public void PENDING_WORK_001_017_BeginReservesTimeAndMovesClockOnceWithoutPaying()
        {
            var f = new Fixture(timeCostMinutes: 90); f.Transition("transition");
            int start = f.Student.TimeMinutes;
            Assert.That(f.Reserve("attempt-1").Applied, Is.True);
            Assert.That(f.Student.TimePointsRemaining, Is.EqualTo(1));
            // 흐르는 시간 3.2절: 예약은 끝나는 시각만 정하고 시계는 정산 때 남은 몫만 간다(MinigameActivityTimeTests).
            Assert.That(f.Student.TimeMinutes, Is.EqualTo(start));
            Assert.That(f.Student.BusyUntilMinutes, Is.EqualTo(start + 90));
            Assert.That(f.Inventory.CountOf(Fixture.Coin), Is.Zero);
            var pending = f.Progress.ToSaveData().PendingWork;
            Assert.That(pending.RequestId, Is.EqualTo("attempt-1"));
            Assert.That(pending.WorkId, Is.EqualTo("work.fixture"));
            Assert.That(pending.MinigameId, Is.EqualTo("minigame.fixture"));
            Assert.That(pending.ReservedTimePoints, Is.EqualTo(1));
            Assert.That(f.Student.GetTodayActivityIds(), Is.Empty, "PENDING_WORK_016: a reservation alone is not a finished activity");

            Assert.That(f.Settle("attempt-1", 100).Applied, Is.True);
            Assert.That(f.Student.TimeMinutes, Is.EqualTo(start + 90), "settling must not move the clock again");
        }

        [Test]
        public void PENDING_WORK_002_SecondBeginCannotReplaceOrSpendAnotherReservation()
        {
            var f = new Fixture(); f.Transition("transition"); f.Reserve("attempt-1");
            Assert.That(f.Reserve("attempt-2").Applied, Is.False);
            Assert.That(f.Reserve("attempt-1").Applied, Is.False);
            Assert.That(f.Progress.PendingWork.RequestId, Is.EqualTo("attempt-1"));
            Assert.That(f.Student.TimePointsRemaining, Is.EqualTo(1));
        }

        [Test]
        public void PENDING_WORK_003_014_SettlementPaysOnceWithReceiptAndDailyActivity()
        {
            var f = new Fixture(); f.Transition("transition"); f.Reserve("attempt-1");
            Assert.That(f.Settle("attempt-1", 100).Applied, Is.True);
            Assert.That(f.Inventory.CountOf(Fixture.Coin), Is.EqualTo(20));
            Assert.That(f.Student.TimePointsRemaining, Is.EqualTo(1));
            var saved = f.Progress.ToSaveData();
            Assert.That(PendingFacilityWorkSaveData.IsEmpty(saved.PendingWork), Is.True);
            Assert.That(saved.AppliedRequestIds, Does.Contain("attempt-1"));
            Assert.That(saved.Works[0].SuccessfulCompletions, Is.EqualTo(1));
            Assert.That(f.Student.GetTodayActivityIds(), Is.EqualTo(new[] { "work.fixture" }));
            Assert.That(f.Settle("attempt-1", 100).Kind, Is.EqualTo(AdultCareerResultKind.DuplicateRequest));
            Assert.That(f.Inventory.CountOf(Fixture.Coin), Is.EqualTo(20));
        }

        [Test]
        public void PENDING_WORK_004_OtherRequestCannotSettleTheReservation()
        {
            var f = new Fixture(); f.Transition("transition"); f.Reserve("attempt-1");
            Assert.That(f.Settle("forged", 100).Kind, Is.EqualTo(AdultCareerResultKind.InvalidRequest));
            Assert.That(f.Settle("attempt-1", 101).Kind, Is.EqualTo(AdultCareerResultKind.InvalidRequest));
            Assert.That(f.Inventory.CountOf(Fixture.Coin), Is.Zero);
            Assert.That(f.Progress.HasPendingWork, Is.True);
        }

        [Test]
        public void PENDING_WORK_007_ForfeitKeepsReservedTimeAndCannotGrantOrReplay()
        {
            var f = new Fixture(); f.Transition("transition"); f.Reserve("attempt-1");
            Assert.That(f.Service.TryForfeitReservedWork(f.Student, f.Progress, "attempt-1").Applied, Is.True);
            Assert.That(f.Student.TimePointsRemaining, Is.EqualTo(1));
            Assert.That(f.Inventory.CountOf(Fixture.Coin), Is.Zero);
            Assert.That(f.Progress.GetSuccessfulCompletions(f.FacilityWork), Is.Zero);
            Assert.That(f.Reserve("attempt-1").Kind, Is.EqualTo(AdultCareerResultKind.DuplicateRequest));
            Assert.That(f.Settle("attempt-1", 100).Kind, Is.EqualTo(AdultCareerResultKind.DuplicateRequest));
        }

        [Test]
        public void PENDING_WORK_009_MissingAuthoredMinigameRejectsBeforeSpendingTime()
        {
            var f = new Fixture(withMinigame: false); f.Transition("transition");
            Assert.That(f.Reserve("attempt-1").Kind, Is.EqualTo(AdultCareerResultKind.InvalidDefinition));
            Assert.That(f.Student.TimePointsRemaining, Is.EqualTo(2));
        }

        [Test]
        public void PENDING_WORK_010_DirectWorkAndUpgradeCannotBypassActiveReservation()
        {
            var f = new Fixture(); f.Transition("transition"); f.Slots.Add(Fixture.Coin, 10); f.Reserve("attempt-1");
            Assert.That(f.Work("bypass", 100, false).Applied, Is.False);
            Assert.That(f.Upgrade("upgrade-bypass").Applied, Is.False);
            Assert.That(f.Student.TimePointsRemaining, Is.EqualTo(1));
            Assert.That(f.Inventory.CountOf(Fixture.Coin), Is.EqualTo(10));
        }

        // --- 할 수 있는지 조회(Unity AdultCareerRuntimeComponent.CanTransition/CanWork/CanAutoSkip/CanUpgrade) ---

        [Test]
        public void CanTransition_GivesTheSameReasonAsTheCommand_AndChangesNothing()
        {
            var f = new Fixture(earned: false);
            Assert.That(f.Service.CanTransition(f.Facility, f.Student, f.Candidates, f.Progress), Is.EqualTo(AdultCareerResultKind.RequirementFailed));
            f.Student.UnlockCareerHint("career.fixture");
            f.Candidates = Fixture.ReadyCandidates();
            Assert.That(f.Service.CanTransition(f.Facility, f.Student, f.Candidates, f.Progress), Is.EqualTo(AdultCareerResultKind.Applied));
            Assert.That(f.Progress.IsAdult, Is.False, "조회는 바꾸지 않는다");
            f.Transition("transition");
            Assert.That(f.Service.CanTransition(f.Facility, f.Student, f.Candidates, f.Progress), Is.EqualTo(AdultCareerResultKind.AlreadyAdult));
            Assert.That(f.Service.CanTransition(new Facility("facility.other", null, null, null, "c", null, null, Fixture.Levels(), null), f.Student, f.Candidates, f.Progress),
                Is.EqualTo(AdultCareerResultKind.InvalidDefinition), "등록하지 않은 시설");
        }

        [Test]
        public void CanWork_FollowsReservationRules_ForAMinigameWork()
        {
            var f = new Fixture();
            Assert.That(f.Service.CanWork(f.FacilityWork, f.Student, f.Progress, f.Inventory), Is.EqualTo(AdultCareerResultKind.RequirementFailed), "아직 성인이 아니다");
            f.Transition("transition");
            Assert.That(f.Service.CanWork(f.FacilityWork, f.Student, f.Progress, f.Inventory), Is.EqualTo(AdultCareerResultKind.Applied));
            Assert.That(f.Student.TimePointsRemaining, Is.EqualTo(2), "조회는 시간을 쓰지 않는다");
            f.Reserve("attempt-1");
            Assert.That(f.Service.CanWork(f.FacilityWork, f.Student, f.Progress, f.Inventory), Is.EqualTo(AdultCareerResultKind.RequirementFailed), "예약이 남아 있다");
            f.Service.TryForfeitReservedWork(f.Student, f.Progress, "attempt-1");
            FlowFixtures.Exhaust(f.Student);
            Assert.That(f.Service.CanWork(f.FacilityWork, f.Student, f.Progress, f.Inventory), Is.EqualTo(AdultCareerResultKind.InsufficientTime));
            Assert.That(f.Reserve("attempt-2").Kind, Is.EqualTo(AdultCareerResultKind.InsufficientTime), "명령도 같은 이유");
        }

        [Test]
        public void CanWork_ForADirectWork_ChecksTheFullRewardFits()
        {
            var f = new Fixture(withMinigame: false); f.Transition("transition");
            Assert.That(f.Service.CanWork(f.FacilityWork, f.Student, f.Progress, f.Inventory), Is.EqualTo(AdultCareerResultKind.Applied));
            for (int i = 0; i < Fixture.MaxSlots; i++) f.Slots.Add("full-" + i, 1);
            Assert.That(f.Service.CanWork(f.FacilityWork, f.Student, f.Progress, f.Inventory), Is.EqualTo(AdultCareerResultKind.InsufficientCapacity));
            Assert.That(f.Work("job", 100, false).Kind, Is.EqualTo(AdultCareerResultKind.InsufficientCapacity));
        }

        [Test]
        public void CanWorkAndCanAutoSkip_CountTheSpaceBonus_LikeTheCommand()
        {
            // 만점 보상: 레벨 비율 100%면 동전 20, 이득 +50%면 30. 이득 없는 20만 들어가게 채운다.
            var f = new Fixture(withMinigame: false); f.Transition("transition");
            f.Seed(2, 2, 2);
            for (int i = 0; i < Fixture.MaxSlots - 2; i++) f.Slots.Add("full-" + i, 1);
            f.Slots.Add(Fixture.Equipment);
            f.Slots.Add(Fixture.Coin, 999 - 20);
            var bonus = new WorkExtras(50, 0, null);
            Assert.That(f.Service.CanWork(f.FacilityWork, f.Student, f.Progress, f.Inventory), Is.EqualTo(AdultCareerResultKind.Applied), "준비: 이득 없는 보상은 들어간다");
            Assert.That(f.Service.CanAutoSkip(f.FacilityWork, f.Student, f.Progress, f.Inventory), Is.EqualTo(AdultCareerResultKind.Applied));

            Assert.That(f.Service.CanWork(f.FacilityWork, f.Student, f.Progress, f.Inventory, bonus), Is.EqualTo(AdultCareerResultKind.InsufficientCapacity));
            Assert.That(f.Service.CanAutoSkip(f.FacilityWork, f.Student, f.Progress, f.Inventory, bonus), Is.EqualTo(AdultCareerResultKind.InsufficientCapacity));
            Assert.That(f.Service.TryCompleteWork(f.FacilityWork, f.Student, f.Progress, f.Inventory, "job", 100, false, bonus).Kind, Is.EqualTo(AdultCareerResultKind.InsufficientCapacity), "명령도 같은 이유");
            Assert.That(f.Service.TryCompleteWork(f.FacilityWork, f.Student, f.Progress, f.Inventory, "skip", 100, true, bonus).Kind, Is.EqualTo(AdultCareerResultKind.InsufficientCapacity));
            Assert.That(f.Inventory.CountOf(Fixture.Coin), Is.EqualTo(999 - 20), "가방 그대로");
        }

        [Test]
        public void CanWork_IgnoresAReservationLeftFromAnEarlierDay_LikeTheCommand()
        {
            var f = new Fixture(); f.Transition("transition"); f.Reserve("attempt-1");
            f.Student.TryEndDay(null, out _);
            f.Student.TryStartNextDay();
            Assert.That(f.Progress.HasPendingWork, Is.True, "아직 포기 처리하지 않았다");
            Assert.That(f.Service.CanWork(f.FacilityWork, f.Student, f.Progress, f.Inventory), Is.EqualTo(AdultCareerResultKind.Applied));
            Assert.That(f.Progress.HasPendingWork, Is.True, "조회는 포기 처리하지 않는다");
        }

        [Test]
        public void CanAutoSkip_NeedsTheAutoSkipRulesAndRoom()
        {
            var f = new Fixture(); f.Transition("transition");
            Assert.That(f.Service.CanAutoSkip(f.FacilityWork, f.Student, f.Progress, f.Inventory), Is.EqualTo(AdultCareerResultKind.RequirementFailed));
            f.Seed(2, 2, 2); f.Slots.Add(Fixture.Equipment);
            Assert.That(f.Service.CanAutoSkip(f.FacilityWork, f.Student, f.Progress, f.Inventory), Is.EqualTo(AdultCareerResultKind.Applied));
            Assert.That(f.Inventory.CountOf(Fixture.Coin), Is.Zero);
            Assert.That(f.Work("skip", 0, true).Kind, Is.EqualTo(AdultCareerResultKind.Applied));
        }

        [Test]
        public void CanUpgrade_ReportsMissingCostsAndTheMaximumLevel()
        {
            var f = new Fixture(); f.Transition("transition"); f.Slots.Add(Fixture.Coin, 9);
            Assert.That(f.Service.CanUpgrade(f.Facility, f.Student, f.Progress, f.Inventory), Is.EqualTo(AdultCareerResultKind.InsufficientResources));
            f.Slots.Add(Fixture.Coin);
            Assert.That(f.Service.CanUpgrade(f.Facility, f.Student, f.Progress, f.Inventory), Is.EqualTo(AdultCareerResultKind.Applied));
            Assert.That(f.Inventory.CountOf(Fixture.Coin), Is.EqualTo(10), "조회는 비용을 쓰지 않는다");
            f.Seed(3, 0, 0);
            Assert.That(f.Service.CanUpgrade(f.Facility, f.Student, f.Progress, f.Inventory), Is.EqualTo(AdultCareerResultKind.AtMaximumLevel));
        }

        // --- 원래 테스트가 안 다루던 경계 ---

        [Test]
        public void LowScore_PaysPartOfMaximumAndBelowMinimumPaysNothingButStillCounts()
        {
            var f = new Fixture(minimumSuccessScore: 50); f.Transition("transition");
            Assert.That(f.Work("half", 60, false).Applied, Is.True);
            Assert.That(f.Inventory.CountOf(Fixture.Coin), Is.EqualTo(12), "20 × 60 × 100% / 10000");
            Assert.That(f.Work("weak", 10, false).Applied, Is.True);
            Assert.That(f.Inventory.CountOf(Fixture.Coin), Is.EqualTo(12));
            Assert.That(f.Progress.GetSuccessfulCompletions(f.FacilityWork), Is.EqualTo(1), "a weak shift is recorded but not a success");
            Assert.That(f.Progress.Reputation, Is.EqualTo(1));
            Assert.That(f.Work("negative", -1, false).Kind, Is.EqualTo(AdultCareerResultKind.InvalidRequest));
        }

        [Test]
        public void MasteryNeverExceedsMaximum()
        {
            var f = new Fixture(); f.Transition("transition");
            f.Work("a", 100, false);
            f.Student.TryEndDay(null, out _); f.Student.TryStartNextDay();
            f.Work("b", 100, false); f.Work("c", 100, false);
            Assert.That(f.Progress.GetMastery(f.FacilityWork), Is.EqualTo(2));
            Assert.That(f.Progress.GetSuccessfulCompletions(f.FacilityWork), Is.EqualTo(3));
        }

        [Test]
        public void Upgrade_RejectsWorldStateOfAnotherPlayerWithoutChange()
        {
            var f = new Fixture(); f.Transition("transition"); f.Slots.Add(Fixture.Coin, 10);
            f.World = new FakeWorld("slot", "someone-else");
            Assert.That(f.Upgrade("upgrade").Kind, Is.EqualTo(AdultCareerResultKind.InvalidRequest));
            Assert.That(f.Inventory.CountOf(Fixture.Coin), Is.EqualTo(10));
            Assert.That(f.Progress.FacilityLevel, Is.EqualTo(1));
        }

        [Test]
        public void ReentrantCommandDuringSettlement_IsRejected()
        {
            var f = new Fixture(); f.Transition("transition");
            AdultCareerResult nested = default;
            f.Inventory = new ReentrantInventory(f.Inventory, () => nested = f.Service.TryTransition(f.Facility, f.Student, f.Candidates, f.Progress, "nested"));
            Assert.That(f.Work("job", 100, false).Applied, Is.True);
            Assert.That(nested.Kind, Is.EqualTo(AdultCareerResultKind.InvalidRequest));
        }

        [Test]
        public void SaveLoad_ClampsBrokenValues()
        {
            var progress = AdultCareerProgress.FromSaveData(new AdultCareerProgressSaveData
            {
                SaveSlot = "slot", PlayerId = "player", FacilityId = "facility.fixture", FacilityLevel = 9, Reputation = -5,
                Works = new[] { new FacilityWorkProgressSaveData { WorkId = "work.fixture", Mastery = -2, SuccessfulCompletions = 4 }, null, new FacilityWorkProgressSaveData { WorkId = "" } },
                PendingWork = new PendingFacilityWorkSaveData(),
            });
            Assert.That(progress.FacilityLevel, Is.EqualTo(3));
            Assert.That(progress.Reputation, Is.Zero);
            Assert.That(progress.HasPendingWork, Is.False, "an empty pending shell is no reservation");
            Assert.That(progress.ToSaveData().Works.Length, Is.EqualTo(1));
            Assert.That(progress.ToSaveData().Works[0].Mastery, Is.Zero);
            Assert.That(AdultCareerProgress.FromSaveData(new AdultCareerProgressSaveData { FacilityLevel = 2 }).FacilityLevel, Is.Zero, "no facility, no level");
            Assert.That(AdultCareerProgress.FromSaveData(null).SaveSlot, Is.EqualTo("default"));
        }

        [Test]
        public void Transition_NeedsBothCareerHintAndCandidateReadyRules_ButAcceptsExtraKinds()
        {
            var f = new Fixture();
            var onlyHint = new Facility("facility.hint-only", null, null, null, "career.fixture", f.Facility.Candidate, new IFacilityTransitionRequirement[] { new CareerHintTransitionRequirement() }, Fixture.Levels(), null);
            var withExtra = new Facility("facility.extra", null, null, null, "career.fixture", f.Facility.Candidate,
                new IFacilityTransitionRequirement[] { new CareerHintTransitionRequirement(), new AlwaysTransition(), new CandidateReadyTransitionRequirement() }, Fixture.Levels(), null);
            var service = new AdultCareerService(new[] { onlyHint, withExtra });
            Assert.That(service.TryTransition(onlyHint, f.Student, f.Candidates, f.Progress, "a").Kind, Is.EqualTo(AdultCareerResultKind.RequirementFailed));
            Assert.That(service.TryTransition(withExtra, f.Student, f.Candidates, f.Progress, "b").Kind, Is.EqualTo(AdultCareerResultKind.Applied));
        }

        [Test]
        public void DuplicateFacilityIds_AreNotRegistered()
        {
            var f = new Fixture();
            var copy = new Facility("facility.fixture", null, null, null, "career.fixture", f.Facility.Candidate, new IFacilityTransitionRequirement[] { new CareerHintTransitionRequirement(), new CandidateReadyTransitionRequirement() }, Fixture.Levels(), null);
            f.Service = new AdultCareerService(new[] { f.Facility, copy });
            Assert.That(f.Transition("t").Kind, Is.EqualTo(AdultCareerResultKind.InvalidDefinition));
        }

        // --- 공간 이득(WorkExtras): 보상 비율·명성 보너스·기술 성장 ---

        private static WorkExtras Extras(int rewardPercentBonus, int reputationBonus = 0, int skillAmount = 0)
            => new WorkExtras(rewardPercentBonus, reputationBonus, skillAmount > 0 ? new[] { new SkillGain("skill.test", skillAmount) } : null);

        [Test]
        public void CompleteWork_WithRewardBonus_PaysMore()
        {
            var f = new Fixture(maximumReward: 30); f.Transition("transition");
            var result = f.Service.TryCompleteWork(f.FacilityWork, f.Student, f.Progress, f.Inventory, "job", 100, false, Extras(50));
            Assert.That(result.Kind, Is.EqualTo(AdultCareerResultKind.Applied));
            Assert.That(f.Inventory.CountOf(Fixture.Coin), Is.EqualTo(45), "30 × 100 × (100% + 50%) / 10000");
        }

        [Test]
        public void CompleteWork_RewardPercentIsCappedAt10000()
        {
            var f = new Fixture(maximumReward: 30); f.Transition("transition");
            var result = f.Service.TryCompleteWork(f.FacilityWork, f.Student, f.Progress, f.Inventory, "job", 100, false, Extras(20000));
            Assert.That(result.Kind, Is.EqualTo(AdultCareerResultKind.Applied), "합이 한도를 넘어도 일은 실패하지 않고 한도로 자른다");
            Assert.That(f.Inventory.CountOf(Fixture.Coin), Is.EqualTo(30 * 100), "비율 10000 = 최대 보상의 100배");
        }

        [Test]
        public void CompleteWork_Success_AddsReputationBonusAndSkill()
        {
            var f = new Fixture(); f.Transition("transition");
            var result = f.Service.TryCompleteWork(f.FacilityWork, f.Student, f.Progress, f.Inventory, "job", 100, false, Extras(0, 3, 2));
            Assert.That(result.Kind, Is.EqualTo(AdultCareerResultKind.Applied));
            Assert.That(f.Progress.Reputation, Is.EqualTo(f.FacilityWork.ReputationPerSuccess + 3));
            Assert.That(f.Student.GetSkillValue("skill.test"), Is.EqualTo(2));
        }

        [Test]
        public void CompleteWork_BelowSuccessScore_NoBonusNoSkill()
        {
            var f = new Fixture(minimumSuccessScore: 50); f.Transition("transition");
            var result = f.Service.TryCompleteWork(f.FacilityWork, f.Student, f.Progress, f.Inventory, "weak", 10, false, Extras(50, 3, 2));
            Assert.That(result.Kind, Is.EqualTo(AdultCareerResultKind.Applied));
            Assert.That(f.Inventory.CountOf(Fixture.Coin), Is.Zero);
            Assert.That(f.Progress.Reputation, Is.Zero);
            Assert.That(f.Student.GetSkillValue("skill.test"), Is.Zero);
        }

        [Test]
        public void CompleteWork_FullInventory_NoBonusNoSkill()
        {
            var f = new Fixture(); f.Transition("transition");
            for (int i = 0; i < Fixture.MaxSlots; i++) f.Slots.Add("full-" + i, 1);
            var result = f.Service.TryCompleteWork(f.FacilityWork, f.Student, f.Progress, f.Inventory, "job", 100, false, Extras(50, 3, 2));
            Assert.That(result.Kind, Is.EqualTo(AdultCareerResultKind.InsufficientCapacity));
            Assert.That(f.Progress.Reputation, Is.Zero);
            Assert.That(f.Student.GetSkillValue("skill.test"), Is.Zero);
            Assert.That(f.Progress.HasAppliedRequest("job"), Is.False);
        }

        [Test]
        public void ReserveThenSettle_UsesBonusFromReservation()
        {
            var f = new Fixture(); f.Transition("transition");
            Assert.That(f.Service.TryReserveWork(f.FacilityWork, f.Student, f.Progress, "attempt-1", 123, Extras(50)).Applied, Is.True);
            Assert.That(f.Progress.PendingWork.RewardPercent, Is.EqualTo(150));
            var result = f.Service.TrySettleReservedWork(f.FacilityWork, f.Student, f.Progress, f.Inventory, "attempt-1", 100, Extras(0, 3, 2));
            Assert.That(result.Kind, Is.EqualTo(AdultCareerResultKind.Applied));
            Assert.That(f.Inventory.CountOf(Fixture.Coin), Is.EqualTo(30), "20 × 100 × 150% / 10000 — 예약 때의 비율");
            Assert.That(f.Progress.Reputation, Is.EqualTo(f.FacilityWork.ReputationPerSuccess + 3));
            Assert.That(f.Student.GetSkillValue("skill.test"), Is.EqualTo(2));
        }

        [Test]
        public void Reserve_RewardPercentIsCappedAt10000()
        {
            var f = new Fixture(); f.Transition("transition");
            Assert.That(f.Service.TryReserveWork(f.FacilityWork, f.Student, f.Progress, "attempt-1", 123, Extras(int.MaxValue)).Applied, Is.True);
            Assert.That(f.Progress.PendingWork.RewardPercent, Is.EqualTo(10000));
        }

        [Test]
        public void DuplicateRequest_DoesNotAddSkillTwice()
        {
            var f = new Fixture(); f.Transition("transition");
            var extras = Extras(0, 3, 2);
            Assert.That(f.Service.TryCompleteWork(f.FacilityWork, f.Student, f.Progress, f.Inventory, "job", 100, false, extras).Applied, Is.True);
            Assert.That(f.Service.TryCompleteWork(f.FacilityWork, f.Student, f.Progress, f.Inventory, "job", 100, false, extras).Kind, Is.EqualTo(AdultCareerResultKind.DuplicateRequest));
            Assert.That(f.Student.GetSkillValue("skill.test"), Is.EqualTo(2));
            Assert.That(f.Progress.Reputation, Is.EqualTo(f.FacilityWork.ReputationPerSuccess + 3));

            f.Progress = AdultCareerProgress.FromSaveData(f.Progress.ToSaveData());
            f.Student = StudentLifeProgress.FromSaveData(f.Student.ToSaveData()); f.Student.ConfigureCalendar(new LifeCalendar(2, 3));
            Assert.That(f.Service.TryCompleteWork(f.FacilityWork, f.Student, f.Progress, f.Inventory, "job", 100, false, extras).Kind, Is.EqualTo(AdultCareerResultKind.DuplicateRequest));
            Assert.That(f.Student.GetSkillValue("skill.test"), Is.EqualTo(2), "저장·다시 읽기 뒤에도 한 번만");
            Assert.That(f.Progress.Reputation, Is.EqualTo(f.FacilityWork.ReputationPerSuccess + 3));
        }

        [Test]
        public void DefaultExtras_SameAsBefore()
        {
            var f = new Fixture(); f.Transition("transition");
            Assert.That(f.Service.TryCompleteWork(f.FacilityWork, f.Student, f.Progress, f.Inventory, "job", 100, false, default).Applied, Is.True);
            Assert.That(f.Inventory.CountOf(Fixture.Coin), Is.EqualTo(20));
            Assert.That(f.Progress.Reputation, Is.EqualTo(f.FacilityWork.ReputationPerSuccess));
            Assert.That(f.Student.GetSkillIds(), Is.Empty);
        }

        private sealed class AlwaysTransition : IFacilityTransitionRequirement
        {
            public string Coverage => null;
            public bool IsSatisfied(FacilityTransitionContext context) => true;
        }

        private sealed class FakeWorld : ICareerWorldState
        {
            private readonly HashSet<string> _flags = new HashSet<string>();
            public FakeWorld(string slot, string player) { SaveSlot = slot; PlayerId = player; }
            public string SaveSlot { get; }
            public string PlayerId { get; }
            public bool IsActive(string flagId) => _flags.Contains(flagId);
            public bool TryActivate(string flagId, string sourceRequestId, int day) => _flags.Add(flagId);
        }

        private sealed class ReentrantInventory : ICareerInventory
        {
            private readonly ICareerInventory _inner;
            private readonly Action _during;
            public ReentrantInventory(ICareerInventory inner, Action during) { _inner = inner; _during = during; }
            public bool CanExchange(IReadOnlyList<ItemAmount<string>> costs, IReadOnlyList<ItemAmount<string>> rewards) => _inner.CanExchange(costs, rewards);
            public bool TryExchange(IReadOnlyList<ItemAmount<string>> costs, IReadOnlyList<ItemAmount<string>> rewards, Action commit)
                => _inner.TryExchange(costs, rewards, () => { commit?.Invoke(); _during(); });
            public int CountOf(string itemId) => _inner.CountOf(itemId);
        }

        internal sealed class Fixture
        {
            public const string Coin = "currency.fixture";
            public const string Equipment = "equipment.fixture";
            public const int MaxSlots = 32;

            public readonly ItemSlots<string, ItemSlot<string>> Slots = new ItemSlots<string, ItemSlot<string>>(MaxSlots, id => id == Coin ? 999 : 1);
            public ICareerInventory Inventory;
            public ICareerWorldState World = new FakeWorld("slot", "player");
            public readonly Facility Facility;
            public FacilityWork FacilityWork;
            public AdultCareerProgress Progress = new AdultCareerProgress("slot", "player");
            public AdultCareerService Service;
            public StudentLifeProgress Student;
            public CareerCandidateProgress Candidates;

            public Fixture(bool earned = true, IFacilityTransitionRequirement extraTransitionRule = null, int timeCostMinutes = 60, bool withMinigame = true, int minimumSuccessScore = 1, int maximumReward = 20)
            {
                Inventory = new ItemSlotsCareerInventory<ItemSlot<string>>(Slots);
                var candidate = new CareerCandidate("candidate.fixture", null, "fixture", null, null, null, null, null, null, null, revealedHintThreshold: 1, deepeningHintThreshold: 2, readyHintThreshold: 3);
                Candidates = earned ? ReadyCandidates() : new CareerCandidateProgress("slot", "player");
                Student = new StudentLifeProgress("slot", "player", 10, 10);
                Student.ConfigureCalendar(new LifeCalendar(2, 3));
                if (earned) Student.UnlockCareerHint("career.fixture");
                var game = withMinigame ? new Minigame("minigame.fixture", null, new IChallenge[] { new ChoiceChallenge("c", new[] { 0 }, new[] { "p" }, new[] { "a", "b" }) }, null) : null;
                FacilityWork = new FacilityWork("work.fixture", null, "facility.fixture", game, new[] { new ItemAmount<string>(Coin, maximumReward) },
                    new IAutoSkipRequirement[] { new MasteryAutoSkipRequirement(), new SuccessCountAutoSkipRequirement(2), new FacilityEquipmentAutoSkipRequirement(2, Equipment) },
                    timeCostMinutes: timeCostMinutes, maximumMastery: 2, masteryPerSuccess: 1, minimumSuccessScore: minimumSuccessScore);
                var rules = new List<IFacilityTransitionRequirement> { new CareerHintTransitionRequirement(), new CandidateReadyTransitionRequirement() };
                if (extraTransitionRule != null) rules.Add(extraTransitionRule);
                Facility = new Facility("facility.fixture", null, null, null, "career.fixture", candidate, rules.ToArray(), Levels(), new[] { FacilityWork });
                Service = new AdultCareerService(new[] { Facility });
            }

            public static FacilityLevel[] Levels() => new[]
            {
                new FacilityLevel("level-1", null, 1, null, null),
                new FacilityLevel("level-2", null, 2, new[] { new ItemAmount<string>(Coin, 6), new ItemAmount<string>(Coin, 4) }, new[] { "world.fixture.level2" }),
                new FacilityLevel("level-3", null, 3, new[] { new ItemAmount<string>(Coin, 20) }, null, completesRoute: true),
            };

            public static CareerCandidateProgress ReadyCandidates() => CareerCandidateProgress.FromSaveData(new CareerCandidateProgressSaveData
            {
                SaveSlot = "slot", PlayerId = "player",
                Candidates = new[] { new CareerCandidateProgressSaveData.CandidateEntry { CandidateId = "candidate.fixture", UnderstandingScore = 3 } },
            });

            public void Seed(int level, int mastery, int successes)
            {
                var saved = Progress.ToSaveData();
                saved.FacilityLevel = level;
                saved.Works = new[] { new FacilityWorkProgressSaveData { WorkId = "work.fixture", Mastery = mastery, SuccessfulCompletions = successes } };
                Progress = AdultCareerProgress.FromSaveData(saved);
            }

            public AdultCareerResult Transition(string request) => Service.TryTransition(Facility, Student, Candidates, Progress, request);
            public AdultCareerResult Work(string request, int score, bool skip) => Service.TryCompleteWork(FacilityWork, Student, Progress, Inventory, request, score, skip);
            public AdultCareerResult Upgrade(string request) => Service.TryUpgrade(Facility, Student, Progress, Inventory, World, request);
            public AdultCareerResult Reserve(string request) => Service.TryReserveWork(FacilityWork, Student, Progress, request, 123);
            public AdultCareerResult Settle(string request, int score) => Service.TrySettleReservedWork(FacilityWork, Student, Progress, Inventory, request, score);
        }
    }
}
