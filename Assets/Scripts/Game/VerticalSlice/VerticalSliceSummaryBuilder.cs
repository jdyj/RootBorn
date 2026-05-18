using System.Collections.Generic;
using Rootborn.Game.StudentLife;

namespace Rootborn.Game.VerticalSlice
{
    public static class VerticalSliceSummaryBuilder
    {
        public static VerticalSliceSummary Build(StudentLifeProgress progress, bool questChanged, bool clueChanged, bool worldStateChanged, string houseMotivation)
        {
            var domains = new List<string>(4);
            if (progress != null && (progress.GetTodayActivityIds().Length > 0 || progress.GetTodayResultLogIds().Length > 0 || progress.GetCareerHintIds().Length > 0))
            {
                domains.Add("StudentLife");
            }

            if (questChanged) domains.Add("Quest");
            if (clueChanged) domains.Add("DiscoveryClue");
            if (worldStateChanged) domains.Add("WorldState");

            string nextObjective = domains.Count >= 2 ? "Choose the next town objective" : "Start a town activity";
            string nextAction = domains.Count >= 2 ? "Open Objective Journal and pick a follow-up route" : "Talk, study, help, or inspect a town object";
            string dayGuide = domains.Count >= 2 ? "Today connected " + string.Join(", ", domains) : "Start the first vertical slice route";
            string followUp = string.IsNullOrEmpty(houseMotivation) ? "House: visit later when a route points there" : houseMotivation;

            return new VerticalSliceSummary("vertical.slice.foundation", domains.ToArray(), nextObjective, nextAction, followUp, dayGuide);
        }
    }
}