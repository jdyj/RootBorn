using System.Collections;
using Rootborn.Game.Common;
using Rootborn.Game.Encyclopedia;
using Rootborn.Game.Managers;
using Rootborn.Game.Player;
using Rootborn.Game.StudentLife;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rootborn.UI.Encyclopedia
{
    public static class EncyclopediaRuntimeInstaller
    {
        private const string RunnerName = "[EncyclopediaRuntimeInstaller]";
        private const string PanelName = "EncyclopediaPanel";
        private const string ButtonName = "EncyclopediaButton";
        private const string BootSceneName = "Boot";
        private const string MainMenuSceneName = "MainMenu";

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
            if (ShouldInstall(scene)) StartRunner(scene);
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (ShouldInstall(scene)) StartRunner(scene);
        }

        private static bool ShouldInstall(Scene scene)
        {
            return scene.IsValid() && scene.isLoaded && scene.name != BootSceneName && scene.name != MainMenuSceneName;
        }

        private static void StartRunner(Scene scene)
        {
            if (FindRoot(scene, RunnerName) != null) return;
            var go = new GameObject(RunnerName);
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<Runner>();
        }

        private sealed class Runner : MonoBehaviour
        {
            private IEnumerator Start()
            {
                var bootstrap = Managers.BootstrapAsync();
                while (!bootstrap.IsCompleted) yield return null;

                float elapsed = 0f;
                Canvas canvas = null;
                GameObject player = null;
                while (elapsed < 15f)
                {
                    var scene = SceneManager.GetActiveScene();
                    canvas = canvas != null ? canvas : FindComponentInScene<Canvas>(scene);
                    player = player != null ? player : FindRoot(scene, "Player");
                    if (canvas != null && player != null && player.GetComponent<StudentLifeProgressComponent>() != null) break;
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                Install(canvas, player, Managers.Data != null ? Managers.Data.Registry : null);
            }
        }

        public static EncyclopediaPanel Install(Canvas canvas, GameObject player, GameDataRegistry registry)
        {
            if (canvas == null || player == null || registry == null || registry.EncyclopediaCategories == null || registry.EncyclopediaEntries == null || registry.EncyclopediaCategories.Length == 0 || registry.EncyclopediaEntries.Length == 0)
            {
                return null;
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 1000);

            var component = player.GetComponent<EncyclopediaProgressComponent>();
            if (component == null) component = player.AddComponent<EncyclopediaProgressComponent>();
            component.Bind(registry.EncyclopediaCategories, registry.EncyclopediaEntries, player.GetComponent<StudentLifeProgressComponent>(), player.GetComponent<PlayerInventory>(), player.GetComponent<GatherInteractor>());
            component.EvaluateUnlocks();

            var panel = EnsurePanel(canvas.transform);
            panel.Bind(component.Index, component.Progress);
            panel.Hide();
            EnsureButton(canvas.transform, panel);
            return panel;
        }

        private static EncyclopediaPanel EnsurePanel(Transform canvasTransform)
        {
            var child = canvasTransform.Find(PanelName);
            GameObject go = child != null ? child.gameObject : new GameObject(PanelName, typeof(RectTransform));
            if (child == null) go.transform.SetParent(canvasTransform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(820f, 560f);
            rect.SetAsLastSibling();
            var panel = go.GetComponent<EncyclopediaPanel>();
            if (panel == null) panel = go.AddComponent<EncyclopediaPanel>();
            return panel;
        }

        private static void EnsureButton(Transform canvasTransform, EncyclopediaPanel panel)
        {
            if (canvasTransform.Find(ButtonName) != null) return;
            var go = new GameObject(ButtonName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(ModernUiTileImage));
            go.transform.SetParent(canvasTransform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-24f, -86f);
            rect.sizeDelta = new Vector2(150f, 34f);
            go.GetComponent<Image>().color = Color.clear;
            var tiles = go.GetComponent<ModernUiTileImage>();
            tiles.SetRecipe(ModernUiRecipes.CommonPanel);
            tiles.Rebuild();
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(() =>
            {
                if (panel.IsVisible) panel.Hide(); else panel.Show();
            });
            MakeLabel(rect, "Label", "ENCYCLOPEDIA", Vector2.zero, new Vector2(150f, 32f), 13, TextAnchor.MiddleCenter);
        }

        private static Text MakeLabel(RectTransform parent, string name, string value, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = go.GetComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color(0.28f, 0.19f, 0.12f, 1f);
            return text;
        }

        private static GameObject FindRoot(Scene scene, string rootName)
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
