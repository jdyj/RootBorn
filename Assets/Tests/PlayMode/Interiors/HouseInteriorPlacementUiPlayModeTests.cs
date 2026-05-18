using System.Collections;
using NUnit.Framework;
using Rootborn.Game.Interiors;
using Rootborn.UI.Interiors;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.Interiors
{
    public sealed class HouseInteriorPlacementUiPlayModeTests
    {
        [UnityTest]
        public IEnumerator HouseScene_CreatesFurniturePlacementPanelWithModernCommonPanels()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var panel = Object.FindFirstObjectByType<InteriorFurniturePlacementPanel>();
            Assert.IsNotNull(panel, "House should create the developer interior furniture placement panel.");
            Assert.IsNotNull(EventSystem.current, "The player-facing furniture UI should install an EventSystem for real mouse interaction.");
            Assert.IsNotNull(Object.FindFirstObjectByType<FurniturePlacementPointerRouter>(), "The placement panel should consume mouse clicks on UI before the world placement overlay sees them.");
            Assert.IsNotNull(GameObject.Find("FurniturePalette"));
            Assert.IsNotNull(GameObject.Find("SelectedFurnitureSettings"));
            Assert.IsNotNull(GameObject.Find("InteriorPlacementToolbar"));
            Assert.IsNotNull(GameObject.Find("InteriorPlacementBottomBar"));
            Assert.IsNotNull(GameObject.Find("SeedInput")?.GetComponent<InputField>());
            Assert.IsNotNull(GameObject.Find("RegenerateButton")?.GetComponent<Button>());
            Assert.IsNotNull(GameObject.Find("ApplyButton")?.GetComponent<Button>());
            AssertModernPanel("FurniturePalette");
            AssertModernPanel("SelectedFurnitureSettings");
            AssertModernPanel("InteriorPlacementToolbar");
            AssertModernPanel("InteriorPlacementBottomBar");
        }

        [UnityTest]
        public IEnumerator HouseScene_FurnitureButtonsUseMockupCategoryIconGrid()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;

            Assert.IsNotNull(GameObject.Find("FurnitureCategory_Chair"), "The palette should group available furniture under Korean category headers like the mockup.");
            Assert.IsNull(GameObject.Find("Palette_Desk"), "Generic object-kind buttons must not exist because they trigger desk/chair/computer cluster placement.");
            AssertIconFurnitureButton("PaletteFurniture_chair.black");
            AssertIconFurnitureButton("PaletteFurniture_desk.large");
        }

        [UnityTest]
        public IEnumerator RegenerateButton_SeedChangeUpdatesHouseLayoutWithoutAutoFurniture()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var walls = GameObject.Find("HouseWallTilemap")?.GetComponent<Tilemap>();
            var decorations = GameObject.Find("HouseDecorationTilemap")?.GetComponent<Tilemap>();
            Assert.IsNotNull(walls);
            Assert.IsNotNull(decorations);
            var before = Signature(walls);

            var seedInput = GameObject.Find("SeedInput")?.GetComponent<InputField>();
            Assert.IsNotNull(seedInput);
            seedInput.text = "8801";
            GameObject.Find("RegenerateButton").GetComponent<Button>().onClick.Invoke();
            yield return null;
            yield return null;

            var after = Signature(walls);
            Assert.AreNotEqual(before, after, "Changing the seed and regenerating should immediately apply a different House layout.");
            AssertNoLegacyGeneratedFurniture(decorations);
            var status = GameObject.Find("InteriorPlacementStatusText")?.GetComponent<Text>();
            Assert.IsNotNull(status);
            StringAssert.Contains("Walkable OK", status.text);
            Assert.IsNull(Object.FindFirstObjectByType<InteriorObjectInteractor>(), "Manual placement mode should not spawn computer interactors until a computer object is explicitly placed.");
        }

        [UnityTest]
        public IEnumerator WorldClickWithoutSelectedFurniture_DoesNotPlaceGenericDeskCluster()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;

            Assert.IsNull(GameObject.Find("Palette_Desk"));
            var decorations = GameObject.Find("HouseDecorationTilemap")?.GetComponent<Tilemap>();
            Assert.IsNotNull(decorations);
            AssertNoLegacyGeneratedFurniture(decorations);

            var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>();
            Assert.IsNull(overlay, "No placement overlay should be active before the player chooses an actual furniture item from the palette.");
        }

        private static void AssertModernPanel(string name)
        {
            var tileImage = GameObject.Find(name)?.GetComponent<ModernUiTileImage>();
            Assert.IsNotNull(tileImage, name + " should use ModernUiTileImage instead of a flat Image background.");
            Assert.IsNotNull(tileImage.Recipe, name + " must use a tiled Modern UI recipe.");
            Assert.AreNotEqual(Vector2.zero, tileImage.TileSize, name + " must tile panel sprites.");
        }

        private static void AssertIconFurnitureButton(string name)
        {
            var button = GameObject.Find(name)?.GetComponent<Button>();
            Assert.IsNotNull(button, name + " should be visible as a selectable furniture object button.");
            Assert.IsTrue(button.gameObject.activeInHierarchy, name + " should be active in the palette.");
            Assert.IsTrue(button.interactable, name + " should be clickable.");

            var rect = button.GetComponent<RectTransform>();
            Assert.GreaterOrEqual(rect.sizeDelta.x, 64f, name + " should use a mockup-style icon tile button.");
            Assert.GreaterOrEqual(rect.sizeDelta.y, 64f, name + " should use a mockup-style icon tile button.");
            var icon = FindChild(button.transform, "Icon").GetComponent<Image>();
            Assert.IsNotNull(icon.sprite, name + " should display its furniture sprite preview, not only text.");
        }

        private static void AssertNoLegacyGeneratedFurniture(Tilemap tilemap)
        {
            Assert.AreEqual(0, CountTilesContaining(tilemap, "Desk"), "Manual placement mode must not auto-generate desks.");
            Assert.AreEqual(0, CountTilesContaining(tilemap, "Computer"), "Manual placement mode must not auto-generate computer clusters.");
            Assert.AreEqual(0, CountTilesContaining(tilemap, "Chair"), "Manual placement mode must not auto-generate chairs.");
            Assert.AreEqual(0, CountTilesContaining(tilemap, "Sofa"), "Manual placement mode must not auto-generate sofas.");
        }

        private static int CountTilesContaining(Tilemap tilemap, string text)
        {
            int count = 0;
            foreach (var position in tilemap.cellBounds.allPositionsWithin)
            {
                var tile = tilemap.GetTile(position);
                if (tile != null && tile.name.Contains(text))
                {
                    count++;
                }
            }

            return count;
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return child;
                }
            }

            Assert.Fail("Missing child " + name + " under " + root.name);
            return null;
        }

        private static string Signature(Tilemap tilemap)
        {
            var bounds = tilemap.cellBounds;
            var signature = string.Empty;
            foreach (var position in bounds.allPositionsWithin)
            {
                var tile = tilemap.GetTile(position);
                if (tile != null)
                {
                    signature += position.x + ":" + position.y + ":" + tile.name + ";";
                }
            }

            return signature;
        }
    }
}
