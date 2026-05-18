using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "ExplorationChoice_New", menuName = "Rootborn/Student Life/Exploration Choices/Choice")]
    public sealed class ExplorationChoiceDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayNameKey;
        [SerializeField] private string _descriptionKey;
        [SerializeField] private ExplorationConditionBase[] _conditions = Array.Empty<ExplorationConditionBase>();
        [SerializeField] private ExplorationOutcomeBase[] _outcomes = Array.Empty<ExplorationOutcomeBase>();
        [SerializeField] private ExplorationRiskPolicyDefinition _riskPolicy;
        [SerializeField] private bool _repeatable;
        [SerializeField] private string _previewHintKey;

        public string Id => string.IsNullOrEmpty(_id) ? name : _id;
        public string DisplayNameKey => string.IsNullOrEmpty(_displayNameKey) ? Id : _displayNameKey;
        public string DescriptionKey => string.IsNullOrEmpty(_descriptionKey) ? DisplayNameKey : _descriptionKey;
        public IReadOnlyList<ExplorationConditionBase> Conditions => _conditions;
        public IReadOnlyList<ExplorationOutcomeBase> Outcomes => _outcomes;
        public ExplorationRiskPolicyDefinition RiskPolicy => _riskPolicy;
        public bool Repeatable => _repeatable;
        public string PreviewHintKey => string.IsNullOrEmpty(_previewHintKey) ? DescriptionKey : _previewHintKey;

        public bool ConditionsSatisfied(in ExplorationInteractionContext context, out string lockedReasonKey)
        {
            lockedReasonKey = string.Empty;
            for (int i = 0; i < _conditions.Length; i++)
            {
                var condition = _conditions[i];
                if (condition == null || condition.Evaluate(context, this)) continue;
                lockedReasonKey = condition.LockedReasonKey;
                return false;
            }

            return true;
        }

        public void ConfigureForTests(string id, string displayNameKey, string descriptionKey, ExplorationConditionBase[] conditions, ExplorationOutcomeBase[] outcomes, ExplorationRiskPolicyDefinition riskPolicy, bool repeatable, string previewHintKey)
        {
            _id = id;
            _displayNameKey = displayNameKey;
            _descriptionKey = descriptionKey;
            _conditions = conditions ?? Array.Empty<ExplorationConditionBase>();
            _outcomes = outcomes ?? Array.Empty<ExplorationOutcomeBase>();
            _riskPolicy = riskPolicy;
            _repeatable = repeatable;
            _previewHintKey = previewHintKey;
        }
    }
}
