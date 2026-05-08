using System.IO;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.UI.Modern;

namespace Rootborn.Tests.EditMode.TownConcept
{
    public sealed class ModernUiStyle2RecipeTests
    {
        private const string ManifestPath = "docs/art/modern-ui-style2-reconstruction-manifest.json";

        [Test]
        public void Style2Manifest_UsesStyle2AddressAndSpriteNames()
        {
            string json = File.ReadAllText(ManifestPath);

            StringAssert.Contains("\"sourceAddress\": \"" + ModernUISpriteAddresses.Style16Alt + "\"", json);
            StringAssert.DoesNotContain("sprites/ui/modern/16/style-1", json);
            StringAssert.DoesNotContain("ModernUI_16_Style1", json);
            StringAssert.Contains("ModernUI_16_Style2_r0_c0", json);
        }

        [Test]
        public void RuntimeCommonPanel_UsesStyle2SpritesForTownBaseline()
        {
            foreach (ModernUiSpriteKey tile in ModernUiRecipes.CommonPanel.Tiles)
            {
                Assert.AreEqual(ModernUISpriteAddresses.Style16Alt, tile.SheetAddress);
                StringAssert.StartsWith("ModernUI_16_Style2_", tile.SubSpriteName);
            }
        }
    }
}
