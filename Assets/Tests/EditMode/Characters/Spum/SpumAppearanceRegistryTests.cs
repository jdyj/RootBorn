using NUnit.Framework;
using Rootborn.Game.Characters;
using Rootborn.Game.Characters.Spum;
using Rootborn.Game.Common;
using Rootborn.Game.Save;
using Rootborn.Game.Tools;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Characters.Spum
{
    public sealed class SpumAppearanceRegistryTests
    {
        [Test]
        public void SPUM_REGISTRY_001_GameDataRegistryExposesSpumAppearanceCatalogsPresetsAndToolMappings()
        {
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();

            Assert.IsNotNull(registry.CharacterAppearances);
            Assert.IsNotNull(registry.SpumPartCatalogs);
            Assert.IsNotNull(registry.SpumCharacterCreatorPresets);
            Assert.IsNotNull(registry.ToolVisualMappings);
            Assert.AreEqual(0, registry.CharacterAppearances.Length);
            Assert.AreEqual(0, registry.SpumPartCatalogs.Length);
            Assert.AreEqual(0, registry.SpumCharacterCreatorPresets.Length);
            Assert.AreEqual(0, registry.ToolVisualMappings.Length);
        }

        [Test]
        public void SPUM_REGISTRY_002_SaveSlotMetadataRoundTripsAppearanceSnapshotWithoutRemovingLegacyAppearance()
        {
            var metadata = new SaveSlotMetadata
            {
                SlotId = "slot-spum",
                DisplayName = "SPUM Slot",
                CharacterAppearanceSnapshot = new CharacterAppearanceSnapshot(
                    schemaVersion: 1,
                    visualKind: "spum",
                    appearanceDefinitionId: "appearance.student.default",
                    catalogId: "spum.catalog.student",
                    selectedParts: new[]
                    {
                        new CharacterAppearancePartSelection("body", "spum.body.default"),
                        new CharacterAppearancePartSelection("hair", "spum.hair.short")
                    })
            };

            string json = JsonUtility.ToJson(metadata);
            var roundTripped = JsonUtility.FromJson<SaveSlotMetadata>(json);

            Assert.IsNotNull(roundTripped.Appearance, "Legacy Pixelwood appearance must remain for migration and rollback.");
            Assert.IsNotNull(roundTripped.CharacterAppearanceSnapshot);
            Assert.AreEqual("spum", roundTripped.CharacterAppearanceSnapshot.VisualKind);
            Assert.AreEqual("appearance.student.default", roundTripped.CharacterAppearanceSnapshot.AppearanceDefinitionId);
            Assert.AreEqual("spum.catalog.student", roundTripped.CharacterAppearanceSnapshot.CatalogId);
            Assert.AreEqual("spum.hair.short", roundTripped.CharacterAppearanceSnapshot.GetSelectedPartId("hair"));
        }

        [Test]
        public void SPUM_REGISTRY_003_RegistryPropertiesUseDefinitionArrays()
        {
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();

            Assert.IsInstanceOf<CharacterAppearanceDefinition[]>(registry.CharacterAppearances);
            Assert.IsInstanceOf<SpumPartCatalogDefinition[]>(registry.SpumPartCatalogs);
            Assert.IsInstanceOf<SpumCharacterCreatorPresetDefinition[]>(registry.SpumCharacterCreatorPresets);
            Assert.IsInstanceOf<ToolVisualMappingDefinition[]>(registry.ToolVisualMappings);
        }
    }
}
