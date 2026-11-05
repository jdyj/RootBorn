using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Player;
using Rootborn.Game.Common;
using Rootborn.Game.Managers;
using Rootborn.Game.Save;
using Rootborn.Game.Placement;
using Rootborn.UI.Interiors;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.PlayMode.Interiors
{
    public sealed class SchoolPixelPlayModeTests : Rootborn.Tests.PlayMode.PlayModeInputFixture
    {
        private SaveSlotMetadata _previousSave;
        private string _slot;
        private Keyboard _keyboard;
        private Mouse _mouse;
        private string _searchInputRoute;
        private static string Evidence => Path.GetFullPath(Path.Combine(Application.dataPath, "../output/school-game-integration/evidence"));

        public override void Setup()
        {
            base.Setup();
            _previousSave = ActiveSaveContext.Metadata;
            _slot = "school-pixel-qa-" + Guid.NewGuid().ToString("N");
            ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = _slot, DisplayName = "School pixel QA" });
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
            Directory.CreateDirectory(Evidence);
        }

        public override void TearDown()
        {
            ActiveSaveContext.Set(_previousSave);
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

        [UnityTest]
        public IEnumerator Classroom_KeyboardMovementFloorCollisionAndDepthSorting()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/ArtReview/SchoolPixelReview.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return new WaitForSeconds(.3f);
            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.IsNotNull(player);
            var start = player.transform.position;
            Capture("classroom-start.png");
            yield return Hold(Key.W, .3f);
            var blocked = player.transform.position;
            Assert.Less(blocked.y - start.y, .2f, "Walking into the chair must stop at its floor collider.");
            yield return Hold(Key.D, .25f);
            var aisleStart = player.transform.position;
            Assert.Greater(aisleStart.x - blocked.x, .7f, "Actual keyboard input must move the real player into the aisle.");
            yield return Hold(Key.W, .4f);
            var aisleEnd = player.transform.position;
            Assert.Greater(aisleEnd.y - aisleStart.y, 1f, "The aisle must stay walkable despite tall sprite artwork.");
            Capture("classroom-aisle.png");
            File.WriteAllText(Path.Combine(Evidence, "classroom-movement.txt"),
                "input=KeyboardState W/D/W through PlayerController.Update and Rigidbody2D\n" +
                "start=" + start + "\nchairBlocked=" + blocked + "\naisleStart=" + aisleStart + "\naisleEnd=" + aisleEnd + "\n" +
                "playerOrder=" + player.GetComponent<SortingGroup>().sortingOrder + "\n" +
                "playerSpriteCount=" + player.GetComponentsInChildren<SpriteRenderer>().Count(r => r.enabled && r.sprite != null));
            Assert.Greater(player.GetComponentsInChildren<SpriteRenderer>().Count(r => r.enabled && r.sprite != null), 0, "Player must actually render.");
        }

        [UnityTest]
        public IEnumerator Classroom_WalkingAroundBookcaseChangesFrontBackOcclusion()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/ArtReview/SchoolPixelReview.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return new WaitForSeconds(.3f);
            var player = Object.FindFirstObjectByType<PlayerController>();
            yield return WalkTo(player, Key.A, -3f, true);
            yield return WalkTo(player, Key.W, -1.8f, false);
            yield return WalkTo(player, Key.A, -4f, true);
            var shelf = GameObject.Find("shelf").GetComponent<SortingGroup>();
            var group = player.GetComponent<SortingGroup>();
            var behind = player.transform.position;
            int behindOrder = group.sortingOrder;
            Assert.Less(behindOrder, shelf.sortingOrder, "Walking behind the bookcase should render the player behind it.");
            Capture("classroom-behind-bookcase.png");
            yield return WalkTo(player, Key.D, -3f, true);
            yield return WalkTo(player, Key.S, -3.1f, false);
            yield return WalkTo(player, Key.A, -4f, true);
            Assert.Greater(group.sortingOrder, shelf.sortingOrder, "Walking around to the front must put the player in front.");
            Capture("classroom-front-bookcase.png");
            File.WriteAllText(Path.Combine(Evidence, "classroom-occlusion.txt"),
                "input=keyboard A/W/A/D/S/A; no Transform position assigned\n" +
                "behindPosition=" + behind + "\nbehindOrder=" + behindOrder + "\nshelfOrder=" + shelf.sortingOrder +
                "\nfrontPosition=" + player.transform.position + "\nfrontOrder=" + group.sortingOrder);
        }

        [UnityTest]
        public IEnumerator SchoolAssets_AddressableAsyncLoadAndRelease()
        {
            var boot = Managers.BootstrapAsync();
            yield return new WaitUntil(() => boot.IsCompleted);
            Assert.IsFalse(boot.IsFaulted, boot.Exception?.ToString());
            var resource = Managers.Resource;
            const string shelfAddress = "sprites/school/sprites/bookcase";
            const string deskAddress = "data/furniture/school-desk-with-open-book";
            const string tileAddress = "tiles/school/school-desk-with-open-book-tile";
            try
            {
                var pack = resource.LoadByLabelAsync<Object>("SchoolPixels");
                yield return new WaitUntil(() => pack.IsCompleted);
                Assert.IsFalse(pack.IsFaulted, pack.Exception?.ToString());
                Assert.IsTrue(pack.Result.All(a => a != null));
                // Packed catalogs expose both Sprite and Texture2D locations for a single PNG.
                // Count logical assets, not provider-specific typed locations.
                Assert.AreEqual(58, pack.Result.Select(a => a.name).Distinct().Count());
                Assert.AreEqual(36, pack.Result.Where(a => a is Sprite || a is Texture2D).Select(a => a.name).Distinct().Count());
                Assert.AreEqual(11, pack.Result.OfType<FurnitureDefinition>().Count());
                Assert.AreEqual(11, pack.Result.OfType<Tile>().Count());
                var shelf = resource.LoadAsync<Sprite>(shelfAddress);
                yield return new WaitUntil(() => shelf.IsCompleted);
                Assert.IsFalse(shelf.IsFaulted, shelf.Exception?.ToString());
                Assert.IsNotNull(shelf.Result);
                Assert.AreEqual("bookcase", shelf.Result.name);
                Assert.AreEqual(FilterMode.Point, shelf.Result.texture.filterMode);
                var desk = resource.LoadAsync<FurnitureDefinition>(deskAddress);
                yield return new WaitUntil(() => desk.IsCompleted);
                Assert.IsFalse(desk.IsFaulted, desk.Exception?.ToString());
                Assert.IsNotNull(desk.Result);
                Assert.AreEqual("school.desk.with.open.book", desk.Result.StableId);
                var tile = resource.LoadAsync<Tile>(tileAddress);
                yield return new WaitUntil(() => tile.IsCompleted);
                Assert.IsFalse(tile.IsFaulted, tile.Exception?.ToString());
                Assert.IsNotNull(tile.Result);
                Assert.IsNotNull(tile.Result.sprite);
                File.WriteAllText(Path.Combine(Evidence, "addressables-load.txt"),
                    "loader=Managers.Resource.LoadAsync / LoadByLabelAsync\nlogicalAssetCount=58\nloadedObjectCount=" + pack.Result.Count +
                    "\nsprite=" + shelf.Result.name + "\ndefinition=" + desk.Result.StableId + "\ntile=" + tile.Result.name);
            }
            finally
            {
                resource.Release(shelfAddress);
                resource.Release(deskAddress);
                resource.Release(tileAddress);
                resource.Release("label:SchoolPixels");
            }
            Assert.IsNull(resource.Load<Sprite>(shelfAddress));
            Assert.IsNull(resource.Load<FurnitureDefinition>(deskAddress));
            Assert.IsNull(resource.Load<Tile>(tileAddress));
        }

        [UnityTest]
        public IEnumerator SchoolDesk_MouseSelectionPlacementSaveClearLoadAndSceneReload()
        {
            var boot = Managers.BootstrapAsync();
            yield return new WaitUntil(() => boot.IsCompleted);
            Assert.IsFalse(boot.IsFaulted, boot.Exception?.ToString());
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return new WaitForSeconds(.4f);
            Assert.AreEqual(_slot, ActiveSaveContext.SlotId);
            yield return SearchPalette("school.desk.with.open.book");
            const string buttonName = "PaletteFurniture_school.desk.with.open.book";
            const string tileName = "School_desk_with_open_book_Tile";
            var button = GameObject.Find(buttonName);
            Assert.IsNotNull(button, "The imported school desk must be exposed through the actual furniture palette.");
            yield return Click(RectTransformUtility.WorldToScreenPoint(null, button.GetComponent<RectTransform>().position));
            var overlay = Object.FindFirstObjectByType<InteriorPlacementPreviewOverlay>();
            Assert.IsNotNull(overlay);
            Assert.Greater(overlay.ValidCellCount, 0, "A real mouse click must select the desk and show valid cells.");
            var availability = GameObject.Find("HousePlacementAvailabilityTilemap").GetComponent<Tilemap>();
            Assert.IsTrue(FindVisibleCell(availability, out var target, out var pointer), "A valid visible cell outside UI is needed.");
            yield return Click(pointer);
            var objects = GameObject.Find("HouseFurnitureObjectTilemap").GetComponent<Tilemap>();
            var occupancy = GameObject.Find("HouseFurnitureOccupancyTilemap").GetComponent<Tilemap>();
            Assert.AreEqual(tileName, objects.GetTile(target)?.name, overlay.LastMessage);
            Assert.IsNotNull(occupancy.GetTile(target));
            Assert.IsNotNull(occupancy.GetTile(target + Vector3Int.right));
            Assert.AreEqual(0, overlay.ValidCellCount, "Availability preview must clear after placement.");
            Assert.AreEqual(0, overlay.InvalidCellCount);
            Assert.IsNull(GameObject.Find("ModernOfficeChairSampleTilemap"));
            Capture("house-school-desk-placed.png");
            yield return ClickButton("SaveFurnitureLayoutButton");
            var saved = new SaveService(_slot).ReadJson(FurniturePlacementLayoutPersistence.GetHouseLayoutFileNameForStage(0));
            StringAssert.Contains("school.desk.with.open.book", saved);
            yield return ClickButton("ClearFurnitureLayoutButton");
            Assert.IsNull(objects.GetTile(target));
            yield return ClickButton("LoadFurnitureLayoutButton");
            Assert.AreEqual(tileName, objects.GetTile(target)?.name);
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return new WaitForSeconds(.4f);
            objects = GameObject.Find("HouseFurnitureObjectTilemap").GetComponent<Tilemap>();
            occupancy = GameObject.Find("HouseFurnitureOccupancyTilemap").GetComponent<Tilemap>();
            Assert.AreEqual(tileName, objects.GetTile(target)?.name, "Saved placement must survive scene reload.");
            Assert.IsNotNull(occupancy.GetTile(target + Vector3Int.right));
            Capture("house-school-desk-reloaded.png");
            File.WriteAllText(Path.Combine(Evidence, "house-placement.txt"),
                "input=MouseState press/release through EventSystem and overlay Update; no placement method invoked\n" +
                "paletteSearchInput=" + _searchInputRoute + "\nslot=" + _slot + "\nselected=" + buttonName + "\ntile=" + objects.GetTile(target).name + "\ncell=" + target +
                "\noccupancy=" + target + ";" + (target + Vector3Int.right) + "\nsaveClearLoadAndReload=passed\n" + saved);
        }

        private IEnumerator WalkTo(PlayerController player, Key key, float target, bool horizontal)
        {
            float sign = key == Key.A || key == Key.S ? -1 : 1;
            float deadline = UnityEngine.Time.realtimeSinceStartup + 4f;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(key));
            InputSystem.Update();
            while (sign * (target - (horizontal ? player.transform.position.x : player.transform.position.y)) > .03f)
            {
                if (UnityEngine.Time.realtimeSinceStartup > deadline) break;
                yield return new WaitForFixedUpdate();
            }
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            InputSystem.Update();
            yield return null;
            yield return new WaitForFixedUpdate();
            Assert.Less(Mathf.Abs(target - (horizontal ? player.transform.position.x : player.transform.position.y)), .15f,
                "Keyboard route blocked at " + player.transform.position + ", target=" + target + ", key=" + key);
        }

        private IEnumerator Hold(Key key, float seconds)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(key));
            InputSystem.Update();
            yield return new WaitForSeconds(seconds);
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            InputSystem.Update();
            yield return null;
            yield return new WaitForFixedUpdate();
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
            Assert.IsNotNull(go, name);
            yield return Click(RectTransformUtility.WorldToScreenPoint(null, go.GetComponent<RectTransform>().position));
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
            foreach (var p in map.cellBounds.allPositionsWithin)
            {
                var tile = map.GetTile<Tile>(p);
                if (tile == null || tile.color.g < .8f || tile.color.r > .3f) continue;
                var screen = camera.WorldToScreenPoint(map.GetCellCenterWorld(p));
                if (screen.z <= 0 || screen.x < Screen.width*.28f || screen.x > Screen.width*.7f || screen.y < Screen.height*.25f || screen.y > Screen.height*.75f) continue;
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screen }, hits);
                if (hits.Count > 0) continue;
                cell = p; pointer = screen; return true;
            }
            cell = default; pointer = default; return false;
        }

        private static void Capture(string filename)
        {
            var camera = Camera.main;
            Assert.IsNotNull(camera);
            var target = new RenderTexture(1024, 768, 24);
            var previous = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var texture = new Texture2D(1024, 768, TextureFormat.RGBA32, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1024, 768), 0, 0);
                texture.Apply();
                File.WriteAllBytes(Path.Combine(Evidence, filename), texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous;
                RenderTexture.active = previousActive;
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(target);
            }
        }
    }
}
