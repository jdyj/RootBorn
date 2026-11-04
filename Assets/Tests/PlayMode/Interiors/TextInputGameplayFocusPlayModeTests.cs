using System.Collections;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Player;
using Rootborn.Game.Resources;
using Rootborn.UI.Interiors;
using Rootborn.UI.Modern;
using Rootborn.UI.Objectives;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.Interiors
{
    // Focus is fixture setup; real KeyboardState events exercise the production Update/FixedUpdate consumers.
    // Native mouse/text entry through the furniture palette remains a separate player-flow verification.
    public sealed class TextInputGameplayFocusPlayModeTests : InputTestFixture
    {
        private GameObject _host;
        private GameObject _worldRoot;
        private Keyboard _keyboard;
        private InputField _input;
        private EventSystem _eventSystem;
        private GameObject _previousSelection;

        public override void Setup()
        {
            base.Setup();
            InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();
            _eventSystem = EventSystem.current;
            Assert.IsNotNull(_eventSystem);
            _previousSelection = _eventSystem.currentSelectedGameObject;
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _host = new GameObject("TextInputFocusFixture", typeof(RectTransform), typeof(Canvas));
            _worldRoot = new GameObject("TextInputWorldFixture");
            _host.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var inputObject = Child("SearchInput", typeof(RectTransform), typeof(Image), typeof(InputField));
            inputObject.GetComponent<RectTransform>().sizeDelta = new Vector2(280f, 40f);
            var textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(inputObject.transform, false);
            textObject.GetComponent<RectTransform>().sizeDelta = new Vector2(260f, 36f);
            var label = textObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.supportRichText = false;
            _input = inputObject.GetComponent<InputField>();
            _input.textComponent = label;
            _input.targetGraphic = inputObject.GetComponent<Image>();
        }

        public override void TearDown()
        {
            if (_eventSystem != null) _eventSystem.SetSelectedGameObject(_previousSelection);
            if (_host != null) Object.DestroyImmediate(_host);
            if (_worldRoot != null) Object.DestroyImmediate(_worldRoot);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator FocusedSearch_BlocksCameraPanFullViewAndZoom_ThenRestoresControlsOnButtonFocus()
        {
            var cameraObject = WorldChild("PlacementCamera", typeof(Camera), typeof(HousePlacementCameraController));
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 4f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            var controller = cameraObject.GetComponent<HousePlacementCameraController>();
            controller.Configure(camera, new Bounds(Vector3.zero, new Vector3(100f, 100f, 1f)));
            // Configure supplies camera/bounds; only the placement entry point enables input ownership.
            controller.BeginPlacementControl();

            SetKeys(Key.D);
            yield return Frames(3);
            Assert.Greater(camera.transform.position.x, 0f, "Normal keyboard camera control must work before focusing text.");
            SetKeys();
            yield return FocusInput();
            var position = camera.transform.position;
            var zoom = camera.orthographicSize;
            SetKeys(Key.W, Key.F, Key.Minus);
            yield return Frames(4);
            Assert.AreEqual(position, camera.transform.position, "Typing W/F must not pan or recenter the room.");
            Assert.AreEqual(zoom, camera.orthographicSize, "Text editing must not trigger full-view or zoom shortcuts.");

            SetKeys();
            var button = Child("NormalButton", typeof(RectTransform), typeof(Image), typeof(Button));
            _input.DeactivateInputField();
            _eventSystem.SetSelectedGameObject(button);
            SetKeys(Key.D);
            yield return Frames(3);
            Assert.Greater(camera.transform.position.x, position.x, "Ordinary button focus must not disable gameplay keyboard controls.");
        }

        [UnityTest]
        public IEnumerator FocusedSearch_StopsHeldPlayerMovementAndExistingVelocity_ThenResumesAfterDeselect()
        {
            var playerObject = WorldChild("FocusPlayer", typeof(Rigidbody2D), typeof(PlayerController));
            var body = playerObject.GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            var player = playerObject.GetComponent<PlayerController>();
            SetKeys(Key.D);
            yield return null;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.Greater(body.linearVelocity.x, 0f);

            yield return FocusInput();
            var position = body.position;
            body.linearVelocity = new Vector2(3f, 4f); // A pre-focus movement velocity must not leak into the next physics tick.
            SetKeys(Key.W);
            yield return null;
            Assert.AreEqual(Vector2.zero, player.Input);
            Assert.AreEqual(Vector2.zero, body.linearVelocity, "Update must neutralize existing velocity while typing.");
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.Less(Vector2.Distance(position, body.position), 0.001f, "FixedUpdate must not reapply stale movement while text owns the keys.");

            _input.DeactivateInputField();
            _eventSystem.SetSelectedGameObject(null);
            yield return null;
            yield return new WaitForFixedUpdate();
            Assert.Greater(body.linearVelocity.y, 0f, "Held movement resumes after leaving the text field.");
        }

        [UnityTest]
        public IEnumerator FocusedSearch_BlocksAllPanelHotkeys_AndConsumesHeldKeyUntilRelease()
        {
            var inventory = Child("Inventory", typeof(RectTransform), typeof(ModernUiInventoryPanel)).GetComponent<ModernUiInventoryPanel>();
            var status = Child("Status", typeof(RectTransform), typeof(ModernUiStatusPanel)).GetComponent<ModernUiStatusPanel>();
            var settings = Child("Settings", typeof(RectTransform), typeof(SettingsPanel)).GetComponent<SettingsPanel>();
            var journal = Child("Journal", typeof(RectTransform), typeof(ObjectiveJournalPanel)).GetComponent<ObjectiveJournalPanel>();
            var router = _host.AddComponent<ModernUiPanelInputRouter>();
            router.Bind(inventory, status, settings, journal);
            inventory.Hide(); status.Hide(); settings.Hide(); journal.Hide();
            yield return FocusInput();

            foreach (var key in new[] { Key.I, Key.C, Key.Tab, Key.Escape })
            {
                SetKeys(key);
                yield return Frames(2);
                Assert.IsFalse(inventory.IsVisible || status.IsVisible || settings.IsVisible || journal.IsVisible,
                    "A text-editing key must not open a gameplay panel: " + key);
                SetKeys();
                yield return Frames(2);
            }

            SetKeys(Key.C);
            yield return Frames(2);
            _input.DeactivateInputField();
            _eventSystem.SetSelectedGameObject(null);
            yield return Frames(2);
            Assert.IsFalse(status.IsVisible, "Losing focus while C remains held must not replay the consumed shortcut.");
            SetKeys();
            yield return Frames(2);
            SetKeys(Key.C);
            yield return Frames(2);
            Assert.IsTrue(status.IsVisible, "A fresh press after leaving text must retain the normal shortcut.");
        }

        [UnityTest]
        public IEnumerator FocusedSearch_BlocksGatherKeys_ConsumesHeldKeysAndResumesOnFreshPress()
        {
            var definition = ScriptableObject.CreateInstance<ResourceNodeDefinition>();
            try
            {
                // Keep the fixture away from any loaded scene interactables. Outcomes still come from
                // KeyboardState -> GatherInteractor.Update -> the real nearby ResourceNode.Hit path.
                var player = WorldChild("GatherFocusPlayer", typeof(GatherInteractor));
                player.transform.position = new Vector3(10000f, 10000f, 0f);
                var resourceObject = WorldChild("GatherFocusResource", typeof(SpriteRenderer), typeof(ResourceNode));
                resourceObject.transform.position = player.transform.position + Vector3.right;
                var resource = resourceObject.GetComponent<ResourceNode>();
                resource.BindForRuntime(definition, resourceObject.GetComponent<SpriteRenderer>());
                int gathered = 0;
                resource.OnGathered += (_, __) => gathered++;
                var button = Child("GatherNormalButton", typeof(RectTransform), typeof(Image), typeof(Button));
                _eventSystem.SetSelectedGameObject(button);

                SetKeys(Key.E);
                yield return Frames(2);
                Assert.AreEqual(1, gathered, "The nearby resource must react to a real keyboard press before text takes focus.");
                SetKeys();
                yield return Frames(2);
                int expected = 1;

                foreach (var key in new[] { Key.E, Key.Space, Key.LeftCtrl, Key.RightCtrl })
                {
                    yield return FocusInput();
                    SetKeys(key);
                    yield return Frames(2);
                    Assert.AreEqual(expected, gathered, "Typing/editing must not gather or interact with the world: " + key);

                    _input.DeactivateInputField();
                    _eventSystem.SetSelectedGameObject(button);
                    yield return Frames(2);
                    Assert.AreEqual(expected, gathered, "Leaving text while the key remains held must not replay the consumed interaction: " + key);

                    SetKeys();
                    yield return Frames(2);
                    SetKeys(key);
                    yield return Frames(2);
                    expected++;
                    Assert.AreEqual(expected, gathered, "A fresh press after release must resume normal interaction even with an ordinary button selected: " + key);
                    SetKeys();
                    yield return Frames(2);
                }

                Assert.IsFalse(resource.IsBroken, "The target must remain available for all blocked and resumed input checks.");
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        private IEnumerator FocusInput()
        {
            _eventSystem.SetSelectedGameObject(_input.gameObject);
            _input.ActivateInputField();
            yield return Frames(2);
            Assert.IsTrue(_input.isFocused, "The fixture must establish a real uGUI editing focus before testing keyboard consumers.");
        }

        private void SetKeys(params Key[] keys)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(keys));
            InputSystem.Update();
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        private GameObject Child(string name, params System.Type[] components)
        {
            var go = new GameObject(name, components);
            go.transform.SetParent(_host.transform, false);
            return go;
        }

        private GameObject WorldChild(string name, params System.Type[] components)
        {
            var go = new GameObject(name, components);
            go.transform.SetParent(_worldRoot.transform, false);
            return go;
        }
    }
}
