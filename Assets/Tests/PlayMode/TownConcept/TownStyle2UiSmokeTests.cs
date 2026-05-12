using System.Collections;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.TownConcept
{
    public sealed class TownStyle2UiSmokeTests
    {
        [UnityTest]
        public IEnumerator TownScene_ModernUiCommonPanelUsesStyle2()
        {
            yield return SceneManager.LoadSceneAsync("Town", LoadSceneMode.Single);
            yield return null;

            foreach (ModernUiSpriteKey tile in ModernUiRecipes.CommonPanel.Tiles)
            {
                Assert.AreEqual(ModernUISpriteAddresses.Style16Alt, tile.SheetAddress);
                StringAssert.StartsWith("ModernUI_16_Style2_", tile.SubSpriteName);
            }
        }

        [UnityTest]
        public IEnumerator TownScene_HasNonEmptyVisualRoots()
        {
            yield return SceneManager.LoadSceneAsync("Town", LoadSceneMode.Single);
            yield return null;

            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            Assert.Greater(roots.Length, 0);

            bool hasRendererOrCanvas = false;
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].GetComponentInChildren<Renderer>(true) != null ||
                    roots[i].GetComponentInChildren<Canvas>(true) != null)
                {
                    hasRendererOrCanvas = true;
                    break;
                }
            }

            Assert.IsTrue(hasRendererOrCanvas, "Town scene should expose visible world or UI roots.");
        }

        [UnityTest]
        public IEnumerator TownScene_InventoryPanelRendersResolvedStyle2Sprites()
        {
            yield return SceneManager.LoadSceneAsync("Town", LoadSceneMode.Single);
            yield return WaitForModernUiRouter();

            Canvas canvas = FindInActiveScene<Canvas>();
            Assert.IsNotNull(canvas, "Town scene should have a Canvas.");
            var router = canvas.GetComponent<ModernUiPanelInputRouter>();
            Assert.IsNotNull(router, "Town scene should install ModernUiPanelInputRouter.");

            router.ToggleInventoryPanel();
            yield return null;

            var tileImages = canvas.GetComponentsInChildren<Image>(true);
            int style2SpriteImageCount = 0;
            var missingSprites = new System.Collections.Generic.List<string>();
            for (int i = 0; i < tileImages.Length; i++)
            {
                Image image = tileImages[i];
                if (image.gameObject.name.StartsWith("Tile_") && image.transform.parent != null)
                {
                    if (image.sprite == null)
                    {
                        missingSprites.Add(image.transform.parent.name + "/" + image.gameObject.name);
                    }
                    else if (image.sprite.name.StartsWith("ModernUI_16_Style2_"))
                    {
                        style2SpriteImageCount++;
                    }
                }
            }

            Assert.IsEmpty(missingSprites, "Modern UI tile Images are rendering without resolved sprites: " + string.Join(", ", missingSprites));
            Assert.Greater(style2SpriteImageCount, 0, "Town inventory panel should render actual Style2 sprite Images, not only colored placeholders.");
        }

        private static IEnumerator WaitForModernUiRouter()
        {
            for (int i = 0; i < 120; i++)
            {
                Canvas canvas = FindInActiveScene<Canvas>();
                if (canvas != null && canvas.GetComponent<ModernUiPanelInputRouter>() != null)
                {
                    yield break;
                }

                yield return null;
            }
        }

        private static T FindInActiveScene<T>() where T : Component
        {
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var match = roots[i].GetComponentInChildren<T>(true);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }
    }
}
