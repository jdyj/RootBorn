using System.Collections;
using Rootborn.Game.Common;
using Rootborn.Game.Managers;
using Rootborn.Game.Player;
using Rootborn.Game.StudentLife;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rootborn.UI.StudentLife
{
    public static class DailyPlanRuntimeInstaller
    {
        private const string TownSceneName = "Town";
        private const string RunnerName = "[DailyPlanRuntimeInstaller]";
        private const string BoardName = "DailyPlanBoard";
        private const string PanelName = "DailyPlanPanel";
        private const string CanvasName = "DailyPlanCanvas";

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
            if (scene.name == TownSceneName)
            {
                EnsureScene(scene);
            }
        }

        public static void EnsureScene(Scene scene)
        {
            if (scene.name != TownSceneName || FindRoot(scene, RunnerName) != null)
            {
                return;
            }

            var runner = new GameObject(RunnerName);
            SceneManager.MoveGameObjectToScene(runner, scene);
            runner.AddComponent<Runner>();
        }

        public static void EnsureSceneForTests(Scene scene, Canvas canvas, GameDataRegistry registry, GameObject player)
        {
            if (registry == null)
            {
                return;
            }

            var runtimePlayer = player != null ? player : FindRuntimePlayer(scene);
            var visibleCanvas = canvas != null ? canvas : EnsureCanvas(scene);
            var panel = EnsurePanel(scene, visibleCanvas);
            var board = EnsureBoard(scene);
            var daily = EnsureDailyPlanProgress(runtimePlayer);
            var student = runtimePlayer != null ? runtimePlayer.GetComponent<StudentLifeProgressComponent>() : null;
            board.Bind(panel, registry.DailyPlanRouteSets, daily, student);
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureScene(scene);
        }

        private sealed class Runner : MonoBehaviour
        {
            private IEnumerator Start()
            {
                var scene = SceneManager.GetActiveScene();
                GameObject player = null;
                GameDataRegistry registry = null;
                float elapsed = 0f;
                while ((player == null || registry == null) && elapsed < 12f)
                {
                    if (player == null) player = FindRuntimePlayer(scene);
                    if (registry == null && Managers.Data != null) registry = Managers.Data.Registry;
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                if (registry == null)
                {
                    Debug.LogWarning("[ROOTBORN] Daily Plan runtime install skipped: registry not loaded.");
                    yield break;
                }

                var canvas = FindComponentInScene<Canvas>(scene);
                EnsureSceneForTests(scene, canvas, registry, player);
            }
        }

        private static DailyPlanProgressComponent EnsureDailyPlanProgress(GameObject player)
        {
            if (player == null) return null;
            var daily = player.GetComponent<DailyPlanProgressComponent>();
            if (daily == null) daily = player.AddComponent<DailyPlanProgressComponent>();
            return daily;
        }

        private static Canvas EnsureCanvas(Scene scene)
        {
            var existing = FindComponentInScene<Canvas>(scene);
            if (existing != null) return existing;

            var go = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(go, scene);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static DailyPlanPanel EnsurePanel(Scene scene, Canvas canvas)
        {
            var existing = FindComponentInScene<DailyPlanPanel>(scene);
            if (existing != null) return existing;

            var parent = canvas != null ? canvas.transform : null;
            var go = new GameObject(PanelName, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            else SceneManager.MoveGameObjectToScene(go, scene);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(640f, 420f);
            var panel = go.AddComponent<DailyPlanPanel>();
            go.SetActive(false);
            return panel;
        }

        private static DailyPlanBoardInteractor EnsureBoard(Scene scene)
        {
            var existing = FindComponentInScene<DailyPlanBoardInteractor>(scene);
            if (existing != null) return existing;

            var go = new GameObject(BoardName);
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.position = new Vector3(-1.5f, -2.75f, 0f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = CreateMarkerSprite(new Color(0.36f, 0.63f, 0.92f, 1f));
            renderer.sortingOrder = 48;
            var collider = go.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(0.85f, 0.85f);
            return go.AddComponent<DailyPlanBoardInteractor>();
        }

        private static Sprite CreateMarkerSprite(Color color)
        {
            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            texture.name = "DailyPlanBoardMarker";
            var pixels = new Color[64];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            texture.SetPixels(pixels);
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 8f, 8f), new Vector2(0.5f, 0.5f), 16f);
            sprite.name = texture.name;
            return sprite;
        }

        private static T FindComponentInScene<T>(Scene scene) where T : Component
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var component = roots[i].GetComponentInChildren<T>(true);
                if (component != null) return component;
            }

            return null;
        }

        private static GameObject FindRuntimePlayer(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var identities = roots[i].GetComponentsInChildren<PlayerIdentity>(false);
                for (int j = 0; j < identities.Length; j++)
                {
                    var identity = identities[j];
                    if (identity == null) continue;
                    var go = identity.gameObject;
                    var controller = go.GetComponent<PlayerController>();
                    var router = go.GetComponent<PlayerInteractionRouter>();
                    if (controller != null && controller.enabled && router != null && router.enabled)
                    {
                        return go;
                    }
                }
            }

            return FindRoot(scene, "Player");
        }

        private static GameObject FindRoot(Scene scene, string rootName)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == rootName) return roots[i];
            }

            return null;
        }
    }
}
