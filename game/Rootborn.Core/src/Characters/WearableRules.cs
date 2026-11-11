using System;
using System.Collections.Generic;

namespace Rootborn.Core.Characters
{
    /// <summary>A growth stage and when it applies. Data comes from the stage ScriptableObjects.</summary>
    public sealed class GrowthStageRule
    {
        public GrowthStageRule(string id, int minimumStudentDay, bool requiresAdult)
        {
            Id = id ?? string.Empty;
            MinimumStudentDay = minimumStudentDay;
            RequiresAdult = requiresAdult;
        }

        public string Id { get; }
        public int MinimumStudentDay { get; }
        public bool RequiresAdult { get; }
    }

    /// <summary>A wearable slot (face, hair, top, hat, accessory, ...). Slots are data, never an enum.</summary>
    public sealed class WearableSlotRule
    {
        public WearableSlotRule(string id, int drawOrder, IReadOnlyList<string> defaultWearableIds, IReadOnlyList<string> hidesSlotIds)
        {
            Id = id ?? string.Empty;
            DrawOrder = drawOrder;
            DefaultWearableIds = defaultWearableIds ?? Array.Empty<string>();
            HidesSlotIds = hidesSlotIds ?? Array.Empty<string>();
        }

        public string Id { get; }
        public int DrawOrder { get; }
        /// <summary>Worn when nothing is chosen: the first of these with art for the stage. Empty means the slot may stay empty.</summary>
        public IReadOnlyList<string> DefaultWearableIds { get; }
        /// <summary>Slots not drawn while this slot shows something (a hat may hide the top of the hair).</summary>
        public IReadOnlyList<string> HidesSlotIds { get; }
    }

    /// <summary>One wearable item: which slot it goes in and which growth stages have art for it.</summary>
    public sealed class WearableRule
    {
        public WearableRule(string id, string slotId, IReadOnlyCollection<string> stageIdsWithArt, string autoEquipFacilityId)
        {
            Id = id ?? string.Empty;
            SlotId = slotId ?? string.Empty;
            StageIdsWithArt = stageIdsWithArt ?? Array.Empty<string>();
            AutoEquipFacilityId = autoEquipFacilityId ?? string.Empty;
        }

        public string Id { get; }
        public string SlotId { get; }
        public IReadOnlyCollection<string> StageIdsWithArt { get; }
        /// <summary>Worn automatically while working at this facility (work uniforms). Empty for normal items.</summary>
        public string AutoEquipFacilityId { get; }

        public bool HasArtFor(string stageId)
        {
            foreach (string id in StageIdsWithArt)
                if (string.Equals(id, stageId, StringComparison.Ordinal)) return true;
            return false;
        }
    }

    /// <summary>A layer to draw, in draw order.</summary>
    public readonly struct WearableLayer
    {
        public WearableLayer(string slotId, string wearableId, int drawOrder)
        {
            SlotId = slotId;
            WearableId = wearableId;
            DrawOrder = drawOrder;
        }

        public string SlotId { get; }
        public string WearableId { get; }
        public int DrawOrder { get; }
    }

    /// <summary>
    /// What the player chose. Keys are a slot id ("top") for items drawn at every stage, or slot@stage ("top@school")
    /// for items that only exist for some stages, so a choice made while small is kept after growing up and back.
    /// Saved as a list of (key, wearable) pairs.
    /// </summary>
    public sealed class WearableState
    {
        public const char StageSeparator = '@';
        private readonly Dictionary<string, string> _equipped = new Dictionary<string, string>(StringComparer.Ordinal);

        public int Count => _equipped.Count;

        public static string StageKey(string slotId, string stageId) => slotId + StageSeparator + stageId;

        /// <summary>The choice that applies at a stage: the stage-scoped one first, then the general one.</summary>
        public string Get(string slotId, string stageId)
        {
            string scoped = Get(StageKey(slotId, stageId));
            return scoped.Length > 0 ? scoped : Get(slotId);
        }

        public string Get(string slotId)
        {
            return slotId != null && _equipped.TryGetValue(slotId, out string id) ? id : string.Empty;
        }

        public bool Has(string slotId) => slotId != null && _equipped.ContainsKey(slotId);

        public void Set(string slotId, string wearableId)
        {
            if (string.IsNullOrEmpty(slotId)) return;
            if (string.IsNullOrEmpty(wearableId)) _equipped.Remove(slotId);
            else _equipped[slotId] = wearableId;
        }

