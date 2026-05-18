using NUnit.Framework;
using Rootborn.Game.StudentLife;
using Rootborn.UI.StudentLife;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.Tests.EditMode.UI
{
    public sealed class CareerInterestHudWidgetTests
    {
        [Test]
        public void CAREER_INTEREST_UI_004_HudShowsSelectedInterestAndRecommendedActions()
        {
            var canvasGo = new GameObject("career-interest-hud-canvas", typeof(Canvas));
            try
            {
                var widget = CareerInterestHudWidget.EnsureInScene(canvasGo.GetComponent<Canvas>());
                var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
                var candidates = new CareerCandidateProgress(student.SaveSlot, student.PlayerId);
                var interest = CreateInterest("interest.learning");
                var progress = new CareerInterestProgress(student.SaveSlot, student.PlayerId);
                progress.TrySelect(interest, candidates, student, 1, "select-learning");

                widget.Refresh(new[] { interest }, progress, candidates, student, 1);

                StringAssert.Contains("interest.learning", widget.VisibleText);
                StringAssert.Contains("action.visit-library", widget.VisibleText);
            }
            finally
            {
                Object.DestroyImmediate(canvasGo);
            }
        }

        private static CareerInterestDefinition CreateInterest(string id)
        {
            var route = ScriptableObject.CreateInstance<CareerCandidateRouteDefinition>();
            route.ConfigureForTests("route.library", "route.library", "route.library.desc", null, null, new[] { "action.visit-library" });
            var candidate = ScriptableObject.CreateInstance<CareerCandidateDefinition>();
            candidate.ConfigureForTests("candidate.learning", "candidate.learning", "candidate.learning.desc", new[] { route }, new CareerCandidateRequirementBase[0], new[] { "action.visit-library" }, null, null);
            var requirement = ScriptableObject.CreateInstance<CareerInterestCandidateStateRequirement>();
            requirement.ConfigureForTests(CareerCandidateState.Locked);
            var recommendation = ScriptableObject.CreateInstance<CareerInterestStaticRecommendation>();
            recommendation.ConfigureForTests(new[] { "action.visit-library" });
            var interest = ScriptableObject.CreateInstance<CareerInterestDefinition>();
            interest.ConfigureForTests(id, id, id + ".desc", candidate, null, null, new CareerInterestUnlockRequirementBase[] { requirement }, new CareerInterestSelectionRuleBase[0], new CareerInterestRecommendationBase[] { recommendation }, new CareerInterestRewardBase[0]);
            return interest;
        }
    }
}
