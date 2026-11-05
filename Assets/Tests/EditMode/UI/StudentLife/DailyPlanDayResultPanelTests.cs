using NUnit.Framework;
using Rootborn.UI.StudentLife;
using UnityEngine;

namespace Rootborn.Tests.EditMode.UI.StudentLife
{
    public sealed class DailyPlanDayResultPanelTests
    {
        [Test]
        public void AppendDailyPlanSummary_AddsPlanActivityLogsAndNextHint()
        {
            var go = new GameObject("StudentDayResultPanel");
            var panel = go.AddComponent<StudentDayResultPanel>();
            var summary = new DailyPlanDaySummary(
                "daily.route.library",
                "activity.library.read",
                true,
                new[] { "daily.result.focus" },
                "objective.daily.library.next");

            panel.AppendDailyPlanSummary(summary);

            Assert.That(panel.ResultsTextForTests, Does.Contain("daily.route.library"));
            Assert.That(panel.ResultsTextForTests, Does.Contain("activity.library.read"));
            Assert.That(panel.ResultsTextForTests, Does.Contain("daily.result.focus"));
            Assert.That(panel.NextGuideTextForTests, Does.Contain(Rootborn.Game.Localization.GameText.Get("objective.daily.library.next")));
            Object.DestroyImmediate(go);
        }
    }
}
