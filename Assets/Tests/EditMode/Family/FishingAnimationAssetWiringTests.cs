using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Family;
using UnityEditor;

namespace Rootborn.Tests.EditMode.Family
{
    public sealed class FishingAnimationAssetWiringTests
    {
        private const string DataRegistryPath = "Assets/Data/Registry/GameDataRegistry.asset";
        private const string RuntimeRegistryPath = "Assets/Resources/GameDataRegistry.asset";
        private const string FishingAnimationPath = "Assets/Data/Family/CharacterPartAnimations/FishingAnimation_Default.asset";

        [Test]
        public void FishingAnimationAsset_ReferencesAllFishingPhaseClips()
        {
            var animation = AssetDatabase.LoadAssetAtPath<FishingAnimationDefinition>(FishingAnimationPath);
            Assert.IsNotNull(animation, FishingAnimationPath);
            Assert.IsTrue(animation.IsValid(out var error), error);
            Assert.AreEqual("character.fishing.default", animation.Id);
            Assert.AreEqual("character.action.fishing.throw_hook.side", animation.ThrowHookClip.Id);
            Assert.AreEqual("character.action.fishing.idle.side", animation.WaitingIdleClip.Id);
            Assert.AreEqual("character.action.fishing.pull_hook.side", animation.PullHookClip.Id);
            Assert.AreEqual("character.action.fishing.caught.side", animation.CaughtClip.Id);
        }

        [Test]
        public void FishingAnimationAsset_IsRegisteredInBothGameDataRegistries()
        {
            AssertRegistryContainsFishingAnimation(DataRegistryPath);
            AssertRegistryContainsFishingAnimation(RuntimeRegistryPath);
        }

        private static void AssertRegistryContainsFishingAnimation(string path)
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(path);
            Assert.IsNotNull(registry, path);
            Assert.IsNotNull(registry.FishingAnimations, path);
            Assert.IsTrue(
                registry.FishingAnimations.Any(animation => animation != null && animation.Id == "character.fishing.default"),
                path + " must register character.fishing.default");
        }
    }
}
