using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rootborn.Game.DiscoveryClues
{
    [CreateAssetMenu(fileName = "ClueInterpretation_New", menuName = "Rootborn/Discovery Clues/Interpretations/Definition")]
    public sealed class ClueInterpretationDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayNameKey;
        [SerializeField] private string _descriptionKey;
        [SerializeField] private DiscoveryClueDefinition _clue;
        [SerializeField] private string _methodKey;
        [SerializeField] private string _publicTextKey;
        [SerializeField] private string _hiddenTextKey;
        [SerializeField] private ClueInterpretationSourceDefinition[] _sources = Array.Empty<ClueInterpretationSourceDefinition>();
        [SerializeField] private ClueInterpretationConditionBase[] _conditions = Array.Empty<ClueInterpretationConditionBase>();
        [SerializeField] private ClueInterpretationOutcomeBase[] _outcomes = Array.Empty<ClueInterpretationOutcomeBase>();
        [SerializeField] private ClueInterpretationPolicyDefinition _policy;
        [SerializeField] private int _sortPriority;

        public string Id => string.IsNullOrEmpty(_id) ? name : _id;
        public string DisplayNameKey => string.IsNullOrEmpty(_displayNameKey) ? Id : _displayNameKey;
        public string DescriptionKey => string.IsNullOrEmpty(_descriptionKey) ? DisplayNameKey : _descriptionKey;
        public DiscoveryClueDefinition Clue => _clue;
        public string ClueId => _clue != null ? _clue.Id : string.Empty;
        public string MethodKey => string.IsNullOrEmpty(_methodKey) ? DisplayNameKey : _methodKey;
        public string PublicTextKey => string.IsNullOrEmpty(_publicTextKey) ? DescriptionKey : _publicTextKey;
        public string HiddenTextKey => string.IsNullOrEmpty(_hiddenTextKey) ? "???" : _hiddenTextKey;
        public IReadOnlyList<ClueInterpretationSourceDefinition> Sources => _sources;
        public IReadOnlyList<ClueInterpretationConditionBase> Conditions => _conditions;
        public IReadOnlyList<ClueInterpretationOutcomeBase> Outcomes => _outcomes;
        public ClueInterpretationPolicyDefinition Policy => _policy;
        public int SortPriority => _sortPriority;

        public bool ConditionsSatisfied(in ClueInterpretationContext context, out string reason)
        {
            reason = string.Empty;
            if (_conditions == null) return true;
            for (int i = 0; i < _conditions.Length; i++)
            {
                var condition = _conditions[i];
                if (condition == null || condition.Evaluate(context, this)) continue;
                reason = condition.LockedReasonKey;
                return false;
            }

            return true;
        }

        public void ConfigureForTests(string id, string displayNameKey, string descriptionKey, DiscoveryClueDefinition clue, string methodKey, string publicTextKey, string hiddenTextKey, ClueInterpretationSourceDefinition[] sources, ClueInterpretationConditionBase[] conditions, ClueInterpretationOutcomeBase[] outcomes, ClueInterpretationPolicyDefinition policy, int sortPriority)
        {
            _id = id;
            _displayNameKey = displayNameKey;
            _descriptionKey = descriptionKey;
            _clue = clue;
            _methodKey = methodKey;
            _publicTextKey = publicTextKey;
            _hiddenTextKey = hiddenTextKey;
            _sources = sources ?? Array.Empty<ClueInterpretationSourceDefinition>();
            _conditions = conditions ?? Array.Empty<ClueInterpretationConditionBase>();
            _outcomes = outcomes ?? Array.Empty<ClueInterpretationOutcomeBase>();
            _policy = policy;
            _sortPriority = sortPriority;
        }
    }
}
