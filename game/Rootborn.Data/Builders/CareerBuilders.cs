using System;
using Rootborn.Core.Minigames;
using Rootborn.Core.StudentLife;

namespace Rootborn.Data.Builders
{
    /// <summary>학생 진로: 진로, 진로 실습, 진로 후보·경로·힌트, 진로 관심.</summary>
    internal static class CareerBuilders
    {
        public static void Register(DefinitionBuilders b)
        {
            b.Add("CareerDefinition", (n, c) => new Career(
                n.Id, n.Str("displayNameKey"), c.All<ICareerUnlockRequirement>(n, "requirements").ToArray()));

            b.Add("CareerPracticeDefinition", (n, c) => new CareerPractice(
                n.Id, n.Str("displayNameKey"), c.One<LifeActivity>(n, "activity"), n.RefId("careerHint"),
                c.All<PracticeStep>(n, "steps").ToArray(), c.One<Minigame>(n, "minigame")));
            b.Add("PracticeStepDefinition", (n, c) => new PracticeStep(n.Id, n.Str("displayNameKey")));

            // 문턱 값은 Unity CareerCandidateDefinition 게터처럼 단계마다 앞 단계보다 크게 다듬는다.
            b.Add("CareerCandidateDefinition", (n, c) =>
            {
                var revealed = Math.Max(1, n.Int("revealedHintThreshold"));
                var deepening = Math.Max(revealed + 1, n.Int("deepeningHintThreshold"));
                var ready = Math.Max(deepening + 1, n.Int("readyHintThreshold"));
                return new CareerCandidate(
                    n.Id, n.Str("displayNameKey"), n.Str("descriptionKey"), n.Str("lockedTeaserKey"),
                    BuilderHelpers.DefinitionRefs(n, "relatedLocations", c),
                    BuilderHelpers.DefinitionRefs(n, "relatedActivities", c),
                    c.All<ICareerCandidateRequirement>(n, "requirements").ToArray(),
                    c.All<CareerCandidateRoute>(n, "routes").ToArray(),
                    n.Strings("recommendedActionKeys"),
                    c.All<ICareerCandidateReward>(n, "rewards").ToArray(),
                    revealed, deepening, ready);
            });

            b.Add("CareerCandidateRouteDefinition", (n, c) => new CareerCandidateRoute(
                n.Id, n.Str("displayNameKey"), n.Str("descriptionKey"),
                BuilderHelpers.DefinitionRefs(n, "locations", c),
                BuilderHelpers.DefinitionRefs(n, "activities", c),
                n.Strings("recommendedActionKeys")));

            // Unity CareerHintDefinition.UnderstandingDelta는 1 이상.
            b.Add("CareerHintDefinition", BuilderHelpers.AddedLater(new[] { "appliesOnce" }, (n, c) => new CareerHint(
                n.Id, n.Str("displayNameKey"), c.One<CareerCandidate>(n, "candidate"), c.One<CareerCandidateRoute>(n, "route"),
                n.Str("insightKey"), Math.Max(1, n.Int("understandingDelta")), n.Bool("appliesOnce"),
                c.All<ICareerHintSource>(n, "sources").ToArray())));
            b.Add("CareerResultLogHintSource", (n, c) => new CareerResultLogHintSource(n.Str("requiredText")));

            b.Add("CareerInterestDefinition", BuilderHelpers.AddedLater(new[] { "linkedQuests", "followUpNpc", "followUpLineKey", "changedLineKey" }, (n, c) => new CareerInterest(
                n.Id, n.Str("displayNameKey"), n.Str("descriptionKey"), c.One<CareerCandidate>(n, "candidate"),
                BuilderHelpers.DefinitionRefs(n, "relatedLocations", c),
                BuilderHelpers.DefinitionRefs(n, "relatedActivities", c),
                c.All<ICareerInterestUnlockRequirement>(n, "unlockRequirements").ToArray(),
                c.All<ICareerInterestSelectionRule>(n, "selectionRules").ToArray(),
                c.All<ICareerInterestRecommendation>(n, "recommendations").ToArray(),
                c.All<ICareerInterestReward>(n, "rewards").ToArray(),
                n.RefIds("linkedQuests"), n.RefId("followUpNpc"), n.Str("followUpLineKey"), n.Str("changedLineKey"))));
            b.Add("CareerInterestCandidateStateRequirement", (n, c) => new CareerInterestCandidateStateRequirement(
                n.Enum<CareerCandidateState>("minimumState")));
            b.Add("CareerInterestDailyChangeLimitRule", (n, c) => new CareerInterestDailyChangeLimitRule(
                n.Int("maxChangesPerDay"), n.Bool("allowSameInterestNoOp")));
            b.Add("CareerInterestStaticRecommendation", (n, c) => new CareerInterestStaticRecommendation(n.Strings("recommendationKeys")));
            b.Add("CareerInterestFocusReward", (n, c) => new CareerInterestFocusReward(n.Str("rewardId"), n.Int("focusDelta")));
        }
    }
}
