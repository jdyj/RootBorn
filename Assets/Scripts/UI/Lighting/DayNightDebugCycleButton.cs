using Rootborn.Game.Time;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rootborn.UI.Lighting
{
    public sealed class DayNightDebugCycleButton : MonoBehaviour
    {
        private const string CanvasName = "DayNightDebugCanvas";
        private const string ButtonName = "DayNightDebugCycleButton";

        [SerializeField] private GameClock _clock;
        [SerializeField] private Vector2 _anchoredPosition = new Vector2(12f, -154f);
        [SerializeField] private Vector2 _size = new Vector2(132f, 28f);
        [SerializeField] private float[] _hours = { 18f, 22f, 5f, 12f };

        private Button _button;
        private Text _label;
        private int _index;

        private void OnEnable() => LifeTimePresentation.AvailabilityChanged += RefreshVisibility;
        private void OnDisable() => LifeTimePresentation.AvailabilityChanged -= RefreshVisibility;
        private bool UsesLifeTime => LifeTimePresentation.FindInScene(gameObject.scene) != null;
        private void RefreshVisibility()
        {
            if (_button != null) _button.gameObject.SetActive(!UsesLifeTime);
        }

        private void Start()
        {
            ResolveClock();
            EnsureEventSystem();
            EnsureButton();
            UpdateLabel();
            RefreshVisibility();
        }

        private void OnDestroy()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(HandleClick);
            }
        }

        public void HandleClick()
        {
            if (UsesLifeTime) return;
            ResolveClock();
            if (_clock == null || _hours == null || _hours.Length == 0)
            {
                return;
            }

            float hour = GameClock.WrapHour(_hours[_index]);
            _index = (_index + 1) % _hours.Length;
            _clock.SetCurrentHour(hour);
            UpdateLabel();
        }

        private void ResolveClock()
        {
            if (_clock == null)
            {
                _clock = GameClock.Instance != null ? GameClock.Instance : Object.FindFirstObjectByType<GameClock>();
            }
        }

        private void EnsureButton()
        {
            var existing = GameObject.Find(ButtonName);
            if (existing != null)
            {
                _button = existing.GetComponent<Button>();
                _label = existing.GetComponentInChildren<Text>(true);
                if (_button != null)
                {
                    _button.onClick.RemoveListener(HandleClick);
                    _button.onClick.AddListener(HandleClick);
                }
                return;
            }

            var canvas = GameObject.Find(CanvasName)?.GetComponent<Canvas>();
            if (canvas == null)
            {
                var canvasObject = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 5000;

                var scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            var buttonObject = new GameObject(ButtonName, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(canvas.transform, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = _anchoredPosition;
            rect.sizeDelta = _size;

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.16f, 0.2f, 0.26f, 0.92f);

            _button = buttonObject.GetComponent<Button>();
            _button.targetGraphic = image;
            _button.onClick.AddListener(HandleClick);

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(buttonObject.transform, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(6f, 0f);
            labelRect.offsetMax = new Vector2(-6f, 0f);

            _label = labelObject.GetComponent<Text>();
            _label.alignment = TextAnchor.MiddleCenter;
            _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _label.fontSize = 13;
            _label.color = Color.white;
            _label.raycastTarget = false;
        }

        private void UpdateLabel()
        {
            if (_label == null)
            {
                return;
            }

            string phase = _clock != null && _clock.IsNight ? "Night" : _clock != null && _clock.IsEvening ? "Evening" : _clock != null && _clock.IsMorning ? "Morning" : "Day";
            _label.text = "Time: " + phase;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Object.DontDestroyOnLoad(eventSystem);
        }
    }
}