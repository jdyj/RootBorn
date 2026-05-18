using System.Collections;
using NUnit.Framework;
using Rootborn.UI.Interiors;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode.Interiors
{
    public sealed class HouseFurniturePlacementVisualCleanupPlayModeTests
    {
        [UnityTest]
        public IEnumerator HouseFurniturePlacementUi_HidesUnrelatedTopLevelButtonsAndTransparentEmptyPreview()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return WaitForPlacementPanel();
            yield return null;
            yield return null;

            Assert.IsNull(GameObject.Find("WorldStateLogButton"), "House furniture placement must not show the unrelated World Log button over the furniture palette.");
            Assert.IsNull(GameObject.Find("EncyclopediaButton"), "House furniture placement must not show the unrelated Encyclopedia button over the furniture settings panel.");

            var preview = GameObject.Find("SelectedFurniturePreviewIcon");
            Assert.IsNotNull(preview, "The selected furniture preview image should exist.");
            var image = preview.GetComponent<Image>();
            Assert.IsNotNull(image);
            Assert.IsNull(image.sprite, "Before selecting furniture the preview must not show a placeholder sprite.");
            Assert.AreEqual(0f, image.color.a, 0.01f, "Before selecting furniture the preview image must be transparent, not a white block.");
        }

        private static IEnumerator WaitForPlacementPanel()
        {
            for (int i = 0; i < 180; i++)
            {
                if (Object.FindFirstObjectByType<InteriorFurniturePlacementPanel>() != null)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("House furniture placement panel did not appear.");
        }
    }
}
