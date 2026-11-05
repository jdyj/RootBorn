using System;
using Rootborn.Game.Player;
using Rootborn.UI.Modern;
using Rootborn.Game.Common;
using Rootborn.UI.StudentLife;
using Rootborn.Game.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.Objectives
{
    [DisallowMultipleComponent]
    public sealed class TrackedObjectiveHud : MonoBehaviour
    {
        private static readonly Vector2 DefaultSize = new Vector2(640f, 200f);
        private const string IconName = "TrackedObjectiveIcon";
        private GameObject _root;
        private Text _title;
        private Text _progress;
        private Text _hint;
        private bool _isVisible;
        private string _trackedStableKey = string.Empty;
        private CanvasGroup _visibility;
        private RectTransform _screenReservationRoot;
        private Canvas _screenReservationCanvas;
        private Func<Rect?> _screenReservationProvider;
        private readonly Vector3[] _screenReservationCorners = new Vector3[4];

        private void OnEnable()
        {
            RefreshScreenReservationCanvas();
            if (_screenReservationProvider == null) _screenReservationProvider = GetScreenReservation;
            InteractionPromptScreenReservations.Register(this, _screenReservationProvider);
        }

        private void OnDisable()
        {
            InteractionPromptScreenReservations.Unregister(this);
        }

        private void OnDestroy()
        {
            InteractionPromptScreenReservations.Unregister(this);
        }

        private void OnTransformParentChanged()
        {
            RefreshScreenReservationCanvas();
        }

        private void RefreshScreenReservationCanvas()
        {
            _screenReservationCanvas = GetComponentInParent<Canvas>();
        }

        private Rect? GetScreenReservation()
        {
            if (!isActiveAndEnabled || !_isVisible || _screenReservationRoot == null ||
                !_screenReservationRoot.gameObject.activeInHierarchy || _visibility == null || _visibility.alpha <= 0f ||
                _screenReservationCanvas == null || !_screenReservationCanvas.isActiveAndEnabled)
                return null;
            var effectiveCanvas = _screenReservationCanvas.rootCanvas;
            if (effectiveCanvas == null || !effectiveCanvas.isActiveAndEnabled) return null;
            bool overlay = effectiveCanvas.renderMode == RenderMode.ScreenSpaceOverlay;
            var camera = overlay ? null : effectiveCanvas.worldCamera;
            if (!overlay && (camera == null || !camera.isActiveAndEnabled)) return null;

            _screenReservationRoot.GetWorldCorners(_screenReservationCorners);
            float left = float.PositiveInfinity, bottom = float.PositiveInfinity;
            float right = float.NegativeInfinity, top = float.NegativeInfinity;
            for (int i = 0; i < _screenReservationCorners.Length; i++)
            {
                var corner = _screenReservationCorners[i];
                if (!overlay && camera.WorldToScreenPoint(corner).z <= 0f) return null;
                var point = RectTransformUtility.WorldToScreenPoint(camera, corner);
                left = Mathf.Min(left, point.x); right = Mathf.Max(right, point.x);
                bottom = Mathf.Min(bottom, point.y); top = Mathf.Max(top, point.y);
            }
            return Rect.MinMaxRect(left, bottom, right, top);
        }

        public bool IsVisible => _isVisible;
        public string TrackedStableKey => _trackedStableKey;
        public string VisibleText => ((_title != null ? _title.text : string.Empty) + "\n" + (_progress != null ? _progress.text : string.Empty) + "\n" + (_hint != null ? _hint.text : string.Empty)).Trim();

        public void Show(ObjectiveJournalItem item)
        {
            EnsureBuilt();
            _trackedStableKey = item.StableKey;
            _title.text = string.IsNullOrEmpty(item.Title) ? FallbackTitle() : ObjectiveJournalSourceText.Shown(item.Title);
            _progress.text = string.IsNullOrEmpty(item.ProgressText) ? ObjectiveJournalSourceText.ShownState(item.State) : ObjectiveJournalSourceText.Shown(item.ProgressText);
            _hint.text = ObjectiveJournalSourceText.Shown(string.IsNullOrEmpty(item.NextHintText) ? item.Subtitle : item.NextHintText);
            gameObject.SetActive(true);
            if (_root != null)
            {
                _root.SetActive(true);
            }
            _isVisible = true;
            LateUpdate();
        }

        public void Hide()
        {
            EnsureBuilt();
            if (_root != null)
            {
                _root.SetActive(false);
            }
            _isVisible = false;
        }

        private void LateUpdate()
        {
            if (_visibility == null) return;
            float alpha = UiGameplayInputLock.IsBlocked ? 0f : 1f;
            if (_visibility.alpha != alpha) _visibility.alpha = alpha;
        }

        private void EnsureBuilt()
        {
            var rect = transform as RectTransform;
            if (rect != null)
            {
                rect.sizeDelta = new Vector2(Mathf.Max(DefaultSize.x, rect.sizeDelta.x), Mathf.Max(DefaultSize.y, rect.sizeDelta.y));
            }
            // This decoration is owned by the journal; central modal panels render above it.
            transform.SetAsFirstSibling();
            if (_root != null)
            {
                ((RectTransform)_root.transform).sizeDelta = rect != null ? rect.sizeDelta : DefaultSize;
                return;
            }

            _root = new GameObject("TrackedObjectiveHudRoot", typeof(RectTransform), typeof(ModernUiTileImage), typeof(CanvasGroup));
            _root.transform.SetParent(transform, false);
            _visibility = _root.GetComponent<CanvasGroup>();
            _visibility.interactable = false;
            _visibility.blocksRaycasts = false;
            var rootRect = (RectTransform)_root.transform;
            _screenReservationRoot = rootRect;
            RefreshScreenReservationCanvas();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.sizeDelta = rect != null && rect.sizeDelta != Vector2.zero ? rect.sizeDelta : DefaultSize;
            var tile = _root.GetComponent<ModernUiTileImage>();
            tile.SetRecipe(ModernUiRecipes.CommonPanel48);
            tile.Rebuild();

            var icon = new GameObject(IconName, typeof(RectTransform), typeof(Image), typeof(UrbanUiIcon));
            icon.transform.SetParent(rootRect, false);
            var iconRect = (RectTransform)icon.transform;
            iconRect.anchorMin = new Vector2(0f, 1f);
            iconRect.anchorMax = new Vector2(0f, 1f);
            iconRect.pivot = new Vector2(0f, 1f);
            iconRect.anchoredPosition = new Vector2(40f, -52f);
            iconRect.sizeDelta = new Vector2(80f, 80f);
            icon.GetComponent<Image>().raycastTarget = false;
            icon.GetComponent<Image>().preserveAspect = true;
            icon.GetComponent<UrbanUiIcon>().Configure("journal");

            _title = MakeText(rootRect, "TrackedObjectiveTitle", string.Empty, new Vector2(136f, -36f), new Vector2(-172f, 32f), 22, TextAnchor.UpperLeft);
            _progress = MakeText(rootRect, "TrackedObjectiveProgress", string.Empty, new Vector2(136f, -76f), new Vector2(-172f, 28f), 18, TextAnchor.UpperLeft);
            _hint = MakeText(rootRect, "TrackedObjectiveHint", string.Empty, new Vector2(136f, -110f), new Vector2(-172f, 54f), 18, TextAnchor.UpperLeft);
        }

        private static string FallbackTitle()
        {
            const string key = "ui.objectives.tracked";
            var translated = GameText.Get(key);
            return translated != key ? translated : (GameText.Language == "ko" ? "\uCD94\uC801 \uC911\uC778 \uBAA9\uD45C" : "Tracked objective");
        }

        private static Text MakeText(RectTransform parent, string name, string value, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = go.GetComponent<Text>();
            text.text = value;
            text.font = LifeUiFont.Shared;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color32(48, 71, 83, 255);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }
    }
}
