using System;
using System.Collections.Generic;

namespace Rootborn.Game.WorldState
{
    [Serializable]
    public sealed class WorldStateProgressSaveData
    {
        public string SaveSlot;
        public string PlayerId;
        public WorldStateFlagRecordSaveData[] Records = Array.Empty<WorldStateFlagRecordSaveData>();
        public string[] TodayWorldChangeFlagIds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class WorldStateFlagRecordSaveData
    {
        public string FlagId;
        public string Scope;
        public int ActivatedDay;
        public string SourceQuestChainId;
        public string SourceQuestStepId;
        public string SourceEventId;
        public bool SeenNotification;
        public int AppliedEffectVersion;
    }

    public readonly struct WorldStateFlagRecord
    {
        public readonly string FlagId;
        public readonly WorldStateScopeKind Scope;
        public readonly int ActivatedDay;
        public readonly string SourceQuestChainId;
        public readonly string SourceQuestStepId;
        public readonly string SourceEventId;
        public readonly bool SeenNotification;
        public readonly int AppliedEffectVersion;

        public WorldStateFlagRecord(string flagId, WorldStateScopeKind scope, int activatedDay, string sourceQuestChainId, string sourceQuestStepId, string sourceEventId, bool seenNotification, int appliedEffectVersion)
        {
            FlagId = flagId ?? string.Empty;
            Scope = scope;
            ActivatedDay = activatedDay;
            SourceQuestChainId = sourceQuestChainId ?? string.Empty;
            SourceQuestStepId = sourceQuestStepId ?? string.Empty;
            SourceEventId = sourceEventId ?? string.Empty;
            SeenNotification = seenNotification;
            AppliedEffectVersion = appliedEffectVersion;
        }

        public WorldStateFlagRecord WithSeenNotification(bool seen)
        {
            return new WorldStateFlagRecord(FlagId, Scope, ActivatedDay, SourceQuestChainId, SourceQuestStepId, SourceEventId, seen, AppliedEffectVersion);
        }

        public WorldStateFlagRecord WithAppliedEffectVersion(int version)
        {
            return new WorldStateFlagRecord(FlagId, Scope, ActivatedDay, SourceQuestChainId, SourceQuestStepId, SourceEventId, SeenNotification, version);
        }
    }

    public readonly struct TodayWorldChangeSummaryEntry
    {
        public readonly string FlagId;
        public readonly WorldStateScopeKind Scope;
        public readonly int ActivatedDay;

        public TodayWorldChangeSummaryEntry(string flagId, WorldStateScopeKind scope, int activatedDay)
        {
            FlagId = flagId ?? string.Empty;
            Scope = scope;
            ActivatedDay = activatedDay;
        }
    }

    public sealed class WorldStateProgress
    {
        private readonly Dictionary<string, WorldStateFlagRecord> _recordsByFlagId = new Dictionary<string, WorldStateFlagRecord>(StringComparer.Ordinal);
        private readonly List<string> _todayFlagIds = new List<string>();

        public WorldStateProgress(string saveSlot, string playerId)
        {
            SaveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            PlayerId = string.IsNullOrEmpty(playerId) ? "player" : playerId;
        }

        public string SaveSlot { get; }
        public string PlayerId { get; }
        public int ActiveFlagCount => _recordsByFlagId.Count;
        public int NewNotificationCount
        {
            get
            {
                int count = 0;
                foreach (var pair in _recordsByFlagId) if (!pair.Value.SeenNotification) count++;
                return count;
            }
        }

        public TodayWorldChangeSummaryEntry[] TodayWorldChangeSummary
        {
            get
            {
                var result = new List<TodayWorldChangeSummaryEntry>(_todayFlagIds.Count);
                for (int i = 0; i < _todayFlagIds.Count; i++)
                {
                    if (_recordsByFlagId.TryGetValue(_todayFlagIds[i], out var record)) result.Add(new TodayWorldChangeSummaryEntry(record.FlagId, record.Scope, record.ActivatedDay));
                }

                return result.ToArray();
            }
        }

        public bool IsActive(WorldStateFlagDefinition flag)
        {
            return flag != null && IsActive(flag.Id);
        }

        public bool IsActive(string flagId)
        {
            return !string.IsNullOrEmpty(flagId) && _recordsByFlagId.ContainsKey(flagId);
        }

