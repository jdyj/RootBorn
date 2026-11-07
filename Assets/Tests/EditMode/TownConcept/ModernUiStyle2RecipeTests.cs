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
            // Slot/Tab/Scrollbar are mapped onto Gemini-confirmed semantic coordinates from
            // docs/art/modern-ui-style2-icon-catalog.json. The legacy r0_c4..r2_c9 region was
            // empty cells, which caused inventory slots and tabs to render as white boxes.
            AssertHudKey("Button", "Small", "ModernUI_16_Style2_r0_c8");
            AssertHudKey("Button", "Action", "ModernUI_16_Style2_r1_c8");
            AssertHudKey("Button", "Close", "ModernUI_16_Style2_r1_c9");
            AssertHudKey("Slot", "Item", "ModernUI_16_Style2_r6_c0");
            AssertHudKey("Slot", "Equipment", "ModernUI_16_Style2_r6_c0");
            AssertHudKey("Slot", "Base", "ModernUI_16_Style2_r6_c0");
            AssertHudKey("Slot", "Highlight", "ModernUI_16_Style2_r7_c0");
            // Tabs use the whole 22x22 box cut by an explicit rect: on the sheet it straddles
            // r0..r2 / c3..c5, so no single 16px cell holds it.
            AssertHudKey("Tab", "All", "ModernUI_16_Style2_tab_box");
            AssertHudKey("Tab", "Title", "ModernUI_16_Style2_tab_box");
            AssertHudKey("Tab", "Selected", "ModernUI_16_Style2_tab_box");
            AssertHudKey("Tab", "Misc", "ModernUI_16_Style2_tab_box");
            // The horizontal ribbon and the long track use the r3_c4..r3_c6 bar (left | middle | right).
            AssertHudKey("TabRibbon", "Left", "ModernUI_16_Style2_r3_c4");
            AssertHudKey("TabRibbon", "Middle", "ModernUI_16_Style2_r3_c5");
            AssertHudKey("TabRibbon", "Right", "ModernUI_16_Style2_r3_c6");
            AssertHudKey("Scrollbar", "TrackTop", "ModernUI_16_Style2_r3_c3");
            AssertHudKey("Scrollbar", "TrackMiddle", "ModernUI_16_Style2_r3_c5");
            AssertHudKey("Scrollbar", "TrackBottom", "ModernUI_16_Style2_r3_c3");
            AssertHudKey("IconButton", "Search", "ModernUI_16_Style2_r12_c46");
            AssertHudKey("IconButton", "Delete", "ModernUI_16_Style2_r13_c46");
            AssertHudKey("IconButton", "Settings", "ModernUI_16_Style2_r6_c46");
            AssertHudKey("Gauge", "Fill", "ModernUI_16_Style2_r3_c3");
        }

        [Test]
        public void Style2TabBox_IsCutFromTheWholeBoxOnTheSheet()
        {
            UnityEngine.Sprite box = LoadStyle2Sprite(ModernUiStyle2Sprites.TabBoxName);
            Assert.NotNull(box, "Style2 sheet has no " + ModernUiStyle2Sprites.TabBoxName + " sub-sprite. Run Rootborn/Modern UI/Slice Sprite Sheets.");

            // PNG top-left (61,13) size 22x22 -> Unity bottom-left y = 544 - 13 - 22 = 509.
            Assert.AreEqual(new UnityEngine.Rect(61, 509, 22, 22), box.rect);
        }

        // Thumb, Gauge.Fill and the track bottom must hold a whole drawn box, not a slice of a larger one:
        // nothing drawn may touch the cell's outer edge.
        [Test]
        public void Style2ThumbAndGaugeFill_HoldTheWholeHandleBox()
        {
            foreach (string name in new[] { ModernUiStyle2Sprites.Scrollbar.ThumbName, ModernUiStyle2Sprites.Gauge.FillName, ModernUiStyle2Sprites.Scrollbar.TrackBottomName })
            {
                UnityEngine.Sprite sprite = LoadStyle2Sprite(name);
                Assert.NotNull(sprite, name);
                var r = sprite.rect;
                UnityEngine.Color32[] pixels = sprite.texture.GetPixels32();
                int width = sprite.texture.width, x0 = (int)r.x, y0 = (int)r.y, x1 = x0 + (int)r.width - 1, y1 = y0 + (int)r.height - 1;
                int drawn = 0;
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        if (pixels[y * width + x].a == 0) continue;
                        drawn++;
                        Assert.IsFalse(x == x0 || x == x1 || y == y0 || y == y1, name + " is cut from a larger box: drawn pixel on its edge at (" + x + "," + y + ").");
                    }
                }

                Assert.Greater(drawn, 0, name + " is empty.");
            }
        }

        [Test]
        public void Style2PreloadSprites_AllHaveVisiblePixelsOnTheSheet()
        {
            var blank = new List<string>();
            foreach (string name in ModernUiStyle2Sprites.PreloadSubSprites.Distinct())
            {
                UnityEngine.Sprite sprite = LoadStyle2Sprite(name);
                if (sprite == null)
                {
                    blank.Add(name + " (missing)");
                    continue;
                }

                var r = sprite.rect;
                UnityEngine.Color32[] pixels = sprite.texture.GetPixels32();
                int width = sprite.texture.width;
                bool visible = false;
                for (int y = (int)r.y; y < (int)(r.y + r.height) && !visible; y++)
                {
                    for (int x = (int)r.x; x < (int)(r.x + r.width); x++)
                    {
                        if (pixels[y * width + x].a > 0) { visible = true; break; }
                    }
                }

                if (!visible) blank.Add(name);
            }

            Assert.IsEmpty(blank, "Style2 preload sprites with no visible pixels: " + string.Join(", ", blank));
        }

        private static UnityEngine.Sprite LoadStyle2Sprite(string name)
        {
            foreach (UnityEngine.Object asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/modernuserinterface-win/16x16/Modern_UI_Style_2.png"))
            {
                if (asset is UnityEngine.Sprite sprite && sprite.name == name) return sprite;
            }

            return null;
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
