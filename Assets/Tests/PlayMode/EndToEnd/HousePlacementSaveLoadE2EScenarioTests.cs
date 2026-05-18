using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rootborn.Tests.PlayMode.Scenarios;
using Rootborn.Game.Placement;
using Rootborn.Game.Save;
using Rootborn.UI.Interiors;
using Rootborn.UI.MainMenu;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    public sealed class HousePlacementSaveLoadE2EScenarioTests
    {
        private const string LayoutFileName = "house-furniture-layout.json";
        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            ActiveSaveContext.Clear();
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-house-placement-e2e-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveRoot);
            SaveSlotSelectPanel.SetSaveRootForTests(_saveRoot);
            SaveService.SetRootDirectoryForTests(_saveRoot);
            ClearStaticFurnitureLayoutCache();
            DestroyIfFound("SaveSlotSelectPanel");
            DestroyIfFound("SaveSlotCanvas");
            DestroyIfFound("EventSystem");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            ActiveSaveContext.Clear();
            SaveSlotSelectPanel.SetSaveRootForTests(null);
            SaveService.SetRootDirectoryForTests(null);
            ClearStaticFurnitureLayoutCache();
            DestroyIfFound("SaveSlotSelectPanel");
            DestroyIfFound("SaveSlotCanvas");
            DestroyIfFound("EventSystem");
            if (!string.IsNullOrEmpty(_saveRoot) && Directory.Exists(_saveRoot))
            {
                Directory.Delete(_saveRoot, true);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator HOUSE_FLOW_001_003_NewGamePlaceSaveExitLoadRestoresHouseFurnitureWithVisualEvidence()
        {
            Assert.AreEqual("HOUSE-FLOW-001", ScenarioId.HOUSE_FLOW_001);
            Assert.AreEqual("HOUSE-FLOW-002", ScenarioId.HOUSE_FLOW_002);
            Assert.AreEqual("HOUSE-FLOW-003", ScenarioId.HOUSE_FLOW_003);
            yield return OpenCreatorFromMainMenu();
            yield return ClickButtonNamed("ConfirmButton");
            yield return WaitForScene("Town", 10f);
            Assert.AreEqual("slot-0", ActiveSaveContext.SlotId, "New Game confirmation should activate slot-0 before entering the world.");
            Assert.IsTrue(File.Exists(Path.Combine(_saveRoot, "slot-0", "metadata.json")), "New Game should create slot metadata before House placement persistence.");

            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return WaitForStableScene("House", 0.25f, 10f);
            yield return PlaceModernShadowlessFurnitureByPlayerFacingInput();

            var furnitureTilemap = RequiredTilemap("HouseFurnitureObjectTilemap");
            var occupancyTilemap = RequiredTilemap("HouseFurnitureOccupancyTilemap");
            Assert.AreEqual(1, CountModernShadowlessTiles(furnitureTilemap, out var placedCell, out var placedTileName), "Exactly one player-placed Modern Interiors furniture tile should exist before saving.");
            Assert.IsNotNull(occupancyTilemap.GetTile(placedCell), "Placed furniture must write occupancy before saving.");
            int occupiedBefore = CountTiles(occupancyTilemap);

            yield return ClickButtonNamed("SaveFurnitureLayoutButton");
            var layoutPath = Path.Combine(_saveRoot, "slot-0", LayoutFileName);
            Assert.IsTrue(File.Exists(layoutPath), "House furniture save must persist to the active save slot, not only a runtime static list.");
            StringAssert.Contains(placedTileName, File.ReadAllText(layoutPath), "The saved layout JSON should identify the same furniture tile placed by the player-facing world click.");
            yield return CaptureVisualEvidence(
                "house-interior-placement-before-reload.png",
                "house-interior-placement-before-reload-probe.txt",
                furnitureTilemap,
                occupancyTilemap,
                placedCell,
                placedTileName);

            ClearStaticFurnitureLayoutCache();
            ActiveSaveContext.Clear();
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            yield return WaitForStableScene("MainMenu", 0.25f, 10f);
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            yield return ClickButtonNamed("LoadButton");
            yield return WaitForScene("Town", 10f);
            Assert.AreEqual("slot-0", ActiveSaveContext.SlotId, "Loading the save slot should restore the active save context before House reload.");

            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return WaitForStableScene("House", 0.35f, 10f);
            yield return null;

            var restoredFurnitureTilemap = RequiredTilemap("HouseFurnitureObjectTilemap");
            var restoredOccupancyTilemap = RequiredTilemap("HouseFurnitureOccupancyTilemap");
            Assert.AreEqual(1, CountModernShadowlessTiles(restoredFurnitureTilemap, out var restoredCell, out var restoredTileName), "Loading a saved game and entering House should restore the placed furniture visually.");
            Assert.AreEqual(placedCell, restoredCell, "Restored furniture should remain at the same tile cell after save-slot reload.");
            Assert.AreEqual(placedTileName, restoredTileName, "Restored furniture should keep the same tile asset after save-slot reload.");
            Assert.AreEqual(occupiedBefore, CountTiles(restoredOccupancyTilemap), "Restored occupancy should match the saved placement footprint without duplicates.");
            Assert.IsNotNull(restoredOccupancyTilemap.GetTile(restoredCell), "Restored furniture must also restore occupancy for gameplay collision/placement rules.");
            Assert.IsNull(GameObject.Find("ModernOfficeChairSampleTilemap"), "Stale sample/debug furniture tilemaps must not be visible after saved-game House reload.");
            yield return CaptureVisualEvidence(
                "house-interior-placement-after-reload.png",
                "house-interior-placement-after-reload-probe.txt",
                restoredFurnitureTilemap,
                restoredOccupancyTilemap,
                restoredCell,
                restoredTileName);
        }

        private IEnumerator OpenCreatorFromMainMenu()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            yield return WaitForStableScene("MainMenu", 0.25f, 10f);
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            yield return ClickButtonNamed("NewGameButton");
            Assert.IsNotNull(GameObject.Find("SpumCharacterCreatorRoot"), "New Game should open the SPUM creator before the save is created.");
        }

        private static IEnumerator PlaceModernShadowlessFurnitureByPlayerFacingInput()
        {
            Assert.IsNull(GameObject.Find("ModernOfficeChairSampleTilemap"), "Debug/sample furniture tilemap must not be visible before placement.");
            var button = FindModernShadowlessPaletteButton();
            Assert.IsNotNull(button, "A generated Modern Interiors shadowless furniture palette button is required for the E2E scenario.");
            ExecuteEvents.Execute<IPointerClickHandler>(button, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
            yield return null;

            var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>();
            Assert.IsNotNull(overlay, "Selecting furniture should create the placement overlay through the player-facing UI.");
            Assert.Greater(overlay.ValidCellCount, 0, "Selecting furniture should expose valid world placement cells.");
            var overlayTilemap = RequiredTilemap("HousePlacementAvailabilityTilemap");
            var camera = Camera.main;
            Assert.IsNotNull(camera, "House placement requires a Main Camera for world click projection.");
            Assert.IsTrue(TryFindVisibleValidOverlayCell(overlayTilemap, camera, out _, out var screenPosition), "A visible valid placement cell outside UI should be available for the player-facing mouse click.");

            var mouse = InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
            var driver = new GameObject("HousePlacementSaveLoadE2EMouseClickDriver").AddComponent<MouseClickFrameDriver>();
            driver.Configure(mouse, screenPosition);
            yield return null;
            yield return null;
            InputSystem.RemoveDevice(mouse);
            Assert.AreEqual(0, overlay.ValidCellCount, "Successful world click placement should clear the availability overlay.");
            Assert.AreEqual(0, overlay.InvalidCellCount, "Successful world click placement should clear invalid overlay cells.");
        }

        private static IEnumerator CaptureVisualEvidence(string screenshotFileName, string probeFileName, Tilemap furnitureTilemap, Tilemap occupancyTilemap, Vector3Int restoredCell, string restoredTileName)
        {
            string evidenceDirectory = Path.Combine(Application.dataPath, "..", "production", "qa", "evidence");
            Directory.CreateDirectory(evidenceDirectory);
            string screenshotPath = Path.Combine(evidenceDirectory, screenshotFileName);
            string probePath = Path.Combine(evidenceDirectory, probeFileName);
            if (File.Exists(screenshotPath))
            {
                File.Delete(screenshotPath);
            }

            var availabilityTilemap = GameObject.Find("HousePlacementAvailabilityTilemap")?.GetComponent<Tilemap>();
            var ghostTilemap = GameObject.Find("HouseFurnitureGhostPreviewTilemap")?.GetComponent<Tilemap>();
            var footprintTilemap = GameObject.Find("HouseFurnitureFootprintPreviewTilemap")?.GetComponent<Tilemap>();
            File.WriteAllText(probePath,
                "scene=" + SceneManager.GetActiveScene().name + "\n" +
                "tilemap=" + furnitureTilemap.gameObject.name + "\n" +
                "tileName=" + restoredTileName + "\n" +
                "cell=" + restoredCell + "\n" +
                "objectTileCount=" + CountTiles(furnitureTilemap) + "\n" +
                "occupancyTileCount=" + CountTiles(occupancyTilemap) + "\n" +
                "availabilityOverlayTileCount=" + (availabilityTilemap != null ? CountTiles(availabilityTilemap) : 0) + "\n" +
                "ghostTileCount=" + (ghostTilemap != null ? CountTiles(ghostTilemap) : 0) + "\n" +
                "footprintTileCount=" + (footprintTilemap != null ? CountTiles(footprintTilemap) : 0) + "\n" +
                "sampleTilemapVisible=" + (GameObject.Find("ModernOfficeChairSampleTilemap") != null));
            ScreenCapture.CaptureScreenshot(screenshotPath);
            float elapsed = 0f;
            while (!File.Exists(screenshotPath) && elapsed < 3f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!File.Exists(screenshotPath))
            {
                CaptureCameraScreenshot(screenshotPath);
            }

            Assert.IsTrue(File.Exists(screenshotPath), "The visual verification gate requires a Game View or Camera screenshot artifact.");
            Assert.IsTrue(new FileInfo(screenshotPath).Length > 0, "The Game View screenshot artifact must not be empty.");
            Assert.IsTrue(File.Exists(probePath), "The runtime Tilemap probe artifact must be written for visual placement verification.");
        }

        private static void CaptureCameraScreenshot(string path)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                return;
            }

            var camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var texture = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            try
            {
                camera.targetTexture = texture;
                RenderTexture.active = texture;
                camera.Render();
                var image = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0f, 0f, texture.width, texture.height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
                Object.DestroyImmediate(image);
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                texture.Release();
                Object.DestroyImmediate(texture);
            }
        }

        private static IEnumerator ClickButtonNamed(string name)
        {
            var go = GameObject.Find(name);
            Assert.IsNotNull(go, name);
            var button = go.GetComponent<Button>();
            Assert.IsNotNull(button, name);
            Assert.IsTrue(button.IsInteractable(), name + " must be interactable.");
            Assert.IsNotNull(EventSystem.current, "UI click tests require an EventSystem.");
            ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
            yield return null;
        }

        private static IEnumerator WaitForScene(string sceneName, float timeout)
        {
            float elapsed = 0f;
            while (SceneManager.GetActiveScene().name != sceneName && elapsed < timeout)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }

            Assert.AreEqual(sceneName, SceneManager.GetActiveScene().name);
        }

        private static IEnumerator WaitForStableScene(string sceneName, float stableSeconds, float timeout)
        {
            float elapsed = 0f;
            float stable = 0f;
            while (elapsed < timeout)
            {
                if (SceneManager.GetActiveScene().name == sceneName)
                {
                    stable += Time.deltaTime;
                    if (stable >= stableSeconds)
                    {
                        yield break;
                    }
                }
                else
                {
                    stable = 0f;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.AreEqual(sceneName, SceneManager.GetActiveScene().name);
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

            var eventData = new PointerEventData(EventSystem.current) { position = new Vector2(screen.x, screen.y) };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            return results.Count > 0;
        }

        private static Tilemap RequiredTilemap(string name)
        {
            var tilemap = GameObject.Find(name)?.GetComponent<Tilemap>();
            Assert.IsNotNull(tilemap, name);
            return tilemap;
        }

        private static int CountModernShadowlessTiles(Tilemap tilemap, out Vector3Int firstCell, out string firstTileName)
        {
            firstCell = default;
            firstTileName = string.Empty;
            int count = 0;
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

        private static void ClearStaticFurnitureLayoutCache()
        {
            var field = typeof(InteriorPlacementPreviewOverlay).GetField("SavedFurnitureLayout", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var list = field != null ? field.GetValue(null) as IList<FurniturePlacementSaveData> : null;
            if (list != null)
            {
                list.Clear();
            }
        }

        private static void DestroyIfFound(string name)
        {
            var go = GameObject.Find(name);
            if (go != null)
            {
                Object.DestroyImmediate(go);
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