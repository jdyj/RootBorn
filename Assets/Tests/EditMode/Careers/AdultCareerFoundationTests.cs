using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.StudentLife;
using Rootborn.Game.WorldState;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Careers
{
    // Reflection is intentional: this fixture must compile and fail before new production types exist.
    public sealed class AdultCareerFoundationTests
    {
        private readonly List<UnityEngine.Object> _objects = new List<UnityEngine.Object>();
        private const string Domain = "Rootborn.Game.Careers.";
        [TearDown] public void TearDown() { foreach (var value in _objects) UnityEngine.Object.DestroyImmediate(value); _objects.Clear(); }

        [Test] public void ADULT_001_TransitionRequiresEarnedHintAndReadyCandidate()
        {
            var f = Fixture(false);
            AssertKind("RequirementFailed", Transition(f, "transition"));
            Assert.That(Get<bool>(f.Progress, "IsAdult"), Is.False);
            f.Student.UnlockCareerHint(f.Career);
            AssertKind("RequirementFailed", Transition(f, "transition"));
            f.Candidates = ReadyCandidate(f.Candidate);
            AssertKind("Applied", Transition(f, "transition"));
            Assert.That(Get<int>(f.Progress, "FacilityLevel"), Is.EqualTo(1));
        }

        [Test] public void ADULT_002_TransitionPreservesStudentGrowthAndRejectsSecondFacility()
        {
            var f = Fixture();
            var trait = New<TraitDefinition>(); trait.ConfigureForTests("trait.fixture", "trait.fixture"); f.Student.AddTrait(trait, 17);
            AssertKind("Applied", Transition(f, "first"));
            AssertKind("DuplicateRequest", Transition(f, "first"));
            AssertKind("AlreadyAdult", Transition(f, "different-request"));
            Assert.That(f.Student.GetTraitValue(trait), Is.EqualTo(17));
        }

        [Test] public void ADULT_003_EarliestDayRuleIsConfigurableAndDoesNotConsumeRequest()
        {
            var f = Fixture(); var rule = New("MinimumDayTransitionRequirement"); Set(rule, "_minimumDay", 2);
            AppendRule(f.Facility, "_transitionRequirements", "FacilityTransitionRequirementBase", rule);
            AssertKind("RequirementFailed", Transition(f, "same-request"));
            f.Student.TryEndDay(null, out _); f.Student.TryStartNextDay();
            AssertKind("Applied", Transition(f, "same-request"));
        }

        [Test] public void ADULT_004_WorkConsumesSharedTimeAndAwardsDeclaredMaximumExactlyOnce()
        {
            var f = Fixture(); Transition(f, "transition");
            AssertKind("Applied", Work(f, "job", 100, false));
            Assert.That(f.Inventory.CountOf(f.Coin), Is.EqualTo(20));
            Assert.That(Get<int>(f.Student, "TimePointsRemaining"), Is.EqualTo(1));
            Assert.That((int)Call(f.Progress, "GetSuccessfulCompletions", f.Work), Is.EqualTo(1));
            AssertKind("DuplicateRequest", Work(f, "job", 100, false));
            Assert.That(f.Inventory.CountOf(f.Coin), Is.EqualTo(20));
            Assert.That(Get<int>(f.Student, "TimePointsRemaining"), Is.EqualTo(1));
        }

        [Test] public void ADULT_018_DirectWorkAppearsInDailyActivityExactlyOnce()
        {
            var f=Fixture(); Transition(f,"transition"); AssertKind("Applied",Work(f,"recorded-work",100,false));
            Assert.That(f.Student.GetTodayActivityIds(),Is.EqualTo(new[]{(string)Get<string>(f.Work,"Id")}));
            AssertKind("DuplicateRequest",Work(f,"recorded-work",100,false));
            Assert.That(f.Student.GetTodayActivityIds(),Has.Length.EqualTo(1));
        }
        [Test] public void ADULT_019_WorkAdvancesTheClockByItsMinutesOnlyWhenApplied()
        {
            var f = Fixture(); Transition(f, "transition"); Set(f.Work, "_timeCostMinutes", 90);
            int start = f.Student.TimeMinutes;
            AssertKind("Applied", Work(f, "clock", 100, false));
            Assert.That(f.Student.TimeMinutes, Is.EqualTo(start + 90), "a finished work shift must move the clock");
            AssertKind("DuplicateRequest", Work(f, "clock", 100, false));
            Assert.That(f.Student.TimeMinutes, Is.EqualTo(start + 90), "a repeated request must not move the clock again");
            for (int i = 0; i < Inventory.MaxSlots; i++) f.Inventory.Add(Item("full-" + i, 1), 1);
            AssertKind("InsufficientCapacity", Work(f, "clock-full", 100, false));
            Assert.That(f.Student.TimeMinutes, Is.EqualTo(start + 90), "a refused work must not move the clock");
        }

        [Test] public void ADULT_005_FullInventoryPreflightPreservesTimeMasteryAndRequest()
        {
            var f = Fixture(); Transition(f, "transition");
            for (int i = 0; i < Inventory.MaxSlots; i++) f.Inventory.Add(Item("full-" + i, 1), 1);
            AssertKind("InsufficientCapacity", Work(f, "retry", 100, false));
            Assert.That(Get<int>(f.Student, "TimePointsRemaining"), Is.EqualTo(2));
            Assert.That((int)Call(f.Progress, "GetMastery", f.Work), Is.Zero);
            f.Inventory.Clear(); AssertKind("Applied", Work(f, "retry", 100, false));
        }

        [Test] public void ADULT_006_TimeBudgetExhaustionDoesNotPayOrAdvanceMastery()
        {
            var f = Fixture(); Transition(f, "transition"); Call(f.Student, "TrySpendTimePoints", 2);
            AssertKind("InsufficientTime", Work(f, "job", 100, false));
            Assert.That(f.Inventory.CountOf(f.Coin), Is.Zero);
            Assert.That((int)Call(f.Progress, "GetMastery", f.Work), Is.Zero);
        }

        [TestCase(1, 2, 2, true)]
        [TestCase(2, 1, 2, true)]
        [TestCase(2, 2, 1, true)]
        [TestCase(2, 2, 2, false)]
        public void ADULT_007_AutoSkipRequiresMasterySuccessCountFacilityAndEquipmentTogether(int mastery, int successCount, int level, bool equipment)
        {
            var f = Fixture(); Transition(f, "transition"); SeedProgress(f, level, mastery, successCount);
            if (equipment) f.Inventory.Add(f.Equipment);
            AssertKind("RequirementFailed", Work(f, "skip", 0, true));
            Assert.That(f.Inventory.CountOf(f.Coin), Is.Zero);
            Assert.That(Get<int>(f.Student, "TimePointsRemaining"), Is.EqualTo(2));
        }

        [Test] public void ADULT_008_AutoSkipAwardsMaximumRewardNotSeventyPercent()
        {
            var f = Fixture(); Transition(f, "transition"); SeedProgress(f, 2, 2, 2); f.Inventory.Add(f.Equipment);
            AssertKind("Applied", Work(f, "skip", 0, true));
            Assert.That(f.Inventory.CountOf(f.Coin), Is.EqualTo(20));
            Assert.That(Get<int>(f.Student, "TimePointsRemaining"), Is.EqualTo(1));
        }

        [Test] public void ADULT_009_UpgradeConsumesAggregatedCostsAndActivatesWorldChange()
        {
            var f = Fixture(); Transition(f, "transition"); f.Inventory.Add(f.Coin, 10);
            AssertKind("Applied", Upgrade(f, "upgrade"));
            Assert.That(Get<int>(f.Progress, "FacilityLevel"), Is.EqualTo(2));
            Assert.That(f.Inventory.CountOf(f.Coin), Is.Zero);
            Assert.That(f.World.IsActive(f.Flag2), Is.True);
            AssertKind("DuplicateRequest", Upgrade(f, "upgrade"));
        }

        [Test] public void ADULT_010_UpgradeWithInsufficientDuplicateItemCostsIsAtomic()
        {
            var f = Fixture(); Transition(f, "transition"); f.Inventory.Add(f.Coin, 9);
            AssertKind("InsufficientResources", Upgrade(f, "upgrade"));
            Assert.That(f.Inventory.CountOf(f.Coin), Is.EqualTo(9));
            Assert.That(Get<int>(f.Progress, "FacilityLevel"), Is.EqualTo(1));
            Assert.That(f.World.IsActive(f.Flag2), Is.False);
            f.Inventory.Add(f.Coin); AssertKind("Applied", Upgrade(f, "upgrade"));
        }

        [Test] public void ADULT_011_LevelThreeRecordsOnlySelectedRouteEndingOnce()
        {
            var f = Fixture(); Transition(f, "transition"); f.Inventory.Add(f.Coin, 30);
            AssertKind("Applied", Upgrade(f, "upgrade-2")); AssertKind("Applied", Upgrade(f, "upgrade-3"));
            Assert.That(Get<int>(f.Progress, "FacilityLevel"), Is.EqualTo(3));
            Assert.That((bool)Call(f.Progress, "HasEnding", f.Facility), Is.True);
            Assert.That(Get<string[]>(f.Progress, "EndingIds").Length, Is.EqualTo(1));
            AssertKind("AtMaximumLevel", Upgrade(f, "upgrade-4"));
        }

        [Test] public void ADULT_012_SaveRoundTripPreservesWorkClaimEndingAndStudentBudget()
        {
            var f = Fixture(); Transition(f, "transition"); Work(f, "job", 100, false); f.Inventory.Add(f.Coin, 20);
            Upgrade(f, "upgrade-2"); Upgrade(f, "upgrade-3");
            object saved = Call(f.Progress, "ToSaveData"); string json = JsonUtility.ToJson(saved);
            f.Progress = CallStatic("AdultCareerProgress", "FromSaveData", JsonUtility.FromJson(json, saved.GetType()));
            var studentSaved = f.Student.ToSaveData(); f.Student = StudentLifeProgress.FromSaveData(studentSaved, null, null, null); BindCalendar(f.Student);
            AssertKind("DuplicateRequest", Work(f, "job", 100, false));
            Assert.That(Get<int>(f.Student, "TimePointsRemaining"), Is.EqualTo(1));
            Assert.That((bool)Call(f.Progress, "HasEnding", f.Facility), Is.True);
            Assert.That((int)Call(f.Progress, "GetSuccessfulCompletions", f.Work), Is.EqualTo(1));
        }

        [Test] public void ADULT_013_UnregisteredFacilityCannotTransition()
        {
            var f = Fixture(); f.Service = Construct("AdultCareerService", ArrayOf("FacilityDefinition"));
            AssertKind("InvalidDefinition", Transition(f, "transition"));
        }

        [Test] public void ADULT_014_ForgedWorkCannotAwardRewardsEvenForSameFacility()
        {
            var f = Fixture(); Transition(f, "transition"); var work = New("FacilityWorkDefinition"); Set(work, "_id", "forged"); Set(work, "_facility", f.Facility); f.Work = work;
            AssertKind("InvalidDefinition", Work(f, "forged", 100, false));
        }

        [Test] public void ADULT_015_CrossPlayerGrowthCannotAuthorizeTransition()
        {
            var f = Fixture(); f.Progress = Construct("AdultCareerProgress", "slot", "other-player");
            AssertKind("InvalidRequest", Transition(f, "transition"));
        }

        private sealed class Data
        {
            public object Facility, Work, Progress, Service;
            public CareerDefinition Career; public CareerCandidateDefinition Candidate; public CareerCandidateProgress Candidates;
            public StudentLifeProgress Student; public Inventory Inventory = new Inventory();
            public ItemDefinition Coin, Equipment; public WorldStateFlagDefinition Flag2; public WorldStateProgress World = new WorldStateProgress("slot", "player");
        }

        private Data Fixture(bool earned = true)
        {
            var f = new Data(); f.Progress = Construct("AdultCareerProgress", "slot", "player");
            f.Career = New<CareerDefinition>(); f.Career.ConfigureForTests("career.fixture", "career.fixture", null);
            f.Candidate = New<CareerCandidateDefinition>(); f.Candidate.ConfigureForTests("candidate.fixture", "candidate.fixture", "fixture", null, null, null, revealedHintThreshold: 1, deepeningHintThreshold: 2, readyHintThreshold: 3);
            f.Candidates = earned ? ReadyCandidate(f.Candidate) : new CareerCandidateProgress("slot", "player");
            f.Student = new StudentLifeProgress("slot", "player", 10, 10); BindCalendar(f.Student); if (earned) f.Student.UnlockCareerHint(f.Career);
            f.Coin = Item("currency.fixture", 999); f.Equipment = Item("equipment.fixture", 1);
            f.Flag2 = New<WorldStateFlagDefinition>(); Set(f.Flag2, "_id", "world.fixture.level2");
            f.Facility = New("FacilityDefinition"); Set(f.Facility, "_id", "facility.fixture"); Set(f.Facility, "_career", f.Career); Set(f.Facility, "_candidate", f.Candidate);
            var hintRule = New("CareerHintTransitionRequirement"); var candidateRule = New("CandidateReadyTransitionRequirement");
            Set(f.Facility, "_transitionRequirements", ArrayOf("FacilityTransitionRequirementBase", hintRule, candidateRule));
            var levels = new object[3]; for (int i = 0; i < 3; i++) { levels[i] = New("FacilityLevelDefinition"); Set(levels[i], "_id", "level-" + (i + 1)); Set(levels[i], "_level", i + 1); }
            Set(levels[1], "_costItems", new[] { f.Coin, f.Coin }); Set(levels[1], "_costCounts", new[] { 6, 4 }); Set(levels[1], "_worldFlags", new[] { f.Flag2 });
            Set(levels[2], "_costItems", new[] { f.Coin }); Set(levels[2], "_costCounts", new[] { 20 }); Set(levels[2], "_completesRoute", true);
            Set(f.Facility, "_levels", ArrayOf("FacilityLevelDefinition", levels));
            f.Work = New("FacilityWorkDefinition"); Set(f.Work, "_id", "work.fixture"); Set(f.Work, "_facility", f.Facility); Set(f.Work, "_timePointCost", 1);
            Set(f.Work, "_maximumMastery", 2); Set(f.Work, "_masteryPerSuccess", 1); Set(f.Work, "_minimumSuccessScore", 1);
            Set(f.Work, "_rewardItems", new[] { f.Coin }); Set(f.Work, "_maximumRewardCounts", new[] { 20 });
            var mastery = New("MasteryAutoSkipRequirement"); var successes = New("SuccessCountAutoSkipRequirement"); Set(successes, "_minimumSuccesses", 2);
            var equipment = New("FacilityEquipmentAutoSkipRequirement"); Set(equipment, "_minimumLevel", 2); Set(equipment, "_equipment", f.Equipment);
            Set(f.Work, "_autoSkipRequirements", ArrayOf("AutoSkipRequirementBase", mastery, successes, equipment));
            Set(f.Facility, "_works", ArrayOf("FacilityWorkDefinition", f.Work));
            f.Service = Construct("AdultCareerService", ArrayOf("FacilityDefinition", f.Facility)); return f;
        }

        private CareerCandidateProgress ReadyCandidate(CareerCandidateDefinition candidate) => CareerCandidateProgress.FromSaveData(new CareerCandidateProgressSaveData { SaveSlot = "slot", PlayerId = "player", Candidates = new[] { new CareerCandidateProgressSaveData.CandidateEntry { CandidateId = candidate.Id, UnderstandingScore = 3 } } });
        private void BindCalendar(StudentLifeProgress student) { var type = typeof(StudentLifeProgress).Assembly.GetType("Rootborn.Game.StudentLife.LifeCalendarDefinition"); Assert.That(type, Is.Not.Null, "Shared LifeCalendarDefinition missing"); var calendar = ScriptableObject.CreateInstance(type); _objects.Add(calendar); Call(calendar, "ConfigureForTests", 2, 3); Call(student, "ConfigureCalendar", calendar); }
        private void SeedProgress(Data f, int level, int mastery, int successes) { var saved = Call(f.Progress, "ToSaveData"); Set(saved, "FacilityLevel", level); var record = Construct("FacilityWorkProgressSaveData"); Set(record, "WorkId", "work.fixture"); Set(record, "Mastery", mastery); Set(record, "SuccessfulCompletions", successes); Set(saved, "Works", ArrayOf("FacilityWorkProgressSaveData", record)); f.Progress = CallStatic("AdultCareerProgress", "FromSaveData", saved); }
        private object Transition(Data f, string request) => Call(f.Service, "TryTransition", f.Facility, f.Student, f.Candidates, f.Progress, request);
        private object Work(Data f, string request, int score, bool skip) => Call(f.Service, "TryCompleteWork", f.Work, f.Student, f.Progress, f.Inventory, request, score, skip);
        private object Upgrade(Data f, string request) => Call(f.Service, "TryUpgrade", f.Facility, f.Student, f.Progress, f.Inventory, f.World, request);
        private void AppendRule(object target, string field, string baseName, object value) { var old = (Array)Field(target.GetType(), field).GetValue(target); var values = new object[old.Length + 1]; for (int i = 0; i < old.Length; i++) values[i] = old.GetValue(i); values[old.Length] = value; Set(target, field, ArrayOf(baseName, values)); }
        private T New<T>() where T : ScriptableObject { var value = ScriptableObject.CreateInstance<T>(); _objects.Add(value); return value; }
        private object New(string name) { var value = ScriptableObject.CreateInstance(Type(name)); _objects.Add(value); return value; }
        private ItemDefinition Item(string id, int stack) { var item = New<ItemDefinition>(); Set(item, "_id", id); Set(item, "_maxStack", stack); return item; }
        private static Type Type(string name) { var type = typeof(StudentLifeProgress).Assembly.GetType(Domain + name); Assert.That(type, Is.Not.Null, Domain + name + " is not implemented"); return type; }
        private static object Construct(string name, params object[] args) => Activator.CreateInstance(Type(name), args);
        private static Array ArrayOf(string name, params object[] values) { var array = Array.CreateInstance(Type(name), values.Length); for (int i = 0; i < values.Length; i++) array.SetValue(values[i], i); return array; }
        private static FieldInfo Field(Type type, string name) { var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance); Assert.That(field, Is.Not.Null, type.Name + "." + name); return field; }
        private static void Set(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);
        private static T Get<T>(object target, string name) { var prop = target.GetType().GetProperty(name); Assert.That(prop, Is.Not.Null, target.GetType().Name + "." + name); return (T)prop.GetValue(target); }
        private static object Call(object target, string name, params object[] args) { var method = target.GetType().GetMethod(name); Assert.That(method, Is.Not.Null, target.GetType().Name + "." + name); return method.Invoke(target, args); }
        private static object CallStatic(string type, string name, params object[] args) { var method = Type(type).GetMethod(name, BindingFlags.Public | BindingFlags.Static); Assert.That(method, Is.Not.Null, type + "." + name); return method.Invoke(null, args); }
        private static void AssertKind(string expected, object result) => Assert.That(Get<object>(result, "Kind").ToString(), Is.EqualTo(expected));
    }
}
