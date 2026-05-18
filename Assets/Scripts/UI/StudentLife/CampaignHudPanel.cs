using Rootborn.Game.StudentLife;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.StudentLife
{
    public sealed class CampaignHudPanel : MonoBehaviour
    {
        private GameObject _root;
        private Text _direction;
        private Text _routes;
        private Text _progress;
        private Text _nextGuide;

        public bool IsOpen => _root != null && _root.activeSelf;

        public static CampaignHudPanel EnsureInScene(Canvas canvas)
        {
            var existing = Object.FindFirstObjectByType<CampaignHudPanel>(FindObjectsInactive.Include);
            if (existing != null) return existing;
            var go = new GameObject("CampaignHudPanel");
            go.transform.SetParent(canvas.transform, false);
            return go.AddComponent<CampaignHudPanel>();
        }

        private void Awake()
        {
            BuildIfNeeded();
        }

        public void Hide()
        {
            BuildIfNeeded();
            if (_root != null) _root.SetActive(false);
        }

        public void Show()
        {
            BuildIfNeeded();
            if (_root != null) _root.SetActive(true);
        }

        public void Refresh(CampaignHudSummary summary)
        {
            BuildIfNeeded();
            _direction.text = "Campaign\n" + summary.TodayDirection;
            _routes.text = "Routes\n" + summary.AvailableRoutesText;
            _progress.text = "Progress\n" + (string.IsNullOrEmpty(summary.CurrentProgressText) ? "No campaign objectives yet" : summary.CurrentProgressText);
            _nextGuide.text = string.IsNullOrEmpty(summary.NextGuideText) ? "Next campaign guide" : summary.NextGuideText;
            _root.SetActive(true);
        }

        private void BuildIfNeeded()
        {
            if (_root != null) return;
            _root = new GameObject("CampaignHudRoot", typeof(RectTransform), typeof(ModernUiTileImage));
            _root.transform.SetParent(transform, false);
            var rt = (RectTransform)_root.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(26f, -118f);
            rt.sizeDelta = new Vector2(420f, 260f);
            var tile = _root.GetComponent<ModernUiTileImage>();
            tile.SetRecipe(ModernUiRecipes.CommonPanel);
            tile.Rebuild();

            _direction = MakeText(rt, "CampaignDirection", string.Empty, new Vector2(20f, -18f), new Vector2(380f, 54f), 17, TextAnchor.UpperLeft);
            _routes = MakeText(rt, "CampaignRoutes", string.Empty, new Vector2(20f, -78f), new Vector2(380f, 72f), 14, TextAnchor.UpperLeft);
            _progress = MakeText(rt, "CampaignProgress", string.Empty, new Vector2(20f, -154f), new Vector2(380f, 58f), 14, TextAnchor.UpperLeft);
            _nextGuide = MakeText(rt, "CampaignNextGuide", string.Empty, new Vector2(20f, -216f), new Vector2(380f, 34f), 13, TextAnchor.UpperLeft);
        }

        private static Text MakeText(RectTransform parent, string name, string value, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
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
