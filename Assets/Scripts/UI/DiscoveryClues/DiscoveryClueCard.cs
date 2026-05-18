using Rootborn.Game.DiscoveryClues;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.DiscoveryClues
{
    public sealed class DiscoveryClueCard : MonoBehaviour
    {
        private Text _title;
        private Text _body;
        private Text _footer;

        public string TextForTests => (_title != null ? _title.text : string.Empty) + "\n" + (_body != null ? _body.text : string.Empty) + "\n" + (_footer != null ? _footer.text : string.Empty);

        public static DiscoveryClueCard Create(RectTransform parent, string name)
        {
            var go = new GameObject(string.IsNullOrEmpty(name) ? "DiscoveryClueCard" : name, typeof(RectTransform), typeof(ModernUiTileImage));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(340f, 126f);
            var tile = go.GetComponent<ModernUiTileImage>();
            tile.SetRecipe(ModernUiRecipes.CommonPanel);
            tile.Rebuild();
            return go.AddComponent<DiscoveryClueCard>();
        }

        private void Awake()
        {
            BuildIfNeeded();
        }

        public void Bind(DiscoveryClueSummaryModel model)
        {
            BuildIfNeeded();
            _title.text = Humanize(model.DisplayName);
            _body.text = Humanize(model.HintText) + "\n" + Humanize(model.PublicText);
            _footer.text = model.SourceKind + " " + (model.Completed ? "Completed" : model.Seen ? "Seen" : "Hidden") + " " + model.HiddenText;
        }

        private void BuildIfNeeded()
        {
            if (_title != null) return;
            var rt = (RectTransform)transform;
            _title = MakeText(rt, "Title", new Vector2(0f, 42f), new Vector2(292f, 24f), 15, TextAnchor.MiddleLeft);
            _body = MakeText(rt, "Body", new Vector2(0f, 2f), new Vector2(292f, 54f), 12, TextAnchor.UpperLeft);
            _footer = MakeText(rt, "Footer", new Vector2(0f, -46f), new Vector2(292f, 22f), 11, TextAnchor.MiddleLeft);
        }

        private static Text MakeText(RectTransform parent, string name, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
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
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color(0.25f, 0.18f, 0.12f, 1f);
            text.raycastTarget = false;
            return text;
        }

        private static string Humanize(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            int dot = key.LastIndexOf('.');
            return dot >= 0 && dot + 1 < key.Length ? key.Substring(dot + 1) : key;
        }
    }
}
