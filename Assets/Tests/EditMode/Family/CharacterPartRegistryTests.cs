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
        public void GameDataRegistry_ExposesCharacterPartAnimationClips()
        {
            var clip = CreateClip("character.action.watering.down", 5, 3, 10f, false);
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();
            SetRegistryAnimationClips(registry, new[] { clip });

            Assert.AreEqual(1, registry.CharacterPartAnimationClips.Length);
            Assert.AreSame(clip, registry.CharacterPartAnimationClips[0]);
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
