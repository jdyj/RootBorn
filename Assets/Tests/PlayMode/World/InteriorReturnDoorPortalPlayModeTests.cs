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
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.World
{
    public sealed class InteriorReturnDoorPortalPlayModeTests : Rootborn.Tests.PlayMode.PlayModeInputFixture
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SaveService.SetRootDirectoryForTests(Path.Combine(Application.temporaryCachePath, "FarmerDoorQa", System.Guid.NewGuid().ToString("N")));
            ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = "interior-return-door-test-slot", DisplayName = "Interior Return Door Test", WorldSeed = 2026052401, TileSeed = 2026052402 });
            yield return LoadScene("Town");
            yield return WaitForObjectWithPortal("TownLdtkDoor_house_18_12", 20f);
            yield return WaitForObjectWithPortal("TownLdtkDoor_kitchen_24_54", 20f);
        }

        [UnityTearDown]
        public new IEnumerator TearDown()
        {
            ActiveSaveContext.Clear();
            yield return null;
            SaveService.SetRootDirectoryForTests(null);
        }

        [UnityTest]
        public IEnumerator TOWN_DOOR_PM_005_PlayerWalksIntoHouseThenReturnsToTownThroughInteriorLdtkDoor()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var player = GameObject.Find("Player");
            Assert.IsNotNull(player?.GetComponent<PlayerController>(), "Town must expose a controllable player.");
            var houseDoor = GameObject.Find("TownLdtkDoor_house_18_12");
            Assert.IsNotNull(houseDoor, "Town LDtk house door must exist.");

            player.transform.position = houseDoor.transform.position + new Vector3(0f, -1.2f, 0f);
            Physics2D.SyncTransforms();
            yield return null;
            yield return WalkPlayerWithKeyboardUntilSceneChangesOrNear(player, keyboard, houseDoor.transform.position, 0.25f, 160, "HouseInterior", "Player must enter the LDtk house door through movement input.");
            yield return WaitForScene("HouseInterior", 10f);
            yield return null;

            player = GameObject.Find("Player");
            Assert.IsNotNull(player?.GetComponent<PlayerController>(), "Player must persist into HouseInterior.");
            var returnDoor = GameObject.Find("InteriorLdtkDoor_town_10_28");
            Assert.IsNotNull(returnDoor, "House_1 LDtk Door Where='town' must create an interior return portal.");
            var portal = returnDoor.GetComponent<WorldPortal>();
            Assert.IsNotNull(portal, "Interior return door must use WorldPortal.");
            Assert.AreEqual("Town", portal.DestinationScene);
            Assert.AreEqual("town-entry", portal.DestinationSpawnId);

            player.transform.position = returnDoor.transform.position + new Vector3(0f, -1.0f, 0f);
            Physics2D.SyncTransforms();
            yield return null;
            yield return WalkPlayerWithKeyboardUntilSceneChangesOrNear(player, keyboard, returnDoor.transform.position, 0.25f, 160, "Town", "Player must leave HouseInterior through the LDtk return door using movement input.");
            yield return WaitForScene("Town", 20f);
            Assert.AreEqual("Town", SceneManager.GetActiveScene().name);
            Assert.IsNotNull(GameObject.Find("Player"), "Player must persist back to Town.");
            AssertCleanTownReturnView();
            string screenshotPath = Path.Combine(Application.temporaryCachePath, "HouseInteriorReturnToTownVisual.png");
            if (File.Exists(screenshotPath)) File.Delete(screenshotPath);
            ScreenCapture.CaptureScreenshot(screenshotPath);
            float screenshotWait = 0f;
            while (!File.Exists(screenshotPath) && screenshotWait < 3f)
            {
                screenshotWait += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsTrue(File.Exists(screenshotPath), "Return-to-Town Game View screenshot was not captured: " + screenshotPath);
        }


        [UnityTest]
        public IEnumerator TOWN_DOOR_PM_006_PlayerWalksIntoShopThenReturnsToTownThroughInteriorLdtkDoor()
        {
            yield return EnterInteriorThenReturnToTown("TownLdtkDoor_shopping_center_61_10", "ShopInterior", "InteriorLdtkDoor_town_18_2", "ShopInteriorReturnToTownVisual.png");
        }

        [UnityTest]
        public IEnumerator TOWN_DOOR_PM_007_PlayerWalksIntoHospitalThenReturnsToTownThroughInteriorLdtkDoorWhenPresent()
        {
            yield return EnterInteriorThenReturnToTown("TownLdtkDoor_hospital_49_41", "HospitalInterior", "InteriorLdtkDoor_town_6_28", "HospitalInteriorReturnToTownVisual.png");
        }


        [UnityTest]
        public IEnumerator TOWN_DOOR_PM_009_PlayerWalksIntoPoliceThenUsesInternalPoliceDoor()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var player = GameObject.Find("Player");
            Assert.IsNotNull(player?.GetComponent<PlayerController>(), "Town must expose a controllable player." );
            var policeDoor = GameObject.Find("TownLdtkDoor_police_24_38");
            Assert.IsNotNull(policeDoor, "Town LDtk police door must exist." );

            player.transform.position = policeDoor.transform.position + new Vector3(0f, -1.2f, 0f);
            Physics2D.SyncTransforms();
            yield return null;
            yield return WalkPlayerWithKeyboardUntilSceneChangesOrNear(player, keyboard, policeDoor.transform.position, 0.25f, 180, "PoliceInterior", "Player must enter the LDtk police door through movement input." );
            yield return WaitForScene("PoliceInterior", 10f);
            yield return null;
            AssertInteriorCameraZoom("PoliceInterior");

            player = GameObject.Find("Player");
            Assert.IsNotNull(player?.GetComponent<PlayerController>(), "Player must persist into PoliceInterior." );
            var internalDoor = GameObject.Find("InteriorLdtkDoor_police_2_24_13");
            Assert.IsNotNull(internalDoor, "Police_1 LDtk Door Where='police_2' must create an internal police portal." );
            AssertScenePortalCount("PoliceInterior", 1, "Police_1 must only expose the populated LDtk police_2 door; empty police doors must stay inactive." );
            var portal = internalDoor.GetComponent<WorldPortal>();
            Assert.IsNotNull(portal, "Police internal door must use WorldPortal." );
            Assert.AreEqual("PoliceShootingInterior", portal.DestinationScene);
            Assert.AreEqual("police-2-entry", portal.DestinationSpawnId);

            player.transform.position = internalDoor.transform.position + new Vector3(0f, -1.0f, 0f);
            Physics2D.SyncTransforms();
            yield return null;
            yield return WalkPlayerWithKeyboardUntilSceneChangesOrNear(player, keyboard, internalDoor.transform.position, 0.25f, 180, "PoliceShootingInterior", "Player must enter PoliceShootingInterior through the LDtk internal police door." );
            yield return WaitForScene("PoliceShootingInterior", 10f);
            Assert.AreEqual("PoliceShootingInterior", SceneManager.GetActiveScene().name);
            Assert.IsNotNull(GameObject.Find("Police_2"), "PoliceShootingInterior must expose the source LDtk level marker." );
            AssertInteriorCameraZoom("PoliceShootingInterior");
            CaptureScreenshot("PoliceShootingInteriorDoorEntryVisual.png");
            yield return WaitForScreenshot("PoliceShootingInteriorDoorEntryVisual.png", 3f);
        }


        [UnityTest]
        public IEnumerator TOWN_DOOR_PM_011_PlayerWalksIntoKitchenThenUsesKitchen2DoorAndReturnsToKitchen()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var player = GameObject.Find("Player");
            Assert.IsNotNull(player?.GetComponent<PlayerController>(), "Town must expose a controllable player." );
            var kitchenDoor = GameObject.Find("TownLdtkDoor_kitchen_24_54");
            Assert.IsNotNull(kitchenDoor, "Town LDtk kitchen door must exist." );

            player.transform.position = kitchenDoor.transform.position + new Vector3(0f, -1.2f, 0f);
            Physics2D.SyncTransforms();
            yield return null;
            yield return WalkPlayerWithKeyboardUntilSceneChangesOrNear(player, keyboard, kitchenDoor.transform.position, 0.25f, 180, "KitchenInterior", "Player must enter the LDtk kitchen door through movement input." );
            yield return WaitForScene("KitchenInterior", 10f);
            yield return null;
            AssertInteriorCameraZoom("KitchenInterior");

            player = GameObject.Find("Player");
            Assert.IsNotNull(player?.GetComponent<PlayerController>(), "Player must persist into KitchenInterior." );
            var internalDoor = GameObject.Find("InteriorLdtkDoor_kitchen2_0_4");
            Assert.IsNotNull(internalDoor, "Kitchen LDtk Door Where='kitchen2' must create a Kitchen2Interior portal." );
            var portal = internalDoor.GetComponent<WorldPortal>();
            Assert.IsNotNull(portal, "Kitchen internal door must use WorldPortal." );
            Assert.AreEqual("Kitchen2Interior", portal.DestinationScene);
            Assert.AreEqual("kitchen-2-entry", portal.DestinationSpawnId);

            player.transform.position = internalDoor.transform.position + new Vector3(1.0f, 0f, 0f);
            Physics2D.SyncTransforms();
            yield return null;
            yield return WalkPlayerWithKeyboardUntilSceneChangesOrNear(player, keyboard, internalDoor.transform.position, 0.25f, 180, "Kitchen2Interior", "Player must enter Kitchen2Interior through the LDtk kitchen2 door." );
            yield return WaitForScene("Kitchen2Interior", 10f);
            yield return null;
            Assert.AreEqual("Kitchen2Interior", SceneManager.GetActiveScene().name);
            Assert.IsNotNull(GameObject.Find("Kitchen2"), "Kitchen2Interior must expose the source LDtk level marker." );
            AssertInteriorCameraZoom("Kitchen2Interior");

            player = GameObject.Find("Player");
            Assert.IsNotNull(player?.GetComponent<PlayerController>(), "Player must persist into Kitchen2Interior." );
            var returnDoor = GameObject.Find("InteriorLdtkDoor_kitchen1_24_4");
            Assert.IsNotNull(returnDoor, "Kitchen2 LDtk Door Where='kitchen1' must create a return portal to KitchenInterior." );
            portal = returnDoor.GetComponent<WorldPortal>();
            Assert.IsNotNull(portal, "Kitchen2 return door must use WorldPortal." );
            Assert.AreEqual("KitchenInterior", portal.DestinationScene);
            Assert.AreEqual("kitchen-entry", portal.DestinationSpawnId);
            CaptureScreenshot("Kitchen2InteriorDoorEntryVisual.png");
            yield return WaitForScreenshot("Kitchen2InteriorDoorEntryVisual.png", 3f);
        }

        private static IEnumerator EnterInteriorThenReturnToTown(string townDoorName, string destinationScene, string expectedReturnDoorName, string screenshotName)
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var player = GameObject.Find("Player");
            Assert.IsNotNull(player?.GetComponent<PlayerController>(), "Town must expose a controllable player.");
            var townDoor = GameObject.Find(townDoorName);
            Assert.IsNotNull(townDoor, townDoorName + " must exist.");

            player.transform.position = townDoor.transform.position + new Vector3(0f, -1.2f, 0f);
            Physics2D.SyncTransforms();
            yield return null;
            yield return WalkPlayerWithKeyboardUntilSceneChangesOrNear(player, keyboard, townDoor.transform.position, 0.25f, 180, destinationScene, "Player must enter " + townDoorName + " through movement input.");
            yield return WaitForScene(destinationScene, 10f);
            yield return null;
            AssertInteriorCameraZoom(destinationScene);

            if (string.IsNullOrEmpty(expectedReturnDoorName))
            {
                Assert.IsNull(FindFirstInteriorReturnDoor(), destinationScene + " must not create a return portal until its LDtk Door layer contains a town/outside/self Door entity.");
                yield break;
            }

            player = GameObject.Find("Player");
            Assert.IsNotNull(player?.GetComponent<PlayerController>(), "Player must persist into " + destinationScene + ".");
            var returnDoor = GameObject.Find(expectedReturnDoorName);
            Assert.IsNotNull(returnDoor, expectedReturnDoorName + " must be generated from the interior LDtk Door entity.");
            var portal = returnDoor.GetComponent<WorldPortal>();
            Assert.IsNotNull(portal, expectedReturnDoorName + " must use WorldPortal.");
            Assert.AreEqual("Town", portal.DestinationScene);
            Assert.AreEqual("town-entry", portal.DestinationSpawnId);

            player.transform.position = returnDoor.transform.position + new Vector3(0f, -1.0f, 0f);
            Physics2D.SyncTransforms();
            yield return null;
            yield return WalkPlayerWithKeyboardUntilSceneChangesOrNear(player, keyboard, returnDoor.transform.position, 0.25f, 180, "Town", "Player must leave " + destinationScene + " through the LDtk return door using movement input.");
            yield return WaitForScene("Town", 20f);
            Assert.AreEqual("Town", SceneManager.GetActiveScene().name);
            Assert.IsNotNull(GameObject.Find("Player"), "Player must persist back to Town.");
            AssertCleanTownReturnView();
            CaptureScreenshot(screenshotName);
            yield return WaitForScreenshot(screenshotName, 3f);
        }

        private static void AssertCleanTownReturnView()
                {
                    AssertInactiveOrMissing("QuestChainLogOpenButton");
            AssertInactiveOrMissing("LocationStateLogButton");
                    AssertInactiveOrMissing("StudentDayResultRoot");
                    AssertInactiveOrMissing("BookPanel");
                    AssertInactiveOrMissing("QuestLogPanel");
                    AssertInactiveOrMissing("ObjectiveJournalPanel");
                    AssertInactiveOrMissing("ObjectiveJournalRoot");
                    AssertInactiveOrMissing("WorldStateLogPanel");
                    AssertInactiveOrMissing("WorldStateLogRoot");
        
                    var player = GameObject.Find("Player");
                    Assert.IsNotNull(player, "Town return view must contain Player.");
                    Assert.IsNotNull(Camera.main, "Town return view must contain Main Camera.");
                    Vector2 cameraDelta = Camera.main.transform.position - player.transform.position;
                    Assert.LessOrEqual(cameraDelta.magnitude, 1.5f, "Town return camera should be snapped near the returned player instead of easing from the previous scene. delta=" + cameraDelta);
        
                    var groundObject = GameObject.Find("Town2LdtkGroundTilemap");
                    Assert.IsNotNull(groundObject, "Town return view must contain the LDtk ground tilemap.");
                    var ground = groundObject.GetComponent<Tilemap>();
                    Assert.IsNotNull(ground, "Town2LdtkGroundTilemap must be a Tilemap.");
                    var playerCell = ground.WorldToCell(player.transform.position);
                    Assert.IsNotNull(ground.GetTile(playerCell), "Returned player should be framed on a non-empty Town ground tile. cell=" + playerCell + " position=" + player.transform.position);
                }
        
                private static void AssertScenePortalCount(string sceneName, int expectedCount, string message)
                {
                    int count = 0;
                    var portals = Object.FindObjectsByType<TownAutoDoorPortal>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    for (int i = 0; i < portals.Length; i++)
                    {
                        if (portals[i] != null && portals[i].gameObject.scene.name == sceneName)
                        {
                            count++;
                        }
                    }

                    Assert.AreEqual(expectedCount, count, message);
                }

                private static void AssertInactiveOrMissing(string objectName)
                {
                    var all = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    for (int i = 0; i < all.Length; i++)
                    {
                        var go = all[i];
                        if (go != null && go.name == objectName)
                        {
                            Assert.IsFalse(go.activeInHierarchy, objectName + " must not be visible in the player-facing Town return view.");
                        }
                    }
                }
        
                private static GameObject FindFirstInteriorReturnDoor()
        {
            var objects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i].name.StartsWith("InteriorLdtkDoor_", System.StringComparison.Ordinal)) return objects[i];
            }
            return null;
        }

        private static void AssertInteriorCameraZoom(string sceneName)
        {
            Assert.IsNotNull(Camera.main, sceneName + " must have a Main Camera.");
            Assert.AreEqual(6.5f, Camera.main.orthographicSize, 0.01f, sceneName + " camera must use the interior zoom.");
        }

        private static void CaptureScreenshot(string screenshotName)
        {
            string screenshotPath = Path.Combine(Application.temporaryCachePath, screenshotName);
            if (File.Exists(screenshotPath)) File.Delete(screenshotPath);
            ScreenCapture.CaptureScreenshot(screenshotPath);
        }

        private static IEnumerator WaitForScreenshot(string screenshotName, float timeoutSeconds)
        {
            string screenshotPath = Path.Combine(Application.temporaryCachePath, screenshotName);
            float screenshotWait = 0f;
            while (!File.Exists(screenshotPath) && screenshotWait < timeoutSeconds)
            {
                screenshotWait += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsTrue(File.Exists(screenshotPath), "Game View screenshot was not captured: " + screenshotPath);
        }
        private static IEnumerator LoadScene(string sceneName)
        {
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            Assert.IsNotNull(op, sceneName + " should be present in BuildSettings.");
            float deadline = Time.realtimeSinceStartup + 20f;
            while (!op.isDone && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(op.isDone, sceneName + " scene load timed out.");
        }

        private static IEnumerator WaitForObjectWithPortal(string objectName, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                var go = GameObject.Find(objectName);
                if (go != null && go.GetComponent<WorldPortal>() != null) yield break;
                yield return null;
            }
            Assert.Fail(objectName + " did not become portal-ready.");
        }

        private static IEnumerator WaitForScene(string sceneName, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (SceneManager.GetActiveScene().name != sceneName && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(sceneName, SceneManager.GetActiveScene().name);
        }

        private static IEnumerator WalkPlayerWithKeyboardUntilSceneChangesOrNear(GameObject player, Keyboard keyboard, Vector3 targetPosition, float tolerance, int maxFrames, string destinationScene, string failure)
        {
            for (int i = 0; i < maxFrames; i++)
            {
                if (SceneManager.GetActiveScene().name == destinationScene) yield break;
                if (player == null || Vector3.Distance(player.transform.position, targetPosition) <= tolerance) yield break;
                Vector3 delta = targetPosition - player.transform.position;
                Key key = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y) ? (delta.x >= 0f ? Key.D : Key.A) : (delta.y >= 0f ? Key.W : Key.S);
                QueueKey(keyboard, key);
                InputSystem.Update();
                yield return null;
                yield return new WaitForFixedUpdate();
            }
            ReleaseMovementKeys(keyboard);
            InputSystem.Update();
            Assert.IsTrue(SceneManager.GetActiveScene().name == destinationScene || (player != null && Vector3.Distance(player.transform.position, targetPosition) <= tolerance), failure);
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
    }
}
