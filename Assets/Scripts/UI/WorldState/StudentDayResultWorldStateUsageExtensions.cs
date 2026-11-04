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
            var host = panel.EnsureSummaryHost(HostName);
            StudentDayResultPanel.ClearSummaryHost(host);
            var values = summaries ?? System.Array.Empty<WorldStateUsageSummaryModel>();
            for (int i = 0; i < values.Length; i++)
            {
                var card = WorldStateUsageCard.Create(host, "WorldStateUsageDayResultCard_" + i);
                var rt = (RectTransform)card.transform;
                card.Bind(values[i]);
                panel.ReflowSummaryCard(rt);
                foreach (var label in card.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                {
                    label.fontSize = 20; label.verticalOverflow = VerticalWrapMode.Overflow;
                    label.horizontalOverflow = HorizontalWrapMode.Wrap; label.text = panel.ResolveText(label.text);
                }
            }
        }

        public static int GetWorldStateUsageCardCountForTests(this StudentDayResultPanel panel)
        {
            var host = (panel != null ? panel.FindSummaryHost(HostName) : null);
            return host != null ? host.childCount : 0;
        }

        public static string GetWorldStateUsageTextForTests(this StudentDayResultPanel panel)
        {
            var host = (panel != null ? panel.FindSummaryHost(HostName) : null);
            if (host == null) return string.Empty;
            string text = string.Empty;
            for (int i = 0; i < host.childCount; i++)
            {
                var card = host.GetChild(i).GetComponent<WorldStateUsageCard>();
                if (card != null) text += card.TextForTests + "\n";
            }
            return text;
        }

    }
}
