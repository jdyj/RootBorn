using System;
using System.Globalization;
using System.Linq;
using Rootborn.Game.StudentLife;
using Rootborn.Game.Localization;
using Rootborn.UI.Careers;

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
            if (progress == null) return Array.Empty<ObjectiveJournalItem>();
            var adult = context.Player.GetComponent<AdultCareerRuntimeComponent>()?.Progress;
            bool isAdult = adult != null && adult.IsAdult;
            string stableKey = string.IsNullOrEmpty(progress.NextObjectiveId) ? "student-day.current" : "student-day." + progress.NextObjectiveId;
            string title = isAdult
                ? Text(context, "ui.objectives.career_day_guide", "Career Day Guide")
                : Text(context, "ui.objectives.student_day_guide", "Student Day Guide");
            string hint;
            if (progress.HasPendingMinigame || (adult != null && adult.HasPendingWork))
                hint = Text(context, "ui.objectives.pending_activity_hint", "Finish the activity already in progress first.");
            else if (progress.TimePointsRemaining <= 0)
                hint = Text(context, "ui.objectives.finish_day_hint", "Today's activities are complete. Return home to finish the day.");
            else if (isAdult)
                hint = Text(context, "ui.objectives.career_day_hint", "Work at your facility or review its operations and next upgrade.");
            else
            {
                var stage = context.Registry != null ? context.Registry.TutorialStages?.FirstOrDefault(s => s != null && s.Id == progress.TutorialStageId) : null;
                string authored = stage != null ? stage.GuideText : progress.NextGuideText;
                hint = string.IsNullOrEmpty(authored)
                    ? Text(context, "ui.objectives.student_day_hint", "Choose a town activity or relationship action.")
                    : Text(context, authored, authored);
            }
            string format = Text(context, "ui.objectives.day_progress", "Day {0} | Energy {1} | Focus {2}");
            string progressText = string.Format(CultureInfo.CurrentCulture, format, progress.CurrentDay, progress.Energy, progress.Focus);
            return new[] { new ObjectiveJournalItem(stableKey, Category.CategoryId, title, progress.TutorialStageId, progress.DayState.ToString(), progressText, hint, false, 0) };
        }

        private static string Text(ObjectiveJournalContext context, string key, string fallback)
        {
            var catalog = context.Registry != null ? context.Registry.TextCatalog : null;
            if (catalog == null) return fallback;
            string value = catalog.Get(key, GameText.Language);
            return value == key ? fallback : value;
        }
    }
}
