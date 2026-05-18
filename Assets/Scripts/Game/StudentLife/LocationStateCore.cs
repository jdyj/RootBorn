using System;
using System.Collections.Generic;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Quests;
using Rootborn.Game.WorldState;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    public readonly struct LocationStateContext
    {
        public readonly LocationDefinition Location;
        public readonly TimeSlotDefinition TimeSlot;
        public readonly StudentLifeProgress StudentProgress;
        public readonly WorldStateProgress WorldStateProgress;
        public readonly QuestLog QuestLog;
        public readonly NpcScheduleResolver NpcScheduleResolver;
        public readonly LocationStateProgress LocationStateProgress;

        public LocationStateContext(LocationDefinition location, TimeSlotDefinition timeSlot, StudentLifeProgress studentProgress, WorldStateProgress worldStateProgress, QuestLog questLog, NpcScheduleResolver npcScheduleResolver, LocationStateProgress locationStateProgress)
        {
            Location = location;
            TimeSlot = timeSlot;
            StudentProgress = studentProgress;
            WorldStateProgress = worldStateProgress;
            QuestLog = questLog;
            NpcScheduleResolver = npcScheduleResolver;
            LocationStateProgress = locationStateProgress;
        }
    }

    public enum LocationStateConflictMode
    {
        HighestPriority,
        MergeAll,
        FallbackFirst
    }

    public sealed class LocationStateLookupCache
    {
        private readonly Dictionary<string, List<LocationStateDefinition>> _statesByLocationId = new Dictionary<string, List<LocationStateDefinition>>(StringComparer.Ordinal);

        public LocationStateLookupCache(IReadOnlyList<LocationStateDefinition> states)
        {
            BuildCount = 1;
            if (states == null) return;
            for (int i = 0; i < states.Count; i++)
            {
                var state = states[i];
                if (state == null || string.IsNullOrEmpty(state.LocationId)) continue;
                if (!_statesByLocationId.TryGetValue(state.LocationId, out var list))
                {
                    list = new List<LocationStateDefinition>();
                    _statesByLocationId.Add(state.LocationId, list);
                }
                list.Add(state);
            }
        }

        public int BuildCount { get; }

        public IReadOnlyList<LocationStateDefinition> GetStatesForLocation(LocationDefinition location)
        {
            if (location == null || string.IsNullOrEmpty(location.Id)) return Array.Empty<LocationStateDefinition>();
            return _statesByLocationId.TryGetValue(location.Id, out var list) ? list : Array.Empty<LocationStateDefinition>();
        }
    }

    public sealed class LocationStateResolver
    {
        private readonly LocationStateLookupCache _cache;
        private readonly LocationStateConflictPolicyDefinition _policy;

        public LocationStateResolver(LocationStateLookupCache cache, LocationStateConflictPolicyDefinition policy)
        {
            _cache = cache;
            _policy = policy;
        }

        public LocationStateSummaryModel Resolve(in LocationStateContext context)
        {
            var candidates = _cache != null ? _cache.GetStatesForLocation(context.Location) : Array.Empty<LocationStateDefinition>();
            var matches = new List<LocationStateDefinition>();
            for (int i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (candidate != null && candidate.IsSatisfied(in context)) matches.Add(candidate);
            }
            if (matches.Count == 0) return LocationStateSummaryModel.Empty(context.Location);

            var selected = Select(matches);
            var builder = new LocationStateSummaryBuilder(context.Location);
            for (int i = 0; i < selected.Count; i++) builder.AddState(selected[i]);
            var summary = builder.Build();
            if (context.LocationStateProgress != null && summary.PrimaryState != null)
            {
                context.LocationStateProgress.MarkDiscovered(summary.LocationId, summary.PrimaryState.Id, context.StudentProgress != null ? context.StudentProgress.CurrentDay : 1, context.TimeSlot != null ? context.TimeSlot.Id : string.Empty, summary.DisplayName, summary.IsHidden);
            }
            return summary;
        }

        private List<LocationStateDefinition> Select(List<LocationStateDefinition> matches)
        {
            var mode = _policy != null ? _policy.Mode : LocationStateConflictMode.HighestPriority;
            if (mode == LocationStateConflictMode.MergeAll) return matches;
            LocationStateDefinition selected = matches[0];
            if (mode == LocationStateConflictMode.HighestPriority)
            {
                for (int i = 1; i < matches.Count; i++) if (matches[i].Priority > selected.Priority) selected = matches[i];
            }
            return new List<LocationStateDefinition> { selected };
        }
    }

    public sealed class LocationStateSummaryBuilder
    {
        private readonly LocationDefinition _location;
        private readonly List<LocationStateDefinition> _states = new List<LocationStateDefinition>();
        private readonly List<string> _activityIds = new List<string>();
        private readonly List<string> _npcIds = new List<string>();
        private readonly List<string> _clueIds = new List<string>();
        private readonly List<string> _objectIds = new List<string>();
        private readonly List<string> _portalIds = new List<string>();
        private readonly List<string> _shopItemIds = new List<string>();

        public LocationStateSummaryBuilder(LocationDefinition location) { _location = location; }
        public void AddState(LocationStateDefinition state) { if (state == null) return; _states.Add(state); state.AppendEffects(this); }
        public void AddActivities(IReadOnlyList<LocationActivityDefinition> values) { if (values == null) return; for (int i = 0; i < values.Count; i++) AddId(_activityIds, values[i] != null ? values[i].Id : string.Empty); }
        public void AddNpcs(IReadOnlyList<Rootborn.Game.Dialogue.NpcDefinition> values) { if (values == null) return; for (int i = 0; i < values.Count; i++) AddId(_npcIds, values[i] != null ? values[i].Id : string.Empty); }
        public void AddClues(IReadOnlyList<Rootborn.Game.DiscoveryClues.DiscoveryClueDefinition> values) { if (values == null) return; for (int i = 0; i < values.Count; i++) AddId(_clueIds, values[i] != null ? values[i].Id : string.Empty); }
        public void AddInteractableObjects(string[] values) => AddIds(_objectIds, values);
        public void AddPortals(string[] values) => AddIds(_portalIds, values);
        public void AddShopItems(string[] values) => AddIds(_shopItemIds, values);

        public LocationStateSummaryModel Build()
        {
            var primary = _states.Count > 0 ? _states[0] : null;
            var stateIds = new string[_states.Count];
            for (int i = 0; i < _states.Count; i++) stateIds[i] = _states[i].Id;
            return new LocationStateSummaryModel(_location != null ? _location.Id : string.Empty, primary, stateIds, primary != null ? primary.DisplayNameKey : string.Empty, primary != null ? primary.DescriptionKey : string.Empty, false, _activityIds.ToArray(), _npcIds.ToArray(), _clueIds.ToArray(), _objectIds.ToArray(), _portalIds.ToArray(), _shopItemIds.ToArray());
        }

        private static void AddIds(List<string> target, string[] values) { if (values == null) return; for (int i = 0; i < values.Length; i++) AddId(target, values[i]); }
        private static void AddId(List<string> target, string value) { if (!string.IsNullOrEmpty(value) && !target.Contains(value)) target.Add(value); }
    }

    public readonly struct LocationStateSummaryModel
    {
        public readonly string LocationId;
        public readonly LocationStateDefinition PrimaryState;
        public readonly string[] StateIds;
        public readonly string DisplayName;
        public readonly string Description;
        public readonly bool IsHidden;
        public readonly string[] AvailableActivityIds;
        public readonly string[] PresentNpcIds;
        public readonly string[] ClueIds;
        public readonly string[] InteractableObjectIds;
        public readonly string[] PortalIds;
        public readonly string[] ShopItemIds;

        public LocationStateSummaryModel(string locationId, LocationStateDefinition primaryState, string[] stateIds, string displayName, string description, bool isHidden, string[] availableActivityIds, string[] presentNpcIds, string[] clueIds, string[] interactableObjectIds, string[] portalIds, string[] shopItemIds)
        {
            LocationId = locationId ?? string.Empty;
            PrimaryState = primaryState;
            StateIds = stateIds ?? Array.Empty<string>();
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
            IsHidden = isHidden;
            AvailableActivityIds = availableActivityIds ?? Array.Empty<string>();
            PresentNpcIds = presentNpcIds ?? Array.Empty<string>();
            ClueIds = clueIds ?? Array.Empty<string>();
            InteractableObjectIds = interactableObjectIds ?? Array.Empty<string>();
            PortalIds = portalIds ?? Array.Empty<string>();
            ShopItemIds = shopItemIds ?? Array.Empty<string>();
        }

        public static LocationStateSummaryModel Empty(LocationDefinition location)
        {
            return new LocationStateSummaryModel(location != null ? location.Id : string.Empty, null, Array.Empty<string>(), string.Empty, string.Empty, false, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());
        }
    }

    [Serializable] public sealed class LocationStateProgressSaveData { public string SaveSlot; public string PlayerId; public LocationStateRecordSaveData[] Records = Array.Empty<LocationStateRecordSaveData>(); public LocationStateLastVisitSaveData[] LastVisitedStates = Array.Empty<LocationStateLastVisitSaveData>(); public LocationStateTodaySummarySaveData[] TodaySummary = Array.Empty<LocationStateTodaySummarySaveData>(); public string[] HiddenHintVisibleStateIds = Array.Empty<string>(); }
    [Serializable] public sealed class LocationStateRecordSaveData { public string StateId; public bool Discovered; public int FirstDiscoveredDay; public string FirstDiscoveredTimeSlotId; public string[] RewardClaimedIds = Array.Empty<string>(); }
    [Serializable] public sealed class LocationStateLastVisitSaveData { public string LocationId; public string StateId; }
    [Serializable] public sealed class LocationStateTodaySummarySaveData { public string LocationId; public string StateId; public int Day; public string TimeSlotId; public string DisplayName; public bool Hidden; }

    public sealed class LocationStateProgress
    {
        private sealed class Record { public string StateId; public bool Discovered; public int FirstDiscoveredDay; public string FirstDiscoveredTimeSlotId; public readonly HashSet<string> RewardClaimedIds = new HashSet<string>(StringComparer.Ordinal); }
        private readonly Dictionary<string, Record> _records = new Dictionary<string, Record>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _lastVisitedStateByLocation = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly List<LocationStateTodaySummarySaveData> _todaySummary = new List<LocationStateTodaySummarySaveData>();
        private readonly HashSet<string> _hiddenHintVisibleStateIds = new HashSet<string>(StringComparer.Ordinal);
        public LocationStateProgress(string saveSlot, string playerId) { SaveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot; PlayerId = string.IsNullOrEmpty(playerId) ? "player" : playerId; }
        public string SaveSlot { get; }
        public string PlayerId { get; }
        public LocationStateTodaySummarySaveData[] TodaySummary => _todaySummary.ToArray();
        public bool HasDiscovered(string stateId) => !string.IsNullOrEmpty(stateId) && _records.TryGetValue(stateId, out var record) && record.Discovered;
        public bool HasRewardClaimed(string stateId, string rewardId) => !string.IsNullOrEmpty(stateId) && !string.IsNullOrEmpty(rewardId) && _records.TryGetValue(stateId, out var record) && record.RewardClaimedIds.Contains(rewardId);
        public string GetLastVisitedStateId(string locationId) => !string.IsNullOrEmpty(locationId) && _lastVisitedStateByLocation.TryGetValue(locationId, out var stateId) ? stateId : string.Empty;
        public bool IsHiddenHintVisible(string stateId) => !string.IsNullOrEmpty(stateId) && _hiddenHintVisibleStateIds.Contains(stateId);
        public void MarkDiscovered(string locationId, string stateId, int day, string timeSlotId, string displayName, bool hidden) { if (string.IsNullOrEmpty(stateId)) return; var record = GetOrCreate(stateId); if (!record.Discovered) { record.Discovered = true; record.FirstDiscoveredDay = Mathf.Max(1, day); record.FirstDiscoveredTimeSlotId = timeSlotId ?? string.Empty; } if (!string.IsNullOrEmpty(locationId)) _lastVisitedStateByLocation[locationId] = stateId; AddToday(locationId, stateId, day, timeSlotId, displayName, hidden); }
        public bool MarkRewardClaimed(string stateId, string rewardId) { if (string.IsNullOrEmpty(stateId) || string.IsNullOrEmpty(rewardId)) return false; return GetOrCreate(stateId).RewardClaimedIds.Add(rewardId); }
        public void MarkHiddenHintVisible(string stateId) { if (!string.IsNullOrEmpty(stateId)) _hiddenHintVisibleStateIds.Add(stateId); }
        public LocationStateProgressSaveData ToSaveData() { var records = new List<LocationStateRecordSaveData>(_records.Count); foreach (var pair in _records) { var record = pair.Value; records.Add(new LocationStateRecordSaveData { StateId = record.StateId, Discovered = record.Discovered, FirstDiscoveredDay = record.FirstDiscoveredDay, FirstDiscoveredTimeSlotId = record.FirstDiscoveredTimeSlotId, RewardClaimedIds = ToArray(record.RewardClaimedIds) }); } var visits = new List<LocationStateLastVisitSaveData>(_lastVisitedStateByLocation.Count); foreach (var pair in _lastVisitedStateByLocation) visits.Add(new LocationStateLastVisitSaveData { LocationId = pair.Key, StateId = pair.Value }); return new LocationStateProgressSaveData { SaveSlot = SaveSlot, PlayerId = PlayerId, Records = records.ToArray(), LastVisitedStates = visits.ToArray(), TodaySummary = _todaySummary.ToArray(), HiddenHintVisibleStateIds = ToArray(_hiddenHintVisibleStateIds) }; }
        public static LocationStateProgress FromSaveData(LocationStateProgressSaveData saveData, string fallbackSaveSlot, string fallbackPlayerId) { var progress = new LocationStateProgress(saveData != null && !string.IsNullOrEmpty(saveData.SaveSlot) ? saveData.SaveSlot : fallbackSaveSlot, saveData != null && !string.IsNullOrEmpty(saveData.PlayerId) ? saveData.PlayerId : fallbackPlayerId); if (saveData == null) return progress; if (saveData.Records != null) for (int i = 0; i < saveData.Records.Length; i++) { var saved = saveData.Records[i]; if (saved == null || string.IsNullOrEmpty(saved.StateId)) continue; var record = progress.GetOrCreate(saved.StateId); record.Discovered = saved.Discovered; record.FirstDiscoveredDay = Mathf.Max(0, saved.FirstDiscoveredDay); record.FirstDiscoveredTimeSlotId = saved.FirstDiscoveredTimeSlotId ?? string.Empty; AddRange(record.RewardClaimedIds, saved.RewardClaimedIds); } if (saveData.LastVisitedStates != null) for (int i = 0; i < saveData.LastVisitedStates.Length; i++) { var saved = saveData.LastVisitedStates[i]; if (saved != null && !string.IsNullOrEmpty(saved.LocationId) && !string.IsNullOrEmpty(saved.StateId)) progress._lastVisitedStateByLocation[saved.LocationId] = saved.StateId; } if (saveData.TodaySummary != null) for (int i = 0; i < saveData.TodaySummary.Length; i++) if (saveData.TodaySummary[i] != null) progress._todaySummary.Add(saveData.TodaySummary[i]); AddRange(progress._hiddenHintVisibleStateIds, saveData.HiddenHintVisibleStateIds); return progress; }
        private Record GetOrCreate(string stateId) { if (!_records.TryGetValue(stateId, out var record)) { record = new Record { StateId = stateId }; _records.Add(stateId, record); } return record; }
        private void AddToday(string locationId, string stateId, int day, string timeSlotId, string displayName, bool hidden) { for (int i = 0; i < _todaySummary.Count; i++) if (_todaySummary[i].StateId == stateId) return; _todaySummary.Add(new LocationStateTodaySummarySaveData { LocationId = locationId ?? string.Empty, StateId = stateId, Day = Mathf.Max(1, day), TimeSlotId = timeSlotId ?? string.Empty, DisplayName = displayName ?? string.Empty, Hidden = hidden }); }
        private static string[] ToArray(HashSet<string> values) { var result = new string[values.Count]; values.CopyTo(result); return result; }
        private static void AddRange(HashSet<string> target, string[] values) { if (target == null || values == null) return; for (int i = 0; i < values.Length; i++) if (!string.IsNullOrEmpty(values[i])) target.Add(values[i]); }
    }
}
