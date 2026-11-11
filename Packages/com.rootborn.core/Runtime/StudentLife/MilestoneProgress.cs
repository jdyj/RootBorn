using System;
using System.Collections.Generic;
using Rootborn.Core.Inventory;

namespace Rootborn.Core.StudentLife
{
    public readonly struct MilestoneProgressChange
    {
        public readonly string MilestoneId;
        public readonly string ObjectiveId;
        public readonly int BeforeValue;
        public readonly int AfterValue;
        public readonly bool Completed;

        public MilestoneProgressChange(string milestoneId, string objectiveId, int beforeValue, int afterValue, bool completed)
        {
            MilestoneId = milestoneId ?? string.Empty;
            ObjectiveId = objectiveId ?? string.Empty;
            BeforeValue = Math.Max(0, beforeValue);
            AfterValue = Math.Max(0, afterValue);
            Completed = completed;
        }
    }

    public readonly struct MilestoneApplyResult
    {
        public readonly MilestoneProgressChange[] ProgressChanges;
        public readonly string[] RecommendedActionIds;

        public MilestoneApplyResult(MilestoneProgressChange[] progressChanges, string[] recommendedActionIds)
        {
            ProgressChanges = progressChanges ?? Array.Empty<MilestoneProgressChange>();
            RecommendedActionIds = recommendedActionIds ?? Array.Empty<string>();
        }
    }

    // 세이브 모양: 원본과 필드 이름·형이 같다(옛 세이브를 읽는다).
    [Serializable]
    public sealed class MilestoneProgressSaveData
    {
        public string SaveSlot;
        public string PlayerId;
        public MilestoneRecordSaveData[] Records = Array.Empty<MilestoneRecordSaveData>();
        public string[] AppliedDayResultKeys = Array.Empty<string>();
    }

    [Serializable]
    public sealed class MilestoneRecordSaveData
    {
        public string MilestoneId;
        public bool Completed;
        public bool RewardClaimed;
        public MilestoneObjectiveProgressSaveData[] Objectives = Array.Empty<MilestoneObjectiveProgressSaveData>();
    }

    [Serializable]
    public struct MilestoneObjectiveProgressSaveData
    {
        public string ObjectiveId;
        public int Value;
    }

    /// <summary>
    /// 마일스톤 진행. 하루 결과(키마다 한 번)로 목표 값을 올리고(내려가지 않음) 끝난 마일스톤을 기록한다.
    /// 보상은 끝난 마일스톤에서 한 번만, 모든 보상이 함께 들어갈 때만 준다.
    /// </summary>
    public sealed class MilestoneProgress
    {
        private readonly Dictionary<string, MilestoneRecord> _records = new Dictionary<string, MilestoneRecord>(StringComparer.Ordinal);
        private readonly HashSet<string> _appliedDayResultKeys = new HashSet<string>(StringComparer.Ordinal);

        public MilestoneProgress(string saveSlot, string playerId, IReadOnlyList<Milestone> milestones)
        {
            SaveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            PlayerId = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            EnsureRecords(milestones);
        }

        public string SaveSlot { get; }
        public string PlayerId { get; }

        public MilestoneApplyResult ApplyDayResult(IReadOnlyList<Milestone> milestones, StudentLifeProgress studentLifeProgress, ItemInventory inventory, string dayResultKey)
        {
            EnsureRecords(milestones);
            if (string.IsNullOrEmpty(dayResultKey) || !_appliedDayResultKeys.Add(dayResultKey)) return new MilestoneApplyResult(null, CollectRecommendations(milestones));

            var changes = new List<MilestoneProgressChange>();
            var context = new MilestoneEvaluationContext(studentLifeProgress, inventory, Array.Empty<string>());
            if (milestones != null)
            {
                for (int i = 0; i < milestones.Count; i++)
                {
                    var milestone = milestones[i];
                    if (milestone == null || milestone.Id.Length == 0 || !PrerequisitesSatisfied(milestone)) continue;
                    var record = GetOrCreateRecord(milestone.Id);
                    int completedCount = 0;
                    var objectives = milestone.Objectives;
                    for (int j = 0; j < objectives.Count; j++)
                    {
                        var objective = objectives[j];
                        if (objective == null || objective.Id.Length == 0) continue;
                        var evaluation = objective.Evaluate(in context);
                        int before = record.GetObjectiveProgress(objective.Id);
                        int evaluatedCurrent = evaluation.Current <= 0 ? objective.DayResultFallback(in context, evaluation) : evaluation.Current;
                        int after = Math.Max(before, evaluatedCurrent);
                        record.SetObjectiveProgress(objective.Id, after);
                        if (after >= evaluation.Required) completedCount++;
                        if (after != before) changes.Add(new MilestoneProgressChange(milestone.Id, objective.Id, before, after, false));
                    }
                    int required = milestone.RequiredObjectiveCount;
                    if (!record.Completed && completedCount >= required)
                    {
                        record.Completed = true;
                        changes.Add(new MilestoneProgressChange(milestone.Id, string.Empty, completedCount, required, true));
                    }
                }
            }
            return new MilestoneApplyResult(changes.ToArray(), CollectRecommendations(milestones));
        }

