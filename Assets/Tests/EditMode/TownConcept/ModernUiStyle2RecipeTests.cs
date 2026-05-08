using System;
using System.Collections.Generic;
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

        [Test]
        public void RuntimeWindowRecipes_MatchStyle2ManifestFirstScope()
        {
            string json = File.ReadAllText(ManifestPath);

            AssertRecipeMatchesManifest(json, ModernUiRecipes.SettingsWindow);
            AssertRecipeMatchesManifest(json, ModernUiRecipes.InventoryWindow);
            AssertRecipeMatchesManifest(json, ModernUiRecipes.StatusWindow);
            AssertRecipeMatchesManifest(json, ModernUiRecipes.QuestWindow);
            AssertRecipeMatchesManifest(json, ModernUiRecipes.PopupWindow);
        }

        private static void AssertRecipeMatchesManifest(string json, ModernUiWindowRecipe recipe)
        {
            IReadOnlyList<string> manifestParts = ReadManifestRecipeParts(json, recipe.Name);

            CollectionAssert.AreEqual(recipe.RequiredParts, manifestParts, recipe.Name + " recipe must match the Style2 manifest exactly.");
        }

        private static IReadOnlyList<string> ReadManifestRecipeParts(string json, string recipeName)
        {
            string marker = "\"" + recipeName + "\":";
            int markerIndex = json.IndexOf(marker, StringComparison.Ordinal);
            Assert.GreaterOrEqual(markerIndex, 0, "Missing manifest recipe: " + recipeName);

            int openIndex = json.IndexOf('[', markerIndex);
            int closeIndex = json.IndexOf(']', openIndex);
            Assert.Greater(openIndex, markerIndex, "Missing manifest recipe array: " + recipeName);
            Assert.Greater(closeIndex, openIndex, "Unclosed manifest recipe array: " + recipeName);

            string body = json.Substring(openIndex + 1, closeIndex - openIndex - 1);
            string[] rawParts = body.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var parts = new List<string>();
            for (int i = 0; i < rawParts.Length; i++)
            {
                parts.Add(rawParts[i].Trim().Trim('"'));
            }

            return parts;
        }
    }
}
