using Rootborn.Game.WorldState;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.WorldState
{
    public sealed class WorldStateLogPanel : MonoBehaviour
    {
        private GameObject _root;
        private RectTransform _cardsRoot;
        private Text _title;
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

        public static WorldStateLogPanel EnsureInScene(Canvas canvas)
        {
            var existing = Object.FindFirstObjectByType<WorldStateLogPanel>(FindObjectsInactive.Include);
            if (existing != null) return existing;
            var go = new GameObject("WorldStateLogPanel");
            go.transform.SetParent(canvas.transform, false);
            return go.AddComponent<WorldStateLogPanel>();
        }

        private void Awake()
        {
            BuildIfNeeded();
            Hide();
        }

        public void Show(WorldStateSummaryModel[] summaries)
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
                    var card = WorldStateChangeCard.Create(_cardsRoot, "WorldStateLogCard_" + i);
                    ((RectTransform)card.transform).anchoredPosition = new Vector2(0f, -i * 112f);
                    card.Bind(summaries[i]);
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
            _root = new GameObject("WorldStateLogRoot", typeof(RectTransform), typeof(ModernUiTileImage));
            _root.transform.SetParent(transform, false);
            var rt = (RectTransform)_root.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(380f, 480f);
            var tile = _root.GetComponent<ModernUiTileImage>();
            tile.SetRecipe(ModernUiRecipes.CommonPanel);
            tile.Rebuild();

            _title = MakeText(rt, "Title", "World Changes", new Vector2(0f, 206f), new Vector2(320f, 28f), 20, TextAnchor.MiddleCenter);
            _empty = MakeText(rt, "Empty", "No world changes", new Vector2(0f, 140f), new Vector2(320f, 28f), 14, TextAnchor.MiddleCenter);
            var cards = new GameObject("Cards", typeof(RectTransform));
            cards.transform.SetParent(rt, false);
            _cardsRoot = (RectTransform)cards.transform;
            _cardsRoot.anchorMin = new Vector2(0.5f, 1f);
            _cardsRoot.anchorMax = new Vector2(0.5f, 1f);
            _cardsRoot.pivot = new Vector2(0.5f, 1f);
            _cardsRoot.anchoredPosition = new Vector2(0f, -62f);
            _cardsRoot.sizeDelta = new Vector2(320f, 360f);
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
