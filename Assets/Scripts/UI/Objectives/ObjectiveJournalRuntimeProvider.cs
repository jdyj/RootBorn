using System;
using System.Collections.Generic;
using Rootborn.Game.Common;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.UI.Quests;
using UnityEngine;

namespace Rootborn.UI.Objectives
{
    [DisallowMultipleComponent]
    public sealed class ObjectiveJournalRuntimeProvider : MonoBehaviour
    {
        private ObjectiveJournalPanel _panel;
        private TrackedObjectiveHud _trackedHud;
        private GameObject _player;
        private ObjectiveJournalAggregator _aggregator;

        public ObjectiveJournalPanel Panel => _panel;
        public TrackedObjectiveHud TrackedHud => _trackedHud;

        public void Bind(ObjectiveJournalPanel panel, TrackedObjectiveHud trackedHud, GameObject player)
        {
            _panel = panel;
            _trackedHud = trackedHud;
            _player = player;
            if (_panel != null)
            {
                _panel.BindTrackedHud(_trackedHud);
            }

            RebuildAggregator();
            RefreshNow();
        }

        public void RefreshNow()
        {
            if (_panel == null)
            {
                return;
            }

            RebuildAggregator();
            GameDataRegistryProvider.TryGet(out var registry);
            var metadata = ActiveSaveContext.Metadata;
            string trackedStableKey = string.Empty;
            if (ObjectiveJournalTrackingStore.TryLoad(metadata, out _, out var savedStableKey))
            {
                trackedStableKey = savedStableKey;
            }

            _aggregator.Refresh(_panel, new ObjectiveJournalContext(registry, _player, metadata), trackedStableKey);
        }

        private void RebuildAggregator()
        {
            _aggregator = new ObjectiveJournalAggregator(BuildSources());
        }

        private IEnumerable<IObjectiveJournalSource> BuildSources()
        {
            yield return new StudentDayGuideObjectiveSource();
            yield return new QuestLogObjectiveSource();
            yield return new CampaignObjectiveSource();
            yield return new MilestoneObjectiveSource();
        }

        private sealed class StudentDayGuideObjectiveSource : IObjectiveJournalSource
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

        private sealed class QuestLogObjectiveSource : IObjectiveJournalSource
        {
            private static readonly ObjectiveJournalCategory CategoryValue = new ObjectiveJournalCategory("Quests", "Quests");

            public string SourceId => "quest-log";
            public ObjectiveJournalCategory Category => CategoryValue;

            public ObjectiveJournalItem[] BuildItems(ObjectiveJournalContext context)
            {
                var questPanel = FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include);
                if (questPanel == null || questPanel.QuestLog == null || questPanel.Quests == null)
                {
                    return Array.Empty<ObjectiveJournalItem>();
                }

                var items = new List<ObjectiveJournalItem>();
                for (int i = 0; i < questPanel.Quests.Length; i++)
                {
                    var quest = questPanel.Quests[i];
                    if (quest == null || string.IsNullOrEmpty(quest.Id))
                    {
                        continue;
                    }

                    var state = questPanel.QuestLog.GetState(quest);
                    items.Add(new ObjectiveJournalItem(
                        "quest." + quest.Id,
                        Category.CategoryId,
                        SafeText(quest.DisplayNameKey, quest.Id),
                        SafeText(quest.DescriptionKey, string.Empty),
                        BuildQuestStateText(state),
                        BuildObjectiveText(questPanel.QuestLog, quest),
                        BuildQuestHintText(state),
                        false,
                        BuildQuestSortPriority(state)));
                }

                return items.ToArray();
            }

            private static string BuildQuestStateText(QuestState state)
            {
                return state == QuestState.NotStarted ? "Available" : state.ToString();
            }

            private static string BuildQuestHintText(QuestState state)
            {
                if (state == QuestState.NotStarted)
                {
                    return "Accept this quest from its provider.";
                }

                if (state == QuestState.RewardClaimed)
                {
                    return "Reward claimed. Review this completed quest history.";
                }

                return state == QuestState.Completed ? "Claim the quest reward when inventory can accept it." : "Review the quest detail and complete the next objective.";
            }

            private static int BuildQuestSortPriority(QuestState state)
            {
                if (state == QuestState.Completed)
                {
                    return 10;
                }

                if (state == QuestState.Active)
                {
                    return 20;
                }

                if (state == QuestState.NotStarted)
                {
                    return 40;
                }

                return 60;
            }

            private static string BuildObjectiveText(QuestLog questLog, QuestDefinition quest)
            {
                if (quest.Objectives == null || quest.Objectives.Length == 0)
                {
                    return questLog.GetState(quest).ToString();
                }

                string text = string.Empty;
                for (int i = 0; i < quest.Objectives.Length; i++)
                {
                    var objective = quest.Objectives[i];
                    if (objective == null)
                    {
                        continue;
                    }

                    string label = SafeText(objective.DisplayKey, objective.GetType().Name);
                    string line = label + " " + questLog.GetObjectiveCount(quest, i) + " / " + objective.RequiredCount;
                    text += string.IsNullOrEmpty(text) ? line : " | " + line;
                }

                return string.IsNullOrEmpty(text) ? questLog.GetState(quest).ToString() : text;
            }
        }

        private sealed class CampaignObjectiveSource : IObjectiveJournalSource
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
                        SafeText(day.ThemeKey, component.Campaign.DisplayNameKey),
                        SafeText(day.DirectionText, component.Campaign.DescriptionKey),
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
                    text += " | Route: " + SafeText(selectedRoute.DisplayNameKey, selectedRoute.Id);
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
                        names.Add(SafeText(route.DisplayNameKey, route.Id));
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

        private sealed class MilestoneObjectiveSource : IObjectiveJournalSource
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
                    if (milestone == null || string.IsNullOrEmpty(milestone.Id))
                    {
                        continue;
                    }

                    items.Add(new ObjectiveJournalItem(
                        "milestone." + milestone.Id,
                        Category.CategoryId,
                        SafeText(milestone.DisplayNameKey, milestone.Id),
                        "Milestone growth route",
                        BuildMilestoneStateText(progress, milestone),
                        BuildMilestoneProgressText(progress, milestone),
                        BuildMilestoneHintText(progress, milestone),
                        false,
                        BuildMilestoneSortPriority(progress, milestone)));
                }

                return items.ToArray();
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

        private static string SafeText(string primary, string fallback)
        {
            return string.IsNullOrEmpty(primary) ? fallback : primary;
        }
    }
}