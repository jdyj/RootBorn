using System;
using Rootborn.Game.StudentLife;

namespace Rootborn.UI.Objectives
{
    public sealed class DailyPlanObjectiveSource : IObjectiveJournalSource
    {
        private static readonly ObjectiveJournalCategory CategoryValue = new ObjectiveJournalCategory("Goals", "Goals");

        public string SourceId => "daily-plan";
        public ObjectiveJournalCategory Category => CategoryValue;

        public ObjectiveJournalItem[] BuildItems(ObjectiveJournalContext context)
        {
            var routeSets = context.Registry != null ? context.Registry.DailyPlanRouteSets : Array.Empty<DailyPlanRouteSetDefinition>();
            var progress = context.Player != null ? context.Player.GetComponent<DailyPlanProgressComponent>()?.Progress : null;
            if (progress == null || string.IsNullOrEmpty(progress.SelectedRouteId))
            {
                return new[] { BuildChooseItem() };
            }

            var route = new DailyPlanRouteRunner().FindRoute(routeSets, progress.SelectedRouteId);
            if (route == null)
            {
                return new[] { BuildChooseItem() };
            }

            bool completed = progress.CompletedRouteId == route.Id;
            string state = completed ? "Completed" : "Selected";
            string progressText = completed
                ? "Completed with " + (string.IsNullOrEmpty(progress.MatchedActivityId) ? "matching activity" : progress.MatchedActivityId)
                : "Selected for day " + progress.CurrentDay;
            string hint = completed ? route.NextHintKey : route.ObjectiveDetailKey;
            return new[]
            {
                new ObjectiveJournalItem(
                    "daily-plan:" + route.Id,
                    Category.CategoryId,
                    route.ObjectiveTitleKey,
                    route.DisplayNameKey,
                    state,
                    progressText,
                    hint,
                    false,
                    completed ? 7 : 5)
            };
        }

        private static ObjectiveJournalItem BuildChooseItem()
        {
            return new ObjectiveJournalItem(
                "daily-plan:choose",
                CategoryValue.CategoryId,
                "objective.daily.choose.title",
                string.Empty,
                "Open",
                "No daily route selected",
                "objective.daily.choose.detail",
                false,
                5);
        }
    }
}
