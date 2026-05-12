using System;
using System.Collections.Generic;

namespace Rootborn.Game.StudentLife
{
    public readonly struct OutsideSchoolActivityResult
    {
        public readonly LifeActivityResultKind Kind;
        public readonly string SaveSlot;
        public readonly string PlayerId;
        public readonly string ActivityId;
        public readonly string CategoryId;
        public readonly string RequestId;
        public readonly string[] OutcomeLogIds;

        public OutsideSchoolActivityResult(LifeActivityResultKind kind, string saveSlot, string playerId, string activityId, string categoryId, string requestId, string[] outcomeLogIds)
        {
            Kind = kind;
            SaveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            PlayerId = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            ActivityId = string.IsNullOrEmpty(activityId) ? string.Empty : activityId;
            CategoryId = string.IsNullOrEmpty(categoryId) ? string.Empty : categoryId;
            RequestId = string.IsNullOrEmpty(requestId) ? string.Empty : requestId;
            OutcomeLogIds = outcomeLogIds ?? Array.Empty<string>();
        }
    }

    public sealed class OutsideSchoolActivityRunner
    {
        public bool TryPerform(OutsideSchoolActivityDefinition activity, StudentLifeProgress progress, string requestId, out OutsideSchoolActivityResult result)
        {
            string saveSlot = progress == null ? "default" : progress.SaveSlot;
            string playerId = progress == null ? "player" : progress.PlayerId;
            string activityId = activity == null ? string.Empty : activity.Id;
            string categoryId = activity == null ? string.Empty : activity.CategoryId;
            result = new OutsideSchoolActivityResult(LifeActivityResultKind.InvalidRequest, saveSlot, playerId, activityId, categoryId, requestId, Array.Empty<string>());
            if (activity == null || progress == null || string.IsNullOrEmpty(requestId))
            {
                return false;
            }

            if (progress.HasAppliedRequest(requestId))
            {
                result = new OutsideSchoolActivityResult(LifeActivityResultKind.DuplicateRequest, progress.SaveSlot, progress.PlayerId, activity.Id, activity.CategoryId, requestId, Array.Empty<string>());
                return false;
            }

            if (!progress.CanSpend(activity.EnergyCost, activity.FocusCost))
            {
                result = new OutsideSchoolActivityResult(LifeActivityResultKind.InsufficientResources, progress.SaveSlot, progress.PlayerId, activity.Id, activity.CategoryId, requestId, Array.Empty<string>());
                return false;
            }

            if (!activity.HasSatisfiedRequirements(progress))
            {
                result = new OutsideSchoolActivityResult(LifeActivityResultKind.RequirementFailed, progress.SaveSlot, progress.PlayerId, activity.Id, activity.CategoryId, requestId, Array.Empty<string>());
                return false;
            }

            progress.Spend(activity.TimeCostMinutes, activity.EnergyCost, activity.FocusCost, activity.StressDelta);
            string[] outcomeLogs = activity.ApplyOutcomes(progress);
            progress.MarkRequestApplied(requestId);
            progress.RecordActivityCompleted(activity.Id, outcomeLogs);
            result = new OutsideSchoolActivityResult(LifeActivityResultKind.Applied, progress.SaveSlot, progress.PlayerId, activity.Id, activity.CategoryId, requestId, outcomeLogs);
            return true;
        }
    }

    public readonly struct OutsideSchoolDayLog
    {
        public readonly string[] ActivityIds;
        public readonly string[] ResultLogIds;
        public readonly string[] RequestIds;

        public OutsideSchoolDayLog(string[] activityIds, string[] resultLogIds, string[] requestIds)
        {
            ActivityIds = activityIds ?? Array.Empty<string>();
            ResultLogIds = resultLogIds ?? Array.Empty<string>();
            RequestIds = requestIds ?? Array.Empty<string>();
        }

        public static OutsideSchoolDayLog FromResultLogs(string[] resultLogIds)
        {
            var activityIds = new List<string>();
            var filteredLogs = new List<string>();
            if (resultLogIds != null)
            {
                for (int i = 0; i < resultLogIds.Length; i++)
                {
                    if (OutsideSchoolLogCodec.TryParseDelta(resultLogIds[i], out var entry))
                    {
                        AddUnique(activityIds, entry.ActivityId);
                        filteredLogs.Add(resultLogIds[i]);
                    }
                }
            }

            return new OutsideSchoolDayLog(activityIds.ToArray(), filteredLogs.ToArray(), Array.Empty<string>());
        }

        private static void AddUnique(List<string> values, string value)
        {
            if (!string.IsNullOrEmpty(value) && !values.Contains(value))
            {
                values.Add(value);
            }
        }
    }

    public readonly struct OutsideSchoolGrowthSummaryEntry
    {
        public readonly string ActivityId;
        public readonly string TargetId;
        public readonly int Delta;

        public OutsideSchoolGrowthSummaryEntry(string activityId, string targetId, int delta)
        {
            ActivityId = string.IsNullOrEmpty(activityId) ? string.Empty : activityId;
            TargetId = string.IsNullOrEmpty(targetId) ? string.Empty : targetId;
            Delta = delta;
        }
    }

    public readonly struct OutsideSchoolGrowthSummary
    {
        public readonly OutsideSchoolGrowthSummaryEntry[] Entries;

        public OutsideSchoolGrowthSummary(OutsideSchoolGrowthSummaryEntry[] entries)
        {
            Entries = entries ?? Array.Empty<OutsideSchoolGrowthSummaryEntry>();
        }

        public static OutsideSchoolGrowthSummary FromDayLog(OutsideSchoolDayLog log)
        {
            return FromResultLogs(log.ResultLogIds);
        }

        public static OutsideSchoolGrowthSummary FromResultLogs(string[] resultLogIds)
        {
            var entries = new List<OutsideSchoolGrowthSummaryEntry>();
            if (resultLogIds != null)
            {
                for (int i = 0; i < resultLogIds.Length; i++)
                {
                    if (OutsideSchoolLogCodec.TryParseDelta(resultLogIds[i], out var entry))
                    {
                        entries.Add(entry);
                    }
                }
            }

            return new OutsideSchoolGrowthSummary(entries.ToArray());
        }
    }

    public static class OutsideSchoolLogCodec
    {
        private const string Prefix = "outside-school:";

        public static string EncodeDelta(string activityId, string targetId, int delta)
        {
            if (string.IsNullOrEmpty(activityId) || string.IsNullOrEmpty(targetId) || delta == 0)
            {
                return string.Empty;
            }

            string sign = delta > 0 ? "+" : string.Empty;
            return Prefix + activityId + ":" + sign + targetId + "=" + delta.ToString();
        }

        public static bool TryParseDelta(string value, out OutsideSchoolGrowthSummaryEntry entry)
        {
            entry = default;
            if (string.IsNullOrEmpty(value) || !value.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return false;
            }

            string payload = value.Substring(Prefix.Length);
            int activityEnd = payload.IndexOf(':');
            int equals = payload.LastIndexOf('=');
            if (activityEnd <= 0 || equals <= activityEnd + 1 || equals + 1 >= payload.Length)
            {
                return false;
            }

            string activityId = payload.Substring(0, activityEnd);
            string target = payload.Substring(activityEnd + 1, equals - activityEnd - 1);
            if (target.StartsWith("+", StringComparison.Ordinal))
            {
                target = target.Substring(1);
            }

            if (!int.TryParse(payload.Substring(equals + 1), out int delta))
            {
                return false;
            }

            entry = new OutsideSchoolGrowthSummaryEntry(activityId, target, delta);
            return true;
        }
    }
}
