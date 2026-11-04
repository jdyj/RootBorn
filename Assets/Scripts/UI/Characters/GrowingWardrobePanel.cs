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
    /// <summary>Lists the outfits that fit the current body. Cards are reused, so reopening creates nothing new.</summary>
    [DisallowMultipleComponent]
    public sealed class GrowingWardrobePanel : MonoBehaviour
    {
        private const float CardWidth = 170f;
        private const float CardHeight = 230f;
        private const float CardGap = 16f;
        private const int StandingRow = 0;
        private const int StandingColumn = 1;

        private sealed class Card
        {
            public GameObject Root;
            public Button Button;
            public Image Highlight;
            public Image Body;
            public Image Outfit;
            public Image Head;
            public Text Label;
        }

        private readonly List<Card> _cards = new List<Card>();
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

        public void Show(IReadOnlyList<GrowingCharacterAppearanceDefinition> options, GrowingCharacterAppearanceDefinition current, Action<string> onChoose)
        {
            Build();
            _onChoose = onChoose;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            UiGameplayInputLock.Acquire(this);
            int count = options != null ? options.Count : 0;
            while (_cards.Count < count) _cards.Add(MakeCard(_cards.Count));
            float start = -(count * CardWidth + Mathf.Max(0, count - 1) * CardGap) / 2f;
            for (int i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                bool used = i < count;
                card.Root.SetActive(used);
                if (used) Fill(card, options[i], options[i] == current, start + i * (CardWidth + CardGap));
            }
        }

        public void Hide()
        {
            UiGameplayInputLock.Release(this);
            gameObject.SetActive(false);
        }

        private void OnDisable() => UiGameplayInputLock.Release(this);
        private void OnDestroy() => UiGameplayInputLock.Release(this);

        private void Fill(Card card, GrowingCharacterAppearanceDefinition look, bool selected, float x)
        {
            string id = look.Id;
            card.Root.name = "Outfit_" + id;
            ((RectTransform)card.Root.transform).anchoredPosition = new Vector2(x, 0f);
            card.Highlight.color = selected ? new Color(0.95f, 0.72f, 0.28f, 1f) : new Color(0.93f, 0.87f, 0.74f, 1f);
            SetFrame(card.Body, look.Body);
            SetFrame(card.Outfit, look.Outfit);
            SetFrame(card.Head, look.Head);
            card.Label.text = GameText.Get(string.IsNullOrEmpty(look.DisplayNameKey) ? id : look.DisplayNameKey);
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
                Outfit = MakeLayer(rect, "Outfit"),
                Head = MakeLayer(rect, "Head"),
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
