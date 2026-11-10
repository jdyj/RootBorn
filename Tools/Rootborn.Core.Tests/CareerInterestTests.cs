using NUnit.Framework;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Tests
{
    // 옮겨 옴: Assets/Tests/EditMode/StudentLife/CareerInterestSelectionLoopTests.cs (CAREER_INTEREST_EDIT_002, 004, 006, 008)
    public sealed class CareerInterestTests
    {
        [Test]
        public void CAREER_INTEREST_EDIT_002_OnlyRevealedCandidatesCanBeSelectedByDefault()
        {
            var set = InterestSet.Create();
            var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            var candidates = new CareerCandidateProgress(student.SaveSlot, student.PlayerId);
            var interests = new CareerInterestProgress(student.SaveSlot, student.PlayerId);
            var locked = interests.TrySelect(set.LearningInterest, candidates, student, 1, "select.learning.locked");
            candidates.ApplyHint(set.LibraryHint, student, "source.library.1", new[] { "library" });
            candidates.ApplyHint(set.ClassHint, student, "source.class.1", new[] { "class" });
            var revealed = interests.TrySelect(set.LearningInterest, candidates, student, 1, "select.learning.revealed");

            Assert.IsFalse(locked.Applied);
            Assert.AreEqual(CareerInterestSelectionState.LockedByCondition, locked.State);
            Assert.AreEqual("candidate state Locked below Revealed", locked.Reason);
            Assert.IsTrue(revealed.Applied);
            Assert.AreEqual("interest.learning", interests.CurrentInterestId);
        }

        [Test]
        public void CAREER_INTEREST_EDIT_004_FirstSelectionPersistsCurrentInterestHistoryAndRewardClaimFlags()
        {
            var set = InterestSet.Create();
            var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            var candidates = Revealed(set, student);
            var interests = new CareerInterestProgress(student.SaveSlot, student.PlayerId);
            var first = interests.TrySelect(set.LearningInterest, candidates, student, 1, "select.learning.day1");
            var duplicate = interests.TrySelect(set.LearningInterest, candidates, student, 1, "select.learning.day1.again");
            var restored = CareerInterestProgress.FromSaveData(interests.ToSaveData());

            Assert.IsTrue(first.Applied);
            Assert.IsFalse(duplicate.Applied);
            Assert.AreEqual(11, student.Focus);
            Assert.AreEqual("interest.learning", restored.CurrentInterestId);
            CollectionAssert.Contains(restored.SelectionHistoryIds, "interest.learning");
            Assert.IsTrue(restored.HasClaimedReward("interest.learning", "reward.learning.focus"));
        }

        [Test]
        public void CAREER_INTEREST_EDIT_006_ChangePolicyCanRequireOneChangePerDay()
        {
            var set = InterestSet.Create();
            var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            var candidates = Revealed(set, student);
            candidates.ApplyHint(set.WorkHint, student, "source.work.1", new[] { "work" });
            candidates.ApplyHint(set.ShopHint, student, "source.shop.1", new[] { "shop" });
            var interests = new CareerInterestProgress(student.SaveSlot, student.PlayerId);

            Assert.IsTrue(interests.TrySelect(set.LearningInterest, candidates, student, 1, "select.learning.day1").Applied);
            var changed = interests.TrySelect(set.TechnicalInterest, candidates, student, 2, "select.technical.day2");
            var blocked = interests.TrySelect(set.LearningInterest, candidates, student, 2, "select.learning.day2.return");

            Assert.IsTrue(changed.Applied);
            Assert.IsFalse(blocked.Applied);
            Assert.AreEqual(CareerInterestSelectionState.ChangedToday, blocked.State);
            Assert.IsNotEmpty(blocked.Reason);
            Assert.AreEqual("interest.learning", interests.PreviousInterestId);
        }

        [Test]
        public void CAREER_INTEREST_EDIT_008_RecommendationsAndSummaryComeFromSelectedInterestData()
        {
            var set = InterestSet.Create();
            var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            var candidates = Revealed(set, student);
            var interests = new CareerInterestProgress(student.SaveSlot, student.PlayerId);
            interests.TrySelect(set.LearningInterest, candidates, student, 1, "select.learning.day1");
            var summary = CareerInterestSummaryBuilder.Build(set.LearningInterest, interests, candidates, student, 1);

            Assert.AreEqual(CareerInterestSelectionState.Selected, summary.State);
            CollectionAssert.Contains(summary.RecommendedActionKeys, "action.visit-library");
            CollectionAssert.Contains(summary.RelatedLocationKeys, "location.library");
            CollectionAssert.Contains(summary.RelatedActivityKeys, "activity.library.study");
        }

        // 원래 테스트가 안 다루던 경계

        [Test]
        public void MissingInterest_IsRejectedWithoutChange()
        {
            var interests = new CareerInterestProgress("slot-a", "player-a");
            var result = interests.TrySelect(null, null, null, 1, "r");
            Assert.IsFalse(result.Applied);
            Assert.AreEqual(CareerInterestSelectionState.LockedByCondition, result.State);
            Assert.AreEqual("missing interest", result.Reason);
            Assert.AreEqual(string.Empty, interests.CurrentInterestId);
        }

        [Test]
        public void ReselectingAfterChange_DoesNotPayRewardAgain_AndFollowUpFiresOncePerPick()
        {
            var set = InterestSet.Create(maxChangesPerDay: 5);
            var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            var candidates = Revealed(set, student);
            candidates.ApplyHint(set.WorkHint, student, "w", new[] { "work" });
            candidates.ApplyHint(set.ShopHint, student, "s", new[] { "shop" });
            var interests = new CareerInterestProgress(student.SaveSlot, student.PlayerId);

            interests.TrySelect(set.LearningInterest, candidates, student, 1, "a");
            Assert.IsTrue(interests.TryBeginFollowUp());
            Assert.IsFalse(interests.TryBeginFollowUp());
            interests.TrySelect(set.TechnicalInterest, candidates, student, 1, "b");
            interests.TrySelect(set.LearningInterest, candidates, student, 1, "c");

            Assert.AreEqual(12, student.Focus);
            Assert.AreEqual(3, interests.ChangeCountForCurrentDay);
            CollectionAssert.AreEqual(new[] { "interest.learning", "interest.technical" }, interests.SelectionHistoryIds);
            CollectionAssert.AreEqual(new[] { "interest.learning:selected", "interest.technical:selected" }, interests.TodayInterestActionSummary);
            Assert.IsTrue(interests.TryBeginFollowUp());
        }

        [Test]
        public void SaveRoundTrip_KeepsEveryField()
        {
            var set = InterestSet.Create();
            var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
            var candidates = Revealed(set, student);
            var interests = new CareerInterestProgress(student.SaveSlot, student.PlayerId);
            interests.TrySelect(set.LearningInterest, candidates, student, 4, "a");
            interests.TryBeginFollowUp();

            var data = CareerInterestProgress.FromSaveData(interests.ToSaveData()).ToSaveData();

            Assert.AreEqual("slot-a", data.SaveSlot);
            Assert.AreEqual("player-a", data.PlayerId);
            Assert.AreEqual("interest.learning", data.CurrentInterestId);
            Assert.AreEqual(4, data.LastChangedDay);
            Assert.AreEqual(1, data.ChangeCountForCurrentDay);
            Assert.AreEqual("interest.learning:4", data.LastRecommendationSourceVersion);
            Assert.AreEqual("interest.learning", data.LastFollowUpInterestId);
            CollectionAssert.AreEqual(new[] { "interest.learning:reward.learning.focus" }, data.ClaimedRewardIds);
            Assert.AreEqual("default", CareerInterestProgress.FromSaveData(null).SaveSlot);
        }

        [Test]
        public void InterestKeepsLinkedQuestAndFollowUpIds()
        {
            var interest = new CareerInterest("interest.x", null, null, null, null, null, null, null, null, null, new[] { "quest.a" }, "npc.mentor", "line.follow", "line.changed");
            CollectionAssert.AreEqual(new[] { "quest.a" }, interest.LinkedQuestIds);
            Assert.AreEqual("npc.mentor", interest.FollowUpNpcId);
            Assert.AreEqual("line.follow", interest.FollowUpLineKey);
            Assert.AreEqual("line.changed", interest.ChangedLineKey);
            Assert.AreEqual("interest.x", interest.DescriptionKey);
        }

        private static CareerCandidateProgress Revealed(InterestSet set, StudentLifeProgress student)
        {
            var candidates = new CareerCandidateProgress(student.SaveSlot, student.PlayerId);
            candidates.ApplyHint(set.LibraryHint, student, "source.library.1", new[] { "library" });
            candidates.ApplyHint(set.ClassHint, student, "source.class.1", new[] { "class" });
            return candidates;
        }

        private sealed class AlwaysMatches : ICareerHintSource
        {
            public bool Matches(CareerCandidateEvaluationContext context) => true;
        }

        private sealed class InterestSet
        {
            public CareerInterest LearningInterest, TechnicalInterest;
            public CareerHint LibraryHint, ClassHint, WorkHint, ShopHint;

            public static InterestSet Create(int maxChangesPerDay = 1)
            {
                var libraryRoute = new CareerCandidateRoute("route.library", null, null, null, null, new[] { "action.visit-library" });
                var workRoute = new CareerCandidateRoute("route.workshop", null, null, null, null, new[] { "action.practice-workshop" });
                var learning = new CareerCandidate("candidate.learning", null, null, null, new[] { new DefinitionRef("location.library") }, new[] { new DefinitionRef("activity.library.study") }, null, new[] { libraryRoute }, new[] { "action.visit-library" }, null);
                var technical = new CareerCandidate("candidate.technical", null, null, null, new[] { new DefinitionRef("location.workshop") }, new[] { new DefinitionRef("activity.workshop.practice") }, null, new[] { workRoute }, new[] { "action.practice-workshop" }, null);
                return new InterestSet
                {
                    LibraryHint = Hint("hint.learning.library", learning, libraryRoute),
                    ClassHint = Hint("hint.learning.class", learning, libraryRoute),
                    WorkHint = Hint("hint.technical.work", technical, workRoute),
                    ShopHint = Hint("hint.technical.shop", technical, workRoute),
                    LearningInterest = Interest("interest.learning", learning, "location.library", "activity.library.study", "action.visit-library", "reward.learning.focus", maxChangesPerDay),
                    TechnicalInterest = Interest("interest.technical", technical, "location.workshop", "activity.workshop.practice", "action.practice-workshop", "reward.technical.focus", maxChangesPerDay),
                };
            }

            private static CareerHint Hint(string id, CareerCandidate candidate, CareerCandidateRoute route)
                => new CareerHint(id, id, candidate, route, id + ".insight", 1, false, new ICareerHintSource[] { new AlwaysMatches() });

            private static CareerInterest Interest(string id, CareerCandidate candidate, string location, string activity, string recommended, string rewardId, int maxChangesPerDay)
                => new CareerInterest(id, id, "desc." + id, candidate, new[] { new DefinitionRef(location) }, new[] { new DefinitionRef(activity) },
                    new ICareerInterestUnlockRequirement[] { new CareerInterestCandidateStateRequirement(CareerCandidateState.Revealed) },
                    new ICareerInterestSelectionRule[] { new CareerInterestDailyChangeLimitRule(maxChangesPerDay, true) },
                    new ICareerInterestRecommendation[] { new CareerInterestStaticRecommendation(new[] { recommended }) },
                    new ICareerInterestReward[] { new CareerInterestFocusReward(rewardId, 1) });
        }
    }
}
