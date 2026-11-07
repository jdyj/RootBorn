using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Save;
using Rootborn.UI.Interiors;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.PlayMode.Interiors
{
    public sealed class HouseInteriorFurniturePlacementPlayModeTests
    {
        private const string ChairFurnitureId = "chair.black";
        private const string ChairTileName = "BlackChair_00";
        private const string BasicDeskFurnitureId = "desk.basic";
        private const string DeskFurnitureId = "desk.large";
        private const string DeskTileName = "ShadowlessOffice_Desk_Table_r39_c01";
        private const string ComputerTileName = "ShadowlessOffice_Computer_Monitor_r44_c08";

        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _saveRoot = System.IO.Path.Combine(Application.temporaryCachePath, "rootborn-house-placement-tests-" + Guid.NewGuid().ToString("N"));
            SaveService.SetRootDirectoryForTests(_saveRoot);
            ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = "house-placement-tests", DisplayName = "house-placement-tests", WorldSeed = 1205, TileSeed = 1205 });
            ClearStaticFurnitureLayoutCache();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            ActiveSaveContext.Clear();
            SaveService.SetRootDirectoryForTests(null);
            ClearStaticFurnitureLayoutCache();
            yield return null;
        }

        [UnityTest]
        public IEnumerator HousePlacementUi_LoadsChairFurnitureObjectButtons()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return HousePlacementTestDriver.OpenPlacementMode();

            Assert.IsNotNull(HousePlacementTestDriver.ShowFurniture(ChairFurnitureId), "House placement UI should expose registry-backed black chair furniture objects in-game.");
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
            yield return HousePlacementTestDriver.OpenPlacementMode();

            HousePlacementTestDriver.ShowFurniture(ChairFurnitureId).GetComponent<Button>().onClick.Invoke();
            yield return null;

            var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>();
            Assert.IsNotNull(overlay);
            Assert.IsTrue(overlay.TryManualPlaceFirstValidForTests(out var message));
            yield return null;

            StringAssert.Contains(ChairFurnitureId, message);
            var furnitureTilemap = GameObject.Find("AuthoredUserFurniture")?.GetComponent<Tilemap>();
            Assert.IsNotNull(furnitureTilemap, "Furniture placement should render selected chair object tiles on a runtime Tilemap.");
            Assert.IsTrue(ContainsTile(furnitureTilemap, ChairTileName, out _), "Selected chair furniture tile should be visible in the House furniture Tilemap.");
        }

        [UnityTest]
        public IEnumerator SelectedBasicDeskFurniture_PlacesOneByOneDeskWithoutComputerTile()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return HousePlacementTestDriver.OpenPlacementMode();

            Assert.IsNotNull(HousePlacementTestDriver.ShowFurniture(BasicDeskFurnitureId), "The regular player-facing desk button must select desk.basic, not desk.large.");
            HousePlacementTestDriver.ShowFurniture(BasicDeskFurnitureId).GetComponent<Button>().onClick.Invoke();
            yield return null;

            var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>();
            Assert.IsNotNull(overlay);
            Assert.IsTrue(overlay.TryManualPlaceFirstValidForTests(out var message));
            yield return null;

            StringAssert.Contains(BasicDeskFurnitureId, message);
            var furnitureTilemap = GameObject.Find("AuthoredUserFurniture")?.GetComponent<Tilemap>();
            var occupancyTilemap = GameObject.Find("AuthoredUserOccupancy")?.GetComponent<Tilemap>();
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
            yield return HousePlacementTestDriver.OpenPlacementMode();

            Assert.IsNotNull(HousePlacementTestDriver.ShowFurniture(DeskFurnitureId), "House placement UI should expose multi-tile desk furniture from the shared registry catalog.");
            HousePlacementTestDriver.ShowFurniture(DeskFurnitureId).GetComponent<Button>().onClick.Invoke();
            yield return null;

            var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>();
            Assert.IsNotNull(overlay);
            Assert.IsTrue(overlay.TryManualPlaceFirstValidForTests(out var message));
            yield return null;

            StringAssert.Contains(DeskFurnitureId, message);
            var furnitureTilemap = GameObject.Find("AuthoredUserFurniture")?.GetComponent<Tilemap>();
            var occupancyTilemap = GameObject.Find("AuthoredUserOccupancy")?.GetComponent<Tilemap>();
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
            yield return HousePlacementTestDriver.OpenPlacementMode();

            HousePlacementTestDriver.ShowFurniture(DeskFurnitureId).GetComponent<Button>().onClick.Invoke();
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
            var furnitureTilemap = GameObject.Find("AuthoredUserFurniture")?.GetComponent<Tilemap>();
            var occupancyTilemap = GameObject.Find("AuthoredUserOccupancy")?.GetComponent<Tilemap>();
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
            yield return HousePlacementTestDriver.OpenPlacementMode();

            HousePlacementTestDriver.ShowFurniture(DeskFurnitureId).GetComponent<Button>().onClick.Invoke();
            yield return null;

            var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>();
            Assert.IsTrue(overlay.TryManualPlaceFirstValidForTests(out _));
            yield return null;

            var furnitureTilemap = GameObject.Find("AuthoredUserFurniture")?.GetComponent<Tilemap>();
            var occupancyTilemap = GameObject.Find("AuthoredUserOccupancy")?.GetComponent<Tilemap>();
            Assert.IsTrue(ContainsTile(furnitureTilemap, DeskTileName, out var originalDeskCell));
            Assert.IsTrue(ContainsTile(furnitureTilemap, ComputerTileName, out var originalComputerCell));

            var moveButton = GameObject.Find("MoveSelectedFurnitureButton")?.GetComponent<Button>();
            Assert.IsNotNull(moveButton, "House placement UI should expose a player-facing move button for the selected furniture instance.");
            moveButton.onClick.Invoke();
            yield return null;

            // Move picks the piece up: nothing moves until the player clicks where it should go.
            Assert.IsNotNull(furnitureTilemap.GetTile(originalDeskCell), "Pressing Move must not send the furniture to some other cell on its own.");
            Assert.Greater(overlay.ValidCellCount, 0, "While carrying, the cells the piece can go to must be shown.");
            Assert.IsTrue(overlay.TryManualPlaceFirstValidForTests(out _), "Clicking a free cell must put the carried piece there.");
            yield return null;

            StringAssert.Contains("Moved", overlay.LastMessage, "Move button should invoke the overlay move path before tile assertions.");
            Assert.AreEqual(1, CountTiles(furnitureTilemap, DeskTileName), "Moving must not leave a second desk behind.");
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

            // The moved piece stays selected, so Delete right after a move removes it.
            GameObject.Find("DeleteSelectedFurnitureButton")?.GetComponent<Button>()?.onClick.Invoke();
            yield return null;
            Assert.AreEqual(0, CountTiles(furnitureTilemap, DeskTileName), "Delete right after a move must remove the moved furniture.");
        }

        [UnityTest]
        public IEnumerator SelectedChairFurniture_ClearsAvailabilityOverlayAfterPlacement()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return HousePlacementTestDriver.OpenPlacementMode();

            HousePlacementTestDriver.ShowFurniture(ChairFurnitureId).GetComponent<Button>().onClick.Invoke();
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
            yield return HousePlacementTestDriver.OpenPlacementMode();

            HousePlacementTestDriver.ShowFurniture(ChairFurnitureId).GetComponent<Button>().onClick.Invoke();
            yield return null;

            var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>();
            Assert.IsTrue(overlay.TryManualPlaceFirstValidForTests(out _));
            yield return null;

            var furnitureTilemap = GameObject.Find("AuthoredUserFurniture")?.GetComponent<Tilemap>();
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
            yield return HousePlacementTestDriver.OpenPlacementMode();

            HousePlacementTestDriver.ShowFurniture(ChairFurnitureId).GetComponent<Button>().onClick.Invoke();
            yield return null;

            var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>();
            Assert.IsTrue(overlay.TryManualPlaceFirstValidForTests(out _));
            yield return null;

            var furnitureTilemap = GameObject.Find("AuthoredUserFurniture")?.GetComponent<Tilemap>();
            var occupancyTilemap = GameObject.Find("AuthoredUserOccupancy")?.GetComponent<Tilemap>();
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

        private static void ClearStaticFurnitureLayoutCache()
        {
            var field = typeof(InteriorPlacementPreviewOverlay).GetField("SavedFurnitureLayout", BindingFlags.Static | BindingFlags.NonPublic);
            var list = field != null ? field.GetValue(null) as IList : null;
            list?.Clear();
        }
    }
}
