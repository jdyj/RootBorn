using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.UI.Interiors;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.Interiors
{
    public sealed class FurniturePlacementMouseFlowPlayModeTests
    {
        private const string ChairButtonName = "PaletteFurniture_chair.black";
        private const string ChairTileName = "BlackChair_00";

        [UnityTest]
        public IEnumerator PaletteButtonMouseClick_SelectsFurnitureAndDoesNotPlaceWorldTileBehindUi()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;

            Assert.IsNotNull(EventSystem.current, "The player-facing furniture UI must install an EventSystem so mouse clicks are handled by UI before world placement.");
            Assert.IsNull(GameObject.Find("Palette_Desk"), "The old generic Desk palette path must not remain because it places desk/chair/computer clusters instead of the clicked furniture item.");

            var button = GameObject.Find(ChairButtonName)?.GetComponent<Button>();
            Assert.IsNotNull(button, "The chair should be selectable from the player-facing furniture palette.");
            var buttonCenter = RectTransformUtility.WorldToScreenPoint(null, button.GetComponent<RectTransform>().position);
            AssertRaycastHits(buttonCenter, ChairButtonName);

            var furnitureTilemap = GameObject.Find("HouseFurnitureObjectTilemap")?.GetComponent<Tilemap>();
            int chairCountBefore = furnitureTilemap != null ? CountTiles(furnitureTilemap, ChairTileName) : 0;

            var mouse = InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
            var driver = new GameObject("FurnitureUiMouseClickDriver").AddComponent<MouseClickFrameDriver>();
            driver.Configure(mouse, buttonCenter);
            yield return null;
            yield return null;
            InputSystem.RemoveDevice(mouse);

            var status = GameObject.Find("InteriorPlacementStatusText")?.GetComponent<Text>();
            Assert.IsNotNull(status);
            StringAssert.Contains("Black Chair", status.text, "A real mouse click on the chair button should select the chair through the UI event path.");

            var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>();
            Assert.IsNotNull(overlay);
            Assert.Greater(overlay.ValidCellCount, 0, "Selecting the chair should show valid placement cells, but must not place immediately on the tile behind the UI.");
            furnitureTilemap = GameObject.Find("HouseFurnitureObjectTilemap")?.GetComponent<Tilemap>();
            int chairCountAfter = furnitureTilemap != null ? CountTiles(furnitureTilemap, ChairTileName) : 0;
            Assert.AreEqual(chairCountBefore, chairCountAfter, "Clicking the UI button must not leak into the world and place furniture behind the UI.");
        }

        [UnityTest]
        public IEnumerator PaletteSelectionAndWorldMouseClick_PlacesFurnitureThroughOverlayUpdate()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var button = GameObject.Find(ChairButtonName);
            Assert.IsNotNull(button, "The player-facing furniture palette should contain the chair button.");

            var pointerData = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                pointerId = -1
            };
            ExecuteEvents.Execute<IPointerClickHandler>(button, pointerData, ExecuteEvents.pointerClickHandler);
            yield return null;

            var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>();
            Assert.IsNotNull(overlay);
            Assert.Greater(overlay.ValidCellCount, 0, "Selecting furniture should draw player-visible valid placement cells before clicking the world.");

            var overlayTilemap = GameObject.Find("HousePlacementAvailabilityTilemap")?.GetComponent<Tilemap>();
            Assert.IsNotNull(overlayTilemap);
            var camera = Camera.main;
            Assert.IsNotNull(camera);
            Assert.IsTrue(TryFindVisibleValidOverlayCell(overlayTilemap, camera, out var clickCell, out var screenPosition), "A visible valid placement overlay cell outside UI should be available for the mouse click.");

            var mouse = InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
            var driver = new GameObject("FurnitureWorldMouseClickDriver").AddComponent<MouseClickFrameDriver>();
            driver.Configure(mouse, screenPosition);
            yield return null;
            yield return null;
            InputSystem.RemoveDevice(mouse);

            var furnitureTilemap = GameObject.Find("HouseFurnitureObjectTilemap")?.GetComponent<Tilemap>();
            var occupancyTilemap = GameObject.Find("HouseFurnitureOccupancyTilemap")?.GetComponent<Tilemap>();
            Assert.IsNotNull(furnitureTilemap);
            Assert.IsNotNull(occupancyTilemap);
            Assert.AreEqual(0, overlay.ValidCellCount, "A successful world click placement should clear the availability overlay. Last message: " + overlay.LastMessage);
            Assert.IsTrue(ContainsTile(furnitureTilemap, ChairTileName, out var furnitureCell), "The selected chair should be placed by the overlay Update mouse path. Last message: " + overlay.LastMessage);
            Assert.IsNotNull(occupancyTilemap.GetTile(furnitureCell), "Mouse placement should write occupancy for the placed furniture cell.");
            StringAssert.Contains("chair.black", overlay.LastMessage);
        }

        [UnityTest]
        public IEnumerator BlackChairMousePlacementTwice_DoesNotRevealStaleOverlayOrSampleTilesBehindFirstChair()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;

            Assert.IsNull(GameObject.Find("ModernOfficeChairSampleTilemap"), "Debug/sample chair tilemap must not be visible in playable House.");

            var button = GameObject.Find(ChairButtonName);
            Assert.IsNotNull(button);
            ExecuteEvents.Execute<IPointerClickHandler>(button, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
            yield return null;

            var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>();
            Assert.IsNotNull(overlay);
            var overlayTilemap = GameObject.Find("HousePlacementAvailabilityTilemap")?.GetComponent<Tilemap>();
            var camera = Camera.main;
            Assert.IsNotNull(overlayTilemap);
            Assert.IsNotNull(camera);
            Assert.IsTrue(TryFindVisibleValidOverlayCell(overlayTilemap, camera, out var firstCell, out var firstScreen));

            var mouse = InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
            var firstDriver = new GameObject("FurnitureFirstChairMouseClickDriver").AddComponent<MouseClickFrameDriver>();
            firstDriver.Configure(mouse, firstScreen);
            yield return null;
            yield return null;

            var furnitureTilemap = GameObject.Find("HouseFurnitureObjectTilemap")?.GetComponent<Tilemap>();
            Assert.IsNotNull(furnitureTilemap);
            Assert.IsTrue(ContainsTile(furnitureTilemap, ChairTileName, out var firstFurnitureCell));
            Assert.AreEqual(firstCell, firstFurnitureCell);
            Assert.AreEqual(0, overlay.ValidCellCount);
            Assert.AreEqual(0, overlay.InvalidCellCount);

            ExecuteEvents.Execute<IPointerClickHandler>(button, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
            yield return null;
            Assert.IsTrue(TryFindVisibleValidOverlayCell(overlayTilemap, camera, out var secondCell, out var secondScreen));
            Assert.AreNotEqual(firstCell, secondCell, "Second placement should use a different visible valid cell for this regression.");

            var secondDriver = new GameObject("FurnitureSecondChairMouseClickDriver").AddComponent<MouseClickFrameDriver>();
            secondDriver.Configure(mouse, secondScreen);
            yield return null;
            yield return null;
            InputSystem.RemoveDevice(mouse);

            Assert.AreEqual(2, CountTiles(furnitureTilemap, ChairTileName), "Two chair placements should render exactly two selected blackChair tiles.");
            Assert.AreEqual(0, overlay.ValidCellCount, "Overlay must be clear after second placement.");
            Assert.AreEqual(0, overlay.InvalidCellCount, "Overlay must be clear after second placement.");

            var decorationTilemap = GameObject.Find("HouseDecorationTilemap")?.GetComponent<Tilemap>();
            Assert.IsNotNull(decorationTilemap);
            Assert.IsNull(decorationTilemap.GetTile(firstFurnitureCell), "First chair cell must not reveal a default/generated chair behind the selected chair after a second placement.");
        }

        private static void AssertRaycastHits(Vector2 screenPosition, string expectedName)
        {
            var eventData = new PointerEventData(EventSystem.current)
            {
                position = screenPosition
            };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            for (int i = 0; i < results.Count; i++)
            {
                if (results[i].gameObject != null && results[i].gameObject.name == expectedName)
                {
                    return;
                }
            }

            Assert.Fail("UI raycast at " + screenPosition + " did not hit " + expectedName + ". Hits=" + string.Join(",", results.ConvertAll(result => result.gameObject != null ? result.gameObject.name : "<null>")));
        }

        private static bool TryFindVisibleValidOverlayCell(Tilemap tilemap, Camera camera, out Vector3Int foundCell, out Vector2 screenPosition)
        {
            foreach (var position in tilemap.cellBounds.allPositionsWithin)
            {
                var tile = tilemap.GetTile<Tile>(position);
                if (tile == null || tile.color.g <= 0.8f || tile.color.r >= 0.3f)
                {
                    continue;
                }

                var screen = camera.WorldToScreenPoint(tilemap.GetCellCenterWorld(position));
                if (screen.z > 0f && screen.x >= Screen.width * 0.2f && screen.y >= Screen.height * 0.35f && screen.x <= Screen.width * 0.8f && screen.y <= Screen.height * 0.85f && !IsPointerOverUi(screen))
                {
                    foundCell = position;
                    screenPosition = new Vector2(screen.x, screen.y);
                    return true;
                }
            }

            foundCell = default;
            screenPosition = default;
            return false;
        }

        private static bool IsPointerOverUi(Vector3 screen)
        {
            if (EventSystem.current == null)
            {
                return false;
            }

            var eventData = new PointerEventData(EventSystem.current)
            {
                position = new Vector2(screen.x, screen.y)
            };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            return results.Count > 0;
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

        [DefaultExecutionOrder(-10000)]
        private sealed class MouseClickFrameDriver : MonoBehaviour
        {
            private Mouse _mouse;
            private Vector2 _screenPosition;
            private int _frame;

            public void Configure(Mouse mouse, Vector2 screenPosition)
            {
                _mouse = mouse;
                _screenPosition = screenPosition;
            }

            private void Update()
            {
                if (_mouse == null)
                {
                    Destroy(gameObject);
                    return;
                }

                _mouse.MakeCurrent();
                if (_frame == 0)
                {
                    InputSystem.QueueStateEvent(_mouse, new MouseState { position = _screenPosition, buttons = 1 });
                    InputSystem.Update();

                    var router = Object.FindFirstObjectByType<FurniturePlacementPointerRouter>();
                    if (router != null)
                    {
                        typeof(FurniturePlacementPointerRouter).GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(router, null);
                    }

                    var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>();
                    if (overlay != null)
                    {
                        var overlayType = typeof(InteriorPlacementPreviewOverlay);
                        overlayType.GetField("_wasMousePressed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(overlay, false);
                        overlayType.GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(overlay, null);
                    }

                    _frame++;
                    return;
                }

                InputSystem.QueueStateEvent(_mouse, new MouseState { position = _screenPosition });
                InputSystem.Update();
                Destroy(gameObject);
            }
        }
    }
}
