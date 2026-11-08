using System;
using System.Collections.Generic;
using Rootborn.Core.Characters;
using Rootborn.Game.Characters.Growing;
using Rootborn.Game.Common;

namespace Rootborn.Game.Characters.Wearables
{
    /// <summary>What to draw: the stage, its body sheet and the wearable sheets in draw order.</summary>
    public sealed class WearableLook
    {
        public WearableLook(string stageId, GrowingCharacterSheetDefinition body, IReadOnlyList<GrowingCharacterSheetDefinition> layers,
            IReadOnlyList<string> wearableIds)
        {
            StageId = stageId ?? string.Empty;
            Body = body;
            Layers = layers ?? Array.Empty<GrowingCharacterSheetDefinition>();
            WearableIds = wearableIds ?? Array.Empty<string>();
        }

        public string StageId { get; }
        public GrowingCharacterSheetDefinition Body { get; }
        public IReadOnlyList<GrowingCharacterSheetDefinition> Layers { get; }
        public IReadOnlyList<string> WearableIds { get; }

        public bool SameAs(WearableLook other)
        {
            if (other == null || other.StageId != StageId || other.Body != Body || other.Layers.Count != Layers.Count) return false;
            for (int i = 0; i < Layers.Count; i++)
                if (other.Layers[i] != Layers[i]) return false;
            return true;
        }
    }

    /// <summary>
    /// Reads the registry's stage, slot and wearable data once and answers what to draw. The decisions (which stage,
    /// which item per slot, uniforms, hidden slots) are Rootborn.Core's WearableRules; this only maps ids back to art.
    /// </summary>
    public sealed class WearableLookResolver
    {
        private readonly Dictionary<string, WearableDefinition> _wearablesById;
        private readonly Dictionary<string, CharacterGrowthStageDefinition> _stagesById = new Dictionary<string, CharacterGrowthStageDefinition>(StringComparer.Ordinal);

        public WearableLookResolver(GameDataRegistry registry)
            : this(registry != null ? registry.WearableSlots : null, registry != null ? registry.Wearables : null,
                registry != null ? registry.GrowthStages : null)
        {
        }

        public WearableLookResolver(IEnumerable<WearableSlotDefinition> slots, IEnumerable<WearableDefinition> wearables,
            IEnumerable<CharacterGrowthStageDefinition> stages)
        {
            Catalog = WearableCatalogFactory.Build(slots, wearables, stages);
            _wearablesById = WearableCatalogFactory.ById(wearables);
            if (stages == null) return;
            foreach (var stage in stages)
                if (stage != null && !string.IsNullOrEmpty(stage.Id) && !_stagesById.ContainsKey(stage.Id)) _stagesById.Add(stage.Id, stage);
        }

        public WearableCatalog Catalog { get; }
        public bool HasData => Catalog.Stages.Count > 0 && Catalog.Slots.Count > 0;

        public string StageFor(int studentDay, bool isAdult) => WearableRules.SelectStage(Catalog.Stages, studentDay, isAdult);

        public CharacterGrowthStageDefinition FindStage(string stageId)
            => stageId != null && _stagesById.TryGetValue(stageId, out var stage) ? stage : null;

        public WearableDefinition FindWearable(string wearableId)
            => wearableId != null && _wearablesById.TryGetValue(wearableId, out var wearable) ? wearable : null;

        /// <summary>The look for a student day / adulthood and workplace, or null when no stage applies.</summary>
        public WearableLook Resolve(WearableState state, int studentDay, bool isAdult, string facilityId)
            => ResolveStage(state, StageFor(studentDay, isAdult), facilityId);

        public WearableLook ResolveStage(WearableState state, string stageId, string facilityId)
        {
            var stage = FindStage(stageId);
            if (stage == null) return null;
            var layers = new List<GrowingCharacterSheetDefinition>();
            var ids = new List<string>();
            foreach (var layer in WearableRules.ResolveLayers(Catalog, state ?? new WearableState(), stage.Id, facilityId ?? string.Empty))
            {
                var sheet = FindWearable(layer.WearableId)?.SheetFor(stage.Id);
                if (sheet == null) continue;
                layers.Add(sheet);
                ids.Add(layer.WearableId);
            }

            return new WearableLook(stage.Id, stage.Body, layers, ids);
        }

        /// <summary>
        /// Maps each legacy appearance id to the wearable that draws its outfit at the stage with its body, computed
        /// from the data (no ids in code). Used once to move old wardrobe and creator choices.
        /// </summary>
        public Dictionary<string, string> LegacyAppearanceMap(IEnumerable<CharacterAppearanceDefinition> appearances)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            if (appearances == null) return map;
            foreach (var definition in appearances)
            {
                if (!(definition is GrowingCharacterAppearanceDefinition appearance) || string.IsNullOrEmpty(appearance.Id) || appearance.Outfit == null) continue;
                foreach (var stage in _stagesById.Values)
                {
                    if (stage.Body != appearance.Body) continue;
                    foreach (var wearable in _wearablesById.Values)
                    {
                        if (wearable.SheetFor(stage.Id) != appearance.Outfit) continue;
                        map[appearance.Id] = wearable.Id;
                        break;
                    }
                }
            }

            return map;
        }
    }
}
