using NUnit.Framework;
using Rootborn.Game.Family;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Family
{
    public sealed class CharacterAppearanceTests
    {
        [Test]
        public void Appearance_StoresStablePartIdsByCategoryAndRoundTripsThroughJson()
        {
            var appearance = new CharacterAppearance();
            appearance.SetSelectedPart("body", "character.body.01");
            appearance.SetSelectedPart("hair", "character.hair.short.blonde");
            appearance.SetSelectedPart("body", "character.body.02");

            Assert.AreEqual("character.body.02", appearance.GetSelectedPartId("body"));
            Assert.AreEqual("character.hair.short.blonde", appearance.GetSelectedPartId("hair"));

            string json = JsonUtility.ToJson(appearance);
            StringAssert.Contains("character.body.02", json);
            Assert.IsFalse(json.Contains("UnityEngine.Object"));

            var loaded = JsonUtility.FromJson<CharacterAppearance>(json);
            Assert.AreEqual("character.body.02", loaded.GetSelectedPartId("body"));
            Assert.AreEqual("character.hair.short.blonde", loaded.GetSelectedPartId("hair"));
        }

        [Test]
        public void ResolveWithDefaults_FillsMissingCategoriesWithoutMutatingSavedAppearance()
        {
            var saved = new CharacterAppearance();
            saved.SetSelectedPart("body", "deleted.body");
            saved.SetSelectedPart("hair", "character.hair.short.blonde");

            var definitions = new[]
            {
                CreateDefinition("character.body.01", "body", true),
                CreateDefinition("character.eyes.blue", "eyes", true),
                CreateDefinition("character.hair.short.blonde", "hair", true),
                CreateDefinition("character.outfit.braces.brown", "outfit", true),
                CreateDefinition("character.accessory.bamboo.brown", "accessory", true),
            };

            var resolved = CharacterAppearance.ResolveWithDefaults(saved, definitions);

            Assert.AreEqual("deleted.body", saved.GetSelectedPartId("body"));
            Assert.AreEqual("character.body.01", resolved.GetSelectedPartId("body"));
            Assert.AreEqual("character.eyes.blue", resolved.GetSelectedPartId("eyes"));
            Assert.AreEqual("character.hair.short.blonde", resolved.GetSelectedPartId("hair"));
            Assert.AreEqual("character.outfit.braces.brown", resolved.GetSelectedPartId("outfit"));
            Assert.AreEqual("character.accessory.bamboo.brown", resolved.GetSelectedPartId("accessory"));
        }

        private static CharacterPartDefinition CreateDefinition(string id, string categoryId, bool isDefault)
        {
            var definition = ScriptableObject.CreateInstance<CharacterPartDefinition>();
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_id").stringValue = id;
            serialized.FindProperty("_categoryId").stringValue = categoryId;
            serialized.FindProperty("_displayNameKey").stringValue = "loc." + id;
            serialized.FindProperty("_sheetAddress").stringValue = "sprites/character/" + categoryId + "/" + id;
            serialized.FindProperty("_subSpriteName").stringValue = id + "_r0_c0";
            serialized.FindProperty("_layerOrder").intValue = 0;
            serialized.FindProperty("_isDefault").boolValue = isDefault;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }
    }
}
