using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Family;
using Rootborn.Game.Tools;
using Rootborn.Game.Tools.Effects;
using UnityEditor;

namespace Rootborn.Tests.EditMode
{
    public sealed class FishingToolAssetWiringTests
    {
        private const string RuntimeRegistryPath = "Assets/Resources/GameDataRegistry.asset";
        private const string DataRegistryPath = "Assets/Data/Registry/GameDataRegistry.asset";
        private const string FishingRodToolPath = "Assets/Data/Tools/Tool_FishingRod.asset";
        private const string FishingRodItemPath = "Assets/Data/Items/Item_Tool_FishingRod.asset";
        private const string FishingEffectPath = "Assets/Data/Tools/Effects/Effect_FishingThrowHook.asset";

        [Test]
        public void FishingRodTool_UsesFishingAnimationEffectAndCharacterClip()
        {
            var tool = AssetDatabase.LoadAssetAtPath<ToolDefinition>(FishingRodToolPath);
            Assert.IsNotNull(tool, FishingRodToolPath);
            Assert.AreEqual("FishingRod", tool.Id);
            Assert.IsNotNull(tool.CharacterPartAnimationClip);
            Assert.AreEqual("character.action.fishing.throw_hook.side", tool.CharacterPartAnimationClip.Id);
            Assert.IsTrue(tool.Effects.OfType<FishingAnimationEffect>().Any(effect => effect.Phase == FishingAnimationPhase.ThrowHook));
        }

        [Test]
        public void FishingRodItem_SharesToolIdAndIsToolCategory()
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(FishingRodItemPath);
            Assert.IsNotNull(item, FishingRodItemPath);
            Assert.AreEqual("FishingRod", item.Id);
            Assert.AreEqual(ItemCategory.Tool, item.Category);
        }

        [Test]
        public void FishingAnimationEffectAsset_IsConfiguredForThrowHookOnWaterSurface()
        {
            var effect = AssetDatabase.LoadAssetAtPath<FishingAnimationEffect>(FishingEffectPath);
            Assert.IsNotNull(effect, FishingEffectPath);
            Assert.AreEqual(FishingAnimationPhase.ThrowHook, effect.Phase);
            Assert.AreEqual("Water", effect.RequiredSurface);
        }

        [Test]
        public void FishingRodToolAndItem_AreRegisteredInBothRegistries()
        {
            AssertRegistryContainsFishingRod(DataRegistryPath);
            AssertRegistryContainsFishingRod(RuntimeRegistryPath);
        }

        private static void AssertRegistryContainsFishingRod(string path)
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(path);
            Assert.IsNotNull(registry, path);
            Assert.IsTrue(registry.Tools.Any(tool => tool != null && tool.Id == "FishingRod"), path + " must register FishingRod tool");
            Assert.IsTrue(registry.Items.Any(item => item != null && item.Id == "FishingRod"), path + " must register FishingRod item");
        }
    }
}
