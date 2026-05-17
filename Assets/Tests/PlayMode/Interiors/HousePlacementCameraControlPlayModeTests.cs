using System.Collections;
using NUnit.Framework;
using Rootborn.UI.Interiors;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

namespace Rootborn.Tests.PlayMode.Interiors
{
    public sealed class HousePlacementCameraControlPlayModeTests
    {
        [UnityTest]
        public IEnumerator HouseScene_InstallsPlacementCameraControllerAndFramesAllTiles()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return WaitForHousePlacementCamera();

            var controller = Object.FindFirstObjectByType<HousePlacementCameraController>();
            var camera = Camera.main;
            var ground = GameObject.Find("HouseGroundTilemap")?.GetComponent<Tilemap>();

            Assert.IsNotNull(controller, "House placement mode should install a modular camera controller.");
            Assert.IsNotNull(camera, "House should have a Main Camera for placement control.");
            Assert.IsNotNull(ground, "House ground Tilemap should exist before framing all tiles.");
            Assert.LessOrEqual(ground.localBounds.size.x * 0.5f / camera.aspect, camera.orthographicSize + 0.01f, "Full view should fit the horizontal tile span.");
            Assert.LessOrEqual(ground.localBounds.size.y * 0.5f, camera.orthographicSize + 0.01f, "Full view should fit the vertical tile span.");
        }

        [UnityTest]
        public IEnumerator HousePlacementCamera_KeyboardZoomAndPanMoveCameraThroughPlayerInput()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return WaitForHousePlacementCamera();

            var camera = Camera.main;
            Assert.IsNotNull(camera);
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                var startSize = camera.orthographicSize;
                yield return DriveKeyboard(keyboard, Key.Minus, 6);
                Assert.Greater(camera.orthographicSize, startSize, "Minus key should zoom out during placement camera control.");

                camera.orthographicSize = 4f;
                camera.transform.position = new Vector3(0f, camera.transform.position.y, camera.transform.position.z);
                var startPosition = camera.transform.position;
                yield return DriveKeyboard(keyboard, Key.D, 6);
                Assert.AreNotEqual(startPosition.x, camera.transform.position.x, "D/right input should pan the placement camera horizontally after the user zooms into the room.");
            }
            finally
            {
                if (keyboard.added)
                {
                    InputSystem.RemoveDevice(keyboard);
                }
            }
        }

        [UnityTest]
        public IEnumerator HousePlacementCamera_RightMouseDragPansCameraThroughPlayerInput()
        {
            yield return SceneManager.LoadSceneAsync("House", LoadSceneMode.Single);
            yield return WaitForHousePlacementCamera();

            var camera = Camera.main;
            Assert.IsNotNull(camera);
            camera.orthographicSize = 4f;
            camera.transform.position = new Vector3(0f, 0f, camera.transform.position.z);

            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                var start = camera.transform.position;
                yield return DriveMouseDrag(mouse, new Vector2(600f, 400f), new Vector2(500f, 360f), 6);
                Assert.AreNotEqual(start, camera.transform.position, "Right mouse drag should pan the placement camera while zoomed in.");
            }
            finally
            {
                if (mouse.added)
                {
                    InputSystem.RemoveDevice(mouse);
                }
            }
        }
        private static IEnumerator DriveKeyboard(Keyboard keyboard, Key key, int frames)
        {
            var driver = new GameObject("PlacementCameraKeyboardInputDriver").AddComponent<KeyboardCameraInputDriver>();
            driver.Configure(keyboard, key, frames);
            for (int i = 0; i < frames + 3; i++)
            {
                yield return null;
            }

            if (driver != null)
            {
                Object.Destroy(driver.gameObject);
            }
        }

        private static IEnumerator DriveMouseDrag(Mouse mouse, Vector2 from, Vector2 to, int frames)
        {
            var driver = new GameObject("PlacementCameraMouseDragInputDriver").AddComponent<MouseCameraInputDriver>();
            driver.Configure(mouse, from, to, frames);
            for (int i = 0; i < frames + 3; i++)
            {
                yield return null;
            }

            if (driver != null)
            {
                Object.Destroy(driver.gameObject);
            }
        }
        private static IEnumerator WaitForHousePlacementCamera()
        {
            for (int i = 0; i < 120; i++)
            {
                if (Object.FindFirstObjectByType<HousePlacementCameraController>() != null && Camera.main != null)
                {
                    for (int stable = 0; stable < 5; stable++)
                    {
                        yield return null;
                    }

                    yield break;
                }

                yield return null;
            }
        }

        [DefaultExecutionOrder(-10000)]
        private sealed class MouseCameraInputDriver : MonoBehaviour
        {
            private Mouse _mouse;
            private Vector2 _from;
            private Vector2 _to;
            private int _frames;
            private int _frame;

            public void Configure(Mouse mouse, Vector2 from, Vector2 to, int frames)
            {
                _mouse = mouse;
                _from = from;
                _to = to;
                _frames = Mathf.Max(1, frames);
            }

            private void Update()
            {
                if (_mouse == null || !_mouse.added)
                {
                    Destroy(gameObject);
                    return;
                }

                _mouse.MakeCurrent();
                if (_frame < _frames)
                {
                    float t = _frames <= 1 ? 1f : _frame / (float)(_frames - 1);
                    var position = Vector2.Lerp(_from, _to, t);
                    InputSystem.QueueStateEvent(_mouse, new MouseState
                    {
                        position = position,
                        buttons = (ushort)(1u << (int)MouseButton.Right)
                    });
                    InputSystem.Update();
                    _frame++;
                    return;
                }

                InputSystem.QueueStateEvent(_mouse, new MouseState { position = _to });
                InputSystem.Update();
                Destroy(gameObject);
            }
        }
        [DefaultExecutionOrder(-10000)]
        private sealed class KeyboardCameraInputDriver : MonoBehaviour
        {
            private Keyboard _keyboard;
            private Key _key;
            private int _frames;
            private int _frame;

            public void Configure(Keyboard keyboard, Key key, int frames)
            {
                _keyboard = keyboard;
                _key = key;
                _frames = frames;
            }

            private void Update()
            {
                if (_keyboard == null || !_keyboard.added)
                {
                    Destroy(gameObject);
                    return;
                }

                _keyboard.MakeCurrent();
                if (_frame < _frames)
                {
                    InputSystem.QueueStateEvent(_keyboard, new KeyboardState(_key));
                    InputSystem.Update();
                    _frame++;
                    return;
                }

                InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
                InputSystem.Update();
                Destroy(gameObject);
            }
        }
    }
}
