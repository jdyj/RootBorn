using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.Tests.EditMode
{
    public sealed class ModernUiTileImageTests
    {
        [Test]
        public void Rebuild_CreatesDeterministicSixteenPixelPanelTiles()
        {
            var go = new GameObject("ModernTileTest", typeof(RectTransform), typeof(ModernUiTileImage));
            try
            {
                var rect = go.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(160f, 96f);

                var tileImage = go.GetComponent<ModernUiTileImage>();
                tileImage.SetRecipe(ModernUiRecipes.CommonPanel);
                tileImage.Rebuild();

                Assert.GreaterOrEqual(tileImage.TileCount, 30);
                Assert.AreEqual(4, tileImage.CornerTileCount);
                Assert.AreEqual(new Vector2(16f, 16f), tileImage.TileSize);
                Assert.IsFalse(tileImage.HasStretchedCornerTiles);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Builder_CreateCommonPanel48_UsesReusable48RecipeAndTileSize()
        {
            var parent = new GameObject("Parent", typeof(RectTransform));
            try
            {
                var panel = ModernUiPanelBuilder.CreateCommonPanel48(parent.transform, "Panel", Vector2.zero, new Vector2(192f, 96f));
                var tileImage = panel.GetComponent<ModernUiTileImage>();

                Assert.IsNotNull(tileImage);
                Assert.AreSame(ModernUiRecipes.CommonPanel48, tileImage.Recipe);
                Assert.AreEqual(new Vector2(48f, 48f), tileImage.TileSize);
                Assert.Greater(tileImage.TileCount, 0);
                Assert.IsNull(panel.GetComponent<Image>(), "Static panels should not add a root Image unless they need a hit target.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void Builder_CreateCommonPanel48Button_UsesTransparentHitTargetAndReusablePanelArt()
        {
            var parent = new GameObject("Parent", typeof(RectTransform));
            try
            {
                var button = ModernUiPanelBuilder.CreateCommonPanel48Button(parent.transform, "Button", Vector2.zero, new Vector2(144f, 48f));
                var tileImage = button.GetComponent<ModernUiTileImage>();
                var image = button.GetComponent<Image>();
                var unityButton = button.GetComponent<Button>();

                Assert.IsNotNull(tileImage);
                Assert.AreSame(ModernUiRecipes.CommonPanel48, tileImage.Recipe);
                Assert.AreEqual(new Vector2(48f, 48f), tileImage.TileSize);
                Assert.IsNotNull(image);
                Assert.AreEqual(0f, image.color.a);
                Assert.IsTrue(image.raycastTarget);
                Assert.AreSame(image, unityButton.targetGraphic);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void QuestLogPanel_UsesReusableModernPanelBuilderForPanelChrome()
        {
            string source = File.ReadAllText("Assets/Scripts/UI/Quests/QuestLogPanel.cs");

            StringAssert.Contains("ModernUiPanelBuilder.CreateCommonPanel48", source);
            StringAssert.Contains("ModernUiPanelBuilder.CreatePlainButton", source);
            StringAssert.DoesNotContain("new GameObject(\"QuestTitleTab\", typeof(RectTransform), typeof(ModernUiTileImage))", source);
            StringAssert.DoesNotContain("SetRecipe(ModernUiRecipes.CommonPanel48)", source);
        }

        [Test]
        public void Rebuild_AlignsGeneratedTilesToRectPivotBounds()
        {
            var go = new GameObject("ModernTileBoundsTest", typeof(RectTransform), typeof(ModernUiTileImage));
            try
            {
                var rect = go.GetComponent<RectTransform>();
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(160f, 96f);

                var tileImage = go.GetComponent<ModernUiTileImage>();
                tileImage.SetRecipe(ModernUiRecipes.CommonPanel);
                tileImage.Rebuild();

                var tileRects = go.GetComponentsInChildren<RectTransform>()
                    .Where(child => child != rect && child.name.StartsWith("Tile_"))
                    .ToArray();

                Assert.AreEqual(tileImage.TileCount, tileRects.Length);
                Assert.AreEqual(-80f, tileRects.Min(child => child.anchoredPosition.x));
                Assert.AreEqual(64f, tileRects.Max(child => child.anchoredPosition.x));
                Assert.AreEqual(48f, tileRects.Max(child => child.anchoredPosition.y));
                Assert.AreEqual(-32f, tileRects.Min(child => child.anchoredPosition.y));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Rebuild_UsesParentPivotAsGeneratedTileAnchor()
        {
            var go = new GameObject("ModernTileAnchorTest", typeof(RectTransform), typeof(ModernUiTileImage));
            try
            {
                var rect = go.GetComponent<RectTransform>();
                rect.pivot = new Vector2(0f, 0.5f);
                rect.sizeDelta = new Vector2(96f, 48f);

                var tileImage = go.GetComponent<ModernUiTileImage>();
                tileImage.SetRecipe(ModernUiRecipes.CommonPanel);
                tileImage.Rebuild();

                var tileRects = go.GetComponentsInChildren<RectTransform>()
                    .Where(child => child != rect && child.name.StartsWith("Tile_"))
                    .ToArray();

                Assert.IsNotEmpty(tileRects);
                Assert.IsTrue(tileRects.All(child => child.anchorMin == rect.pivot && child.anchorMax == rect.pivot));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Rebuild_KeepsGeneratedTilesBehindExistingContent()
        {
            var go = new GameObject("ModernTileLayeringTest", typeof(RectTransform), typeof(ModernUiTileImage));
            try
            {
                var rect = go.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(96f, 48f);
                var content = new GameObject("Content", typeof(RectTransform));
                content.transform.SetParent(go.transform, false);

                var tileImage = go.GetComponent<ModernUiTileImage>();
                tileImage.SetRecipe(ModernUiRecipes.CommonPanel);
                tileImage.Rebuild();

                Assert.Greater(content.transform.GetSiblingIndex(), 0);
                Assert.IsTrue(go.transform.GetChild(0).name.StartsWith("Tile_"));
                Assert.AreEqual("Content", go.transform.GetChild(go.transform.childCount - 1).name);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Rebuild_UsesFallbackSpriteWhenResolverCannotLoadStyle2()
        {
            var go = new GameObject("ModernTileFallbackTest", typeof(RectTransform), typeof(ModernUiTileImage));
            try
            {
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(48f, 48f);
                var tileImage = go.GetComponent<ModernUiTileImage>();
                tileImage.SetResolver(new ThrowingResolver());

                Assert.DoesNotThrow(() => tileImage.Rebuild());

                var images = go.GetComponentsInChildren<Image>(true).Where(image => image.gameObject.name.StartsWith("Tile_")).ToArray();
                Assert.IsNotEmpty(images);
                Assert.IsTrue(images.All(image => image.sprite != null));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private sealed class ThrowingResolver : IModernUiSpriteResolver
        {
            public Sprite Resolve(ModernUiSpriteKey key)
            {
                throw new InvalidOperationException("missing style2 sprite");
            }
        }
    }
}
