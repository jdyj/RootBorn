using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode
{
    public sealed class ModernFarmDataWiringTests
    {
        private const string ModernFarmRoot = "Assets/Modern_Farm_v1.2/";
        private const string PixelwoodMarker = "Assets/Pixelwood Valley/";

        [Test]
        public void CoreFarmVisualData_UsesModernFarmSpritesInsteadOfPixelwood()
        {
            AssertObjectReferenceUsesModernFarmSprite("Assets/Data/Tiles/GroundTile.asset", "m_Sprite");
            AssertObjectReferenceUsesModernFarmSprite("Assets/Data/Resources/Resource_Rock.asset", "_sprite");
            AssertObjectReferenceUsesModernFarmSprite("Assets/Data/Resources/Resource_Tree.asset", "_sprite");
            AssertObjectReferenceUsesModernFarmSprite("Assets/Data/Items/Item_Wood.asset", "_icon");
            AssertObjectReferenceUsesModernFarmSprite("Assets/Data/Items/Item_Stone.asset", "_icon");
            AssertObjectReferenceUsesModernFarmSprite("Assets/Data/Items/Item_Tool_StoneAxe.asset", "_icon");
            AssertObjectReferenceUsesModernFarmSprite("Assets/Data/Tools/Tool_StoneAxe.asset", "_icon");
        }

        [Test]
        public void RegistryVisualFallbacks_UseModernFarmSpritesInsteadOfPixelwood()
        {
            AssertObjectReferenceUsesModernFarmSprite("Assets/Resources/GameDataRegistry.asset", "_groundSprite");
            AssertObjectReferenceUsesModernFarmSprite("Assets/Resources/GameDataRegistry.asset", "_playerSprite");
        }

        private static void AssertObjectReferenceUsesModernFarmSprite(string assetPath, string propertyPath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            Assert.IsNotNull(asset, assetPath);
            var serialized = new SerializedObject(asset);
            var property = serialized.FindProperty(propertyPath);
            Assert.IsNotNull(property, $"{assetPath} missing {propertyPath}");
            AssertSpritePathIsModernFarm(property.objectReferenceValue as Sprite, $"{assetPath}:{propertyPath}");
        }

        private static void AssertSpritePathIsModernFarm(Sprite sprite, string context)
        {
            Assert.IsNotNull(sprite, context);
            string path = AssetDatabase.GetAssetPath(sprite);
            Assert.IsFalse(path.StartsWith(PixelwoodMarker, System.StringComparison.Ordinal), context + " still references Pixelwood: " + path);
            StringAssert.StartsWith(ModernFarmRoot, path, context + " must reference Modern Farm but was " + path);
        }
    }
}
