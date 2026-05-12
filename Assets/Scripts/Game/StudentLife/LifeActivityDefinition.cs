using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
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
        [SerializeField] private LifeChoiceDefinition[] _choices = Array.Empty<LifeChoiceDefinition>();

        public string Id => string.IsNullOrEmpty(_id) ? name : _id;
        public string DisplayNameKey => string.IsNullOrEmpty(_displayNameKey) ? Id : _displayNameKey;
        public LifeActivityCategory Category => _category;
        public int TimeCostMinutes => Mathf.Max(0, _timeCostMinutes);
        public int EnergyCost => Mathf.Max(0, _energyCost);
        public int FocusCost => Mathf.Max(0, _focusCost);
        public int StressDelta => _stressDelta;
        public IReadOnlyList<LifeChoiceDefinition> Choices => _choices;

        public LifeChoiceDefinition GetChoice(int index)
        {
            return index >= 0 && index < _choices.Length ? _choices[index] : null;
        }

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

        public void ConfigureForTests(
            string id,
            string displayNameKey,
            LifeActivityCategory category,
            int timeCostMinutes,
            int energyCost,
            int focusCost,
            int stressDelta,
            LifeActivityRequirementBase[] requirements,
            LifeActivityEffectBase[] effects,
            LifeChoiceDefinition[] choices)
        {
            ConfigureForTests(id, displayNameKey, category, timeCostMinutes, energyCost, focusCost, stressDelta, requirements, effects);
            _choices = choices ?? Array.Empty<LifeChoiceDefinition>();
        }
    }
}
