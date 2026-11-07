using System.Collections;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Game.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

namespace Rootborn.Tests.PlayMode.World
{
    public sealed class TownDoorPortalPlayModeTests : Rootborn.Tests.PlayMode.PlayModeInputFixture
    {
        private static readonly ExpectedDoor[] ExpectedDoors =
        {
            new ExpectedDoor("TownLdtkDoor_house_18_12", "house", "House", "house-entry"),
            new ExpectedDoor("TownLdtkDoor_hospital_49_41", "hospital", "HospitalInterior", "hospital-entry"),
            new ExpectedDoor("TownLdtkDoor_shopping_center_61_10", "shopping center", "ShopInterior", "shop-entry"),
            new ExpectedDoor("TownLdtkDoor_police_24_38", "police", "PoliceInterior", "police-entry"),
            new ExpectedDoor("TownLdtkDoor_kitchen_24_54", "kitchen", "KitchenInterior", "kitchen-entry")
        };

        private static readonly string[] DisabledDoorNames =
        {
            "TownLdtkDoor_inactive_23_12",
            "TownLdtkDoor_inactive_28_11",
            "TownLdtkDoor_inactive_24_38"
        };

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SaveService.SetRootDirectoryForTests(Path.Combine(Application.temporaryCachePath, "FarmerDoorQa", System.Guid.NewGuid().ToString("N")));
            Debug.Log("TOWN_DOOR_TEST SetUp begin");
            ActiveSaveContext.Set(new SaveSlotMetadata
            {
                SlotId = "town-ldtk-door-portal-test-slot",
                DisplayName = "Town LDtk Door Portal Test",
                WorldSeed = 2026052301,
                TileSeed = 2026052302
            });
            yield return LoadScene("Town");
            yield return WaitForTownDoorRuntime(10f);
            Debug.Log("TOWN_DOOR_TEST SetUp ready scene=" + SceneManager.GetActiveScene().name);
        }

        [UnityTearDown]
        public new IEnumerator TearDown()
        {
            ActiveSaveContext.Clear();
            yield return null;
            SaveService.SetRootDirectoryForTests(null);
        }

