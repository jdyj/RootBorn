using System;
using System.Collections.Generic;
using Rootborn.Game.Common;
using Rootborn.Game.DiscoveryClues;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.Game.WorldState;

namespace Rootborn.UI.Objectives
{
    public sealed class DailyPlanRouteHintObjectiveSource : IObjectiveJournalSource
    {
        private static readonly ObjectiveJournalCategory CategoryValue = new ObjectiveJournalCategory("Hints", "Hints");

        public string SourceId => "daily-plan-route-hints";
        public ObjectiveJournalCategory Category => CategoryValue;

        public ObjectiveJournalItem[] BuildItems(ObjectiveJournalContext context)
        {
            if (context.Registry == null || context.Player == null) return Array.Empty<ObjectiveJournalItem>();

            var routeSets = context.Registry.DailyPlanRouteSets;
            if (routeSets == null || routeSets.Length == 0) return Array.Empty<ObjectiveJournalItem>();

            var studentProgress = context.Player.GetComponent<StudentLifeProgressComponent>()?.EnsureProgress();
            if (studentProgress == null) return Array.Empty<ObjectiveJournalItem>();

            string saveSlot = ResolveSaveSlot(context, studentProgress);
            string playerId = ResolvePlayerId(context, studentProgress);
            var routeContext = new DailyPlanRouteContext(
                studentProgress,
                WorldStateProgressPersistence.LoadOrCreate(saveSlot, playerId),
                DiscoveryClueProgressPersistence.LoadOrCreate(saveSlot, playerId),
                ClueInterpretationProgressPersistence.LoadOrCreate(saveSlot, playerId));

            var dailyProgress = context.Player.GetComponent<DailyPlanProgressComponent>()?.EnsureProgress(studentProgress);
            var available = new DailyPlanRouteRunner().GetAvailableRoutes(routeSets, in routeContext);
            if (available.Length == 0) return Array.Empty<ObjectiveJournalItem>();

            var items = new List<ObjectiveJournalItem>();
            for (int i = 0; i < available.Length; i++)
            {
                var route = available[i];
                if (route == null || string.IsNullOrEmpty(route.Id)) continue;
                if (dailyProgress != null && string.Equals(dailyProgress.SelectedRouteId, route.Id, StringComparison.Ordinal)) continue;

                items.Add(new ObjectiveJournalItem(
                    "daily-plan-route-hint:" + route.Id,
                    Category.CategoryId,
                    route.ObjectiveTitleKey,
                    route.ObjectiveDetailKey,
                    "Available",
                    ObjectiveJournalSourceText.Localized("ui.journal.daily.route-unlocked", "Route unlocked"),
                    route.NextHintKey,
                    false,
                    20 + i));
            }

            return items.ToArray();
        }

        private static string ResolveSaveSlot(ObjectiveJournalContext context, StudentLifeProgress studentProgress)
        {
            if (context.ActiveSave != null && !string.IsNullOrEmpty(context.ActiveSave.SlotId)) return context.ActiveSave.SlotId;
            return string.IsNullOrEmpty(studentProgress.SaveSlot) ? "default" : studentProgress.SaveSlot;
        }

        private static string ResolvePlayerId(ObjectiveJournalContext context, StudentLifeProgress studentProgress)
        {
            var identity = context.Player != null ? context.Player.GetComponent<PlayerIdentity>() : null;
            if (identity != null && !string.IsNullOrEmpty(identity.PlayerId)) return identity.PlayerId;
            return string.IsNullOrEmpty(studentProgress.PlayerId) ? PlayerIdentity.DefaultPlayerId : studentProgress.PlayerId;
        }
    }
}