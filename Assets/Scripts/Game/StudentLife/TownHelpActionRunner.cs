using System;
using System.Collections.Generic;

namespace Rootborn.Game.StudentLife
{
    public readonly struct TownHelpActionResult
    {
        public readonly LifeActivityResultKind Kind;
        public readonly string SaveSlot;
        public readonly string PlayerId;
        public readonly string ActionId;
        public readonly string NpcId;
        public readonly string LocationId;
        public readonly string RequestId;
        public readonly string[] OutcomeLogIds;

        public TownHelpActionResult(LifeActivityResultKind kind, string saveSlot, string playerId, string actionId, string npcId, string locationId, string requestId, string[] outcomeLogIds)
        {
            Kind = kind;
            SaveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            PlayerId = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            ActionId = string.IsNullOrEmpty(actionId) ? string.Empty : actionId;
            NpcId = string.IsNullOrEmpty(npcId) ? string.Empty : npcId;
            LocationId = string.IsNullOrEmpty(locationId) ? string.Empty : locationId;
            RequestId = string.IsNullOrEmpty(requestId) ? string.Empty : requestId;
            OutcomeLogIds = outcomeLogIds ?? Array.Empty<string>();
        }
    }

    public sealed class TownHelpActionRunner
    {
        public bool TryPerform(TownHelpActionDefinition action, StudentLifeProgress progress, string requestId, out TownHelpActionResult result)
        {
            string saveSlot = progress == null ? "default" : progress.SaveSlot;
            string playerId = progress == null ? "player" : progress.PlayerId;
            string actionId = action == null ? string.Empty : action.Id;
            string npcId = action == null ? string.Empty : action.NpcId;
            string locationId = action == null ? string.Empty : action.LocationId;
            result = new TownHelpActionResult(LifeActivityResultKind.InvalidRequest, saveSlot, playerId, actionId, npcId, locationId, requestId, Array.Empty<string>());
            if (action == null || progress == null || string.IsNullOrEmpty(requestId))
            {
                return false;
            }

            if (progress.HasAppliedRequest(requestId))
            {
                result = new TownHelpActionResult(LifeActivityResultKind.DuplicateRequest, progress.SaveSlot, progress.PlayerId, action.Id, action.NpcId, action.LocationId, requestId, Array.Empty<string>());
                return false;
            }

            if (!progress.CanSpend(action.EnergyCost, action.FocusCost))
            {
                result = new TownHelpActionResult(LifeActivityResultKind.InsufficientResources, progress.SaveSlot, progress.PlayerId, action.Id, action.NpcId, action.LocationId, requestId, Array.Empty<string>());
                return false;
            }

            if (!action.HasSatisfiedRequirements(progress))
            {
                result = new TownHelpActionResult(LifeActivityResultKind.RequirementFailed, progress.SaveSlot, progress.PlayerId, action.Id, action.NpcId, action.LocationId, requestId, Array.Empty<string>());
                return false;
            }

            progress.Spend(action.TimeCostMinutes, action.EnergyCost, action.FocusCost, action.StressDelta);
            string[] outcomeLogs = action.ApplyOutcomes(progress);
            progress.MarkRequestApplied(requestId);
            progress.RecordActivityCompleted(action.Id, outcomeLogs);
            result = new TownHelpActionResult(LifeActivityResultKind.Applied, progress.SaveSlot, progress.PlayerId, action.Id, action.NpcId, action.LocationId, requestId, outcomeLogs);
            return true;
        }
    }

    public readonly struct TownHelpActionDayLog
    {
        public readonly string[] ActionIds;
        public readonly string[] ResultLogIds;

        public TownHelpActionDayLog(string[] actionIds, string[] resultLogIds)
        {
            ActionIds = actionIds ?? Array.Empty<string>();
            ResultLogIds = resultLogIds ?? Array.Empty<string>();
        }

        public static TownHelpActionDayLog FromResultLogs(string[] resultLogIds)
        {
            var actionIds = new List<string>();
            var filteredLogs = new List<string>();
            if (resultLogIds != null)
            {
                for (int i = 0; i < resultLogIds.Length; i++)
                {
                    if (TownHelpActionLogCodec.TryParseDelta(resultLogIds[i], out var entry))
                    {
                        AddUnique(actionIds, entry.ActionId);
                        filteredLogs.Add(resultLogIds[i]);
                    }
                }
            }

            return new TownHelpActionDayLog(actionIds.ToArray(), filteredLogs.ToArray());
        }

        private static void AddUnique(List<string> values, string value)
        {
            if (!string.IsNullOrEmpty(value) && !values.Contains(value))
            {
                values.Add(value);
            }
        }
    }

    public readonly struct TownHelpActionGrowthSummaryEntry
    {
        public readonly string ActionId;
        public readonly string TargetId;
        public readonly int Delta;

        public TownHelpActionGrowthSummaryEntry(string actionId, string targetId, int delta)
        {
            ActionId = string.IsNullOrEmpty(actionId) ? string.Empty : actionId;
            TargetId = string.IsNullOrEmpty(targetId) ? string.Empty : targetId;
            Delta = delta;
        }
    }

    public readonly struct TownHelpActionGrowthSummary
    {
        public readonly TownHelpActionGrowthSummaryEntry[] Entries;

        public TownHelpActionGrowthSummary(TownHelpActionGrowthSummaryEntry[] entries)
        {
            Entries = entries ?? Array.Empty<TownHelpActionGrowthSummaryEntry>();
        }

        public static TownHelpActionGrowthSummary FromResultLogs(string[] resultLogIds)
        {
            var entries = new List<TownHelpActionGrowthSummaryEntry>();
            if (resultLogIds != null)
            {
                for (int i = 0; i < resultLogIds.Length; i++)
                {
                    if (TownHelpActionLogCodec.TryParseDelta(resultLogIds[i], out var entry))
                    {
                        entries.Add(entry);
                    }
                }
            }

            return new TownHelpActionGrowthSummary(entries.ToArray());
        }
    }

    public static class TownHelpActionLogCodec
    {
        private const string Prefix = "town-help:";

        public static string EncodeDelta(string actionId, string targetId, int delta)
        {
            if (string.IsNullOrEmpty(actionId) || string.IsNullOrEmpty(targetId) || delta == 0)
            {
                return string.Empty;
            }

            string sign = delta > 0 ? "+" : string.Empty;
            return Prefix + actionId + ":" + sign + targetId + "=" + delta.ToString();
        }

        public static bool TryParseDelta(string value, out TownHelpActionGrowthSummaryEntry entry)
        {
            entry = default;
            if (string.IsNullOrEmpty(value) || !value.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return false;
            }

            string payload = value.Substring(Prefix.Length);
            int actionEnd = payload.IndexOf(':');
            int equals = payload.LastIndexOf('=');
            if (actionEnd <= 0 || equals <= actionEnd + 1 || equals + 1 >= payload.Length)
            {
                return false;
            }

            string actionId = payload.Substring(0, actionEnd);
            string target = payload.Substring(actionEnd + 1, equals - actionEnd - 1);
            if (target.StartsWith("+", StringComparison.Ordinal))
            {
                target = target.Substring(1);
            }

            if (!int.TryParse(payload.Substring(equals + 1), out int delta))
            {
                return false;
            }

            entry = new TownHelpActionGrowthSummaryEntry(actionId, target, delta);
            return true;
        }
    }
}
