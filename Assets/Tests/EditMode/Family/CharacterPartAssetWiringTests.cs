using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Family;
using Rootborn.Game.Tools;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Family
{
    public sealed class CharacterPartAssetWiringTests
    {
        private static readonly string[] RequiredCategories = { "body", "eyes", "hair", "outfit", "accessory" };
        private static readonly string[] RequiredAnimationClipIds =
        {
            "character.action.watering.down",
            "character.action.harvesting.down",
            "character.action.chopping.side",
            "character.action.digging.down",
            "character.action.fishing.side",
            "character.action.fishing.throw_hook.side",
            "character.action.fishing.idle.side",
            "character.action.fishing.pull_hook.side",
            "character.action.fishing.caught.side",
        };

        [Test]
        public void CharacterPartAssets_HaveAtLeastTwoDefinitionsPerRequiredCategory()
        {
            var definitions = LoadDefinitions();
            var counts = new Dictionary<string, int>();
            foreach (var definition in definitions)
            {
                if (definition == null)
                {
                    continue;
                }
                counts.TryGetValue(definition.CategoryId, out int count);
                counts[definition.CategoryId] = count + 1;
            }

            foreach (string category in RequiredCategories)
            {
                counts.TryGetValue(category, out int count);
                Assert.GreaterOrEqual(count, 2, category);
            }
        }

        [Test]
        public void CharacterPartAssets_AreRegisteredInGameDataRegistry()
        {
            AssertRegistryHasCharacterParts("Assets/Data/Registry/GameDataRegistry.asset");
        }

        [Test]
        public void CharacterPartAssets_AreRegisteredInResourcesFallbackRegistry()
        {
            AssertRegistryHasCharacterParts("Assets/Resources/GameDataRegistry.asset");
        }

        [Test]
        public void CharacterPartAnimationClipAssets_AreRegisteredInBothRegistries()
        {
            AssertRegistryHasAnimationClips("Assets/Data/Registry/GameDataRegistry.asset");
            AssertRegistryHasAnimationClips("Assets/Resources/GameDataRegistry.asset");
        }

        [Test]
        public void CharacterPartActionClips_MatchAnimationGuideRowsAndFrameCounts()
        {
            AssertClipGuide("character.action.harvesting.down", 2, 36);
            AssertClipGuide("character.action.digging.down", 3, 36);
            AssertClipGuide("character.action.watering.down", 4, 56);
            AssertClipGuide("character.action.chopping.side", 5, 40);
            AssertClipGuide("character.action.fishing.side", 6, 40);
            AssertClipGuide("character.action.fishing.throw_hook.side", 6, 40);
            AssertClipGuide("character.action.fishing.idle.side", 7, 24);
            AssertClipGuide("character.action.fishing.pull_hook.side", 8, 8);
            AssertClipGuide("character.action.fishing.caught.side", 9, 40);
        }

        [Test]
        public void CharacterPartActionClips_TargetCellsWithinRuntimePartSheetBounds()
        {
            var body = AssetDatabase.LoadAssetAtPath<CharacterPartDefinition>("Assets/Data/Family/CharacterParts/character_body_02.asset");
            Assert.IsNotNull(body);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(body.EditorAssetPath);
            Assert.IsNotNull(texture);
            int cols = texture.width / 16;
            int rows = texture.height / 16;
            foreach (var clip in LoadAnimationClips())
            {
                Assert.Less(clip.Row, rows, clip.Id);
                Assert.LessOrEqual(clip.FrameCount, cols, clip.Id);
            }
        }

        [Test]
        public void ToolDefinitions_ReferenceRegisteredCharacterPartActionClips()
        {
            AssertToolClip("Assets/Data/Tools/Tool_WateringCan.asset", "character.action.watering.down");
            AssertToolClip("Assets/Data/Tools/Tool_StoneAxe.asset", "character.action.chopping.side");
            AssertToolClip("Assets/Data/Tools/Tool_StoneHoe.asset", "character.action.digging.down");
        }

        [Test]
        public void CharacterPartDefinitions_ReferenceExistingSixteenBySixteenSubSprites()
        {
            var definitions = LoadDefinitions();
            Assert.GreaterOrEqual(definitions.Length, 10);

            foreach (var definition in definitions)
            {
                Assert.IsTrue(definition.IsValid(out var error), error);
                string assetPath = definition.EditorAssetPath;
                Assert.IsTrue(assetPath.StartsWith("Assets/"), definition.Id);
                var sprites = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                Sprite matched = null;
                foreach (var asset in sprites)
                {
                    if (asset is Sprite sprite && sprite.name == definition.SubSpriteName)
                    {
                        matched = sprite;
                        break;
                    }
                }

                Assert.IsNotNull(matched, definition.Id + " -> " + definition.SubSpriteName);
                Assert.AreEqual(16f, matched.rect.width, definition.Id);
                Assert.AreEqual(16f, matched.rect.height, definition.Id);
                Assert.IsNotNull(definition.PreviewSprite, definition.Id);
                Assert.AreEqual(definition.SubSpriteName, definition.PreviewSprite.name, definition.Id);
                Assert.AreEqual(16f, definition.PreviewSprite.rect.width, definition.Id);
                Assert.AreEqual(16f, definition.PreviewSprite.rect.height, definition.Id);
            }
        }

        private static void AssertClipGuide(string expectedId, int expectedRow, int expectedFrameCount)
        {
            CharacterPartAnimationClipDefinition matched = null;
            foreach (var clip in LoadAnimationClips())
            {
                if (clip.Id == expectedId)
                {
                    matched = clip;
                    break;
                }
            }

            Assert.IsNotNull(matched, expectedId);
            Assert.AreEqual(expectedRow, matched.Row, expectedId);
            Assert.AreEqual(expectedFrameCount, matched.FrameCount, expectedId);
        }

        private static CharacterPartAnimationClipDefinition[] LoadAnimationClips()
        {
            string[] guids = AssetDatabase.FindAssets("t:CharacterPartAnimationClipDefinition", new[] { "Assets/Data/Family/CharacterPartAnimations" });
            var clips = new List<CharacterPartAnimationClipDefinition>(guids.Length);
            foreach (string guid in guids)
            {
                string clipPath = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<CharacterPartAnimationClipDefinition>(clipPath);
                if (clip != null)
                {
                    clips.Add(clip);
                }
            }
            return clips.ToArray();
        }

        private static void AssertToolClip(string path, string expectedClipId)
        {
            var tool = AssetDatabase.LoadAssetAtPath<ToolDefinition>(path);
            Assert.IsNotNull(tool, path);
            Assert.IsNotNull(tool.CharacterPartAnimationClip, path);
            Assert.AreEqual(expectedClipId, tool.CharacterPartAnimationClip.Id, path);
        }

        private static void AssertRegistryHasCharacterParts(string path)
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(path);
            Assert.IsNotNull(registry, path);
            Assert.IsNotNull(registry.CharacterParts, path);
            Assert.GreaterOrEqual(registry.CharacterParts.Length, 10, path);

            var ids = new HashSet<string>();
            foreach (var definition in registry.CharacterParts)
            {
                Assert.IsNotNull(definition, path);
                Assert.IsTrue(definition.IsValid(out var error), error);
                Assert.IsTrue(ids.Add(definition.Id), definition.Id);
            }
        }

        private static void AssertRegistryHasAnimationClips(string path)
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(path);
            Assert.IsNotNull(registry, path);
            Assert.IsNotNull(registry.CharacterPartAnimationClips, path);
            var ids = new HashSet<string>();
            foreach (var clip in registry.CharacterPartAnimationClips)
            {
                Assert.IsNotNull(clip, path);
                Assert.IsTrue(clip.IsValid(out var error), error);
                ids.Add(clip.Id);
            }

            foreach (string requiredId in RequiredAnimationClipIds)
            {
                Assert.IsTrue(ids.Contains(requiredId), requiredId + " in " + path);
            }
        }

        private static CharacterPartDefinition[] LoadDefinitions()
        {
            string[] guids = AssetDatabase.FindAssets("t:CharacterPartDefinition", new[] { "Assets/Data/Family/CharacterParts" });
            var definitions = new List<CharacterPartDefinition>(guids.Length);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var definition = AssetDatabase.LoadAssetAtPath<CharacterPartDefinition>(path);
                if (definition != null)
                {
                    definitions.Add(definition);
                }
            }
            return definitions.ToArray();
        }
    }
}
