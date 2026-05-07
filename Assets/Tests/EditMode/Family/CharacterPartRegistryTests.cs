using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Family;
using Rootborn.Game.Managers;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Family
{
    public sealed class CharacterPartRegistryTests
    {
        [Test]
        public void GameDataRegistry_ExposesCharacterPartDefinitions()
        {
            var body = CreateDefinition("character.body.01", "body");
            var eyes = CreateDefinition("character.eyes.blue", "eyes");
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();
            SetRegistryParts(registry, new[] { body, eyes });

            Assert.AreEqual(2, registry.CharacterParts.Length);
            Assert.AreSame(body, registry.CharacterParts[0]);
            Assert.AreSame(eyes, registry.CharacterParts[1]);
        }

        [Test]
        public void GameDataRegistry_ExposesCharacterPartAnimationClips()
        {
            var clip = CreateClip("character.action.watering.down", 5, 3, 10f, false);
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();
            SetRegistryAnimationClips(registry, new[] { clip });

            Assert.AreEqual(1, registry.CharacterPartAnimationClips.Length);
            Assert.AreSame(clip, registry.CharacterPartAnimationClips[0]);
        }

        [Test]
        public void DataManager_BuildsCharacterPartLookupByStableId()
        {
            var body = CreateDefinition("character.body.01", "body");
            var eyes = CreateDefinition("character.eyes.blue", "eyes");
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();
            SetRegistryParts(registry, new[] { body, eyes });
            var data = new DataManager();
            SetRegistry(data, registry);

            InvokeBuildLookups(data);

            Assert.AreSame(body, data.CharacterPartById["character.body.01"]);
            Assert.AreSame(eyes, data.CharacterPartById["character.eyes.blue"]);
            CollectionAssert.Contains(data.CharacterPartsByCategory["body"], body);
            CollectionAssert.Contains(data.CharacterPartsByCategory["eyes"], eyes);
        }

        [Test]
        public void DataManager_BuildsCharacterPartAnimationClipLookupByStableId()
        {
            var clip = CreateClip("character.action.watering.down", 5, 3, 10f, false);
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();
            SetRegistryAnimationClips(registry, new[] { clip });
            var data = new DataManager();
            SetRegistry(data, registry);

            InvokeBuildLookups(data);

            Assert.AreSame(clip, data.CharacterPartAnimationClipById["character.action.watering.down"]);
        }

        private static CharacterPartDefinition CreateDefinition(string id, string categoryId)
        {
            var definition = ScriptableObject.CreateInstance<CharacterPartDefinition>();
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_id").stringValue = id;
            serialized.FindProperty("_categoryId").stringValue = categoryId;
            serialized.FindProperty("_displayNameKey").stringValue = "loc." + id;
            serialized.FindProperty("_sheetAddress").stringValue = "sprites/character/" + categoryId + "/" + id;
            serialized.FindProperty("_subSpriteName").stringValue = id + "_r0_c0";
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

        private static void SetRegistryParts(GameDataRegistry registry, CharacterPartDefinition[] parts)
        {
            var serialized = new SerializedObject(registry);
            var property = serialized.FindProperty("_characterParts");
            property.arraySize = parts.Length;
            for (int i = 0; i < parts.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = parts[i];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetRegistryAnimationClips(GameDataRegistry registry, CharacterPartAnimationClipDefinition[] clips)
        {
            var serialized = new SerializedObject(registry);
            var property = serialized.FindProperty("_characterPartAnimationClips");
            property.arraySize = clips.Length;
            for (int i = 0; i < clips.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetRegistry(DataManager data, GameDataRegistry registry)
        {
            var property = typeof(DataManager).GetProperty("Registry", BindingFlags.Instance | BindingFlags.Public);
            property.SetValue(data, registry);
        }

        private static void InvokeBuildLookups(DataManager data)
        {
            var method = typeof(DataManager).GetMethod("BuildLookups", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            method.Invoke(data, null);
        }
    }
}
