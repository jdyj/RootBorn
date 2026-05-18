using Rootborn.Game.StudentLife;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.StudentLife
{
    public static class StudentDayResultLocationStateExtensions
    {
        private const string HostName = "LocationStateDayResultCards";

        public static void ShowLocationStates(this StudentDayResultPanel panel, LocationStateTodaySummarySaveData[] summaries)
        {
            if (panel == null) return;
            var host = EnsureHost(panel.transform);
            Clear(host);
            if (summaries == null) return;
            for (int i = 0; i < summaries.Length; i++)
            {
                var card = CreateCard(host, "LocationStateDayResultCard_" + i);
                ((RectTransform)card.transform).anchoredPosition = new Vector2(0f, -i * 64f);
                Bind(card, summaries[i]);
            }
        }

        public static int GetLocationStateCardCountForTests(this StudentDayResultPanel panel)
        {
            var host = FindHost(panel);
            return host != null ? host.childCount : 0;
        }

        public static string GetLocationStateTextForTests(this StudentDayResultPanel panel)
        {
            var host = FindHost(panel);
            if (host == null) return string.Empty;
            string text = string.Empty;
            var labels = host.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < labels.Length; i++) text += labels[i].text + "\n";
            return text;
        }

        private static GameObject CreateCard(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(320f, 56f);
            go.GetComponent<Image>().color = new Color(0.92f, 0.82f, 0.58f, 0.92f);
            MakeText(rt, "Title", string.Empty, new Vector2(0f, -15f), new Vector2(296f, 22f), 14, TextAnchor.MiddleLeft);
            MakeText(rt, "Detail", string.Empty, new Vector2(0f, -38f), new Vector2(296f, 20f), 12, TextAnchor.MiddleLeft);
            return go;
        }

        private static void Bind(GameObject card, LocationStateTodaySummarySaveData summary)
        {
            if (card == null || summary == null) return;
            var title = FindChild(card.transform, "Title");
            var detail = FindChild(card.transform, "Detail");
            string displayName = string.IsNullOrEmpty(summary.DisplayName) ? summary.StateId : summary.DisplayName;
            if (title != null) title.GetComponent<Text>().text = displayName;
            if (detail != null) detail.GetComponent<Text>().text = summary.StateId + " | " + summary.LocationId + " | " + summary.TimeSlotId;
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
            rt.anchoredPosition = new Vector2(-250f, -316f);
            rt.sizeDelta = new Vector2(320f, 140f);
            return rt;
        }

        private static RectTransform FindHost(StudentDayResultPanel panel)
        {
            if (panel == null) return null;
            return panel.transform.Find(HostName) as RectTransform;
        }

        private static Text MakeText(RectTransform parent, string name, string value, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
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

        private static Transform FindChild(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var match = FindChild(root.GetChild(i), name);
                if (match != null) return match;
            }
            return null;
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
