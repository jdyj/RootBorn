using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "DailyEventChoice_New", menuName = "Rootborn/Student Life/Daily Events/Choice")]
    public sealed class DailyEventChoiceDefinition : StudentLifeDefinitionBase
    {
        [SerializeField] private string _resultSummaryKey;
        [SerializeField] private int _timeCostMinutes;
        [SerializeField] private int _energyCost;
        [SerializeField] private int _focusCost;
        [SerializeField] private int _stressDelta;
        [SerializeField] private bool _isDeferChoice;
        [SerializeField] private bool _isDeclineChoice;
        [SerializeField] private DailyEventAvailabilityRuleBase[] _availabilityRules = Array.Empty<DailyEventAvailabilityRuleBase>();
        [SerializeField] private DailyEventOutcomeBase[] _outcomes = Array.Empty<DailyEventOutcomeBase>();

        public string ResultSummaryKey => string.IsNullOrEmpty(_resultSummaryKey) ? Id : _resultSummaryKey;
        public int TimeCostMinutes => Mathf.Max(0, _timeCostMinutes);
        public int EnergyCost => Mathf.Max(0, _energyCost);
        public int FocusCost => Mathf.Max(0, _focusCost);
        public int StressDelta => _stressDelta;
        public bool IsDeferChoice => _isDeferChoice;
        public bool IsDeclineChoice => _isDeclineChoice;
        public IReadOnlyList<DailyEventAvailabilityRuleBase> AvailabilityRules => _availabilityRules;
        public IReadOnlyList<DailyEventOutcomeBase> Outcomes => _outcomes;

        public bool HasSatisfiedAvailability(DailyEventContext context, DailyEventDefinition dailyEvent)
        {
            for (int i = 0; i < _availabilityRules.Length; i++)
            {
                var rule = _availabilityRules[i];
                if (rule != null && !rule.IsSatisfied(context, dailyEvent)) return false;
            }

            return true;
        }

        public string[] ApplyOutcomes(StudentLifeProgress progress, DailyEventDefinition dailyEvent)
        {
            var logs = new List<string>();
            for (int i = 0; i < _outcomes.Length; i++)
            {
                string log = _outcomes[i] != null ? _outcomes[i].Apply(progress, dailyEvent, this) : string.Empty;
                if (!string.IsNullOrEmpty(log)) logs.Add(log);
            }

            return logs.ToArray();
        }

        public void ConfigureForTests(string id, string displayNameKey, string resultSummaryKey, int timeCostMinutes, int energyCost, int focusCost, int stressDelta, bool isDeferChoice, bool isDeclineChoice, DailyEventAvailabilityRuleBase[] availabilityRules, DailyEventOutcomeBase[] outcomes)
        {
            ConfigureForTests(id, displayNameKey);
            _resultSummaryKey = string.IsNullOrEmpty(resultSummaryKey) ? id : resultSummaryKey;
            _timeCostMinutes = timeCostMinutes;
            _energyCost = energyCost;
            _focusCost = focusCost;
            _stressDelta = stressDelta;
            _isDeferChoice = isDeferChoice;
            _isDeclineChoice = isDeclineChoice;
            _availabilityRules = availabilityRules ?? Array.Empty<DailyEventAvailabilityRuleBase>();
            _outcomes = outcomes ?? Array.Empty<DailyEventOutcomeBase>();
        }
    }
}
