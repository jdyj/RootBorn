using System;
using System.Collections.Generic;

namespace Rootborn.Core.DiscoveryClues
{
    /// <summary>세이브 파일에 쓰는 모양. 필드 이름은 기존 세이브 파일과 같아야 한다.</summary>
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

    /// <summary>해석 하나의 기록. 반복 정책이면 완료 횟수가 늘고, 첫 완료일은 남는다.</summary>
    public sealed class ClueInterpretationRecord
    {
        private readonly List<string> _outcomeIds = new List<string>();
        private readonly List<string> _rewardClaimedIds = new List<string>();
        private readonly List<string> _encyclopediaExpansionIds = new List<string>();
        private readonly List<string> _careerHintGrantHistory = new List<string>();
        private readonly List<string> _followUpQuestIds = new List<string>();
        private readonly List<string> _relatedWorldStateChanges = new List<string>();
        private readonly List<string> _relationshipChangeIds = new List<string>();
        private readonly List<string> _statusChangeIds = new List<string>();

        public ClueInterpretationRecord(string clueId, string interpretationId, string policyGroupId)
        {
            ClueId = clueId ?? string.Empty;
            InterpretationId = interpretationId ?? string.Empty;
            PolicyGroupId = policyGroupId ?? string.Empty;
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
        public string[] OutcomeIds => _outcomeIds.ToArray();
        public string[] RewardClaimedIds => _rewardClaimedIds.ToArray();
        public string[] EncyclopediaExpansionIds => _encyclopediaExpansionIds.ToArray();
        public string[] CareerHintGrantHistory => _careerHintGrantHistory.ToArray();
        public string[] FollowUpQuestIds => _followUpQuestIds.ToArray();
        public string[] RelatedWorldStateChanges => _relatedWorldStateChanges.ToArray();
        public string[] RelationshipChangeIds => _relationshipChangeIds.ToArray();
        public string[] StatusChangeIds => _statusChangeIds.ToArray();

        internal void MarkCompleted(int day, string[] outcomeIds, string[] encyclopediaIds, string[] careerHintIds, string[] questIds, string[] worldStateIds, string[] relationshipIds, string[] statusIds)
        {
            int safeDay = Math.Max(1, day);
            if (FirstSelectedDay <= 0) FirstSelectedDay = safeDay;
            LastInterpretedDay = safeDay;
            if (!Completed) CompletedDay = safeDay;
            Selected = true;
            Completed = true;
            RewardClaimed = true;
            CompletedCount = Math.Max(1, CompletedCount + 1);
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
            var record = new ClueInterpretationRecord(saveData?.ClueId, saveData?.InterpretationId, saveData?.PolicyGroupId);
            if (saveData == null) return record;
            record.Selected = saveData.Selected;
            record.Completed = saveData.Completed;
            record.RewardClaimed = saveData.RewardClaimed;
            record.FirstSelectedDay = Math.Max(0, saveData.FirstSelectedDay);
            record.LastInterpretedDay = Math.Max(0, saveData.LastInterpretedDay);
            record.CompletedDay = Math.Max(0, saveData.CompletedDay);
            record.CompletedCount = Math.Max(0, saveData.CompletedCount);
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

        // 원본은 HashSet이라 순서가 정해지지 않았다. 여기는 넣은 순서를 지킨다.
        private static void AddRange(List<string> target, string[] values)
        {
            if (values == null) return;
            for (int i = 0; i < values.Length; i++) if (!string.IsNullOrEmpty(values[i]) && !target.Contains(values[i])) target.Add(values[i]);
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
            ClueId = clueId ?? string.Empty;
            InterpretationId = interpretationId ?? string.Empty;
            Completed = completed;
            RewardClaimed = rewardClaimed;
        }
    }

    /// <summary>플레이어 한 명의 단서 해석 기록과 정책 판정.</summary>
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

        /// <summary>기록이 없으면 빈 기록을 돌려준다. 원본은 이때 빈 기록을 저장해 두었다(조회만 해도 세이브에 남았다).</summary>
        public ClueInterpretationRecord GetRecord(string interpretationId)
        {
            string key = interpretationId ?? string.Empty;
            return _records.TryGetValue(key, out var record) ? record : new ClueInterpretationRecord(string.Empty, key, string.Empty);
        }

        /// <param name="catalog">순서형 정책의 앞 순서를 찾을 해석 목록. 없으면 순서 1 이상의 순서형은 막힌다.</param>
        public bool CanComplete(ClueInterpretation interpretation, int currentDay, ClueInterpretationLookupCache catalog, out string reason, out ClueInterpretationResultKind resultKind)
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

            if (policy.Kind == ClueInterpretationPolicyKind.Sequential && policy.SequenceOrder > 0 && !HasCompletedPreviousSequence(policy.GroupId, policy.SequenceOrder, catalog))
            {
                reason = "previous interpretation required";
                resultKind = ClueInterpretationResultKind.LockedByPolicy;
                return false;
            }

            return true;
        }

