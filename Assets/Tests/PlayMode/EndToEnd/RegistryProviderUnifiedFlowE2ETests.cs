using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Managers;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.UI.MainMenu;
using Rootborn.UI.Objectives;
using Rootborn.UI.Quests;
using Rootborn.UI.StudentLife;
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
    public sealed class RegistryProviderUnifiedFlowE2ETests : InputTestFixture
    {
        private const string EvidencePath = "production/qa/evidence/registry-provider-town-flow.png";
        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            ActiveSaveContext.Clear();
            GameDataRegistryProvider.ResetCacheForTests();
            _saveRoot = NewSaveRoot("registry-provider-e2e");
            SetTestSaveRoot(_saveRoot);
            InputSystemUiModulePlayModeTestGuard.UseRealInputSystemForCurrentTest();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            ActiveSaveContext.Clear();
            SetTestSaveRoot(null);
            GameDataRegistryProvider.ResetCacheForTests();
            yield return null;
        }

        [UnityTest]
        public IEnumerator REGISTRY_PROVIDER_E2E_001_MainMenuNewGameEntersTownWithProviderBackedRuntimeInstallers()
        {
            yield return LoadScene("Boot");
            yield return WaitForScene("MainMenu", 10f);
            yield return WaitForRealInputEventSystem(5f);

            Assert.IsNotNull(Managers.Instance, "Boot should initialize Managers before player-facing menu flow.");
            Assert.IsNotNull(Managers.Data, "Boot should initialize DataManager before player-facing menu flow.");
            Assert.IsNotNull(Managers.Data.Registry, "DataManager should expose GameDataRegistry before menu flow.");
            Assert.IsTrue(GameDataRegistryProvider.TryGet(out var bootRegistry), "GameDataRegistryProvider should resolve the boot registry.");
            Assert.AreSame(Managers.Data.Registry, bootRegistry, "Provider should expose the same registry DataManager loaded.");

            yield return ClickActiveButton("SinglePlayButton", 5f);
            yield return WaitForSaveSlotPanel(5f);
            yield return ClickActiveButton("NewGameButton", 5f);
            yield return ClickActiveButton("ConfirmButton", 5f);
            yield return WaitForScene("Town", 15f);
            yield return WaitForRegistryDrivenTownRuntime(15f);
            yield return CaptureEvidence(EvidencePath);

            var metadata = ActiveSaveContext.Metadata;
            Assert.IsNotNull(metadata, "New Game flow should create active save metadata.");
            Assert.AreEqual("slot-0", metadata.SlotId);
            Assert.IsTrue(File.Exists(Path.Combine(_saveRoot, "slot-0", "metadata.json")), "New Game flow should persist slot metadata.");
        }

        private IEnumerator ClickActiveButton(string buttonName, float timeoutSeconds)
        {
            float elapsed = 0f;
            bool sawActiveButton = false;
            while (elapsed < timeoutSeconds)
            {
                var go = GameObject.Find(buttonName);
                var button = go != null ? go.GetComponent<Button>() : null;
                if (button != null && button.gameObject.activeInHierarchy && button.interactable)
                {
                    sawActiveButton = true;
                    var eventSystem = Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
                    Assert.IsNotNull(eventSystem, "A real EventSystem is required for player-facing pointer clicks.");
                    Assert.IsNotNull(eventSystem.GetComponent<InputSystemUIInputModule>(), "Pointer flow must use InputSystemUIInputModule.");
                    if (TryFindRaycastPointFor(button.gameObject, eventSystem, out Vector2 clickPoint))
                    {
                        yield return ClickWithMouse(clickPoint);
                        yield break;
                    }
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail(sawActiveButton
                ? "Expected active button named " + buttonName + " to become raycastable for a player-facing pointer click."
                : "Expected active interactable button named " + buttonName + ".");
        }

        private static IEnumerator LoadScene(string sceneName)
        {
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            Assert.IsNotNull(op, sceneName + " should be present in BuildSettings.");
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

        private static IEnumerator WaitForRealInputEventSystem(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var eventSystem = Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
                if (eventSystem != null && eventSystem.GetComponent<InputSystemUIInputModule>() != null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("A player-facing EventSystem with InputSystemUIInputModule was not available.");
        }

        private static IEnumerator WaitForSaveSlotPanel(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var panel = Object.FindFirstObjectByType<SaveSlotSelectPanel>(FindObjectsInactive.Include);
                var root = GameObject.Find("SaveSlotSelectRoot");
                if (panel != null && root != null && root.activeInHierarchy)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("Single Play pointer click did not open SaveSlotSelectPanel.");
        }

        private static IEnumerator WaitForRegistryDrivenTownRuntime(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var player = GameObject.Find("Player");
                bool providerReady = GameDataRegistryProvider.TryGet(out var providerRegistry);
                if (providerReady &&
                    Managers.Data != null &&
                    Managers.Data.Registry != null &&
                    ReferenceEquals(providerRegistry, Managers.Data.Registry) &&
                    player != null &&
                    player.GetComponent<StudentLifeProgressComponent>() != null &&
                    Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include) != null &&
                    Object.FindFirstObjectByType<ObjectiveJournalPanel>(FindObjectsInactive.Include) != null &&
                    Object.FindFirstObjectByType<ObjectiveJournalRuntimeProvider>(FindObjectsInactive.Include) != null &&
                    Object.FindFirstObjectByType<TrackedObjectiveHud>(FindObjectsInactive.Include) != null &&
                    Object.FindFirstObjectByType<DailyEventPanel>(FindObjectsInactive.Include) != null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("Town runtime did not expose provider-backed player, quest UI, Objective Journal, tracked objective HUD, and daily event panel within timeout.");
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
                        var world = Vector3.Lerp(
                            Vector3.Lerp(corners[0], corners[3], xStep / 4f),
                            Vector3.Lerp(corners[1], corners[2], xStep / 4f),
                            yStep / 4f);
                        var point = RectTransformUtility.WorldToScreenPoint(null, world);
                        if (RaycastsToTarget(point, target, eventSystem, results))
                        {
                            screenPoint = point;
                            return true;
                        }
                    }
                }
            }

            for (int yStep = 1; yStep <= 19; yStep++)
            {
                for (int xStep = 1; xStep <= 19; xStep++)
                {
                    var point = new Vector2(Screen.width * xStep / 20f, Screen.height * yStep / 20f);
                    if (RaycastsToTarget(point, target, eventSystem, results))
                    {
                        screenPoint = point;
                        return true;
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
                if (hit == target || hit.transform.IsChildOf(target.transform))
                {
                    return true;
                }
            }
            return false;
        }

        private static IEnumerator ClickWithMouse(Vector2 screenPoint)
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
            var driver = new GameObject("RegistryProviderPointerClickDriver").AddComponent<MouseClickFrameDriver>();
            driver.Configure(mouse, screenPoint);
            yield return null;
            yield return null;
            yield return null;
            InputSystem.RemoveDevice(mouse);
        }

        private static IEnumerator CaptureEvidence(string relativePath)
        {
            string fullPath = Path.GetFullPath(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            if (File.Exists(fullPath)) File.Delete(fullPath);
            ScreenCapture.CaptureScreenshot(fullPath);
            float elapsed = 0f;
            while (!File.Exists(fullPath) && elapsed < 3f)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsTrue(File.Exists(fullPath), "Registry provider E2E screenshot was not written: " + fullPath);
        }

        private static string NewSaveRoot(string prefix)
        {
            string root = Path.Combine(Application.temporaryCachePath, prefix, System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static void SetTestSaveRoot(string root)
        {
            SaveSlotSelectPanel.SetSaveRootForTests(root);
            SaveService.SetRootDirectoryForTests(root);
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
