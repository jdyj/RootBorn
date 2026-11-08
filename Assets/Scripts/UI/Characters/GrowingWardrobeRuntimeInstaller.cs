using System.Collections;
using Rootborn.Game.Localization;
using Rootborn.UI.Modern;
using Rootborn.UI.StudentLife;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rootborn.UI.Characters
{
    /// <summary>Adds the wardrobe button under the top-right menu in any scene where the player has a layered character.</summary>
    public static class GrowingWardrobeRuntimeInstaller
    {
        public const string ButtonName = "WardrobeButton";
        public const string PanelName = "GrowingWardrobePanel";
        private const string RunnerName = "[GrowingWardrobeRuntimeInstaller]";
        private const float WaitSeconds = 15f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            StartRunner(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => StartRunner(scene);

        private static void StartRunner(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            var go = new GameObject(RunnerName);
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<Runner>();
        }

        private sealed class Runner : MonoBehaviour
        {
            private IEnumerator Start()
            {
                float elapsed = 0f;
                while (elapsed < WaitSeconds)
                {
                    var driver = FindDriver(gameObject.scene);
                    var canvas = FindCanvas(gameObject.scene);
                    if (driver != null && canvas != null)
                    {
                        Install(canvas, driver);
                        break;
                    }

                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                Destroy(gameObject);
            }
        }

        private static void Install(Canvas canvas, GrowingCharacterStageDriver driver)
        {
            var parent = canvas.transform;
            if (parent.Find(ButtonName) != null) return;
            var panelObject = new GameObject(PanelName, typeof(RectTransform));
            panelObject.transform.SetParent(parent, false);
            var panelRect = (RectTransform)panelObject.transform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(1040f, 420f);
            var panel = panelObject.AddComponent<GrowingWardrobePanel>();
            panelObject.SetActive(false);

            var buttonObject = new GameObject(ButtonName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(ModernUiTileImage));
            buttonObject.transform.SetParent(parent, false);
            var rect = (RectTransform)buttonObject.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-24f, -126f);
            rect.sizeDelta = new Vector2(150f, 34f);
            buttonObject.GetComponent<Image>().color = Color.clear;
            var tiles = buttonObject.GetComponent<ModernUiTileImage>();
            tiles.SetRecipe(ModernUiRecipes.CommonPanel);
            tiles.Rebuild();
            buttonObject.GetComponent<Button>().onClick.AddListener(() => Toggle(panel, FindDriver(panelObject.scene)));

            var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(rect, false);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            var text = label.GetComponent<Text>();
            text.text = GameText.Get("ui.wardrobe.open");
            text.font = LifeUiFont.Shared;
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            text.color = new Color(0.28f, 0.19f, 0.12f, 1f);
        }

        private static void Toggle(GrowingWardrobePanel panel, GrowingCharacterStageDriver driver)
        {
            if (panel.IsVisible)
            {
                panel.Hide();
                return;
            }

            if (driver != null) Show(panel, driver);
        }

        private static void Show(GrowingWardrobePanel panel, GrowingCharacterStageDriver driver)
        {
            panel.Show(driver.WardrobeSections(), id =>
            {
                driver.ChooseOutfit(id);
                Show(panel, driver);
            });
        }

        /// <summary>Only the local player's character. Other players' copies are never offered, whatever the scene order.</summary>
        public static GrowingCharacterStageDriver FindDriver(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var driver in root.GetComponentsInChildren<GrowingCharacterStageDriver>(true))
                {
                    if (driver.IsLocalPlayer) return driver;
                }
            }

            return null;
        }

        private static Canvas FindCanvas(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var canvas = root.GetComponentInChildren<Canvas>(true);
                if (canvas != null && canvas.isRootCanvas) return canvas;
            }

            return null;
        }
    }
}
