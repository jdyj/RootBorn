using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "TownHelpAction_New", menuName = "Rootborn/Student Life/Town Help/Action")]
    public sealed class TownHelpActionDefinition : StudentLifeDefinitionBase
    {
        [SerializeField] private string _npcId;
        [SerializeField] private string _locationId;
        [SerializeField] private int _timeCostMinutes;
        [SerializeField] private int _energyCost;
        [SerializeField] private int _focusCost;
        [SerializeField] private int _stressDelta;
        [SerializeField] private HelpActionRequirementBase[] _requirements = Array.Empty<HelpActionRequirementBase>();
        [SerializeField] private HelpActionOutcomeBase[] _outcomes = Array.Empty<HelpActionOutcomeBase>();

        public string NpcId => string.IsNullOrEmpty(_npcId) ? string.Empty : _npcId;
        public string LocationId => string.IsNullOrEmpty(_locationId) ? string.Empty : _locationId;
        public int TimeCostMinutes => Mathf.Max(0, _timeCostMinutes);
        public int EnergyCost => Mathf.Max(0, _energyCost);
        public int FocusCost => Mathf.Max(0, _focusCost);
        public int StressDelta => _stressDelta;
        public IReadOnlyList<HelpActionRequirementBase> Requirements => _requirements;
        public IReadOnlyList<HelpActionOutcomeBase> Outcomes => _outcomes;

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
            string npcId,
            string locationId,
            int timeCostMinutes,
            int energyCost,
            int focusCost,
            int stressDelta,
            HelpActionRequirementBase[] requirements,
            HelpActionOutcomeBase[] outcomes)
        {
            ConfigureForTests(id, displayNameKey);
            _npcId = string.IsNullOrEmpty(npcId) ? string.Empty : npcId;
            _locationId = string.IsNullOrEmpty(locationId) ? string.Empty : locationId;
            _timeCostMinutes = timeCostMinutes;
            _energyCost = energyCost;
            _focusCost = focusCost;
            _stressDelta = stressDelta;
            _requirements = requirements ?? Array.Empty<HelpActionRequirementBase>();
            _outcomes = outcomes ?? Array.Empty<HelpActionOutcomeBase>();
        }
    }

    public abstract class HelpActionRequirementBase : ScriptableObject
    {
        public abstract bool IsSatisfied(StudentLifeProgress progress);
    }

    public abstract class HelpActionOutcomeBase : ScriptableObject
    {
        public abstract string Apply(StudentLifeProgress progress, string actionId);
    }
}
