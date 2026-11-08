using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Core.Characters;
using Rootborn.Game.Characters.Growing;
using Rootborn.Game.Characters.Wearables;
using Rootborn.Game.Common;
using UnityEditor;

namespace Rootborn.Tests.EditMode.Characters
{
    /// <summary>The wearable data reproduces every legacy appearance and is wired into both registries.</summary>
    public sealed class WearableDataWiringTests
    {
        private const string RegistryPath = "Assets/Data/Registry/GameDataRegistry.asset";
        private const string ResourcesRegistryPath = "Assets/Resources/GameDataRegistry.asset";

        private static GameDataRegistry Registry(string path)
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(path);
            Assert.That(registry, Is.Not.Null, path);
            return registry;
        }

        [Test]
        public void WEAR_DATA_001_EveryWearableHasARegisteredSlotAndArtForRegisteredStages()
        {
            var registry = Registry(RegistryPath);
            Assert.That(registry.GrowthStages, Is.Not.Empty);
            Assert.That(registry.WearableSlots, Is.Not.Empty);
            Assert.That(registry.Wearables, Is.Not.Empty);
            var stages = new HashSet<CharacterGrowthStageDefinition>(registry.GrowthStages);
            var slots = new HashSet<WearableSlotDefinition>(registry.WearableSlots);
            var ids = new HashSet<string>();
            foreach (var stage in registry.GrowthStages)
            {
                Assert.That(stage, Is.Not.Null);
                Assert.That(ids.Add(stage.Id), Is.True, "duplicate id " + stage.Id);
                Assert.That(stage.Body, Is.Not.Null, stage.name + " needs body art");
            }

            foreach (var slot in registry.WearableSlots)
            {
                Assert.That(slot, Is.Not.Null);
                Assert.That(ids.Add(slot.Id), Is.True, "duplicate id " + slot.Id);
                foreach (var fallback in slot.DefaultWearables) Assert.That(fallback, Is.Not.Null, slot.name + " default");
            }

            foreach (var wearable in registry.Wearables)
            {
                Assert.That(wearable, Is.Not.Null);
                Assert.That(ids.Add(wearable.Id), Is.True, "duplicate id " + wearable.Id);
                Assert.That(slots.Contains(wearable.Slot), Is.True, wearable.name + " slot must be registered");
                Assert.That(wearable.Art, Is.Not.Empty, wearable.name + " needs art");
                foreach (var art in wearable.Art)
                {
                    Assert.That(stages.Contains(art.Stage), Is.True, wearable.name + " art stage must be registered");
                    Assert.That(art.Sheet, Is.Not.Null, wearable.name + " art sheet");
                }
            }
        }

        [Test]
        public void WEAR_DATA_002_EveryLegacyAppearanceIsReproducedByStageAndWearables()
        {
            var registry = Registry(RegistryPath);
            var catalog = WearableCatalogFactory.Build(registry.WearableSlots, registry.Wearables, registry.GrowthStages);
            var wearablesById = WearableCatalogFactory.ById(registry.Wearables);
            int checkedAppearances = 0;
            foreach (var definition in registry.CharacterAppearances)
            {
                if (!(definition is GrowingCharacterAppearanceDefinition appearance)) continue;
                checkedAppearances++;
                CharacterGrowthStageDefinition stage = null;
                foreach (var candidate in registry.GrowthStages)
                    if (candidate.Body == appearance.Body) stage = candidate;
                Assert.That(stage, Is.Not.Null, appearance.name + ": a stage must draw its body");
                Assert.That(stage.MinimumStudentDay, Is.EqualTo(appearance.MinimumStudentDay), appearance.name);
                Assert.That(stage.RequiresAdult, Is.EqualTo(appearance.RequiresAdult), appearance.name);

                WearableDefinition outfit = null;
                foreach (var wearable in registry.Wearables)
                    if (wearable.SheetFor(stage.Id) == appearance.Outfit) outfit = wearable;
                Assert.That(outfit, Is.Not.Null, appearance.name + ": a wearable must draw its outfit");
                Assert.That(outfit.AutoEquipFacilityId ?? string.Empty, Is.EqualTo(appearance.FacilityId ?? string.Empty), appearance.name);

                var state = new WearableState();
                string facility = string.IsNullOrEmpty(appearance.FacilityId) ? string.Empty : appearance.FacilityId;
                if (facility.Length == 0) Assert.That(WearableRules.Choose(catalog, state, outfit.Slot.Id, outfit.Id, stage.Id), Is.True, appearance.name);

                var drawn = new List<GrowingCharacterSheetDefinition>();
                foreach (var layer in WearableRules.ResolveLayers(catalog, state, stage.Id, facility))
                    drawn.Add(wearablesById[layer.WearableId].SheetFor(stage.Id));
                Assert.That(drawn, Is.EqualTo(new[] { appearance.Outfit, appearance.Head }), appearance.name + ": outfit then head, over the body");
            }

            Assert.That(checkedAppearances, Is.EqualTo(7));
        }

        [Test]
        public void WEAR_DATA_003_StudentDayAndAdulthoodSelectTheLegacyStages()
        {
            var registry = Registry(RegistryPath);
            var catalog = WearableCatalogFactory.Build(registry.WearableSlots, registry.Wearables, registry.GrowthStages);
            string StageBody(int day, bool adult)
            {
                string id = WearableRules.SelectStage(catalog.Stages, day, adult);
                foreach (var stage in registry.GrowthStages) if (stage.Id == id) return stage.Body.name;
                return "none";
            }

            Assert.That(StageBody(1, false), Is.EqualTo("Sheet_body_playground"));
            Assert.That(StageBody(2, false), Is.EqualTo("Sheet_body_school"));
            Assert.That(StageBody(30, false), Is.EqualTo("Sheet_body_school"));
            Assert.That(StageBody(1, true), Is.EqualTo("Sheet_body_adult"));
        }

        [Test]
        public void WEAR_DATA_004_BothRegistriesListTheSameWearableData()
        {
            var main = Registry(RegistryPath);
            var resources = Registry(ResourcesRegistryPath);
            Assert.That(resources.GrowthStages, Is.EqualTo(main.GrowthStages));
            Assert.That(resources.WearableSlots, Is.EqualTo(main.WearableSlots));
            Assert.That(resources.Wearables, Is.EqualTo(main.Wearables));
        }
    }
}