        /// <summary>
        /// 끝났고 아직 받지 않은 마일스톤의 보상을 준다. 물건 보상은 합쳐서 한 번에 들어가는지 확인한다
        /// (원본은 하나씩만 확인해 함께 넘치면 일부만 주고 받은 것으로 기록했다). 실패하면 아무것도 바꾸지 않는다.
        /// </summary>
        public bool TryClaimReward(Milestone milestone, ItemInventory inventory)
        {
            if (milestone == null || milestone.Id.Length == 0) return false;
            var record = GetOrCreateRecord(milestone.Id);
            if (!record.Completed || record.RewardClaimed) return false;
            var rewards = milestone.Rewards;
            var grants = new List<InventoryGrant>();
            var others = new List<IMilestoneReward>();
            for (int i = 0; i < rewards.Count; i++)
            {
                var reward = rewards[i];
                if (reward == null) continue;
                if (!reward.CanApply(inventory)) return false;
                if (reward is IMilestoneItemReward item) grants.Add(item.Grant);
                else others.Add(reward);
            }

            void Commit()
            {
                for (int i = 0; i < others.Count; i++) others[i].Apply(inventory);
                record.RewardClaimed = true;
            }

            if (grants.Count == 0)
            {
                Commit();
                return true;
            }
            return inventory.TryAddAll(grants, Commit);
        }

        public int GetObjectiveProgress(Milestone milestone, MilestoneObjective objective) => milestone == null || objective == null ? 0 : GetOrCreateRecord(milestone.Id).GetObjectiveProgress(objective.Id);
        public bool IsCompleted(Milestone milestone) => milestone != null && IsCompleted(milestone.Id);
        public bool IsCompleted(string milestoneId) => GetOrCreateRecord(milestoneId).Completed;
        public bool IsRewardClaimed(Milestone milestone) => milestone != null && GetOrCreateRecord(milestone.Id).RewardClaimed;

        public void SetObjectiveProgressForTests(Milestone milestone, MilestoneObjective objective, int value)
        {
            if (milestone != null && objective != null) GetOrCreateRecord(milestone.Id).SetObjectiveProgress(objective.Id, value);
        }

        public void MarkCompletedForTests(Milestone milestone) { if (milestone != null) GetOrCreateRecord(milestone.Id).Completed = true; }
        public void MarkRewardClaimedForTests(Milestone milestone) { if (milestone != null) GetOrCreateRecord(milestone.Id).RewardClaimed = true; }

        public MilestoneProgressSaveData ToSaveData()
        {
            var records = new List<MilestoneRecordSaveData>(_records.Count);
            foreach (var pair in _records) records.Add(pair.Value.ToSaveData(pair.Key));
            var applied = new string[_appliedDayResultKeys.Count];
            _appliedDayResultKeys.CopyTo(applied);
            return new MilestoneProgressSaveData { SaveSlot = SaveSlot, PlayerId = PlayerId, Records = records.ToArray(), AppliedDayResultKeys = applied };
        }

        public static MilestoneProgress FromSaveData(MilestoneProgressSaveData saveData, IReadOnlyList<Milestone> milestones)
        {
            var progress = new MilestoneProgress(saveData != null ? saveData.SaveSlot : "default", saveData != null ? saveData.PlayerId : "player", milestones);
            if (saveData == null) return progress;
            if (saveData.Records != null)
            {
                for (int i = 0; i < saveData.Records.Length; i++)
                {
                    var record = saveData.Records[i];
                    if (record == null || string.IsNullOrEmpty(record.MilestoneId)) continue;
                    progress._records[record.MilestoneId] = MilestoneRecord.FromSaveData(record);
                }
            }
            if (saveData.AppliedDayResultKeys != null)
            {
                for (int i = 0; i < saveData.AppliedDayResultKeys.Length; i++) if (!string.IsNullOrEmpty(saveData.AppliedDayResultKeys[i])) progress._appliedDayResultKeys.Add(saveData.AppliedDayResultKeys[i]);
            }
            progress.EnsureRecords(milestones);
            return progress;
        }

        private void EnsureRecords(IReadOnlyList<Milestone> milestones)
        {
            if (milestones == null) return;
            for (int i = 0; i < milestones.Count; i++) if (milestones[i] != null && milestones[i].Id.Length > 0) GetOrCreateRecord(milestones[i].Id);
        }

        private MilestoneRecord GetOrCreateRecord(string milestoneId)
        {
            string key = milestoneId ?? string.Empty;
            if (!_records.TryGetValue(key, out var record))
            {
                record = new MilestoneRecord();
                _records[key] = record;
            }
            return record;
        }

