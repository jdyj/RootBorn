using System.Collections;
using Rootborn.Game.Common;
using Rootborn.Game.World;
using Rootborn.Game.Managers;
using Rootborn.Game.StudentLife;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rootborn.UI.StudentLife
{
    public static class StudentDayRuntimeInstaller
    {
        private const string TownSceneName = "Town";
        private const string RunnerName = "[StudentDayRuntimeInstaller]";

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
            if (scene.name == TownSceneName || AuthoredWorldScene.Find(scene) != null)
            {
                EnsureScene(scene);
            }
        }

        public static void EnsureScene(Scene scene)
        {
            if ((scene.name != TownSceneName && AuthoredWorldScene.Find(scene) == null) || FindRoot(scene, RunnerName) != null)
            {
                return;
            }

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
            private IEnumerator Start()
            {
                Scene scene = gameObject.scene;
                float elapsed = 0f;
                while (FindRoot(scene, "Player") == null && elapsed < 5f)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                var canvas = EnsureCanvas(scene);
                EnsureEventSystem(scene);
                var panel = StudentDayResultPanel.EnsureInScene(canvas);
                EnsureDayEndBoard(scene, panel);
            }
        }

        // Only authored scenes reach this point (Town is authored); their saved day-end interactors are bound, never generated.
        private static void EnsureDayEndBoard(Scene scene, StudentDayResultPanel panel)
        {
            if (AuthoredWorldScene.Find(scene) == null) return;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var authored in root.GetComponentsInChildren<StudentDayEndInteractor>(true))
                    authored.Bind(ResolveDefaultDayEndRules(), panel);
        }

        public static DayEndRuleDefinition ResolveDefaultDayEndRulesForTests(GameDataRegistry registry)
        {
            return registry != null ? registry.DefaultDayEndRule : null;
        }

        private static DayEndRuleDefinition ResolveDefaultDayEndRules()
        {
            var registry = Managers.Data != null ? Managers.Data.Registry : null;
            return ResolveDefaultDayEndRulesForTests(registry);
        }

        private static Canvas EnsureCanvas(Scene scene)
        {
            var canvas = FindComponentInScene<Canvas>(scene);
            if (canvas != null)
            {
                return canvas;
            }

            var canvasGo = new GameObject("[StudentDayCanvas]", typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(canvasGo, scene);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
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

            if (eventSystem.GetComponent<BaseInputModule>() == null)
            {
                AddUiInputModule(eventSystem.gameObject);
            }
        }

        private static void AddUiInputModule(GameObject go)
        {
            UiInputModuleInstaller.AddPreferredInputModule(go);
        }

        private static GameObject FindRoot(Scene scene, string rootName)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == rootName)
                {
                    return roots[i];
                }
            }

            return null;
        }

        private static T FindComponentInScene<T>(Scene scene) where T : Component
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var match = roots[i].GetComponentInChildren<T>(true);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }
    }
}