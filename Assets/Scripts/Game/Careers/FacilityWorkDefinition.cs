using System;
using System.Collections.Generic;
using Rootborn.Game.Common;
using Rootborn.Game.Minigames;
using Rootborn.Game.Quests;
using UnityEngine;

namespace Rootborn.Game.Careers
{
    [CreateAssetMenu(fileName = "FacilityWork_New", menuName = "Rootborn/Careers/Facility Work")]
    public sealed class FacilityWorkDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayNameKey;
        [SerializeField] private FacilityDefinition _facility;
        [SerializeField] private MinigameDefinition _minigame;
        [SerializeField] private int _minimumFacilityLevel = 1;
        [SerializeField] private int _timePointCost = 1;
        [SerializeField] private int _maximumMastery = 100;
        [SerializeField] private int _masteryPerSuccess = 1;
        [SerializeField] private int _minimumSuccessScore = 1;
        [SerializeField] private int _reputationPerSuccess = 1;
        [SerializeField] private ItemDefinition[] _rewardItems = Array.Empty<ItemDefinition>();
        [SerializeField] private int[] _maximumRewardCounts = Array.Empty<int>();
        [SerializeField] private AutoSkipRequirementBase[] _autoSkipRequirements = Array.Empty<AutoSkipRequirementBase>();
        public string Id => _id;
        public string DisplayNameKey => string.IsNullOrEmpty(_displayNameKey) ? Id : _displayNameKey;
        public FacilityDefinition Facility => _facility;
        public MinigameDefinition Minigame => _minigame;
        public int MinimumFacilityLevel => _minimumFacilityLevel;
        public int TimePointCost => _timePointCost;
        public int MaximumMastery => _maximumMastery;
        public int MasteryPerSuccess => _masteryPerSuccess;
        public int MinimumSuccessScore => _minimumSuccessScore;
        public int ReputationPerSuccess => _reputationPerSuccess;
        public bool IsValid
        {
            get
            {
                if (string.IsNullOrEmpty(Id) || Facility == null || _minimumFacilityLevel < 1 || _minimumFacilityLevel > 3 || _timePointCost <= 0 || _maximumMastery <= 0 || _masteryPerSuccess <= 0 || _minimumSuccessScore < 1 || _minimumSuccessScore > 100 || _reputationPerSuccess < 0) return false;
                if (_rewardItems == null || _maximumRewardCounts == null || _rewardItems.Length != _maximumRewardCounts.Length) return false;
                for (int i = 0; i < _rewardItems.Length; i++) if (_rewardItems[i] == null || string.IsNullOrEmpty(_rewardItems[i].Id) || _maximumRewardCounts[i] <= 0) return false;
                return true;
            }
        }
        public bool CanAutoSkip(in AutoSkipContext context)
        {
            var coverage = AutoSkipRequirementCoverage.None;
            if (_autoSkipRequirements == null) return false;
            for (int i = 0; i < _autoSkipRequirements.Length; i++)
            {
                var requirement = _autoSkipRequirements[i];
                if (requirement == null || !requirement.IsSatisfied(in context)) return false;
                coverage |= requirement.Coverage;
            }
            return (coverage & AutoSkipRequirementCoverage.All) == AutoSkipRequirementCoverage.All;
        }
        public bool TryBuildRewards(int score, int levelRewardPercent, out InventoryGrant[] rewards)
        {
            rewards = Array.Empty<InventoryGrant>();
            if (!IsValid || score < 0 || score > 100 || levelRewardPercent < 0 || levelRewardPercent > 10000) return false;
            if (score < MinimumSuccessScore) return true;
            var values = new List<InventoryGrant>(_rewardItems.Length);
            for (int i = 0; i < _rewardItems.Length; i++)
            {
                long count = (long)_maximumRewardCounts[i] * score * levelRewardPercent / 10000L;
                if (count > int.MaxValue) return false;
                if (count > 0) values.Add(new InventoryGrant(_rewardItems[i], (int)count));
            }
            rewards = values.ToArray(); return true;
        }
    }
}
