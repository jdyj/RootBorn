using System;
using System.Collections.Generic;
using Rootborn.Game.StudentLife;

namespace Rootborn.UI.Objectives
{
    public sealed class CampaignObjectiveSource : IObjectiveJournalSource
    {
        private static readonly ObjectiveJournalCategory CategoryValue = new ObjectiveJournalCategory("Campaign", "Campaign");

        public string SourceId => "campaign-progress";
        public ObjectiveJournalCategory Category => CategoryValue;

        public ObjectiveJournalItem[] BuildItems(ObjectiveJournalContext context)
        {
            var component = context.Player != null ? context.Player.GetComponent<CampaignProgressComponent>() : null;
            if (component == null || component.Progress == null || component.Campaign == null)
            {
                return Array.Empty<ObjectiveJournalItem>();
            }

            var student = context.Player.GetComponent<StudentLifeProgressComponent>()?.Progress;
            if (student != null)
            {
                component.Progress.SetCurrentDayNumber(student.CurrentDay);
            }

            var day = component.Campaign.GetDayByNumber(component.Progress.CurrentDayNumber);
            if (day == null)
            {
                return Array.Empty<ObjectiveJournalItem>();
            }

            return new[]
            {
                new ObjectiveJournalItem(
                    "campaign." + component.Campaign.Id + ".day." + day.DayNumber,
                    Category.CategoryId,
                    ObjectiveJournalSourceText.SafeText(day.ThemeKey, component.Campaign.DisplayNameKey),
                    ObjectiveJournalSourceText.SafeText(day.DirectionText, component.Campaign.DescriptionKey),
                    "Active",
                    BuildCampaignProgressText(component.Progress, day),
                    day.NextGuideText,
                    false,
                    0)
            };
        }

        private static string BuildCampaignProgressText(CampaignProgress progress, CampaignDayDefinition day)
        {
            string text = "Campaign day " + day.DayNumber;
            var selectedRoute = ResolveSelectedRoute(progress, day);
            if (selectedRoute != null)
            {
                text += " | Route: " + ObjectiveJournalSourceText.SafeText(selectedRoute.DisplayNameKey, selectedRoute.Id);
            }
            else
            {
                string routesText = BuildAvailableRouteText(day);
                if (!string.IsNullOrEmpty(routesText))
                {
                    text += " | Routes: " + routesText;
                }
            }

            var completed = progress != null ? progress.LastProgressLogIds : Array.Empty<string>();
            if (completed.Length > 0)
            {
                text += " | Completed: " + string.Join(", ", completed);
            }

            return text;
        }

        private static string BuildAvailableRouteText(CampaignDayDefinition day)
        {
            if (day == null || day.Routes == null || day.Routes.Length == 0)
            {
                return string.Empty;
            }

            var names = new List<string>();
            for (int i = 0; i < day.Routes.Length; i++)
            {
                var route = day.Routes[i];
                if (route != null)
                {
                    names.Add(ObjectiveJournalSourceText.SafeText(route.DisplayNameKey, route.Id));
                }
            }

            return string.Join(", ", names.ToArray());
        }

        private static CampaignRouteDefinition ResolveSelectedRoute(CampaignProgress progress, CampaignDayDefinition day)
        {
            if (progress == null || day == null)
            {
                return null;
            }

            string selectedRouteId = progress.GetSelectedRouteId(day.Id);
            if (string.IsNullOrEmpty(selectedRouteId) || day.Routes == null)
            {
                return null;
            }

            for (int i = 0; i < day.Routes.Length; i++)
            {
                var route = day.Routes[i];
                if (route != null && route.Id == selectedRouteId)
                {
                    return route;
                }
            }

            return null;
        }
    }
}
