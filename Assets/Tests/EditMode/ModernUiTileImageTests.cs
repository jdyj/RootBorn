using System.Linq;
using NUnit.Framework;
using Rootborn.UI.Modern;
using UnityEngine;

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
                Object.DestroyImmediate(go);
            }
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
                Object.DestroyImmediate(go);
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
                Object.DestroyImmediate(go);
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
                Object.DestroyImmediate(go);
            }
        }
    }
}
