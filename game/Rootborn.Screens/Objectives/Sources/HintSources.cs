using System;
using System.Collections.Generic;
using Rootborn.Core.Gathering;
using Rootborn.Core.StudentLife;
using Rootborn.Game;

namespace Rootborn.Screens.Objectives.Sources
{
    /// <summary>열린 일과 길(Unity DailyPlanRouteHintObjectiveSource): 지금 고를 수 있는 길마다 한 줄(지금 고른 길은 뺀다).</summary>
    public sealed class DailyPlanRouteHintSource : IObjectiveJournalSource
    {
        public string SourceId => "daily-plan-route-hints";
        public string CategoryId => JournalCategories.Hints;

        public IReadOnlyList<JournalItem> BuildItems(GameSession session)
        {
            DailyPlanRoute[] available = session.AvailableDailyPlanRoutes();
            string selected = session.DailyPlan?.SelectedRouteId ?? string.Empty;
            var items = new List<JournalItem>(available.Length);
            for (int i = 0; i < available.Length; i++)
            {
                DailyPlanRoute route = available[i];
                if (route == null || string.IsNullOrEmpty(route.Id) || route.Id == selected) continue;
                items.Add(new JournalItem("daily-plan-route-hint:" + route.Id, CategoryId, JournalText.Data(route.ObjectiveTitleKey), JournalText.Data(route.ObjectiveDetailKey),
                    JournalSourceText.State("Available"), JournalText.Key("ui.journal.daily.route-unlocked"), JournalText.Data(route.NextHintKey), false, 20 + i));
            }
            return items;
        }
    }

    /// <summary>찾은 자원 레시피(Unity ResourceRecipeObjectiveSource).</summary>
    public sealed class ResourceRecipeSource : IObjectiveJournalSource
    {
        public string SourceId => "resource-recipes";
        public string CategoryId => JournalCategories.Hints;

        public IReadOnlyList<JournalItem> BuildItems(GameSession session)
        {
            IReadOnlyList<RecipeDefinition> recipes = session.Definitions.All<RecipeDefinition>();
            if (recipes.Count == 0) return Array.Empty<JournalItem>();
            ResourceRecipeProgress progress = session.ResourceRecipes();
            var items = new List<JournalItem>();
            for (int i = 0; i < recipes.Count; i++)
            {
                RecipeDefinition recipe = recipes[i];
                if (recipe == null || string.IsNullOrEmpty(recipe.Id) || !progress.IsRecipeRevealed(recipe.Id)) continue;
                items.Add(new JournalItem("resource-recipe:" + recipe.Id, CategoryId, JournalSourceText.Safe(recipe.DisplayNameKey, recipe.Id), JournalText.Key("ui.journal.recipe.subtitle"),
                    JournalSourceText.State("Revealed"), JournalText.Key("ui.journal.recipe.discovered"),
                    string.IsNullOrEmpty(recipe.NextHintKey) ? JournalText.Key("ui.journal.recipe.next-hint") : JournalText.Data(recipe.NextHintKey), false, 35 + i));
            }
            return items;
        }
    }

    /// <summary>바뀐 컨디션 상태(Unity StatusActivityObjectiveSource): registry studentConditionStatuses 중 값이 0이 아닌 것.</summary>
    public sealed class StatusActivitySource : IObjectiveJournalSource
    {
        public string SourceId => "status-activity";
        public string CategoryId => JournalCategories.Hints;

        public IReadOnlyList<JournalItem> BuildItems(GameSession session)
        {
            IReadOnlyList<string> ids = session.Definitions.RegistryIds("studentConditionStatuses");
            var items = new List<JournalItem>();
            for (int i = 0; i < ids.Count; i++)
            {
                if (!session.Definitions.TryGet(ids[i] ?? string.Empty, out StatusDefinition status) || string.IsNullOrEmpty(status.Id)) continue;
                int value = session.Student.GetStatusValue(status.Id);
                if (value == 0) continue;
                JournalText name = JournalSourceText.Safe(status.DisplayNameKey, status.Id);
                items.Add(new JournalItem("status-activity:" + status.Id, CategoryId, name, JournalText.Key("ui.journal.status.subtitle"), JournalSourceText.State("Changed"),
                    JournalText.Key("ui.journal.status.value", name, value), JournalText.Key("ui.journal.status.next-hint"), false, 45 + i));
            }
            return items;
        }
    }

    /// <summary>성장 마일스톤(Unity MilestoneObjectiveSource): 앞선 마일스톤을 다 끝낸 것만.</summary>
    public sealed class MilestoneSource : IObjectiveJournalSource
    {
        public string SourceId => "milestones";
        public string CategoryId => JournalCategories.Hints;

