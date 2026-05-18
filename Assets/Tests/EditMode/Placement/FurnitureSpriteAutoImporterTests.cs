using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Editor.Interiors;
using Rootborn.Game.Common;
using Rootborn.Game.Placement;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Rootborn.Tests.EditMode.Placement
{
    public sealed class FurnitureSpriteAutoImporterTests
    {
        private const string TestRoot = "Assets/Temp/FurnitureSpriteAutoImporterTests";

        [SetUp]
        public void SetUp()
        {
            if (AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.DeleteAsset(TestRoot);
            }

            Directory.CreateDirectory(TestRoot);
            AssetDatabase.Refresh();
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.DeleteAsset(TestRoot);
            }

            var generatedRoot = "Assets/Data/Interiors/Furniture/AutoImported/__Tests";
            if (AssetDatabase.IsValidFolder(generatedRoot))
            {
                AssetDatabase.DeleteAsset(generatedRoot);
            }

            AssetDatabase.Refresh();
        }

        [Test]
        public void ResolveFootprint_UsesFilenameBeforePixelSize()
        {
            var result = FurnitureSpriteAutoImporter.ResolveFootprint("Assets/Incoming/FurnitureSprites/Tables/table_shadow_2x1.png", 96, 96);

            Assert.AreEqual(new Vector2Int(2, 1), result.FootprintSize);
            Assert.AreEqual(FurnitureSpriteFootprintSource.FileName, result.Source);
        }

        [Test]
        public void ResolveFootprint_UsesPixelSizeWhenFilenameHasNoSize()
        {
            var result = FurnitureSpriteAutoImporter.ResolveFootprint("Assets/Incoming/FurnitureSprites/Beds/bed_blue.png", 96, 96);

            Assert.AreEqual(new Vector2Int(2, 2), result.FootprintSize);
            Assert.AreEqual(FurnitureSpriteFootprintSource.PixelSize, result.Source);
        }

        [Test]
        public void ResolveFootprint_CeilsNonDivisiblePixelSizeInsteadOfFallingBackToOneByOne()
        {
            var result = FurnitureSpriteAutoImporter.ResolveFootprint("Assets/Incoming/FurnitureSprites/Decor/weird_sofa.png", 100, 54);

            Assert.AreEqual(new Vector2Int(3, 2), result.FootprintSize);
            Assert.AreEqual(FurnitureSpriteFootprintSource.PixelSize, result.Source);
        }

        [Test]
        public void ResolveLowerLeftAnchorPivot_UsesHalfTilePixelOffsetForWideAndTallSprites()
        {
            Assert.AreEqual(new Vector2(0.25f, 0.5f), FurnitureSpriteAutoImporter.ResolveLowerLeftAnchorPivot(96, 48));
            Assert.AreEqual(new Vector2(0.5f, 0.3f), FurnitureSpriteAutoImporter.ResolveLowerLeftAnchorPivot(48, 80));
        }

        [Test]
        public void ImportSprite_CreatesFurnitureDefinitionTileAndRegistersIt()
        {
            var pngPath = CreatePng("Chairs/chair_black_2x1.png", 96, 48);
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();
            var registryPath = TestRoot + "/Registry.asset";
            AssetDatabase.CreateAsset(registry, registryPath);
            AssetDatabase.SaveAssets();

            var result = FurnitureSpriteAutoImporter.ImportSprite(
                pngPath,
                "Assets/Data/Interiors/Furniture/AutoImported/__Tests",
                registryPath);

            Assert.IsTrue(result.Success, result.Message);
            Assert.IsNotNull(result.FurnitureDefinition);
            Assert.AreEqual("chair.black", result.FurnitureDefinition.StableId);
            Assert.AreEqual("Chairs", result.FurnitureDefinition.Category);
            CollectionAssert.AreEquivalent(new[] { new Vector2Int(0, 0), new Vector2Int(1, 0) }, result.FurnitureDefinition.FootprintCells);
            Assert.AreEqual(1, result.FurnitureDefinition.TileParts.Count, "Single PNG furniture should render as one sprite tile part while using the footprint for occupancy.");
            Assert.IsInstanceOf<Tile>(result.FurnitureDefinition.TileParts[0].Tile);

            var importedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
            Assert.IsNotNull(importedSprite);
            Assert.AreEqual(new Vector2(24f, 24f), importedSprite.pivot, "A 96x48 sprite should align its lower-left visual corner to the selected anchor cell.");

            var reloadedRegistry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(registryPath);
            CollectionAssert.Contains(reloadedRegistry.FurnitureDefinitions, result.FurnitureDefinition);
            Assert.IsTrue(File.Exists(result.FurnitureAssetPath));
            Assert.IsTrue(File.Exists(result.TileAssetPath));
        }

        private static string CreatePng(string relativePath, int width, int height)
        {
            var path = TestRoot + "/" + relativePath.Replace('\\', '/');
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = Enumerable.Repeat(Color.white, width * height).ToArray();
            texture.SetPixels(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = FurnitureSpriteAutoImporter.DefaultTilePixelSize;
            importer.SaveAndReimport();
            return path;
        }
    }
}
