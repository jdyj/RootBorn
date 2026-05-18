using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Editor.Interiors;
using Rootborn.Game.Common;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Interiors
{
    public sealed class ModernInteriorsShadowlessFurnitureImporterTests
    {
        private const string TestRoot = "Assets/Temp/ModernInteriorsShadowlessImporterTests";
        private const string GeneratedRoot = "Assets/Data/Interiors/Furniture/AutoImported/__ModernInteriorsTests";

        [SetUp]
        public void SetUp()
        {
            if (AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.DeleteAsset(TestRoot);
            }

            if (AssetDatabase.IsValidFolder(GeneratedRoot))
            {
                AssetDatabase.DeleteAsset(GeneratedRoot);
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

            if (AssetDatabase.IsValidFolder(GeneratedRoot))
            {
                AssetDatabase.DeleteAsset(GeneratedRoot);
            }

            AssetDatabase.Refresh();
        }

        [Test]
        public void SourceScope_UsesOnlyThemeSorterShadowlessSingles48()
        {
            Assert.AreEqual(
                "Assets/moderninteriors-win/1_Interiors/48x48/Theme_Sorter_Shadowless_Singles_48x48",
                ModernInteriorsShadowlessFurnitureImporter.ProjectSourceRoot);

            Assert.IsTrue(ModernInteriorsShadowlessFurnitureImporter.IsAllowedSourcePath(
                "Assets/moderninteriors-win/1_Interiors/48x48/Theme_Sorter_Shadowless_Singles_48x48/2_Living_Room_Singles_Shadowless_48x48/Living_Room_Singles_Shadowless_48x48_1.png"));

            Assert.IsFalse(ModernInteriorsShadowlessFurnitureImporter.IsAllowedSourcePath(
                "Assets/moderninteriors-win/1_Interiors/48x48/Theme_Sorter_Singles_48x48/2_Living_Room_Singles_48x48/Living_Room_Singles_48x48_1.png"));

            Assert.IsFalse(ModernInteriorsShadowlessFurnitureImporter.IsAllowedSourcePath(
                "Assets/moderninteriors-win/1_Interiors/48x48/Theme_Sorter_Black_Shadow_Singles_48x48/2_Living_Room_Black_Shadow_Singles_48x48/Living_Room_Singles_Black_Shadow_48x48_1.png"));
        }

        [Test]
        public void BuildInventory_ReadsEveryShadowlessSinglesFolder()
        {
            var inventory = ModernInteriorsShadowlessFurnitureImporter.BuildDownloadInventory();

            Assert.GreaterOrEqual(inventory.Count, 20);
            Assert.IsTrue(inventory.Any(item => item.FolderName == "2_Living_Room_Singles_Shadowless_48x48" && item.PngCount == 122));
            Assert.IsTrue(inventory.Any(item => item.FolderName == "3_Bathroom_Singles_Shadowless_48x48" && item.PngCount == 158));
            Assert.IsTrue(inventory.Any(item => item.FolderName == "4_Bedroom_Singles_Shadowless_48x48" && item.PngCount == 555));
            Assert.IsTrue(inventory.Any(item => item.FolderName == "12_Kitchen_Singles_Shadowless_48x48" && item.PngCount == 408));
            Assert.IsFalse(inventory.Any(item => item.SourcePath.Contains("Black_Shadow")));
            Assert.IsFalse(inventory.Any(item => item.SourcePath.Contains("Theme_Sorter_Singles_48x48")));
        }

        [TestCase("2_Living_Room_Singles_Shadowless_48x48", "LivingRoom", true)]
        [TestCase("3_Bathroom_Singles_Shadowless_48x48", "Bathroom", true)]
        [TestCase("4_Bedroom_Singles_Shadowless_48x48", "Bedroom", true)]
        [TestCase("12_Kitchen_Singles_Shadowless_48x48", "Kitchen", true)]
        [TestCase("20_Japanese_Interiors_Singles_Shadowless_48x48", "Japanese", true)]
        [TestCase("26_Condominium_Singles_Shadowless_48x48", "Condominium", true)]
        [TestCase("22_Museum_Singles_Shadowless_48x48", "Museum", false)]
        public void ResolveCategory_SeparatesHouseFirstCandidates(string folderName, string expectedCategory, bool expectedHouseFirst)
        {
            var category = ModernInteriorsShadowlessFurnitureImporter.ResolveCategory(folderName);

            Assert.AreEqual(expectedCategory, category.Category);
            Assert.AreEqual(expectedHouseFirst, category.IsHouseFirstCandidate);
        }

        [Test]
        public void BuildCopyPlan_MapsDownloadPathToProjectShadowlessSinglesRoot()
        {
            var plan = ModernInteriorsShadowlessFurnitureImporter.BuildCopyPlanForTests(
                ModernInteriorsShadowlessFurnitureImporter.DownloadSourceRoot + "/2_Living_Room_Singles_Shadowless_48x48/Living_Room_Singles_Shadowless_48x48_1.png");

            Assert.AreEqual(
                "Assets/moderninteriors-win/1_Interiors/48x48/Theme_Sorter_Shadowless_Singles_48x48/2_Living_Room_Singles_Shadowless_48x48/Living_Room_Singles_Shadowless_48x48_1.png",
                plan.ProjectAssetPath);
            Assert.IsTrue(ModernInteriorsShadowlessFurnitureImporter.IsAllowedSourcePath(plan.ProjectAssetPath));
        }

        [Test]
        public void ImportHouseFirstCandidates_RegistersOnlyHouseFirstShadowlessDefinitions()
        {
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();
            var registryPath = TestRoot + "/Registry.asset";
            AssetDatabase.CreateAsset(registry, registryPath);
            AssetDatabase.SaveAssets();

            var livingRoomPath = CreatePng("2_Living_Room_Singles_Shadowless_48x48/Living_Room_Singles_Shadowless_48x48_1.png", 96, 128);
            CreatePng("22_Museum_Singles_Shadowless_48x48/Museum_Singles_Shadowless_48x48_1.png", 48, 48);

            var result = ModernInteriorsShadowlessFurnitureImporter.ImportHouseFirstCandidatesForTests(
                TestRoot,
                GeneratedRoot,
                registryPath);

            Assert.AreEqual(1, result.ImportedCount);
            Assert.AreEqual(1, result.SkippedNonHouseFirstCount);
            var reloaded = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(registryPath);
            Assert.AreEqual(1, reloaded.FurnitureDefinitions.Length);
            Assert.AreEqual("LivingRoom", reloaded.FurnitureDefinitions[0].Category);
            CollectionAssert.Contains(reloaded.FurnitureDefinitions[0].AllowedSurfaceIds, "house");

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(livingRoomPath);
            Assert.IsNotNull(sprite);
            Assert.AreEqual(new Vector2(24f, 24f), sprite.pivot, "Modern Interiors sprites should use the selected cell as the lower-left placement anchor.");
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
            importer.spritePixelsPerUnit = 48;
            importer.SaveAndReimport();
            return path;
        }
    }
}
