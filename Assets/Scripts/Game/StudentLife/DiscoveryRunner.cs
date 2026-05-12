using System;
using System.Collections.Generic;

namespace Rootborn.Game.StudentLife
{
    public readonly struct DiscoveryResult
    {
        public readonly LifeActivityResultKind Kind;
        public readonly string SaveSlot;
        public readonly string PlayerId;
        public readonly string DiscoveryId;
        public readonly DiscoveryScope Scope;
        public readonly string RequestId;
        public readonly string[] OutcomeLogIds;

        public DiscoveryResult(LifeActivityResultKind kind, string saveSlot, string playerId, string discoveryId, DiscoveryScope scope, string requestId, string[] outcomeLogIds)
        {
            Kind = kind;
            SaveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            PlayerId = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            DiscoveryId = string.IsNullOrEmpty(discoveryId) ? string.Empty : discoveryId;
            Scope = scope;
            RequestId = string.IsNullOrEmpty(requestId) ? string.Empty : requestId;
            OutcomeLogIds = outcomeLogIds ?? Array.Empty<string>();
        }
    }

    public sealed class DiscoveryRunner
    {
        public static string RequestIdFor(DiscoveryDefinition discovery)
        {
            return discovery == null ? string.Empty : "discovery:" + discovery.Scope + ":" + discovery.Id;
        }

        public bool TryDiscover(DiscoveryDefinition discovery, StudentLifeProgress progress, out DiscoveryResult result)
        {
            return TryDiscover(discovery, progress, RequestIdFor(discovery), out result);
        }

        public bool TryDiscover(DiscoveryDefinition discovery, StudentLifeProgress progress, string requestId, out DiscoveryResult result)
        {
            string saveSlot = progress == null ? "default" : progress.SaveSlot;
            string playerId = progress == null ? "player" : progress.PlayerId;
            string discoveryId = discovery == null ? string.Empty : discovery.Id;
            var scope = discovery == null ? DiscoveryScope.Personal : discovery.Scope;
            result = new DiscoveryResult(LifeActivityResultKind.InvalidRequest, saveSlot, playerId, discoveryId, scope, requestId, Array.Empty<string>());
            if (discovery == null || progress == null || string.IsNullOrEmpty(requestId))
            {
                return false;
            }

            if (progress.HasAppliedRequest(requestId))
            {
                result = new DiscoveryResult(LifeActivityResultKind.DuplicateRequest, progress.SaveSlot, progress.PlayerId, discovery.Id, discovery.Scope, requestId, Array.Empty<string>());
                return false;
            }

            if (!discovery.HasSatisfiedRequirements(progress))
            {
                result = new DiscoveryResult(LifeActivityResultKind.RequirementFailed, progress.SaveSlot, progress.PlayerId, discovery.Id, discovery.Scope, requestId, Array.Empty<string>());
                return false;
            }

            string[] outcomeLogs = discovery.ApplyOutcomes(progress);
            progress.MarkRequestApplied(requestId);
            progress.RecordActivityCompleted(discovery.Id, outcomeLogs);
            result = new DiscoveryResult(LifeActivityResultKind.Applied, progress.SaveSlot, progress.PlayerId, discovery.Id, discovery.Scope, requestId, outcomeLogs);
            return true;
        }
    }

    public readonly struct DiscoveryDayLog
    {
        public readonly string[] DiscoveryIds;
        public readonly string[] ResultLogIds;

        public DiscoveryDayLog(string[] discoveryIds, string[] resultLogIds)
        {
            DiscoveryIds = discoveryIds ?? Array.Empty<string>();
            ResultLogIds = resultLogIds ?? Array.Empty<string>();
        }

        public static DiscoveryDayLog FromResultLogs(string[] resultLogIds)
        {
            var discoveryIds = new List<string>();
            var filteredLogs = new List<string>();
            if (resultLogIds != null)
            {
                for (int i = 0; i < resultLogIds.Length; i++)
                {
                    if (DiscoveryLogCodec.TryParseDelta(resultLogIds[i], out var entry))
                    {
                        AddUnique(discoveryIds, entry.DiscoveryId);
                        filteredLogs.Add(resultLogIds[i]);
                    }
                }
            }

            return new DiscoveryDayLog(discoveryIds.ToArray(), filteredLogs.ToArray());
        }

        private static void AddUnique(List<string> values, string value)
        {
            if (!string.IsNullOrEmpty(value) && !values.Contains(value))
            {
                values.Add(value);
            }
        }
    }

    public readonly struct DiscoveryGrowthSummaryEntry
    {
        public readonly string DiscoveryId;
        public readonly string TargetId;
        public readonly int Delta;

        public DiscoveryGrowthSummaryEntry(string discoveryId, string targetId, int delta)
        {
            DiscoveryId = string.IsNullOrEmpty(discoveryId) ? string.Empty : discoveryId;
            TargetId = string.IsNullOrEmpty(targetId) ? string.Empty : targetId;
            Delta = delta;
        }
    }

    public readonly struct DiscoveryGrowthSummary
    {
        public readonly DiscoveryGrowthSummaryEntry[] Entries;

        public DiscoveryGrowthSummary(DiscoveryGrowthSummaryEntry[] entries)
        {
            Entries = entries ?? Array.Empty<DiscoveryGrowthSummaryEntry>();
        }

        public static DiscoveryGrowthSummary FromResultLogs(string[] resultLogIds)
        {
            var entries = new List<DiscoveryGrowthSummaryEntry>();
            if (resultLogIds != null)
            {
                for (int i = 0; i < resultLogIds.Length; i++)
                {
                    if (DiscoveryLogCodec.TryParseDelta(resultLogIds[i], out var entry))
                    {
                        entries.Add(entry);
                    }
                }
            }

            return new DiscoveryGrowthSummary(entries.ToArray());
        }
    }

    public static class DiscoveryLogCodec
    {
        private const string Prefix = "discovery:";

        public static string EncodeDelta(string discoveryId, string targetId, int delta)
        {
            if (string.IsNullOrEmpty(discoveryId) || string.IsNullOrEmpty(targetId) || delta == 0)
            {
                return string.Empty;
            }

            string sign = delta > 0 ? "+" : string.Empty;
            return Prefix + discoveryId + ":" + sign + targetId + "=" + delta.ToString();
        }

        public static bool TryParseDelta(string value, out DiscoveryGrowthSummaryEntry entry)
        {
            entry = default;
            if (string.IsNullOrEmpty(value) || !value.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return false;
            }

            string payload = value.Substring(Prefix.Length);
            int discoveryEnd = payload.IndexOf(':');
            int equals = payload.LastIndexOf('=');
            if (discoveryEnd <= 0 || equals <= discoveryEnd + 1 || equals + 1 >= payload.Length)
            {
                return false;
            }

            string discovery = payload.Substring(0, discoveryEnd);
            string target = payload.Substring(discoveryEnd + 1, equals - discoveryEnd - 1);
            if (target.StartsWith("+", StringComparison.Ordinal))
            {
                target = target.Substring(1);
            }

            if (!int.TryParse(payload.Substring(equals + 1), out int delta))
            {
                return false;
            }

            entry = new DiscoveryGrowthSummaryEntry(discovery, target, delta);
            return true;
        }
    }
}
