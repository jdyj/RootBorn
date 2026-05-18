using NUnit.Framework;
using Rootborn.Editor.Interiors;
using Rootborn.Game.Interiors;
using UnityEditor;

namespace Rootborn.Tests.EditMode.Interiors
{
    public sealed class HomeDesignRoomPresetImporterTests
    {
        private const string SourceRoot = @"C:\Users\jdyj\Downloads\moderninteriors-win\6_Home_Designs";
        private const string ProjectRoot = "Assets/Temp/Tests/ModernInteriorsHomeDesigns";
        private const string OutputRoot = "Assets/Temp/Tests/HomeDesignRoomPresets";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(ProjectRoot);
            AssetDatabase.DeleteAsset(OutputRoot);
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(ProjectRoot);
            AssetDatabase.DeleteAsset(OutputRoot);
        }

        private static InteriorRoomPresetDefinition ReloadCompactPreset()
        {
            return AssetDatabase.LoadAssetAtPath<InteriorRoomPresetDefinition>(OutputRoot + "/Condominium_Design_2/InteriorRoomPreset_Condominium_Design_2.asset");
        }

        [Test]
        public void ImportCondominiumSamples_CreatesTwoExactGridPresetAssets()
        {
            var result = ModernInteriorsHomeDesignPresetImporter.ImportCondominiumSamplesForTests(SourceRoot, ProjectRoot, OutputRoot);

            Assert.AreEqual(2, result.ImportedCount);
            Assert.AreEqual(2, result.ExactGridCount);
            Assert.AreEqual(0, result.DeferredNonExactGridCount);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var compact = ReloadCompactPreset();
            if (compact != null && string.IsNullOrEmpty(compact.StableId))
            {
                ModernInteriorsHomeDesignPresetImporter.ImportCondominiumSamplesForTests(SourceRoot, ProjectRoot, OutputRoot);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                compact = ReloadCompactPreset();
            }
            var apartment = AssetDatabase.LoadAssetAtPath<InteriorRoomPresetDefinition>(OutputRoot + "/Condominium_Design/InteriorRoomPreset_Condominium_Design.asset");

            Assert.IsNotNull(compact);
            Assert.AreEqual("modern.home-designs.condominium-design-2", compact.StableId);
            Assert.AreEqual("Condominium Design 2", compact.DisplayName);
            Assert.AreEqual(14, compact.Size.x);
            Assert.AreEqual(6, compact.Size.y);
            Assert.AreEqual(84, compact.BaseLayerCells.Count);

            Assert.IsNotNull(apartment);
            Assert.AreEqual("modern.home-designs.condominium-design", apartment.StableId);
            Assert.AreEqual("Condominium Design", apartment.DisplayName);
            Assert.AreEqual(14, apartment.Size.x);
            Assert.AreEqual(11, apartment.Size.y);
            Assert.AreEqual(154, apartment.BaseLayerCells.Count);
        }
    }
}
