using System;
using System.Collections.Generic;
using Rootborn.Game.Common;
using Rootborn.Game.Quests;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "PartTimeWork_New", menuName = "Rootborn/Student Life/Part-Time Work/Work")]
    public sealed class PartTimeWorkDefinition : StudentLifeDefinitionBase
    {
        [SerializeField] private WorkplaceDefinition _workplace;
        [SerializeField] private int _timeCostMinutes;
        [SerializeField] private int _energyCost;
        [SerializeField] private int _focusCost;
        [SerializeField] private int _stressDelta;
        [SerializeField] private ItemDefinition[] _rewardItems = Array.Empty<ItemDefinition>();
        [SerializeField] private int[] _rewardCounts = Array.Empty<int>();
        [SerializeField] private WorkRequirementBase[] _requirements = Array.Empty<WorkRequirementBase>();
        [SerializeField] private WorkOutcomeBase[] _outcomes = Array.Empty<WorkOutcomeBase>();

        [NonSerialized] private InventoryGrant[] _configuredRewards;

        public WorkplaceDefinition Workplace => _workplace;
        public string WorkplaceId => _workplace != null ? _workplace.Id : string.Empty;
        public int TimeCostMinutes => Mathf.Max(0, _timeCostMinutes);
        public int EnergyCost => Mathf.Max(0, _energyCost);
        public int FocusCost => Mathf.Max(0, _focusCost);
        public int StressDelta => _stressDelta;
        public IReadOnlyList<InventoryGrant> Rewards => _configuredRewards ?? BuildSerializedRewards();
        public IReadOnlyList<WorkRequirementBase> Requirements => _requirements;
        public IReadOnlyList<WorkOutcomeBase> Outcomes => _outcomes;

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
            WorkplaceDefinition workplace,
            int timeCostMinutes,
            int energyCost,
            int focusCost,
            int stressDelta,
            InventoryGrant[] rewards,
            WorkRequirementBase[] requirements,
            WorkOutcomeBase[] outcomes)
        {
            ConfigureForTests(id, displayNameKey);
            _workplace = workplace;
            _timeCostMinutes = timeCostMinutes;
            _energyCost = energyCost;
            _focusCost = focusCost;
            _stressDelta = stressDelta;
            _configuredRewards = rewards ?? Array.Empty<InventoryGrant>();
            _requirements = requirements ?? Array.Empty<WorkRequirementBase>();
            _outcomes = outcomes ?? Array.Empty<WorkOutcomeBase>();
        }

        private InventoryGrant[] BuildSerializedRewards()
        {
            if (_rewardItems == null || _rewardItems.Length == 0)
            {
                return Array.Empty<InventoryGrant>();
            }

            var rewards = new List<InventoryGrant>(_rewardItems.Length);
            for (int i = 0; i < _rewardItems.Length; i++)
            {
                var item = _rewardItems[i];
                int count = _rewardCounts != null && i < _rewardCounts.Length ? _rewardCounts[i] : 1;
                if (item != null && count > 0)
                {
                    rewards.Add(new InventoryGrant(item, count));
                }
            }

            return rewards.ToArray();
        }
    }

    public abstract class WorkRequirementBase : ScriptableObject
    {
        public abstract bool IsSatisfied(StudentLifeProgress progress);
    }

    public abstract class WorkOutcomeBase : ScriptableObject
    {
        public abstract string Apply(StudentLifeProgress progress, string workId);
    }
}
