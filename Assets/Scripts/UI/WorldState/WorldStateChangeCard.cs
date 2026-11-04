using System.Collections.Generic;
using Rootborn.Game.Localization;
using Rootborn.UI.StudentLife;
using Rootborn.Game.Common;
using Rootborn.Game.WorldState;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.WorldState
{
    public sealed class WorldStateChangeCard : MonoBehaviour
    {
        private Text _title;
        private Text _body;
        private Text _badges;

        public string TitleTextForTests => _title != null ? _title.text : string.Empty;
        public string BodyTextForTests => _body != null ? _body.text : string.Empty;
        public string BadgeTextForTests => _badges != null ? _badges.text : string.Empty;

        public static WorldStateChangeCard Create(RectTransform parent, string name)
        {
            return Create(parent, name, new Vector2(300f, 104f));
        }

        public static WorldStateChangeCard Create(RectTransform parent, string name, Vector2 size)
        {
            var go = new GameObject(string.IsNullOrEmpty(name) ? "WorldStateChangeCard" : name, typeof(RectTransform), typeof(ModernUiTileImage));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;
            var tile = go.GetComponent<ModernUiTileImage>();
            tile.SetRecipe(ModernUiRecipes.CommonPanel);
            tile.Rebuild();
            return go.AddComponent<WorldStateChangeCard>();
        }

        private void Awake()
        {
            BuildIfNeeded();
        }

        public void Bind(WorldStateSummaryModel model)
        {
            BuildIfNeeded();
            _title.text = Humanize(model.DisplayName);
            _body.text = ComposeBody(model);
            _badges.text = ComposeBadges(model.Badges, model.Scope, model.SeenNotification);
        }

        private void BuildIfNeeded()
        {
            if (_title != null) return;
            var rt = (RectTransform)transform;
            bool wide = rt.rect.width >= 600f;
            float width = wide ? rt.rect.width - 64f : 260f;
            _title = MakeText(rt, "Title", new Vector2(0f, wide ? rt.rect.height / 2f - 40f : 32f), new Vector2(width, wide ? 36f : 24f), wide ? 26 : 15, TextAnchor.MiddleLeft);
            _body = MakeText(rt, "Body", new Vector2(0f, wide ? 4f : -2f), new Vector2(width, wide ? 76f : 42f), wide ? 20 : 12, TextAnchor.UpperLeft);
            _badges = MakeText(rt, "Badges", new Vector2(0f, wide ? -rt.rect.height / 2f + 48f : -38f), new Vector2(width, wide ? 26f : 18f), wide ? 18 : 11, TextAnchor.MiddleLeft);
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
            text.font = LifeUiFont.Shared;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color32(48, 71, 83, 255);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static string ComposeBody(WorldStateSummaryModel model)
        {
            string body = Humanize(model.Description);
            if (!string.IsNullOrEmpty(model.LocationName)) body += "\n" + Humanize(model.LocationName);
            if (!string.IsNullOrEmpty(model.NpcName)) body += " / " + Humanize(model.NpcName);
            if (!string.IsNullOrEmpty(model.NextAction)) body += "\n" + Humanize(model.NextAction);
            return body;
        }

        private static string ComposeBadges(WorldStateBadgeKind[] badges, WorldStateScopeKind scope, bool seen)
        {
            var labels = new List<string>();
            var distinct = new HashSet<string>(System.StringComparer.Ordinal);
            void Add(string key, string fallback)
            {
                string label = GameText.Get(key);
                if (label == key) label = fallback;
                if (distinct.Add(label)) labels.Add(label);
            }
            var state = seen ? WorldStateBadgeKind.Seen : WorldStateBadgeKind.New;
            Add("day-result.world-badge." + state, state.ToString());
            Add("day-result.world-scope." + scope, scope.ToString());
            if (badges != null)
                foreach (var badge in badges) Add("day-result.world-badge." + badge, badge.ToString());
            return string.Join(" · ", labels);
        }

        private static string Humanize(string key)
        {
            // Preserve a custom/unregistered key rather than losing all but its final word.
            return string.IsNullOrEmpty(key) ? string.Empty : GameText.Get(key);
        }
    }
}
