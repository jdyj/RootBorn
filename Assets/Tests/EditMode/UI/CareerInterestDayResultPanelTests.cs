using NUnit.Framework;
using Rootborn.Game.StudentLife;
using Rootborn.UI.StudentLife;
using UnityEngine;

namespace Rootborn.Tests.EditMode.UI
{
    public sealed class CareerInterestDayResultPanelTests
    {
        [Test]
        public void CAREER_INTEREST_UI_003_DayResultShowsSelectedInterestAndRecommendations()
        {
            var panelObject = new GameObject("CareerInterestDayResultPanel");
            var player = new GameObject("CareerInterestDayResultPlayer");
            try
            {
                var panel = panelObject.AddComponent<StudentDayResultPanel>();
                var studentComponent = player.AddComponent<StudentLifeProgressComponent>();
                studentComponent.ConfigureForTests("slot-a", "player-a", 10, 10, 0, 8 * 60);
                var interestComponent = player.AddComponent<CareerInterestProgressComponent>();
                var progress = interestComponent.EnsureProgress();
                var interest = CreateInterest("interest.learning");
                var student = studentComponent.Progress;
                var candidates = new CareerCandidateProgress(student.SaveSlot, student.PlayerId);
                candidates.ApplyHint(CreateHint("hint.a", interest.Candidate), student, "hint.a", new[] { "a" });
                candidates.ApplyHint(CreateHint("hint.b", interest.Candidate), student, "hint.b", new[] { "b" });
                progress.TrySelect(interest, candidates, student, 1, "select.learning");
                interestComponent.Bind(new[] { interest });

                panel.Show(studentComponent, new StudentDaySummary(1, new string[0], new string[0], string.Empty, string.Empty, string.Empty, string.Empty));

                StringAssert.Contains("interest.learning", panel.ResultsTextForTests);
                StringAssert.Contains("action.visit-library", panel.ResultsTextForTests);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
                UnityEngine.Object.DestroyImmediate(panelObject);
            }
        }

        private static CareerInterestDefinition CreateInterest(string id)
        {
            var location = ScriptableObject.CreateInstance<LocationDefinition>();
            location.ConfigureForTests("location.library", "location.library", Vector2.zero, null);
            var activity = ScriptableObject.CreateInstance<LifeActivityDefinition>();
            activity.ConfigureForTests("activity.library", "activity.library", LifeActivityCategory.Hobby, 0, 0, 0, 0, null, null, null);
            var route = ScriptableObject.CreateInstance<CareerCandidateRouteDefinition>();
            route.ConfigureForTests("route.library", "route.library", "route.library.desc", new[] { location }, new[] { activity }, new[] { "action.visit-library" });
            var candidate = ScriptableObject.CreateInstance<CareerCandidateDefinition>();
            candidate.ConfigureForTests("candidate.learning", "candidate.learning", "candidate.learning.desc", new[] { route }, new CareerCandidateRequirementBase[0], new[] { "action.visit-library" }, new[] { location }, new[] { activity });
            var requirement = ScriptableObject.CreateInstance<CareerInterestCandidateStateRequirement>();
            requirement.ConfigureForTests(CareerCandidateState.Revealed);
            var recommendation = ScriptableObject.CreateInstance<CareerInterestStaticRecommendation>();
            recommendation.ConfigureForTests(new[] { "action.visit-library" });
            var interest = ScriptableObject.CreateInstance<CareerInterestDefinition>();
            interest.ConfigureForTests(id, id, id + ".desc", candidate, new[] { location }, new[] { activity }, new CareerInterestUnlockRequirementBase[] { requirement }, new CareerInterestSelectionRuleBase[0], new CareerInterestRecommendationBase[] { recommendation }, new CareerInterestRewardBase[0]);
            return interest;
        }

        private static CareerHintDefinition CreateHint(string id, CareerCandidateDefinition candidate)
        {
            var hint = ScriptableObject.CreateInstance<CareerHintDefinition>();
            hint.ConfigureForTests(id, id, candidate, null, id + ".insight", 1, new CareerHintSourceBase[] { new AlwaysMatchingCareerHintSource() });
            return hint;
        }

        private sealed class AlwaysMatchingCareerHintSource : CareerHintSourceBase
        {
            public override bool Matches(CareerCandidateEvaluationContext context) => true;
        }
    }
}
