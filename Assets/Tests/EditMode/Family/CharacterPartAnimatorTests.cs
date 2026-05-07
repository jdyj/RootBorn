using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Game.Family;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Family
{
    public sealed class CharacterPartAnimatorTests
    {
        [Test]
        public void Tick_WhenIdleDown_AppliesDownIdleFrameToAllLayers()
        {
            var player = new GameObject("Player");
            var texture = new Texture2D(16, 16);
            var sprites = new Dictionary<string, Sprite>();
            try
            {
                var body = CreateDefinition("character.body.02", "body", 0, "Body_2_r0_c0");
                var outfit = CreateDefinition("character.outfit.braces.green", "outfit", 2, "Outfit_Braces_Green_r0_c0");
                sprites["Body_2_r0_c0"] = Sprite.Create(texture, new Rect(0, 0, 16, 16), Vector2.one * 0.5f, 16);
                sprites["Outfit_Braces_Green_r0_c0"] = Sprite.Create(texture, new Rect(0, 0, 16, 16), Vector2.one * 0.5f, 16);
                var appearance = new CharacterAppearance();
                appearance.SetSelectedPart("body", "character.body.02");
                appearance.SetSelectedPart("outfit", "character.outfit.braces.green");

                var composer = player.AddComponent<CharacterPartComposer>();
                var animator = player.AddComponent<CharacterPartAnimator>();
                animator.Configure(composer, appearance, new[] { body, outfit }, (definition, subSpriteName) => Resolve(sprites, subSpriteName));
                animator.SetMotion(Vector2.zero, new Vector2(0f, -1f));

                animator.Tick(0.5f);

                Assert.AreSame(sprites["Body_2_r0_c0"], player.transform.Find("Part_body").GetComponent<SpriteRenderer>().sprite);
                Assert.AreSame(sprites["Outfit_Braces_Green_r0_c0"], player.transform.Find("Part_outfit").GetComponent<SpriteRenderer>().sprite);
            }
            finally
            {
                foreach (var sprite in sprites.Values) Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void Tick_WhenWalkingSide_AdvancesColumnOverTimeAndUsesSideRow()
        {
            var player = new GameObject("Player");
            var texture = new Texture2D(16, 16);
            var sprites = new Dictionary<string, Sprite>();
            try
            {
                var body = CreateDefinition("character.body.02", "body", 0, "Body_2_r0_c0");
                sprites["Body_2_r1_c2"] = Sprite.Create(texture, new Rect(0, 0, 16, 16), Vector2.one * 0.5f, 16);
                var appearance = new CharacterAppearance();
                appearance.SetSelectedPart("body", "character.body.02");

                var composer = player.AddComponent<CharacterPartComposer>();
                var animator = player.AddComponent<CharacterPartAnimator>();
                animator.Configure(composer, appearance, new[] { body }, (definition, subSpriteName) => Resolve(sprites, subSpriteName));
                animator.FramesPerSecond = 8f;
                animator.WalkFrameCount = 4;
                animator.SetMotion(new Vector2(1f, 0f), new Vector2(1f, 0f));

                animator.Tick(0.25f);

                Assert.AreSame(sprites["Body_2_r1_c2"], player.transform.Find("Part_body").GetComponent<SpriteRenderer>().sprite);
            }
            finally
            {
                foreach (var sprite in sprites.Values) Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void SetMotion_WhenDirectionChanges_ResetsWalkFrameTimer()
        {
            var player = new GameObject("Player");
            var texture = new Texture2D(16, 16);
            var sprites = new Dictionary<string, Sprite>();
            try
            {
                var body = CreateDefinition("character.body.02", "body", 0, "Body_2_r0_c0");
                sprites["Body_2_r2_c0"] = Sprite.Create(texture, new Rect(0, 0, 16, 16), Vector2.one * 0.5f, 16);
                var appearance = new CharacterAppearance();
                appearance.SetSelectedPart("body", "character.body.02");

                var composer = player.AddComponent<CharacterPartComposer>();
                var animator = player.AddComponent<CharacterPartAnimator>();
                animator.Configure(composer, appearance, new[] { body }, (definition, subSpriteName) => Resolve(sprites, subSpriteName));
                animator.FramesPerSecond = 8f;
                animator.WalkFrameCount = 4;
                animator.SetMotion(new Vector2(1f, 0f), new Vector2(1f, 0f));
                animator.Tick(0.25f);

                animator.SetMotion(new Vector2(0f, 1f), new Vector2(0f, 1f));
                animator.Tick(0f);

                Assert.AreSame(sprites["Body_2_r2_c0"], player.transform.Find("Part_body").GetComponent<SpriteRenderer>().sprite);
            }
            finally
            {
                foreach (var sprite in sprites.Values) Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void PlayClip_UsesDataDrivenRowFramesAndBlocksMotionUntilNonLoopingClipCompletes()
        {
            var player = new GameObject("Player");
            var texture = new Texture2D(16, 16);
            var sprites = new Dictionary<string, Sprite>();
            try
            {
                var body = CreateDefinition("character.body.02", "body", 0, "Body_2_r0_c0");
                sprites["Body_2_r5_c2"] = Sprite.Create(texture, new Rect(0, 0, 16, 16), Vector2.one * 0.5f, 16);
                sprites["Body_2_r0_c0"] = Sprite.Create(texture, new Rect(0, 0, 16, 16), Vector2.one * 0.5f, 16);
                var appearance = new CharacterAppearance();
                appearance.SetSelectedPart("body", "character.body.02");
                var clip = CreateClip("character.action.watering.down", 5, 3, 10f, false);

                var composer = player.AddComponent<CharacterPartComposer>();
                var animator = player.AddComponent<CharacterPartAnimator>();
                animator.Configure(composer, appearance, new[] { body }, (definition, subSpriteName) => Resolve(sprites, subSpriteName));
                animator.SetMotion(Vector2.zero, new Vector2(0f, -1f));

                animator.PlayClip(clip);
                animator.Tick(0.25f);

                Assert.AreSame(sprites["Body_2_r5_c2"], player.transform.Find("Part_body").GetComponent<SpriteRenderer>().sprite);

                animator.Tick(0.1f);

                Assert.AreSame(sprites["Body_2_r0_c0"], player.transform.Find("Part_body").GetComponent<SpriteRenderer>().sprite);
            }
            finally
            {
                foreach (var sprite in sprites.Values) Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(player);
            }
        }

        private static Sprite Resolve(Dictionary<string, Sprite> sprites, string subSpriteName)
        {
            return sprites.TryGetValue(subSpriteName, out var sprite) ? sprite : null;
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

        private static CharacterPartAnimationClipDefinition CreateClip(string id, int row, int frameCount, float fps, bool loop)
        {
            var clip = ScriptableObject.CreateInstance<CharacterPartAnimationClipDefinition>();
            var serialized = new SerializedObject(clip);
            serialized.FindProperty("_id").stringValue = id;
            serialized.FindProperty("_row").intValue = row;
            serialized.FindProperty("_frameCount").intValue = frameCount;
            serialized.FindProperty("_framesPerSecond").floatValue = fps;
            serialized.FindProperty("_loop").boolValue = loop;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return clip;
        }
    }
}