        [UnityTest]
        public IEnumerator TOWN_DOOR_PM_001_LdtkDoorFieldsCreateOnlyActiveInteriorPortals()
        {
            var player = GameObject.Find("Player");
            Assert.IsNotNull(player, "Town must expose Player for LDtk door entry.");
            Assert.IsNotNull(player.GetComponent<PlayerInteractionRouter>(), "Town doors must use the existing player interaction router.");

            for (int i = 0; i < ExpectedDoors.Length; i++)
            {
                ExpectedDoor expected = ExpectedDoors[i];
                var portalObject = GameObject.Find(expected.ObjectName);
                Assert.IsNotNull(portalObject, expected.ObjectName + " must exist for the active LDtk Door='" + expected.DoorField + "' entity.");
                var portal = portalObject.GetComponent<WorldPortal>();
                Assert.IsNotNull(portal, expected.ObjectName + " must use WorldPortal.");
                Assert.IsTrue(portal.CanTravel, expected.ObjectName + " must be travel-ready.");
                Assert.AreEqual(expected.DestinationScene, portal.DestinationScene, expected.ObjectName + " must route by LDtk Door field.");
                Assert.AreEqual(expected.SpawnId, portal.DestinationSpawnId, expected.ObjectName + " must target the matching interior spawn.");
                Assert.IsNotNull(portalObject.GetComponent<TownAutoDoorPortal>(), expected.ObjectName + " must auto-enter when the player reaches the doorway.");
                var collider = portalObject.GetComponent<Collider2D>();
                Assert.IsNotNull(collider, expected.ObjectName + " must have a trigger collider.");
                Assert.IsTrue(collider.isTrigger, expected.ObjectName + " collider must be a trigger.");
            }

            for (int i = 0; i < DisabledDoorNames.Length; i++)
            {
                var disabled = GameObject.Find(DisabledDoorNames[i]);
                if (disabled == null)
                {
                    continue;
                }

                Assert.IsNull(disabled.GetComponent<WorldPortal>(), DisabledDoorNames[i] + " must not have a WorldPortal because its LDtk Door field is empty or unsupported.");
                var collider = disabled.GetComponent<Collider2D>();
                Assert.IsTrue(collider == null || !collider.enabled || !collider.isTrigger, DisabledDoorNames[i] + " must not be an active trigger.");
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator TOWN_DOOR_PM_002_PlayerWalksIntoHouseDoorAndSeesTheAuthoredHouse()
        {
            // The player's own house is the authored House scene now, not an LDtk interior level.
            var player = GameObject.Find("Player");
            var door = GameObject.Find("TownLdtkDoor_house_18_12");
            Assert.IsNotNull(player?.GetComponent<PlayerController>(), "Town must expose a controllable player.");
            Assert.IsNotNull(door, "The house door must exist before entering.");
            var keyboard = InputSystem.AddDevice<Keyboard>();
            player.transform.position = door.transform.position + new Vector3(0f, -1.2f, 0f);
            Physics2D.SyncTransforms();
            yield return null;
            yield return WalkPlayerWithKeyboardUntilSceneChangesOrNear(player, keyboard, door.transform.position, 0.25f, 160, "House", "Player must reach or trigger the house door using movement input.");
            ReleaseMovementKeys(keyboard);
            InputSystem.Update();
            yield return WaitForScene("House", 10f);
            yield return null;

            Assert.AreEqual("House", SceneManager.GetActiveScene().name, "The house door must load the House scene.");
            Assert.IsNotNull(GameObject.Find("Player"), "Player must persist into House.");
            var ground = GameObject.Find("HouseGroundTilemap")?.GetComponent<Tilemap>();
            Assert.IsNotNull(ground, "House must show its saved floor.");
            Assert.Greater(CountTiles(ground), 0, "House floor must render tiles.");
            Assert.IsNull(GameObject.Find("Town2LdtkTilemaps"), "House must not leak the Town LDtk root.");
        }

        [UnityTest]
        public IEnumerator TOWN_DOOR_PM_003_PlayerWalksIntoHospitalDoorAndSeesHospitalInteriorLdtkLevel()
        {
            yield return EnterDoorAndAssertInterior("TownLdtkDoor_hospital_49_41", "HospitalInterior", "HospitalInteriorLdtkTilemaps", "HospitalInteriorGroundTilemap", "Hospital_1", "HospitalInteriorDoorEntryVisual.png");
        }

        [UnityTest]
        public IEnumerator TOWN_DOOR_PM_004_PlayerWalksIntoShopDoorAndSeesShopInteriorLdtkLevel()
        {
            yield return EnterDoorAndAssertInterior("TownLdtkDoor_shopping_center_61_10", "ShopInterior", "ShopInteriorLdtkTilemaps", "ShopInteriorGroundTilemap", "Shop", "ShopInteriorDoorEntryVisual.png");
        }

        [UnityTest]
        public IEnumerator TOWN_DOOR_PM_008_PlayerWalksIntoPoliceDoorAndSeesPoliceInteriorLdtkLevel()
        {
            yield return EnterDoorAndAssertInterior("TownLdtkDoor_police_24_38", "PoliceInterior", "PoliceInteriorLdtkTilemaps", "PoliceInteriorGroundTilemap", "Police_1", "PoliceInteriorDoorEntryVisual.png");
        }

        [UnityTest]
        public IEnumerator TOWN_DOOR_PM_010_PlayerWalksIntoKitchenDoorAndSeesKitchenInteriorLdtkLevel()
        {
            yield return EnterDoorAndAssertInterior("TownLdtkDoor_kitchen_24_54", "KitchenInterior", "KitchenInteriorLdtkTilemaps", "KitchenInteriorGroundTilemap", "Level_14", "KitchenInteriorDoorEntryVisual.png");
        }

        private static IEnumerator EnterDoorAndAssertInterior(string doorName, string destinationScene, string rootName, string groundName, string expectedLevelName, string screenshotName)
        {
            var player = GameObject.Find("Player");
            Assert.IsNotNull(player?.GetComponent<PlayerController>(), "Town must expose a controllable player.");
            var door = GameObject.Find(doorName);
            Assert.IsNotNull(door, doorName + " must exist before entering.");

            var keyboard = InputSystem.AddDevice<Keyboard>();
            player.transform.position = door.transform.position + new Vector3(0f, -1.2f, 0f);
            Physics2D.SyncTransforms();
            yield return null;
            yield return WalkPlayerWithKeyboardUntilSceneChangesOrNear(player, keyboard, door.transform.position, 0.25f, 160, destinationScene, "Player must reach or trigger " + doorName + " using movement input.");
            Debug.Log("TOWN_DOOR_TEST Walk reached/triggered " + doorName + " activeScene=" + SceneManager.GetActiveScene().name + " " + DoorState(player, door));
            yield return WaitForScene(destinationScene, 10f);
            yield return null;
            yield return null;

            Assert.AreEqual(destinationScene, SceneManager.GetActiveScene().name, doorName + " must load the configured interior scene.");
            Assert.IsNotNull(GameObject.Find("Player"), "Player must persist into " + destinationScene + ".");
            Assert.IsNotNull(GameObject.Find(expectedLevelName), destinationScene + " must expose the source LDtk level marker.");
            var root = GameObject.Find(rootName);
            Assert.IsNotNull(root, destinationScene + " must expose saved LDtk Tilemap root " + rootName + ".");
            var groundTransform = root.transform.Find(groundName);
            Assert.IsNotNull(groundTransform, destinationScene + " must expose ground tilemap " + groundName + ".");
            var ground = groundTransform.GetComponent<Tilemap>();
            Assert.IsNotNull(ground, groundName + " must be a Tilemap.");
            Assert.Greater(CountTiles(ground), 0, groundName + " must render LDtk interior tiles.");
            AssertInteriorVisualState(destinationScene, root);
            AssertNoVisibleStaleObjects(destinationScene);
            AssertPlayerFacingDebugUiHidden(destinationScene);
            AssertInteriorCameraZoom(destinationScene);

            string screenshotPath = Path.Combine(Application.temporaryCachePath, screenshotName);
            if (File.Exists(screenshotPath)) File.Delete(screenshotPath);
            ScreenCapture.CaptureScreenshot(screenshotPath);
            float screenshotWait = 0f;
            while (!File.Exists(screenshotPath) && screenshotWait < 3f)
            {
                screenshotWait += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsTrue(File.Exists(screenshotPath), destinationScene + " Game View screenshot was not captured: " + screenshotPath);
        }

        private static void AssertInteriorVisualState(string sceneName, GameObject root)
        {
            if (sceneName == "ShopInterior")
            {
                AssertLayer(root, "ShopInteriorGroundTilemap", 149, 0);
                AssertLayer(root, "ShopInteriorWallTilemap", 48, 1);
                AssertLayer(root, "ShopInteriorWall3dTilemap", 0, 2);
                AssertLayer(root, "ShopInteriorForeGroundTilemap", 0, 3);
                AssertLayer(root, "ShopInteriorShopTilemap", 68, 4);
                AssertLayer(root, "ShopInteriorShopOverdraw01Tilemap", 20, 5);
                AssertLayer(root, "ShopInteriorShopOverdraw02Tilemap", 7, 6);
                AssertLayer(root, "ShopInteriorBorderTilemap", 42, 5);
                AssertLayer(root, "ShopInteriorBorderOverdraw01Tilemap", 3, 6);
                AssertLayer(root, "ShopInteriorBorderOverdraw02Tilemap", 1, 7);
                AssertLayer(root, "ShopInteriorHouse1Tilemap", 0, 6);
                AssertLayer(root, "ShopInteriorHouse2Tilemap", 0, 7);
                AssertLayer(root, "ShopInteriorPoliceTilemap", 0, 8);
                AssertLayer(root, "ShopInteriorShootingTilemap", 0, 9);
                AssertLayer(root, "ShopInteriorSchoolTilemap", 0, 10);
                AssertLayer(root, "ShopInteriorKitchenTilemap", 0, 11);
                return;
            }

            if (sceneName == "PoliceInterior")
            {
                AssertLayer(root, "PoliceInteriorGroundTilemap", 105, 0);
                AssertLayer(root, "PoliceInteriorWallTilemap", 39, 1);
                AssertLayer(root, "PoliceInteriorWall3dTilemap", 0, 2);
                AssertLayer(root, "PoliceInteriorForeGroundTilemap", 0, 3);
                AssertLayer(root, "PoliceInteriorShopTilemap", 0, 4);
                AssertLayer(root, "PoliceInteriorBorderTilemap", 39, 5);
                AssertLayer(root, "PoliceInteriorHouse1Tilemap", 0, 6);
                AssertLayer(root, "PoliceInteriorHouse2Tilemap", 0, 7);
                AssertLayer(root, "PoliceInteriorPoliceTilemap", 40, 8);
                AssertLayer(root, "PoliceInteriorPoliceOverdraw01Tilemap", 8, 9);
                AssertLayer(root, "PoliceInteriorPoliceOverdraw02Tilemap", 1, 10);
                AssertLayer(root, "PoliceInteriorShootingTilemap", 0, 9);
                AssertLayer(root, "PoliceInteriorSchoolTilemap", 0, 10);
                AssertLayer(root, "PoliceInteriorKitchenTilemap", 0, 11);
                return;
            }

            if (sceneName == "HospitalInterior")
            {
                AssertLayer(root, "HospitalInteriorGroundTilemap", 241, 0);
                AssertLayer(root, "HospitalInteriorWallTilemap", 0, 1);
                AssertLayer(root, "HospitalInteriorWall3dTilemap", 60, 2);
                AssertLayer(root, "HospitalInteriorForeGroundTilemap", 8, 3);
                AssertLayer(root, "HospitalInteriorForeGroundOverdraw01Tilemap", 2, 4);
                AssertLayer(root, "HospitalInteriorShopTilemap", 0, 4);
                AssertLayer(root, "HospitalInteriorBorderTilemap", 37, 5);
                AssertLayer(root, "HospitalInteriorHouse1Tilemap", 0, 6);
                AssertLayer(root, "HospitalInteriorHouse2Tilemap", 0, 7);
                AssertLayer(root, "HospitalInteriorPoliceTilemap", 0, 8);
                AssertLayer(root, "HospitalInteriorShootingTilemap", 0, 9);
                AssertLayer(root, "HospitalInteriorSchoolTilemap", 0, 10);
                AssertLayer(root, "HospitalInteriorKitchenTilemap", 0, 11);
                return;
            }

            if (sceneName == "KitchenInterior")
            {
                AssertLayer(root, "KitchenInteriorGroundTilemap", 167, 0);
                AssertLayer(root, "KitchenInteriorWallTilemap", 40, 1);
                AssertLayer(root, "KitchenInteriorWall3dTilemap", 0, 2);
                AssertLayer(root, "KitchenInteriorForeGroundTilemap", 0, 3);
                AssertLayer(root, "KitchenInteriorShopTilemap", 0, 4);
                AssertLayer(root, "KitchenInteriorBorderTilemap", 45, 5);
                AssertLayer(root, "KitchenInteriorBorderOverdraw01Tilemap", 3, 6);
                AssertLayer(root, "KitchenInteriorBorderOverdraw02Tilemap", 1, 7);
                AssertLayer(root, "KitchenInteriorHouse1Tilemap", 0, 6);
                AssertLayer(root, "KitchenInteriorHouse2Tilemap", 0, 7);
                AssertLayer(root, "KitchenInteriorPoliceTilemap", 0, 8);
                AssertLayer(root, "KitchenInteriorShootingTilemap", 0, 9);
                AssertLayer(root, "KitchenInteriorSchoolTilemap", 0, 10);
                AssertLayer(root, "KitchenInteriorKitchenTilemap", 63, 11);
                AssertLayer(root, "KitchenInteriorKitchenOverdraw01Tilemap", 20, 12);
                AssertLayer(root, "KitchenInteriorKitchenOverdraw02Tilemap", 5, 13);
                return;
            }

            Assert.Fail("Unexpected interior scene for LDtk visual assertion: " + sceneName);
        }

        private static void AssertLayer(GameObject root, string layerName, int expectedTileCount, int expectedSortingOrder)
        {
            Transform child = root.transform.Find(layerName);
            Assert.IsNotNull(child, root.name + " must contain LDtk layer " + layerName + "." );
            var tilemap = child.GetComponent<Tilemap>();
            Assert.IsNotNull(tilemap, layerName + " must have a Tilemap component." );
            Assert.AreEqual(expectedTileCount, CountTiles(tilemap), layerName + " must preserve its saved LDtk tile count in PlayMode." );
            var renderer = child.GetComponent<TilemapRenderer>();
            Assert.IsNotNull(renderer, layerName + " must have a TilemapRenderer." );
            Assert.IsTrue(renderer.enabled, layerName + " renderer must stay enabled in the player-facing view." );
            Assert.AreEqual(expectedSortingOrder, renderer.sortingOrder, layerName + " must preserve LDtk layer draw order in PlayMode." );
        }

        private static void AssertNoVisibleStaleObjects(string sceneName)
        {
            Assert.IsNull(GameObject.Find("Town2LdtkTilemaps"), sceneName + " must not leak the Town LDtk root into the interior view." );
            var objects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < objects.Length; i++)
            {
                string name = objects[i].name;
                if ((name.IndexOf("Sample", System.StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Placeholder", System.StringComparison.OrdinalIgnoreCase) >= 0) && objects[i].GetComponent<TilemapRenderer>() != null)
                {
                    Assert.Fail(sceneName + " must not show stale sample/debug TilemapRenderer object: " + name);
                }
            }
        }

        private static void AssertPlayerFacingDebugUiHidden(string sceneName)
        {
            // Gameplay entries remain reachable; utility entry tests cover their visibility.
            AssertInactiveOrMissing(sceneName, "LocationStateLogButton");
            AssertInactiveOrMissing(sceneName, "StudentDayResultRoot");
        }

        private static void AssertInactiveOrMissing(string sceneName, string objectName)
        {
            var objects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null && objects[i].name == objectName)
                {
                    Assert.IsFalse(objects[i].activeInHierarchy, sceneName + " must not show player-facing debug UI object " + objectName + "." );
                }
            }
        }

        private static void AssertInteriorCameraZoom(string sceneName)
        {
            Assert.IsNotNull(Camera.main, sceneName + " must have a Main Camera." );
            Assert.AreEqual(6.5f, Camera.main.orthographicSize, 0.01f, sceneName + " camera must use the interior zoom." );
        }

        private static IEnumerator WalkPlayerWithKeyboardUntilSceneChangesOrNear(GameObject player, Keyboard keyboard, Vector3 targetPosition, float tolerance, int maxFrames, string destinationScene, string failure)
        {
            for (int i = 0; i < maxFrames; i++)
            {
                if (SceneManager.GetActiveScene().name == destinationScene)
                {
                    yield break;
                }

                if (player == null || Vector3.Distance(player.transform.position, targetPosition) <= tolerance)
                {
                    yield break;
                }

                Vector3 delta = targetPosition - player.transform.position;
                Key key = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y) ? (delta.x >= 0f ? Key.D : Key.A) : (delta.y >= 0f ? Key.W : Key.S);
                QueueKey(keyboard, key);
                InputSystem.Update();
                yield return null;
                yield return new WaitForFixedUpdate();
            }

            ReleaseMovementKeys(keyboard);
            InputSystem.Update();
            Assert.IsTrue(SceneManager.GetActiveScene().name == destinationScene || (player != null && Vector3.Distance(player.transform.position, targetPosition) <= tolerance), failure + " player=" + (player != null ? player.transform.position.ToString("F2") : "none") + " target=" + targetPosition.ToString("F2") + " pendingTravel=" + Rootborn.Game.World.WorldTravelRuntime.HasPendingTravel);
        }

        private static string DoorState(GameObject player, GameObject door)
        {
            if (door == null) return "door=gone";
            var trigger = door.GetComponent<Collider2D>();
            var auto = door.GetComponent<TownAutoDoorPortal>();
            return "player=" + (player != null ? player.transform.position.ToString("F2") : "none")
                + " door=" + door.transform.position.ToString("F2")
                + " trigger=" + (trigger != null ? trigger.bounds.ToString() + " isTrigger=" + trigger.isTrigger + " enabled=" + trigger.enabled : "none")
                + " opening=" + (auto != null && auto.IsOpening)
                + " pendingTravel=" + Rootborn.Game.World.WorldTravelRuntime.HasPendingTravel;
        }

        private static void QueueKey(Keyboard keyboard, Key key)
        {
            if (key == Key.A) InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.A));
            else if (key == Key.D) InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.D));
            else if (key == Key.W) InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.W));
            else if (key == Key.S) InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.S));
            else Assert.Fail("Unsupported movement key: " + key);
        }

        private static void ReleaseMovementKeys(Keyboard keyboard)
        {
            InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
        }

        private static IEnumerator LoadScene(string sceneName)
        {
            Debug.Log("TOWN_DOOR_TEST LoadScene begin " + sceneName);
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            Assert.IsNotNull(op, sceneName + " should be present in BuildSettings.");
            float loadDeadline = Time.realtimeSinceStartup + 20f;
            while (!op.isDone && Time.realtimeSinceStartup < loadDeadline) yield return null;
            Assert.IsTrue(op.isDone, sceneName + " scene load timed out.");
            Debug.Log("TOWN_DOOR_TEST LoadScene done " + SceneManager.GetActiveScene().name);
        }

        private static IEnumerator WaitForTownDoorRuntime(float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                bool ready = GameObject.Find("Player")?.GetComponent<PlayerInteractionRouter>() != null;
                for (int i = 0; i < ExpectedDoors.Length && ready; i++)
                {
                    var portal = GameObject.Find(ExpectedDoors[i].ObjectName);
                    ready = portal != null && portal.GetComponent<WorldPortal>() != null;
                }

                if (ready)
                {
                    Debug.Log("TOWN_DOOR_TEST Runtime ready.");
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("Town runtime did not expose active LDtk door portals within timeout. scene=" + SceneManager.GetActiveScene().name);
        }

        private static IEnumerator WaitForScene(string sceneName, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (SceneManager.GetActiveScene().name != sceneName && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.AreEqual(sceneName, SceneManager.GetActiveScene().name);
        }

        private static int CountTiles(Tilemap tilemap)
        {
            int count = 0;
            foreach (Vector3Int cell in tilemap.cellBounds.allPositionsWithin)
            {
                if (tilemap.GetTile(cell) != null) count++;
            }
            return count;
        }

        private readonly struct ExpectedDoor
        {
            public readonly string ObjectName;
            public readonly string DoorField;
            public readonly string DestinationScene;
            public readonly string SpawnId;

            public ExpectedDoor(string objectName, string doorField, string destinationScene, string spawnId)
            {
                ObjectName = objectName;
                DoorField = doorField;
                DestinationScene = destinationScene;
                SpawnId = spawnId;
            }
        }
    }
}
