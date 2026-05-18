using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Rootborn.Game.Common;
using Rootborn.Game.Save;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    public readonly struct CampaignEvaluationContext
    {
        public readonly StudentLifeProgress StudentProgress;
        public readonly CampaignProgress CampaignProgress;
        public readonly IReadOnlyList<LocationDefinition> VisitedLocations;
        public readonly IReadOnlyList<string> EventIds;

        public CampaignEvaluationContext(StudentLifeProgress studentProgress, CampaignProgress campaignProgress, IReadOnlyList<LocationDefinition> visitedLocations, IReadOnlyList<string> eventIds)
        {
            StudentProgress = studentProgress;
            CampaignProgress = campaignProgress;
            VisitedLocations = visitedLocations ?? Array.Empty<LocationDefinition>();
            EventIds = eventIds ?? Array.Empty<string>();
        }
    }

    public readonly struct CampaignObjectiveEvaluation
    {
        public readonly int Current;
        public readonly int Required;
        public bool IsComplete => Current >= Required;

        public CampaignObjectiveEvaluation(int current, int required)
        {
            Required = Mathf.Max(1, required);
            Current = Mathf.Max(0, current);
        }
    }

    [Serializable]
    public sealed class CampaignProgressSaveData
    {
        public string SaveSlot;
        public string PlayerId;
        public string ActiveCampaignId;
        public int CurrentDayNumber = 1;
        public string[] CompletedObjectiveIds = Array.Empty<string>();
        public SelectedRouteSaveData[] SelectedRoutes = Array.Empty<SelectedRouteSaveData>();
        public string[] RewardClaimedIds = Array.Empty<string>();
        public string[] AppliedResultIds = Array.Empty<string>();
        public string[] LastProgressLogIds = Array.Empty<string>();

        [Serializable]
        public struct SelectedRouteSaveData
        {
            public string DayId;
            public string RouteId;
        }
    }

    public sealed class CampaignProgress
    {
        private readonly HashSet<string> _completedObjectiveIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _selectedRouteIdsByDay = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly HashSet<string> _rewardClaimedIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _appliedResultIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> _lastProgressLogIds = new List<string>();

        public CampaignProgress(string saveSlot, string playerId, CampaignDefinition campaign)
        {
            SaveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            PlayerId = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            ActiveCampaignId = campaign != null ? campaign.Id : string.Empty;
            CurrentDayNumber = 1;
        }

        public string SaveSlot { get; }
        public string PlayerId { get; }
        public string ActiveCampaignId { get; private set; }
        public int CurrentDayNumber { get; private set; }
        public string[] LastProgressLogIds => _lastProgressLogIds.ToArray();

        public void SetCurrentDayNumber(int dayNumber) => CurrentDayNumber = Mathf.Max(1, dayNumber);
        public bool MarkResultApplied(string resultId) => !string.IsNullOrEmpty(resultId) && _appliedResultIds.Add(resultId);
        public bool IsObjectiveCompleted(string objectiveId) => !string.IsNullOrEmpty(objectiveId) && _completedObjectiveIds.Contains(objectiveId);

        public bool CompleteObjective(string objectiveId)
        {
            if (string.IsNullOrEmpty(objectiveId)) return false;
            bool added = _completedObjectiveIds.Add(objectiveId);
            if (added && !_lastProgressLogIds.Contains(objectiveId)) _lastProgressLogIds.Add(objectiveId);
            return added;
        }

        public bool SelectRoute(CampaignDayDefinition day, CampaignRouteDefinition route)
        {
            if (day == null || route == null || string.IsNullOrEmpty(day.Id) || string.IsNullOrEmpty(route.Id)) return false;
            _selectedRouteIdsByDay[day.Id] = route.Id;
            return true;
        }

        public string GetSelectedRouteId(string dayId)
        {
            return !string.IsNullOrEmpty(dayId) && _selectedRouteIdsByDay.TryGetValue(dayId, out var routeId) ? routeId : string.Empty;
        }

        public bool TryClaimRewards(CampaignDefinition campaign, string requestId)
        {
            return TryClaimRewards(campaign, null, requestId);
        }

        public bool TryClaimRewards(CampaignDefinition campaign, StudentLifeProgress studentProgress, string requestId)
        {
            if (campaign == null || string.IsNullOrEmpty(requestId) || _rewardClaimedIds.Contains(requestId)) return false;
            var rewards = campaign.Rewards ?? Array.Empty<CampaignRewardBase>();
            for (int i = 0; i < rewards.Length; i++) if (rewards[i] != null && !rewards[i].CanApply(studentProgress)) return false;
            _rewardClaimedIds.Add(requestId);
            for (int i = 0; i < rewards.Length; i++)
            {
                var reward = rewards[i];
                if (reward == null) continue;
                if (!string.IsNullOrEmpty(reward.Id)) _rewardClaimedIds.Add(reward.Id);
                reward.Apply(studentProgress);
            }
            return true;
        }

        public CampaignProgressSaveData ToSaveData()
        {
            var selected = new List<CampaignProgressSaveData.SelectedRouteSaveData>();
            foreach (var pair in _selectedRouteIdsByDay) selected.Add(new CampaignProgressSaveData.SelectedRouteSaveData { DayId = pair.Key, RouteId = pair.Value });
            return new CampaignProgressSaveData
            {
                SaveSlot = SaveSlot,
                PlayerId = PlayerId,
                ActiveCampaignId = ActiveCampaignId,
                CurrentDayNumber = CurrentDayNumber,
                CompletedObjectiveIds = ToArray(_completedObjectiveIds),
                SelectedRoutes = selected.ToArray(),
                RewardClaimedIds = ToArray(_rewardClaimedIds),
                AppliedResultIds = ToArray(_appliedResultIds),
                LastProgressLogIds = _lastProgressLogIds.ToArray(),
            };
        }

        public static CampaignProgress FromSaveData(CampaignProgressSaveData saveData, CampaignDefinition campaign)
        {
            var progress = new CampaignProgress(saveData != null ? saveData.SaveSlot : "default", saveData != null ? saveData.PlayerId : "player", campaign);
            if (saveData == null) return progress;
            progress.ActiveCampaignId = string.IsNullOrEmpty(saveData.ActiveCampaignId) ? (campaign != null ? campaign.Id : string.Empty) : saveData.ActiveCampaignId;
            progress.CurrentDayNumber = Mathf.Max(1, saveData.CurrentDayNumber);
            AddAll(progress._completedObjectiveIds, saveData.CompletedObjectiveIds);
            AddAll(progress._rewardClaimedIds, saveData.RewardClaimedIds);
            AddAll(progress._appliedResultIds, saveData.AppliedResultIds);
            if (saveData.SelectedRoutes != null)
            {
                for (int i = 0; i < saveData.SelectedRoutes.Length; i++) if (!string.IsNullOrEmpty(saveData.SelectedRoutes[i].DayId)) progress._selectedRouteIdsByDay[saveData.SelectedRoutes[i].DayId] = saveData.SelectedRoutes[i].RouteId;
            }
            if (saveData.LastProgressLogIds != null)
            {
                for (int i = 0; i < saveData.LastProgressLogIds.Length; i++) if (!string.IsNullOrEmpty(saveData.LastProgressLogIds[i])) progress._lastProgressLogIds.Add(saveData.LastProgressLogIds[i]);
            }
            return progress;
        }

        private static void AddAll(HashSet<string> target, string[] values)
        {
            if (values == null) return;
            for (int i = 0; i < values.Length; i++) if (!string.IsNullOrEmpty(values[i])) target.Add(values[i]);
        }

        private static string[] ToArray(HashSet<string> values)
        {
            var result = new string[values.Count];
            values.CopyTo(result);
            return result;
        }
    }

    [DisallowMultipleComponent]
    public sealed class CampaignProgressComponent : MonoBehaviour
    {
        public CampaignProgress Progress { get; private set; }
        public CampaignDefinition Campaign { get; private set; }

        public CampaignProgress EnsureProgress(CampaignDefinition campaign, StudentLifeProgress studentProgress)
        {
            Campaign = campaign != null ? campaign : Campaign;
            if (Progress == null)
            {
                string saveSlot = studentProgress != null ? studentProgress.SaveSlot : "default";
                string playerId = studentProgress != null ? studentProgress.PlayerId : "player";
                Progress = new CampaignProgress(saveSlot, playerId, Campaign);
            }
            if (studentProgress != null) Progress.SetCurrentDayNumber(studentProgress.CurrentDay);
            return Progress;
        }

        public void Restore(CampaignDefinition campaign, CampaignProgressSaveData saveData, StudentLifeProgress studentProgress)
        {
            Campaign = campaign;
            Progress = CampaignProgress.FromSaveData(saveData, campaign);
            if (studentProgress != null) Progress.SetCurrentDayNumber(studentProgress.CurrentDay);
        }
    }

    public static class CampaignProgressPersistence
    {
        private const string FileName = "campaign-progress.json";

        public static bool TryLoad(CampaignProgressComponent component, CampaignDefinition campaign, StudentLifeProgress studentProgress)
        {
            if (component == null || studentProgress == null) return false;
            string json = new SaveService(studentProgress.SaveSlot).ReadJson(FileNameFor(studentProgress.PlayerId));
            if (string.IsNullOrEmpty(json)) return false;
            component.Restore(campaign, JsonUtility.FromJson<CampaignProgressSaveData>(json), studentProgress);
            return true;
        }

        public static void Save(CampaignProgress progress)
        {
            if (progress == null) return;
            string json = JsonUtility.ToJson(progress.ToSaveData(), true);
            new SaveService(progress.SaveSlot).WriteJson(FileNameFor(progress.PlayerId), json);
        }

        public static string FileNameFor(string playerId)
        {
            return string.IsNullOrEmpty(playerId) || playerId == "player" || playerId == "local-player" ? FileName : "campaign-progress-" + Sanitize(playerId) + ".json";
        }

        private static string Sanitize(string value)
        {
            var chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++) if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '-' && chars[i] != '_') chars[i] = '_';
            return new string(chars);
        }
    }

    public readonly struct CampaignApplyResult
    {
        public readonly string[] CompletedObjectiveIds;

        public CampaignApplyResult(string[] completedObjectiveIds)
        {
            CompletedObjectiveIds = completedObjectiveIds ?? Array.Empty<string>();
        }
    }

    public sealed class CampaignRunner
    {
        public bool TryApplyProgress(CampaignDefinition campaign, CampaignEvaluationContext context, string resultId, out CampaignApplyResult result)
        {
            result = new CampaignApplyResult(Array.Empty<string>());
            if (campaign == null || context.CampaignProgress == null || string.IsNullOrEmpty(resultId)) return false;
            if (!context.CampaignProgress.MarkResultApplied(resultId)) return false;
            var completed = new List<string>();
            var days = campaign.Days ?? Array.Empty<CampaignDayDefinition>();
            for (int i = 0; i < days.Length; i++)
            {
                var routes = days[i] != null ? days[i].Routes : null;
                if (routes == null) continue;
                for (int j = 0; j < routes.Length; j++)
                {
                    var objectives = routes[j] != null ? routes[j].Objectives : null;
                    if (objectives == null) continue;
                    for (int k = 0; k < objectives.Length; k++)
                    {
                        var objective = objectives[k];
                        if (objective == null || context.CampaignProgress.IsObjectiveCompleted(objective.Id)) continue;
                        if (!objective.Evaluate(in context).IsComplete) continue;
                        if (context.CampaignProgress.CompleteObjective(objective.Id)) completed.Add(objective.Id);
                    }
                }
            }

            result = new CampaignApplyResult(completed.ToArray());
            return completed.Count > 0;
        }
    }

    public readonly struct CampaignHudSummary
    {
        public readonly string TodayDirection;
        public readonly string AvailableRoutesText;
        public readonly string CurrentProgressText;
        public readonly string NextGuideText;

        public CampaignHudSummary(string todayDirection, string availableRoutesText, string currentProgressText, string nextGuideText)
        {
            TodayDirection = todayDirection ?? string.Empty;
            AvailableRoutesText = availableRoutesText ?? string.Empty;
            CurrentProgressText = currentProgressText ?? string.Empty;
            NextGuideText = nextGuideText ?? string.Empty;
        }
    }

    public readonly struct CampaignDayResultSummary
    {
        public readonly string SelectedRouteText;
        public readonly string ProgressText;
        public readonly string NextGuideText;

        public CampaignDayResultSummary(string selectedRouteText, string progressText, string nextGuideText)
        {
            SelectedRouteText = selectedRouteText ?? string.Empty;
            ProgressText = progressText ?? string.Empty;
            NextGuideText = nextGuideText ?? string.Empty;
        }
    }

    public static class CampaignSummaryBuilder
    {
        public static CampaignHudSummary BuildHudSummary(CampaignDefinition campaign, CampaignProgress progress)
        {
            var day = campaign != null ? campaign.GetDayByNumber(progress != null ? progress.CurrentDayNumber : 1) : null;
            return new CampaignHudSummary(day != null ? day.ThemeKey : string.Empty, FormatRoutes(day), FormatProgress(progress), day != null ? day.NextGuideText : string.Empty);
        }

        public static CampaignDayResultSummary BuildDayResultSummary(CampaignDefinition campaign, CampaignProgress progress, CampaignApplyResult result)
        {
            var day = campaign != null ? campaign.GetDayByNumber(progress != null ? progress.CurrentDayNumber : 1) : null;
            string selected = day != null && progress != null ? progress.GetSelectedRouteId(day.Id) : string.Empty;
            string progressText = result.CompletedObjectiveIds != null && result.CompletedObjectiveIds.Length > 0 ? string.Join("\n", result.CompletedObjectiveIds) : FormatProgress(progress);
            return new CampaignDayResultSummary(selected, progressText, day != null ? day.NextGuideText : string.Empty);
        }

        private static string FormatRoutes(CampaignDayDefinition day)
        {
            if (day == null || day.Routes == null) return string.Empty;
            var names = new List<string>();
            for (int i = 0; i < day.Routes.Length; i++) if (day.Routes[i] != null) names.Add(day.Routes[i].DisplayNameKey);
            return string.Join("\n", names.ToArray());
        }

        private static string FormatProgress(CampaignProgress progress)
        {
            return progress == null ? string.Empty : string.Join("\n", progress.LastProgressLogIds);
        }
    }

    public static class GameDataRegistryCampaignExtensions
    {
        private static readonly ConditionalWeakTable<GameDataRegistry, CampaignRegistryData> Data = new ConditionalWeakTable<GameDataRegistry, CampaignRegistryData>();

        public static void ConfigureCampaignsForTests(this GameDataRegistry registry, CampaignDefinition[] campaigns)
        {
            if (registry == null) return;
            Data.Remove(registry);
            Data.Add(registry, new CampaignRegistryData(campaigns));
        }

        public static CampaignDefinition[] GetCampaigns(GameDataRegistry registry)
        {
            if (registry != null && Data.TryGetValue(registry, out var data)) return data.Campaigns;
            return Array.Empty<CampaignDefinition>();
        }

        private sealed class CampaignRegistryData
        {
            public readonly CampaignDefinition[] Campaigns;
            public CampaignRegistryData(CampaignDefinition[] campaigns) => Campaigns = campaigns ?? Array.Empty<CampaignDefinition>();
        }
    }
}
