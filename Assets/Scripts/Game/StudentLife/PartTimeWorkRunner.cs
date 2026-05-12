using System;
using System.Collections.Generic;
using Rootborn.Game.Common;
using Rootborn.Game.Quests;

namespace Rootborn.Game.StudentLife
{
    public readonly struct PartTimeWorkResult
    {
        public readonly LifeActivityResultKind Kind;
        public readonly string SaveSlot;
        public readonly string PlayerId;
        public readonly string WorkId;
        public readonly string WorkplaceId;
        public readonly string LocationId;
        public readonly string NpcId;
        public readonly string RequestId;
        public readonly string[] OutcomeLogIds;

        public PartTimeWorkResult(LifeActivityResultKind kind, string saveSlot, string playerId, string workId, string workplaceId, string locationId, string npcId, string requestId, string[] outcomeLogIds)
        {
            Kind = kind;
            SaveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            PlayerId = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            WorkId = string.IsNullOrEmpty(workId) ? string.Empty : workId;
            WorkplaceId = string.IsNullOrEmpty(workplaceId) ? string.Empty : workplaceId;
            LocationId = string.IsNullOrEmpty(locationId) ? string.Empty : locationId;
            NpcId = string.IsNullOrEmpty(npcId) ? string.Empty : npcId;
            RequestId = string.IsNullOrEmpty(requestId) ? string.Empty : requestId;
            OutcomeLogIds = outcomeLogIds ?? Array.Empty<string>();
        }
    }

    public sealed class PartTimeWorkRunner
    {
        public bool TryPerform(PartTimeWorkDefinition work, StudentLifeProgress progress, Inventory inventory, string requestId, out PartTimeWorkResult result)
        {
            string saveSlot = progress == null ? "default" : progress.SaveSlot;
            string playerId = progress == null ? "player" : progress.PlayerId;
            string workId = work == null ? string.Empty : work.Id;
            string workplaceId = work == null ? string.Empty : work.WorkplaceId;
            string locationId = work != null && work.Workplace != null ? work.Workplace.LocationId : string.Empty;
            string npcId = work != null && work.Workplace != null ? work.Workplace.NpcId : string.Empty;
            result = new PartTimeWorkResult(LifeActivityResultKind.InvalidRequest, saveSlot, playerId, workId, workplaceId, locationId, npcId, requestId, Array.Empty<string>());
            if (work == null || progress == null || inventory == null || string.IsNullOrEmpty(requestId))
            {
                return false;
            }

            if (progress.HasAppliedRequest(requestId))
            {
                result = new PartTimeWorkResult(LifeActivityResultKind.DuplicateRequest, progress.SaveSlot, progress.PlayerId, work.Id, work.WorkplaceId, locationId, npcId, requestId, Array.Empty<string>());
                return false;
            }

            if (!progress.CanSpend(work.EnergyCost, work.FocusCost) || !inventory.CanAddAll(work.Rewards))
            {
                result = new PartTimeWorkResult(LifeActivityResultKind.InsufficientResources, progress.SaveSlot, progress.PlayerId, work.Id, work.WorkplaceId, locationId, npcId, requestId, Array.Empty<string>());
                return false;
            }

            if (!work.HasSatisfiedRequirements(progress))
            {
                result = new PartTimeWorkResult(LifeActivityResultKind.RequirementFailed, progress.SaveSlot, progress.PlayerId, work.Id, work.WorkplaceId, locationId, npcId, requestId, Array.Empty<string>());
                return false;
            }

            progress.Spend(work.TimeCostMinutes, work.EnergyCost, work.FocusCost, work.StressDelta);
            var logs = new List<string>();
            AddRewardLogsAndApply(work, inventory, logs);
            logs.AddRange(work.ApplyOutcomes(progress));
            string[] outcomeLogs = logs.ToArray();
            progress.MarkRequestApplied(requestId);
            progress.RecordActivityCompleted(work.Id, outcomeLogs);
            result = new PartTimeWorkResult(LifeActivityResultKind.Applied, progress.SaveSlot, progress.PlayerId, work.Id, work.WorkplaceId, locationId, npcId, requestId, outcomeLogs);
            return true;
        }

