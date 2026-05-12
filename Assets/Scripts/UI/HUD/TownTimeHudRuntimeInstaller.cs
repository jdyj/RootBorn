using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rootborn.UI.HUD
{
    public static class TownTimeHudRuntimeInstaller
    {
        private const string TownSceneName = "Town";
        private const string CanvasName = "[TownTimeCanvas]";
        private const string RootName = "TownWorldTimeHud";
        private const string LabelName = "WorldTimeLabel";

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
                EnsureInScene(scene);
            }
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == TownSceneName)
            {
                EnsureInScene(scene);
            }
        }

        private static void EnsureInScene(Scene scene)
        {
            var canvas = EnsureCanvas(scene);
            var root = canvas.transform.Find(RootName) as RectTransform;
            if (root == null)
            {
                var rootGo = new GameObject(RootName, typeof(RectTransform));
                rootGo.transform.SetParent(canvas.transform, false);
                root = (RectTransform)rootGo.transform;
            }

            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = new Vector2(36f, -36f);
            root.sizeDelta = new Vector2(560f, 44f);
            root.gameObject.SetActive(true);

            var label = root.Find(LabelName) as RectTransform;
            if (label == null)
            {
                var labelGo = new GameObject(LabelName, typeof(RectTransform));
                labelGo.transform.SetParent(root, false);
                label = (RectTransform)labelGo.transform;
            }

            label.anchorMin = Vector2.zero;
            label.anchorMax = Vector2.one;
            label.offsetMin = Vector2.zero;
            label.offsetMax = Vector2.zero;

            var text = label.GetComponent<Text>();
            if (text == null)
            {
                text = label.gameObject.AddComponent<Text>();
            }

            text.raycastTarget = false;
            text.alignment = TextAnchor.MiddleLeft;
            text.fontSize = 24;
            text.fontStyle = FontStyle.Bold;
            text.color = new Color(1f, 0.96f, 0.72f, 1f);
            text.text = "Day --";
            if (text.font == null)
            {
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            var hud = root.GetComponent<TimeHud>();
            if (hud == null)
            {
                hud = root.gameObject.AddComponent<TimeHud>();
            }

            hud.BindLabelForRuntime(text);
        }

        private static Canvas EnsureCanvas(Scene scene)
        {
            var canvas = FindNamedComponentInScene<Canvas>(scene, CanvasName);
            if (canvas != null)
            {
                return canvas;
            }

            var canvasGo = new GameObject(CanvasName, typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(canvasGo, scene);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 95;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static T FindNamedComponentInScene<T>(Scene scene, string objectName) where T : Component
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name != objectName)
                {
                    continue;
                }

                var match = roots[i].GetComponent<T>();
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }
    }
}
