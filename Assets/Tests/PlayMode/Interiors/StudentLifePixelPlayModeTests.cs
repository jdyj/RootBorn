using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Rootborn.Game.Managers;
using Rootborn.Game.Placement;
using Rootborn.Game.Save;
using Rootborn.UI.Interiors;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.PlayMode.Interiors
{
    public sealed class StudentLifePixelPlayModeTests : Rootborn.Tests.PlayMode.PlayModeInputFixture
    {
        private static readonly PropCase[] Props =
        {
            new PropCase("studentlife.snack.delivery.crate", "StudentLife_snack_delivery_crate_Tile", 1, "snack-crate"),
            new PropCase("studentlife.toy.kitchen", "StudentLife_toy_kitchen_Tile", 2, "toy-kitchen")
        };

        private SaveSlotMetadata _previousSave;
        private string _previousSaveRoot;
        private string _runId;
        private string _saveRoot;
        private Mouse _mouse;
        private Keyboard _keyboard;
        private string _searchInputRoute;
        private static string EvidenceRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../output/student-life-integration-20261027/evidence/student-life-playmode"));
        private string Evidence => Path.Combine(EvidenceRoot, _runId);

        public override void Setup()
        {
            base.Setup();
            _previousSave = ActiveSaveContext.Metadata;
            var rootField = typeof(SaveService).GetField("s_rootDirectoryOverride", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(rootField, "Preserve the caller's save-root override rather than resetting it blindly.");
            _previousSaveRoot = (string)rootField.GetValue(null);
            _runId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            _saveRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../output/student-life-integration-20261027/saves-playmode", _runId));
            Directory.CreateDirectory(_saveRoot);
            Directory.CreateDirectory(Evidence);
            SaveService.SetRootDirectoryForTests(_saveRoot);
            ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = "studentlife-qa-bootstrap", DisplayName = "Student-life art QA" });
            _mouse = InputSystem.AddDevice<Mouse>();
            _keyboard = InputSystem.AddDevice<Keyboard>();
        }

        public override void TearDown()
        {
            ActiveSaveContext.Set(_previousSave);
            SaveService.SetRootDirectoryForTests(_previousSaveRoot);
            base.TearDown();
        }

        [UnitySetUp]
        public IEnumerator EnableRealUiInputForPlayerFlow()
        {
            // The assembly guard otherwise replaces the real UI module with a no-op PassiveInputModule.
            // Buttons have a separate runtime pointer router, but InputField focus needs the real UI module.
            InputSystemUiModulePlayModeTestGuard.UseRealInputSystemForCurrentTest();
            yield return null;
        }

        [Ignore(LegacyPremise.AuthoredHouse + "자동 저장되는 House에서는 초기화가 영구 삭제라 저장 뒤 초기화를 불러오기로 되돌리는 흐름이 없음")]
        [UnityTest]
        public IEnumerator FloorProps_MousePalettePlacementSaveClearLoadAndReload()
        {
            var boot = Managers.BootstrapAsync();
            yield return Await(boot, "Managers bootstrap");

            foreach (var prop in Props)
            {
                string slot = "studentlife-qa-" + prop.EvidenceName;
                ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = slot, DisplayName = "Student-life " + prop.EvidenceName + " QA" });
                Assert.IsFalse(File.Exists(Path.Combine(_saveRoot, slot, FurniturePlacementLayoutPersistence.GetHouseLayoutFileNameForStage(0))),
                    "Each prop must begin in a separate empty test slot.");
                yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
                yield return WaitForHousePalette();
                Assert.AreEqual(slot, ActiveSaveContext.SlotId);

                yield return SearchPalette(prop.StableId);
                string buttonName = "PaletteFurniture_" + prop.StableId;
                var paletteButton = GameObject.Find(buttonName);
                Assert.IsNotNull(paletteButton, "The real palette must expose " + prop.StableId + "; do not bypass its category limit with a direct selection call.");
                yield return ClickButton(buttonName);
                var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>();
                Assert.IsNotNull(overlay);
                Assert.Greater(overlay.ValidCellCount, 0, "Mouse selection must show actual valid cells.");
                var availability = FindMap("AuthoredPlacementAvailability");
                var objects = FindMap("AuthoredUserFurniture");
                var occupancy = FindMap("AuthoredUserOccupancy");
                var occupancyBefore = OccupiedCells(occupancy);
                Assert.IsTrue(FindVisibleCell(availability, out var target, out var pointer),
                    "A valid visible floor cell outside all UI is required; no forced placement is allowed.");

                yield return Click(pointer);
                AssertPlacement(objects, occupancy, target, prop);
                var addedCells = OccupiedCells(occupancy);
                addedCells.ExceptWith(occupancyBefore);
                var expectedCells = Enumerable.Range(0, prop.FootprintWidth).Select(x => target + Vector3Int.right * x).ToArray();
                CollectionAssert.AreEquivalent(expectedCells, addedCells, "Sprite height must not add phantom floor occupancy.");
                Assert.AreEqual(0, overlay.ValidCellCount);
                Assert.AreEqual(0, overlay.InvalidCellCount);
                Assert.IsNull(GameObject.Find("ModernOfficeChairSampleTilemap"));
                yield return Capture(prop.EvidenceName + "-placed");

                yield return ClickButton("SaveFurnitureLayoutButton");
                var service = new SaveService(slot);
                StringAssert.StartsWith(_saveRoot, Path.GetFullPath(service.DirectoryPath));
                string saved = service.ReadJson(FurniturePlacementLayoutPersistence.GetHouseLayoutFileNameForStage(0));
                Assert.IsNotEmpty(saved);
                var layout = JsonUtility.FromJson<FurniturePlacementLayoutSaveData>(saved);
                Assert.IsNotNull(layout);
                var savedProp = layout.Items.Single(item => item != null && item.FurnitureId == prop.StableId);
                Assert.AreEqual(target, savedProp.ToAnchorCell());

                yield return ClickButton("ClearFurnitureLayoutButton");
                Assert.IsNull(objects.GetTile(target), "Clear must remove the placed visual.");
                foreach (var cell in expectedCells) Assert.IsNull(occupancy.GetTile(cell), "Clear must release floor occupancy at " + cell);
                yield return ClickButton("LoadFurnitureLayoutButton");
                AssertPlacement(objects, occupancy, target, prop);

                yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
                yield return WaitForHousePalette();
                objects = FindMap("AuthoredUserFurniture");
                occupancy = FindMap("AuthoredUserOccupancy");
                Assert.AreEqual(slot, ActiveSaveContext.SlotId);
                AssertPlacement(objects, occupancy, target, prop);
                yield return Capture(prop.EvidenceName + "-reloaded");
                File.WriteAllText(Path.Combine(Evidence, prop.EvidenceName + "-placement.txt"),
                    "input=MouseState press/release through EventSystem and overlay Update; no placement/selection method called\n" +
                    "paletteSearchInput=" + _searchInputRoute + "\nsaveRoot=" + _saveRoot + "\nslot=" + slot + "\npalette=" + buttonName + "\ntile=" + prop.TileName +
                    "\ncell=" + target + "\nfootprint=" + prop.FootprintWidth + "x1\noccupancy=" + string.Join(";", expectedCells.Select(cell => cell.ToString())) +
                    "\nobjectRendererEnabled=" + objects.GetComponent<TilemapRenderer>().enabled +
                    "\nsaveClearLoadAndReload=passed\n" + saved);
            }
        }

        [UnityTest]
        public IEnumerator StudentLifeAssets_AddressableAsyncLoadAndRelease()
        {
            yield return Await(Managers.BootstrapAsync(), "Managers bootstrap");
            var resource = Managers.Resource;
            Assert.IsNotNull(resource);
            const string label = "StudentLifePixels";
            const string spriteAddress = "sprites/student-life/sprites/old-town-ledger";
            var addresses = new List<string> { spriteAddress };
            foreach (var prop in Props)
            {
                addresses.Add("data/furniture/" + prop.StableId.Replace('.', '-'));
                addresses.Add("tiles/student-life/" + prop.TileName.ToLowerInvariant().Replace('_', '-'));
            }
            try
            {
                var pack = resource.LoadByLabelAsync<Object>(label);
                yield return Await(pack, "StudentLifePixels label load");
                Assert.IsNotNull(pack.Result);
                Assert.IsTrue(pack.Result.All(asset => asset != null));
                // Packed catalogs can expose Sprite and Texture2D for the same PNG.
                Assert.AreEqual(12, pack.Result.Select(asset => asset.name).Distinct().Count(), "8 image assets + 2 furniture definitions + 2 tiles.");
                Assert.AreEqual(8, pack.Result.Where(asset => asset is Sprite || asset is Texture2D).Select(asset => asset.name).Distinct().Count());
                Assert.AreEqual(2, pack.Result.OfType<FurnitureDefinition>().Count());
                Assert.AreEqual(2, pack.Result.OfType<Tile>().Count());

                var ledger = resource.LoadAsync<Sprite>(spriteAddress);
                yield return Await(ledger, "Ledger sprite load");
                Assert.IsNotNull(ledger.Result);
                Assert.AreEqual("old_town_ledger", ledger.Result.name);
                Assert.AreEqual(FilterMode.Point, ledger.Result.texture.filterMode);
                Assert.AreEqual(48f, ledger.Result.pixelsPerUnit);
                foreach (var prop in Props)
                {
                    string definitionAddress = "data/furniture/" + prop.StableId.Replace('.', '-');
                    string tileAddress = "tiles/student-life/" + prop.TileName.ToLowerInvariant().Replace('_', '-');
                    var definition = resource.LoadAsync<FurnitureDefinition>(definitionAddress);
                    yield return Await(definition, prop.StableId + " definition load");
                    Assert.IsNotNull(definition.Result);
                    Assert.AreEqual(prop.StableId, definition.Result.StableId);
                    Assert.AreEqual(prop.FootprintWidth, definition.Result.FootprintCells.Count);
                    var tile = resource.LoadAsync<Tile>(tileAddress);
                    yield return Await(tile, prop.StableId + " tile load");
                    Assert.IsNotNull(tile.Result);
                    Assert.AreEqual(prop.TileName, tile.Result.name);
                    Assert.IsNotNull(tile.Result.sprite);
                    Assert.AreEqual(Tile.ColliderType.None, tile.Result.colliderType, "Floor occupancy owns blocking; tall art must not become a full-height collider.");
                }
                File.WriteAllText(Path.Combine(Evidence, "addressables-load.txt"),
                    "loader=Managers.Resource.LoadAsync / LoadByLabelAsync\nlabel=" + label + "\nlogicalAssetCount=12\nloadedObjectCount=" + pack.Result.Count +
                    "\nimages=8\ndefinitions=2\ntiles=2\naddresses=" + string.Join("\n", addresses));
            }
            finally
            {
                foreach (string address in addresses) resource.Release(address);
                resource.Release("label:" + label);
            }
            foreach (string address in addresses) Assert.IsNull(resource.Load<Object>(address), "Explicit release must evict the load cache: " + address);
        }

        private static IEnumerator Await(Task task, string operation)
        {
            float deadline = Time.realtimeSinceStartup + 60f;
            while (!task.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(task.IsCompleted, operation + " timed out.");
            Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
            Assert.IsFalse(task.IsCanceled, operation + " was cancelled.");
        }

        private IEnumerator WaitForHousePalette()
        {
            float deadline = Time.realtimeSinceStartup + 15f;
            InteriorFurniturePlacementPanel panel = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                panel = Object.FindFirstObjectByType<InteriorFurniturePlacementPanel>(FindObjectsInactive.Include);
                if (panel != null && EventSystem.current != null && Camera.main != null) break;
                yield return null;
            }
            Assert.IsNotNull(panel, "House placement UI did not install.");
            // The toggle button is created a frame or more after the panel.
            yield return HousePlacementTestDriver.OpenPlacementMode();
            yield return null;
            Assert.IsTrue(panel.gameObject.activeInHierarchy);
            Assert.IsNotNull(Camera.main);
            Assert.IsNotNull(EventSystem.current);
        }

        private static Tilemap FindMap(string name)
        {
            var go = GameObject.Find(name);
            Assert.IsNotNull(go, name);
            var map = go.GetComponent<Tilemap>();
            Assert.IsNotNull(map, name);
            return map;
        }

        private static HashSet<Vector3Int> OccupiedCells(Tilemap map)
        {
            var cells = new HashSet<Vector3Int>();
            foreach (var cell in map.cellBounds.allPositionsWithin)
                if (map.HasTile(cell)) cells.Add(cell);
            return cells;
        }

        private static void AssertPlacement(Tilemap objects, Tilemap occupancy, Vector3Int target, PropCase prop)
        {
            Assert.AreEqual(prop.TileName, objects.GetTile(target)?.name);
            var tile = objects.GetTile<Tile>(target);
            Assert.IsNotNull(tile.sprite);
            Assert.IsNotNull(tile.sprite.texture);
            Assert.IsTrue(objects.gameObject.activeInHierarchy);
            Assert.IsTrue(objects.GetComponent<TilemapRenderer>().enabled);
            for (int x = 0; x < prop.FootprintWidth; x++) Assert.IsNotNull(occupancy.GetTile(target + Vector3Int.right * x));
            int visibleInstances = 0;
            foreach (var cell in objects.cellBounds.allPositionsWithin)
                if (objects.GetTile(cell)?.name == prop.TileName) visibleInstances++;
            Assert.AreEqual(1, visibleInstances,
                "The full sprite is drawn once at its lower-left anchor.");
        }

        private IEnumerator SearchPalette(string query)
        {
            Assert.IsNotNull(EventSystem.current, "Search requires the player-facing EventSystem.");
            Assert.IsInstanceOf<InputSystemUIInputModule>(EventSystem.current.currentInputModule,
                "Search focus must be delivered by the real UI module; the assembly's PassiveInputModule cannot process pointer events.");
            var go = GameObject.Find("FurniturePaletteSearchInput");
            Assert.IsNotNull(go, "The palette search must be reachable through its actual InputField.");
            var input = go.GetComponent<InputField>();
            Assert.IsNotNull(input);
            var rect = go.GetComponent<RectTransform>();
            var canvas = go.GetComponentInParent<Canvas>();
            Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            var position = RectTransformUtility.WorldToScreenPoint(eventCamera, rect.TransformPoint(rect.rect.center));
            Assert.IsTrue(position.x >= 0 && position.x <= Screen.width && position.y >= 0 && position.y <= Screen.height);
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
            Assert.IsNotEmpty(hits);
            Assert.IsTrue(hits[0].gameObject == go || hits[0].gameObject.transform.IsChildOf(go.transform), "Search input is covered by " + hits[0].gameObject.name);
            yield return Click(position);
            yield return null;
            Assert.IsTrue(input.isFocused, "A real mouse click must focus the search input.");
            Assert.AreEqual(string.Empty, input.text, "A newly loaded House must start with an empty search field.");
            _keyboard.MakeCurrent();
            foreach (char character in query) InputSystem.QueueTextEvent(_keyboard, character);
            InputSystem.Update();
            yield return null;
            yield return null;
            if (input.text == query)
            {
                _searchInputRoute = "MouseState focus + InputSystem.QueueTextEvent";
            }
            else
            {
                // This installed uGUI InputField consumes Event.PopEvent rather than Keyboard.onTextInput.
                // Exercise its supported text-event API; never assign .text or call selection/placement methods.
                Assert.AreEqual(string.Empty, input.text, "Unexpected partial text delivery; do not append a duplicate query.");
                foreach (char character in query)
                    input.ProcessEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.None, character = character });
                input.ForceLabelUpdate();
                _searchInputRoute = "MouseState focus + uGUI InputField.ProcessEvent(KeyDown characters); QueueTextEvent unsupported by this uGUI version";
            }
            yield return null;
            Assert.AreEqual(query, input.text, "Search must respond to actual text input events.");
        }

        private IEnumerator ClickButton(string name)
        {
            var go = GameObject.Find(name);
            Assert.IsNotNull(go, "Missing active UI target: " + name);
            var button = go.GetComponent<Button>();
            Assert.IsNotNull(button, name);
            Assert.IsTrue(button.interactable, name);
            var rect = go.GetComponent<RectTransform>();
            var canvas = go.GetComponentInParent<Canvas>();
            Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            var position = RectTransformUtility.WorldToScreenPoint(eventCamera, rect.TransformPoint(rect.rect.center));
            Assert.IsTrue(position.x >= 0 && position.x <= Screen.width && position.y >= 0 && position.y <= Screen.height,
                name + " lies outside the Game View at " + position + "; the palette needs a real visible navigation path.");
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
            Assert.IsNotEmpty(hits, name + " has no raycast target.");
            Assert.IsTrue(hits[0].gameObject == go || hits[0].gameObject.transform.IsChildOf(go.transform),
                name + " is covered by " + hits[0].gameObject.name);
            yield return Click(position);
        }

        private IEnumerator Click(Vector2 position)
        {
            _mouse.MakeCurrent();
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = position });
            InputSystem.Update();
            yield return null;
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = position, buttons = 1 });
            InputSystem.Update();
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = position });
            InputSystem.Update();
            yield return null;
            yield return null;
        }

        private static bool FindVisibleCell(Tilemap map, out Vector3Int cell, out Vector2 pointer)
        {
            var camera = Camera.main;
            foreach (var candidate in map.cellBounds.allPositionsWithin)
            {
                var tile = map.GetTile<Tile>(candidate);
                if (tile == null || tile.color.g < .8f || tile.color.r > .3f) continue;
                var screen = camera.WorldToScreenPoint(map.GetCellCenterWorld(candidate));
                if (screen.z <= 0 || screen.x < Screen.width * .28f || screen.x > Screen.width * .7f || screen.y < Screen.height * .25f || screen.y > Screen.height * .75f) continue;
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screen }, hits);
                if (hits.Count > 0) continue;
                cell = candidate;
                pointer = screen;
                return true;
            }
            cell = default;
            pointer = default;
            return false;
        }

        private IEnumerator Capture(string basename)
        {
            string gameViewPath = Path.Combine(Evidence, basename + "-gameview.png");
            ScreenCapture.CaptureScreenshot(gameViewPath);
            for (int frame = 0; frame < 30 && !File.Exists(gameViewPath); frame++) yield return null;
            Assert.IsTrue(File.Exists(gameViewPath), "Actual Game View screenshot must be written, including UI.");
            Assert.Greater(new FileInfo(gameViewPath).Length, 0L);

            var camera = Camera.main;
            Assert.IsNotNull(camera);
            int width = Mathf.Max(1, camera.pixelWidth);
            int height = Mathf.Max(1, camera.pixelHeight);
            var target = new RenderTexture(width, height, 24);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(Path.Combine(Evidence, basename + "-camera.png"), texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(target);
            }
        }

        private readonly struct PropCase
        {
            public readonly string StableId;
            public readonly string TileName;
            public readonly int FootprintWidth;
            public readonly string EvidenceName;
            public PropCase(string stableId, string tileName, int footprintWidth, string evidenceName)
            {
                StableId = stableId;
                TileName = tileName;
                FootprintWidth = footprintWidth;
                EvidenceName = evidenceName;
            }
        }
    }
}
