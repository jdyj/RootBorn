using System;
using Rootborn.Game.StudentLife;

namespace Rootborn.UI.Objectives
{
    public sealed class StudentDayGuideObjectiveSource : IObjectiveJournalSource
    {
        private static readonly ObjectiveJournalCategory CategoryValue = new ObjectiveJournalCategory("Goals", "Goals");

        public string SourceId => "student-day-guide";
        public ObjectiveJournalCategory Category => CategoryValue;

        public ObjectiveJournalItem[] BuildItems(ObjectiveJournalContext context)
        {
            var progress = context.Player != null ? context.Player.GetComponent<StudentLifeProgressComponent>()?.Progress : null;
            if (progress == null)
            {
                return Array.Empty<ObjectiveJournalItem>();
            }

            string stableKey = string.IsNullOrEmpty(progress.NextObjectiveId) ? "student-day.current" : "student-day." + progress.NextObjectiveId;
            string hint = string.IsNullOrEmpty(progress.NextGuideText) ? "Choose a town activity or relationship action." : progress.NextGuideText;
            string progressText = "Day " + progress.CurrentDay + " | Energy " + progress.Energy + " | Focus " + progress.Focus;
            return new[]
            {
                new ObjectiveJournalItem(stableKey, Category.CategoryId, "Student Day Guide", progress.TutorialStageId, progress.DayState.ToString(), progressText, hint, false, 0)
            };
        }
    }
}
