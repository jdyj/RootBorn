using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.UI.Modern;

namespace Rootborn.Tests.EditMode.UI.Modern
{
    public sealed class DialoguePanelRecipeTests
    {
        [Test]
        public void DIALOGUE_RECIPE_001_NineTilesAtR14ToR16C3ToC5()
        {
            var tiles = ModernUiStyle2Sprites.DialoguePanel.Tiles;
            Assert.AreEqual(9, tiles.Count, "DialoguePanel must contain exactly 9 tiles (3x3 9-slice).");

            for (int i = 0; i < tiles.Count; i++)
            {
                int row = tiles[i].Row;
                int column = tiles[i].Column;
                Assert.IsTrue(row >= 14 && row <= 16, $"Tile {i} row {row} out of [14,16].");
                Assert.IsTrue(column >= 3 && column <= 5, $"Tile {i} column {column} out of [3,5].");
            }

            int distinct = tiles.Select(t => (t.Row, t.Column)).Distinct().Count();
            Assert.AreEqual(9, distinct, "All 9 (row,column) pairs must be distinct.");
        }

        [Test]
        public void DIALOGUE_RECIPE_002_AllNineRolesPresent()
        {
            var recipe = ModernUiRecipes.DialoguePanel;
            string[] roles = { "corner-tl", "edge-t", "corner-tr", "edge-l", "fill", "edge-r", "corner-bl", "edge-b", "corner-br" };
            for (int i = 0; i < roles.Length; i++)
            {
                var key = recipe.FindByRole(roles[i]);
                Assert.AreEqual(roles[i], key.Role, $"Role '{roles[i]}' must be present in DialoguePanel recipe.");
                Assert.IsNotEmpty(key.SubSpriteName ?? string.Empty, $"Role '{roles[i]}' sub-sprite name must be non-empty.");
            }
        }

        [Test]
        public void DIALOGUE_PRELOAD_001_AllNineNamesInPreload()
        {
            var preload = ModernUiStyle2Sprites.PreloadSubSprites;
            string[] expected =
            {
                ModernUiStyle2Sprites.DialoguePanel.TopLeftName,
                ModernUiStyle2Sprites.DialoguePanel.TopName,
                ModernUiStyle2Sprites.DialoguePanel.TopRightName,
                ModernUiStyle2Sprites.DialoguePanel.LeftName,
                ModernUiStyle2Sprites.DialoguePanel.FillName,
                ModernUiStyle2Sprites.DialoguePanel.RightName,
                ModernUiStyle2Sprites.DialoguePanel.BottomLeftName,
                ModernUiStyle2Sprites.DialoguePanel.BottomName,
                ModernUiStyle2Sprites.DialoguePanel.BottomRightName,
            };

            for (int i = 0; i < expected.Length; i++)
            {
                CollectionAssert.Contains(preload, expected[i], $"PreloadSubSprites must contain '{expected[i]}'.");
            }
        }

        [Test]
        public void DIALOGUE_RECIPE_REGISTERED_001_RegistryExposesSameTiles()
        {
            Assert.IsNotNull(ModernUiRecipes.DialoguePanel, "ModernUiRecipes.DialoguePanel must be registered.");
            Assert.AreSame(ModernUiStyle2Sprites.DialoguePanel.Tiles, ModernUiRecipes.DialoguePanel.Tiles,
                "Recipe must reference the same Tiles list as the Style2 catalog.");
        }
    }
}
