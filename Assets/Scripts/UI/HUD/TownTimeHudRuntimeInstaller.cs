using Rootborn.Game.Time;
using Rootborn.Game.StudentLife;
using Rootborn.Game.World;
using Rootborn.UI.Modern;
using Rootborn.UI.StudentLife;
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
            if (IsEligible(scene))
            {
                EnsureInScene(scene);
            }
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (IsEligible(scene))
            {
                EnsureInScene(scene);
            }
        }

        private static bool IsEligible(Scene scene) => scene.IsValid() && scene.isLoaded
            && (scene.name == TownSceneName || AuthoredWorldScene.Find(scene) != null);

        // The travel binder calls this after moving its validated canonical owner.
        // Moving an enabled actor between scenes does not emit OnEnable.
        public static void BindOwner(Scene scene, StudentLifeProgressComponent owner)
        {
            if (!IsEligible(scene) || owner == null || owner.gameObject.scene != scene || !owner.isActiveAndEnabled) return;
            EnsureInScene(scene).Bind(owner);
        }

        private static LifeTimePresentation EnsureInScene(Scene scene)
        {
            var canvas = EnsureCanvas(scene);
            var root = canvas.transform.Find(RootName) as RectTransform;
            if (root == null)
            {
                var rootGo = new GameObject(RootName, typeof(RectTransform));
                rootGo.transform.SetParent(canvas.transform, false);
                root = (RectTransform)rootGo.transform;
            }

            UrbanGameplayHudLayout.PositionClock(root);
            root.gameObject.SetActive(true);

            var background = root.GetComponent<ModernUiTileImage>();
            if (background == null) background = root.gameObject.AddComponent<ModernUiTileImage>();
            background.SetRecipe(ModernUiRecipes.CommonPanel);
            background.Rebuild();
            EnsureTimeIcon(root);

            var label = root.Find(LabelName) as RectTransform;
            if (label == null)
            {
                var labelGo = new GameObject(LabelName, typeof(RectTransform));
                labelGo.transform.SetParent(root, false);
                label = (RectTransform)labelGo.transform;
            }

            label.anchorMin = Vector2.zero;
            label.anchorMax = Vector2.one;
            label.offsetMin = new Vector2(128f, 36f);
            label.offsetMax = new Vector2(-36f, -36f);

            var text = label.GetComponent<Text>();
            if (text == null)
            {
                text = label.gameObject.AddComponent<Text>();
            }

            text.raycastTarget = false;
            text.alignment = TextAnchor.MiddleLeft;
            text.fontSize = 24;
            text.fontStyle = FontStyle.Bold;
            text.color = new Color32(48, 71, 83, 255);
            text.text = "Day --";
            text.font = LifeUiFont.Shared;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;

            var hud = root.GetComponent<TimeHud>();
            if (hud == null)
            {
                hud = root.gameObject.AddComponent<TimeHud>();
            }

            var lifeTime = root.GetComponent<LifeTimePresentation>();
            if (lifeTime == null) lifeTime = root.gameObject.AddComponent<LifeTimePresentation>();
            hud.BindLabelForRuntime(text);
            hud.BindLifeTimePresentation(lifeTime);
            var vitals = root.GetComponent<UrbanLifeVitalsHud>();
            if (vitals == null) vitals = root.gameObject.AddComponent<UrbanLifeVitalsHud>();
            vitals.Bind(lifeTime);
            return lifeTime;
        }

        private static void EnsureTimeIcon(RectTransform root)
        {
            var icon = root.Find("UrbanTimeIcon") as RectTransform;
            if (icon == null)
            {
                var go = new GameObject("UrbanTimeIcon", typeof(RectTransform), typeof(Image), typeof(UrbanUiIcon));
                go.transform.SetParent(root, false);
                icon = (RectTransform)go.transform;
            }
            icon.anchorMin = new Vector2(0f, 1f);
            icon.anchorMax = new Vector2(0f, 1f);
            icon.pivot = new Vector2(0f, 1f);
            icon.anchoredPosition = new Vector2(40f, -40f);
            icon.sizeDelta = new Vector2(64f, 64f);
            var image = icon.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            icon.GetComponent<UrbanUiIcon>().Configure("time");
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
