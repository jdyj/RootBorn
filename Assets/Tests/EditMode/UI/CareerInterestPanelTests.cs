using System;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.UI.StudentLife;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.Tests.EditMode.UI
{
    public sealed class CareerInterestPanelTests
    {
        [Test]
        public void CAREER_INTEREST_UI_001_ButtonSelectionUpdatesProgressAndDetailsText()
        {
            var canvasGo = new GameObject("career-interest-ui-canvas", typeof(Canvas));
            try
            {
                var canvas = canvasGo.GetComponent<Canvas>();
                var panel = CareerInterestPanel.EnsureInScene(canvas);
                var interest = CreateInterest("interest.learning");
                var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);
                var candidates = new CareerCandidateProgress(student.SaveSlot, student.PlayerId);
                candidates.ApplyHint(CreateHint("hint.a", interest.Candidate), student, "hint.a", new[] { "a" });
                candidates.ApplyHint(CreateHint("hint.b", interest.Candidate), student, "hint.b", new[] { "b" });
                var progress = new CareerInterestProgress(student.SaveSlot, student.PlayerId);

                panel.Show(new[] { interest }, progress, candidates, student, 1);
                var button = panel.GetInterestButtonForTests(0);
                button.onClick.Invoke();

                Assert.AreEqual("interest.learning", progress.CurrentInterestId);
                StringAssert.Contains("Selected", panel.DetailsTextForTests);
                StringAssert.Contains("action.visit-library", panel.DetailsTextForTests);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvasGo);
            }
        }

        [Test]
        public void CAREER_INTEREST_UI_002_ButtonSelectionPersistsThroughActiveSaveSlot()
        {
            string root = Path.Combine(Path.GetTempPath(), "rootborn-career-interest-ui-" + Guid.NewGuid().ToString("N"));
            var canvasGo = new GameObject("career-interest-save-ui-canvas", typeof(Canvas));
            var player = new GameObject("career-interest-player");
            try
            {
                Directory.CreateDirectory(root);
                SaveService.SetRootDirectoryForTests(root);
                ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = "slot-a", DisplayName = "slot-a" });
                var component = player.AddComponent<CareerInterestProgressComponent>();
                var interest = CreateInterest("interest.learning");
                var student = new StudentLifeProgress("slot-a", "local-player", 10, 10);
                var candidates = new CareerCandidateProgress(student.SaveSlot, student.PlayerId);
                candidates.ApplyHint(CreateHint("hint.a", interest.Candidate), student, "hint.a", new[] { "a" });
                candidates.ApplyHint(CreateHint("hint.b", interest.Candidate), student, "hint.b", new[] { "b" });

                var panel = CareerInterestPanel.EnsureInScene(canvasGo.GetComponent<Canvas>());
                panel.Show(new[] { interest }, component, candidates, student, 1);
                panel.GetInterestButtonForTests(0).onClick.Invoke();

                string saveFile = Path.Combine(root, "slot-a", "career-interest-progress.json");
                Assert.IsTrue(File.Exists(saveFile), "CAREER-INTEREST-UI-002 failed: UI button selection did not persist through SaveService slot file.");
                StringAssert.Contains("interest.learning", File.ReadAllText(saveFile));
            }
            finally
            {
                ActiveSaveContext.Clear();
                SaveService.SetRootDirectoryForTests(null);
                UnityEngine.Object.DestroyImmediate(player);
                UnityEngine.Object.DestroyImmediate(canvasGo);
                if (Directory.Exists(root)) Directory.Delete(root, true);
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
