using NUnit.Framework;
using Rootborn.Game.WorldState;
using Rootborn.UI.StudentLife;
using Rootborn.UI.WorldState;
using UnityEngine;

namespace Rootborn.Tests.EditMode.UI
{
    public sealed class WorldStateUsageUiSurfaceTests
    {
        [Test]
        public void WORLD_USAGE_UI_001_DayResultAndWorldLogRenderSameUsageSummaryCards()
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas));
            var canvas = canvasObject.GetComponent<Canvas>();
            var model = new WorldStateUsageSummaryModel("usage.library.archive-table", "world.library.archive-open", "Archive Table", "Review old records", "Inspect archive", "Find map clue", true, true, false, "already used", new[] { "quest.library.follow-up", "encyclopedia.archive" });

            var dayResult = StudentDayResultPanel.EnsureInScene(canvas);
            dayResult.ShowWorldStateUsages(new[] { model });
            var worldLog = WorldStateUsageLogPanel.EnsureInScene(canvas);
            worldLog.Show(new[] { model });

            Assert.AreEqual(1, dayResult.GetWorldStateUsageCardCountForTests());
            Assert.AreEqual(1, worldLog.CardCountForTests);
            StringAssert.Contains("Archive Table", dayResult.GetWorldStateUsageTextForTests());
            StringAssert.Contains("quest.library.follow-up", worldLog.TextForTests);

            Object.DestroyImmediate(canvasObject);
        }
    }
}
