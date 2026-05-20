using System;
using System.Collections.Generic;
using Rootborn.Game.Save;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [Serializable]
    public sealed class DailyPlanProgressSaveData
    {
        public string SaveSlot;
        public string PlayerId;
        public int CurrentDay = 1;
        public string SelectedRouteId;
        public string CompletedRouteId;
        public string MatchedActivityId;
        public string[] AppliedCompletionKeys = Array.Empty<string>();
        public string LastNextHintKey;
    }

    public sealed class DailyPlanProgress
    {
        private readonly HashSet<string> _appliedCompletionKeys = new HashSet<string>();

        public DailyPlanProgress(string saveSlot, string playerId, int currentDay)
        {
            SaveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            PlayerId = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            CurrentDay = Mathf.Max(1, currentDay);
            SelectedRouteId = string.Empty;
            CompletedRouteId = string.Empty;
            MatchedActivityId = string.Empty;
            LastNextHintKey = string.Empty;
        }

        public string SaveSlot { get; }
        public string PlayerId { get; }
        public int CurrentDay { get; private set; }
        public string SelectedRouteId { get; private set; }
        public string CompletedRouteId { get; private set; }
        public string MatchedActivityId { get; private set; }
        public string LastNextHintKey { get; private set; }
        public IReadOnlyCollection<string> AppliedCompletionKeys => _appliedCompletionKeys;

        public bool SelectRoute(DailyPlanRouteDefinition route)
        {
            if (route == null || string.IsNullOrEmpty(route.Id)) return false;
            if (SelectedRouteId == route.Id) return false;

            SelectedRouteId = route.Id;
            CompletedRouteId = string.Empty;
            MatchedActivityId = string.Empty;
            LastNextHintKey = route.NextHintKey;
            _appliedCompletionKeys.Clear();
            return true;
        }

        public bool CompleteRoute(DailyPlanRouteDefinition route, LocationActivityDefinition activity, string requestKey)
        {
            if (route == null || activity == null || string.IsNullOrEmpty(requestKey)) return false;
            if (SelectedRouteId != route.Id || CompletedRouteId == route.Id || _appliedCompletionKeys.Contains(requestKey)) return false;

            CompletedRouteId = route.Id;
            MatchedActivityId = activity.Id;
            LastNextHintKey = route.NextHintKey;
            _appliedCompletionKeys.Add(requestKey);
            return true;
        }

        public DailyPlanProgressSaveData ToSaveData()
        {
            var keys = new string[_appliedCompletionKeys.Count];
            _appliedCompletionKeys.CopyTo(keys);
            return new DailyPlanProgressSaveData
            {
                SaveSlot = SaveSlot,
                PlayerId = PlayerId,
                CurrentDay = CurrentDay,
                SelectedRouteId = SelectedRouteId,
                CompletedRouteId = CompletedRouteId,
                MatchedActivityId = MatchedActivityId,
                AppliedCompletionKeys = keys,
                LastNextHintKey = LastNextHintKey
            };
        }

        public static DailyPlanProgress FromSaveData(DailyPlanProgressSaveData saveData, string fallbackSaveSlot, string fallbackPlayerId, int fallbackDay)
        {
            var progress = new DailyPlanProgress(
                string.IsNullOrEmpty(saveData?.SaveSlot) ? fallbackSaveSlot : saveData.SaveSlot,
                string.IsNullOrEmpty(saveData?.PlayerId) ? fallbackPlayerId : saveData.PlayerId,
                saveData == null || saveData.CurrentDay <= 0 ? fallbackDay : saveData.CurrentDay);

            if (saveData == null) return progress;

            progress.SelectedRouteId = saveData.SelectedRouteId ?? string.Empty;
            progress.CompletedRouteId = saveData.CompletedRouteId ?? string.Empty;
            progress.MatchedActivityId = saveData.MatchedActivityId ?? string.Empty;
            progress.LastNextHintKey = saveData.LastNextHintKey ?? string.Empty;
            if (saveData.AppliedCompletionKeys != null)
            {
                for (int i = 0; i < saveData.AppliedCompletionKeys.Length; i++)
                {
                    if (!string.IsNullOrEmpty(saveData.AppliedCompletionKeys[i])) progress._appliedCompletionKeys.Add(saveData.AppliedCompletionKeys[i]);
                }
            }

            return progress;
        }
    }

    public readonly struct DailyPlanCompletionResult
    {
        public DailyPlanCompletionResult(bool isCompleted, DailyPlanRouteDefinition route, LocationActivityDefinition activity, string[] resultLogs)
        {
            IsCompleted = isCompleted;
            Route = route;
            Activity = activity;
            ResultLogs = resultLogs ?? Array.Empty<string>();
        }

        public bool IsCompleted { get; }
        public DailyPlanRouteDefinition Route { get; }
        public LocationActivityDefinition Activity { get; }
        public string[] ResultLogs { get; }

        public static DailyPlanCompletionResult Empty => new DailyPlanCompletionResult(false, null, null, Array.Empty<string>());
    }

    public sealed class DailyPlanRouteRunner
    {
        public DailyPlanRouteDefinition[] GetAvailableRoutes(DailyPlanRouteSetDefinition[] routeSets, StudentLifeProgress progress)
        {
            return GetAvailableRoutes(routeSets, new DailyPlanRouteContext(progress));
        }

        public DailyPlanRouteDefinition[] GetAvailableRoutes(DailyPlanRouteSetDefinition[] routeSets, in DailyPlanRouteContext context)
        {
            if (routeSets == null || context.StudentProgress == null) return Array.Empty<DailyPlanRouteDefinition>();
            var routes = new List<DailyPlanRouteDefinition>();
            for (int i = 0; i < routeSets.Length; i++)
            {
                if (routeSets[i] != null) routes.AddRange(routeSets[i].GetAvailableRoutes(in context));
            }

            return routes.ToArray();
        }

        public bool TrySelectRoute(DailyPlanProgress dailyProgress, DailyPlanRouteDefinition route, StudentLifeProgress studentProgress)
        {
            return TrySelectRoute(dailyProgress, route, new DailyPlanRouteContext(studentProgress));
        }

        public bool TrySelectRoute(DailyPlanProgress dailyProgress, DailyPlanRouteDefinition route, in DailyPlanRouteContext context)
        {
            if (dailyProgress == null || route == null || !route.IsAvailable(in context)) return false;
            return dailyProgress.SelectRoute(route);
        }

        public bool TryRecordActivity(
            DailyPlanProgress dailyProgress,
            DailyPlanRouteSetDefinition[] routeSets,
            LocationActivityDefinition activity,
            StudentLifeProgress studentProgress,
            string requestKey,
            out DailyPlanCompletionResult result)
        {
            result = DailyPlanCompletionResult.Empty;
            if (dailyProgress == null || routeSets == null || activity == null || string.IsNullOrEmpty(dailyProgress.SelectedRouteId)) return false;

            var route = FindRoute(routeSets, dailyProgress.SelectedRouteId);
            if (route == null || !route.Matches(activity)) return false;
            if (!dailyProgress.CompleteRoute(route, activity, requestKey)) return false;

            var logs = route.ApplyOutcomes(studentProgress);
            result = new DailyPlanCompletionResult(true, route, activity, logs);
            return true;
        }

        public DailyPlanRouteDefinition FindRoute(DailyPlanRouteSetDefinition[] routeSets, string routeId)
        {
            if (routeSets == null || string.IsNullOrEmpty(routeId)) return null;
            for (int i = 0; i < routeSets.Length; i++)
            {
                var set = routeSets[i];
                if (set == null) continue;
                for (int routeIndex = 0; routeIndex < set.Routes.Count; routeIndex++)
                {
                    var route = set.Routes[routeIndex];
                    if (route != null && route.Id == routeId) return route;
                }
            }

            return null;
        }
    }

    public static class DailyPlanProgressPersistence
    {
        private const string DailyPlanFileName = "daily-plan-progress.json";

        public static DailyPlanProgress LoadOrCreate(string saveSlot, string playerId, int currentDay)
        {
            string safeSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            string safePlayer = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            string json = new SaveService(safeSlot).ReadJson(FileNameFor(safePlayer));
            if (string.IsNullOrEmpty(json)) return new DailyPlanProgress(safeSlot, safePlayer, currentDay);

            return DailyPlanProgress.FromSaveData(JsonUtility.FromJson<DailyPlanProgressSaveData>(json), safeSlot, safePlayer, currentDay);
        }

        public static void Save(DailyPlanProgress progress)
        {
            if (progress == null) return;
            string json = JsonUtility.ToJson(progress.ToSaveData(), true);
            new SaveService(progress.SaveSlot).WriteJson(FileNameFor(progress.PlayerId), json);
        }

        public static string FileNameFor(string playerId)
        {
            if (string.IsNullOrEmpty(playerId) || playerId == "player") return DailyPlanFileName;
            return "daily-plan-progress-" + SanitizeFileName(playerId) + ".json";
        }

        private static string SanitizeFileName(string value)
        {
            var chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (!char.IsLetterOrDigit(c) && c != '-' && c != '_') chars[i] = '_';
            }

            return new string(chars);
        }
    }
}