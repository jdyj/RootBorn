using System;
using System.Collections.Generic;

namespace Rootborn.Game.StudentLife
{
    public readonly struct LocationActivityResult
    {
        public readonly LifeActivityResultKind Kind;
        public readonly string SaveSlot;
        public readonly string PlayerId;
        public readonly string ActivityId;
        public readonly string LocationId;
        public readonly LocationGrowthRoute GrowthRoute;
        public readonly string RequestId;
        public readonly string[] OutcomeLogIds;

        public LocationActivityResult(LifeActivityResultKind kind, string saveSlot, string playerId, string activityId, string locationId, LocationGrowthRoute growthRoute, string requestId, string[] outcomeLogIds)
        {
            Kind = kind;
            SaveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            PlayerId = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            ActivityId = string.IsNullOrEmpty(activityId) ? string.Empty : activityId;
            LocationId = string.IsNullOrEmpty(locationId) ? string.Empty : locationId;
            GrowthRoute = growthRoute;
            RequestId = string.IsNullOrEmpty(requestId) ? string.Empty : requestId;
            OutcomeLogIds = outcomeLogIds ?? Array.Empty<string>();
        }
    }

    public sealed class LocationActivityRunner
    {
        public bool TryPerform(LocationActivityDefinition activity, StudentLifeProgress progress, string requestId, out LocationActivityResult result)
        {
            string saveSlot = progress == null ? "default" : progress.SaveSlot;
            string playerId = progress == null ? "player" : progress.PlayerId;
            string activityId = activity == null ? string.Empty : activity.Id;
            string locationId = activity == null ? string.Empty : activity.LocationId;
            LocationGrowthRoute growthRoute = activity == null ? LocationGrowthRoute.Exploration : activity.GrowthRoute;
            result = new LocationActivityResult(LifeActivityResultKind.InvalidRequest, saveSlot, playerId, activityId, locationId, growthRoute, requestId, Array.Empty<string>());
            if (activity == null || progress == null || string.IsNullOrEmpty(requestId)) return false;

            if (progress.HasAppliedRequest(requestId))
            {
                result = new LocationActivityResult(LifeActivityResultKind.DuplicateRequest, progress.SaveSlot, progress.PlayerId, activity.Id, activity.LocationId, activity.GrowthRoute, requestId, Array.Empty<string>());
                return false;
            }

            if (!progress.CanSpend(activity.EnergyCost, activity.FocusCost))
            {
                result = new LocationActivityResult(LifeActivityResultKind.InsufficientResources, progress.SaveSlot, progress.PlayerId, activity.Id, activity.LocationId, activity.GrowthRoute, requestId, Array.Empty<string>());
                return false;
            }

            if (!activity.HasSatisfiedRequirements(progress))
            {
                result = new LocationActivityResult(LifeActivityResultKind.RequirementFailed, progress.SaveSlot, progress.PlayerId, activity.Id, activity.LocationId, activity.GrowthRoute, requestId, Array.Empty<string>());
                return false;
            }

            progress.Spend(activity.TimeCostMinutes, activity.EnergyCost, activity.FocusCost, activity.StressDelta);
            string[] outcomeLogs = activity.ApplyOutcomes(progress);
            progress.MarkRequestApplied(requestId);
            progress.RecordActivityCompleted(activity.Id, outcomeLogs);
            result = new LocationActivityResult(LifeActivityResultKind.Applied, progress.SaveSlot, progress.PlayerId, activity.Id, activity.LocationId, activity.GrowthRoute, requestId, outcomeLogs);
            return true;
        }
    }

    public readonly struct LocationActivityGrowthSummaryEntry
    {
        public readonly string ActivityId;
        public readonly string TargetId;
        public readonly int Delta;
        public readonly bool IsUnlock;

        public LocationActivityGrowthSummaryEntry(string activityId, string targetId, int delta, bool isUnlock)
        {
            ActivityId = string.IsNullOrEmpty(activityId) ? string.Empty : activityId;
            TargetId = string.IsNullOrEmpty(targetId) ? string.Empty : targetId;
            Delta = delta;
            IsUnlock = isUnlock;
        }
    }

    public readonly struct LocationActivityGrowthSummary
    {
        public readonly LocationActivityGrowthSummaryEntry[] Entries;

        public LocationActivityGrowthSummary(LocationActivityGrowthSummaryEntry[] entries)
        {
            Entries = entries ?? Array.Empty<LocationActivityGrowthSummaryEntry>();
        }

        public static LocationActivityGrowthSummary FromResultLogs(string[] resultLogIds)
        {
            var entries = new List<LocationActivityGrowthSummaryEntry>();
            if (resultLogIds != null)
            {
                for (int i = 0; i < resultLogIds.Length; i++)
                {
                    if (LocationActivityLogCodec.TryParse(resultLogIds[i], out var entry)) entries.Add(entry);
                }
            }

            return new LocationActivityGrowthSummary(entries.ToArray());
        }
    }

    public static class LocationActivityLogCodec
    {
        private const string Prefix = "location-activity:";
        private const string UnlockMarker = ":unlock=";

        public static string EncodeDelta(string activityId, string targetId, int delta)
        {
            if (string.IsNullOrEmpty(activityId) || string.IsNullOrEmpty(targetId) || delta == 0) return string.Empty;
            string sign = delta > 0 ? "+" : string.Empty;
            return Prefix + activityId + ":" + sign + targetId + "=" + delta.ToString();
        }

        public static string EncodeUnlock(string activityId, string targetId)
        {
            if (string.IsNullOrEmpty(activityId) || string.IsNullOrEmpty(targetId)) return string.Empty;
            return Prefix + activityId + UnlockMarker + targetId;
        }

        public static bool TryParse(string value, out LocationActivityGrowthSummaryEntry entry)
        {
            entry = default;
            if (string.IsNullOrEmpty(value) || !value.StartsWith(Prefix, StringComparison.Ordinal)) return false;
            string payload = value.Substring(Prefix.Length);
            int unlockIndex = payload.IndexOf(UnlockMarker, StringComparison.Ordinal);
            if (unlockIndex > 0)
            {
                entry = new LocationActivityGrowthSummaryEntry(payload.Substring(0, unlockIndex), payload.Substring(unlockIndex + UnlockMarker.Length), 1, true);
                return true;
            }

            int actionEnd = payload.IndexOf(':');
            int equals = payload.LastIndexOf('=');
            if (actionEnd <= 0 || equals <= actionEnd + 1 || equals + 1 >= payload.Length) return false;
            string activityId = payload.Substring(0, actionEnd);
            string target = payload.Substring(actionEnd + 1, equals - actionEnd - 1);
            if (target.StartsWith("+", StringComparison.Ordinal)) target = target.Substring(1);
            if (!int.TryParse(payload.Substring(equals + 1), out int delta)) return false;
            entry = new LocationActivityGrowthSummaryEntry(activityId, target, delta, false);
            return true;
        }
    }
}
