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
    public sealed class HouseInteriorFurniturePlacementPlayModeTests
    {
        private const string ChairButtonName = "PaletteFurniture_chair.black";
        private const string BasicDeskButtonName = "PaletteFurniture_desk.basic";
        private const string DeskButtonName = "PaletteFurniture_desk.large";
        private const string ChairFurnitureId = "chair.black";
        private const string ChairTileName = "BlackChair_00";
        private const string BasicDeskFurnitureId = "desk.basic";
        private const string DeskFurnitureId = "desk.large";
        private const string DeskTileName = "ShadowlessOffice_Desk_Table_r39_c01";
        private const string ComputerTileName = "ShadowlessOffice_Computer_Monitor_r44_c08";

        [UnityTest]
        public IEnumerator HousePlacementUi_LoadsChairFurnitureObjectButtons()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;

            Assert.IsNotNull(GameObject.Find(ChairButtonName), "House placement UI should expose registry-backed black chair furniture objects in-game.");
            Assert.IsNull(GameObject.Find("ModernOfficeChairSampleTilemap"), "Debug/sample display tilemaps must not be visible in the playable House scene.");

            var text = GameObject.Find("PlacementAvailabilityText")?.GetComponent<Text>();
            Assert.IsNotNull(text);
            StringAssert.Contains("가구", text.text);
        }

        [UnityTest]
        public IEnumerator SelectedChairFurniture_PlacesSelectedTileIntoRuntimeFurnitureTilemap()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;

            GameObject.Find(ChairButtonName).GetComponent<Button>().onClick.Invoke();
            yield return null;

            var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>();
            Assert.IsNotNull(overlay);
            Assert.IsTrue(overlay.TryManualPlaceFirstValidForTests(out var message));
            yield return null;

            StringAssert.Contains(ChairFurnitureId, message);
            var furnitureTilemap = GameObject.Find("HouseFurnitureObjectTilemap")?.GetComponent<Tilemap>();
            Assert.IsNotNull(furnitureTilemap, "Furniture placement should render selected chair object tiles on a runtime Tilemap.");
            Assert.IsTrue(ContainsTile(furnitureTilemap, ChairTileName, out _), "Selected chair furniture tile should be visible in the House furniture Tilemap.");
        }

        [UnityTest]
        public IEnumerator SelectedBasicDeskFurniture_PlacesOneByOneDeskWithoutComputerTile()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;

            Assert.IsNotNull(GameObject.Find(BasicDeskButtonName), "The regular player-facing desk button must select desk.basic, not desk.large.");
            GameObject.Find(BasicDeskButtonName).GetComponent<Button>().onClick.Invoke();
            yield return null;

            var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>();
            Assert.IsNotNull(overlay);
            Assert.IsTrue(overlay.TryManualPlaceFirstValidForTests(out var message));
            yield return null;

            StringAssert.Contains(BasicDeskFurnitureId, message);
            var furnitureTilemap = GameObject.Find("HouseFurnitureObjectTilemap")?.GetComponent<Tilemap>();
            var occupancyTilemap = GameObject.Find("HouseFurnitureOccupancyTilemap")?.GetComponent<Tilemap>();
            Assert.IsNotNull(furnitureTilemap);
            Assert.IsNotNull(occupancyTilemap);
            Assert.AreEqual(1, CountTiles(furnitureTilemap, DeskTileName), "Regular desk placement must render exactly one desk tile.");
            Assert.AreEqual(0, CountTiles(furnitureTilemap, ComputerTileName), "Regular desk placement must not attach a computer tile.");
            Assert.IsTrue(ContainsTile(furnitureTilemap, DeskTileName, out var deskCell));
            Assert.IsNotNull(occupancyTilemap.GetTile(deskCell));
        }

        [UnityTest]
        public IEnumerator SelectedDeskFurniture_PlacesMultiTileObjectThroughPalette()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;

            Assert.IsNotNull(GameObject.Find(DeskButtonName), "House placement UI should expose multi-tile desk furniture from the shared registry catalog.");
            GameObject.Find(DeskButtonName).GetComponent<Button>().onClick.Invoke();
            yield return null;

            var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>();
            Assert.IsNotNull(overlay);
            Assert.IsTrue(overlay.TryManualPlaceFirstValidForTests(out var message));
            yield return null;

            StringAssert.Contains(DeskFurnitureId, message);
            var furnitureTilemap = GameObject.Find("HouseFurnitureObjectTilemap")?.GetComponent<Tilemap>();
            var occupancyTilemap = GameObject.Find("HouseFurnitureOccupancyTilemap")?.GetComponent<Tilemap>();
            Assert.IsNotNull(furnitureTilemap);
            Assert.IsNotNull(occupancyTilemap);
            Assert.IsTrue(ContainsTile(furnitureTilemap, DeskTileName, out var deskCell));
            Assert.IsTrue(ContainsTile(furnitureTilemap, ComputerTileName, out var computerCell));
            Assert.AreEqual(Vector3Int.right, computerCell - deskCell, "The default registry-defined desk object should place its second tile at the local +X offset.");
            Assert.IsNotNull(occupancyTilemap.GetTile(deskCell));
            Assert.IsNotNull(occupancyTilemap.GetTile(computerCell));
        }

        [UnityTest]
        public IEnumerator RotateFurnitureButton_PlacesDeskWithRotatedFootprintThroughPalette()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;

            GameObject.Find(DeskButtonName).GetComponent<Button>().onClick.Invoke();
            yield return null;

            var rotateButton = GameObject.Find("RotateFurnitureButton")?.GetComponent<Button>();
            Assert.IsNotNull(rotateButton, "House placement UI should expose a player-facing furniture rotation button.");
            rotateButton.onClick.Invoke();
            yield return null;

            var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>();
            Assert.IsNotNull(overlay);
            Assert.IsTrue(overlay.TryManualPlaceFirstValidForTests(out var message));
            yield return null;

            StringAssert.Contains(DeskFurnitureId, message);
            var furnitureTilemap = GameObject.Find("HouseFurnitureObjectTilemap")?.GetComponent<Tilemap>();
            var occupancyTilemap = GameObject.Find("HouseFurnitureOccupancyTilemap")?.GetComponent<Tilemap>();
            Assert.IsTrue(ContainsTile(furnitureTilemap, DeskTileName, out var deskCell));
            Assert.IsTrue(ContainsTile(furnitureTilemap, ComputerTileName, out var computerCell));
            Assert.AreEqual(Vector3Int.down, computerCell - deskCell, "Rotating once should place the desk's second tile on the East-direction rotated footprint.");
            Assert.IsNotNull(occupancyTilemap.GetTile(computerCell));
            Assert.IsNull(furnitureTilemap.GetTile(deskCell + Vector3Int.right), "Rotated placement must not leave an unrotated desk part at +X.");
        }

        [UnityTest]
        public IEnumerator MoveSelectedFurnitureButton_MovesPlacedFurnitureAndClearsOriginalCells()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;

            GameObject.Find(DeskButtonName).GetComponent<Button>().onClick.Invoke();
            yield return null;

            var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>();
            Assert.IsTrue(overlay.TryManualPlaceFirstValidForTests(out _));
            yield return null;

            var furnitureTilemap = GameObject.Find("HouseFurnitureObjectTilemap")?.GetComponent<Tilemap>();
            var occupancyTilemap = GameObject.Find("HouseFurnitureOccupancyTilemap")?.GetComponent<Tilemap>();
            Assert.IsTrue(ContainsTile(furnitureTilemap, DeskTileName, out var originalDeskCell));
            Assert.IsTrue(ContainsTile(furnitureTilemap, ComputerTileName, out var originalComputerCell));

            var moveButton = GameObject.Find("MoveSelectedFurnitureButton")?.GetComponent<Button>();
            Assert.IsNotNull(moveButton, "House placement UI should expose a player-facing move button for the selected furniture instance.");
            moveButton.onClick.Invoke();
            yield return null;

            StringAssert.Contains("Moved", overlay.LastMessage, "Move button should invoke the overlay move path before tile assertions.");
            Assert.IsNull(furnitureTilemap.GetTile(originalDeskCell));
            Assert.IsNull(furnitureTilemap.GetTile(originalComputerCell));
            Assert.IsNull(occupancyTilemap.GetTile(originalDeskCell));
            Assert.IsNull(occupancyTilemap.GetTile(originalComputerCell));
            Assert.IsTrue(ContainsTile(furnitureTilemap, DeskTileName, out var movedDeskCell));
            Assert.IsTrue(ContainsTile(furnitureTilemap, ComputerTileName, out var movedComputerCell));
            Assert.AreNotEqual(originalDeskCell, movedDeskCell);
            Assert.AreEqual(Vector3Int.right, movedComputerCell - movedDeskCell);
            Assert.IsNotNull(occupancyTilemap.GetTile(movedDeskCell));
            Assert.IsNotNull(occupancyTilemap.GetTile(movedComputerCell));
        }

        [UnityTest]
        public IEnumerator SaveLoadFurnitureButtons_RestorePlacedFurnitureWithoutDuplicateTiles()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;

            GameObject.Find(ChairButtonName).GetComponent<Button>().onClick.Invoke();
            yield return null;
            var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>();
            Assert.IsTrue(overlay.TryManualPlaceFirstValidForTests(out _));
            yield return null;

            GameObject.Find(DeskButtonName).GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.IsTrue(overlay.TryManualPlaceFirstValidForTests(out _));
            yield return null;

            var furnitureTilemap = GameObject.Find("HouseFurnitureObjectTilemap")?.GetComponent<Tilemap>();
            var occupancyTilemap = GameObject.Find("HouseFurnitureOccupancyTilemap")?.GetComponent<Tilemap>();
            Assert.AreEqual(1, CountTiles(furnitureTilemap, ChairTileName));
            Assert.AreEqual(1, CountTiles(furnitureTilemap, DeskTileName));
            Assert.AreEqual(1, CountTiles(furnitureTilemap, ComputerTileName));
            var occupiedBefore = CountOccupied(occupancyTilemap);
            Assert.GreaterOrEqual(occupiedBefore, 3);

            var saveButton = GameObject.Find("SaveFurnitureLayoutButton")?.GetComponent<Button>();
            var clearButton = GameObject.Find("ClearFurnitureLayoutButton")?.GetComponent<Button>();
            var loadButton = GameObject.Find("LoadFurnitureLayoutButton")?.GetComponent<Button>();
            Assert.IsNotNull(saveButton, "House placement UI should expose a player-facing save button.");
            Assert.IsNotNull(clearButton, "House placement UI should expose a clear button so load restoration can be verified without stale tiles.");
            Assert.IsNotNull(loadButton, "House placement UI should expose a player-facing load button.");

            saveButton.onClick.Invoke();
            yield return null;
            StringAssert.Contains("Saved", overlay.LastMessage);

            clearButton.onClick.Invoke();
            yield return null;
            Assert.AreEqual(0, CountTiles(furnitureTilemap, ChairTileName));
            Assert.AreEqual(0, CountTiles(furnitureTilemap, DeskTileName));
            Assert.AreEqual(0, CountTiles(furnitureTilemap, ComputerTileName));
            Assert.AreEqual(0, CountOccupied(occupancyTilemap));

            loadButton.onClick.Invoke();
            yield return null;
            StringAssert.Contains("Loaded", overlay.LastMessage);
            Assert.AreEqual(1, CountTiles(furnitureTilemap, ChairTileName));
            Assert.AreEqual(1, CountTiles(furnitureTilemap, DeskTileName));
            Assert.AreEqual(1, CountTiles(furnitureTilemap, ComputerTileName));
            Assert.AreEqual(occupiedBefore, CountOccupied(occupancyTilemap));

            loadButton.onClick.Invoke();
            yield return null;
            Assert.AreEqual(1, CountTiles(furnitureTilemap, ChairTileName), "Repeated load must not duplicate chair tiles.");
            Assert.AreEqual(1, CountTiles(furnitureTilemap, DeskTileName), "Repeated load must not duplicate desk tiles.");
            Assert.AreEqual(1, CountTiles(furnitureTilemap, ComputerTileName), "Repeated load must not duplicate computer tiles.");
            Assert.AreEqual(occupiedBefore, CountOccupied(occupancyTilemap), "Repeated load must not duplicate occupancy cells.");
        }

        [UnityTest]
        public IEnumerator SelectedChairFurniture_ClearsAvailabilityOverlayAfterPlacement()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;

            GameObject.Find(ChairButtonName).GetComponent<Button>().onClick.Invoke();
            yield return null;

            var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>();
            Assert.Greater(overlay.ValidCellCount, 0);
            Assert.IsTrue(overlay.TryManualPlaceFirstValidForTests(out _));
            yield return null;

            Assert.AreEqual(0, overlay.ValidCellCount, "Availability overlay should clear after placement so it does not cover the placed furniture sprite.");
            Assert.AreEqual(0, overlay.InvalidCellCount, "Availability overlay should clear after placement so it does not cover the placed furniture sprite.");
        }

        [UnityTest]
        public IEnumerator SelectedChairFurniture_DoesNotLeaveDefaultChairTileUnderFurniture()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;

            GameObject.Find(ChairButtonName).GetComponent<Button>().onClick.Invoke();
            yield return null;

            var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>();
            Assert.IsTrue(overlay.TryManualPlaceFirstValidForTests(out _));
            yield return null;

            var furnitureTilemap = GameObject.Find("HouseFurnitureObjectTilemap")?.GetComponent<Tilemap>();
            var decorationTilemap = GameObject.Find("HouseDecorationTilemap")?.GetComponent<Tilemap>();
            Assert.IsTrue(ContainsTile(furnitureTilemap, ChairTileName, out var furnitureCell));
            Assert.IsNotNull(decorationTilemap);
            Assert.IsNull(decorationTilemap.GetTile(furnitureCell), "Furniture object placement should clear the default generated Chair tile underneath so the selected chair sprite is not visually mixed with another chair.");
        }

        [UnityTest]
        public IEnumerator DeleteSelectedFurnitureButton_ClearsPlacedFurnitureAndOccupancy()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;

            GameObject.Find(ChairButtonName).GetComponent<Button>().onClick.Invoke();
            yield return null;

            var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>();
            Assert.IsTrue(overlay.TryManualPlaceFirstValidForTests(out _));
            yield return null;

            var furnitureTilemap = GameObject.Find("HouseFurnitureObjectTilemap")?.GetComponent<Tilemap>();
            var occupancyTilemap = GameObject.Find("HouseFurnitureOccupancyTilemap")?.GetComponent<Tilemap>();
            Assert.IsTrue(ContainsTile(furnitureTilemap, ChairTileName, out var furnitureCell));
            Assert.IsNotNull(occupancyTilemap, "Furniture placement should write shared occupancy state, not only visual object tiles.");
            Assert.IsNotNull(occupancyTilemap.GetTile(furnitureCell));

            GameObject.Find("DeleteSelectedFurnitureButton").GetComponent<Button>().onClick.Invoke();
            yield return null;

            Assert.IsNull(furnitureTilemap.GetTile(furnitureCell));
            Assert.IsNull(occupancyTilemap.GetTile(furnitureCell));
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

        private static int CountOccupied(Tilemap tilemap)
        {
            int count = 0;
            foreach (var position in tilemap.cellBounds.allPositionsWithin)
            {
                if (tilemap.GetTile(position) != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool ContainsTile(Tilemap tilemap, string tileName, out Vector3Int foundCell)
        {
            foreach (var position in tilemap.cellBounds.allPositionsWithin)
            {
                var tile = tilemap.GetTile(position);
                if (tile != null && tile.name == tileName)
                {
                    foundCell = position;
                    return true;
                }
            }

            foundCell = default;
            return false;
        }
    }
}
