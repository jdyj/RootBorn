using Rootborn.Game.StudentLife;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.StudentLife
{
    public sealed class LocationStateLogPanel : MonoBehaviour
    {
        private GameObject _root;
        private RectTransform _cardsRoot;
        private Text _empty;

        public int CardCountForTests => _cardsRoot != null ? _cardsRoot.childCount : 0;
        public string TextForTests
        {
            get
            {
                if (_cardsRoot == null) return string.Empty;
                string text = string.Empty;
                var labels = _cardsRoot.GetComponentsInChildren<Text>(true);
                for (int i = 0; i < labels.Length; i++) text += labels[i].text + "\n";
                return text;
            }
        }

        public static LocationStateLogPanel EnsureInScene(Canvas canvas)
        {
            var root = canvas != null ? canvas.transform.Find("LocationStateLogPanel") : null;
            if (root != null)
            {
                var panel = root.GetComponent<LocationStateLogPanel>();
                if (panel != null) return panel;
            }
            var go = new GameObject("LocationStateLogPanel");
            if (canvas != null) go.transform.SetParent(canvas.transform, false);
            return go.AddComponent<LocationStateLogPanel>();
        }

        private void Awake()
        {
            BuildIfNeeded();
            Hide();
        }

        public void Show(LocationStateTodaySummarySaveData[] summaries)
        {
            BuildIfNeeded();
            ClearCards();
            if (summaries == null || summaries.Length == 0)
            {
                _empty.gameObject.SetActive(true);
            }
            else
            {
                _empty.gameObject.SetActive(false);
                for (int i = 0; i < summaries.Length; i++)
                {
                    var card = CreateCard(_cardsRoot, "LocationStateLogCard_" + i);
                    ((RectTransform)card.transform).anchoredPosition = new Vector2(0f, -i * 64f);
                    Bind(card, summaries[i]);
                }
            }

            _root.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }

        private void BuildIfNeeded()
        {
            if (_root != null) return;
            _root = new GameObject("LocationStateLogRoot", typeof(RectTransform), typeof(ModernUiTileImage));
            _root.transform.SetParent(transform, false);
            var rt = (RectTransform)_root.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(380f, 420f);
            var tile = _root.GetComponent<ModernUiTileImage>();
            tile.SetRecipe(ModernUiRecipes.CommonPanel);
            tile.Rebuild();

            MakeText(rt, "Title", "Location States", new Vector2(0f, 176f), new Vector2(320f, 28f), 20, TextAnchor.MiddleCenter);
            _empty = MakeText(rt, "Empty", "No location states discovered", new Vector2(0f, 126f), new Vector2(320f, 28f), 14, TextAnchor.MiddleCenter);
            var cards = new GameObject("Cards", typeof(RectTransform));
            cards.transform.SetParent(rt, false);
            _cardsRoot = (RectTransform)cards.transform;
            _cardsRoot.anchorMin = new Vector2(0.5f, 1f);
            _cardsRoot.anchorMax = new Vector2(0.5f, 1f);
            _cardsRoot.pivot = new Vector2(0.5f, 1f);
            _cardsRoot.anchoredPosition = new Vector2(0f, -66f);
            _cardsRoot.sizeDelta = new Vector2(320f, 310f);
        }

        private static GameObject CreateCard(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(320f, 56f);
            go.GetComponent<Image>().color = new Color(0.92f, 0.82f, 0.58f, 0.92f);
            MakeText(rt, "Title", string.Empty, new Vector2(0f, -15f), new Vector2(296f, 22f), 14, TextAnchor.MiddleLeft);
            MakeText(rt, "Detail", string.Empty, new Vector2(0f, -38f), new Vector2(296f, 20f), 12, TextAnchor.MiddleLeft);
            return go;
        }

        private static void Bind(GameObject card, LocationStateTodaySummarySaveData summary)
        {
            if (card == null || summary == null) return;
            var title = FindChild(card.transform, "Title");
            var detail = FindChild(card.transform, "Detail");
            string displayName = string.IsNullOrEmpty(summary.DisplayName) ? summary.StateId : summary.DisplayName;
            if (summary.Hidden) displayName = "??? " + displayName;
            if (title != null) title.GetComponent<Text>().text = displayName;
            if (detail != null) detail.GetComponent<Text>().text = summary.StateId + " | " + summary.LocationId + " | " + summary.TimeSlotId;
        }

        private static Text MakeText(RectTransform parent, string name, string value, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
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

        private static Transform FindChild(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var match = FindChild(root.GetChild(i), name);
                if (match != null) return match;
            }
            return null;
        }

        private void ClearCards()
        {
            if (_cardsRoot == null) return;
            for (int i = _cardsRoot.childCount - 1; i >= 0; i--)
            {
                var child = _cardsRoot.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
        }
    }
}
