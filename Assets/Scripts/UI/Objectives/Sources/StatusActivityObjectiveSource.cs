using System;
using System.Collections.Generic;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.UI.Objectives
{
    public sealed class StatusActivityObjectiveSource : IObjectiveJournalSource
    {
        private static readonly ObjectiveJournalCategory CategoryValue = new ObjectiveJournalCategory("Hints", "Hints");

        public string SourceId => "status-activity";
        public ObjectiveJournalCategory Category => CategoryValue;

        public ObjectiveJournalItem[] BuildItems(ObjectiveJournalContext context)
        {
            var statuses = context.Registry != null ? context.Registry.StudentConditionStatuses : Array.Empty<StatusDefinition>();
            if (statuses == null || statuses.Length == 0 || context.Player == null) return Array.Empty<ObjectiveJournalItem>();

            var progress = context.Player.GetComponent<StudentLifeProgressComponent>()?.EnsureProgress();
            if (progress == null) return Array.Empty<ObjectiveJournalItem>();

            var items = new List<ObjectiveJournalItem>();
            for (int i = 0; i < statuses.Length; i++)
            {
                var status = statuses[i];
                if (status == null || string.IsNullOrEmpty(status.Id)) continue;
                int value = progress.GetStatusValue(status);
                if (value == 0) continue;

                items.Add(new ObjectiveJournalItem(
                    "status-activity:" + status.Id,
                    Category.CategoryId,
                    string.IsNullOrEmpty(status.DisplayNameKey) ? status.Id : status.DisplayNameKey,
                    ObjectiveJournalSourceText.Localized("ui.journal.status.subtitle", "Status activity progress: " + status.Id),
                    "Changed",
                    status.Id + " = " + value,
                    ObjectiveJournalSourceText.Localized("ui.journal.status.next-hint", "Next: choose an activity that helps manage this status or turn it into another growth route."),
                    false,
                    45 + i));
            }

            return items.ToArray();
        }
    }
}