        /// <summary>Stable order (by slot id) so saves do not churn.</summary>
        public List<KeyValuePair<string, string>> ToEntries()
        {
            var entries = new List<KeyValuePair<string, string>>(_equipped);
            entries.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            return entries;
        }

        public static WearableState FromEntries(IEnumerable<KeyValuePair<string, string>> entries)
        {
            var state = new WearableState();
            if (entries == null) return state;
            foreach (var entry in entries) state.Set(entry.Key, entry.Value);
            return state;
        }
    }

    /// <summary>The wearable catalog the rules work on: slots, items and growth stages, looked up by id.</summary>
    public sealed class WearableCatalog
    {
        private readonly List<WearableSlotRule> _slots;
        private readonly Dictionary<string, WearableSlotRule> _slotsById = new Dictionary<string, WearableSlotRule>(StringComparer.Ordinal);
        private readonly Dictionary<string, WearableRule> _wearablesById = new Dictionary<string, WearableRule>(StringComparer.Ordinal);
        private readonly List<WearableRule> _wearables;
        private readonly List<GrowthStageRule> _stages;

        public WearableCatalog(IEnumerable<WearableSlotRule> slots, IEnumerable<WearableRule> wearables, IEnumerable<GrowthStageRule> stages)
        {
            _slots = new List<WearableSlotRule>();
            if (slots != null)
                foreach (var slot in slots)
                    if (slot != null && slot.Id.Length > 0 && !_slotsById.ContainsKey(slot.Id)) { _slots.Add(slot); _slotsById[slot.Id] = slot; }
            _slots.Sort((a, b) => a.DrawOrder != b.DrawOrder ? a.DrawOrder.CompareTo(b.DrawOrder) : string.CompareOrdinal(a.Id, b.Id));

            _wearables = new List<WearableRule>();
            if (wearables != null)
                foreach (var wearable in wearables)
                    if (wearable != null && wearable.Id.Length > 0 && !_wearablesById.ContainsKey(wearable.Id)) { _wearables.Add(wearable); _wearablesById[wearable.Id] = wearable; }
            _wearables.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));

