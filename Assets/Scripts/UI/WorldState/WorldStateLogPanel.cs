using Rootborn.Game.Localization;
using Rootborn.UI.StudentLife;
using Rootborn.Game.Common;
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
        private Text _scrollHint;

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
            _title.text = Ui("ui.world-log.title", "마을의 변화", "World Changes");
            _empty.text = Ui("ui.world-log.empty", "아직 기록된 변화가 없습니다.", "No world changes yet.");
            int count = summaries != null ? summaries.Length : 0;
            _cardsRoot.sizeDelta = new Vector2(1024f, Mathf.Max(592f, count * 224f));
            _cardsRoot.anchoredPosition = Vector2.zero;
            _scrollHint.text = Ui("ui.world-log.scroll", "마우스 휠로 지난 기록을 살펴보세요.", "Scroll to browse earlier changes.");
            _scrollHint.gameObject.SetActive(count * 224f > 592f);
            if (summaries == null || summaries.Length == 0)
            {
                _empty.gameObject.SetActive(true);
            }
            else
            {
                _empty.gameObject.SetActive(false);
                for (int i = 0; i < summaries.Length; i++)
                {
                    var card = WorldStateChangeCard.Create(_cardsRoot, "WorldStateLogCard_" + i, new Vector2(992f, 208f));
                    var cardRect = (RectTransform)card.transform;
                    cardRect.anchorMin = cardRect.anchorMax = new Vector2(.5f, 1f);
                    cardRect.pivot = new Vector2(.5f, 1f);
                    cardRect.anchoredPosition = new Vector2(0f, -i * 224f);
                    card.Bind(summaries[i]);
                }
            }

            gameObject.SetActive(true);
            _root.SetActive(true);
            transform.SetAsLastSibling();
            UiGameplayInputLock.Acquire(this);
        }

        public void Hide()
        {
            UiGameplayInputLock.Release(this);
            if (_root != null) _root.SetActive(false);
        }

        private void OnDisable() => UiGameplayInputLock.Release(this);
        private void OnDestroy() => UiGameplayInputLock.Release(this);

        private static string Ui(string key, string ko, string en)
        {
            string value = GameText.Get(key);
            return value == key ? (GameText.Language == "ko" ? ko : en) : value;
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
            rt.sizeDelta = new Vector2(1120f, 800f);
            var tile = _root.GetComponent<ModernUiTileImage>();
            tile.SetRecipe(ModernUiRecipes.CommonPanel);
            tile.Rebuild();

            var close = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(ModernUiTileImage));
            close.transform.SetParent(rt, false);
            var closeRect = (RectTransform)close.transform;
            closeRect.anchoredPosition = new Vector2(484f, 334f);
            closeRect.sizeDelta = new Vector2(56f, 56f);
            close.GetComponent<Image>().color = Color.clear;
            close.GetComponent<ModernUiTileImage>().SetRecipe(ModernUiRecipes.CommonPanel);
            close.GetComponent<ModernUiTileImage>().Rebuild();
            close.GetComponent<Button>().onClick.AddListener(Hide);
            MakeText(closeRect, "Label", "×", Vector2.zero, new Vector2(48f, 48f), 28, TextAnchor.MiddleCenter);
            var icon = new GameObject("HeaderIcon", typeof(RectTransform), typeof(Image), typeof(UrbanUiIcon));
            icon.transform.SetParent(rt, false);
            var iconRect = (RectTransform)icon.transform;
            iconRect.anchoredPosition = new Vector2(-488f, 332f);
            iconRect.sizeDelta = new Vector2(64f, 64f);
            icon.GetComponent<Image>().raycastTarget = false; icon.GetComponent<Image>().preserveAspect = true;
            icon.GetComponent<UrbanUiIcon>().Configure("journal");
            _title = MakeText(rt, "Title", Ui("ui.world-log.title", "마을의 변화", "World Changes"), new Vector2(0f, 332f), new Vector2(832f, 48f), 32, TextAnchor.MiddleCenter);
            _empty = MakeText(rt, "Empty", Ui("ui.world-log.empty", "아직 기록된 변화가 없습니다.", "No world changes yet."), new Vector2(0f, 160f), new Vector2(960f, 48f), 22, TextAnchor.MiddleCenter);
            var viewportObject = new GameObject("HistoryViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewportObject.transform.SetParent(rt, false);
            var viewport = (RectTransform)viewportObject.transform;
            viewport.anchoredPosition = new Vector2(0f, -40f);
            viewport.sizeDelta = new Vector2(1024f, 592f);
            viewportObject.GetComponent<Image>().color = Color.clear;
            var scroll = viewportObject.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32f;
            var cards = new GameObject("Cards", typeof(RectTransform));
            cards.transform.SetParent(viewport, false);
            _cardsRoot = (RectTransform)cards.transform;
            _cardsRoot.anchorMin = new Vector2(0.5f, 1f);
            _cardsRoot.anchorMax = new Vector2(0.5f, 1f);
            _cardsRoot.pivot = new Vector2(0.5f, 1f);
            _cardsRoot.anchoredPosition = Vector2.zero;
            _cardsRoot.sizeDelta = new Vector2(1024f, 592f);
            scroll.content = _cardsRoot;
            _scrollHint = MakeText(rt, "ScrollHint", string.Empty, new Vector2(0f, -352f), new Vector2(960f, 24f), 18, TextAnchor.MiddleCenter);
            _scrollHint.gameObject.SetActive(false);
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
            text.font = LifeUiFont.Shared;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color32(48, 71, 83, 255);
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
