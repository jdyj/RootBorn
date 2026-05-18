using System.Collections;
using NUnit.Framework;
using Rootborn.UI.Interiors;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.Interiors
{
    public sealed class HouseInteriorPlacementAvailabilityPlayModeTests
    {
        private const string ChairButtonName = "PaletteFurniture_chair.black";
        private const string DeskButtonName = "PaletteFurniture_desk.large";
        private const string ChairTileName = "BlackChair_00";
        private const string DeskTileName = "ShadowlessOffice_Desk_Table_r39_c01";

        [UnityTest]
        public IEnumerator SelectingFurniture_ShowsPlacementAvailabilityOverlayAndCounts()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;

            GameObject.Find(ChairButtonName).GetComponent<Button>().onClick.Invoke();
            yield return null;

            var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>();
            Assert.IsNotNull(overlay, "Selecting an actual furniture object should create an availability overlay Tilemap.");
            Assert.Greater(overlay.ValidCellCount, 0, "Overlay should mark valid cells for the selected furniture.");
            Assert.Greater(overlay.InvalidCellCount, 0, "Overlay should distinguish invalid cells from valid cells.");

            var text = GameObject.Find("PlacementAvailabilityText")?.GetComponent<Text>();
            Assert.IsNotNull(text, "Settings panel should show available/requested placement counts.");
            StringAssert.Contains("available", text.text);
            StringAssert.Contains("requested", text.text);
        }

        [UnityTest]
        public IEnumerator ManualPlacementMode_PlacesSelectedFurnitureObjectOnValidCellAndRefreshesTilemap()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;

            GameObject.Find(DeskButtonName).GetComponent<Button>().onClick.Invoke();
            yield return null;

            var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>();
            Assert.IsNotNull(overlay);
            var furnitureTilemap = GameObject.Find("HouseFurnitureObjectTilemap")?.GetComponent<Tilemap>();
            int before = furnitureTilemap != null ? CountTiles(furnitureTilemap, DeskTileName) : 0;

            Assert.IsTrue(overlay.TryManualPlaceFirstValidForTests(out var message));
            yield return null;

            StringAssert.Contains("desk.large", message);
            furnitureTilemap = GameObject.Find("HouseFurnitureObjectTilemap")?.GetComponent<Tilemap>();
            Assert.IsNotNull(furnitureTilemap);
            Assert.Greater(CountTiles(furnitureTilemap, DeskTileName), before, "Manual placement should place the selected registry furniture object, not a generic desk cluster request.");
        }

        [UnityTest]
        public IEnumerator BeforeSelectingFurniture_DoesNotShowGenericRequestPreflight()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;

            Assert.IsNull(GameObject.Find("Palette_Sofa"), "Generic category request buttons must not be exposed in the mockup furniture picker.");
            Assert.IsNull(Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>(), "The availability overlay should wait until the player selects a concrete furniture item.");

            var text = GameObject.Find("PlacementAvailabilityText")?.GetComponent<Text>();
            Assert.IsNotNull(text);
            StringAssert.Contains("가구", text.text);
        }

        private static int CountTiles(Tilemap tilemap, string tileName)
        {
            int count = 0;
            foreach (var position in tilemap.cellBounds.allPositionsWithin)
            {
                var tile = tilemap.GetTile(position);
                if (tile != null && tile.name == tileName)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