            _stages = new List<GrowthStageRule>();
            if (stages != null) foreach (var stage in stages) if (stage != null && stage.Id.Length > 0) _stages.Add(stage);
        }

        /// <summary>Slots in draw order (lowest first).</summary>
        public IReadOnlyList<WearableSlotRule> Slots => _slots;
        public IReadOnlyList<WearableRule> Wearables => _wearables;
        public IReadOnlyList<GrowthStageRule> Stages => _stages;

        public WearableSlotRule FindSlot(string id) => id != null && _slotsById.TryGetValue(id, out var slot) ? slot : null;
        public WearableRule FindWearable(string id) => id != null && _wearablesById.TryGetValue(id, out var wearable) ? wearable : null;

        /// <summary>Items a player may pick for a slot at a stage. Work uniforms are pickable too; a facility only adds auto-wearing.</summary>
        public List<WearableRule> Choices(string slotId, string stageId)
        {
            var result = new List<WearableRule>();
            foreach (var wearable in _wearables)
                if (wearable.SlotId == slotId && wearable.HasArtFor(stageId)) result.Add(wearable);
            return result;
        }
    }

    public static class WearableRules
    {
        /// <summary>
        /// Adults use the first adult stage. Students use the non-adult stage with the highest minimum day they have reached.
        /// Returns empty when no stage applies.
        /// </summary>
        public static string SelectStage(IReadOnlyList<GrowthStageRule> stages, int studentDay, bool isAdult)
        {
            if (stages == null) return string.Empty;
            GrowthStageRule best = null;
            foreach (var stage in stages)
            {
                if (stage == null) continue;
                if (isAdult)
                {
                    if (stage.RequiresAdult) return stage.Id;
                    continue;
                }

                if (stage.RequiresAdult || stage.MinimumStudentDay > studentDay) continue;
                if (best == null || stage.MinimumStudentDay > best.MinimumStudentDay) best = stage;
            }

            return best != null ? best.Id : string.Empty;
        }

        /// <summary>True when the item exists, belongs to that slot and has art for the stage.</summary>
        public static bool CanEquip(WearableCatalog catalog, string slotId, string wearableId, string stageId)
        {
            var wearable = catalog?.FindWearable(wearableId);
            return wearable != null && catalog.FindSlot(slotId) != null && wearable.SlotId == slotId && wearable.HasArtFor(stageId);
        }

        /// <summary>
        /// The layers to draw for a stage, lowest draw order first. Per slot: the facility uniform for that slot if working
        /// there, else the chosen item, else the slot default. An item without art for the stage falls back to the default;
        /// a default without art leaves the slot empty. Slots hidden by a drawn slot are removed.
        /// </summary>
        public static List<WearableLayer> ResolveLayers(WearableCatalog catalog, WearableState state, string stageId, string facilityId)
        {
            var layers = new List<WearableLayer>();
            if (catalog == null) return layers;
            var hidden = new HashSet<string>(StringComparer.Ordinal);
            var picked = new List<KeyValuePair<WearableSlotRule, string>>();
            foreach (var slot in catalog.Slots)
            {
                string wearableId = Uniform(catalog, slot.Id, stageId, facilityId);
                if (wearableId.Length == 0)
                {
                    string chosen = state != null ? state.Get(slot.Id, stageId) : string.Empty;
                    var chosenItem = catalog.FindWearable(chosen);
                    if (chosenItem != null && chosenItem.SlotId == slot.Id && chosenItem.HasArtFor(stageId)) wearableId = chosen;
                }
                if (wearableId.Length == 0)
                {
                    foreach (string defaultId in slot.DefaultWearableIds)
                    {
                        var fallback = catalog.FindWearable(defaultId);
                        if (fallback != null && fallback.SlotId == slot.Id && fallback.HasArtFor(stageId)) { wearableId = fallback.Id; break; }
                    }
                }
                if (wearableId.Length == 0) continue;
                picked.Add(new KeyValuePair<WearableSlotRule, string>(slot, wearableId));
                foreach (string hiddenSlot in slot.HidesSlotIds) hidden.Add(hiddenSlot);
            }

            foreach (var pair in picked)
                if (!hidden.Contains(pair.Key.Id)) layers.Add(new WearableLayer(pair.Key.Id, pair.Value, pair.Key.DrawOrder));
            return layers;
        }

        /// <summary>
        /// Wears an item chosen at a stage. Items with art for every stage become the slot's general choice (and clear
        /// stage-scoped ones); other items are remembered for that stage only. Returns false and changes nothing if not allowed.
        /// </summary>
        public static bool Choose(WearableCatalog catalog, WearableState state, string slotId, string wearableId, string stageId)
        {
            if (state == null || !CanEquip(catalog, slotId, wearableId, stageId)) return false;
            var wearable = catalog.FindWearable(wearableId);
            bool everyStage = catalog.Stages.Count > 0;
            foreach (var stage in catalog.Stages) if (!wearable.HasArtFor(stage.Id)) { everyStage = false; break; }
            if (everyStage)
            {
                state.Set(slotId, wearableId);
                foreach (var stage in catalog.Stages) state.Set(WearableState.StageKey(slotId, stage.Id), string.Empty);
            }
            else state.Set(WearableState.StageKey(slotId, stageId), wearableId);
            return true;
        }

        private static string Uniform(WearableCatalog catalog, string slotId, string stageId, string facilityId)
        {
            if (string.IsNullOrEmpty(facilityId)) return string.Empty;
            foreach (var wearable in catalog.Wearables)
                if (wearable.SlotId == slotId && wearable.AutoEquipFacilityId == facilityId && wearable.HasArtFor(stageId)) return wearable.Id;
            return string.Empty;
        }

        /// <summary>
        /// One-time move from the old per-body outfit choices (wardrobe file and creator snapshot) into slot choices.
        /// Each old look id maps to the item that replaces it; the item's own slot and stages decide the key, as if the
        /// player had chosen it at each stage it has art for. Choices already in <paramref name="current"/> win.
        /// </summary>
        public static WearableState MigrateLegacyChoices(WearableCatalog catalog, WearableState current, IEnumerable<string> legacyLookIds,
            IReadOnlyDictionary<string, string> wearableIdByLegacyLook)
        {
            var state = WearableState.FromEntries(current != null ? current.ToEntries() : null);
            if (catalog == null || legacyLookIds == null || wearableIdByLegacyLook == null) return state;
            foreach (string lookId in legacyLookIds)
            {
                if (lookId == null || !wearableIdByLegacyLook.TryGetValue(lookId, out string wearableId)) continue;
                var wearable = catalog.FindWearable(wearableId);
                if (wearable == null) continue;
                foreach (var stage in catalog.Stages)
                {
                    if (!wearable.HasArtFor(stage.Id) || state.Get(wearable.SlotId, stage.Id).Length > 0) continue;
                    Choose(catalog, state, wearable.SlotId, wearable.Id, stage.Id);
                }
            }

            return state;
        }
    }
}
