using NUnit.Framework;
using Rootborn.Game.Family;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Family
{
    public sealed class CharacterPartDefinitionTests
    {
        [Test]
        public void Definition_ExposesStableDataFieldsWithoutUnityObjectSaveReference()
        {
            var definition = CreateDefinition(
                "character.body.01",
                "body",
                "loc.character.body.01",
                "sprites/character/body/body-1",
                "Body_1_r0_c0",
                0,
                true);

            Assert.AreEqual("character.body.01", definition.Id);
            Assert.AreEqual("body", definition.CategoryId);
            Assert.AreEqual("loc.character.body.01", definition.DisplayNameKey);
            Assert.AreEqual("sprites/character/body/body-1", definition.SheetAddress);
            Assert.AreEqual("Body_1_r0_c0", definition.SubSpriteName);
            Assert.AreEqual(0, definition.LayerOrder);
            Assert.IsTrue(definition.IsDefault);
            Assert.IsTrue(definition.IsValid(out var error), error);
        }

        [Test]
        public void Definition_IsInvalidWhenRequiredStableSpriteKeysAreMissing()
        {
            var definition = CreateDefinition(
                string.Empty,
                "body",
                "loc.character.body.01",
                "sprites/character/body/body-1",
                string.Empty,
                0,
                true);

            Assert.IsFalse(definition.IsValid(out var error));
            StringAssert.Contains("id", error.ToLowerInvariant());
            StringAssert.Contains("sub", error.ToLowerInvariant());
        }

        private static CharacterPartDefinition CreateDefinition(
            string id,
            string categoryId,
            string displayNameKey,
            string sheetAddress,
            string subSpriteName,
            int layerOrder,
            bool isDefault)
        {
            var definition = ScriptableObject.CreateInstance<CharacterPartDefinition>();
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_id").stringValue = id;
            serialized.FindProperty("_categoryId").stringValue = categoryId;
            serialized.FindProperty("_displayNameKey").stringValue = displayNameKey;
            serialized.FindProperty("_sheetAddress").stringValue = sheetAddress;
            serialized.FindProperty("_subSpriteName").stringValue = subSpriteName;
            serialized.FindProperty("_layerOrder").intValue = layerOrder;
            serialized.FindProperty("_isDefault").boolValue = isDefault;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }
    }
}
