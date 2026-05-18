using System.Collections;
using Rootborn.Game.Common;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rootborn.UI.Interiors
{
    public sealed class HousePlacementModeToggleInstaller : MonoBehaviour
    {
        private const string SceneName = "House";
        private const string ButtonName = "PlacementModeToggleButton";
        private static readonly Color LabelColor = new Color(0.16f, 0.12f, 0.08f, 1f);
        private InteriorFurniturePlacementPanel _panel;
        private Button _button;
        private Text _label;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneLoaded()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureToggleAfterInitialSceneLoad()
        {
            EnsureInstaller(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureInstaller(scene);
        }

        private static void EnsureInstaller(Scene scene)
        {
            if (scene.name != SceneName)
            {
                return;
            }

            if (FindFirstObjectByType<HousePlacementModeToggleInstaller>() != null)
            {
                return;
            }

            var go = new GameObject("[HousePlacementModeToggleInstaller]");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<HousePlacementModeToggleInstaller>();
        }

        private IEnumerator Start()
        {
            for (int i = 0; i < 180; i++)
            {
                if (TryInstall())
                {
                    yield break;
                }

                yield return null;
            }
        }

        private bool TryInstall()
        {
            if (_button != null)
            {
                return true;
            }

            _panel = FindFirstObjectByType<InteriorFurniturePlacementPanel>(FindObjectsInactive.Include);
            if (_panel == null)
            {
                return false;
            }

            var canvas = GameObject.Find("HouseInteriorPlacementCanvas")?.GetComponent<Canvas>() ?? FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                return false;
            }

            var existing = GameObject.Find(ButtonName);
            var buttonObject = existing != null
                ? existing
                : ModernUiPanelBuilder.CreateCommonPanel48Button(canvas.transform, ButtonName, new Vector2(-790f, 480f), new Vector2(240f, 64f));
            ConfigureButtonRect(buttonObject.GetComponent<RectTransform>());
            buttonObject.transform.SetAsLastSibling();
            _button = buttonObject.GetComponent<Button>();
            _label = EnsureLabel(buttonObject.transform);
            var handler = buttonObject.GetComponent<HousePlacementModeToggleButton>() ?? buttonObject.AddComponent<HousePlacementModeToggleButton>();
            handler.Bind(_label);
            SetLabel(_panel.gameObject.activeSelf);
            EnsureEventSystem();
            return true;
        }

        private static void ConfigureButtonRect(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(-790f, 480f);
            rect.sizeDelta = new Vector2(240f, 64f);
        }

        private static Text EnsureLabel(Transform parent)
        {
            var existing = parent.Find("Label")?.GetComponent<Text>();
            if (existing != null)
            {
                existing.color = LabelColor;
                existing.fontSize = 20;
                existing.raycastTarget = false;
                existing.transform.SetAsLastSibling();
                return existing;
            }

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(parent, false);
            var rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(10f, 6f);
            rect.offsetMax = new Vector2(-10f, -6f);

            var label = labelObject.GetComponent<Text>();
            label.alignment = TextAnchor.MiddleCenter;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 20;
            label.color = LabelColor;
            label.raycastTarget = false;
            label.transform.SetAsLastSibling();
            return label;
        }

        private void SetLabel(bool placementModeActive)
        {
            HousePlacementModeToggleButton.SetLabel(_label, placementModeActive);
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var go = new GameObject("EventSystem", typeof(EventSystem));
            SceneManager.MoveGameObjectToScene(go, SceneManager.GetActiveScene());
            UiInputModuleInstaller.AddPreferredInputModule(go);
        }
    }

    public sealed class HousePlacementModeToggleButton : MonoBehaviour, IPointerClickHandler
    {
        private Text _label;

        public void Bind(Text label)
        {
            _label = label;
            SetLabel(_label, IsPlacementModeActive());
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            TogglePlacementMode();
        }

        private void TogglePlacementMode()
        {
            var panel = FindFirstObjectByType<InteriorFurniturePlacementPanel>(FindObjectsInactive.Include);
            if (panel == null)
            {
                return;
            }

            bool nextActive = !panel.gameObject.activeSelf;
            panel.gameObject.SetActive(nextActive);
            if (!nextActive)
            {
                var overlay = FindFirstObjectByType<InteriorPlacementPreviewOverlay>();
                if (overlay != null)
                {
                    Destroy(overlay.gameObject);
                }
            }

            transform.SetAsLastSibling();
            SetLabel(_label, nextActive);
        }

        private static bool IsPlacementModeActive()
        {
            var panel = FindFirstObjectByType<InteriorFurniturePlacementPanel>(FindObjectsInactive.Include);
            return panel != null && panel.gameObject.activeSelf;
        }

        public static void SetLabel(Text label, bool placementModeActive)
        {
            if (label == null)
            {
                return;
            }

            label.text = placementModeActive ? "Placement ON" : "Placement OFF";
            label.transform.SetAsLastSibling();
        }
    }
}
