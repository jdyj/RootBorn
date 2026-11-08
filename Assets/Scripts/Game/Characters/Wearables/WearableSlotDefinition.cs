using System;
using System.Collections.Generic;
using Rootborn.Core.Characters;
using UnityEngine;

namespace Rootborn.Game.Characters.Wearables
{
    /// <summary>A wearable slot (hair, outfit, hat, ...). Slots are data; code never names a slot.</summary>
    [CreateAssetMenu(fileName = "Slot", menuName = "Rootborn/Characters/Wearable Slot")]
    public sealed class WearableSlotDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayNameKey;
        [Tooltip("Lower draws first. The stage body is drawn under every slot.")]
        [SerializeField] private int _drawOrder;
        [Tooltip("Worn when nothing is chosen: the first with art for the current stage. Empty lets the slot stay empty.")]
        [SerializeField] private WearableDefinition[] _defaultWearables = Array.Empty<WearableDefinition>();
        [Tooltip("Slots not drawn while this slot shows something (a hat may hide hair).")]
        [SerializeField] private WearableSlotDefinition[] _hidesSlots = Array.Empty<WearableSlotDefinition>();

        public string Id => _id;
        public string DisplayNameKey => _displayNameKey;
        public int DrawOrder => _drawOrder;
        public IReadOnlyList<WearableDefinition> DefaultWearables => _defaultWearables;
        public IReadOnlyList<WearableSlotDefinition> HidesSlots => _hidesSlots;

        public WearableSlotRule ToRule()
        {
            var defaults = new List<string>(_defaultWearables != null ? _defaultWearables.Length : 0);
            if (_defaultWearables != null)
                foreach (var wearable in _defaultWearables)
                    if (wearable != null && !string.IsNullOrEmpty(wearable.Id)) defaults.Add(wearable.Id);
            var hides = new List<string>(_hidesSlots != null ? _hidesSlots.Length : 0);
            if (_hidesSlots != null)
                foreach (var slot in _hidesSlots)
                    if (slot != null && !string.IsNullOrEmpty(slot.Id)) hides.Add(slot.Id);
            return new WearableSlotRule(_id, _drawOrder, defaults, hides);
        }
    }
}
