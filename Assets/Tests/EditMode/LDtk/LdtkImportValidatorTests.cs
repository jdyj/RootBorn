using System.Linq;
using NUnit.Framework;
using Rootborn.Editor.LDtk;

namespace Rootborn.Tests.EditMode.LDtk
{
    public sealed class LdtkImportValidatorTests
    {
        [Test]
        public void LDTK_IMPORT_EDIT_001_TownGeneratedTilesMatchOriginalTilesetRects()
        {
            AssertNoIssues(LdtkImportValidator.ValidateGeneratedTileRects("Assets/LDtk/Generated/TownTiles"), "rect");
        }

        [Test]
        public void LDTK_IMPORT_EDIT_002_InteriorGeneratedTilesMatchOriginalTilesetRects()
        {
            AssertNoIssues(LdtkImportValidator.ValidateGeneratedTileRects("Assets/LDtk/Generated/InteriorTiles"), "rect");
        }

        [Test]
        public void LDTK_IMPORT_EDIT_003_LDtkTilesetTexturesUsePixelPerfectImportSettings()
        {
            AssertNoIssues(LdtkImportValidator.ValidateTextureImportSettings(new[]
            {
                "Assets/LDtk/Generated/TownTiles",
                "Assets/LDtk/Generated/InteriorTiles"
            }), "PPU");
        }

        [Test]
        public void LDTK_IMPORT_EDIT_004_TownAndInteriorLayerSortingMatchesLDtkOrder()
        {
            AssertNoIssues(LdtkImportValidator.ValidateSavedSceneLayerSorting(), "sorting");
        }

        [Test]
        public void LDTK_IMPORT_EDIT_005_NoStaleOrDuplicateGeneratedLDtkTilesAreVisibleToImport()
        {
            AssertNoIssues(LdtkImportValidator.ValidateGeneratedTileAssetHygiene(new[]
            {
                "Assets/LDtk/Generated/TownTiles",
                "Assets/LDtk/Generated/InteriorTiles"
            }), "stale asset");
        }

        private static void AssertNoIssues(System.Collections.Generic.IReadOnlyList<LdtkImportIssue> issues, string category)
        {
            string message = string.Join("\n", issues.Select(issue => issue.Category + ": " + issue.AssetPath + " - " + issue.Message));
            Assert.IsEmpty(issues, "Expected no LDtk import issues while validating category '" + category + "'.\n" + message);
        }
    }
}