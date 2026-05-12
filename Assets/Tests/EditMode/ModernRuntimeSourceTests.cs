using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode
{
    public sealed class ModernRuntimeSourceTests
    {
        private const string ManagersPath = "Assets/Scripts/Game/Managers/Managers.cs";
        private const string AddressableManifestPath = "Assets/Scripts/Game/Common/AddressableManifest.cs";
        private const string SpriteAddressableGroupPath = "Assets/AddressableAssetsData/AssetGroups/Sprites.asset";
        private const string ModernToolFallbackFolder = "Assets/Modern_Farm_v1.2/Generated/ToolFallbacks";

        [Test]
        public void Managers_BootstrapDoesNotEagerLoadRuntimeSpriteSheets()
        {
            string source = File.ReadAllText(ManagersPath);

            Assert.IsFalse(source.Contains("PreloadPlayerToolSpritesAsync"), "Managers should not preload every player tool sheet during boot.");
            Assert.IsFalse(source.Contains("PreloadSheetsAsync(rm, ModernUISpriteAddresses.AllSheets)"), "Managers should not preload every Modern UI sheet during boot.");
            Assert.IsFalse(source.Contains("PlayerToolSpriteAddresses.AllSheets"), "Managers should leave tool sprites to on-demand runtime loading.");
            Assert.IsFalse(source.Contains("ModernUISpriteAddresses.AllSheets"), "Managers should leave Modern UI sprites to on-demand runtime loading.");
        }

        [Test]
        public void AddressableManifest_DefaultSheetAddressesAreModernFarmNotLegacySheets()
        {
            string source = File.ReadAllText(AddressableManifestPath);

            Assert.IsFalse(source.Contains("sheet/Tile"), "AddressableManifest still exposes legacy tile sheet address.");
            Assert.IsFalse(source.Contains("sheet/Down"), "AddressableManifest still exposes legacy player idle sheet address.");
            StringAssert.Contains("sprites/modernfarm/terrain-16", source);
        }

        [Test]
        public void SpriteAddressableGroup_DoesNotRetainLegacyPixelwoodSheetAddresses()
        {
            string source = File.ReadAllText(SpriteAddressableGroupPath);

            Assert.IsFalse(source.Contains("m_Address: sheet/Tile"), "Sprites Addressables group still exposes the legacy Pixelwood tile sheet address.");
            Assert.IsFalse(source.Contains("m_Address: sheet/Down"), "Sprites Addressables group still exposes the legacy Pixelwood player sheet address.");
        }

        [Test]
        public void SpriteAddressableGroup_DoesNotPointAnyRuntimeAddressAtPixelwoodAssets()
        {
            string source = File.ReadAllText(SpriteAddressableGroupPath);

            Assert.IsFalse(source.Contains("Assets/Pixelwood Valley/"), "Sprites Addressables group still points runtime addresses at Pixelwood assets.");
            Assert.IsFalse(source.Contains("Fantasy Book UI"), "Sprites Addressables group still points runtime addresses at Fantasy Book UI assets.");
        }

        [Test]
        public void SpriteAddressableGroup_DoesNotKeepLegacyFantasyBookUiRuntimeAddresses()
        {
            string source = File.ReadAllText(SpriteAddressableGroupPath);

            Assert.IsFalse(source.Contains("m_Address: sprites/ui/book/"), "Sprites Addressables group still keeps legacy book UI runtime addresses.");
            Assert.IsFalse(source.Contains("m_Address: sprites/ui/panel/"), "Sprites Addressables group still keeps legacy panel UI runtime addresses.");
            Assert.IsFalse(source.Contains("m_Address: sprites/ui/slot/"), "Sprites Addressables group still keeps legacy slot UI runtime addresses.");
            Assert.IsFalse(source.Contains("m_Address: sprites/ui/ribbon/"), "Sprites Addressables group still keeps legacy ribbon UI runtime addresses.");
            Assert.IsFalse(source.Contains("m_Address: sprites/ui/decor/"), "Sprites Addressables group still keeps legacy decor UI runtime addresses.");
            Assert.IsFalse(source.Contains("m_Address: sprites/ui/button/"), "Sprites Addressables group still keeps legacy button UI runtime addresses.");
            Assert.IsFalse(source.Contains("m_Address: sprites/ui/sheet/"), "Sprites Addressables group still keeps legacy sheet UI runtime addresses.");
            Assert.IsFalse(source.Contains("m_Address: sprites/ui/character"), "Sprites Addressables group still keeps legacy character UI runtime addresses.");
        }

        [Test]
        public void ModernPlayerToolFallbackSheets_ExistForAllRuntimeToolAddresses()
        {
            string[] expected =
            {
                "Axe_Down_0.png", "Axe_Side_0.png", "Axe_Up_0.png",
                "Hoe_Down_0.png", "Hoe_Side_0.png", "Hoe_Up_0.png",
                "Pickaxe_Down_0.png", "Pickaxe_Side_0.png", "Pickaxe_Up_0.png",
                "Pickup_Down_0.png", "Pickup_Side_0.png", "Pickup_Up_0.png",
            };

            foreach (string fileName in expected)
            {
                Assert.IsTrue(File.Exists(Path.Combine(ModernToolFallbackFolder, fileName)), $"Missing Modern Farm tool fallback sprite: {fileName}");
            }
        }
    }
}
