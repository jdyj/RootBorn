using Rootborn.Game.WorldState;
using Rootborn.UI.StudentLife;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.WorldState
{
    public static class StudentDayResultWorldStateExtensions
    {
        private const string HostName = "WorldStateDayResultCards";

        public static void ShowWorldStateChanges(this StudentDayResultPanel panel, WorldStateSummaryModel[] summaries)
        {
            if (panel == null) return;
            var host = EnsureHost(panel.transform);
            Clear(host);
            if (summaries == null) return;
            for (int i = 0; i < summaries.Length; i++)
            {
                var card = WorldStateChangeCard.Create(host, "WorldStateDayResultCard_" + i);
                ((RectTransform)card.transform).anchoredPosition = new Vector2(0f, -i * 112f);
                card.Bind(summaries[i]);
            }
        }

        public static int GetWorldStateCardCountForTests(this StudentDayResultPanel panel)
        {
            var host = FindHost(panel);
            return host != null ? host.childCount : 0;
        }

        public static string GetWorldStateTextForTests(this StudentDayResultPanel panel)
        {
            var host = FindHost(panel);
            if (host == null) return string.Empty;
            string text = string.Empty;
            var labels = host.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < labels.Length; i++) text += labels[i].text + "\n";
            return text;
        }

        private static RectTransform EnsureHost(Transform parent)
        {
            var existing = parent.Find(HostName) as RectTransform;
            if (existing != null) return existing;
            var go = new GameObject(HostName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -18f);
            rt.sizeDelta = new Vector2(320f, 240f);
            return rt;
        }

        private static RectTransform FindHost(StudentDayResultPanel panel)
        {
            if (panel == null) return null;
            return panel.transform.Find(HostName) as RectTransform;
        }

        private static void Clear(RectTransform host)
        {
            for (int i = host.childCount - 1; i >= 0; i--)
            {
                var child = host.GetChild(i).gameObject;
                if (Application.isPlaying) Object.Destroy(child);
                else Object.DestroyImmediate(child);
            }
        }
    }
}
