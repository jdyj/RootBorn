using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rootborn.Game.Encyclopedia
{
    [CreateAssetMenu(fileName = "EncyclopediaEntry_New", menuName = "Rootborn/Encyclopedia/Entry")]
    public sealed class EncyclopediaEntryDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private EncyclopediaCategoryDefinition _category;
        [SerializeField] private string _displayName;
        [SerializeField] private string _lockedDisplayName = "???";
        [SerializeField] private string _description;
        [SerializeField] private string _lockedHint;
        [SerializeField] private string[] _detailLines = Array.Empty<string>();
        [SerializeField] private EncyclopediaUnlockConditionBase[] _unlockConditions = Array.Empty<EncyclopediaUnlockConditionBase>();
        [SerializeField] private int _revealStage = 1;

        public string Id => string.IsNullOrEmpty(_id) ? name : _id;
        public EncyclopediaCategoryDefinition Category => _category;
        public string DisplayName => string.IsNullOrEmpty(_displayName) ? Id : _displayName;
        public string LockedDisplayName => string.IsNullOrEmpty(_lockedDisplayName) ? "???" : _lockedDisplayName;
        public string Description => string.IsNullOrEmpty(_description) ? DisplayName : _description;
        public string LockedHint => string.IsNullOrEmpty(_lockedHint) ? "???" : _lockedHint;
        public IReadOnlyList<string> DetailLines => _detailLines;
        public IReadOnlyList<EncyclopediaUnlockConditionBase> UnlockConditions => _unlockConditions;
        public int RevealStage => Mathf.Max(1, _revealStage);

        public bool IsUnlocked(in EncyclopediaUnlockContext context)
        {
            if (_unlockConditions == null || _unlockConditions.Length == 0) return false;
            for (int i = 0; i < _unlockConditions.Length; i++)
            {
                var condition = _unlockConditions[i];
                if (condition == null || !condition.IsSatisfied(context)) return false;
            }
            return true;
        }

        public void ConfigureForTests(string id, EncyclopediaCategoryDefinition category, string displayName, string lockedDisplayName, string description, string lockedHint, string[] detailLines, EncyclopediaUnlockConditionBase[] unlockConditions)
        {
            _id = id;
            _category = category;
            _displayName = displayName;
            _lockedDisplayName = string.IsNullOrEmpty(lockedDisplayName) ? "???" : lockedDisplayName;
            _description = description;
            _lockedHint = lockedHint;
            _detailLines = detailLines ?? Array.Empty<string>();
            _unlockConditions = unlockConditions ?? Array.Empty<EncyclopediaUnlockConditionBase>();
            _revealStage = 1;
        }
    }
}
