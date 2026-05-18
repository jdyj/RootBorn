using NUnit.Framework;
using Rootborn.Game.WorldState;
using Rootborn.UI.StudentLife;
using Rootborn.UI.WorldState;
using UnityEngine;

namespace Rootborn.Tests.EditMode.UI
{
    public sealed class WorldStateUiSurfaceTests
    {
        [Test]
        public void WORLD_STATE_UI_002_DayResultAndWorldLogRenderSameSummaryWithCommonCards()
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas));
            var canvas = canvasObject.GetComponent<Canvas>();
            var model = new WorldStateSummaryModel(
                "world.library.archive-open",
                "Library archive opened",
                "A quiet archive shelf is now available in the library.",
                "Library",
                "Librarian",
                "Check the archive shelf",
                WorldStateChangeKind.LocationUnlocked,
                WorldStateScopeKind.Shared,
                new[] { WorldStateBadgeKind.New, WorldStateBadgeKind.Shared },
                100,
                false);

            var dayResult = StudentDayResultPanel.EnsureInScene(canvas);
            dayResult.ShowWorldStateChanges(new[] { model });
            var worldLog = WorldStateLogPanel.EnsureInScene(canvas);
            worldLog.Show(new[] { model });

            Assert.AreEqual(1, dayResult.GetWorldStateCardCountForTests(), "WORLD-STATE-UI-002 failed: day result must render world changes with common cards.");
            Assert.AreEqual(1, worldLog.CardCountForTests, "WORLD-STATE-UI-002 failed: world log must render world changes with common cards.");
            StringAssert.Contains("Library archive opened", dayResult.GetWorldStateTextForTests());
            StringAssert.Contains("Library archive opened", worldLog.TextForTests);

            Object.DestroyImmediate(canvasObject);
        }
    }
}
