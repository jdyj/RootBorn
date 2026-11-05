using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.Game.World;
using Rootborn.UI.MainMenu;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    public sealed class BootToPoliceInteriorE2ETests : Rootborn.Tests.PlayMode.PlayModeInputFixture
    {
        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            ActiveSaveContext.Clear();
            GameDataRegistryProvider.ResetCacheForTests();
            _saveRoot = Path.Combine(Application.temporaryCachePath, "boot-police-e2e", System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveRoot);
            SaveSlotSelectPanel.SetSaveRootForTests(_saveRoot);
            SaveService.SetRootDirectoryForTests(_saveRoot);
            InputSystemUiModulePlayModeTestGuard.UseRealInputSystemForCurrentTest();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            ActiveSaveContext.Clear();
            SaveSlotSelectPanel.SetSaveRootForTests(null);
            SaveService.SetRootDirectoryForTests(null);
            GameDataRegistryProvider.ResetCacheForTests();
            yield return null;
        }

        [UnityTest]
        public IEnumerator BOOT_FLOW_PM_001_NewGameFromBootCanEnterPoliceInteriorDoor()
        {
            yield return LoadScene("Boot");
            yield return WaitForScene("MainMenu", 10f);
            yield return WaitForRealInputEventSystem(5f);
            yield return ClickActiveButton("SinglePlayButton", 5f);
            yield return WaitForSaveSlotPanel(5f);
            yield return ClickActiveButton("NewGameButton", 5f);
            yield return ClickActiveButton("ConfirmButton", 5f);
            yield return WaitForScene("Town", 20f);
            yield return WaitForTownReady(15f);

            var player = GameObject.Find("Player");
            Assert.IsNotNull(player?.GetComponent<PlayerController>(), "Boot New Game flow must create a controllable Player in Town." );
            var policeDoor = GameObject.Find("TownLdtkDoor_police_24_38");
            Assert.IsNotNull(policeDoor, "Boot New Game Town must expose the LDtk police door portal." );
            var portal = policeDoor.GetComponent<WorldPortal>();
            Assert.IsNotNull(portal, "Police LDtk door must be a WorldPortal after Boot New Game flow." );
            Assert.AreEqual("PoliceInterior", portal.DestinationScene);
            Assert.AreEqual("police-entry", portal.DestinationSpawnId);

            player.transform.position = policeDoor.transform.position + new Vector3(0f, -1.2f, 0f);
            Physics2D.SyncTransforms();
            yield return null;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return WalkPlayerWithKeyboardUntilSceneChanges(player, keyboard, policeDoor.transform.position, "PoliceInterior", 180);
            InputSystem.RemoveDevice(keyboard);

            yield return WaitForScene("PoliceInterior", 10f);
            Assert.IsNotNull(GameObject.Find("Police_1"), "PoliceInterior must expose the LDtk Police_1 level marker." );
            Assert.IsNotNull(GameObject.Find("PoliceInteriorLdtkTilemaps"), "PoliceInterior must render saved LDtk Tilemap layers." );
            Assert.IsNotNull(GameObject.Find("InteriorLdtkDoor_police_2_24_13"), "PoliceInterior must expose the internal police_2 door." );
            Assert.AreEqual(6.5f, Camera.main.orthographicSize, 0.01f, "Interior camera zoom must stay at 6.5 after Boot-origin flow." );
            yield return CaptureScreenshot("BootToPoliceInteriorVisual.png");
        }

        private static IEnumerator LoadScene(string sceneName)
        {
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            Assert.IsNotNull(op, sceneName + " must be present in BuildSettings." );
            while (!op.isDone) yield return null;
        }

        private static IEnumerator WaitForScene(string sceneName, float timeoutSeconds)
        {
            float elapsed = 0f;
            while (SceneManager.GetActiveScene().name != sceneName && elapsed < timeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.AreEqual(sceneName, SceneManager.GetActiveScene().name);
        }

        private static IEnumerator WaitForTownReady(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var player = GameObject.Find("Player");
                if (player != null &&
                    player.GetComponent<PlayerController>() != null &&
                    player.GetComponent<StudentLifeProgressComponent>() != null &&
                    GameObject.Find("Town2LdtkTilemaps") != null &&
                    GameObject.Find("TownLdtkDoor_police_24_38")?.GetComponent<WorldPortal>() != null)
                {
                    yield break;
                }
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.Fail("Boot New Game Town runtime did not expose Player, LDtk tilemaps, and police door portal within timeout." );
        }

        private static IEnumerator WaitForRealInputEventSystem(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var eventSystem = Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
                if (eventSystem != null && eventSystem.GetComponent<InputSystemUIInputModule>() != null) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.Fail("A player-facing EventSystem with InputSystemUIInputModule was not available." );
        }

        private static IEnumerator WaitForSaveSlotPanel(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var panel = Object.FindFirstObjectByType<SaveSlotSelectPanel>(FindObjectsInactive.Include);
                var root = GameObject.Find("SaveSlotSelectRoot");
                if (panel != null && root != null && root.activeInHierarchy) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.Fail("Single Play pointer click did not open SaveSlotSelectPanel." );
        }

        private IEnumerator ClickActiveButton(string buttonName, float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var go = GameObject.Find(buttonName);
                var button = go != null ? go.GetComponent<Button>() : null;
                if (button != null && button.gameObject.activeInHierarchy && button.interactable)
                {
                    var eventSystem = Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
                    Assert.IsNotNull(eventSystem, "A real EventSystem is required for player-facing pointer clicks." );
                    Assert.IsNotNull(eventSystem.GetComponent<InputSystemUIInputModule>(), "Pointer flow must use InputSystemUIInputModule." );
                    if (TryFindRaycastPointFor(button.gameObject, eventSystem, out Vector2 clickPoint))
                    {
                        yield return ClickWithMouse(clickPoint);
                        yield break;
                    }
                }
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.Fail("Expected active interactable button named " + buttonName + "." );
        }

        private static bool TryFindRaycastPointFor(GameObject target, EventSystem eventSystem, out Vector2 screenPoint)
        {
            var results = new List<RaycastResult>();
            if (target.TryGetComponent(out RectTransform rect))
            {
                var corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                for (int yStep = 1; yStep <= 3; yStep++)
                {
                    for (int xStep = 1; xStep <= 3; xStep++)
                    {
                        var world = Vector3.Lerp(Vector3.Lerp(corners[0], corners[3], xStep / 4f), Vector3.Lerp(corners[1], corners[2], xStep / 4f), yStep / 4f);
                        var point = RectTransformUtility.WorldToScreenPoint(null, world);
                        if (RaycastsToTarget(point, target, eventSystem, results))
                        {
                            screenPoint = point;
                            return true;
                        }
                    }
                }
            }
            screenPoint = Vector2.zero;
            return false;
        }

        private static bool RaycastsToTarget(Vector2 point, GameObject target, EventSystem eventSystem, List<RaycastResult> results)
        {
            var eventData = new PointerEventData(eventSystem) { position = point };
            results.Clear();
            eventSystem.RaycastAll(eventData, results);
            for (int i = 0; i < results.Count; i++)
            {
                var hit = results[i].gameObject;
                if (hit == target || hit.transform.IsChildOf(target.transform)) return true;
            }
            return false;
        }

        private static IEnumerator ClickWithMouse(Vector2 screenPoint)
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
            var driver = new GameObject("BootPolicePointerClickDriver").AddComponent<MouseClickFrameDriver>();
            driver.Configure(mouse, screenPoint);
            yield return null;
            yield return null;
            yield return null;
            InputSystem.RemoveDevice(mouse);
        }

        private IEnumerator WalkPlayerWithKeyboardUntilSceneChanges(GameObject player, Keyboard keyboard, Vector3 targetPosition, string destinationScene, int maxFrames)
        {
            for (int i = 0; i < maxFrames; i++)
            {
                if (SceneManager.GetActiveScene().name == destinationScene) yield break;
                Vector3 direction = targetPosition - player.transform.position;
                Key key = Mathf.Abs(direction.x) > Mathf.Abs(direction.y)
                    ? (direction.x > 0f ? Key.D : Key.A)
                    : (direction.y > 0f ? Key.W : Key.S);
                Press(keyboard[key]);
                yield return null;
                Release(keyboard[key]);
                yield return null;
            }
            Assert.Fail("Player did not enter " + destinationScene + " through police door movement input." );
        }

        private static IEnumerator CaptureScreenshot(string screenshotName)
        {
            string screenshotPath = Path.Combine(Application.temporaryCachePath, screenshotName);
            if (File.Exists(screenshotPath)) File.Delete(screenshotPath);
            ScreenCapture.CaptureScreenshot(screenshotPath);
            float elapsed = 0f;
            while (!File.Exists(screenshotPath) && elapsed < 3f)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsTrue(File.Exists(screenshotPath), "Game View screenshot was not captured: " + screenshotPath);
            Assert.Greater(new FileInfo(screenshotPath).Length, 0L, "Game View screenshot was empty: " + screenshotPath);
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
                    InputSystem.QueueStateEvent(_mouse, new MouseState { position = _screenPosition });
                    InputSystem.Update();
                    _frame++;
                    return;
                }
                if (_frame == 1)
                {
                    InputSystem.QueueStateEvent(_mouse, new MouseState { position = _screenPosition, buttons = 1 });
                    InputSystem.Update();
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
