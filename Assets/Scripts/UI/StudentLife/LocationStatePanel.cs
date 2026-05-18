using Rootborn.Game.StudentLife;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.StudentLife
{
    public sealed class LocationStatePanel : MonoBehaviour
    {
        private GameObject _root;
        private Text _title;
        private Text _description;
        private Text _availability;
        private Text _unknownHint;

        public bool IsOpen => _root != null && _root.activeSelf;
        public LocationStateSummaryModel LastSummary { get; private set; }

        public static LocationStatePanel EnsureInScene(Canvas canvas)
        {
            var existing = Object.FindFirstObjectByType<LocationStatePanel>(FindObjectsInactive.Include);
            if (existing != null) return existing;
            var go = new GameObject("LocationStatePanel");
            go.transform.SetParent(canvas.transform, false);
            return go.AddComponent<LocationStatePanel>();
        }

        private void Awake()
        {
            BuildIfNeeded();
            Hide();
        }

        public void Show(LocationStateSummaryModel summary)
        {
            BuildIfNeeded();
            LastSummary = summary;
            _title.text = string.IsNullOrEmpty(summary.DisplayName) ? "Unknown Location State" : summary.DisplayName;
            _description.text = string.IsNullOrEmpty(summary.Description) ? summary.LocationId : summary.Description;
            _availability.text = FormatAvailability(summary);
            _unknownHint.text = summary.IsHidden ? "???" : string.Empty;
            _root.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }

        public string GetTextForTests()
        {
            BuildIfNeeded();
            return _title.text + "\n" + _description.text + "\n" + _availability.text + "\n" + _unknownHint.text;
        }

        private void BuildIfNeeded()
        {
            if (_root != null) return;
            _root = new GameObject("LocationStateRoot", typeof(RectTransform), typeof(ModernUiTileImage));
            _root.transform.SetParent(transform, false);
            var rt = (RectTransform)_root.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -20f);
            rt.sizeDelta = new Vector2(620f, 220f);
            var tile = _root.GetComponent<ModernUiTileImage>();
            tile.SetRecipe(ModernUiRecipes.CommonPanel);
            tile.Rebuild();

            _title = MakeText(rt, "LocationStateName", string.Empty, new Vector2(0f, 78f), new Vector2(540f, 30f), 19, TextAnchor.MiddleCenter);
            _description = MakeText(rt, "LocationStateDescription", string.Empty, new Vector2(0f, 42f), new Vector2(540f, 34f), 14, TextAnchor.MiddleCenter);
            _availability = MakeText(rt, "LocationStateAvailability", string.Empty, new Vector2(0f, -32f), new Vector2(540f, 98f), 13, TextAnchor.UpperLeft);
            _unknownHint = MakeText(rt, "LocationStateUnknownHint", string.Empty, new Vector2(248f, 78f), new Vector2(58f, 30f), 16, TextAnchor.MiddleCenter);
        }

        private static string FormatAvailability(LocationStateSummaryModel summary)
        {
            string text = "Activities: " + Join(summary.AvailableActivityIds);
            text += "\nNPCs: " + Join(summary.PresentNpcIds);
            text += "\nClues: " + Join(summary.ClueIds);
            text += "\nObjects: " + Join(summary.InteractableObjectIds);
            text += "\nPortals: " + Join(summary.PortalIds);
            text += "\nShop: " + Join(summary.ShopItemIds);
            return text;
        }

        private static string Join(string[] values)
        {
            if (values == null || values.Length == 0) return "none";
            return string.Join(", ", values);
        }

        private static Text MakeText(RectTransform parent, string name, string value, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
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
