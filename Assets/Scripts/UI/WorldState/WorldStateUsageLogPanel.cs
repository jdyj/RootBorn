using Rootborn.Game.WorldState;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.WorldState
{
    public sealed class WorldStateUsageLogPanel : MonoBehaviour
    {
        private GameObject _root;
        private RectTransform _cardsRoot;
        private Text _title;

        public int CardCountForTests => _cardsRoot != null ? _cardsRoot.childCount : 0;
        public string TextForTests
        {
            get
            {
                if (_cardsRoot == null) return string.Empty;
                string text = string.Empty;
                for (int i = 0; i < _cardsRoot.childCount; i++)
                {
                    var card = _cardsRoot.GetChild(i).GetComponent<WorldStateUsageCard>();
                    if (card != null) text += card.TextForTests + "\n";
                }
                return text;
            }
        }

        public static WorldStateUsageLogPanel EnsureInScene(Canvas canvas)
        {
            var root = canvas != null ? canvas.transform.Find("WorldStateUsageLogPanel") : null;
            if (root != null)
            {
                var panel = root.GetComponent<WorldStateUsageLogPanel>();
                if (panel != null) return panel;
            }

            var go = new GameObject("WorldStateUsageLogPanel");
            if (canvas != null) go.transform.SetParent(canvas.transform, false);
            return go.AddComponent<WorldStateUsageLogPanel>();
        }

        private void Awake()
        {
            BuildIfNeeded();
            Hide();
        }

        public void Show(WorldStateUsageSummaryModel[] summaries)
        {
            BuildIfNeeded();
            ClearCards();
            var values = summaries ?? System.Array.Empty<WorldStateUsageSummaryModel>();
            for (int i = 0; i < values.Length; i++)
            {
                var card = WorldStateUsageCard.Create(_cardsRoot, "WorldStateUsageLogCard_" + i);
                var rt = (RectTransform)card.transform;
                rt.anchoredPosition = new Vector2(0f, -64f - i * 126f);
                card.Bind(values[i]);
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
            _root = new GameObject("WorldStateUsageLogRoot", typeof(RectTransform), typeof(ModernUiTileImage));
            _root.transform.SetParent(transform, false);
            var rt = (RectTransform)_root.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(430f, 540f);
            var tile = _root.GetComponent<ModernUiTileImage>();
            tile.SetRecipe(ModernUiRecipes.CommonPanel);
            tile.Rebuild();
            _title = MakeText(rt, "Title", "World Usage", new Vector2(0f, 238f), new Vector2(360f, 32f), 22, TextAnchor.MiddleCenter);
            var cards = new GameObject("Cards", typeof(RectTransform));
            cards.transform.SetParent(rt, false);
            _cardsRoot = (RectTransform)cards.transform;
            _cardsRoot.anchorMin = new Vector2(0.5f, 1f);
            _cardsRoot.anchorMax = new Vector2(0.5f, 1f);
            _cardsRoot.pivot = new Vector2(0.5f, 1f);
            _cardsRoot.anchoredPosition = Vector2.zero;
            _cardsRoot.sizeDelta = new Vector2(360f, 460f);
        }

        private void ClearCards()
        {
            if (_cardsRoot == null) return;
            for (int i = _cardsRoot.childCount - 1; i >= 0; i--) Object.DestroyImmediate(_cardsRoot.GetChild(i).gameObject);
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
