using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Characters;
using Rootborn.Game.Characters.Spum;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Characters.Spum
{
    public sealed class SpumAppearanceDefinitionTests
    {
        [Test]
        public void SPUM_APPEARANCE_001_SnapshotSerializesStableAppearanceFieldsAndSelectedParts()
        {
            var snapshot = new CharacterAppearanceSnapshot(
                schemaVersion: 2,
                visualKind: "spum",
                appearanceDefinitionId: "appearance.student.default",
                catalogId: "spum.catalog.student",
                selectedParts: new[]
                {
                    new CharacterAppearancePartSelection("body", "spum.body.default"),
                    new CharacterAppearancePartSelection("hair", "spum.hair.short")
                });

            string json = JsonUtility.ToJson(snapshot);

            StringAssert.Contains("\"schemaVersion\":2", json);
            StringAssert.Contains("\"visualKind\":\"spum\"", json);
            StringAssert.Contains("\"appearanceDefinitionId\":\"appearance.student.default\"", json);
            StringAssert.Contains("\"catalogId\":\"spum.catalog.student\"", json);
            StringAssert.Contains("\"categoryId\":\"body\"", json);
            StringAssert.Contains("\"partId\":\"spum.hair.short\"", json);
        }

        [Test]
        public void SPUM_APPEARANCE_002_CatalogResolvesSelectedPartByStableId()
        {
            var bodyDefault = CreatePart("spum.body.default", "body", true);
            var bodyTall = CreatePart("spum.body.tall", "body", false);
            var catalog = CreateCatalog("spum.catalog.student", bodyDefault, bodyTall);
            var snapshot = new CharacterAppearanceSnapshot(1, "spum", "appearance.student", catalog.Id, new[]
            {
                new CharacterAppearancePartSelection("body", bodyTall.StableId)
            });

            SpumPartDefinition resolved = catalog.ResolveSelectedPart(snapshot, "body");

            Assert.AreSame(bodyTall, resolved);
        }

        [Test]
        public void SPUM_APPEARANCE_003_MissingSavedPartFallsBackToCategoryDefault()
        {
            var bodyDefault = CreatePart("spum.body.default", "body", true);
            var bodyTall = CreatePart("spum.body.tall", "body", false);
            var catalog = CreateCatalog("spum.catalog.student", bodyDefault, bodyTall);
            var snapshot = new CharacterAppearanceSnapshot(1, "spum", "appearance.student", catalog.Id, new[]
            {
                new CharacterAppearancePartSelection("body", "spum.body.deleted")
            });

            SpumPartDefinition resolved = catalog.ResolveSelectedPart(snapshot, "body");

            Assert.AreSame(bodyDefault, resolved);
        }

        [Test]
        public void SPUM_APPEARANCE_004_MissingCategoryReturnsNullWithoutChangingSnapshot()
        {
            var catalog = CreateCatalog("spum.catalog.student", CreatePart("spum.body.default", "body", true));
            var snapshot = new CharacterAppearanceSnapshot(1, "spum", "appearance.student", catalog.Id, new[]
            {
                new CharacterAppearancePartSelection("wings", "spum.wings.none")
            });
            string beforeJson = JsonUtility.ToJson(snapshot);

            SpumPartDefinition resolved = catalog.ResolveSelectedPart(snapshot, "hair");

            Assert.IsNull(resolved);
            Assert.AreEqual(beforeJson, JsonUtility.ToJson(snapshot));
        }

        [Test]
        public void SPUM_APPEARANCE_005_PresetProducesRequiredDefaultsWhenCatalogContainsThem()
        {
            var catalog = CreateCatalog(
                "spum.catalog.student",
                CreatePart("spum.body.default", "body", true),
                CreatePart("spum.skin.default", "skin", true),
                CreatePart("spum.eyes.default", "eyes", true),
                CreatePart("spum.hair.default", "hair", true),
                CreatePart("spum.outfit.default", "outfit", true),
                CreatePart("spum.accessory.default", "accessory", true));
            var appearance = ScriptableObject.CreateInstance<SpumAppearanceDefinition>();
            appearance.ConfigureForTests("appearance.student.default", "spum", catalog);
            var preset = ScriptableObject.CreateInstance<SpumCharacterCreatorPresetDefinition>();
            preset.ConfigureForTests("preset.student.default", appearance, new[] { "body", "skin", "eyes", "hair", "outfit" });

            CharacterAppearanceSnapshot snapshot = preset.CreateDefaultSnapshot();
            string[] categories = snapshot.SelectedParts.Select(part => part.CategoryId).ToArray();

            CollectionAssert.AreEquivalent(new[] { "body", "skin", "eyes", "hair", "outfit" }, categories);
            Assert.AreEqual("spum.catalog.student", snapshot.CatalogId);
            Assert.AreEqual("appearance.student.default", snapshot.AppearanceDefinitionId);
            Assert.AreEqual("spum", snapshot.VisualKind);
        }

        private static SpumPartCatalogDefinition CreateCatalog(string id, params SpumPartDefinition[] parts)
        {
            var catalog = ScriptableObject.CreateInstance<SpumPartCatalogDefinition>();
            catalog.ConfigureForTests(id, parts);
            return catalog;
        }

        private static SpumPartDefinition CreatePart(string stableId, string categoryId, bool isDefault)
        {
            var part = ScriptableObject.CreateInstance<SpumPartDefinition>();
            part.ConfigureForTests(stableId, categoryId, isDefault);
            return part;
        }
    }
}
