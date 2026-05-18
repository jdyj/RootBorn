using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rootborn.Game.DiscoveryClues
{
    [Serializable]
    public sealed class ClueInterpretationProgressSaveData
    {
        public string SaveSlot;
        public string PlayerId;
        public ClueInterpretationRecordSaveData[] Records = Array.Empty<ClueInterpretationRecordSaveData>();
        public string[] TodayInterpretationIds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class ClueInterpretationRecordSaveData
    {
        public string ClueId;
        public string InterpretationId;
        public string PolicyGroupId;
        public bool Selected;
        public bool Completed;
        public bool RewardClaimed;
        public int FirstSelectedDay;
        public int LastInterpretedDay;
        public int CompletedDay;
        public int CompletedCount;
        public string[] OutcomeIds = Array.Empty<string>();
        public string[] RewardClaimedIds = Array.Empty<string>();
        public string[] EncyclopediaExpansionIds = Array.Empty<string>();
        public string[] CareerHintGrantHistory = Array.Empty<string>();
        public string[] FollowUpQuestIds = Array.Empty<string>();
        public string[] RelatedWorldStateChanges = Array.Empty<string>();
        public string[] RelationshipChangeIds = Array.Empty<string>();
        public string[] StatusChangeIds = Array.Empty<string>();
    }

    public sealed class ClueInterpretationRecord
    {
        private readonly HashSet<string> _outcomeIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _rewardClaimedIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _encyclopediaExpansionIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _careerHintGrantHistory = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _followUpQuestIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _relatedWorldStateChanges = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _relationshipChangeIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _statusChangeIds = new HashSet<string>(StringComparer.Ordinal);

        public ClueInterpretationRecord(string clueId, string interpretationId, string policyGroupId)
        {
            ClueId = string.IsNullOrEmpty(clueId) ? string.Empty : clueId;
            InterpretationId = string.IsNullOrEmpty(interpretationId) ? string.Empty : interpretationId;
            PolicyGroupId = string.IsNullOrEmpty(policyGroupId) ? string.Empty : policyGroupId;
        }

        public string ClueId { get; }
        public string InterpretationId { get; }
        public string PolicyGroupId { get; }
        public bool Selected { get; private set; }
        public bool Completed { get; private set; }
        public bool RewardClaimed { get; private set; }
        public int FirstSelectedDay { get; private set; }
        public int LastInterpretedDay { get; private set; }
        public int CompletedDay { get; private set; }
        public int CompletedCount { get; private set; }
        public string[] OutcomeIds => ToArray(_outcomeIds);
        public string[] RewardClaimedIds => ToArray(_rewardClaimedIds);
        public string[] EncyclopediaExpansionIds => ToArray(_encyclopediaExpansionIds);
        public string[] CareerHintGrantHistory => ToArray(_careerHintGrantHistory);
        public string[] FollowUpQuestIds => ToArray(_followUpQuestIds);
        public string[] RelatedWorldStateChanges => ToArray(_relatedWorldStateChanges);
        public string[] RelationshipChangeIds => ToArray(_relationshipChangeIds);
        public string[] StatusChangeIds => ToArray(_statusChangeIds);

        public void MarkCompleted(int day, string[] outcomeIds, string[] encyclopediaIds, string[] careerHintIds, string[] questIds, string[] worldStateIds, string[] relationshipIds, string[] statusIds)
        {
            int safeDay = Mathf.Max(1, day);
            if (FirstSelectedDay <= 0) FirstSelectedDay = safeDay;
            LastInterpretedDay = safeDay;
            if (!Completed) CompletedDay = safeDay;
            Selected = true;
            Completed = true;
            RewardClaimed = true;
            CompletedCount = Mathf.Max(1, CompletedCount + 1);
            AddRange(_outcomeIds, outcomeIds);
            AddRange(_rewardClaimedIds, outcomeIds);
            AddRange(_encyclopediaExpansionIds, encyclopediaIds);
            AddRange(_careerHintGrantHistory, careerHintIds);
            AddRange(_followUpQuestIds, questIds);
            AddRange(_relatedWorldStateChanges, worldStateIds);
            AddRange(_relationshipChangeIds, relationshipIds);
            AddRange(_statusChangeIds, statusIds);
        }

        public ClueInterpretationRecordSaveData ToSaveData()
        {
            return new ClueInterpretationRecordSaveData
            {
                ClueId = ClueId,
                InterpretationId = InterpretationId,
                PolicyGroupId = PolicyGroupId,
                Selected = Selected,
                Completed = Completed,
                RewardClaimed = RewardClaimed,
                FirstSelectedDay = FirstSelectedDay,
                LastInterpretedDay = LastInterpretedDay,
                CompletedDay = CompletedDay,
                CompletedCount = CompletedCount,
                OutcomeIds = OutcomeIds,
                RewardClaimedIds = RewardClaimedIds,
                EncyclopediaExpansionIds = EncyclopediaExpansionIds,
                CareerHintGrantHistory = CareerHintGrantHistory,
                FollowUpQuestIds = FollowUpQuestIds,
                RelatedWorldStateChanges = RelatedWorldStateChanges,
                RelationshipChangeIds = RelationshipChangeIds,
                StatusChangeIds = StatusChangeIds,
            };
        }

        public static ClueInterpretationRecord FromSaveData(ClueInterpretationRecordSaveData saveData)
        {
            var record = new ClueInterpretationRecord(saveData != null ? saveData.ClueId : string.Empty, saveData != null ? saveData.InterpretationId : string.Empty, saveData != null ? saveData.PolicyGroupId : string.Empty);
            if (saveData == null) return record;
            record.Selected = saveData.Selected;
            record.Completed = saveData.Completed;
            record.RewardClaimed = saveData.RewardClaimed;
            record.FirstSelectedDay = Mathf.Max(0, saveData.FirstSelectedDay);
            record.LastInterpretedDay = Mathf.Max(0, saveData.LastInterpretedDay);
            record.CompletedDay = Mathf.Max(0, saveData.CompletedDay);
            record.CompletedCount = Mathf.Max(0, saveData.CompletedCount);
            AddRange(record._outcomeIds, saveData.OutcomeIds);
            AddRange(record._rewardClaimedIds, saveData.RewardClaimedIds);
            AddRange(record._encyclopediaExpansionIds, saveData.EncyclopediaExpansionIds);
            AddRange(record._careerHintGrantHistory, saveData.CareerHintGrantHistory);
            AddRange(record._followUpQuestIds, saveData.FollowUpQuestIds);
            AddRange(record._relatedWorldStateChanges, saveData.RelatedWorldStateChanges);
            AddRange(record._relationshipChangeIds, saveData.RelationshipChangeIds);
            AddRange(record._statusChangeIds, saveData.StatusChangeIds);
            return record;
        }

        private static void AddRange(HashSet<string> target, string[] values)
        {
            if (target == null || values == null) return;
            for (int i = 0; i < values.Length; i++) if (!string.IsNullOrEmpty(values[i])) target.Add(values[i]);
        }

        private static string[] ToArray(HashSet<string> values)
        {
            var result = new string[values.Count];
            values.CopyTo(result);
            return result;
        }
    }

    public readonly struct ClueInterpretationTodaySummaryEntry
    {
        public readonly string ClueId;
        public readonly string InterpretationId;
        public readonly bool Completed;
        public readonly bool RewardClaimed;

        public ClueInterpretationTodaySummaryEntry(string clueId, string interpretationId, bool completed, bool rewardClaimed)
        {
            ClueId = string.IsNullOrEmpty(clueId) ? string.Empty : clueId;
            InterpretationId = string.IsNullOrEmpty(interpretationId) ? string.Empty : interpretationId;
            Completed = completed;
            RewardClaimed = rewardClaimed;
        }
    }

    public sealed class ClueInterpretationProgress
    {
        private readonly Dictionary<string, ClueInterpretationRecord> _records = new Dictionary<string, ClueInterpretationRecord>(StringComparer.Ordinal);
        private readonly List<string> _todayInterpretationIds = new List<string>();

        public ClueInterpretationProgress(string saveSlot, string playerId)
        {
            SaveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            PlayerId = string.IsNullOrEmpty(playerId) ? "player" : playerId;
        }

        public string SaveSlot { get; }
        public string PlayerId { get; }

        public ClueInterpretationTodaySummaryEntry[] TodayInterpretationSummary
        {
            get
            {
                var result = new List<ClueInterpretationTodaySummaryEntry>(_todayInterpretationIds.Count);
                for (int i = 0; i < _todayInterpretationIds.Count; i++)
                {
                    if (_records.TryGetValue(_todayInterpretationIds[i], out var record)) result.Add(new ClueInterpretationTodaySummaryEntry(record.ClueId, record.InterpretationId, record.Completed, record.RewardClaimed));
                }

                return result.ToArray();
            }
        }

        public ClueInterpretationRecord GetRecord(string interpretationId)
        {
            string key = string.IsNullOrEmpty(interpretationId) ? string.Empty : interpretationId;
            if (!_records.TryGetValue(key, out var record))
            {
                record = new ClueInterpretationRecord(string.Empty, key, string.Empty);
                _records[key] = record;
            }

            return record;
        }

        public bool CanComplete(ClueInterpretationDefinition interpretation, int currentDay, out string reason, out ClueInterpretationResultKind resultKind)
        {
            reason = string.Empty;
            resultKind = ClueInterpretationResultKind.Completed;
            if (interpretation == null) return false;
            var policy = interpretation.Policy;
            var record = GetRecord(interpretation.Id);
            if (record.Completed && (policy == null || policy.Kind != ClueInterpretationPolicyKind.Repeatable))
            {
                reason = "already completed";
                resultKind = ClueInterpretationResultKind.AlreadyCompleted;
                return false;
            }

            if (policy == null) return true;
            if (policy.CooldownDays > 0 && record.LastInterpretedDay > 0 && currentDay - record.LastInterpretedDay < policy.CooldownDays)
            {
                reason = "cooldown active";
                resultKind = ClueInterpretationResultKind.CooldownActive;
                return false;
            }

            if (policy.Kind == ClueInterpretationPolicyKind.Exclusive && HasCompletedInGroup(policy.GroupId, interpretation.Id))
            {
                reason = "exclusive group already completed";
                resultKind = ClueInterpretationResultKind.LockedByPolicy;
                return false;
            }

            if (policy.Kind == ClueInterpretationPolicyKind.Sequential && policy.SequenceOrder > 0 && !HasCompletedPreviousSequence(policy.GroupId, policy.SequenceOrder))
            {
                reason = "previous interpretation required";
                resultKind = ClueInterpretationResultKind.LockedByPolicy;
                return false;
            }

            return true;
        }

        public void MarkCompleted(ClueInterpretationDefinition interpretation, int day, string[] outcomeIds, string[] encyclopediaIds, string[] careerHintIds, string[] questIds, string[] worldStateIds, string[] relationshipIds, string[] statusIds)
        {
            if (interpretation == null || string.IsNullOrEmpty(interpretation.Id)) return;
            string groupId = interpretation.Policy != null ? interpretation.Policy.GroupId : string.Empty;
            var record = new ClueInterpretationRecord(interpretation.ClueId, interpretation.Id, groupId);
            if (_records.TryGetValue(interpretation.Id, out var existing) && existing.Completed) record = existing;
            record.MarkCompleted(day, outcomeIds, encyclopediaIds, careerHintIds, questIds, worldStateIds, relationshipIds, statusIds);
            _records[interpretation.Id] = record;
            AddToday(interpretation.Id);
        }

        public ClueInterpretationProgressSaveData ToSaveData()
        {
            var records = new List<ClueInterpretationRecordSaveData>(_records.Count);
            foreach (var pair in _records)
            {
                if (!string.IsNullOrEmpty(pair.Key)) records.Add(pair.Value.ToSaveData());
            }

            return new ClueInterpretationProgressSaveData
            {
                SaveSlot = SaveSlot,
                PlayerId = PlayerId,
                Records = records.ToArray(),
                TodayInterpretationIds = _todayInterpretationIds.ToArray(),
            };
        }

        public static ClueInterpretationProgress FromSaveData(ClueInterpretationProgressSaveData saveData)
        {
            var progress = new ClueInterpretationProgress(saveData != null ? saveData.SaveSlot : "default", saveData != null ? saveData.PlayerId : "player");
            if (saveData == null) return progress;
            if (saveData.Records != null)
            {
                for (int i = 0; i < saveData.Records.Length; i++)
                {
                    var record = ClueInterpretationRecord.FromSaveData(saveData.Records[i]);
                    if (!string.IsNullOrEmpty(record.InterpretationId)) progress._records[record.InterpretationId] = record;
                }
            }

            if (saveData.TodayInterpretationIds != null)
            {
                for (int i = 0; i < saveData.TodayInterpretationIds.Length; i++)
                {
                    string id = saveData.TodayInterpretationIds[i];
                    if (!string.IsNullOrEmpty(id) && progress._records.ContainsKey(id) && !progress._todayInterpretationIds.Contains(id)) progress._todayInterpretationIds.Add(id);
                }
            }

            return progress;
        }

        private bool HasCompletedInGroup(string groupId, string exceptInterpretationId)
        {
            if (string.IsNullOrEmpty(groupId)) return false;
            foreach (var record in _records.Values)
            {
                if (record.Completed && record.PolicyGroupId == groupId && record.InterpretationId != exceptInterpretationId) return true;
            }

            return false;
        }

        private bool HasCompletedPreviousSequence(string groupId, int sequenceOrder)
        {
            if (string.IsNullOrEmpty(groupId)) return false;
            foreach (var record in _records.Values)
            {
                if (record.Completed && record.PolicyGroupId == groupId) return true;
            }

            return sequenceOrder <= 0;
        }

        private void AddToday(string interpretationId)
        {
            if (!string.IsNullOrEmpty(interpretationId) && !_todayInterpretationIds.Contains(interpretationId)) _todayInterpretationIds.Add(interpretationId);
        }
    }
}
