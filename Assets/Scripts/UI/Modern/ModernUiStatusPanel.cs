using System.Globalization;
using Rootborn.Game.Localization;
using Rootborn.Game.StudentLife;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.Modern
{
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class ModernUiStatusPanel : MonoBehaviour
    {
        private static readonly Vector2 DefaultSize = new Vector2(560f, 540f);
        private static readonly Color Ink = new Color32(48, 71, 83, 255);
        private readonly Text[] _labels = new Text[3];
        private readonly Text[] _values = new Text[3];
        private GameObject _owner;
        private StudentLifeProgressComponent _source;
        private Text _title, _dayPoints, _closeHint;
        private string _language;
        private bool _built, _isVisible;

        public bool IsVisible => _isVisible;
        public int TileCount
        {
            get
            {
                int count = 0;
                foreach (var tiles in GetComponentsInChildren<ModernUiTileImage>(true)) count += tiles.TileCount;
                return count;
            }
        }

        public void BindOwner(GameObject owner)
        {
            DetachProgress();
            _owner = owner;
            ResolveBinding();
        }

        public void Show()
        {
            EnsureBuilt();
            _isVisible = true;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            SubscribeAvailability();
            ResolveBinding();
        }

        public void Hide()
        {
            _isVisible = false;
            StudentLifeProgressComponent.AvailabilityChanged -= ResolveBinding;
            DetachProgress();
            gameObject.SetActive(false);
        }

        private void OnEnable() { SubscribeAvailability(); ResolveBinding(); }
        private void OnDisable()
        {
            StudentLifeProgressComponent.AvailabilityChanged -= ResolveBinding;
            DetachProgress();
            _isVisible = false;
        }
        private void OnDestroy()
        {
            StudentLifeProgressComponent.AvailabilityChanged -= ResolveBinding;
            DetachProgress();
        }
        private void Update()
        {
            if (_isVisible && _language != GameText.Language) RefreshReadout();
        }
        private void SubscribeAvailability()
        {
            StudentLifeProgressComponent.AvailabilityChanged -= ResolveBinding;
            if (isActiveAndEnabled) StudentLifeProgressComponent.AvailabilityChanged += ResolveBinding;
        }
        private void ResolveBinding()
        {
            // Resolve only the actor explicitly selected by the existing local inventory owner.
            var candidate = isActiveAndEnabled && _owner != null ? _owner.GetComponent<StudentLifeProgressComponent>() : null;
            if (candidate != null && !candidate.isActiveAndEnabled) candidate = null;
            if (!ReferenceEquals(candidate, _source))
            {
                DetachProgress();
                _source = candidate;
                if (_source != null)
                {
                    _source.TimeChanged += RefreshReadout;
                    _source.VitalsChanged += RefreshReadout;
                }
            }
            RefreshReadout();
        }
        private void DetachProgress()
        {
            if (_source != null)
            {
                _source.TimeChanged -= RefreshReadout;
                _source.VitalsChanged -= RefreshReadout;
            }
            _source = null;
        }

        private void RefreshReadout()
        {
            if (!_built) return;
            _language = GameText.Language;
            _title.text = Local("ui.status.title", "나의 상태", "My status");
            _labels[0].text = Local("ui.status.energy", "체력", "Energy");
            _labels[1].text = Local("ui.status.focus", "집중", "Focus");
            _labels[2].text = Local("ui.status.stress", "스트레스", "Stress");
            _closeHint.text = Local("ui.status.close-hint", "C 키로 닫기", "Press C to close");
            var progress = _source != null ? _source.Progress : null;
            if (progress == null)
            {
                _dayPoints.text = Local("ui.status.waiting", "상태를 불러오는 중", "Loading status");
                foreach (var value in _values) value.text = "--";
                return;
            }
            _dayPoints.text = string.Format(CultureInfo.CurrentCulture,
                Local("ui.status.day-points", "{0}일차 · 남은 활동 {1}", "Day {0} · Activity points left: {1}"),
                progress.CurrentDay, progress.TimePointsRemaining);
            _values[0].text = progress.Energy.ToString(CultureInfo.CurrentCulture);
            _values[1].text = progress.Focus.ToString(CultureInfo.CurrentCulture);
            _values[2].text = progress.Stress.ToString(CultureInfo.CurrentCulture);
        }

        private void EnsureBuilt()
        {
            if (_built) return;
            var rect = (RectTransform)transform;
            if (rect.sizeDelta.x < DefaultSize.x || rect.sizeDelta.y < DefaultSize.y) rect.sizeDelta = DefaultSize;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            var hit = GetComponent<Image>();
            if (hit == null) hit = gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;
            var frame = GetComponent<ModernUiTileImage>();
            if (frame == null) frame = gameObject.AddComponent<ModernUiTileImage>();
            frame.SetRecipe(ModernUiRecipes.CommonPanel);
            frame.Rebuild();
            float width = rect.rect.width;
            _title = MakeText(rect, "StatusTitle", new Vector2(32f, -32f), new Vector2(width - 160f, 44f), 30, TextAnchor.MiddleLeft);
            _dayPoints = MakeText(rect, "DayPoints", new Vector2(32f, -88f), new Vector2(width - 64f, 28f), 20, TextAnchor.MiddleLeft);
            var close = ModernUiPanelBuilder.CreateCommonPanel48Button(rect, "StatusCloseButton", Vector2.zero, new Vector2(48f, 48f));
            Place((RectTransform)close.transform, new Vector2(width - 80f, -28f), new Vector2(48f, 48f));
            var closeIcon = MakeIcon((RectTransform)close.transform, "Icon", new Vector2(8f, -8f), new Vector2(32f, 32f), "close");
            close.GetComponent<Button>().onClick.AddListener(Hide);
            string[] names = { "EnergyCard", "FocusCard", "StressCard" };
            string[] icons = { "energy", "focus", "stress" };
            for (int i = 0; i < names.Length; i++)
            {
                var card = new GameObject(names[i], typeof(RectTransform), typeof(ModernUiTileImage));
                card.transform.SetParent(rect, false);
                var cardRect = (RectTransform)card.transform;
                Place(cardRect, new Vector2(32f, -140f - i * 108f), new Vector2(width - 64f, 96f));
                var skin = card.GetComponent<ModernUiTileImage>();
                skin.SetRecipe(ModernUiRecipes.SlotPanel);
                skin.Rebuild();
                MakeIcon(cardRect, "Icon", new Vector2(20f, -24f), new Vector2(48f, 48f), icons[i]);
                _labels[i] = MakeText(cardRect, "Label", new Vector2(92f, -24f), new Vector2(width - 316f, 48f), 26, TextAnchor.MiddleLeft);
                _values[i] = MakeText(cardRect, "Value", new Vector2(width - 216f, -24f), new Vector2(132f, 48f), 36, TextAnchor.MiddleRight);
            }
            _closeHint = MakeText(rect, "CloseHint", new Vector2(32f, -rect.rect.height + 56f), new Vector2(width - 64f, 28f), 18, TextAnchor.MiddleRight);
            _built = true;
            RefreshReadout();
        }
        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
        private static Text MakeText(RectTransform parent, string name, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, position, size);
            var label = go.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = Ink;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }
        private static Image MakeIcon(RectTransform parent, string name, Vector2 position, Vector2 size, string key)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(UrbanUiIcon));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, position, size);
            go.GetComponent<UrbanUiIcon>().Configure(key);
            return go.GetComponent<Image>();
        }
        private static string Local(string key, string korean, string english)
        {
            string value = GameText.Get(key);
            return value != key ? value : GameText.Language == "ko" ? korean : english;
        }
    }
}
