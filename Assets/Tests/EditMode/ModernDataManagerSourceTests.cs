using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode
{
    public sealed class ModernDataManagerSourceTests
    {
        private const string DataManagerPath = "Assets/Scripts/Game/Managers/DataManager.cs";

        [Test]
        public void DataManager_UsesRegistrySpritesInsteadOfLegacyPixelwoodSheetAddresses()
        {
            string source = File.ReadAllText(DataManagerPath);

            Assert.IsFalse(source.Contains("sheet/Tile"), "DataManager still references the legacy Pixelwood tile sheet address.");
            Assert.IsFalse(source.Contains("sheet/Down"), "DataManager still references the legacy Pixelwood player idle sheet address.");
            Assert.IsFalse(source.Contains("LoadSubSpriteAsync(AddrSheetTile"), "Ground sprite should come from GameDataRegistry, not a legacy sheet lookup.");
            Assert.IsFalse(source.Contains("LoadSubSpriteAsync(AddrSheetIdleDown"), "Player sprite should come from GameDataRegistry, not a legacy sheet lookup.");
            StringAssert.Contains("GroundSprite = Registry.GroundSprite", source);
            StringAssert.Contains("PlayerSprite = Registry.PlayerSprite", source);
        }
    }
}
