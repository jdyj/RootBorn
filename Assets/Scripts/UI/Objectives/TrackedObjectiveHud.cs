using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.Objectives
{
    [DisallowMultipleComponent]
    public sealed class TrackedObjectiveHud : MonoBehaviour
    {
        private static readonly Vector2 DefaultSize = new Vector2(360f, 96f);
        private GameObject _root;
        private Text _title;
        private Text _progress;
        private Text _hint;
        private bool _isVisible;

        public bool IsVisible => _isVisible;
        public string VisibleText => ((_title != null ? _title.text : string.Empty) + "\n" + (_progress != null ? _progress.text : string.Empty) + "\n" + (_hint != null ? _hint.text : string.Empty)).Trim();

        public void Show(ObjectiveJournalItem item)
        {
            EnsureBuilt();
            _title.text = string.IsNullOrEmpty(item.Title) ? "Tracked objective" : item.Title;
            _progress.text = string.IsNullOrEmpty(item.ProgressText) ? item.State : item.ProgressText;
            _hint.text = string.IsNullOrEmpty(item.NextHintText) ? item.Subtitle : item.NextHintText;
            gameObject.SetActive(true);
            if (_root != null)
            {
                _root.SetActive(true);
            }
            _isVisible = true;
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

        private void EnsureBuilt()
        {
            if (_root != null)
            {
                return;
            }

            var rect = transform as RectTransform;
            if (rect != null && rect.sizeDelta == Vector2.zero)
            {
                rect.sizeDelta = DefaultSize;
            }

            _root = new GameObject("TrackedObjectiveHudRoot", typeof(RectTransform), typeof(ModernUiTileImage));
            _root.transform.SetParent(transform, false);
            var rootRect = (RectTransform)_root.transform;
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.sizeDelta = rect != null && rect.sizeDelta != Vector2.zero ? rect.sizeDelta : DefaultSize;
            var tile = _root.GetComponent<ModernUiTileImage>();
            tile.SetRecipe(ModernUiRecipes.CommonPanel48);
            tile.Rebuild();

            _title = MakeText(rootRect, "TrackedObjectiveTitle", string.Empty, new Vector2(18f, -14f), new Vector2(324f, 24f), 16, TextAnchor.UpperLeft);
            _progress = MakeText(rootRect, "TrackedObjectiveProgress", string.Empty, new Vector2(18f, -40f), new Vector2(324f, 22f), 13, TextAnchor.UpperLeft);
            _hint = MakeText(rootRect, "TrackedObjectiveHint", string.Empty, new Vector2(18f, -64f), new Vector2(324f, 20f), 13, TextAnchor.UpperLeft);
        }

        private static Text MakeText(RectTransform parent, string name, string value, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
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
            text.color = new Color(0.25f, 0.18f, 0.12f, 1f);
            text.raycastTarget = false;
            return text;
        }
    }
}
