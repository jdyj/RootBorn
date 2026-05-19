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
                    if (state == QuestState.RewardClaimed)
                    {
                        continue;
                    }

                    items.Add(new ObjectiveJournalItem(
                        "quest." + quest.Id,
                        Category.CategoryId,
                        SafeText(quest.DisplayNameKey, quest.Id),
                        SafeText(quest.DescriptionKey, string.Empty),
                        state == QuestState.NotStarted ? "Available" : state.ToString(),
                        BuildObjectiveText(questPanel.QuestLog, quest),
                        state == QuestState.NotStarted ? "Accept this quest from its provider." : "Review the quest detail and complete the next objective.",
                        false,
                        state == QuestState.Completed ? 10 : state == QuestState.Active ? 20 : 40));
                }

                return items.ToArray();
            }

            private static string BuildObjectiveText(QuestLog questLog, QuestDefinition quest)
            {
                if (quest.Objectives == null || quest.Objectives.Length == 0 || quest.Objectives[0] == null)
                {
                    return questLog.GetState(quest).ToString();
                }

                var objective = quest.Objectives[0];
                return SafeText(objective.DisplayKey, objective.GetType().Name) + " " + questLog.GetObjectiveCount(quest, 0) + " / " + objective.RequiredCount;
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
                        "Campaign day " + day.DayNumber,
                        day.NextGuideText,
                        false,
                        0)
                };
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
                    if (milestone == null || string.IsNullOrEmpty(milestone.Id) || progress.IsRewardClaimed(milestone))
                    {
                        continue;
                    }

                    items.Add(new ObjectiveJournalItem(
                        "milestone." + milestone.Id,
                        Category.CategoryId,
                        SafeText(milestone.DisplayNameKey, milestone.Id),
                        "Milestone growth route",
                        progress.IsCompleted(milestone) ? "Completed" : "Active",
                        BuildMilestoneProgressText(progress, milestone),
                        "Use any matching growth route; school is not the only path.",
                        false,
                        progress.IsCompleted(milestone) ? 10 : 30));
                }

                return items.ToArray();
            }

            private static string BuildMilestoneProgressText(MilestoneProgress progress, MilestoneDefinition milestone)
            {
                var objectives = milestone.Objectives ?? Array.Empty<MilestoneObjectiveBase>();
                if (objectives.Length == 0)
                {
                    return progress.IsCompleted(milestone) ? "Completed" : "Available";
                }

                int completed = 0;
                for (int i = 0; i < objectives.Length; i++)
                {
                    var objective = objectives[i];
                    if (objective == null)
                    {
                        continue;
                    }

                    if (progress.GetObjectiveProgress(milestone, objective) > 0)
                    {
                        completed++;
                    }
                }

                return completed + " / " + Mathf.Max(1, milestone.RequiredObjectiveCount) + " objectives";
            }
        }

        private static string SafeText(string primary, string fallback)
        {
            return string.IsNullOrEmpty(primary) ? fallback : primary;
        }
    }
}
