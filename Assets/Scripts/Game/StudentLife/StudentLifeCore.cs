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

    public readonly struct LifeActivityResult
    {
        public readonly LifeActivityResultKind Kind;
        public readonly string SaveSlot;
        public readonly string PlayerId;
        public readonly string ActivityId;
        public readonly string RequestId;

        public LifeActivityResult(
            LifeActivityResultKind kind,
            string saveSlot,
            string playerId,
            string activityId,
            string requestId)
        {
            Kind = kind;
            SaveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            PlayerId = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            ActivityId = string.IsNullOrEmpty(activityId) ? string.Empty : activityId;
            RequestId = string.IsNullOrEmpty(requestId) ? string.Empty : requestId;
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
        public StatEntry[] Traits = Array.Empty<StatEntry>();
        public StatEntry[] Skills = Array.Empty<StatEntry>();
        public string[] CareerHintIds = Array.Empty<string>();
        public string[] AppliedRequestIds = Array.Empty<string>();

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
        private readonly HashSet<string> _careerHintIds = new HashSet<string>();
        private readonly HashSet<string> _appliedRequestIds = new HashSet<string>();

        public StudentLifeProgress(string saveSlot, string playerId, int energy, int focus, int stress = 0, int timeMinutes = 0)
        {
            SaveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            PlayerId = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            Energy = Mathf.Max(0, energy);
            Focus = Mathf.Max(0, focus);
            Stress = Mathf.Max(0, stress);
            TimeMinutes = Mathf.Max(0, timeMinutes);
        }

        public string SaveSlot { get; }
        public string PlayerId { get; }
        public int Energy { get; private set; }
        public int Focus { get; private set; }
        public int Stress { get; private set; }
        public int TimeMinutes { get; private set; }

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

        public void AddTrait(TraitDefinition trait, int delta)
        {
            string key = GetId(trait);
            if (string.IsNullOrEmpty(key) || delta == 0)
            {
                return;
            }

            _traitValues.TryGetValue(key, out int current);
            _traitValues[key] = Mathf.Max(0, current + delta);
        }

        public int GetTraitValue(TraitDefinition trait)
        {
            return _traitValues.TryGetValue(GetId(trait), out int value) ? value : 0;
        }

        public void AddSkill(SkillDefinition skill, int delta)
        {
            string key = GetId(skill);
            if (string.IsNullOrEmpty(key) || delta == 0)
            {
                return;
            }

            _skillValues.TryGetValue(key, out int current);
            _skillValues[key] = Mathf.Max(0, current + delta);
        }

        public int GetSkillValue(SkillDefinition skill)
        {
            return _skillValues.TryGetValue(GetId(skill), out int value) ? value : 0;
        }

        public void UnlockCareerHint(CareerDefinition career)
        {
            string key = GetId(career);
            if (!string.IsNullOrEmpty(key))
            {
                _careerHintIds.Add(key);
            }
        }

        public bool IsCareerHintUnlocked(CareerDefinition career)
        {
            return _careerHintIds.Contains(GetId(career));
        }

        public bool MarkRequestApplied(string requestId)
        {
            return !string.IsNullOrEmpty(requestId) && _appliedRequestIds.Add(requestId);
        }

        public bool HasAppliedRequest(string requestId)
        {
            return !string.IsNullOrEmpty(requestId) && _appliedRequestIds.Contains(requestId);
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
                Traits = ToEntries(_traitValues),
                Skills = ToEntries(_skillValues),
                CareerHintIds = ToArray(_careerHintIds),
                AppliedRequestIds = ToArray(_appliedRequestIds),
            };
        }

        public static StudentLifeProgress FromSaveData(
            StudentLifeProgressSaveData saveData,
            IReadOnlyList<TraitDefinition> traits,
            IReadOnlyList<SkillDefinition> skills,
            IReadOnlyList<CareerDefinition> careers)
        {
            if (saveData == null)
            {
                return new StudentLifeProgress("default", "player", 0, 0);
            }

            var progress = new StudentLifeProgress(
                saveData.SaveSlot,
                saveData.PlayerId,
                saveData.Energy,
                saveData.Focus,
                saveData.Stress,
                saveData.TimeMinutes);

            RestoreEntries(progress._traitValues, saveData.Traits, traits);
            RestoreEntries(progress._skillValues, saveData.Skills, skills);
            RestoreIds(progress._careerHintIds, saveData.CareerHintIds, careers);
            RestoreStrings(progress._appliedRequestIds, saveData.AppliedRequestIds);
            return progress;
        }

        private static string GetId(TraitDefinition trait) => trait == null ? string.Empty : trait.Id;
        private static string GetId(SkillDefinition skill) => skill == null ? string.Empty : skill.Id;
        private static string GetId(CareerDefinition career) => career == null ? string.Empty : career.Id;

        private static StudentLifeProgressSaveData.StatEntry[] ToEntries(Dictionary<string, int> values)
        {
            var entries = new StudentLifeProgressSaveData.StatEntry[values.Count];
            int index = 0;
            foreach (var pair in values)
            {
                entries[index++] = new StudentLifeProgressSaveData.StatEntry { Id = pair.Key, Value = pair.Value };
            }

            return entries;
        }

        private static string[] ToArray(HashSet<string> values)
        {
            var result = new string[values.Count];
            values.CopyTo(result);
            return result;
        }

        private static void RestoreEntries<TDefinition>(
            Dictionary<string, int> target,
            StudentLifeProgressSaveData.StatEntry[] entries,
            IReadOnlyList<TDefinition> definitions)
            where TDefinition : StudentLifeDefinitionBase
        {
            if (entries == null || definitions == null)
            {
                return;
            }

            var knownIds = new HashSet<string>();
            for (int i = 0; i < definitions.Count; i++)
            {
                if (definitions[i] != null && !string.IsNullOrEmpty(definitions[i].Id))
                {
                    knownIds.Add(definitions[i].Id);
                }
            }

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (!string.IsNullOrEmpty(entry.Id) && knownIds.Contains(entry.Id))
                {
                    target[entry.Id] = Mathf.Max(0, entry.Value);
                }
            }
        }

        private static void RestoreIds<TDefinition>(HashSet<string> target, string[] ids, IReadOnlyList<TDefinition> definitions)
            where TDefinition : StudentLifeDefinitionBase
        {
            if (ids == null || definitions == null)
            {
                return;
            }

            var knownIds = new HashSet<string>();
            for (int i = 0; i < definitions.Count; i++)
            {
                if (definitions[i] != null && !string.IsNullOrEmpty(definitions[i].Id))
                {
                    knownIds.Add(definitions[i].Id);
                }
            }

            for (int i = 0; i < ids.Length; i++)
            {
                if (!string.IsNullOrEmpty(ids[i]) && knownIds.Contains(ids[i]))
                {
                    target.Add(ids[i]);
                }
            }
        }

        private static void RestoreStrings(HashSet<string> target, string[] values)
        {
            if (values == null)
            {
                return;
            }

            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrEmpty(values[i]))
                {
                    target.Add(values[i]);
                }
            }
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

    [CreateAssetMenu(fileName = "Trait_New", menuName = "Rootborn/Student Life/Trait")]
    public sealed class TraitDefinition : StudentLifeDefinitionBase
    {
    }

    [CreateAssetMenu(fileName = "Skill_New", menuName = "Rootborn/Student Life/Skill")]
    public sealed class SkillDefinition : StudentLifeDefinitionBase
    {
    }

    [CreateAssetMenu(fileName = "Career_New", menuName = "Rootborn/Student Life/Career")]
    public sealed class CareerDefinition : StudentLifeDefinitionBase
    {
        [SerializeField] private CareerUnlockRequirementBase[] _requirements = Array.Empty<CareerUnlockRequirementBase>();

        public bool IsUnlocked(StudentLifeProgress progress)
        {
            if (progress == null)
            {
                return false;
            }

            for (int i = 0; i < _requirements.Length; i++)
            {
                var requirement = _requirements[i];
                if (requirement != null && !requirement.IsSatisfied(progress))
                {
                    return false;
                }
            }

            return true;
        }

        public void ConfigureForTests(string id, string displayNameKey, CareerUnlockRequirementBase[] requirements)
        {
            ConfigureForTests(id, displayNameKey);
            _requirements = requirements ?? Array.Empty<CareerUnlockRequirementBase>();
        }
    }

    public abstract class CareerUnlockRequirementBase : ScriptableObject
    {
        public abstract bool IsSatisfied(StudentLifeProgress progress);
    }

    [CreateAssetMenu(fileName = "CareerReq_TraitThreshold", menuName = "Rootborn/Student Life/Career Requirements/Trait Threshold")]
    public sealed class TraitThresholdCareerRequirement : CareerUnlockRequirementBase
    {
        [SerializeField] private TraitDefinition _trait;
        [SerializeField] private int _minimumValue;

        public override bool IsSatisfied(StudentLifeProgress progress)
        {
            return progress != null && progress.GetTraitValue(_trait) >= Mathf.Max(0, _minimumValue);
        }

        public void ConfigureForTests(TraitDefinition trait, int minimumValue)
        {
            _trait = trait;
            _minimumValue = minimumValue;
        }
    }

    [CreateAssetMenu(fileName = "CareerReq_SkillThreshold", menuName = "Rootborn/Student Life/Career Requirements/Skill Threshold")]
    public sealed class SkillThresholdCareerRequirement : CareerUnlockRequirementBase
    {
        [SerializeField] private SkillDefinition _skill;
        [SerializeField] private int _minimumValue;

        public override bool IsSatisfied(StudentLifeProgress progress)
        {
            return progress != null && progress.GetSkillValue(_skill) >= Mathf.Max(0, _minimumValue);
        }

        public void ConfigureForTests(SkillDefinition skill, int minimumValue)
        {
            _skill = skill;
            _minimumValue = minimumValue;
        }
    }

    public abstract class LifeActivityRequirementBase : ScriptableObject
    {
        public abstract bool IsSatisfied(StudentLifeProgress progress);
    }

    [CreateAssetMenu(fileName = "ActivityReq_TraitThreshold", menuName = "Rootborn/Student Life/Activity Requirements/Trait Threshold")]
    public sealed class TraitThresholdActivityRequirement : LifeActivityRequirementBase
    {
        [SerializeField] private TraitDefinition _trait;
        [SerializeField] private int _minimumValue;

        public override bool IsSatisfied(StudentLifeProgress progress)
        {
            return progress != null && progress.GetTraitValue(_trait) >= Mathf.Max(0, _minimumValue);
        }

        public void ConfigureForTests(TraitDefinition trait, int minimumValue)
        {
            _trait = trait;
            _minimumValue = minimumValue;
        }
    }

    [CreateAssetMenu(fileName = "ActivityReq_SkillThreshold", menuName = "Rootborn/Student Life/Activity Requirements/Skill Threshold")]
    public sealed class SkillThresholdActivityRequirement : LifeActivityRequirementBase
    {
        [SerializeField] private SkillDefinition _skill;
        [SerializeField] private int _minimumValue;

        public override bool IsSatisfied(StudentLifeProgress progress)
        {
            return progress != null && progress.GetSkillValue(_skill) >= Mathf.Max(0, _minimumValue);
        }

        public void ConfigureForTests(SkillDefinition skill, int minimumValue)
        {
            _skill = skill;
            _minimumValue = minimumValue;
        }
    }

    public abstract class LifeActivityEffectBase : ScriptableObject
    {
        public abstract void Apply(StudentLifeProgress progress);
    }

    [CreateAssetMenu(fileName = "ActivityFx_TraitDelta", menuName = "Rootborn/Student Life/Activity Effects/Trait Delta")]
    public sealed class TraitDeltaActivityEffect : LifeActivityEffectBase
    {
        [SerializeField] private TraitDefinition _trait;
        [SerializeField] private int _delta;

        public override void Apply(StudentLifeProgress progress)
        {
            progress?.AddTrait(_trait, _delta);
        }

        public void ConfigureForTests(TraitDefinition trait, int delta)
        {
            _trait = trait;
            _delta = delta;
        }
    }

    [CreateAssetMenu(fileName = "ActivityFx_SkillProgress", menuName = "Rootborn/Student Life/Activity Effects/Skill Progress")]
    public sealed class SkillProgressActivityEffect : LifeActivityEffectBase
    {
        [SerializeField] private SkillDefinition _skill;
        [SerializeField] private int _delta;

        public override void Apply(StudentLifeProgress progress)
        {
            progress?.AddSkill(_skill, _delta);
        }

        public void ConfigureForTests(SkillDefinition skill, int delta)
        {
            _skill = skill;
            _delta = delta;
        }
    }

    [CreateAssetMenu(fileName = "ActivityFx_CareerHint", menuName = "Rootborn/Student Life/Activity Effects/Career Hint Unlock")]
    public sealed class CareerHintUnlockActivityEffect : LifeActivityEffectBase
    {
        [SerializeField] private CareerDefinition _career;

        public override void Apply(StudentLifeProgress progress)
        {
            progress?.UnlockCareerHint(_career);
        }

        public void ConfigureForTests(CareerDefinition career)
        {
            _career = career;
        }
    }

    [CreateAssetMenu(fileName = "LifeActivity_New", menuName = "Rootborn/Student Life/Activity")]
    public sealed class LifeActivityDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayNameKey;
        [SerializeField] private LifeActivityCategory _category;
        [SerializeField] private int _timeCostMinutes;
        [SerializeField] private int _energyCost;
        [SerializeField] private int _focusCost;
        [SerializeField] private int _stressDelta;
        [SerializeField] private LifeActivityRequirementBase[] _requirements = Array.Empty<LifeActivityRequirementBase>();
        [SerializeField] private LifeActivityEffectBase[] _effects = Array.Empty<LifeActivityEffectBase>();

        public string Id => string.IsNullOrEmpty(_id) ? name : _id;
        public string DisplayNameKey => string.IsNullOrEmpty(_displayNameKey) ? Id : _displayNameKey;
        public LifeActivityCategory Category => _category;
        public int TimeCostMinutes => Mathf.Max(0, _timeCostMinutes);
        public int EnergyCost => Mathf.Max(0, _energyCost);
        public int FocusCost => Mathf.Max(0, _focusCost);
        public int StressDelta => _stressDelta;

        public bool HasSatisfiedRequirements(StudentLifeProgress progress)
        {
            if (progress == null)
            {
                return false;
            }

            for (int i = 0; i < _requirements.Length; i++)
            {
                var requirement = _requirements[i];
                if (requirement != null && !requirement.IsSatisfied(progress))
                {
                    return false;
                }
            }

            return true;
        }

        public void ApplyEffects(StudentLifeProgress progress)
        {
            for (int i = 0; i < _effects.Length; i++)
            {
                _effects[i]?.Apply(progress);
            }
        }

        public void ConfigureForTests(
            string id,
            string displayNameKey,
            LifeActivityCategory category,
            int timeCostMinutes,
            int energyCost,
            int focusCost,
            int stressDelta,
            LifeActivityRequirementBase[] requirements,
            LifeActivityEffectBase[] effects)
        {
            _id = id;
            _displayNameKey = displayNameKey;
            _category = category;
            _timeCostMinutes = timeCostMinutes;
            _energyCost = energyCost;
            _focusCost = focusCost;
            _stressDelta = stressDelta;
            _requirements = requirements ?? Array.Empty<LifeActivityRequirementBase>();
            _effects = effects ?? Array.Empty<LifeActivityEffectBase>();
        }
    }

    public sealed class LifeActivityRunner
    {
        public bool TryPerform(
            LifeActivityDefinition activity,
            StudentLifeProgress progress,
            string requestId,
            out LifeActivityResult result)
        {
            string saveSlot = progress == null ? "default" : progress.SaveSlot;
            string playerId = progress == null ? "player" : progress.PlayerId;
            string activityId = activity == null ? string.Empty : activity.Id;
            result = new LifeActivityResult(LifeActivityResultKind.InvalidRequest, saveSlot, playerId, activityId, requestId);

            if (activity == null || progress == null || string.IsNullOrEmpty(requestId))
            {
                return false;
            }

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

            progress.Spend(activity.TimeCostMinutes, activity.EnergyCost, activity.FocusCost, activity.StressDelta);
            activity.ApplyEffects(progress);
            progress.MarkRequestApplied(requestId);
            result = new LifeActivityResult(LifeActivityResultKind.Applied, progress.SaveSlot, progress.PlayerId, activity.Id, requestId);
            return true;
        }
    }
}
