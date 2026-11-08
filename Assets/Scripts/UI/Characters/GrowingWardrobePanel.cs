using System;
using System.Collections.Generic;
using Rootborn.Game.Characters.Growing;
using Rootborn.Game.Common;
using Rootborn.Game.Localization;
using Rootborn.UI.Modern;
using Rootborn.UI.StudentLife;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.Characters
{
    /// <summary>One wardrobe card: what to wear and how it looks (body plus layers in draw order).</summary>
    public sealed class WardrobeCard
    {
        public WardrobeCard(string id, string labelKey, GrowingCharacterSheetDefinition body, IReadOnlyList<GrowingCharacterSheetDefinition> layers, bool selected)
        {
            Id = id ?? string.Empty;
            LabelKey = labelKey;
            Body = body;
            Layers = layers ?? Array.Empty<GrowingCharacterSheetDefinition>();
            Selected = selected;
        }

        public string Id { get; }
        public string LabelKey { get; }
        public GrowingCharacterSheetDefinition Body { get; }
        public IReadOnlyList<GrowingCharacterSheetDefinition> Layers { get; }
        public bool Selected { get; }
    }

    /// <summary>A wardrobe row: one wearable slot (or the legacy outfit list) and its cards.</summary>
    public sealed class WardrobeSection
    {
        public WardrobeSection(string titleKey)
        {
            TitleKey = titleKey ?? string.Empty;
        }

        public string TitleKey { get; }
        public List<WardrobeCard> Cards { get; } = new List<WardrobeCard>();
    }

    /// <summary>Shows wardrobe rows of cards. Cards and rows are reused, so reopening creates nothing new.</summary>
    [DisallowMultipleComponent]
    public sealed class GrowingWardrobePanel : MonoBehaviour
    {
        private const float CardWidth = 170f;
        private const float CardHeight = 230f;
        private const float CardGap = 16f;
        private const int StandingRow = 0;
        private const int StandingColumn = 1;
        private const float RowTitleHeight = 32f;
        private const float RowGap = 20f;
        private const float PanelChrome = 120f;

        private sealed class Card
        {
            public GameObject Root;
            public Button Button;
            public Image Highlight;
            public Image Body;
            public readonly List<Image> Layers = new List<Image>();
            public Text Label;
        }

        private readonly List<Card> _cards = new List<Card>();
        private readonly List<Text> _rowTitles = new List<Text>();
        private RectTransform _cardRoot;
        private bool _built;
        private Action<string> _onChoose;

        public bool IsVisible => gameObject.activeSelf;

        public int VisibleCardCount
        {
            get
            {
                int count = 0;
                foreach (var card in _cards) if (card.Root.activeSelf) count++;
                return count;
            }
        }

        /// <summary>Legacy entry: one row of appearances that fit the current body.</summary>
        public void Show(IReadOnlyList<GrowingCharacterAppearanceDefinition> options, GrowingCharacterAppearanceDefinition current, Action<string> onChoose)
        {
            var section = new WardrobeSection(string.Empty);
            if (options != null)
                foreach (var look in options)
                    if (look != null) section.Cards.Add(new WardrobeCard(look.Id, look.DisplayNameKey, look.Body, new[] { look.Outfit, look.Head }, look == current));
            Show(new[] { section }, onChoose);
        }

        public void Show(IReadOnlyList<WardrobeSection> sections, Action<string> onChoose)
        {
            Build();
            _onChoose = onChoose;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            UiGameplayInputLock.Acquire(this);
            int rows = sections != null ? sections.Count : 0;
            float rowHeight = CardHeight + RowTitleHeight + RowGap;
            var rect = (RectTransform)transform;
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, Mathf.Max(rect.sizeDelta.y, PanelChrome + rows * rowHeight));
            float top = (rows * rowHeight) / 2f;
            int used = 0;
            for (int row = 0; row < rows; row++)
            {
                var section = sections[row];
                float rowCenter = top - row * rowHeight - RowTitleHeight - CardHeight / 2f;
                while (_rowTitles.Count <= row) _rowTitles.Add(MakeText(_cardRoot, "RowTitle_" + _rowTitles.Count, string.Empty, Vector2.zero, new Vector2(400f, RowTitleHeight), 20, new Vector2(0.5f, 0.5f)));
                var title = _rowTitles[row];
                title.gameObject.SetActive(section.TitleKey.Length > 0);
                title.text = section.TitleKey.Length > 0 ? GameText.Get(section.TitleKey) : string.Empty;
                ((RectTransform)title.transform).anchoredPosition = new Vector2(0f, rowCenter + CardHeight / 2f + RowTitleHeight / 2f);
                int count = section.Cards.Count;
                float start = -(count * CardWidth + Mathf.Max(0, count - 1) * CardGap) / 2f;
                for (int i = 0; i < count; i++)
                {
                    while (_cards.Count <= used) _cards.Add(MakeCard(_cards.Count));
                    var card = _cards[used++];
                    card.Root.SetActive(true);
                    Fill(card, section.Cards[i], start + i * (CardWidth + CardGap), rowCenter);
                }
            }

            for (int i = used; i < _cards.Count; i++) _cards[i].Root.SetActive(false);
            for (int i = rows; i < _rowTitles.Count; i++) _rowTitles[i].gameObject.SetActive(false);
        }

        public void Hide()
        {
            UiGameplayInputLock.Release(this);
            gameObject.SetActive(false);
        }

        private void OnDisable() => UiGameplayInputLock.Release(this);
        private void OnDestroy() => UiGameplayInputLock.Release(this);

        private void Fill(Card card, WardrobeCard model, float x, float y)
        {
            string id = model.Id;
            card.Root.name = "Outfit_" + id;
            ((RectTransform)card.Root.transform).anchoredPosition = new Vector2(x, y);
            card.Highlight.color = model.Selected ? new Color(0.95f, 0.72f, 0.28f, 1f) : new Color(0.93f, 0.87f, 0.74f, 1f);
            SetFrame(card.Body, model.Body);
            var rect = (RectTransform)card.Root.transform;
            while (card.Layers.Count < model.Layers.Count) card.Layers.Add(MakeLayer(rect, "Layer_" + card.Layers.Count));
            for (int i = 0; i < card.Layers.Count; i++) SetFrame(card.Layers[i], i < model.Layers.Count ? model.Layers[i] : null);
            card.Label.transform.SetAsLastSibling();
            card.Label.text = GameText.Get(string.IsNullOrEmpty(model.LabelKey) ? id : model.LabelKey);
            card.Button.onClick.RemoveAllListeners();
            card.Button.onClick.AddListener(() => _onChoose?.Invoke(id));
        }

        private static void SetFrame(Image image, GrowingCharacterSheetDefinition sheet)
        {
            var sprite = sheet != null ? sheet.GetFrame(StandingRow, StandingColumn) : null;
            image.sprite = sprite;
            image.enabled = sprite != null;
        }

        private void Build()
        {
            if (_built) return;
            _built = true;
            var root = (RectTransform)transform;
            var tiles = gameObject.GetComponent<ModernUiTileImage>();
            if (tiles == null) tiles = gameObject.AddComponent<ModernUiTileImage>();
            tiles.SetRecipe(ModernUiRecipes.CommonPanel);
            tiles.Rebuild();
            MakeText(root, "Title", GameText.Get("ui.wardrobe.title"), new Vector2(0f, -36f), new Vector2(400f, 44f), 28, new Vector2(0.5f, 1f));
            var close = MakeButton(root, "CloseButton", new Vector2(-40f, -36f), new Vector2(48f, 48f), new Vector2(1f, 1f));
            MakeText((RectTransform)close.transform, "Label", "×", Vector2.zero, new Vector2(48f, 48f), 28, new Vector2(0.5f, 0.5f));
            close.onClick.AddListener(Hide);
            var cards = new GameObject("Cards", typeof(RectTransform));
            cards.transform.SetParent(root, false);
            _cardRoot = (RectTransform)cards.transform;
            _cardRoot.anchorMin = _cardRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _cardRoot.anchoredPosition = new Vector2(0f, -24f);
        }

        private Card MakeCard(int index)
        {
            var button = MakeButton(_cardRoot, "OutfitCard_" + index, Vector2.zero, new Vector2(CardWidth, CardHeight), new Vector2(0.5f, 0.5f));
            var rect = (RectTransform)button.transform;
            rect.pivot = new Vector2(0f, 0.5f);
            var card = new Card
            {
                Root = button.gameObject,
                Button = button,
                Highlight = button.GetComponent<Image>(),
                Body = MakeLayer(rect, "Body"),
                Label = MakeText(rect, "Label", string.Empty, new Vector2(0f, -94f), new Vector2(CardWidth - 8f, 36f), 18, new Vector2(0.5f, 0.5f)),
            };
            return card;
        }

        private static Image MakeLayer(RectTransform parent, string layerName)
        {
            var go = new GameObject(layerName, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 16f);
            rect.sizeDelta = new Vector2(128f, 180f);
            var image = go.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private static Button MakeButton(RectTransform parent, string buttonName, Vector2 position, Vector2 size, Vector2 anchor)
        {
            var go = new GameObject(buttonName, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.color = new Color(0.93f, 0.87f, 0.74f, 1f);
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            return button;
        }

        private static Text MakeText(RectTransform parent, string textName, string value, Vector2 position, Vector2 size, int fontSize, Vector2 anchor)
        {
            var go = new GameObject(textName, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = go.GetComponent<Text>();
            text.text = value;
            text.font = LifeUiFont.Shared;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            text.color = new Color(0.28f, 0.19f, 0.12f, 1f);
            return text;
        }
    }
}
