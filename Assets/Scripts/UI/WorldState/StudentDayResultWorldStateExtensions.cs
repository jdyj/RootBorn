using Rootborn.Game.StudentLife;
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
            var host = panel.EnsureSummaryHost(HostName);
            StudentDayResultPanel.ClearSummaryHost(host);
            host.gameObject.SetActive(summaries != null && summaries.Length > 0);
            if (summaries == null) return;
            for (int i = 0; i < summaries.Length; i++)
            {
                var card = WorldStateChangeCard.Create(host, "WorldStateDayResultCard_" + i);
                card.Bind(summaries[i]);
                panel.ReflowSummaryCard((RectTransform)card.transform);
                card.transform.Find("Title").GetComponent<Text>().text = panel.ResolveText(summaries[i].DisplayName);
                card.transform.Find("Body").GetComponent<Text>().text = panel.ResolveText(summaries[i].Description + "\n" + summaries[i].LocationName + " " + summaries[i].NpcName + "\n" + summaries[i].NextAction);
                var badges = new System.Text.StringBuilder(StudentDayResultPanel.Ui("world-scope." + summaries[i].Scope, summaries[i].Scope.ToString()));
                foreach (var badge in summaries[i].Badges) badges.Append(" · ").Append(StudentDayResultPanel.Ui("world-badge." + badge, badge.ToString()));
                card.transform.Find("Badges").GetComponent<Text>().text = badges.ToString();
            }
        }
        public static int GetWorldStateCardCountForTests(this StudentDayResultPanel panel)
        {
            var host = panel != null ? panel.FindSummaryHost(HostName) : null;
            return host != null ? host.childCount : 0;
        }
        public static string GetWorldStateTextForTests(this StudentDayResultPanel panel)
        {
            var host = panel != null ? panel.FindSummaryHost(HostName) : null;
            if (host == null) return string.Empty;
            var result = new System.Text.StringBuilder();
            foreach (var text in host.GetComponentsInChildren<Text>(true)) result.AppendLine(text.text);
            return result.ToString();
        }
    }
}