        private static void AddRewardLogsAndApply(PartTimeWorkDefinition work, Inventory inventory, List<string> logs)
        {
            var rewards = work.Rewards;
            for (int i = 0; i < rewards.Count; i++)
            {
                InventoryGrant reward = rewards[i];
                if (reward.Item == null || reward.Count <= 0)
                {
                    continue;
                }

                inventory.Add(reward.Item, reward.Count);
                string itemId = string.IsNullOrEmpty(reward.Item.Id) ? reward.Item.name : reward.Item.Id;
                string log = PartTimeWorkLogCodec.EncodeDelta(work.Id, itemId, reward.Count);
                if (!string.IsNullOrEmpty(log))
                {
                    logs.Add(log);
                }
            }
        }
    }

    public readonly struct PartTimeWorkDayLog
    {
        public readonly string[] WorkIds;
        public readonly string[] ResultLogIds;

        public PartTimeWorkDayLog(string[] workIds, string[] resultLogIds)
        {
            WorkIds = workIds ?? Array.Empty<string>();
            ResultLogIds = resultLogIds ?? Array.Empty<string>();
        }

        public static PartTimeWorkDayLog FromResultLogs(string[] resultLogIds)
        {
            var workIds = new List<string>();
            var filteredLogs = new List<string>();
            if (resultLogIds != null)
            {
                for (int i = 0; i < resultLogIds.Length; i++)
                {
                    if (PartTimeWorkLogCodec.TryParseDelta(resultLogIds[i], out var entry))
                    {
                        AddUnique(workIds, entry.WorkId);
                        filteredLogs.Add(resultLogIds[i]);
                    }
                }
            }

            return new PartTimeWorkDayLog(workIds.ToArray(), filteredLogs.ToArray());
        }

        private static void AddUnique(List<string> values, string value)
        {
            if (!string.IsNullOrEmpty(value) && !values.Contains(value))
            {
                values.Add(value);
            }
        }
    }

    public readonly struct PartTimeWorkGrowthSummaryEntry
    {
        public readonly string WorkId;
        public readonly string TargetId;
        public readonly int Delta;

        public PartTimeWorkGrowthSummaryEntry(string workId, string targetId, int delta)
        {
            WorkId = string.IsNullOrEmpty(workId) ? string.Empty : workId;
            TargetId = string.IsNullOrEmpty(targetId) ? string.Empty : targetId;
            Delta = delta;
        }
    }

    public readonly struct PartTimeWorkGrowthSummary
    {
        public readonly PartTimeWorkGrowthSummaryEntry[] Entries;

        public PartTimeWorkGrowthSummary(PartTimeWorkGrowthSummaryEntry[] entries)
        {
            Entries = entries ?? Array.Empty<PartTimeWorkGrowthSummaryEntry>();
        }

        public static PartTimeWorkGrowthSummary FromResultLogs(string[] resultLogIds)
        {
            var entries = new List<PartTimeWorkGrowthSummaryEntry>();
            if (resultLogIds != null)
            {
                for (int i = 0; i < resultLogIds.Length; i++)
                {
                    if (PartTimeWorkLogCodec.TryParseDelta(resultLogIds[i], out var entry))
                    {
                        entries.Add(entry);
                    }
                }
            }

            return new PartTimeWorkGrowthSummary(entries.ToArray());
        }
    }

    public static class PartTimeWorkLogCodec
    {
        private const string Prefix = "part-time-work:";

        public static string EncodeDelta(string workId, string targetId, int delta)
        {
            if (string.IsNullOrEmpty(workId) || string.IsNullOrEmpty(targetId) || delta == 0)
            {
                return string.Empty;
            }

            string sign = delta > 0 ? "+" : string.Empty;
            return Prefix + workId + ":" + sign + targetId + "=" + delta.ToString();
        }

        public static bool TryParseDelta(string value, out PartTimeWorkGrowthSummaryEntry entry)
        {
            entry = default;
            if (string.IsNullOrEmpty(value) || !value.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return false;
            }

            string payload = value.Substring(Prefix.Length);
            int workEnd = payload.IndexOf(':');
            int equals = payload.LastIndexOf('=');
            if (workEnd <= 0 || equals <= workEnd + 1 || equals + 1 >= payload.Length)
            {
                return false;
            }

            string workId = payload.Substring(0, workEnd);
            string target = payload.Substring(workEnd + 1, equals - workEnd - 1);
            if (target.StartsWith("+", StringComparison.Ordinal))
            {
                target = target.Substring(1);
            }

            if (!int.TryParse(payload.Substring(equals + 1), out int delta))
            {
                return false;
            }

            entry = new PartTimeWorkGrowthSummaryEntry(workId, target, delta);
            return true;
        }
    }
}
