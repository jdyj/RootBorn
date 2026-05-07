using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Family;
using Rootborn.Game.Managers;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Family
{
    public sealed class FishingAnimationRegistryTests
    {
        [Test]
        public void GameDataRegistry_ExposesFishingAnimationDefinitions()
        {
            var animation = CreateFishingAnimation("character.fishing.default");
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();
            try
            {
                SetRegistryFishingAnimations(registry, new[] { animation });

                Assert.AreEqual(1, registry.FishingAnimations.Length);
                Assert.AreSame(animation, registry.FishingAnimations[0]);
            }
            finally
            {
                Object.DestroyImmediate(animation);
                Object.DestroyImmediate(registry);
            }
        }

        [Test]
        public void DataManager_BuildsFishingAnimationLookupByStableId()
        {
            var animation = CreateFishingAnimation("character.fishing.default");
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();
            var data = new DataManager();
            try
            {
                SetRegistryFishingAnimations(registry, new[] { animation });
                SetRegistry(data, registry);

                InvokeBuildLookups(data);

                Assert.AreSame(animation, data.FishingAnimationById["character.fishing.default"]);
            }
            finally
            {
                Object.DestroyImmediate(animation);
                Object.DestroyImmediate(registry);
            }
        }

        private static FishingAnimationDefinition CreateFishingAnimation(string id)
        {
            var animation = ScriptableObject.CreateInstance<FishingAnimationDefinition>();
            var serialized = new SerializedObject(animation);
            serialized.FindProperty("_id").stringValue = id;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return animation;
        }

        private static void SetRegistryFishingAnimations(GameDataRegistry registry, FishingAnimationDefinition[] animations)
        {
            var serialized = new SerializedObject(registry);
            var property = serialized.FindProperty("_fishingAnimations");
            property.arraySize = animations.Length;
            for (int i = 0; i < animations.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = animations[i];
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
