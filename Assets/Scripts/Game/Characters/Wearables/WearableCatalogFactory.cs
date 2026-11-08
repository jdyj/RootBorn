using System.Collections.Generic;
using Rootborn.Core.Characters;

namespace Rootborn.Game.Characters.Wearables
{
    /// <summary>
    /// Turns the stage, slot and wearable assets into Rootborn.Core's WearableCatalog. Every layering, equip and save rule
    /// lives in the core (WearableRules, WearableSaveRules); this only reads data.
    /// </summary>
    public static class WearableCatalogFactory
    {
        public static WearableCatalog Build(IEnumerable<WearableSlotDefinition> slots, IEnumerable<WearableDefinition> wearables,
            IEnumerable<CharacterGrowthStageDefinition> stages)
        {
            var slotRules = new List<WearableSlotRule>();
            if (slots != null)
                foreach (var slot in slots)
                    if (slot != null && !string.IsNullOrEmpty(slot.Id)) slotRules.Add(slot.ToRule());

            var wearableRules = new List<WearableRule>();
            if (wearables != null)
                foreach (var wearable in wearables)
                    if (wearable != null && !string.IsNullOrEmpty(wearable.Id)) wearableRules.Add(wearable.ToRule());

            var stageRules = new List<GrowthStageRule>();
            if (stages != null)
                foreach (var stage in stages)
                    if (stage != null && !string.IsNullOrEmpty(stage.Id)) stageRules.Add(stage.ToRule());

            return new WearableCatalog(slotRules, wearableRules, stageRules);
        }

        /// <summary>Indexes wearables by id so a resolved layer can find its art.</summary>
        public static Dictionary<string, WearableDefinition> ById(IEnumerable<WearableDefinition> wearables)
        {
            var map = new Dictionary<string, WearableDefinition>(System.StringComparer.Ordinal);
            if (wearables == null) return map;
            foreach (var wearable in wearables)
                if (wearable != null && !string.IsNullOrEmpty(wearable.Id) && !map.ContainsKey(wearable.Id)) map.Add(wearable.Id, wearable);
            return map;
        }
    }
}
