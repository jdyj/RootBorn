using System;
using System.Collections.Generic;

namespace Rootborn.Core.WorldState
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

        public WorldStateUsageSummaryModel(string usageId, string sourceWorldStateFlagId, string displayName, string description, string interactionPrompt, string nextAction,
            bool isAvailable, bool isUsed, bool isRepeatable, string repeatState, string[] rewardSummaryIds)
        {
            UsageId = usageId ?? string.Empty;
            SourceWorldStateFlagId = sourceWorldStateFlagId ?? string.Empty;
            DisplayName = string.IsNullOrEmpty(displayName) ? UsageId : displayName;
            Description = string.IsNullOrEmpty(description) ? DisplayName : description;
            InteractionPrompt = string.IsNullOrEmpty(interactionPrompt) ? DisplayName : interactionPrompt;
            NextAction = string.IsNullOrEmpty(nextAction) ? InteractionPrompt : nextAction;
            IsAvailable = isAvailable;
            IsUsed = isUsed;
            IsRepeatable = isRepeatable;
            RepeatState = repeatState ?? string.Empty;
            RewardSummaryIds = rewardSummaryIds ?? Array.Empty<string>();
        }
    }

    /// <summary>id·근거 상태 → 사용 정의 조회. 만들 때 한 번 색인하고 정렬 우선순위가 낮은 것부터 둔다.</summary>
    public sealed class WorldStateUsageLookupCache
    {
        private readonly WorldStateUsage[] _usages;
        private readonly Dictionary<string, WorldStateUsage> _byId = new Dictionary<string, WorldStateUsage>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<WorldStateUsage>> _byFlag = new Dictionary<string, List<WorldStateUsage>>(StringComparer.Ordinal);

        public WorldStateUsageLookupCache(IReadOnlyList<WorldStateUsage> usages)
        {
            BuildCount = 1;
            var values = new List<WorldStateUsage>();
            if (usages != null)
            {
                for (int i = 0; i < usages.Count; i++)
                {
                    var usage = usages[i];
                    if (usage == null || string.IsNullOrEmpty(usage.Id)) continue;
                    values.Add(usage);
                    _byId[usage.Id] = usage;
                    string flagId = usage.SourceFlagId;
                    if (string.IsNullOrEmpty(flagId)) continue;
                    if (!_byFlag.TryGetValue(flagId, out var list))
                    {
                        list = new List<WorldStateUsage>();
                        _byFlag[flagId] = list;
                    }

                    list.Add(usage);
                }
            }

            // List.Sort는 안정 정렬이 아니다(원본도 같다). 같은 우선순위끼리의 순서는 정하지 않는다.
            values.Sort((a, b) => a.SortPriority.CompareTo(b.SortPriority));
            _usages = values.ToArray();
        }

        public int BuildCount { get; }
        public int LookupCount { get; private set; }

        public bool TryGetById(string usageId, out WorldStateUsage usage)
        {
            LookupCount++;
            return _byId.TryGetValue(usageId ?? string.Empty, out usage);
        }

        public WorldStateUsage[] GetByFlag(string flagId)
        {
            LookupCount++;
            return !string.IsNullOrEmpty(flagId) && _byFlag.TryGetValue(flagId, out var list) ? list.ToArray() : Array.Empty<WorldStateUsage>();
        }

        public WorldStateUsage[] GetAll()
        {
            LookupCount++;
            return _usages;
        }
    }

    /// <summary>화면에 보일 사용 목록. 장소 패널에는 지금 쓸 수 있는 것만 나온다.</summary>
    public static class WorldStateUsageSummaryBuilder
    {
        public static WorldStateUsageSummaryModel[] BuildForSurface(WorldStateUsageLookupCache cache, in WorldStateUsageContext context, WorldStateUsageSummarySurface surface)
        {
            if (cache == null) return Array.Empty<WorldStateUsageSummaryModel>();
            return Build(cache.GetAll(), in context, surface);
        }

        public static WorldStateUsageSummaryModel[] BuildForFlag(WorldStateUsageLookupCache cache, string flagId, in WorldStateUsageContext context, WorldStateUsageSummarySurface surface)
        {
            if (cache == null) return Array.Empty<WorldStateUsageSummaryModel>();
            return Build(cache.GetByFlag(flagId), in context, surface);
        }

        private static WorldStateUsageSummaryModel[] Build(IReadOnlyList<WorldStateUsage> usages, in WorldStateUsageContext context, WorldStateUsageSummarySurface surface)
        {
            var result = new List<WorldStateUsageSummaryModel>();
            for (int i = 0; i < usages.Count; i++)
            {
                var usage = usages[i];
                if (usage == null || !usage.IsVisibleOn(surface)) continue;
                bool available = usage.ConditionsSatisfied(in context);
                if (!available && surface == WorldStateUsageSummarySurface.LocationPanel) continue;
                var record = context.UsageProgress != null ? context.UsageProgress.GetRecord(usage.Id) : null;
                bool used = record != null && record.UsedCount > 0;
                bool canUse = usage.RepeatPolicy.CanUse(usage, context.UsageProgress, context.CurrentDay, out string repeatReason);
                result.Add(new WorldStateUsageSummaryModel(usage.Id, usage.SourceFlagId, usage.DisplayNameKey, usage.DescriptionKey, usage.InteractionPromptKey, usage.NextActionKey,
                    available, used, canUse && used, repeatReason, BuildRewardIds(record)));
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
            for (int i = 0; i < values.Length; i++) if (!string.IsNullOrEmpty(values[i]) && !target.Contains(values[i])) target.Add(values[i]);
        }
    }

    /// <summary>사용 기록 세이브 파일 읽기·쓰기. 파일이 없거나 읽을 수 없으면 Read가 null을 돌려준다.</summary>
    public interface IWorldStateUsageSaveStore
    {
        WorldStateUsageProgressSaveData Read(string saveSlot, string fileName);
        void Write(string saveSlot, string fileName, WorldStateUsageProgressSaveData data);
    }

    /// <summary>플레이어마다 파일 하나(원본 WorldStateUsageProgressPersistence). 파일 형식(JSON)은 저장소가 맡는다.</summary>
    public static class WorldStateUsageSaveFiles
    {
        public static string FileNameFor(string playerId)
        {
            string player = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            return "world-state-usage-" + WorldStateSaveFiles.Sanitize(player) + ".json";
        }

        /// <summary>
        /// 파일의 슬롯·플레이어 값을 그대로 쓴다(원본과 같다). 파일에 플레이어 id가 원래 값으로 들어 있어 이름 정리 전의 id가 돌아온다.
        /// </summary>
        public static WorldStateUsageProgress LoadOrCreate(IWorldStateUsageSaveStore store, string saveSlot, string playerId)
        {
            string slot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            string player = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            var data = store?.Read(slot, FileNameFor(player));
            return data == null ? new WorldStateUsageProgress(slot, player) : WorldStateUsageProgress.FromSaveData(data);
        }

        public static void Save(IWorldStateUsageSaveStore store, WorldStateUsageProgress progress)
        {
            if (store == null || progress == null) return;
            store.Write(progress.SaveSlot, FileNameFor(progress.PlayerId), progress.ToSaveData());
        }
    }
}
