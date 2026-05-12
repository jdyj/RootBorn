using System;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "DayEndRule", menuName = "Rootborn/Student Life/Day End Rule Definition")]
    public sealed class DayEndRuleDefinition : StudentLifeDefinitionBase
    {
        [SerializeField] private string _nextDayEntryPointId = "home-entry";
        [SerializeField] private DayEndRuleBase[] _rules = Array.Empty<DayEndRuleBase>();

        public string NextDayEntryPointId => string.IsNullOrEmpty(_nextDayEntryPointId) ? string.Empty : _nextDayEntryPointId;
        public System.Collections.Generic.IReadOnlyList<DayEndRuleBase> Rules => _rules;

        public void Apply(StudentLifeProgress progress)
        {
            if (progress == null || _rules == null)
            {
                return;
            }

            for (int i = 0; i < _rules.Length; i++)
            {
                _rules[i]?.Apply(progress);
            }
        }

        public void ConfigureForTests(string id, string displayNameKey, string nextDayEntryPointId, DayEndRuleBase[] rules)
        {
            ConfigureForTests(id, displayNameKey);
            _nextDayEntryPointId = string.IsNullOrEmpty(nextDayEntryPointId) ? string.Empty : nextDayEntryPointId;
            _rules = rules ?? Array.Empty<DayEndRuleBase>();
        }
    }
}
