using System;
using System.Collections.Generic;
using Rootborn.Game.StudentLife;

namespace Rootborn.UI.Objectives
{
    public sealed class MilestoneObjectiveSource : IObjectiveJournalSource
    {
        private static readonly ObjectiveJournalCategory CategoryValue = new ObjectiveJournalCategory("Hints", "Hints");

        public string SourceId => "milestones";
        public ObjectiveJournalCategory Category => CategoryValue;

        public ObjectiveJournalItem[] BuildItems(ObjectiveJournalContext context)
        {
            var milestones = MilestoneRegistryResolver.ResolveMilestones(context.Registry);
            if (milestones == null || milestones.Length == 0)
            {
                return Array.Empty<ObjectiveJournalItem>();
            }

            var student = context.Player != null ? context.Player.GetComponent<StudentLifeProgressComponent>()?.Progress : null;
            var progress = student != null ? MilestoneProgressPersistence.LoadOrCreate(student.SaveSlot, student.PlayerId, milestones) : new MilestoneProgress("default", "player", milestones);
            var items = new List<ObjectiveJournalItem>();
            for (int i = 0; i < milestones.Length; i++)
            {
                var milestone = milestones[i];
                if (milestone == null || string.IsNullOrEmpty(milestone.Id) || !PrerequisitesSatisfied(progress, milestone))
                {
                    continue;
                }

                items.Add(new ObjectiveJournalItem(
                    "milestone." + milestone.Id,
                    Category.CategoryId,
                    ObjectiveJournalSourceText.SafeText(milestone.DisplayNameKey, milestone.Id),
                    "Milestone growth route",
                    BuildMilestoneStateText(progress, milestone),
                    BuildMilestoneProgressText(progress, milestone),
                    BuildMilestoneHintText(progress, milestone),
                    false,
                    BuildMilestoneSortPriority(progress, milestone)));
            }

            return items.ToArray();
        }

        private static bool PrerequisitesSatisfied(MilestoneProgress progress, MilestoneDefinition milestone)
        {
            var prerequisites = milestone != null ? milestone.PrerequisiteMilestones : null;
            if (prerequisites == null || prerequisites.Length == 0)
            {
                return true;
            }

            for (int i = 0; i < prerequisites.Length; i++)
            {
                var prerequisite = prerequisites[i];
                if (prerequisite != null && !progress.IsCompleted(prerequisite))
                {
                    return false;
                }
            }

            return true;
        }

        private static string BuildMilestoneStateText(MilestoneProgress progress, MilestoneDefinition milestone)
        {
            if (progress.IsRewardClaimed(milestone))
            {
                return "RewardClaimed";
            }

            return progress.IsCompleted(milestone) ? "Completed" : "Active";
        }

        private static string BuildMilestoneHintText(MilestoneProgress progress, MilestoneDefinition milestone)
        {
            if (progress.IsRewardClaimed(milestone))
            {
                return "Reward claimed. Review this completed growth route.";
            }

            if (progress.IsCompleted(milestone))
            {
                return "Claim the milestone reward when inventory can accept it.";
            }

            string routeText = BuildMilestoneRouteText(milestone);
            string hint = "Use any matching growth route; school is not the only path.";
            return string.IsNullOrEmpty(routeText) ? hint : hint + " Routes: " + routeText;
        }

        private static string BuildMilestoneRouteText(MilestoneDefinition milestone)
        {
            var routes = milestone != null ? milestone.Routes : null;
            if (routes == null || routes.Length == 0)
            {
                return string.Empty;
            }

            var ids = new List<string>();
            for (int i = 0; i < routes.Length; i++)
            {
                string id = routes[i] != null ? routes[i].RecommendedActionId : string.Empty;
                if (!string.IsNullOrEmpty(id) && !ids.Contains(id))
                {
                    ids.Add(id);
                }
            }

            return string.Join(" | ", ids.ToArray());
        }

        private static int BuildMilestoneSortPriority(MilestoneProgress progress, MilestoneDefinition milestone)
        {
            if (progress.IsCompleted(milestone) && !progress.IsRewardClaimed(milestone))
            {
                return 10;
            }

            return progress.IsRewardClaimed(milestone) ? 60 : 30;
        }

        private static string BuildMilestoneProgressText(MilestoneProgress progress, MilestoneDefinition milestone)
        {
            var objectives = milestone.Objectives ?? Array.Empty<MilestoneObjectiveBase>();
            if (objectives.Length == 0)
            {
                return progress.IsCompleted(milestone) ? "Completed" : "Available";
            }

            string text = string.Empty;
            for (int i = 0; i < objectives.Length; i++)
            {
                var objective = objectives[i];
                if (objective == null)
                {
                    continue;
                }

                string line = objective.Id + " " + progress.GetObjectiveProgress(milestone, objective) + " / " + objective.RequiredValue;
                text += string.IsNullOrEmpty(text) ? line : " | " + line;
            }

            return string.IsNullOrEmpty(text) ? (progress.IsCompleted(milestone) ? "Completed" : "Available") : text;
        }
    }
}
