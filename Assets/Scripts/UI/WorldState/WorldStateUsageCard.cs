using Rootborn.Game.WorldState;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.WorldState
{
    public sealed class WorldStateUsageCard : MonoBehaviour
    {
        private Text _title;
        private Text _body;
        private Text _badges;

        public string TextForTests => (_title != null ? _title.text : string.Empty) + "\n" + (_body != null ? _body.text : string.Empty) + "\n" + (_badges != null ? _badges.text : string.Empty);

        public static WorldStateUsageCard Create(RectTransform parent, string name)
        {
            var go = new GameObject(string.IsNullOrEmpty(name) ? "WorldStateUsageCard" : name, typeof(RectTransform), typeof(ModernUiTileImage));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(330f, 118f);
            var tile = go.GetComponent<ModernUiTileImage>();
            tile.SetRecipe(ModernUiRecipes.CommonPanel);
            tile.Rebuild();
            return go.AddComponent<WorldStateUsageCard>();
        }

        private void Awake()
        {
            BuildIfNeeded();
        }

        public void Bind(WorldStateUsageSummaryModel model)
        {
            BuildIfNeeded();
            _title.text = Humanize(model.DisplayName);
            _body.text = Humanize(model.Description) + "\n" + Humanize(model.NextAction);
            _badges.text = ComposeFooter(model);
        }

        private void BuildIfNeeded()
        {
            if (_title != null) return;
            var rt = (RectTransform)transform;
            _title = MakeText(rt, "Title", new Vector2(0f, 38f), new Vector2(286f, 24f), 15, TextAnchor.MiddleLeft);
            _body = MakeText(rt, "Body", new Vector2(0f, 0f), new Vector2(286f, 48f), 12, TextAnchor.UpperLeft);
            _badges = MakeText(rt, "Badges", new Vector2(0f, -42f), new Vector2(286f, 24f), 11, TextAnchor.MiddleLeft);
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

        private static string ComposeFooter(WorldStateUsageSummaryModel model)
        {
            string text = model.IsAvailable ? "Available" : "Locked";
            text += model.IsUsed ? " Used" : " New";
            if (!string.IsNullOrEmpty(model.RepeatState)) text += " " + model.RepeatState;
            if (model.RewardSummaryIds != null)
            {
                for (int i = 0; i < model.RewardSummaryIds.Length; i++) if (!string.IsNullOrEmpty(model.RewardSummaryIds[i])) text += " " + model.RewardSummaryIds[i];
            }
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
