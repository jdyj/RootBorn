using System.Collections;
using Rootborn.Game.Common;
using Rootborn.Game.World;
using Rootborn.Game.StudentLife;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rootborn.UI.StudentLife
{
    public static class DailyEventRuntimeInstaller
    {
        private const string TownSceneName = "Town";
        private const string RunnerName = "[DailyEventRuntimeInstaller]";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureForActiveScene();
        }

        public static void EnsureForActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            if ((scene.name == TownSceneName || AuthoredWorldScene.Find(scene) != null)) EnsureScene(scene);
        }

        public static void EnsureScene(Scene scene)
        {
            if ((scene.name != TownSceneName && AuthoredWorldScene.Find(scene) == null) || FindRoot(scene, RunnerName) != null) return;
            var runner = new GameObject(RunnerName);
            SceneManager.MoveGameObjectToScene(runner, scene);
            runner.AddComponent<Runner>();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureScene(scene);
        }

        private sealed class Runner : MonoBehaviour
        {
            private DailyEventPanel _panel;
            private DailyEventResolver _resolver;
            private GameDataRegistry _registry;
            private Coroutine _pendingPresentation;

            private void OnEnable()
            {
                LocationActivityInteractor.OnAnyInteracted += HandleLocationInteracted;
            }

            private void OnDisable()
            {
                LocationActivityInteractor.OnAnyInteracted -= HandleLocationInteracted;
                if (_pendingPresentation != null) StopCoroutine(_pendingPresentation);
                _pendingPresentation = null;
            }

            private IEnumerator Start()
            {
                Scene scene = gameObject.scene;
                float elapsed = 0f;
                GameObject player = null;
                while (elapsed < 5f)
                {
                    player = FindRoot(scene, "Player");
                    if (player != null && player.GetComponent<StudentLifeProgressComponent>() != null) break;
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                var canvas = EnsureCanvas(scene);
                EnsureEventSystem(scene);
                _panel = DailyEventPanel.EnsureInScene(canvas);
                if (player != null) EnsureDailyEventProgress(player);
                _registry = ResolveRegistry();
                _resolver = new DailyEventResolver(_registry != null ? _registry.DailyEvents : null);
            }

            private void HandleLocationInteracted(LocationActivityInteractor interactor, GameObject player)
            {
                if (interactor == null || player == null) return;
                if (_pendingPresentation != null) StopCoroutine(_pendingPresentation);
                _pendingPresentation = StartCoroutine(PresentAfterLocationChoice(interactor, player));
            }

            private IEnumerator PresentAfterLocationChoice(LocationActivityInteractor interactor, GameObject player)
            {
                // All listeners for this interaction must finish before testing the primary modal.
                yield return null;
                while (interactor != null && player != null)
                {
                    var locationPanel = Object.FindFirstObjectByType<LocationIdentityPanel>(FindObjectsInactive.Include);
                    if (locationPanel == null || !locationPanel.IsOpen) break;
                    yield return null;
                }
                _pendingPresentation = null;
                if (interactor == null || player == null) yield break;
                var student = player.GetComponent<StudentLifeProgressComponent>();
                var events = EnsureDailyEventProgress(player);
                if (student == null || events == null) yield break;
                if (_registry == null) _registry = ResolveRegistry();
                if (_resolver == null) _resolver = new DailyEventResolver(_registry != null ? _registry.DailyEvents : null);
                if (_panel == null) _panel = DailyEventPanel.EnsureInScene(EnsureCanvas(gameObject.scene));

                var progress = student.EnsureProgress();
                var eventProgress = events.EnsureProgress();
                var location = interactor.Identity != null ? interactor.Identity.Location : null;
                var available = _resolver.GetAvailableEvents(new DailyEventContext(progress, eventProgress, location, progress.CurrentDay, progress.TimeMinutes));
                if (available.Length == 0) yield break;
                DailyEventProgressPersistence.Save(events);
                _panel.Show(available[0], player);
            }

            private static DailyEventProgressComponent EnsureDailyEventProgress(GameObject player)
            {
                if (player == null) return null;
                var component = player.GetComponent<DailyEventProgressComponent>();
                if (component == null) component = player.AddComponent<DailyEventProgressComponent>();
                DailyEventProgressPersistence.TryLoad(component);
                component.EnsureProgress();
                return component;
            }
        }

        private static GameDataRegistry ResolveRegistry()
        {
            return GameDataRegistryProvider.TryGet(out var registry) ? registry : null;
        }

        private static Canvas EnsureCanvas(Scene scene)
        {
            var canvas = FindComponentInScene<Canvas>(scene);
            if (canvas != null) return canvas;
            var canvasGo = new GameObject("[DailyEventCanvas]", typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(canvasGo, scene);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 85;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static void EnsureEventSystem(Scene scene)
        {
            var current = EventSystem.current;
            if (current != null)
            {
                if (current.GetComponent<BaseInputModule>() == null)
                {
                    AddUiInputModule(current.gameObject);
                }

                return;
            }

            var eventSystem = FindComponentInScene<EventSystem>(scene);
            if (eventSystem == null)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem));
                AddUiInputModule(go);
                SceneManager.MoveGameObjectToScene(go, scene);
                return;
            }

            if (eventSystem.GetComponent<BaseInputModule>() == null) AddUiInputModule(eventSystem.gameObject);
        }


        private static void AddUiInputModule(GameObject go)
        {
            UiInputModuleInstaller.AddPreferredInputModule(go);
        }        private static GameObject FindRoot(Scene scene, string rootName)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++) if (roots[i].name == rootName) return roots[i];
            return null;
        }

        private static T FindComponentInScene<T>(Scene scene) where T : Component
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var match = roots[i].GetComponentInChildren<T>(true);
                if (match != null) return match;
            }
            return null;
        }
    }
}
