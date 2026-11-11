using System;
using NUnit.Framework;
using Rootborn.Core.Careers;
using Rootborn.Core.Inventory;
using Rootborn.Core.Minigames;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Tests
{
    // 옮겨 옴: Assets/Tests/EditMode/StudentLife/PartTimeWorkGrowthTests.cs, Assets/Tests/EditMode/Careers/StudentMinigameReservationTests.cs의 알바 정산(003, 003b)
    // (등록부·소스 문자열 검사와 씬 상호작용 검사는 옮기지 않음)
    public sealed class PartTimeWorkTests
    {
        private static readonly Workplace Store = new Workplace("workplace.corner-store", "workplace.corner-store", "location.store", "npc.owner", null);

        private static PartTimeWork Work(string id, int time, int energy, int focus, int stress, InventoryGrant[] rewards, IWorkRequirement[] requirements = null, IWorkOutcome[] outcomes = null,
            Minigame minigame = null, ActivityTimePolicy timePolicy = null)
            => new PartTimeWork(id, id, minigame, Store, timePolicy, time, energy, focus, stress, rewards, requirements, outcomes);

        private static ItemInventory FullInventoryWithDifferentItems()
        {
            var inventory = new ItemInventory();
            for (int i = 0; i < ItemInventory.MaxSlots; i++) inventory.Add(new ItemDefinition("item.filler." + i, 1), 1);
            return inventory;
        }

        [Test]
        public void WORK_EDIT_002_RequirementsAndOutcomeStrategiesRunWithoutEntityIdBranching()
        {
            var requirement = new WorkTraitThresholdRequirement("trait.responsibility", 2);
            var progress = new StudentLifeProgress("slot-work", "player-work", 8, 8);

            Assert.IsFalse(requirement.IsSatisfied(progress));
            progress.AddTrait("trait.responsibility", 2);
            Assert.IsTrue(requirement.IsSatisfied(progress));
        }

        [Test]
        public void WORK_EDIT_003_RewardPreflightBlocksFullInventoryWithoutMutatingProgressOrInventory()
        {
            var wage = new ItemDefinition("item.wage-token", 99);
            var work = Work("work.shift.store", 45, 1, 1, 1, new[] { new InventoryGrant(wage, 1) }, outcomes: new IWorkOutcome[] { new WorkTraitDeltaOutcome("trait.responsibility", 3) });
            var progress = new StudentLifeProgress("slot-work", "player-work", 4, 4);
            var inventory = FullInventoryWithDifferentItems();

            Assert.IsFalse(new PartTimeWorkRunner().TryPerform(work, progress, inventory, "work-request-1", out var result));
            Assert.AreEqual(LifeActivityResultKind.InsufficientResources, result.Kind);
            Assert.AreEqual(4, progress.Energy);
            Assert.AreEqual(4, progress.Focus);
            Assert.AreEqual(0, progress.GetTraitValue("trait.responsibility"));
            Assert.AreEqual(0, inventory.CountOf(wage));
            Assert.IsFalse(progress.HasAppliedRequest("work-request-1"));
        }

        [Test]
        public void WORK_EDIT_004b_SameWorkTwiceKeepsBothDayLogs()
        {
            var wage = new ItemDefinition("item.wage-token", 99);
            var work = Work("work.shift.store", 30, 1, 1, 1, new[] { new InventoryGrant(wage, 3) }, outcomes: new IWorkOutcome[0]);
            var progress = new StudentLifeProgress("slot-work", "player-work", 5, 5);
            var inventory = new ItemInventory();
            var runner = new PartTimeWorkRunner();

            Assert.IsTrue(runner.TryPerform(work, progress, inventory, "work-request-a", out _));
            Assert.IsTrue(runner.TryPerform(work, progress, inventory, "work-request-b", out _));
            Assert.AreEqual(2, Array.FindAll(progress.GetTodayResultLogIds(), log => log == "part-time-work:work.shift.store:+item.wage-token=3").Length);
        }

        [Test]
        public void WORK_EDIT_004_WorkAppliesItemRewardCostAndGenericGrowthOutcome()
        {
            var wage = new ItemDefinition("item.wage-token", 99);
            var work = Work("work.shift.store", 30, 2, 1, 1, new[] { new InventoryGrant(wage, 3) }, outcomes: new IWorkOutcome[] { new WorkTraitDeltaOutcome("trait.responsibility", 2) });
            var progress = new StudentLifeProgress("slot-work", "player-work", 5, 5);
            var inventory = new ItemInventory();

            Assert.IsTrue(new PartTimeWorkRunner().TryPerform(work, progress, inventory, "work-request-2", out var result));
            string expectedGrowth = "part-time-work:work.shift.store:+trait.responsibility=2";
            string expectedReward = "part-time-work:work.shift.store:+item.wage-token=3";
            Assert.AreEqual(LifeActivityResultKind.Applied, result.Kind);
            Assert.AreEqual("work.shift.store", result.WorkId);
            Assert.AreEqual("workplace.corner-store", result.WorkplaceId);
            Assert.AreEqual("location.store", result.LocationId);
            Assert.AreEqual("npc.owner", result.NpcId);
            Assert.AreEqual(3, progress.Energy);
            Assert.AreEqual(4, progress.Focus);
            Assert.AreEqual(1, progress.Stress);
            Assert.AreEqual(3, inventory.CountOf(wage));
            Assert.AreEqual(2, progress.GetTraitValue("trait.responsibility"));
            CollectionAssert.Contains(result.OutcomeLogIds, expectedGrowth);
            CollectionAssert.Contains(result.OutcomeLogIds, expectedReward);
            CollectionAssert.Contains(progress.GetTodayActivityIds(), "work.shift.store");
            CollectionAssert.Contains(progress.GetTodayResultLogIds(), expectedGrowth);
            CollectionAssert.Contains(progress.GetTodayResultLogIds(), expectedReward);
        }

        [Test]
        public void WORK_EDIT_005_SaveLoadKeepsRequestAppliedAndPreventsDuplicateReward()
        {
            var wage = new ItemDefinition("item.wage-token", 99);
            var work = Work("work.shift.store", 30, 0, 0, 0, new[] { new InventoryGrant(wage, 2) });
            var progress = new StudentLifeProgress("slot-work", "player-work", 5, 5);
            var inventory = new ItemInventory();
            var runner = new PartTimeWorkRunner();

            Assert.IsTrue(runner.TryPerform(work, progress, inventory, "same-work-request", out var first));
            var restored = StudentLifeProgress.FromSaveData(progress.ToSaveData());
            Assert.IsFalse(runner.TryPerform(work, restored, inventory, "same-work-request", out var duplicate));

            Assert.AreEqual(LifeActivityResultKind.Applied, first.Kind);
            Assert.AreEqual(LifeActivityResultKind.DuplicateRequest, duplicate.Kind);
            Assert.AreEqual(2, inventory.CountOf(wage));
            Assert.AreEqual(1, PartTimeWorkDayLog.FromResultLogs(restored.GetTodayResultLogIds()).WorkIds.Length);
        }

        // 미니게임 알바: main의 StudentMinigameService에 실제 정산(PartTimeWorkPayroll)을 잇는다.

        private sealed class MinigameFixture
        {
            public readonly StudentLifeProgress Student = new StudentLifeProgress("slot", "player", 12, 10, 0, 480);
            public readonly AdultCareerProgress Adult = new AdultCareerProgress("slot", "player");
            public readonly ItemDefinition Coin = new ItemDefinition("item.coin", 99);
            public readonly ItemInventory Inventory = new ItemInventory();
            public readonly PartTimeWork Work;
            public readonly PartTimeWorkPayroll Payroll;
            public readonly StudentMinigameService Service;

            public MinigameFixture()
            {
                Student.ConfigureCalendar(new LifeCalendar(3, 3));
                var policy = new ActivityTimePolicy("work-time", 1, 127, false);
                var game = new Minigame("minigame.fixture", null, new IChallenge[] { new ChoiceChallenge("c", new[] { 0 }, new[] { "prompt" }, new[] { "a", "b" }) }, null);
                Work = Work("job.fixture", 30, 1, 0, 0, new[] { new InventoryGrant(Coin, 5) }, outcomes: new IWorkOutcome[] { new WorkTraitDeltaOutcome("trait.fixture", 1) }, minigame: game, timePolicy: policy);
                Payroll = new PartTimeWorkPayroll(Inventory);
                Service = new StudentMinigameService(null, new IMinigamePartTimeWork[] { Work }, null, null);
            }

            public AdultCareerResult Begin(string request) => Service.TryBeginPartTimeWork(Work, Student, Adult, Payroll, request, 123);
            public AdultCareerResult Settle(string request) => Service.TrySettlePartTimeWork(Work, Student, Payroll, request, true);
        }

        [Test]
        public void STUDENT_GAME_003_PartTimeSuccessPaysOnceWithGrowthAndDailyWorkLogs()
        {
            var f = new MinigameFixture();
            Assert.IsTrue(f.Begin("job-1").Applied);
            Assert.AreEqual(0, f.Inventory.CountOf(f.Coin));

            Assert.IsTrue(f.Settle("job-1").Applied);
            Assert.AreEqual(5, f.Inventory.CountOf(f.Coin));
            Assert.AreEqual(1, f.Student.GetTraitValue("trait.fixture"));
            Assert.AreEqual(2, f.Student.TimePointsRemaining);
            CollectionAssert.Contains(f.Student.GetTodayActivityIds(), f.Work.Id);
            CollectionAssert.Contains(PartTimeWorkDayLog.FromResultLogs(f.Student.GetTodayResultLogIds()).WorkIds, f.Work.Id);
            CollectionAssert.AreEqual(new[] { f.Work.Id }, new PartTimeWorkLogReader().ReadWorkIds(f.Student.GetTodayResultLogIds()));
            Assert.IsFalse(f.Settle("job-1").Applied);
            Assert.AreEqual(5, f.Inventory.CountOf(f.Coin));
        }

        [Test]
        public void STUDENT_GAME_003b_SameJobTwiceKeepsBothPaymentsInTheDayLog()
        {
            var f = new MinigameFixture();
            Assert.IsTrue(f.Begin("job-1").Applied);
            Assert.IsTrue(f.Settle("job-1").Applied);
            Assert.IsTrue(f.Begin("job-2").Applied);
            Assert.IsTrue(f.Settle("job-2").Applied);

            Assert.AreEqual(10, f.Inventory.CountOf(f.Coin));
            string paid = PartTimeWorkLogCodec.EncodeDelta(f.Work.Id, f.Coin.Id, 5);
            Assert.AreEqual(2, Array.FindAll(f.Student.GetTodayResultLogIds(), log => log == paid).Length);
        }

        // 더한 경계

        [Test]
        public void PayrollRefusesFullInventoryAtBeginAndAtSettleWithoutPaying()
        {
            var f = new MinigameFixture();
            Assert.IsTrue(f.Payroll.CanReceiveRewards(f.Work));
            Assert.IsTrue(f.Begin("job-1").Applied);
            for (int i = 0; i < ItemInventory.MaxSlots; i++) f.Inventory.Add(new ItemDefinition("item.filler." + i, 1), 1);

            Assert.IsFalse(f.Payroll.CanReceiveRewards(f.Work));
            Assert.IsFalse(f.Settle("job-1").Applied);
            Assert.AreEqual(0, f.Inventory.CountOf(f.Coin));
            Assert.AreEqual(0, f.Student.GetTraitValue("trait.fixture"));
            Assert.IsFalse(f.Student.HasAppliedRequest("job-1"));
            Assert.IsTrue(f.Student.HasPendingMinigame);
        }

        [Test]
        public void ReservedSettleNeedsMatchingReservationAndValidRewards()
        {
            var f = new MinigameFixture();
            var runner = new PartTimeWorkRunner();
            Assert.IsFalse(runner.TryPerformReserved(f.Work, f.Student, f.Inventory, "job-1", out var noReservation));
            Assert.AreEqual(LifeActivityResultKind.InvalidRequest, noReservation.Kind);
            Assert.IsTrue(f.Begin("job-1").Applied);
            Assert.IsFalse(runner.TryPerformReserved(f.Work, f.Student, f.Inventory, "other-request", out _));

            var broken = Work("job.fixture", 0, 0, 0, 0, new[] { new InventoryGrant(f.Coin, 0) }, minigame: f.Work.Minigame);
            Assert.IsFalse(runner.TryPerformReserved(broken, f.Student, f.Inventory, "job-1", out _));
            Assert.IsFalse(f.Payroll.TryPerformReserved(new OtherWork(), f.Student, "job-1"));
            Assert.IsFalse(f.Payroll.CanReceiveRewards(new OtherWork()));
            Assert.AreEqual(0, f.Inventory.CountOf(f.Coin));
            Assert.IsFalse(f.Student.HasAppliedRequest("job-1"));
        }

        private sealed class OtherWork : IMinigamePartTimeWork
        {
            public string Id => "job.fixture";
            public Minigame Minigame => null;
            public ActivityTimePolicy TimePolicy => null;
            public int TimeCostMinutes => 0;
            public int EnergyCost => 0;
            public int FocusCost => 0;
            public int StressDelta => 0;
            public bool HasSatisfiedRequirements(StudentLifeProgress progress) => true;
        }

        [Test]
        public void DirectRunRejectsMinigameWorkMissingInputsRequirementsAndCosts()
        {
            var wage = new ItemDefinition("item.wage-token", 99);
            var progress = new StudentLifeProgress("slot", "player", 2, 2);
            var inventory = new ItemInventory();
            var runner = new PartTimeWorkRunner();
            var minigameWork = Work("w", 0, 0, 0, 0, null, minigame: new Minigame("m", null, null, null));
            var locked = Work("w", 0, 0, 0, 0, new[] { new InventoryGrant(wage, 1) }, new IWorkRequirement[] { new WorkRelationshipThresholdRequirement("relationship.boss", 1) });
            var tiring = Work("w", 0, 5, 0, 0, new[] { new InventoryGrant(wage, 1) });

            Assert.IsFalse(runner.TryPerform(minigameWork, progress, inventory, "r", out var viaGame));
            Assert.AreEqual(LifeActivityResultKind.InvalidRequest, viaGame.Kind);
            Assert.IsFalse(runner.TryPerform(tiring, progress, null, "r", out _));
            Assert.IsFalse(runner.TryPerform(tiring, progress, inventory, "", out _));
            Assert.IsFalse(runner.TryPerform(locked, progress, inventory, "r", out var blocked));
            Assert.AreEqual(LifeActivityResultKind.RequirementFailed, blocked.Kind);
            Assert.IsFalse(runner.TryPerform(tiring, progress, inventory, "r", out var poor));
            Assert.AreEqual(LifeActivityResultKind.InsufficientResources, poor.Kind);
            Assert.AreEqual(0, inventory.CountOf(wage));
            Assert.AreEqual(2, progress.Energy);
            Assert.IsFalse(progress.HasAppliedRequest("r"));
        }

        [Test]
        public void OutcomesLogDeltasAndCareerHintLogsOnlyFirstUnlock()
        {
            var progress = new StudentLifeProgress("slot", "player", 8, 8);
            Assert.AreEqual("part-time-work:w:+skill.s=2", new WorkSkillDeltaOutcome("skill.s", 2).Apply(progress, "w"));
            Assert.AreEqual("part-time-work:w:+relationship.r=1", new WorkRelationshipDeltaOutcome("relationship.r", 1).Apply(progress, "w"));
            Assert.AreEqual("part-time-work:w:+status.tired=3", new WorkStatusDeltaOutcome("status.tired", 3).Apply(progress, "w"));
            Assert.AreEqual("part-time-work:w:+career.clerk=1", new WorkCareerHintOutcome("career.clerk").Apply(progress, "w"));
            Assert.AreEqual(string.Empty, new WorkCareerHintOutcome("career.clerk").Apply(progress, "w"));
            Assert.IsTrue(progress.IsCareerHintUnlocked("career.clerk"));
            Assert.IsTrue(new WorkSkillThresholdRequirement("skill.s", 2).IsSatisfied(progress));
            Assert.IsTrue(new WorkStatusThresholdRequirement("status.tired", 3).IsSatisfied(progress));
            Assert.AreEqual(string.Empty, new WorkTraitDeltaOutcome("t", 0).Apply(progress, "w"));
        }

        [Test]
        public void SummaryAndAggregateUseTheSameLogFormat()
        {
            var logs = new[] { "part-time-work:w:+item.coin=3", "other:x", "part-time-work:w:+item.coin=2", "part-time-work:bad" };
            Assert.AreEqual(2, PartTimeWorkGrowthSummary.FromResultLogs(logs).Entries.Length);
            CollectionAssert.AreEqual(new[] { "part-time-work:w:+item.coin=5", "other:x", "part-time-work:bad" }, PartTimeWorkLogCodec.AggregateDeltas(logs));
            CollectionAssert.AreEqual(new[] { "w" }, PartTimeWorkDayLog.FromResultLogs(logs).WorkIds);
            Assert.AreEqual("workplace.corner-store", Store.Id);
        }
    }
}
