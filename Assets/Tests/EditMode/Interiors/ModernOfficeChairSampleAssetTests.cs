using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Rootborn.Tests.EditMode.Interiors
{
    public sealed class ModernOfficeChairSampleAssetTests
    {
        private const string ChairSpriteFolder = "Assets/Data/Interiors/TileSets/ModernOfficeChairSamples/Sprites";
        private const string ChairTileFolder = "Assets/Data/Interiors/TileSets/ModernOfficeChairSamples";

        [Test]
        public void ModernOfficeChairSheets_AreExportedIntoExpectedFurnitureSprites()
        {
            AssertSpriteAssets("BlackChair", 6);
            AssertSpriteAssets("OrangeChair", 6);
            AssertSpriteAssets("Chair", 4);
        }

        [Test]
        public void ModernOfficeChairSamples_CreateOneTileAssetPerSlice()
        {
            AssertTileAssets("BlackChair", 6);
            AssertTileAssets("OrangeChair", 6);
            AssertTileAssets("Chair", 4);
        }

        private static void AssertSpriteAssets(string prefix, int expected)
        {
            for (int i = 0; i < expected; i++)
            {
                var path = $"{ChairSpriteFolder}/{prefix}_{i:D2}.png";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                Assert.IsNotNull(sprite, $"Missing generated chair sprite asset: {path}");
                Assert.AreEqual(48f, sprite.rect.width, $"Generated chair sprite width should be one office tile: {path}");
                Assert.AreEqual(96f, sprite.rect.height, $"Generated chair sprite height should be two office tiles: {path}");
            }
        }

        private static void AssertTileAssets(string prefix, int expected)
        {
            for (int i = 0; i < expected; i++)
            {
                var path = $"{ChairTileFolder}/{prefix}_{i:D2}.asset";
                var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
                Assert.IsNotNull(tile, $"Missing generated chair tile asset: {path}");
                Assert.IsNotNull(tile.sprite, $"Generated chair tile should reference a sliced sprite: {path}");
            }
        }
    }
}
