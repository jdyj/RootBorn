using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Game.Family;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Family
{
    public sealed class CharacterPartComposerTests
    {
        [Test]
        public void EnsureLayers_CreatesOneNamedSpriteRendererPerCategoryWithSortingOrder()
        {
            var player = new GameObject("Player");
            try
            {
                var composer = player.AddComponent<CharacterPartComposer>();
                var definitions = new[]
                {
                    CreateDefinition("character.body.01", "body", 0),
                    CreateDefinition("character.eyes.blue", "eyes", 3),
                    CreateDefinition("character.hair.short.blonde", "hair", 4),
                    CreateDefinition("character.outfit.braces.brown", "outfit", 2),
                    CreateDefinition("character.accessory.bamboo.brown", "accessory", 5),
                };

                composer.EnsureLayers(definitions);

                AssertLayer(player, "Part_body", 0);
                AssertLayer(player, "Part_eyes", 3);
                AssertLayer(player, "Part_hair", 4);
                AssertLayer(player, "Part_outfit", 2);
                AssertLayer(player, "Part_accessory", 5);
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void SetFlipX_AppliesToAllPartRenderers()
        {
            var player = new GameObject("Player");
            try
            {
                var composer = player.AddComponent<CharacterPartComposer>();
                composer.EnsureLayers(new[]
                {
                    CreateDefinition("character.body.01", "body", 0),
                    CreateDefinition("character.outfit.braces.brown", "outfit", 2),
                });

                composer.SetFlipX(true);

                Assert.IsTrue(player.transform.Find("Part_body").GetComponent<SpriteRenderer>().flipX);
                Assert.IsTrue(player.transform.Find("Part_outfit").GetComponent<SpriteRenderer>().flipX);
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void ApplySprites_AssignsSelectedAppearanceSpritesToMatchingLayers()
        {
            var player = new GameObject("Player");
            var texture = new Texture2D(16, 16);
            var bodySprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16);
            var outfitSprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16);
            try
            {
                var body = CreateDefinition("character.body.01", "body", 0);
                var outfit = CreateDefinition("character.outfit.braces.brown", "outfit", 2);
                var composer = player.AddComponent<CharacterPartComposer>();
                var definitions = new[] { body, outfit };
                composer.EnsureLayers(definitions);
                var appearance = new CharacterAppearance();
                appearance.SetSelectedPart("body", "character.body.01");
                appearance.SetSelectedPart("outfit", "character.outfit.braces.brown");

                composer.ApplySprites(appearance, definitions, definition => definition == body ? bodySprite : outfitSprite);

                Assert.AreSame(bodySprite, player.transform.Find("Part_body").GetComponent<SpriteRenderer>().sprite);
                Assert.AreSame(outfitSprite, player.transform.Find("Part_outfit").GetComponent<SpriteRenderer>().sprite);
            }
            finally
            {
                Object.DestroyImmediate(bodySprite);
                Object.DestroyImmediate(outfitSprite);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void BuildFrameSubSpriteName_UsesDefinitionBaseNameAndRequestedGridCell()
        {
            var definition = CreateDefinition("character.hair.short.brown_dark", "hair", 4, "Hairstyle_Short_Brown_Dark_r0_c0");

            string subSpriteName = CharacterPartComposer.BuildFrameSubSpriteName(definition, 6, 12);

            Assert.AreEqual("Hairstyle_Short_Brown_Dark_r6_c12", subSpriteName);
        }

        [Test]
        public void ApplyAnimationFrame_AssignsSameGridCellAcrossSelectedPartLayers()
        {
            var player = new GameObject("Player");
            var texture = new Texture2D(16, 16);
            var sprites = new Dictionary<string, Sprite>();
            try
            {
                var body = CreateDefinition("character.body.02", "body", 0, "Body_2_r0_c0");
                var outfit = CreateDefinition("character.outfit.braces.green", "outfit", 2, "Outfit_Braces_Green_r0_c0");
                var definitions = new[] { body, outfit };
                var appearance = new CharacterAppearance();
                appearance.SetSelectedPart("body", "character.body.02");
                appearance.SetSelectedPart("outfit", "character.outfit.braces.green");
                sprites["Body_2_r1_c4"] = Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16);
                sprites["Outfit_Braces_Green_r1_c4"] = Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16);

                var composer = player.AddComponent<CharacterPartComposer>();
                composer.EnsureLayers(definitions);

                composer.ApplyAnimationFrame(appearance, definitions, 1, 4, (definition, subSpriteName) => sprites[subSpriteName]);

                Assert.AreSame(sprites["Body_2_r1_c4"], player.transform.Find("Part_body").GetComponent<SpriteRenderer>().sprite);
                Assert.AreSame(sprites["Outfit_Braces_Green_r1_c4"], player.transform.Find("Part_outfit").GetComponent<SpriteRenderer>().sprite);
            }
            finally
            {
                foreach (var sprite in sprites.Values)
                {
                    Object.DestroyImmediate(sprite);
                }
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void ApplyAnimationFrame_FallsBackToDefinitionPreviewCellWhenRequestedCellIsMissing()
        {
            var player = new GameObject("Player");
            var texture = new Texture2D(16, 16);
            var fallbackSprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16);
            try
            {
                var body = CreateDefinition("character.body.02", "body", 0, "Body_2_r0_c0");
                var appearance = new CharacterAppearance();
                appearance.SetSelectedPart("body", "character.body.02");
                var composer = player.AddComponent<CharacterPartComposer>();
                composer.EnsureLayers(new[] { body });

                composer.ApplyAnimationFrame(
                    appearance,
                    new[] { body },
                    10,
                    20,
                    (definition, subSpriteName) => subSpriteName == definition.SubSpriteName ? fallbackSprite : null);

                Assert.AreSame(fallbackSprite, player.transform.Find("Part_body").GetComponent<SpriteRenderer>().sprite);
            }
            finally
            {
                Object.DestroyImmediate(fallbackSprite);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(player);
            }
        }

        private static void AssertLayer(GameObject root, string childName, int sortingOrder)
        {
            var child = root.transform.Find(childName);
            Assert.IsNotNull(child, childName);
            var renderer = child.GetComponent<SpriteRenderer>();
            Assert.IsNotNull(renderer, childName);
            Assert.AreEqual(sortingOrder, renderer.sortingOrder, childName);
            Assert.IsTrue(renderer.enabled, childName);
        }

        private static CharacterPartDefinition CreateDefinition(string id, string categoryId, int layerOrder)
        {
            return CreateDefinition(id, categoryId, layerOrder, id + "_r0_c0");
        }

        private static CharacterPartDefinition CreateDefinition(string id, string categoryId, int layerOrder, string subSpriteName)
        {
            var definition = ScriptableObject.CreateInstance<CharacterPartDefinition>();
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_id").stringValue = id;
            serialized.FindProperty("_categoryId").stringValue = categoryId;
            serialized.FindProperty("_displayNameKey").stringValue = "loc." + id;
            serialized.FindProperty("_sheetAddress").stringValue = "sprites/character/" + categoryId + "/" + id;
            serialized.FindProperty("_subSpriteName").stringValue = subSpriteName;
            serialized.FindProperty("_layerOrder").intValue = layerOrder;
            serialized.FindProperty("_isDefault").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }
    }
}