        private bool PrerequisitesSatisfied(Milestone milestone)
        {
            var prerequisites = milestone.PrerequisiteMilestoneIds;
            for (int i = 0; i < prerequisites.Count; i++) if (!string.IsNullOrEmpty(prerequisites[i]) && !IsCompleted(prerequisites[i])) return false;
            return true;
        }

        private string[] CollectRecommendations(IReadOnlyList<Milestone> milestones)
        {
            var list = new List<string>();
            if (milestones == null) return list.ToArray();
            for (int i = 0; i < milestones.Count; i++)
            {
                var milestone = milestones[i];
                if (milestone == null || milestone.Id.Length == 0 || GetOrCreateRecord(milestone.Id).Completed || !PrerequisitesSatisfied(milestone)) continue;
                var routes = milestone.Routes;
                for (int j = 0; j < routes.Count; j++)
                {
                    string id = routes[j] != null ? routes[j].RecommendedActionId : string.Empty;
                    if (!string.IsNullOrEmpty(id) && !list.Contains(id)) list.Add(id);
                }
            }
            return list.ToArray();
        }

        private sealed class MilestoneRecord
        {
            private readonly Dictionary<string, int> _objectiveProgress = new Dictionary<string, int>(StringComparer.Ordinal);
            public bool Completed;
            public bool RewardClaimed;

            public int GetObjectiveProgress(string objectiveId) => !string.IsNullOrEmpty(objectiveId) && _objectiveProgress.TryGetValue(objectiveId, out int value) ? value : 0;

            public void SetObjectiveProgress(string objectiveId, int value)
            {
                if (!string.IsNullOrEmpty(objectiveId)) _objectiveProgress[objectiveId] = Math.Max(0, value);
            }

            public MilestoneRecordSaveData ToSaveData(string milestoneId)
            {
                var objectives = new List<MilestoneObjectiveProgressSaveData>(_objectiveProgress.Count);
                foreach (var pair in _objectiveProgress) objectives.Add(new MilestoneObjectiveProgressSaveData { ObjectiveId = pair.Key, Value = pair.Value });
                return new MilestoneRecordSaveData { MilestoneId = milestoneId, Completed = Completed, RewardClaimed = RewardClaimed, Objectives = objectives.ToArray() };
            }

            public static MilestoneRecord FromSaveData(MilestoneRecordSaveData saveData)
            {
                var record = new MilestoneRecord { Completed = saveData.Completed, RewardClaimed = saveData.RewardClaimed };
                if (saveData.Objectives != null) for (int i = 0; i < saveData.Objectives.Length; i++) record.SetObjectiveProgress(saveData.Objectives[i].ObjectiveId, saveData.Objectives[i].Value);
                return record;
            }
        }
    }

    /// <summary>세이브 파일 이름 규칙. 파일 읽고 쓰기는 실행 프로그램이 한다.</summary>
    public static class MilestoneProgressPersistence
    {
        public const string FileName = "milestone-progress.json";

        public static string FileNameFor(string playerId)
        {
            if (string.IsNullOrEmpty(playerId) || playerId == "player" || playerId == "local-player") return FileName;
            var chars = playerId.ToCharArray();
            for (int i = 0; i < chars.Length; i++) if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '-' && chars[i] != '_') chars[i] = '_';
            return "milestone-progress-" + new string(chars) + ".json";
        }
    }

    public readonly struct MilestoneDaySummary
    {
        public readonly string[] ProgressLines;
        public readonly string[] RecommendedActionIds;

        public MilestoneDaySummary(string[] progressLines, string[] recommendedActionIds)
        {
            ProgressLines = progressLines ?? Array.Empty<string>();
            RecommendedActionIds = recommendedActionIds ?? Array.Empty<string>();
        }

        /// <summary>바뀐 것마다 한 줄("&lt;id&gt; complete" 또는 "&lt;id&gt;:&lt;목표&gt; 전 -> 후"). 없으면 마일스톤마다 "&lt;id&gt; active".</summary>
        public static MilestoneDaySummary FromApplyResult(MilestoneApplyResult result, IReadOnlyList<Milestone> milestones)
        {
            var lines = new List<string>();
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            for (int i = 0; i < result.ProgressChanges.Length; i++)
            {
                var change = result.ProgressChanges[i];
                if (change.Completed) lines.Add(change.MilestoneId + " complete");
                else lines.Add(change.MilestoneId + ":" + change.ObjectiveId + " " + change.BeforeValue.ToString(inv) + " -> " + change.AfterValue.ToString(inv));
            }
            if (lines.Count == 0 && milestones != null)
            {
                for (int i = 0; i < milestones.Count; i++) if (milestones[i] != null) lines.Add(milestones[i].Id + " active");
            }
            return new MilestoneDaySummary(lines.ToArray(), result.RecommendedActionIds);
        }
    }
}