        public IReadOnlyList<JournalItem> BuildItems(GameSession session)
        {
            IReadOnlyList<Milestone> milestones = session.Definitions.All<Milestone>();
            MilestoneProgress progress = session.Milestones;
            var items = new List<JournalItem>(milestones.Count);
            foreach (Milestone milestone in milestones)
            {
                if (milestone == null || string.IsNullOrEmpty(milestone.Id) || !Ready(progress, milestone)) continue;
                bool claimed = progress.IsRewardClaimed(milestone), completed = progress.IsCompleted(milestone);
                items.Add(new JournalItem("milestone." + milestone.Id, CategoryId, JournalSourceText.Safe(milestone.DisplayNameKey, milestone.Id), JournalText.Key("ui.journal.milestone.subtitle"),
                    JournalSourceText.State(claimed ? "RewardClaimed" : completed ? "Completed" : "Active"), Progress(progress, milestone, completed), Hint(milestone, claimed, completed), false,
                    completed && !claimed ? 10 : claimed ? 60 : 30));
            }
            return items;
        }

        private static bool Ready(MilestoneProgress progress, Milestone milestone)
        {
            foreach (string id in milestone.PrerequisiteMilestoneIds) if (!string.IsNullOrEmpty(id) && !progress.IsCompleted(id)) return false;
            return true;
        }

        private static JournalText Hint(Milestone milestone, bool claimed, bool completed)
        {
            if (claimed) return JournalText.Key("ui.journal.milestone.claimed-hint");
            if (completed) return JournalText.Key("ui.journal.milestone.claim-hint");
            var routes = new List<JournalText>(milestone.Routes.Count);
            foreach (MilestoneRoute route in milestone.Routes) if (route != null && !string.IsNullOrEmpty(route.RecommendedActionId)) routes.Add(JournalText.Data(route.RecommendedActionId));
            return routes.Count == 0 ? JournalText.Key("ui.journal.milestone.next-hint") : JournalText.Key("ui.journal.milestone.next-hint-routes", JournalText.List(routes));
        }

        private static JournalText Progress(MilestoneProgress progress, Milestone milestone, bool completed)
        {
            var lines = new List<JournalText>(milestone.Objectives.Count);
            foreach (MilestoneObjective objective in milestone.Objectives)
                if (objective != null) lines.Add(JournalSourceText.ObjectiveLine(JournalText.Data(objective.Id), progress.GetObjectiveProgress(milestone, objective), objective.RequiredValue));
            return JournalSourceText.Lines(lines, completed ? "Completed" : "Available");
        }
    }

    /// <summary>장래희망(Unity CareerInterestObjectiveSource): 장래희망마다 한 줄(지금 고른 것이 맨 위).</summary>
    public sealed class CareerInterestSource : IObjectiveJournalSource
    {
        public string SourceId => "career-interests";
        public string CategoryId => JournalCategories.Hints;

        public IReadOnlyList<JournalItem> BuildItems(GameSession session)
        {
            IReadOnlyList<CareerInterest> interests = session.Definitions.All<CareerInterest>();
            if (interests.Count == 0) return Array.Empty<JournalItem>();
            CareerInterestProgress progress = session.CareerInterests;
            JournalText current = JournalText.Key("dream.journal.none");
            foreach (CareerInterest interest in interests) if (interest != null && interest.Id == progress.CurrentInterestId) current = JournalText.Data(interest.DisplayNameKey);
            JournalText currentLine = JournalText.Key("dream.journal.current", current);
            var items = new List<JournalItem>(interests.Count);
            for (int i = 0; i < interests.Count; i++)
            {
                CareerInterest interest = interests[i];
                if (interest == null || string.IsNullOrEmpty(interest.Id)) continue;
                CareerInterestSummary summary = CareerInterestSummaryBuilder.Build(interest, progress, session.CareerCandidates, session.Student, session.StudentDay);
                bool selected = progress.CurrentInterestId == interest.Id;
                var recommendations = new List<JournalText>();
                foreach (string key in summary.RecommendedActionKeys ?? Array.Empty<string>()) recommendations.Add(JournalText.Data(key));
                JournalText hint = recommendations.Count == 0 ? JournalText.Key("dream.journal.hint") : JournalText.List(recommendations);
                items.Add(new JournalItem("career-interest." + interest.Id, CategoryId, JournalText.Data(summary.DisplayName), JournalText.Data(summary.Description),
                    JournalSourceText.State(summary.State.ToString()), currentLine, hint, selected, selected ? 10 : 40 + i));
            }
            return items;
        }
    }
}
