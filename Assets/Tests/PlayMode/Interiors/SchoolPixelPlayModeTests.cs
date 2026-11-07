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
                // The SchoolPixels label has grown since the first 58 imports; the label must still hold at least those.
                Assert.GreaterOrEqual(pack.Result.Select(a => a.name).Distinct().Count(), 58);
                Assert.GreaterOrEqual(pack.Result.Where(a => a is Sprite || a is Texture2D).Select(a => a.name).Distinct().Count(), 36);
                Assert.GreaterOrEqual(pack.Result.OfType<FurnitureDefinition>().Count(), 11);
                Assert.GreaterOrEqual(pack.Result.OfType<Tile>().Count(), 11);
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