        public WorldStateFlagRecord GetRecord(WorldStateFlagDefinition flag)
        {
            if (flag == null) return default;
            return _recordsByFlagId.TryGetValue(flag.Id, out var record) ? record : default;
        }

        public bool TryActivate(WorldStateFlagDefinition flag, WorldStateActivationSource source)
        {
            if (flag == null || string.IsNullOrEmpty(flag.Id) || _recordsByFlagId.ContainsKey(flag.Id)) return false;
            var record = new WorldStateFlagRecord(flag.Id, flag.Scope, source.Day, source.QuestChainId, source.QuestStepId, source.EventId, false, 0);
            _recordsByFlagId.Add(flag.Id, record);
            _todayFlagIds.Add(flag.Id);
            return true;
        }

        public bool MarkNotificationSeen(WorldStateFlagDefinition flag)
        {
            if (flag == null || !_recordsByFlagId.TryGetValue(flag.Id, out var record) || record.SeenNotification) return false;
            _recordsByFlagId[flag.Id] = record.WithSeenNotification(true);
            return true;
        }

        public bool MarkEffectVersionApplied(WorldStateFlagDefinition flag, int version)
        {
            if (flag == null || !_recordsByFlagId.TryGetValue(flag.Id, out var record)) return false;
            int normalized = Math.Max(0, version);
            if (record.AppliedEffectVersion >= normalized) return false;
            _recordsByFlagId[flag.Id] = record.WithAppliedEffectVersion(normalized);
            return true;
        }

        public WorldStateProgressSaveData ToSaveData()
        {
            var records = new List<WorldStateFlagRecordSaveData>(_recordsByFlagId.Count);
            foreach (var pair in _recordsByFlagId)
            {
                var record = pair.Value;
                records.Add(new WorldStateFlagRecordSaveData
                {
                    FlagId = record.FlagId,
                    Scope = record.Scope.ToString(),
                    ActivatedDay = record.ActivatedDay,
                    SourceQuestChainId = record.SourceQuestChainId,
                    SourceQuestStepId = record.SourceQuestStepId,
                    SourceEventId = record.SourceEventId,
                    SeenNotification = record.SeenNotification,
                    AppliedEffectVersion = record.AppliedEffectVersion,
                });
            }

            return new WorldStateProgressSaveData
            {
                SaveSlot = SaveSlot,
                PlayerId = PlayerId,
                Records = records.ToArray(),
                TodayWorldChangeFlagIds = _todayFlagIds.ToArray(),
            };
        }

        public static WorldStateProgress FromSaveData(WorldStateProgressSaveData saveData, string fallbackSaveSlot, string fallbackPlayerId)
        {
            var progress = new WorldStateProgress(saveData != null && !string.IsNullOrEmpty(saveData.SaveSlot) ? saveData.SaveSlot : fallbackSaveSlot, saveData != null && !string.IsNullOrEmpty(saveData.PlayerId) ? saveData.PlayerId : fallbackPlayerId);
            if (saveData == null) return progress;
            if (saveData.Records != null)
            {
                for (int i = 0; i < saveData.Records.Length; i++)
                {
                    var saved = saveData.Records[i];
                    if (saved == null || string.IsNullOrEmpty(saved.FlagId)) continue;
                    if (!Enum.TryParse(saved.Scope, out WorldStateScopeKind scope)) scope = WorldStateScopeKind.Shared;
                    progress._recordsByFlagId[saved.FlagId] = new WorldStateFlagRecord(saved.FlagId, scope, saved.ActivatedDay, saved.SourceQuestChainId, saved.SourceQuestStepId, saved.SourceEventId, saved.SeenNotification, saved.AppliedEffectVersion);
                }
            }

            if (saveData.TodayWorldChangeFlagIds != null)
            {
                for (int i = 0; i < saveData.TodayWorldChangeFlagIds.Length; i++)
                {
                    string flagId = saveData.TodayWorldChangeFlagIds[i];
                    if (!string.IsNullOrEmpty(flagId) && progress._recordsByFlagId.ContainsKey(flagId) && !progress._todayFlagIds.Contains(flagId)) progress._todayFlagIds.Add(flagId);
                }
            }

            return progress;
        }
    }
}
