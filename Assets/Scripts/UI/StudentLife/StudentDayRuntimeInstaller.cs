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
        private const string DayEndBoardName = "StudentDayEndBoard";

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

        private static void EnsureDayEndBoard(Scene scene, StudentDayResultPanel panel)
        {
            if (AuthoredWorldScene.Find(scene) != null)
            {
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var authored in root.GetComponentsInChildren<StudentDayEndInteractor>(true))
                        authored.Bind(ResolveDefaultDayEndRules(), panel);
                return;
            }
            var go = FindByName(scene, DayEndBoardName);
            if (go == null)
            {
                go = new GameObject(DayEndBoardName);
                SceneManager.MoveGameObjectToScene(go, scene);
            }

            go.transform.position = new Vector3(-0.5f, -3.0f, 0f);
            var renderer = go.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = go.AddComponent<SpriteRenderer>();
            }
            if (renderer.sprite == null)
            {
                renderer.sprite = CreateMarkerSprite(new Color(0.88f, 0.42f, 0.26f, 1f));
            }
            renderer.sortingOrder = 48;

            var collider = go.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                collider = go.AddComponent<BoxCollider2D>();
            }
            collider.isTrigger = true;
            collider.size = new Vector2(0.85f, 0.85f);

            var interactor = go.GetComponent<StudentDayEndInteractor>();
            if (interactor == null)
            {
                interactor = go.AddComponent<StudentDayEndInteractor>();
            }
            interactor.Bind(ResolveDefaultDayEndRules(), panel);
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

        private static Sprite CreateMarkerSprite(Color color)
        {
            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            texture.name = "StudentDayEndMarker";
            var pixels = new Color[64];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
            texture.SetPixels(pixels);
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 8f, 8f), new Vector2(0.5f, 0.5f), 16f);
            sprite.name = texture.name;
            return sprite;
        }


        private static void AddUiInputModule(GameObject go)
        {
            UiInputModuleInstaller.AddPreferredInputModule(go);
        }        private static GameObject FindRoot(Scene scene, string rootName)
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

        private static GameObject FindByName(Scene scene, string objectName)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var match = FindByName(roots[i].transform, objectName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static GameObject FindByName(Transform root, string objectName)
        {
            if (root.name == objectName)
            {
                return root.gameObject;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                var match = FindByName(root.GetChild(i), objectName);
                if (match != null)
                {
                    return match;
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