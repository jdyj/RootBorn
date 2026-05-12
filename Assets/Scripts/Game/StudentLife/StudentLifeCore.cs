using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    public enum LifeActivityCategory
    {
        School,
        Work,
        Hobby,
        Errand,
        Social,
        Rest,
        SelfStudy
    }

    public enum LifeActivityResultKind
    {
        Applied,
        InvalidRequest,
        InsufficientResources,
        RequirementFailed,
        DuplicateRequest
    }

    public enum StudentDayState
    {
        InProgress,
        ResultReady
    }

    public readonly struct LifeActivityResult
    {
        public readonly LifeActivityResultKind Kind;
        public readonly string SaveSlot;
        public readonly string PlayerId;
        public readonly string ActivityId;
        public readonly string RequestId;
        public readonly string ChoiceId;
        public readonly string[] ChangedTraitIds;

        public LifeActivityResult(LifeActivityResultKind kind, string saveSlot, string playerId, string activityId, string requestId)
        {
            Kind = kind;
            SaveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            PlayerId = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            ActivityId = string.IsNullOrEmpty(activityId) ? string.Empty : activityId;
            RequestId = string.IsNullOrEmpty(requestId) ? string.Empty : requestId;
            ChoiceId = string.Empty;
            ChangedTraitIds = Array.Empty<string>();
        }

        public LifeActivityResult(LifeActivityResultKind kind, string saveSlot, string playerId, string activityId, string requestId, string choiceId, string[] changedTraitIds)
            : this(kind, saveSlot, playerId, activityId, requestId)
        {
            ChoiceId = string.IsNullOrEmpty(choiceId) ? string.Empty : choiceId;
            ChangedTraitIds = changedTraitIds ?? Array.Empty<string>();
        }
    }

    public readonly struct StudentDaySummary
    {
        public readonly int DayNumber;
        public readonly string[] CompletedActivityIds;
        public readonly string[] ResultLogIds;
        public readonly string NextDayEntryPointId;
        public readonly string TutorialStageId;
        public readonly string NextObjectiveId;
        public readonly string NextGuideText;

        public StudentDaySummary(int dayNumber, string[] completedActivityIds, string[] resultLogIds, string nextDayEntryPointId)
            : this(dayNumber, completedActivityIds, resultLogIds, nextDayEntryPointId, string.Empty, string.Empty, string.Empty)
        {
        }

        public StudentDaySummary(int dayNumber, string[] completedActivityIds, string[] resultLogIds, string nextDayEntryPointId, string tutorialStageId, string nextObjectiveId, string nextGuideText)
        {
            DayNumber = Mathf.Max(1, dayNumber);
            CompletedActivityIds = completedActivityIds ?? Array.Empty<string>();
            ResultLogIds = resultLogIds ?? Array.Empty<string>();
            NextDayEntryPointId = string.IsNullOrEmpty(nextDayEntryPointId) ? string.Empty : nextDayEntryPointId;
            TutorialStageId = string.IsNullOrEmpty(tutorialStageId) ? string.Empty : tutorialStageId;
            NextObjectiveId = string.IsNullOrEmpty(nextObjectiveId) ? string.Empty : nextObjectiveId;
            NextGuideText = string.IsNullOrEmpty(nextGuideText) ? string.Empty : nextGuideText;
        }
    }

    [Serializable]
    public sealed class StudentLifeProgressSaveData
    {
        public string SaveSlot;
        public string PlayerId;
        public int Energy;
        public int Focus;
        public int Stress;
        public int TimeMinutes;
        public int CurrentDay = 1;
        public string DayState = StudentDayState.InProgress.ToString();
        public int LastSettledDay;
        public string NextDayEntryPointId;
        public string LastActivityId;
        public string TutorialStageId;
        public string NextObjectiveId;
        public string NextGuideText;
        public StatEntry[] Traits = Array.Empty<StatEntry>();
        public StatEntry[] Skills = Array.Empty<StatEntry>();
        public StatEntry[] Relationships = Array.Empty<StatEntry>();
        public StatEntry[] StudentConditionStatuses = Array.Empty<StatEntry>();
        public string[] CareerHintIds = Array.Empty<string>();
        public string[] AppliedRequestIds = Array.Empty<string>();
        public string[] ActivityLogIds = Array.Empty<string>();
        public string[] TodayActivityIds = Array.Empty<string>();
        public string[] TodayResultLogIds = Array.Empty<string>();
        public string[] PreviousDayActivityIds = Array.Empty<string>();
        public string[] PreviousDayResultLogIds = Array.Empty<string>();

        [Serializable]
        public struct StatEntry
        {
            public string Id;
            public int Value;
        }
    }

    public sealed class StudentLifeProgress
    {
        private readonly Dictionary<string, int> _traitValues = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _skillValues = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _relationshipValues = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _statusValues = new Dictionary<string, int>();
        private readonly HashSet<string> _careerHintIds = new HashSet<string>();
        private readonly HashSet<string> _appliedRequestIds = new HashSet<string>();
        private readonly List<string> _activityLogIds = new List<string>();
        private readonly List<string> _todayActivityIds = new List<string>();
        private readonly List<string> _todayResultLogIds = new List<string>();
        private readonly List<string> _previousDayActivityIds = new List<string>();
        private readonly List<string> _previousDayResultLogIds = new List<string>();

        public StudentLifeProgress(string saveSlot, string playerId, int energy, int focus, int stress = 0, int timeMinutes = 0)
        {
            SaveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            PlayerId = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            Energy = Mathf.Max(0, energy);
            Focus = Mathf.Max(0, focus);
            Stress = Mathf.Max(0, stress);
            TimeMinutes = Mathf.Max(0, timeMinutes);
            CurrentDay = 1;
            DayState = StudentDayState.InProgress;
            LastSettledDay = 0;
            NextDayEntryPointId = string.Empty;
            LastActivityId = string.Empty;
            TutorialStageId = string.Empty;
            NextObjectiveId = string.Empty;
            NextGuideText = string.Empty;
        }

        public string SaveSlot { get; }
        public string PlayerId { get; }
        public int Energy { get; private set; }
        public int Focus { get; private set; }
        public int Stress { get; private set; }
        public int TimeMinutes { get; private set; }
        public int CurrentDay { get; private set; }
        public StudentDayState DayState { get; private set; }
        public int LastSettledDay { get; private set; }
        public string NextDayEntryPointId { get; private set; }
        public string LastActivityId { get; private set; }
        public string TutorialStageId { get; private set; }
        public string NextObjectiveId { get; private set; }
        public string NextGuideText { get; private set; }

        public bool CanSpend(int energyCost, int focusCost)
        {
            return Energy >= Mathf.Max(0, energyCost) && Focus >= Mathf.Max(0, focusCost);
        }

        public void Spend(int timeCostMinutes, int energyCost, int focusCost, int stressDelta)
        {
            Energy = Mathf.Max(0, Energy - Mathf.Max(0, energyCost));
            Focus = Mathf.Max(0, Focus - Mathf.Max(0, focusCost));
            Stress = Mathf.Max(0, Stress + stressDelta);
            TimeMinutes = Mathf.Max(0, TimeMinutes + Mathf.Max(0, timeCostMinutes));
        }

        public void RestoreEnergyForDay(int value) => Energy = Mathf.Max(0, value);
        public void RestoreFocusForDay(int value) => Focus = Mathf.Max(0, value);

        public bool SetTutorialStage(TutorialStageDefinition stage, string nextObjectiveId)
        {
            if (stage == null || string.IsNullOrEmpty(stage.Id)) return false;
            string objective = string.IsNullOrEmpty(nextObjectiveId) ? string.Empty : nextObjectiveId;
            string guideText = string.IsNullOrEmpty(stage.GuideText) ? string.Empty : stage.GuideText;
            bool changed = TutorialStageId != stage.Id || NextObjectiveId != objective || NextGuideText != guideText;
            TutorialStageId = stage.Id;
            NextObjectiveId = objective;
            NextGuideText = guideText;
            return changed;
        }

        public void SetTutorialStageForTests(string tutorialStageId) => TutorialStageId = string.IsNullOrEmpty(tutorialStageId) ? string.Empty : tutorialStageId;

        public void AddTrait(TraitDefinition trait, int delta) => AddValue(_traitValues, GetId(trait), delta);
        public int GetTraitValue(TraitDefinition trait) => GetValue(_traitValues, GetId(trait));
        public int GetTraitValueById(string traitId) => GetValue(_traitValues, traitId);
        public string[] GetTraitIds() => KeysToArray(_traitValues);

        public void AddSkill(SkillDefinition skill, int delta) => AddValue(_skillValues, GetId(skill), delta);
        public int GetSkillValue(SkillDefinition skill) => GetValue(_skillValues, GetId(skill));
        public int GetSkillValueById(string skillId) => GetValue(_skillValues, skillId);
        public string[] GetSkillIds() => KeysToArray(_skillValues);

        public void AddRelationship(RelationshipDefinition relationship, int delta, string sourceId) => AddValue(_relationshipValues, GetId(relationship), delta);
        public void AddRelationshipForTests(RelationshipDefinition relationship, int delta) => AddRelationship(relationship, delta, string.Empty);
        public int GetRelationshipValue(RelationshipDefinition relationship) => GetValue(_relationshipValues, GetId(relationship));
        public int GetRelationshipValueById(string relationshipId) => GetValue(_relationshipValues, relationshipId);
        public string[] GetRelationshipIds() => KeysToArray(_relationshipValues);

        public void AddStatus(StatusDefinition status, int delta, string sourceId) => AddValue(_statusValues, GetId(status), delta);
        public void AddStatusForTests(StatusDefinition status, int delta) => AddStatus(status, delta, string.Empty);
        public int GetStatusValue(StatusDefinition status) => GetValue(_statusValues, GetId(status));
        public int GetStatusValueById(string statusId) => GetValue(_statusValues, statusId);
        public string[] GetStatusIds() => KeysToArray(_statusValues);

        public void UnlockCareerHint(CareerDefinition career)
        {
            string key = GetId(career);
            if (!string.IsNullOrEmpty(key)) _careerHintIds.Add(key);
        }

        public bool IsCareerHintUnlocked(CareerDefinition career) => _careerHintIds.Contains(GetId(career));
        public string[] GetCareerHintIds() => ToArray(_careerHintIds);
        public bool MarkRequestApplied(string requestId) => !string.IsNullOrEmpty(requestId) && _appliedRequestIds.Add(requestId);
        public bool HasAppliedRequest(string requestId) => !string.IsNullOrEmpty(requestId) && _appliedRequestIds.Contains(requestId);

        public void RecordActivityCompleted(string activityId) => RecordActivityCompleted(activityId, Array.Empty<string>());

        public void RecordActivityCompleted(string activityId, string[] resultLogIds)
        {
            if (string.IsNullOrEmpty(activityId)) return;
            LastActivityId = activityId;
            _activityLogIds.Add(activityId);
            _todayActivityIds.Add(activityId);
            AddOrderedUnique(_todayResultLogIds, resultLogIds);
        }

        public string[] GetActivityLogIds() => _activityLogIds.ToArray();
        public string[] GetTodayActivityIds() => _todayActivityIds.ToArray();
        public string[] GetTodayResultLogIds() => _todayResultLogIds.ToArray();
        public string[] GetPreviousDayActivityIds() => _previousDayActivityIds.ToArray();
        public string[] GetPreviousDayResultLogIds() => _previousDayResultLogIds.ToArray();
        public string[] GetPreviousDayRelationshipDeltas() => FilterLogs(_previousDayResultLogIds, "+relationship.");
        public string[] GetPreviousDayStatusDeltas() => FilterLogs(_previousDayResultLogIds, "+status.");

        public bool TryEndDay(DayEndRuleDefinition rules, out StudentDaySummary summary)
        {
            summary = BuildCurrentDaySummary(rules);
            if (DayState != StudentDayState.InProgress || LastSettledDay == CurrentDay) return false;
            rules?.Apply(this);
            _previousDayActivityIds.Clear();
            _previousDayActivityIds.AddRange(_todayActivityIds);
            _previousDayResultLogIds.Clear();
            _previousDayResultLogIds.AddRange(_todayResultLogIds);
            NextDayEntryPointId = rules != null ? rules.NextDayEntryPointId : string.Empty;
            LastSettledDay = CurrentDay;
            DayState = StudentDayState.ResultReady;
            summary = BuildCurrentDaySummary(rules);
            return true;
        }

        public bool TryStartNextDay()
        {
            if (DayState != StudentDayState.ResultReady) return false;
            CurrentDay = Mathf.Max(1, CurrentDay + 1);
            DayState = StudentDayState.InProgress;
            TimeMinutes = 8 * 60;
            _todayActivityIds.Clear();
            _todayResultLogIds.Clear();
            return true;
        }

        private StudentDaySummary BuildCurrentDaySummary(DayEndRuleDefinition rules)
        {
            string entry = rules != null ? rules.NextDayEntryPointId : NextDayEntryPointId;
            return new StudentDaySummary(CurrentDay, _todayActivityIds.ToArray(), _todayResultLogIds.ToArray(), entry, TutorialStageId, NextObjectiveId, NextGuideText);
        }

        public StudentLifeProgressSaveData ToSaveData()
        {
            return new StudentLifeProgressSaveData
            {
                SaveSlot = SaveSlot,
                PlayerId = PlayerId,
                Energy = Energy,
                Focus = Focus,
                Stress = Stress,
                TimeMinutes = TimeMinutes,
                CurrentDay = CurrentDay,
                DayState = DayState.ToString(),
                LastSettledDay = LastSettledDay,
                NextDayEntryPointId = NextDayEntryPointId,
                LastActivityId = LastActivityId,
                TutorialStageId = TutorialStageId,
                NextObjectiveId = NextObjectiveId,
                NextGuideText = NextGuideText,
                Traits = ToEntries(_traitValues),
                Skills = ToEntries(_skillValues),
                Relationships = ToEntries(_relationshipValues),
                StudentConditionStatuses = ToEntries(_statusValues),
                CareerHintIds = ToArray(_careerHintIds),
                AppliedRequestIds = ToArray(_appliedRequestIds),
                ActivityLogIds = _activityLogIds.ToArray(),
                TodayActivityIds = _todayActivityIds.ToArray(),
                TodayResultLogIds = _todayResultLogIds.ToArray(),
                PreviousDayActivityIds = _previousDayActivityIds.ToArray(),
                PreviousDayResultLogIds = _previousDayResultLogIds.ToArray(),
            };
        }

        public static StudentLifeProgress FromSaveData(StudentLifeProgressSaveData saveData, IReadOnlyList<TraitDefinition> traits, IReadOnlyList<SkillDefinition> skills, IReadOnlyList<CareerDefinition> careers)
        {
            return FromSaveData(saveData, traits, skills, careers, null, null);
        }

        public static StudentLifeProgress FromSaveData(StudentLifeProgressSaveData saveData, IReadOnlyList<TraitDefinition> traits, IReadOnlyList<SkillDefinition> skills, IReadOnlyList<CareerDefinition> careers, IReadOnlyList<RelationshipDefinition> relationships, IReadOnlyList<StatusDefinition> statuses)
        {
            if (saveData == null) return new StudentLifeProgress("default", "player", 0, 0);
            var progress = new StudentLifeProgress(saveData.SaveSlot, saveData.PlayerId, saveData.Energy, saveData.Focus, saveData.Stress, saveData.TimeMinutes);
            progress.CurrentDay = Mathf.Max(1, saveData.CurrentDay <= 0 ? 1 : saveData.CurrentDay);
            progress.DayState = ParseDayState(saveData.DayState);
            progress.LastSettledDay = Mathf.Max(0, saveData.LastSettledDay);
            progress.NextDayEntryPointId = string.IsNullOrEmpty(saveData.NextDayEntryPointId) ? string.Empty : saveData.NextDayEntryPointId;
            progress.LastActivityId = string.IsNullOrEmpty(saveData.LastActivityId) ? string.Empty : saveData.LastActivityId;
            progress.TutorialStageId = string.IsNullOrEmpty(saveData.TutorialStageId) ? string.Empty : saveData.TutorialStageId;
            progress.NextObjectiveId = string.IsNullOrEmpty(saveData.NextObjectiveId) ? string.Empty : saveData.NextObjectiveId;
            progress.NextGuideText = string.IsNullOrEmpty(saveData.NextGuideText) ? string.Empty : saveData.NextGuideText;
            RestoreEntries(progress._traitValues, saveData.Traits);
            RestoreEntries(progress._skillValues, saveData.Skills);
            RestoreEntries(progress._relationshipValues, saveData.Relationships);
            RestoreEntries(progress._statusValues, saveData.StudentConditionStatuses);
            RestoreIds(progress._careerHintIds, saveData.CareerHintIds);
            RestoreStrings(progress._appliedRequestIds, saveData.AppliedRequestIds);
            RestoreOrderedStrings(progress._activityLogIds, saveData.ActivityLogIds);
            RestoreOrderedStrings(progress._todayActivityIds, saveData.TodayActivityIds);
            RestoreOrderedStrings(progress._todayResultLogIds, saveData.TodayResultLogIds);
            RestoreOrderedStrings(progress._previousDayActivityIds, saveData.PreviousDayActivityIds);
            RestoreOrderedStrings(progress._previousDayResultLogIds, saveData.PreviousDayResultLogIds);
            return progress;
        }

        private static StudentDayState ParseDayState(string value) => Enum.TryParse(value, out StudentDayState state) ? state : StudentDayState.InProgress;
        private static string GetId(TraitDefinition trait) => trait == null ? string.Empty : trait.Id;
        private static string GetId(SkillDefinition skill) => skill == null ? string.Empty : skill.Id;
        private static string GetId(CareerDefinition career) => career == null ? string.Empty : career.Id;
        private static string GetId(RelationshipDefinition relationship) => relationship == null ? string.Empty : relationship.Id;
        private static string GetId(StatusDefinition status) => status == null ? string.Empty : status.Id;

        private static void AddValue(Dictionary<string, int> values, string key, int delta)
        {
            if (string.IsNullOrEmpty(key) || delta == 0) return;
            values.TryGetValue(key, out int current);
            values[key] = Mathf.Max(0, current + delta);
        }

        private static int GetValue(Dictionary<string, int> values, string key) => !string.IsNullOrEmpty(key) && values.TryGetValue(key, out int value) ? value : 0;
        private static string[] KeysToArray(Dictionary<string, int> values)
        {
            var result = new string[values.Count];
            values.Keys.CopyTo(result, 0);
            return result;
        }

        private static StudentLifeProgressSaveData.StatEntry[] ToEntries(Dictionary<string, int> values)
        {
            var entries = new StudentLifeProgressSaveData.StatEntry[values.Count];
            int index = 0;
            foreach (var pair in values) entries[index++] = new StudentLifeProgressSaveData.StatEntry { Id = pair.Key, Value = pair.Value };
            return entries;
        }

        private static string[] ToArray(HashSet<string> values)
        {
            var result = new string[values.Count];
            values.CopyTo(result);
            return result;
        }

        private static void RestoreEntries(Dictionary<string, int> target, StudentLifeProgressSaveData.StatEntry[] entries)
        {
            if (entries == null) return;
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (!string.IsNullOrEmpty(entry.Id)) target[entry.Id] = Mathf.Max(0, entry.Value);
            }
        }

        private static void RestoreIds(HashSet<string> target, string[] ids)
        {
            if (ids == null) return;
            for (int i = 0; i < ids.Length; i++) if (!string.IsNullOrEmpty(ids[i])) target.Add(ids[i]);
        }

        private static void RestoreStrings(HashSet<string> target, string[] values)
        {
            if (values == null) return;
            for (int i = 0; i < values.Length; i++) if (!string.IsNullOrEmpty(values[i])) target.Add(values[i]);
        }

        private static void RestoreOrderedStrings(List<string> target, string[] values)
        {
            if (values == null) return;
            for (int i = 0; i < values.Length; i++) if (!string.IsNullOrEmpty(values[i])) target.Add(values[i]);
        }

        private static void AddOrderedUnique(List<string> target, string[] values)
        {
            if (values == null) return;
            for (int i = 0; i < values.Length; i++) if (!string.IsNullOrEmpty(values[i]) && !target.Contains(values[i])) target.Add(values[i]);
        }

        private static string[] FilterLogs(List<string> logs, string marker)
        {
            var result = new List<string>();
            for (int i = 0; i < logs.Count; i++) if (!string.IsNullOrEmpty(logs[i]) && logs[i].Contains(marker)) result.Add(logs[i]);
            return result.ToArray();
        }
    }

    public abstract class StudentLifeDefinitionBase : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayNameKey;

        public string Id => string.IsNullOrEmpty(_id) ? name : _id;
        public string DisplayNameKey => string.IsNullOrEmpty(_displayNameKey) ? Id : _displayNameKey;

        public void ConfigureForTests(string id, string displayNameKey)
        {
            _id = id;
            _displayNameKey = displayNameKey;
        }
    }

    public abstract class CareerUnlockRequirementBase : ScriptableObject
    {
        public abstract bool IsSatisfied(StudentLifeProgress progress);
    }

    public abstract class LifeActivityRequirementBase : ScriptableObject
    {
        public abstract bool IsSatisfied(StudentLifeProgress progress);
    }

    public abstract class LifeActivityEffectBase : ScriptableObject
    {
        public abstract void Apply(StudentLifeProgress progress);
    }

    public sealed class LifeActivityRunner
    {
        public bool TryPerform(LifeActivityDefinition activity, StudentLifeProgress progress, string requestId, out LifeActivityResult result)
        {
            string saveSlot = progress == null ? "default" : progress.SaveSlot;
            string playerId = progress == null ? "player" : progress.PlayerId;
            string activityId = activity == null ? string.Empty : activity.Id;
            result = new LifeActivityResult(LifeActivityResultKind.InvalidRequest, saveSlot, playerId, activityId, requestId);
            if (activity == null || progress == null || string.IsNullOrEmpty(requestId)) return false;
            if (progress.HasAppliedRequest(requestId))
            {
                result = new LifeActivityResult(LifeActivityResultKind.DuplicateRequest, progress.SaveSlot, progress.PlayerId, activity.Id, requestId);
                return false;
            }
            if (!progress.CanSpend(activity.EnergyCost, activity.FocusCost))
            {
                result = new LifeActivityResult(LifeActivityResultKind.InsufficientResources, progress.SaveSlot, progress.PlayerId, activity.Id, requestId);
                return false;
            }
            if (!activity.HasSatisfiedRequirements(progress))
            {
                result = new LifeActivityResult(LifeActivityResultKind.RequirementFailed, progress.SaveSlot, progress.PlayerId, activity.Id, requestId);
                return false;
            }
            var before = StudentLifeDeltaSnapshot.Capture(progress);
            progress.Spend(activity.TimeCostMinutes, activity.EnergyCost, activity.FocusCost, activity.StressDelta);
            activity.ApplyEffects(progress);
            progress.MarkRequestApplied(requestId);
            progress.RecordActivityCompleted(activity.Id, before.BuildResultLogs(activity.Id, progress));
            result = new LifeActivityResult(LifeActivityResultKind.Applied, progress.SaveSlot, progress.PlayerId, activity.Id, requestId);
            return true;
        }

        public bool TryPerformChoice(LifeActivityDefinition activity, LifeChoiceDefinition choice, StudentLifeProgress progress, string requestId, out LifeActivityResult result)
        {
            string saveSlot = progress == null ? "default" : progress.SaveSlot;
            string playerId = progress == null ? "player" : progress.PlayerId;
            string activityId = activity == null ? string.Empty : activity.Id;
            string choiceId = choice == null ? string.Empty : choice.Id;
            result = new LifeActivityResult(LifeActivityResultKind.InvalidRequest, saveSlot, playerId, activityId, requestId, choiceId, Array.Empty<string>());
            if (activity == null || choice == null || progress == null || string.IsNullOrEmpty(requestId)) return false;
            if (progress.HasAppliedRequest(requestId))
            {
                result = new LifeActivityResult(LifeActivityResultKind.DuplicateRequest, progress.SaveSlot, progress.PlayerId, activity.Id, requestId, choice.Id, Array.Empty<string>());
                return false;
            }
            if (!progress.CanSpend(activity.EnergyCost, activity.FocusCost))
            {
                result = new LifeActivityResult(LifeActivityResultKind.InsufficientResources, progress.SaveSlot, progress.PlayerId, activity.Id, requestId, choice.Id, Array.Empty<string>());
                return false;
            }
            if (!activity.HasSatisfiedRequirements(progress))
            {
                result = new LifeActivityResult(LifeActivityResultKind.RequirementFailed, progress.SaveSlot, progress.PlayerId, activity.Id, requestId, choice.Id, Array.Empty<string>());
                return false;
            }
            var before = StudentLifeDeltaSnapshot.Capture(progress);
            progress.Spend(activity.TimeCostMinutes, activity.EnergyCost, activity.FocusCost, activity.StressDelta);
            activity.ApplyEffects(progress);
            choice.ApplyEffects(progress);
            progress.MarkRequestApplied(requestId);
            string[] logs = before.BuildResultLogs(activity.Id, progress);
            progress.RecordActivityCompleted(activity.Id, logs);
            string[] changedTraitIds = before.BuildChangedTraitIds(progress);
            result = new LifeActivityResult(LifeActivityResultKind.Applied, progress.SaveSlot, progress.PlayerId, activity.Id, requestId, choice.Id, changedTraitIds);
            return true;
        }

        private sealed class StudentLifeDeltaSnapshot
        {
            private readonly Dictionary<string, int> _traits = new Dictionary<string, int>();
            private readonly Dictionary<string, int> _skills = new Dictionary<string, int>();
            private readonly Dictionary<string, int> _relationships = new Dictionary<string, int>();
            private readonly Dictionary<string, int> _statuses = new Dictionary<string, int>();
            private readonly HashSet<string> _careers = new HashSet<string>();

            public static StudentLifeDeltaSnapshot Capture(StudentLifeProgress progress)
            {
                var snapshot = new StudentLifeDeltaSnapshot();
                CopyValues(snapshot._traits, progress.GetTraitIds(), progress.GetTraitValueById);
                CopyValues(snapshot._skills, progress.GetSkillIds(), progress.GetSkillValueById);
                CopyValues(snapshot._relationships, progress.GetRelationshipIds(), progress.GetRelationshipValueById);
                CopyValues(snapshot._statuses, progress.GetStatusIds(), progress.GetStatusValueById);
                string[] careerIds = progress.GetCareerHintIds();
                for (int i = 0; i < careerIds.Length; i++) snapshot._careers.Add(careerIds[i]);
                return snapshot;
            }

            public string[] BuildChangedTraitIds(StudentLifeProgress progress)
            {
                var changed = new List<string>();
                string[] afterIds = progress.GetTraitIds();
                for (int i = 0; i < afterIds.Length; i++)
                {
                    string id = afterIds[i];
                    _traits.TryGetValue(id, out int before);
                    if (progress.GetTraitValueById(id) != before) changed.Add(id);
                }
                return changed.ToArray();
            }

            public string[] BuildResultLogs(string activityId, StudentLifeProgress progress)
            {
                var logs = new List<string>();
                string prefix = string.IsNullOrEmpty(activityId) ? "activity" : activityId;
                AddDeltaLogs(logs, prefix, progress.GetTraitIds(), progress.GetTraitValueById, _traits);
                AddDeltaLogs(logs, prefix, progress.GetSkillIds(), progress.GetSkillValueById, _skills);
                AddDeltaLogs(logs, prefix, progress.GetRelationshipIds(), progress.GetRelationshipValueById, _relationships);
                AddDeltaLogs(logs, prefix, progress.GetStatusIds(), progress.GetStatusValueById, _statuses);
                string[] careerIds = progress.GetCareerHintIds();
                for (int i = 0; i < careerIds.Length; i++) if (!_careers.Contains(careerIds[i])) logs.Add(prefix + ":unlock=" + careerIds[i]);
                return logs.ToArray();
            }

            private static void CopyValues(Dictionary<string, int> target, string[] ids, Func<string, int> getter)
            {
                for (int i = 0; i < ids.Length; i++) target[ids[i]] = getter(ids[i]);
            }

            private static void AddDeltaLogs(List<string> logs, string prefix, string[] ids, Func<string, int> getter, Dictionary<string, int> beforeValues)
            {
                for (int i = 0; i < ids.Length; i++)
                {
                    string id = ids[i];
                    beforeValues.TryGetValue(id, out int before);
                    int delta = getter(id) - before;
                    if (delta != 0) logs.Add(prefix + ":+" + id + "=" + delta.ToString());
                }
            }
        }
    }

    public sealed class CareerPracticeRunner
    {
        private readonly LifeActivityRunner _activityRunner = new LifeActivityRunner();

        public bool TryPerform(CareerPracticeDefinition practice, StudentLifeProgress progress, string requestId, out LifeActivityResult result)
        {
            if (practice == null)
            {
                string saveSlot = progress == null ? "default" : progress.SaveSlot;
                string playerId = progress == null ? "player" : progress.PlayerId;
                result = new LifeActivityResult(LifeActivityResultKind.InvalidRequest, saveSlot, playerId, string.Empty, requestId);
                return false;
            }
            return _activityRunner.TryPerform(practice.Activity, progress, requestId, out result);
        }
    }
}
