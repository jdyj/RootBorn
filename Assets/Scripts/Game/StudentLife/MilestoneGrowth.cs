using System;
using System.Collections.Generic;
using Rootborn.Game.Common;
using Rootborn.Game.Save;
using UnityEngine;

namespace Rootborn.Game.StudentLife
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
            MilestoneId = string.IsNullOrEmpty(milestoneId) ? string.Empty : milestoneId;
            ObjectiveId = string.IsNullOrEmpty(objectiveId) ? string.Empty : objectiveId;
            BeforeValue = Mathf.Max(0, beforeValue);
            AfterValue = Mathf.Max(0, afterValue);
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

    public sealed class MilestoneProgress
    {
        private readonly Dictionary<string, MilestoneRecord> _records = new Dictionary<string, MilestoneRecord>();
        private readonly HashSet<string> _appliedDayResultKeys = new HashSet<string>();

        public MilestoneProgress(string saveSlot, string playerId, IReadOnlyList<MilestoneDefinition> milestones)
        {
            SaveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            PlayerId = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            EnsureRecords(milestones);
        }

        public string SaveSlot { get; }
        public string PlayerId { get; }

        public MilestoneApplyResult ApplyDayResult(IReadOnlyList<MilestoneDefinition> milestones, StudentLifeProgress studentLifeProgress, Inventory inventory, string dayResultKey)
        {
            EnsureRecords(milestones);
            if (string.IsNullOrEmpty(dayResultKey) || !_appliedDayResultKeys.Add(dayResultKey))
            {
                return new MilestoneApplyResult(Array.Empty<MilestoneProgressChange>(), CollectRecommendations(milestones));
            }

            var changes = new List<MilestoneProgressChange>();
            var context = new MilestoneEvaluationContext(studentLifeProgress, inventory, Array.Empty<string>());
            if (milestones != null)
            {
                for (int i = 0; i < milestones.Count; i++)
                {
                    var milestone = milestones[i];
                    if (milestone == null || string.IsNullOrEmpty(milestone.Id)) continue;
                    var record = GetOrCreateRecord(milestone.Id);
                    bool wasCompleted = record.Completed;
                    int completedCount = 0;
                    var objectives = milestone.Objectives ?? Array.Empty<MilestoneObjectiveBase>();
                    for (int j = 0; j < objectives.Length; j++)
                    {
                        var objective = objectives[j];
                        if (objective == null || string.IsNullOrEmpty(objective.Id)) continue;
                        var evaluation = objective.Evaluate(in context);
                        int before = record.GetObjectiveProgress(objective.Id);
                        int evaluatedCurrent = evaluation.Current;
                        if (evaluatedCurrent <= 0 && objective is OutsideSchoolLogMilestoneObjective && HasOutsideSchoolLog(studentLifeProgress))
                        {
                            evaluatedCurrent = evaluation.Required;
                        }
                        int after = Mathf.Max(before, evaluatedCurrent);
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
                    else if (wasCompleted)
                    {
                        record.Completed = true;
                    }
                }
            }

            return new MilestoneApplyResult(changes.ToArray(), CollectRecommendations(milestones));
        }

        public bool TryClaimReward(MilestoneDefinition milestone, Inventory inventory)
        {
            if (milestone == null || string.IsNullOrEmpty(milestone.Id)) return false;
            var record = GetOrCreateRecord(milestone.Id);
            if (!record.Completed || record.RewardClaimed) return false;
            var rewards = milestone.Rewards ?? Array.Empty<MilestoneRewardBase>();
            for (int i = 0; i < rewards.Length; i++)
            {
                var reward = rewards[i];
                if (reward != null && !reward.CanApply(inventory)) return false;
            }

            for (int i = 0; i < rewards.Length; i++) rewards[i]?.Apply(inventory);
            record.RewardClaimed = true;
            return true;
        }

        public int GetObjectiveProgress(MilestoneDefinition milestone, MilestoneObjectiveBase objective)
        {
            if (milestone == null || objective == null) return 0;
            return GetOrCreateRecord(milestone.Id).GetObjectiveProgress(objective.Id);
        }

        public bool IsCompleted(MilestoneDefinition milestone)
        {
            return milestone != null && GetOrCreateRecord(milestone.Id).Completed;
        }

        public bool IsRewardClaimed(MilestoneDefinition milestone)
        {
            return milestone != null && GetOrCreateRecord(milestone.Id).RewardClaimed;
        }

        public void SetObjectiveProgressForTests(MilestoneDefinition milestone, MilestoneObjectiveBase objective, int value)
        {
            if (milestone == null || objective == null) return;
            GetOrCreateRecord(milestone.Id).SetObjectiveProgress(objective.Id, value);
        }

        public void MarkCompletedForTests(MilestoneDefinition milestone)
        {
            if (milestone != null) GetOrCreateRecord(milestone.Id).Completed = true;
        }

        public void MarkRewardClaimedForTests(MilestoneDefinition milestone)
        {
            if (milestone != null) GetOrCreateRecord(milestone.Id).RewardClaimed = true;
        }

        public MilestoneProgressSaveData ToSaveData()
        {
            var records = new List<MilestoneRecordSaveData>();
            foreach (var pair in _records) records.Add(pair.Value.ToSaveData(pair.Key));
            var applied = new string[_appliedDayResultKeys.Count];
            _appliedDayResultKeys.CopyTo(applied);
            return new MilestoneProgressSaveData { SaveSlot = SaveSlot, PlayerId = PlayerId, Records = records.ToArray(), AppliedDayResultKeys = applied };
        }

        public static MilestoneProgress FromSaveData(MilestoneProgressSaveData saveData, IReadOnlyList<MilestoneDefinition> milestones)
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
                for (int i = 0; i < saveData.AppliedDayResultKeys.Length; i++)
                {
                    if (!string.IsNullOrEmpty(saveData.AppliedDayResultKeys[i])) progress._appliedDayResultKeys.Add(saveData.AppliedDayResultKeys[i]);
                }
            }

            progress.EnsureRecords(milestones);
            return progress;
        }

        private static bool HasOutsideSchoolLog(StudentLifeProgress progress)
        {
            if (progress == null) return false;
            return HasOutsideSchoolLog(progress.GetTodayResultLogIds()) || HasOutsideSchoolLog(progress.GetPreviousDayResultLogIds());
        }

        private static bool HasOutsideSchoolLog(string[] logs)
        {
            if (logs == null) return false;
            for (int i = 0; i < logs.Length; i++)
            {
                if (OutsideSchoolLogCodec.TryParseDelta(logs[i], out _)) return true;
            }

            return false;
        }
        private void EnsureRecords(IReadOnlyList<MilestoneDefinition> milestones)
        {
            if (milestones == null) return;
            for (int i = 0; i < milestones.Count; i++)
            {
                var milestone = milestones[i];
                if (milestone != null && !string.IsNullOrEmpty(milestone.Id)) GetOrCreateRecord(milestone.Id);
            }
        }

        private MilestoneRecord GetOrCreateRecord(string milestoneId)
        {
            if (string.IsNullOrEmpty(milestoneId)) milestoneId = string.Empty;
            if (!_records.TryGetValue(milestoneId, out var record))
            {
                record = new MilestoneRecord();
                _records[milestoneId] = record;
            }

            return record;
        }

        private static string[] CollectRecommendations(IReadOnlyList<MilestoneDefinition> milestones)
        {
            var list = new List<string>();
            if (milestones != null)
            {
                for (int i = 0; i < milestones.Count; i++)
                {
                    var routes = milestones[i] != null ? milestones[i].Routes : null;
                    if (routes == null) continue;
                    for (int j = 0; j < routes.Length; j++)
                    {
                        string id = routes[j] != null ? routes[j].RecommendedActionId : string.Empty;
                        if (!string.IsNullOrEmpty(id) && !list.Contains(id)) list.Add(id);
                    }
                }
            }

            return list.ToArray();
        }

        private sealed class MilestoneRecord
        {
            private readonly Dictionary<string, int> _objectiveProgress = new Dictionary<string, int>();

            public bool Completed;
            public bool RewardClaimed;

            public int GetObjectiveProgress(string objectiveId)
            {
                return !string.IsNullOrEmpty(objectiveId) && _objectiveProgress.TryGetValue(objectiveId, out int value) ? value : 0;
            }

            public void SetObjectiveProgress(string objectiveId, int value)
            {
                if (!string.IsNullOrEmpty(objectiveId)) _objectiveProgress[objectiveId] = Mathf.Max(0, value);
            }

            public MilestoneRecordSaveData ToSaveData(string milestoneId)
            {
                var objectives = new List<MilestoneObjectiveProgressSaveData>();
                foreach (var pair in _objectiveProgress) objectives.Add(new MilestoneObjectiveProgressSaveData { ObjectiveId = pair.Key, Value = pair.Value });
                return new MilestoneRecordSaveData { MilestoneId = milestoneId, Completed = Completed, RewardClaimed = RewardClaimed, Objectives = objectives.ToArray() };
            }

            public static MilestoneRecord FromSaveData(MilestoneRecordSaveData saveData)
            {
                var record = new MilestoneRecord { Completed = saveData.Completed, RewardClaimed = saveData.RewardClaimed };
                if (saveData.Objectives != null)
                {
                    for (int i = 0; i < saveData.Objectives.Length; i++)
                    {
                        var objective = saveData.Objectives[i];
                        record.SetObjectiveProgress(objective.ObjectiveId, objective.Value);
                    }
                }

                return record;
            }
        }
    }

    public static class MilestoneRegistryResolver
    {
        private static MilestoneDefinition[] _runtimeFallbackMilestones;

        public static MilestoneDefinition[] ResolveMilestones(Rootborn.Game.Common.GameDataRegistry registry)
        {
            if (registry != null && registry.Milestones != null && registry.Milestones.Length > 0)
            {
                return registry.Milestones;
            }

            var loadedRegistries = UnityEngine.Resources.FindObjectsOfTypeAll<Rootborn.Game.Common.GameDataRegistry>();
            for (int i = 0; i < loadedRegistries.Length; i++)
            {
                var loaded = loadedRegistries[i];
                if (loaded != null && loaded.Milestones != null && loaded.Milestones.Length > 0)
                {
                    return loaded.Milestones;
                }
            }

            var fallback = UnityEngine.Resources.Load<Rootborn.Game.Common.GameDataRegistry>("GameDataRegistry");
            if (fallback != null && fallback.Milestones != null && fallback.Milestones.Length > 0)
            {
                return fallback.Milestones;
            }

            return CreateRuntimeFallback(registry ?? fallback);
        }

        private static MilestoneDefinition[] CreateRuntimeFallback(Rootborn.Game.Common.GameDataRegistry registry)
        {
            if (_runtimeFallbackMilestones != null && _runtimeFallbackMilestones.Length > 0) return _runtimeFallbackMilestones;
            OutsideSchoolActivityDefinition activity = null;
            var outsideActivities = registry != null ? registry.OutsideSchoolActivities : null;
            if (outsideActivities != null && outsideActivities.Length > 0) activity = outsideActivities[0];
            if (activity == null)
            {
                var interactor = UnityEngine.Object.FindFirstObjectByType<OutsideSchoolActivityInteractor>(UnityEngine.FindObjectsInactive.Include);
                activity = interactor != null ? interactor.Activity : null;
            }
            string recommendedActionId = activity != null && !string.IsNullOrEmpty(activity.Id) ? activity.Id : "outside-school";

            var objective = ScriptableObject.CreateInstance<OutsideSchoolLogMilestoneObjective>();
            objective.ConfigureForTests("milestone.objective.any-outside-school-log", string.Empty, 1);
            var route = ScriptableObject.CreateInstance<MilestoneRouteDefinition>();
            route.ConfigureForTests("milestone.route.first-outside-school", objective, recommendedActionId);
            var milestone = ScriptableObject.CreateInstance<MilestoneDefinition>();
            milestone.ConfigureForTests("milestone.open-ended-town-growth", "milestone.open-ended-town-growth", new MilestoneObjectiveBase[] { objective }, Array.Empty<MilestoneRewardBase>(), new[] { route }, 1);
            _runtimeFallbackMilestones = new[] { milestone };
            return _runtimeFallbackMilestones;
        }    }
    public static class MilestoneProgressPersistence
    {
        private const string FileName = "milestone-progress.json";

        public static MilestoneProgress LoadOrCreate(string saveSlot, string playerId, IReadOnlyList<MilestoneDefinition> milestones)
        {
            string slot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            string json = new SaveService(slot).ReadJson(FileNameFor(playerId));
            if (string.IsNullOrEmpty(json))
            {
                return new MilestoneProgress(slot, playerId, milestones);
            }

            return MilestoneProgress.FromSaveData(JsonUtility.FromJson<MilestoneProgressSaveData>(json), milestones);
        }

        public static bool TryLoad(string saveSlot, string playerId, IReadOnlyList<MilestoneDefinition> milestones, out MilestoneProgress progress)
        {
            progress = null;
            string slot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            string json = new SaveService(slot).ReadJson(FileNameFor(playerId));
            if (string.IsNullOrEmpty(json)) return false;
            progress = MilestoneProgress.FromSaveData(JsonUtility.FromJson<MilestoneProgressSaveData>(json), milestones);
            return true;
        }

        public static void Save(MilestoneProgress progress)
        {
            if (progress == null) return;
            string json = JsonUtility.ToJson(progress.ToSaveData(), true);
            new SaveService(progress.SaveSlot).WriteJson(FileNameFor(progress.PlayerId), json);
        }

        public static string FileNameFor(string playerId)
        {
            return string.IsNullOrEmpty(playerId) || playerId == "player" || playerId == "local-player" ? FileName : "milestone-progress-" + Sanitize(playerId) + ".json";
        }

        private static string Sanitize(string value)
        {
            var chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '-' && chars[i] != '_') chars[i] = '_';
            }

            return new string(chars);
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

        public static MilestoneDaySummary FromApplyResult(MilestoneApplyResult result, IReadOnlyList<MilestoneDefinition> milestones)
        {
            var lines = new List<string>();
            for (int i = 0; i < result.ProgressChanges.Length; i++)
            {
                var change = result.ProgressChanges[i];
                if (change.Completed) lines.Add(change.MilestoneId + " complete");
                else lines.Add(change.MilestoneId + ":" + change.ObjectiveId + " " + change.BeforeValue + " -> " + change.AfterValue);
            }

            if (lines.Count == 0 && milestones != null)
            {
                for (int i = 0; i < milestones.Count; i++)
                {
                    if (milestones[i] != null) lines.Add(milestones[i].Id + " active");
                }
            }

            return new MilestoneDaySummary(lines.ToArray(), result.RecommendedActionIds);
        }
    }
}
