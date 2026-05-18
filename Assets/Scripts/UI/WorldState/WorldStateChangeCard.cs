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
            var go = new GameObject(string.IsNullOrEmpty(name) ? "WorldStateChangeCard" : name, typeof(RectTransform), typeof(ModernUiTileImage));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(300f, 104f);
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
            _title = MakeText(rt, "Title", new Vector2(0f, 32f), new Vector2(260f, 24f), 15, TextAnchor.MiddleLeft);
            _body = MakeText(rt, "Body", new Vector2(0f, -2f), new Vector2(260f, 42f), 12, TextAnchor.UpperLeft);
            _badges = MakeText(rt, "Badges", new Vector2(0f, -38f), new Vector2(260f, 18f), 11, TextAnchor.MiddleLeft);
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
            string text = seen ? WorldStateBadgeKind.Seen.ToString() : WorldStateBadgeKind.New.ToString();
            text += " " + scope;
            if (badges == null) return text;
            for (int i = 0; i < badges.Length; i++)
            {
                string badge = badges[i].ToString();
                if (text.Contains(badge)) continue;
                text += " " + badge;
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
