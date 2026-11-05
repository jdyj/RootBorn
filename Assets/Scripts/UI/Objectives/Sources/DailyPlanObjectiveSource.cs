using System;
using System.Collections.Generic;
using Rootborn.Game.Localization;
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
            var component = context.Player != null ? context.Player.GetComponent<DailyPlanProgressComponent>() : null;
            var student = context.Player != null ? context.Player.GetComponent<StudentLifeProgressComponent>()?.Progress : null;
            var progress = student != null ? component?.EnsureProgress(student) : component?.Progress;
            if (progress == null) return new[] { BuildChooseItem() };
            var items = new List<ObjectiveJournalItem>(progress.Slots.Count);
            var routeKeys = new HashSet<string>(StringComparer.Ordinal);
            bool hasUnassigned = false;
            var runner = new DailyPlanRouteRunner();
            foreach (var slot in progress.Slots)
            {
                if (slot.Completed) continue;
                var route = runner.FindRoute(routeSets, slot.RouteId);
                if (route == null) { hasUnassigned = true; continue; }
                // Preserve the legacy tracked key for a route's first pending slot;
                // additional repeated-practice slots receive distinct keys.
                string key = "daily-plan:" + route.Id;
                if (!routeKeys.Add(route.Id)) key += ":slot:" + slot.SlotIndex;
                string subtitle = progress.Slots.Count > 1
                    ? GameText.Get(route.DisplayNameKey) + " · " + GameText.Get(slot.PeriodKey) : route.DisplayNameKey;
                items.Add(new ObjectiveJournalItem(key, Category.CategoryId, route.ObjectiveTitleKey,
                    subtitle, ObjectiveJournalSourceText.State("Selected"), ObjectiveJournalSourceText.LocalizedFormat("ui.journal.daily.selected-for-day", "Selected for day {0}", progress.CurrentDay),
                    route.ObjectiveDetailKey, false, 5 + slot.SlotIndex));
            }
            if (items.Count == 0 && hasUnassigned) items.Add(BuildChooseItem());
            return items.ToArray();
        }
        private static ObjectiveJournalItem BuildChooseItem()
        {
            return new ObjectiveJournalItem(
                "daily-plan:choose",
                CategoryValue.CategoryId,
                "objective.daily.choose.title",
                string.Empty,
                ObjectiveJournalSourceText.State("Open"),
                ObjectiveJournalSourceText.Localized("ui.journal.daily.none", "No daily route selected"),
                "objective.daily.choose.detail",
                false,
                5);
        }
    }
}
