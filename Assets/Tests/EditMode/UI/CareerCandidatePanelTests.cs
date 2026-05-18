using NUnit.Framework;
using Rootborn.Game.StudentLife;
using Rootborn.UI.StudentLife;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.Tests.EditMode.UI
{
    public sealed class CareerCandidatePanelTests
    {
        [Test]
        public void CAREER_CANDIDATE_EDIT_008_PanelRendersLockedHintedRevealedDetailsAndRecommendations()
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas));
            var panel = CareerCandidatePanel.EnsureInScene(canvasObject.GetComponent<Canvas>());
            var candidate = ScriptableObject.CreateInstance<CareerCandidateDefinition>();
            var route = ScriptableObject.CreateInstance<CareerCandidateRouteDefinition>();
            var activity = ScriptableObject.CreateInstance<LifeActivityDefinition>();
            var location = ScriptableObject.CreateInstance<LocationDefinition>();
            activity.ConfigureForTests("activity.library.self-study", "activity.library.self-study", LifeActivityCategory.SelfStudy, 0, 0, 0, 0, null, null, null);
            location.ConfigureForTests("location.library", "location.library", Vector2.zero, null);
            route.ConfigureForTests("route.library", "route.library", "desc.route.library", new[] { location }, new[] { activity }, new[] { "action.visit-library" });
            candidate.ConfigureForTests("candidate.learning", "candidate.learning", "desc.candidate.learning", new[] { route }, null, new[] { "action.visit-library" }, new[] { location }, new[] { activity });
            var progress = new CareerCandidateProgress("slot-a", "player-a");
            var student = new StudentLifeProgress("slot-a", "player-a", 10, 10);

            panel.Show(new[] { candidate }, progress, student);

            string text = CollectText(canvasObject.transform);
            StringAssert.Contains("???", text);
            StringAssert.Contains("action.visit-library", text);
            StringAssert.Contains("location.library", text);
            Assert.IsTrue(panel.IsOpen);
            Object.DestroyImmediate(canvasObject);
        }

        private static string CollectText(Transform root)
        {
            var texts = root.GetComponentsInChildren<Text>(true);
            string combined = string.Empty;
            for (int i = 0; i < texts.Length; i++) combined += texts[i].text + "\n";
            return combined;
        }
    }
}
