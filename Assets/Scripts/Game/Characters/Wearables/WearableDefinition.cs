using System;
using System.Collections.Generic;
using Rootborn.Core.Characters;
using Rootborn.Game.Characters.Growing;
using UnityEngine;

namespace Rootborn.Game.Characters.Wearables
{
    /// <summary>One wearable item: its slot, its art for each growth stage, and an optional work-uniform facility.</summary>
    [CreateAssetMenu(fileName = "Wearable", menuName = "Rootborn/Characters/Wearable")]
    public sealed class WearableDefinition : ScriptableObject
    {
        [Serializable]
        public struct StageArt
        {
            public CharacterGrowthStageDefinition Stage;
            public GrowingCharacterSheetDefinition Sheet;
        }

        [SerializeField] private string _id;
        [SerializeField] private string _displayNameKey;
        [SerializeField] private WearableSlotDefinition _slot;
        [Tooltip("Art per growth stage. A stage without art cannot wear this item; the slot default is drawn instead.")]
        [SerializeField] private StageArt[] _stageArt = Array.Empty<StageArt>();
        [Tooltip("Worn automatically while working at this facility. Empty for items the player picks.")]
        [SerializeField] private string _autoEquipFacilityId;

        public string Id => _id;
        public string DisplayNameKey => _displayNameKey;
        public WearableSlotDefinition Slot => _slot;
        public IReadOnlyList<StageArt> Art => _stageArt;
        public string AutoEquipFacilityId => _autoEquipFacilityId;

        /// <summary>The sheet drawn at a stage, or null when the item has no art for it.</summary>
        public GrowingCharacterSheetDefinition SheetFor(string stageId)
        {
            if (_stageArt == null || string.IsNullOrEmpty(stageId)) return null;
            for (int i = 0; i < _stageArt.Length; i++)
            {
                var art = _stageArt[i];
                if (art.Stage != null && art.Sheet != null && string.Equals(art.Stage.Id, stageId, StringComparison.Ordinal)) return art.Sheet;
            }

            return null;
        }

        public WearableRule ToRule()
        {
            var stages = new List<string>(_stageArt != null ? _stageArt.Length : 0);
            if (_stageArt != null)
                foreach (var art in _stageArt)
                    if (art.Stage != null && art.Sheet != null && !string.IsNullOrEmpty(art.Stage.Id)) stages.Add(art.Stage.Id);
            return new WearableRule(_id, _slot != null ? _slot.Id : string.Empty, stages, _autoEquipFacilityId);
        }
    }
}
