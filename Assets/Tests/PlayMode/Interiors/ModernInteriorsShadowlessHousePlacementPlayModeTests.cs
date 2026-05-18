using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Rootborn.Game.Interiors;
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
    public sealed class ModernInteriorsShadowlessHousePlacementPlayModeTests
    {
        [UnityTest]
        public IEnumerator HousePalette_ShowsModernInteriorsShadowlessFurnitureFromRegistry()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var panel = Object.FindFirstObjectByType<InteriorFurniturePlacementPanel>();
            Assert.IsNotNull(panel, "House should create the player-facing furniture placement panel.");

            var button = FindModernShadowlessPaletteButton();
            Assert.IsNotNull(button, "House furniture palette should include at least one generated Modern Interiors shadowless furniture button.");
            Assert.IsNotNull(button.GetComponent<Button>(), "The generated Modern Interiors palette item should be clickable through the UI Button path.");
            Assert.LessOrEqual(CountPaletteFurnitureButtons(), 90, "The player-facing palette should not instantiate every generated Modern Interiors asset at once.");
        }

        [UnityTest]
        public IEnumerator ModernShadowlessFurnitureSelectionAndMouseHover_ShowsGhostPreviewBeforePlacement()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var button = FindModernShadowlessPaletteButton();
            Assert.IsNotNull(button, "A generated Modern Interiors shadowless furniture button is required for ghost preview verification.");
            ExecuteEvents.Execute<IPointerClickHandler>(button, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
            yield return null;

            var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>();
            var overlayTilemap = GameObject.Find("HousePlacementAvailabilityTilemap")?.GetComponent<Tilemap>();
            var camera = Camera.main;
            Assert.IsNotNull(overlay);
            Assert.IsNotNull(overlayTilemap);
            Assert.IsNotNull(camera);
            Assert.IsTrue(TryFindVisibleValidOverlayCell(overlayTilemap, camera, out var hoverCell, out var screenPosition), "A visible valid placement cell outside UI should be available.");

            var mouse = InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
            var driver = new GameObject("ModernShadowlessFurnitureGhostPreviewMouseMoveDriver").AddComponent<MouseMoveFrameDriver>();
            driver.Configure(mouse, screenPosition);
            yield return null;
            yield return null;
            InputSystem.RemoveDevice(mouse);

            var ghostTilemap = GameObject.Find("HouseFurnitureGhostPreviewTilemap")?.GetComponent<Tilemap>();
            Assert.IsNotNull(ghostTilemap, "Hovering with a selected furniture item should create the ghost preview Tilemap.");
            Assert.AreEqual(1, CountModernShadowlessTiles(ghostTilemap, out var ghostCell, out var ghostTileName), "Ghost preview should render the selected Modern Interiors furniture tile before placement.");
            Assert.AreEqual(hoverCell, ghostCell, "Ghost preview should use the hovered cell as the lower-left anchor cell.");
            StringAssert.Contains("_Singles_Shadowless_48x48_", ghostTileName);
        }

        [UnityTest]
        public IEnumerator ModernShadowlessFurnitureSelectionAndMouseHover_ShowsFootprintCellsBeforePlacement()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var button = FindModernShadowlessPaletteButton();
            Assert.IsNotNull(button, "A generated Modern Interiors shadowless furniture button is required for footprint preview verification.");
            ExecuteEvents.Execute<IPointerClickHandler>(button, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
            yield return null;

            var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>();
            var overlayTilemap = GameObject.Find("HousePlacementAvailabilityTilemap")?.GetComponent<Tilemap>();
            var camera = Camera.main;
            Assert.IsNotNull(overlay);
            Assert.IsNotNull(overlayTilemap);
            Assert.IsNotNull(camera);
            Assert.IsTrue(TryFindVisibleValidOverlayCell(overlayTilemap, camera, out var hoverCell, out var screenPosition), "A visible valid placement cell outside UI should be available.");
            var expectedFootprintCount = GetActiveFurniture(overlay).PlacementDefinition.FootprintCells.Count;
            Assert.Greater(expectedFootprintCount, 1, "The selected Modern Interiors fixture should exercise multi-cell footprint preview.");

            var mouse = InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
            var driver = new GameObject("ModernShadowlessFurnitureFootprintPreviewMouseMoveDriver").AddComponent<MouseMoveFrameDriver>();
            driver.Configure(mouse, screenPosition);
            yield return null;
            yield return null;
            InputSystem.RemoveDevice(mouse);

            var footprintTilemap = GameObject.Find("HouseFurnitureFootprintPreviewTilemap")?.GetComponent<Tilemap>();
            Assert.IsNotNull(footprintTilemap, "Hovering with a selected furniture item should create the footprint preview Tilemap.");
            Assert.AreEqual(expectedFootprintCount, CountTiles(footprintTilemap), "Footprint preview should color every occupied cell for the selected furniture.");
            Assert.IsNotNull(footprintTilemap.GetTile(hoverCell), "The hovered lower-left anchor cell should be part of the colored footprint preview.");
        }

        [UnityTest]
        public IEnumerator ModernShadowlessFurnitureSelectionAndWorldMouseClick_PlacesGeneratedTile()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;

            Assert.IsNull(GameObject.Find("ModernOfficeChairSampleTilemap"), "Debug/sample chair tilemap must not be visible in playable House.");

            var button = FindModernShadowlessPaletteButton();
            Assert.IsNotNull(button, "A generated Modern Interiors shadowless furniture button is required for player-facing placement.");
            ExecuteEvents.Execute<IPointerClickHandler>(button, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
            yield return null;

            var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>();
            Assert.IsNotNull(overlay);
            Assert.Greater(overlay.ValidCellCount, 0, "Selecting Modern Interiors furniture should draw visible valid placement cells.");

            var overlayTilemap = GameObject.Find("HousePlacementAvailabilityTilemap")?.GetComponent<Tilemap>();
            var camera = Camera.main;
            Assert.IsNotNull(overlayTilemap);
            Assert.IsNotNull(camera);
            Assert.IsTrue(TryFindVisibleValidOverlayCell(overlayTilemap, camera, out var clickCell, out var screenPosition), "A visible valid placement cell outside UI should be available.");

            var mouse = InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
            var driver = new GameObject("ModernShadowlessFurnitureWorldMouseClickDriver").AddComponent<MouseClickFrameDriver>();
            driver.Configure(mouse, screenPosition);
            yield return null;
            yield return null;
            InputSystem.RemoveDevice(mouse);

            var furnitureTilemap = GameObject.Find("HouseFurnitureObjectTilemap")?.GetComponent<Tilemap>();
            var occupancyTilemap = GameObject.Find("HouseFurnitureOccupancyTilemap")?.GetComponent<Tilemap>();
            var ghostTilemap = GameObject.Find("HouseFurnitureGhostPreviewTilemap")?.GetComponent<Tilemap>();
            var footprintTilemap = GameObject.Find("HouseFurnitureFootprintPreviewTilemap")?.GetComponent<Tilemap>();
            Assert.IsNotNull(furnitureTilemap);
            Assert.IsNotNull(occupancyTilemap);
            Assert.AreEqual(0, overlay.ValidCellCount, "A successful world click placement should clear the availability overlay. Last message: " + overlay.LastMessage);
            Assert.AreEqual(0, overlay.InvalidCellCount, "A successful world click placement should clear invalid overlay cells. Last message: " + overlay.LastMessage);
            Assert.IsTrue(ContainsModernShadowlessTile(furnitureTilemap, out var placedCell, out var tileName), "The generated Modern Interiors shadowless tile should be placed by the world mouse path. Last message: " + overlay.LastMessage);
            Assert.AreEqual(clickCell, placedCell);
            Assert.IsNotNull(occupancyTilemap.GetTile(placedCell), "Mouse placement should write occupancy for the placed Modern Interiors furniture cell. Tile=" + tileName);
            if (ghostTilemap != null)
            {
                Assert.AreEqual(0, CountModernShadowlessTiles(ghostTilemap, out _, out _), "Successful placement should clear the transient ghost preview.");
            }

            if (footprintTilemap != null)
            {
                Assert.AreEqual(0, CountTiles(footprintTilemap), "Successful placement should clear the transient footprint preview.");
            }
        }

        [UnityTest]
        public IEnumerator ModernShadowlessFurnitureSaveClearLoad_RestoresGeneratedTileWithoutDuplicates()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var button = FindModernShadowlessPaletteButton();
            Assert.IsNotNull(button, "A generated Modern Interiors shadowless furniture button is required for save/load verification.");
            ExecuteEvents.Execute<IPointerClickHandler>(button, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
            yield return null;

            var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>();
            Assert.IsNotNull(overlay);
            Assert.IsTrue(overlay.TryManualPlaceFirstValidForTests(out var message), "Modern Interiors furniture should be placeable before save/load verification. Last message: " + message);
            yield return null;

            var furnitureTilemap = GameObject.Find("HouseFurnitureObjectTilemap")?.GetComponent<Tilemap>();
            var occupancyTilemap = GameObject.Find("HouseFurnitureOccupancyTilemap")?.GetComponent<Tilemap>();
            Assert.IsNotNull(furnitureTilemap);
            Assert.IsNotNull(occupancyTilemap);
            Assert.AreEqual(1, CountModernShadowlessTiles(furnitureTilemap, out var placedCell, out var tileName));
            Assert.IsNotNull(occupancyTilemap.GetTile(placedCell), "Placed Modern Interiors tile should write occupancy before save. Tile=" + tileName);
            var occupiedBefore = CountTiles(occupancyTilemap);

            var saveButton = GameObject.Find("SaveFurnitureLayoutButton")?.GetComponent<Button>();
            var clearButton = GameObject.Find("ClearFurnitureLayoutButton")?.GetComponent<Button>();
            var loadButton = GameObject.Find("LoadFurnitureLayoutButton")?.GetComponent<Button>();
            Assert.IsNotNull(saveButton, "House placement UI should expose a player-facing save button.");
            Assert.IsNotNull(clearButton, "House placement UI should expose a player-facing clear button.");
            Assert.IsNotNull(loadButton, "House placement UI should expose a player-facing load button.");

            saveButton.onClick.Invoke();
            yield return null;
            StringAssert.Contains("Saved", overlay.LastMessage);

            clearButton.onClick.Invoke();
            yield return null;
            Assert.AreEqual(0, CountModernShadowlessTiles(furnitureTilemap, out _, out _));
            Assert.AreEqual(0, CountTiles(occupancyTilemap));

            loadButton.onClick.Invoke();
            yield return null;
            StringAssert.Contains("Loaded", overlay.LastMessage);
            Assert.AreEqual(1, CountModernShadowlessTiles(furnitureTilemap, out var restoredCell, out var restoredTile));
            Assert.AreEqual(placedCell, restoredCell);
            Assert.AreEqual(tileName, restoredTile);
            Assert.AreEqual(occupiedBefore, CountTiles(occupancyTilemap));
            Assert.IsNotNull(occupancyTilemap.GetTile(restoredCell), "Loaded Modern Interiors tile should restore occupancy. Tile=" + restoredTile);

            loadButton.onClick.Invoke();
            yield return null;
            Assert.AreEqual(1, CountModernShadowlessTiles(furnitureTilemap, out _, out _), "Repeated load must not duplicate generated Modern Interiors tiles.");
            Assert.AreEqual(occupiedBefore, CountTiles(occupancyTilemap), "Repeated load must not duplicate occupancy cells.");
        }

        private static GameObject FindModernShadowlessPaletteButton()
        {
            var transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
            {
                var candidate = transforms[i] != null ? transforms[i].gameObject : null;
                if (candidate != null && candidate.name.StartsWith("PaletteFurniture_modern.interiors.shadowless.", System.StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static int CountPaletteFurnitureButtons()
        {
            int count = 0;
            var transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
            {
                var candidate = transforms[i] != null ? transforms[i].gameObject : null;
                if (candidate != null && candidate.name.StartsWith("PaletteFurniture_", System.StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private static InteriorFurnitureDefinition GetActiveFurniture(InteriorPlacementPreviewOverlay overlay)
        {
            var field = typeof(InteriorPlacementPreviewOverlay).GetField("_activeFurniture", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            return (InteriorFurnitureDefinition)field.GetValue(overlay);
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

        private static bool ContainsModernShadowlessTile(Tilemap tilemap, out Vector3Int foundCell, out string tileName)
        {
            return CountModernShadowlessTiles(tilemap, out foundCell, out tileName) > 0;
        }

        private static int CountModernShadowlessTiles(Tilemap tilemap, out Vector3Int firstCell, out string firstTileName)
        {
            firstCell = default;
            firstTileName = string.Empty;
            int count = 0;
            if (tilemap == null)
            {
                return 0;
            }

            foreach (var position in tilemap.cellBounds.allPositionsWithin)
            {
                var tile = tilemap.GetTile(position);
                if (tile != null && tile.name.Contains("_Singles_Shadowless_48x48_"))
                {
                    count++;
                    if (string.IsNullOrEmpty(firstTileName))
                    {
                        firstCell = position;
                        firstTileName = tile.name;
                    }
                }
            }

            return count;
        }

        private static int CountTiles(Tilemap tilemap)
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

        [DefaultExecutionOrder(-10000)]
        private sealed class MouseMoveFrameDriver : MonoBehaviour
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

                    var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>();
                    if (overlay != null)
                    {
                        var overlayType = typeof(InteriorPlacementPreviewOverlay);
                        overlayType.GetField("_wasMousePressed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(overlay, true);
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
