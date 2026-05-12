using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode
{
    public sealed class ModernSetupPipelineSourceTests
    {
        private const string OneClickSetupPath = "Assets/Scripts/Editor/Tools/OneClickSetup.cs";
        private const string GenerateDefaultDataPath = "Assets/Scripts/Editor/Tools/GenerateDefaultData.cs";
        private const string PlayerSetupPath = "Assets/Scripts/Editor/Tools/PlayerSetup.cs";
        private const string FarmSceneBuilderPath = "Assets/Scripts/Editor/Tools/FarmSceneBuilder.cs";

        [Test]
        public void OneClickSetup_UsesModernSliceAndAddressableToolsInsteadOfPixelwoodPipeline()
        {
            string source = File.ReadAllText(OneClickSetupPath);

            Assert.IsFalse(source.Contains("PixelwoodSliceSetup"), "One-click setup still invokes the legacy Pixelwood slicer.");
            Assert.IsFalse(source.Contains("AddressablesSetup.WireAll"), "One-click setup still invokes the legacy Pixelwood addressable wiring path.");
            StringAssert.Contains("ModernUiSliceSetup.SliceAll", source);
            StringAssert.Contains("ModernFarmSliceSetup.SliceCore16", source);
            StringAssert.Contains("ModernInteriorsSliceSetup.SliceCore16", source);
            StringAssert.Contains("ModernUiAddressablesSetup.WireSheets", source);
            StringAssert.Contains("ModernFarmAddressablesSetup.WireSheets", source);
            StringAssert.Contains("ModernInteriorsAddressablesSetup.WireSheets", source);
        }

        [Test]
        public void GenerateDefaultData_UsesModernFarmSpritesInsteadOfPixelwoodAssets()
        {
            string source = File.ReadAllText(GenerateDefaultDataPath);

            Assert.IsFalse(source.Contains("Assets/Pixelwood"), "Default data generation still hardcodes Pixelwood asset paths.");
            Assert.IsFalse(source.Contains("PixelwoodSliceSetup"), "Default data generation still invokes Pixelwood slicing/configuration.");
            StringAssert.Contains("Assets/Modern_Farm_v1.2", source);
            StringAssert.Contains("ModernFarmSliceSetup.SliceCore16", source);
        }

        [Test]
        public void PlayerSetup_UsesModernFarmPlayerSpriteInsteadOfPixelwoodSheets()
        {
            string source = File.ReadAllText(PlayerSetupPath);

            Assert.IsFalse(source.Contains("Assets/Pixelwood"), "Player setup still hardcodes Pixelwood player sheets.");
            Assert.IsFalse(source.Contains("Pixelwood"), "Player setup still contains Pixelwood-specific instructions or paths.");
            StringAssert.Contains("Assets/Modern_Farm_v1.2", source);
        }

        [Test]
        public void PlayerSetup_GeneratesDedicatedToolPartRendererForLayeredCharacters()
        {
            string source = File.ReadAllText(PlayerSetupPath);

            StringAssert.Contains("Part_tool", source);
            StringAssert.Contains("_toolRenderer", source);
            StringAssert.Contains("sortingOrder = 10", source);
            StringAssert.Contains("objectReferenceValue = toolRenderer", source);
        }

        [Test]
        public void FarmSceneBuilder_UsesRegistryOrModernFarmGroundInsteadOfPixelwoodTileSheet()
        {
            string source = File.ReadAllText(FarmSceneBuilderPath);

            Assert.IsFalse(source.Contains("Assets/Pixelwood"), "Farm scene builder still hardcodes the Pixelwood tile sheet.");
            Assert.IsFalse(source.Contains("Rootborn/Pixelwood"), "Farm scene builder still tells users to run the Pixelwood slicer.");
            StringAssert.Contains("registry.GroundSprite", source);
            StringAssert.Contains("Assets/Modern_Farm_v1.2", source);
        }

        [Test]
        public void FarmSceneBuilder_ReconfiguresExistingMainCameraForModernFarmCenter()
        {
            string source = File.ReadAllText(FarmSceneBuilderPath);

            Assert.IsFalse(source.Contains("if (Camera.main != null) return;"), "Existing scene cameras must be reconfigured instead of leaving stale Pixelwood-era framing.");
            StringAssert.Contains("GroundCols * 0.5f", source);
            StringAssert.Contains("GroundRows * 0.5f", source);
            StringAssert.Contains("cam.orthographic = true", source);
            StringAssert.Contains("cam.orthographicSize = 8f", source);
        }

        [Test]
        public void FarmSceneBuilder_OpensFarmSceneBeforeLoadingRegistryAssets()
        {
            string source = File.ReadAllText(FarmSceneBuilderPath);

            int openSceneIndex = source.IndexOf("EditorSceneManager.OpenScene(FarmScenePath", System.StringComparison.Ordinal);
            int loadRegistryIndex = source.IndexOf("var registry = LoadRegistry();", System.StringComparison.Ordinal);

            Assert.GreaterOrEqual(openSceneIndex, 0, "Farm scene builder must explicitly open the Farm scene.");
            Assert.GreaterOrEqual(loadRegistryIndex, 0, "Farm scene builder must load the data registry.");
            Assert.Less(openSceneIndex, loadRegistryIndex, "Registry loading must happen after opening the target scene so resource placement sees the current asset state.");
        }
    }
}
