using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "OutsideSchoolActivity_New", menuName = "Rootborn/Student Life/Outside School/Activity")]
    public sealed class OutsideSchoolActivityDefinition : StudentLifeDefinitionBase
    {
        [SerializeField] private OutsideSchoolActivityCategoryDefinition _category;
        [SerializeField] private int _timeCostMinutes;
        [SerializeField] private int _energyCost;
        [SerializeField] private int _focusCost;
        [SerializeField] private int _stressDelta;
        [SerializeField] private OutsideSchoolRequirementBase[] _requirements = Array.Empty<OutsideSchoolRequirementBase>();
        [SerializeField] private OutsideSchoolOutcomeBase[] _outcomes = Array.Empty<OutsideSchoolOutcomeBase>();

        public OutsideSchoolActivityCategoryDefinition Category => _category;
        public string CategoryId => _category != null ? _category.Id : string.Empty;
        public int TimeCostMinutes => Mathf.Max(0, _timeCostMinutes);
        public int EnergyCost => Mathf.Max(0, _energyCost);
        public int FocusCost => Mathf.Max(0, _focusCost);
        public int StressDelta => _stressDelta;
        public IReadOnlyList<OutsideSchoolRequirementBase> Requirements => _requirements;
        public IReadOnlyList<OutsideSchoolOutcomeBase> Outcomes => _outcomes;

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

        public string[] ApplyOutcomes(StudentLifeProgress progress)
        {
            var logs = new List<string>();
            for (int i = 0; i < _outcomes.Length; i++)
            {
                string log = _outcomes[i] != null ? _outcomes[i].Apply(progress, Id) : string.Empty;
                if (!string.IsNullOrEmpty(log))
                {
                    logs.Add(log);
                }
            }

            return logs.ToArray();
        }

        public void ConfigureForTests(
            string id,
            string displayNameKey,
            OutsideSchoolActivityCategoryDefinition category,
            int timeCostMinutes,
            int energyCost,
            int focusCost,
            int stressDelta,
            OutsideSchoolRequirementBase[] requirements,
            OutsideSchoolOutcomeBase[] outcomes)
        {
            ConfigureForTests(id, displayNameKey);
            _category = category;
            _timeCostMinutes = timeCostMinutes;
            _energyCost = energyCost;
            _focusCost = focusCost;
            _stressDelta = stressDelta;
            _requirements = requirements ?? Array.Empty<OutsideSchoolRequirementBase>();
            _outcomes = outcomes ?? Array.Empty<OutsideSchoolOutcomeBase>();
        }
    }

    public abstract class OutsideSchoolRequirementBase : ScriptableObject
    {
        public abstract bool IsSatisfied(StudentLifeProgress progress);
    }

    public abstract class OutsideSchoolOutcomeBase : ScriptableObject
    {
        public abstract string Apply(StudentLifeProgress progress, string activityId);
    }
}
