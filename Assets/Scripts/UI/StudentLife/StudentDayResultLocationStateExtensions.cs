using Rootborn.Game.StudentLife;
using Rootborn.Game.WorldState;
using Rootborn.UI.StudentLife;
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
            var host = panel.EnsureSummaryHost(HostName);
            StudentDayResultPanel.ClearSummaryHost(host);
            host.gameObject.SetActive(summaries != null && summaries.Length > 0);
            if (summaries == null) return;
            for (int i = 0; i < summaries.Length; i++)
                if (summaries[i] != null) panel.AddSummaryRow(host, "LocationStateDayResultCard_" + i, LocationStatePresentation.RecordTitle(summaries[i]) + "\n" + LocationStatePresentation.RecordDetail(summaries[i]));
        }
        public static int GetLocationStateCardCountForTests(this StudentDayResultPanel panel)
        {
            var host = panel != null ? panel.FindSummaryHost(HostName) : null;
            return host != null ? host.childCount : 0;
        }
        public static string GetLocationStateTextForTests(this StudentDayResultPanel panel)
        {
            var host = panel != null ? panel.FindSummaryHost(HostName) : null;
            if (host == null) return string.Empty;
            var result = new System.Text.StringBuilder();
            foreach (var text in host.GetComponentsInChildren<Text>(true)) result.AppendLine(text.text);
            return result.ToString();
        }
    }
}
