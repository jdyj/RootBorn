using NUnit.Framework;
using Rootborn.Game.StudentLife;
using Rootborn.UI.StudentLife;
using UnityEngine;

namespace Rootborn.Tests.EditMode.UI
{
    public sealed class LocationStateUiSurfaceTests
    {
        [Test]
        public void LOCATION_STATE_UI_009_DayResultAndWorldLogRenderVisitedLocationStates()
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas));
            try
            {
                var canvas = canvasObject.GetComponent<Canvas>();
                var summaries = new[]
                {
                    new LocationStateTodaySummarySaveData
                    {
                        LocationId = "location.library",
                        StateId = "location.state.library-morning",
                        Day = 1,
                        TimeSlotId = "time.morning",
                        DisplayName = "Library Morning",
                        Hidden = false
                    },
                    new LocationStateTodaySummarySaveData
                    {
                        LocationId = "location.library",
                        StateId = "location.state.library-afternoon",
                        Day = 1,
                        TimeSlotId = "time.afternoon",
                        DisplayName = "Library Afternoon",
                        Hidden = false
                    }
                };

                var dayResult = StudentDayResultPanel.EnsureInScene(canvas);
                dayResult.ShowLocationStates(summaries);
                var worldLog = LocationStateLogPanel.EnsureInScene(canvas);
                worldLog.Show(summaries);

                Assert.AreEqual(2, dayResult.GetLocationStateCardCountForTests(), "LOCATION-STATE-UI-009 failed: day result must render one card per visited state.");
                Assert.AreEqual(2, worldLog.CardCountForTests, "LOCATION-STATE-UI-009 failed: world log must render one card per visited state.");
                StringAssert.Contains("Library Morning", dayResult.GetLocationStateTextForTests());
                StringAssert.Contains("location.state.library-afternoon", dayResult.GetLocationStateTextForTests());
                StringAssert.Contains("Library Afternoon", worldLog.TextForTests);
            }
            finally
            {
                Object.DestroyImmediate(canvasObject);
            }
        }
    }
}