        public void MarkCompleted(ClueInterpretation interpretation, int day, string[] outcomeIds, string[] encyclopediaIds, string[] careerHintIds, string[] questIds, string[] worldStateIds, string[] relationshipIds, string[] statusIds)
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
            foreach (var pair in _records) if (!string.IsNullOrEmpty(pair.Key)) records.Add(pair.Value.ToSaveData());
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
            var progress = new ClueInterpretationProgress(saveData?.SaveSlot, saveData?.PlayerId);
            if (saveData == null) return progress;
            if (saveData.Records != null)
            {
                for (int i = 0; i < saveData.Records.Length; i++)
                {
                    if (saveData.Records[i] == null) continue;
                    var record = ClueInterpretationRecord.FromSaveData(saveData.Records[i]);
                    if (!string.IsNullOrEmpty(record.InterpretationId)) progress._records[record.InterpretationId] = record;
                }
            }

            if (saveData.TodayInterpretationIds != null)
            {
                for (int i = 0; i < saveData.TodayInterpretationIds.Length; i++)
                {
                    string id = saveData.TodayInterpretationIds[i];
                    if (!string.IsNullOrEmpty(id) && progress._records.ContainsKey(id)) progress.AddToday(id);
                }
            }

            return progress;
        }

        private bool HasCompletedInGroup(string groupId, string exceptInterpretationId)
        {
            if (string.IsNullOrEmpty(groupId)) return false;
            foreach (var record in _records.Values)
                if (record.Completed && record.PolicyGroupId == groupId && record.InterpretationId != exceptInterpretationId) return true;
            return false;
        }

        /// <summary>
        /// 같은 그룹에서 이 순서보다 작은 순서 중 가장 큰 것(바로 앞 순서)의 해석 하나가 끝났으면 참. 앞 순서가 정의에 없으면 참.
        /// Unity 판은 그룹의 아무 해석이나(자기 자신 포함) 끝났으면 통과시켰다.
        /// </summary>
        private bool HasCompletedPreviousSequence(string groupId, int sequenceOrder, ClueInterpretationLookupCache catalog)
        {
            if (string.IsNullOrEmpty(groupId) || catalog == null) return false;
            var members = catalog.GetByPolicyGroup(groupId);
            int previousOrder = -1;
            for (int i = 0; i < members.Length; i++)
            {
                var policy = members[i].Policy;
                if (policy.Kind == ClueInterpretationPolicyKind.Sequential && policy.SequenceOrder < sequenceOrder && policy.SequenceOrder > previousOrder) previousOrder = policy.SequenceOrder;
            }

            if (previousOrder < 0) return true;
            for (int i = 0; i < members.Length; i++)
            {
                var member = members[i];
                if (member.Policy.Kind != ClueInterpretationPolicyKind.Sequential || member.Policy.SequenceOrder != previousOrder) continue;
                if (_records.TryGetValue(member.Id, out var record) && record.Completed) return true;
            }

            return false;
        }

        private void AddToday(string interpretationId)
        {
            if (!string.IsNullOrEmpty(interpretationId) && !_todayInterpretationIds.Contains(interpretationId)) _todayInterpretationIds.Add(interpretationId);
        }
    }

    /// <summary>해석 세이브 파일 읽기·쓰기. 파일이 없거나 읽을 수 없으면 Read가 null을 돌려준다.</summary>
    public interface IClueInterpretationSaveStore
    {
        ClueInterpretationProgressSaveData Read(string saveSlot, string fileName);
        void Write(string saveSlot, string fileName, ClueInterpretationProgressSaveData data);
    }

    /// <summary>
    /// 플레이어마다 파일 하나(원본 ClueInterpretationProgressPersistence). 파일 이름은 원본처럼 플레이어 id를 정리하지 않는다
    /// (기존 파일을 찾아야 한다). 저장소를 합칠 때 정리 여부를 정한다.
    /// </summary>
    public static class ClueInterpretationSaveFiles
    {
        public static string FileNameFor(string playerId) => "clue-interpretations-" + (string.IsNullOrEmpty(playerId) ? "player" : playerId) + ".json";

        public static ClueInterpretationProgress LoadOrCreate(IClueInterpretationSaveStore store, string saveSlot, string playerId)
        {
            string slot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            string player = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            var data = store?.Read(slot, FileNameFor(player));
            return data == null ? new ClueInterpretationProgress(slot, player) : ClueInterpretationProgress.FromSaveData(data);
        }

        public static void Save(IClueInterpretationSaveStore store, ClueInterpretationProgress progress)
        {
            if (store == null || progress == null) return;
            store.Write(progress.SaveSlot, FileNameFor(progress.PlayerId), progress.ToSaveData());
        }
    }
}
