using Rootborn.Game.WorldState;
using Rootborn.UI.StudentLife;
using UnityEngine;

namespace Rootborn.UI.WorldState
{
    public static class StudentDayResultWorldStateUsageExtensions
    {
        private const string HostName = "WorldStateUsageDayResultCards";

        public static void ShowWorldStateUsages(this StudentDayResultPanel panel, WorldStateUsageSummaryModel[] summaries)
        {
            if (panel == null) return;
            var host = EnsureHost(panel.transform);
            Clear(host);
            var values = summaries ?? System.Array.Empty<WorldStateUsageSummaryModel>();
            for (int i = 0; i < values.Length; i++)
            {
                var card = WorldStateUsageCard.Create(host, "WorldStateUsageDayResultCard_" + i);
                var rt = (RectTransform)card.transform;
                rt.anchoredPosition = new Vector2(-180f + (i % 2) * 360f, -18f - (i / 2) * 126f);
                card.Bind(values[i]);
            }
        }

        public static int GetWorldStateUsageCardCountForTests(this StudentDayResultPanel panel)
        {
            var host = FindHost(panel != null ? panel.transform : null);
            return host != null ? host.childCount : 0;
        }

        public static string GetWorldStateUsageTextForTests(this StudentDayResultPanel panel)
        {
            var host = FindHost(panel != null ? panel.transform : null);
            if (host == null) return string.Empty;
            string text = string.Empty;
            for (int i = 0; i < host.childCount; i++)
            {
                var card = host.GetChild(i).GetComponent<WorldStateUsageCard>();
                if (card != null) text += card.TextForTests + "\n";
            }
            return text;
        }

        private static RectTransform EnsureHost(Transform root)
        {
            var existing = FindHost(root);
            if (existing != null) return existing;
            var go = new GameObject(HostName, typeof(RectTransform));
            go.transform.SetParent(root, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -10f);
            rt.sizeDelta = new Vector2(740f, 260f);
            return rt;
        }

        private static RectTransform FindHost(Transform root)
        {
            if (root == null) return null;
            var found = root.Find(HostName);
            return found != null ? (RectTransform)found : null;
        }

        private static void Clear(RectTransform host)
        {
            if (host == null) return;
            for (int i = host.childCount - 1; i >= 0; i--) Object.DestroyImmediate(host.GetChild(i).gameObject);
        }
    }
}
