using System;
using System.Collections.Generic;
using Rootborn.Game.Common;
using Rootborn.Game.Quests;
using Rootborn.Game.WorldState;
using UnityEngine;

namespace Rootborn.Game.Careers
{
    [CreateAssetMenu(fileName = "FacilityLevel_New", menuName = "Rootborn/Careers/Facility Level")]
    public sealed class FacilityLevelDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayNameKey;
        [SerializeField] private int _level = 1;
        [SerializeField] private ItemDefinition[] _costItems = Array.Empty<ItemDefinition>();
        [SerializeField] private int[] _costCounts = Array.Empty<int>();
        [SerializeField] private WorldStateFlagDefinition[] _worldFlags = Array.Empty<WorldStateFlagDefinition>();
        [SerializeField] private int _rewardPercent = 100;
        [SerializeField] private bool _completesRoute;
        [SerializeField] private Sprite _achievementSprite;
        public string Id => _id;
        public string DisplayNameKey => string.IsNullOrEmpty(_displayNameKey) ? Id : _displayNameKey;
        public int Level => _level;
        public int RewardPercent => _rewardPercent;
        public bool CompletesRoute => _completesRoute;
        public Sprite AchievementSprite => _achievementSprite;
        public IReadOnlyList<WorldStateFlagDefinition> WorldFlags => _worldFlags ?? Array.Empty<WorldStateFlagDefinition>();
        public bool IsValid
        {
            get
            {
                if (string.IsNullOrEmpty(Id) || Level < 1 || Level > 3 || _rewardPercent < 0 || _rewardPercent > 10000 || (_completesRoute && Level != 3)) return false;
                if (_costItems == null || _costCounts == null || _costItems.Length != _costCounts.Length) return false;
                for (int i = 0; i < _costItems.Length; i++) if (_costItems[i] == null || string.IsNullOrEmpty(_costItems[i].Id) || _costCounts[i] <= 0) return false;
                for (int i = 0; i < WorldFlags.Count; i++) if (WorldFlags[i] == null || string.IsNullOrEmpty(WorldFlags[i].Id)) return false;
                return true;
            }
        }
        public InventoryGrant[] BuildCosts()
        {
            if (!IsValid) return Array.Empty<InventoryGrant>();
            var grants = new InventoryGrant[_costItems.Length];
            for (int i = 0; i < grants.Length; i++) grants[i] = new InventoryGrant(_costItems[i], _costCounts[i]);
            return grants;
        }
    }
}
