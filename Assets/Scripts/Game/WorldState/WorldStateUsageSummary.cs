using System;
using System.Collections.Generic;

namespace Rootborn.Game.WorldState
{
    public readonly struct WorldStateUsageSummaryModel
    {
        public readonly string UsageId;
        public readonly string SourceWorldStateFlagId;
        public readonly string DisplayName;
        public readonly string Description;
        public readonly string InteractionPrompt;
        public readonly string NextAction;
        public readonly bool IsAvailable;
        public readonly bool IsUsed;
        public readonly bool IsRepeatable;
        public readonly string RepeatState;
        public readonly string[] RewardSummaryIds;

        public WorldStateUsageSummaryModel(string usageId, string sourceWorldStateFlagId, string displayName, string description, string interactionPrompt, string nextAction, bool isAvailable, bool isUsed, bool isRepeatable, string repeatState, string[] rewardSummaryIds)
        {
            UsageId = string.IsNullOrEmpty(usageId) ? string.Empty : usageId;
            SourceWorldStateFlagId = string.IsNullOrEmpty(sourceWorldStateFlagId) ? string.Empty : sourceWorldStateFlagId;
            DisplayName = string.IsNullOrEmpty(displayName) ? UsageId : displayName;
            Description = string.IsNullOrEmpty(description) ? DisplayName : description;
            InteractionPrompt = string.IsNullOrEmpty(interactionPrompt) ? DisplayName : interactionPrompt;
            NextAction = string.IsNullOrEmpty(nextAction) ? InteractionPrompt : nextAction;
            IsAvailable = isAvailable;
            IsUsed = isUsed;
            IsRepeatable = isRepeatable;
            RepeatState = string.IsNullOrEmpty(repeatState) ? string.Empty : repeatState;
            RewardSummaryIds = rewardSummaryIds ?? Array.Empty<string>();
        }
    }

    public sealed class WorldStateUsageLookupCache
    {
        private readonly WorldStateUsageDefinition[] _usages;
        private readonly Dictionary<string, WorldStateUsageDefinition> _byId = new Dictionary<string, WorldStateUsageDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<WorldStateUsageDefinition>> _byFlag = new Dictionary<string, List<WorldStateUsageDefinition>>(StringComparer.Ordinal);

        public WorldStateUsageLookupCache(IReadOnlyList<WorldStateUsageDefinition> usages)
        {
            BuildCount = 1;
            if (usages == null)
            {
                _usages = Array.Empty<WorldStateUsageDefinition>();
                return;
            }

            var values = new List<WorldStateUsageDefinition>();
            for (int i = 0; i < usages.Count; i++)
            {
                var usage = usages[i];
                if (usage == null || string.IsNullOrEmpty(usage.Id)) continue;
                values.Add(usage);
                _byId[usage.Id] = usage;
                string flagId = usage.SourceFlagId;
                if (!string.IsNullOrEmpty(flagId))
                {
                    if (!_byFlag.TryGetValue(flagId, out var list))
                    {
                        list = new List<WorldStateUsageDefinition>();
                        _byFlag[flagId] = list;
                    }

                    list.Add(usage);
                }
            }

            values.Sort((a, b) => a.SortPriority.CompareTo(b.SortPriority));
            _usages = values.ToArray();
        }

        public int BuildCount { get; }
        public int LookupCount { get; private set; }

        public bool TryGetById(string usageId, out WorldStateUsageDefinition usage)
        {
            LookupCount++;
            return _byId.TryGetValue(string.IsNullOrEmpty(usageId) ? string.Empty : usageId, out usage);
        }

        public WorldStateUsageDefinition[] GetByFlag(string flagId)
        {
            LookupCount++;
            return !string.IsNullOrEmpty(flagId) && _byFlag.TryGetValue(flagId, out var list) ? list.ToArray() : Array.Empty<WorldStateUsageDefinition>();
        }

        public WorldStateUsageDefinition[] GetAll()
        {
            LookupCount++;
            return _usages;
        }
    }

    public static class WorldStateUsageSummaryBuilder
    {
        public static WorldStateUsageSummaryModel[] BuildForSurface(WorldStateUsageLookupCache cache, in WorldStateUsageContext context, WorldStateUsageSummarySurface surface)
        {
            if (cache == null) return Array.Empty<WorldStateUsageSummaryModel>();
            return Build(cache.GetAll(), context, surface);
        }

        public static WorldStateUsageSummaryModel[] BuildForFlag(WorldStateUsageLookupCache cache, string flagId, in WorldStateUsageContext context, WorldStateUsageSummarySurface surface)
        {
            if (cache == null) return Array.Empty<WorldStateUsageSummaryModel>();
            return Build(cache.GetByFlag(flagId), context, surface);
        }

        private static WorldStateUsageSummaryModel[] Build(IReadOnlyList<WorldStateUsageDefinition> usages, in WorldStateUsageContext context, WorldStateUsageSummarySurface surface)
        {
            if (usages == null) return Array.Empty<WorldStateUsageSummaryModel>();
            var result = new List<WorldStateUsageSummaryModel>();
            for (int i = 0; i < usages.Count; i++)
            {
                var usage = usages[i];
                if (usage == null || !usage.IsVisibleOn(surface)) continue;
                bool available = usage.ConditionsSatisfied(context);
                if (!available && surface == WorldStateUsageSummarySurface.LocationPanel) continue;
                var record = context.UsageProgress != null ? context.UsageProgress.GetRecord(usage.Id) : null;
                bool used = record != null && record.UsedCount > 0;
                bool canUse = usage.RepeatPolicy.CanUse(usage, context.UsageProgress, context.CurrentDay, out string repeatReason);
                result.Add(new WorldStateUsageSummaryModel(usage.Id, usage.SourceFlagId, usage.DisplayNameKey, usage.DescriptionKey, usage.InteractionPromptKey, usage.NextActionKey, available, used, canUse && used, repeatReason, BuildRewardIds(record)));
            }

            return result.ToArray();
        }

        private static string[] BuildRewardIds(WorldStateUsageRecord record)
        {
            if (record == null) return Array.Empty<string>();
            var values = new List<string>();
            AddRange(values, record.UnlockedActivityIds);
            AddRange(values, record.UnlockedFollowUpQuestIds);
            AddRange(values, record.DiscoveredEncyclopediaEntryIds);
            AddRange(values, record.CareerHintGrantHistory);
            return values.ToArray();
        }

        private static void AddRange(List<string> target, string[] values)
        {
            if (values == null) return;
            for (int i = 0; i < values.Length; i++) if (!string.IsNullOrEmpty(values[i]) && !target.Contains(values[i])) target.Add(values[i]);
        }
    }
}
