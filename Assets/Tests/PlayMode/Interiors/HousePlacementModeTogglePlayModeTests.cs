using System.Collections;
using NUnit.Framework;
using Rootborn.UI.Interiors;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.Interiors
{
    public sealed class HousePlacementModeTogglePlayModeTests
    {
        [UnityTest]
        public IEnumerator PlacementModeToggleButton_HidesAndRestoresPlacementModeThroughUiClick()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return WaitForPlacementUi();

            var panel = Object.FindFirstObjectByType<InteriorFurniturePlacementPanel>(FindObjectsInactive.Include);
            var toggle = GameObject.Find("PlacementModeToggleButton")?.GetComponent<Button>();
            Assert.IsNotNull(panel, "House should create the placement panel before the toggle binds to it.");
            Assert.IsNotNull(toggle, "House should expose a player-facing placement mode toggle button.");
            Assert.IsTrue(panel.gameObject.activeSelf, "Placement mode should start visible to preserve the existing House editing flow.");
            Assert.IsTrue(toggle.gameObject.activeInHierarchy, "The toggle must remain visible while placement mode is on.");

            yield return Click(toggle.gameObject);
            Assert.IsFalse(panel.gameObject.activeSelf, "Clicking the toggle should turn placement mode off by hiding the placement panel.");
            Assert.IsTrue(toggle.gameObject.activeInHierarchy, "The toggle must remain visible while placement mode is off so the player can restore it.");
            Assert.IsNull(Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>(), "Turning placement mode off should also remove the active placement preview overlay.");

            yield return Click(toggle.gameObject);
            Assert.IsTrue(panel.gameObject.activeSelf, "Clicking the toggle again should restore placement mode.");
            Assert.IsTrue(toggle.gameObject.activeInHierarchy, "The toggle must remain usable after placement mode is restored.");
        }

        private static IEnumerator WaitForPlacementUi()
        {
            for (int i = 0; i < 180; i++)
            {
                if (Object.FindFirstObjectByType<InteriorFurniturePlacementPanel>(FindObjectsInactive.Include) != null &&
                    GameObject.Find("PlacementModeToggleButton") != null)
                {
                    yield break;
                }

                yield return null;
            }
        }

        private static IEnumerator Click(GameObject target)
        {
            Assert.IsNotNull(EventSystem.current, "UI click flow requires an active EventSystem.");
            var data = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                pointerPress = target,
                rawPointerPress = target,
                eligibleForClick = true
            };

            ExecuteEvents.Execute(target, data, ExecuteEvents.pointerDownHandler);
            yield return null;
            ExecuteEvents.Execute(target, data, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(target, data, ExecuteEvents.pointerClickHandler);
            yield return null;
        }
    }
}
