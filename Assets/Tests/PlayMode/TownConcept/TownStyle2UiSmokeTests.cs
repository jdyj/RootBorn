using System.Collections;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

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
    }
}