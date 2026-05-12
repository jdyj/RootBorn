using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.UI.Modern;

namespace Rootborn.Tests.EditMode.TownConcept
{
    public sealed class ModernUiStyle2RecipeTests
    {
        private const string ManifestPath = "docs/art/modern-ui-style2-reconstruction-manifest.json";
        private const string FullMapPath = "docs/art/modern-ui-style2-sprite-map.json";

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
        public void Style2FullSliceMap_CoversEverySlicedSprite()
        {
            Assert.IsTrue(File.Exists(FullMapPath), "Missing full Style2 slice map: " + FullMapPath);
            string json = File.ReadAllText(FullMapPath);

            StringAssert.Contains("\"sourceAddress\": \"" + ModernUISpriteAddresses.Style16Alt + "\"", json);
            StringAssert.Contains("\"rows\": 34", json);
            StringAssert.Contains("\"columns\": 49", json);
            StringAssert.Contains("\"spriteCount\": 1666", json);
            StringAssert.Contains("ModernUI_16_Style2_r0_c0", json);
            StringAssert.Contains("ModernUI_16_Style2_r33_c48", json);
            StringAssert.Contains("\"id\": \"button.action\"", json);
        }

        [Test]
        public void Style2Catalog_CoversFullSlicedSheetBounds()
        {
            Assert.AreEqual("ModernUI_16_Style2_r0_c0", ModernUiStyle2Sprites.First);
            Assert.AreEqual("ModernUI_16_Style2_r33_c48", ModernUiStyle2Sprites.Last);
        }

        [Test]
        public void Style2Catalog_ExposesSemanticControlGroupsForButtonsAndRelatedUi()
        {
            AssertHudKey("Button", "Small", "ModernUI_16_Style2_r0_c8");
            AssertHudKey("Button", "Action", "ModernUI_16_Style2_r1_c8");
            AssertHudKey("Button", "Close", "ModernUI_16_Style2_r1_c9");
            AssertHudKey("Slot", "Item", "ModernUI_16_Style2_r0_c4");
            AssertHudKey("Slot", "Equipment", "ModernUI_16_Style2_r0_c5");
            AssertHudKey("Tab", "Title", "ModernUI_16_Style2_r2_c8");
            AssertHudKey("Tab", "Selected", "ModernUI_16_Style2_r2_c9");
            AssertHudKey("Scrollbar", "Thumb", "ModernUI_16_Style2_r2_c3");
            AssertHudKey("Scrollbar", "Track", "ModernUI_16_Style2_r3_c3");
            AssertHudKey("Gauge", "Fill", "ModernUI_16_Style2_r5_c3");
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
        public void RuntimeCommonPanel_UsesStyle2PanelBlockWithR3C1Fill()
        {
            var byRole = ModernUiRecipes.CommonPanel.Tiles.ToDictionary(tile => tile.Role, tile => tile.SubSpriteName);

            Assert.AreEqual("ModernUI_16_Style2_r2_c0", byRole["corner-tl"]);
            Assert.AreEqual("ModernUI_16_Style2_r2_c1", byRole["edge-t"]);
            Assert.AreEqual("ModernUI_16_Style2_r2_c2", byRole["corner-tr"]);
            Assert.AreEqual("ModernUI_16_Style2_r3_c0", byRole["edge-l"]);
            Assert.AreEqual("ModernUI_16_Style2_r3_c1", byRole["fill"]);
            Assert.AreEqual("ModernUI_16_Style2_r3_c2", byRole["edge-r"]);
            Assert.AreEqual("ModernUI_16_Style2_r4_c0", byRole["corner-bl"]);
            Assert.AreEqual("ModernUI_16_Style2_r4_c1", byRole["edge-b"]);
            Assert.AreEqual("ModernUI_16_Style2_r4_c2", byRole["corner-br"]);
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

        private static void AssertHudKey(string nestedTypeName, string fieldName, string expectedSubSpriteName)
        {
            Type nestedType = typeof(ModernUiStyle2Sprites).GetNestedType(nestedTypeName, BindingFlags.Public);
            Assert.NotNull(nestedType, "Missing ModernUiStyle2Sprites." + nestedTypeName + " semantic group.");

            FieldInfo field = nestedType.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(field, "Missing ModernUiStyle2Sprites." + nestedTypeName + "." + fieldName + " semantic sprite.");

            object value = field.GetValue(null);
            Assert.IsInstanceOf<ModernHudSpriteKey>(value, "ModernUiStyle2Sprites." + nestedTypeName + "." + fieldName + " must be a ModernHudSpriteKey.");
            var key = (ModernHudSpriteKey)value;
            Assert.AreEqual(ModernUISpriteAddresses.Style16Alt, key.SheetAddress);
            Assert.AreEqual(expectedSubSpriteName, key.SubSpriteName);
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
