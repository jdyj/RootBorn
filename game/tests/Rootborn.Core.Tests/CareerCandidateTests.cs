using System;
using NUnit.Framework;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Tests
{
    // 옮겨 옴: Assets/Tests/EditMode/StudentLife/CareerCandidateDeepeningLoopTests.cs (CAREER_CANDIDATE_EDIT_002, 004~008),
    //          Assets/Tests/EditMode/StudentLife/CareerHintDailyResetTests.cs
    public sealed class CareerCandidateTests
    {
        [Test]
        public void CAREER_CANDIDATE_EDIT_002_FourCandidatesCanExistAsData()
        {
            var set = CandidateSet.Create();
            Assert.AreEqual(4, set.Candidates.Length);
            CollectionAssert.AreEquivalent(new[] { "candidate.learning", "candidate.technical", "candidate.service", "candidate.investigation" }, Array.ConvertAll(set.Candidates, c => c.Id));
            foreach (var candidate in set.Candidates)
            {
                Assert.GreaterOrEqual(candidate.Routes.Count, 1);
                Assert.GreaterOrEqual(candidate.RecommendedActionKeys.Count, 1);
            }
        }

        [Test]
        public void CAREER_CANDIDATE_EDIT_004_AtLeastOneCandidateAcceptsTwoAlternativeRoutes()
        {
            var set = CandidateSet.Create();
            Assert.GreaterOrEqual(set.Learning.Routes.Count, 2);
            Assert.AreNotSame(set.Learning.Routes[0], set.Learning.Routes[1]);
        }

        [Test]
        public void CAREER_CANDIDATE_EDIT_005_OutsideSchoolActivityLogCanIncreaseCandidateHintWithoutClassRoute()
        {
            var set = CandidateSet.Create();
            var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            var candidates = new CareerCandidateProgress(student.SaveSlot, student.PlayerId);
            student.RecordActivityCompleted(CandidateSet.LibraryActivityId, new[] { CandidateSet.LibraryActivityId + ":unlock=career.researcher" });
            var result = candidates.ApplyHint(set.LibraryHint, student, "source.library.day1", student.GetTodayResultLogIds());

            Assert.IsTrue(result.Applied);
            Assert.AreEqual("candidate.learning", result.CandidateId);
            Assert.AreEqual(1, candidates.GetHintCount(set.Learning));
            Assert.AreEqual(CareerCandidateState.Hinted, set.Learning.GetState(candidates));
        }

        [Test]
        public void CAREER_CANDIDATE_EDIT_006_CandidateProgressPersistsBySaveSlotAndPlayerIdentity()
        {
            var set = CandidateSet.Create();
            var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            var candidates = new CareerCandidateProgress(student.SaveSlot, student.PlayerId);
            student.RecordActivityCompleted(CandidateSet.LibraryActivityId, new[] { "library unlock" });
            candidates.ApplyHint(set.LibraryHint, student, "source.library.day1", student.GetTodayResultLogIds());

            var restored = CareerCandidateProgress.FromSaveData(candidates.ToSaveData());
            var otherPlayer = new CareerCandidateProgress("slot-a", "player-b");

            Assert.AreEqual("slot-a", restored.SaveSlot);
            Assert.AreEqual("player-a", restored.PlayerId);
            Assert.AreEqual(1, restored.GetHintCount(set.Learning));
            Assert.AreEqual(0, otherPlayer.GetHintCount(set.Learning));
        }

        [Test]
        public void CAREER_CANDIDATE_EDIT_007_SameHintSourceDoesNotApplyProgressOrRewardTwice()
        {
            var set = CandidateSet.Create();
            var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            var candidates = new CareerCandidateProgress(student.SaveSlot, student.PlayerId);
            student.RecordActivityCompleted(CandidateSet.LibraryActivityId, new[] { "library unlock" });

            var first = candidates.ApplyHint(set.LibraryHint, student, "source.library.day1", student.GetTodayResultLogIds());
            var duplicate = candidates.ApplyHint(set.LibraryHint, student, "source.library.day1", student.GetTodayResultLogIds());

            Assert.IsTrue(first.Applied);
            Assert.IsFalse(duplicate.Applied);
            Assert.AreEqual(1, candidates.GetHintCount(set.Learning));
            Assert.AreEqual(1, candidates.GetUnderstandingScore(set.Learning));
        }

        [Test]
        public void CAREER_CANDIDATE_EDIT_008_SummaryShowsLockedPartialUnlockedAndRecommendedActions()
        {
            var set = CandidateSet.Create();
            var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            var candidates = new CareerCandidateProgress(student.SaveSlot, student.PlayerId);
            var locked = CareerCandidateSummaryBuilder.Build(set.Technical, candidates, student);
            student.RecordActivityCompleted(CandidateSet.LibraryActivityId, new[] { "library unlock" });
            candidates.ApplyHint(set.LibraryHint, student, "source.library.day1", student.GetTodayResultLogIds());
            var hinted = CareerCandidateSummaryBuilder.Build(set.Learning, candidates, student);
            candidates.ApplyHint(set.ClassHint, student, "source.class.day1", new[] { "class unlock" });
            var revealed = CareerCandidateSummaryBuilder.Build(set.Learning, candidates, student);

            Assert.AreEqual("???", locked.DisplayName);
            Assert.AreEqual("Partial hints point toward action.try-workshop", locked.Description);
            Assert.AreEqual(CareerCandidateState.Hinted, hinted.State);
            Assert.AreEqual(CareerCandidateState.Revealed, revealed.State);
            CollectionAssert.Contains(revealed.RelatedLocationKeys, "location.library");
            CollectionAssert.Contains(revealed.RelatedActivityKeys, CandidateSet.LibraryActivityId);
            CollectionAssert.Contains(revealed.RecommendedActionKeys, "action.visit-library");
        }

        [Test]
        public void AdvancingDay_ClearsOnlyTodayHintLines()
        {
            var p = DailyResetFixture();
            Assert.That(p.TryEndDay(null, out _), Is.True);
            Assert.That(p.CareerCandidates.TodayHintResultLines, Has.Length.EqualTo(1));
            Assert.That(p.TryStartNextDay(), Is.True);
            Assert.That(p.CareerCandidates.TodayHintResultLines, Is.Empty);
            var saved = p.ToSaveData().CareerCandidates;
            Assert.That(saved.Candidates[0].UnderstandingScore, Is.EqualTo(3));
            Assert.That(saved.Candidates[0].HintIds, Is.EqualTo(new[] { "hint.fixture" }));
            Assert.That(saved.AppliedSourceRequestIds, Is.EqualTo(new[] { "earned-source" }));
        }

        [Test]
        public void FailedDayAdvance_KeepsTodayHintsForCurrentResult()
        {
            var p = DailyResetFixture();
            Assert.That(p.TryStartNextDay(), Is.False);
            Assert.That(p.CareerCandidates.TodayHintResultLines, Has.Length.EqualTo(1));
        }

        [Test]
        public void ResultReadyReload_PreservesHintsUntilRealDayAdvance()
        {
            var p = DailyResetFixture();
            p.TryEndDay(null, out _);
            var reloaded = StudentLifeProgress.FromSaveData(p.ToSaveData());
            Assert.That(reloaded.CareerCandidates.TodayHintResultLines, Has.Length.EqualTo(1));
            Assert.That(reloaded.TryStartNextDay(), Is.True);
            Assert.That(reloaded.ToSaveData().CareerCandidates.TodayHintResultLines, Is.Empty);
        }

        // 원래 테스트가 안 다루던 경계

        [Test]
        public void NonMatchingSource_DoesNotConsumeRequestSoALaterMatchCanApply()
        {
            var set = CandidateSet.Create();
            var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            var candidates = new CareerCandidateProgress(student.SaveSlot, student.PlayerId);

            var rejected = candidates.ApplyHint(set.ClassHint, student, "source.1", new[] { "nothing here" });
            Assert.IsFalse(rejected.Applied);
            Assert.IsFalse(candidates.HasAppliedSource("source.1"));
            Assert.AreEqual(0, candidates.GetUnderstandingScore(set.Learning));

            Assert.IsTrue(candidates.ApplyHint(set.ClassHint, student, "source.1", new[] { "class" }).Applied);
            Assert.IsTrue(candidates.HasAppliedSource("source.1"));
        }

        [Test]
        public void EmptySourceRequest_UsesHintIdAsRequest_AndNullHintIsRejected()
        {
            var set = CandidateSet.Create();
            var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            var candidates = new CareerCandidateProgress(student.SaveSlot, student.PlayerId);

            Assert.IsTrue(candidates.ApplyHint(set.ClassHint, student, string.Empty, new[] { "class" }).Applied);
            Assert.IsTrue(candidates.HasAppliedSource("hint.learning.class"));
            Assert.IsFalse(candidates.ApplyHint(set.ClassHint, student, null, new[] { "class" }).Applied);
            Assert.IsFalse(candidates.ApplyHint(null, student, "x", null).Applied);
            Assert.IsFalse(candidates.ApplyFirstMatchingHint(null, student, "x", null).Applied);
        }

        [Test]
        public void AppliesOnceHint_CountsOnlyOnceAcrossSources()
        {
            var set = CandidateSet.Create();
            var once = new CareerHint("hint.once", null, set.Learning, null, null, 2, true, new ICareerHintSource[] { new CareerResultLogHintSource("class") });
            var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            var candidates = new CareerCandidateProgress(student.SaveSlot, student.PlayerId);

            Assert.IsTrue(candidates.ApplyHint(once, student, "a", new[] { "class" }).Applied);
            var again = candidates.ApplyHint(once, student, "b", new[] { "class" });

            Assert.IsFalse(again.Applied);
            Assert.IsFalse(candidates.HasAppliedSource("b"));
            Assert.AreEqual(2, candidates.GetUnderstandingScore(set.Learning));
        }

        [Test]
        public void ApplyFirstMatchingHint_StopsAtFirstAppliedHint()
        {
            var set = CandidateSet.Create();
            var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            var candidates = new CareerCandidateProgress(student.SaveSlot, student.PlayerId);

            var result = candidates.ApplyFirstMatchingHint(new[] { set.WorkHint, set.ClassHint, set.LibraryHint }, student, "source.x", new[] { "class library" });

            Assert.IsTrue(result.Applied);
            Assert.AreEqual("hint.learning.class", result.HintId);
            Assert.AreEqual(1, candidates.GetUnderstandingScore(set.Learning));
        }

        [Test]
        public void Rewards_AreClaimedOnlyFromDeepeningAndOnlyOnce_AcrossSave()
        {
            var reward = new CareerCandidateCareerHintReward("reward.hint", "career.researcher");
            var candidate = new CareerCandidate("candidate.reward", null, null, null, null, null, null, null, null, new ICareerCandidateReward[] { reward });
            var hint = new CareerHint("hint.reward", null, candidate, null, null, 1, false, new ICareerHintSource[] { new CareerResultLogHintSource("x") });
            var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            var candidates = new CareerCandidateProgress(student.SaveSlot, student.PlayerId);

            candidates.ApplyHint(hint, student, "s1", new[] { "x" });
            candidates.ApplyHint(hint, student, "s2", new[] { "x" });
            Assert.AreEqual(CareerCandidateState.Revealed, candidate.GetState(candidates));
            Assert.IsFalse(candidates.TryClaimRewards(candidate, student));
            Assert.IsFalse(student.IsCareerHintUnlocked("career.researcher"));

            candidates.ApplyHint(hint, student, "s3", new[] { "x" });
            Assert.AreEqual(CareerCandidateState.Deepening, candidate.GetState(candidates));
            Assert.IsTrue(candidates.TryClaimRewards(candidate, student));
            Assert.IsTrue(student.IsCareerHintUnlocked("career.researcher"));
            Assert.IsTrue(candidates.HasClaimedReward(candidate, reward));

            var restored = CareerCandidateProgress.FromSaveData(candidates.ToSaveData());
            Assert.IsFalse(restored.TryClaimRewards(candidate, student));
            Assert.AreEqual("s3", restored.GetLastHintSourceId(candidate));
            CollectionAssert.AreEqual(new[] { "candidate.reward:hint.reward:Hinted", "candidate.reward:hint.reward:Revealed", "candidate.reward:hint.reward:Deepening" }, restored.TodayHintResultLines);
        }

        [Test]
        public void Thresholds_AreKeptStrictlyIncreasing_AndRequirementsReportInSummary()
        {
            var requirement = new CareerCandidateHintCountRequirement("req.hints", 2);
            var candidate = new CareerCandidate("candidate.t", null, null, null, null, null, new ICareerCandidateRequirement[] { requirement }, null, null, null, revealedHintThreshold: 0, deepeningHintThreshold: 0, readyHintThreshold: 0);
            Assert.AreEqual(1, candidate.RevealedHintThreshold);
            Assert.AreEqual(2, candidate.DeepeningHintThreshold);
            Assert.AreEqual(3, candidate.ReadyHintThreshold);
            var summary = CareerCandidateSummaryBuilder.Build(candidate, new CareerCandidateProgress("s", "p"), null);
            CollectionAssert.AreEqual(new[] { "req.hints:open" }, summary.RequirementLines);
            Assert.AreEqual("Partial hints point toward a career candidate", summary.Description);
        }

        [Test]
        public void ActivityLogRequirementAndSource_ReadStudentLogs()
        {
            var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            var candidate = new CareerCandidate("candidate.a", null, null, null, null, null, new ICareerCandidateRequirement[] { new CareerCandidateActivityLogRequirement("req.log", "activity.a"), new CareerCandidateSkillRequirement("req.skill", "skill.a", 2) }, null, null, null);
            var progress = new CareerCandidateProgress("slot-a", "player-a");
            var source = new CareerActivityLogHintSource("activity.a");

            Assert.IsFalse(candidate.AreRequirementsSatisfied(progress, student));
            Assert.IsFalse(source.Matches(new CareerCandidateEvaluationContext(candidate, progress, student, "", null)));
            student.RecordActivityCompleted("activity.a");
            student.AddSkill("skill.a", 2);
            Assert.IsTrue(candidate.AreRequirementsSatisfied(progress, student));
            Assert.IsTrue(source.Matches(new CareerCandidateEvaluationContext(candidate, progress, student, "", null)));
            Assert.IsFalse(source.Matches(new CareerCandidateEvaluationContext(candidate, progress, student, "", new[] { "activity.b:+x=1" })));
            Assert.IsTrue(source.Matches(new CareerCandidateEvaluationContext(candidate, progress, student, "", new[] { "activity.a:+x=1" })));
        }

        [Test]
        public void StudentSave_KeepsCareerCandidatesOnlyForSameIdentity()
        {
            var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            var set = CandidateSet.Create();
            student.CareerCandidates.ApplyHint(set.ClassHint, student, "s", new[] { "class" });
            var data = student.ToSaveData();
            Assert.That(data.CareerCandidates.SaveSlot, Is.EqualTo("slot-a"));
            Assert.That(StudentLifeProgress.FromSaveData(data).CareerCandidates.GetUnderstandingScore(set.Learning), Is.EqualTo(1));

            data.CareerCandidates.PlayerId = "player-b";
            var foreign = StudentLifeProgress.FromSaveData(data);
            Assert.That(foreign.CareerCandidates.GetUnderstandingScore(set.Learning), Is.EqualTo(0));
            Assert.That(foreign.CareerCandidates.PlayerId, Is.EqualTo("player-a"));
            Assert.IsFalse(foreign.RestoreCareerCandidates(null));
        }

        private static StudentLifeProgress DailyResetFixture()
        {
            var d = new StudentLifeProgress("slot", "player", 12, 10).ToSaveData();
            d.CareerCandidates = new CareerCandidateProgressSaveData
            {
                SaveSlot = "slot", PlayerId = "player", TodayHintResultLines = new[] { "candidate.fixture:hint.fixture:Revealed" },
                Candidates = new[] { new CareerCandidateProgressSaveData.CandidateEntry { CandidateId = "candidate.fixture", UnderstandingScore = 3, HintIds = new[] { "hint.fixture" } } },
                AppliedSourceRequestIds = new[] { "earned-source" },
            };
            return StudentLifeProgress.FromSaveData(d);
        }

        internal sealed class CandidateSet
        {
            public const string LibraryActivityId = "activity.library.self-study";
            public CareerCandidate Learning, Technical, Service, Investigation;
            public CareerCandidate[] Candidates;
            public CareerHint LibraryHint, ClassHint, WorkHint, ServiceHint, InvestigationHint;

            public static CandidateSet Create()
            {
                var set = new CandidateSet();
                var libraryRoute = Route("route.library", "location.library", LibraryActivityId, "action.visit-library");
                var classRoute = Route("route.class", "location.school", "activity.school.class", "action.attend-class");
                var workRoute = Route("route.work", "location.workshop", "activity.workshop.part-time", "action.try-workshop");
                var serviceRoute = Route("route.service", "location.plaza", "activity.plaza.help", "action.help-npc");
                var investigationRoute = Route("route.investigation", "location.alley", "activity.alley.investigate", "action.investigate-alley");
                set.Learning = Candidate("candidate.learning", new[] { classRoute, libraryRoute }, new[] { "location.school", "location.library" }, new[] { "activity.school.class", LibraryActivityId }, "action.visit-library");
                set.Technical = Candidate("candidate.technical", new[] { workRoute }, new[] { "location.workshop" }, new[] { "activity.workshop.part-time" }, "action.try-workshop");
                set.Service = Candidate("candidate.service", new[] { serviceRoute }, new[] { "location.plaza" }, new[] { "activity.plaza.help" }, "action.help-npc");
                set.Investigation = Candidate("candidate.investigation", new[] { investigationRoute }, new[] { "location.alley" }, new[] { "activity.alley.investigate" }, "action.investigate-alley");
                set.LibraryHint = Hint("hint.learning.library", set.Learning, libraryRoute, new CareerActivityLogHintSource(LibraryActivityId), new CareerResultLogHintSource("library"));
                set.ClassHint = Hint("hint.learning.class", set.Learning, classRoute, new CareerResultLogHintSource("class"));
                set.WorkHint = Hint("hint.technical.work", set.Technical, workRoute, new CareerActivityLogHintSource("activity.workshop.part-time"));
                set.ServiceHint = Hint("hint.service.help", set.Service, serviceRoute, new CareerActivityLogHintSource("activity.plaza.help"));
                set.InvestigationHint = Hint("hint.investigation.alley", set.Investigation, investigationRoute, new CareerActivityLogHintSource("activity.alley.investigate"));
                set.Candidates = new[] { set.Learning, set.Technical, set.Service, set.Investigation };
                return set;
            }

            private static CareerCandidateRoute Route(string id, string locationId, string activityId, string recommended)
                => new CareerCandidateRoute(id, id, "desc." + id, new[] { new DefinitionRef(locationId) }, new[] { new DefinitionRef(activityId) }, new[] { recommended });

            private static CareerCandidate Candidate(string id, CareerCandidateRoute[] routes, string[] locations, string[] activities, string recommended)
                => new CareerCandidate(id, id, "desc." + id, null, Array.ConvertAll(locations, l => new DefinitionRef(l)), Array.ConvertAll(activities, a => new DefinitionRef(a)), null, routes, new[] { recommended }, null);

            private static CareerHint Hint(string id, CareerCandidate candidate, CareerCandidateRoute route, params ICareerHintSource[] sources)
                => new CareerHint(id, id, candidate, route, "insight." + id, 1, false, sources);
        }
    }
}
