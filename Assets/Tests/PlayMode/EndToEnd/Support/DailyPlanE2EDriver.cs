using System.Text;
using NUnit.Framework;
using Rootborn.UI.StudentLife;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.EndToEnd.Support
{
    public static class DailyPlanE2EDriver
    {
        public static Button FindRouteButton(DailyPlanPanel panel, string label, string failurePrefix)
        {
            Assert.IsNotNull(panel, failurePrefix + " Daily Plan panel was missing.");
            for (int i = 0; i < panel.RouteRowCountForTests; i++)
            {
                var button = panel.GetRouteButtonForTests(i);
                if (button == null) continue;
                var text = button.GetComponentInChildren<Text>(true);
                if (text != null && text.text == label) return button;
            }

            Assert.Fail(failurePrefix + " Missing Daily Plan route row: " + label + "\n" + DescribeRouteRows(panel));
            return null;
        }

        public static string DescribeRouteRows(DailyPlanPanel panel)
        {
            if (panel == null) return "Visible Daily Plan routes: <panel missing>";
            var builder = new StringBuilder();
            builder.Append("Visible Daily Plan routes:");
            if (panel.RouteRowCountForTests == 0)
            {
                builder.Append(" <none>");
                return builder.ToString();
            }

            for (int i = 0; i < panel.RouteRowCountForTests; i++)
            {
                var button = panel.GetRouteButtonForTests(i);
                if (button == null)
                {
                    builder.Append("\n- [");
                    builder.Append(i);
                    builder.Append("] <button missing>");
                    continue;
                }

                var text = button.GetComponentInChildren<Text>(true);
                builder.Append("\n- [");
                builder.Append(i);
                builder.Append("] ");
                builder.Append(button.name);
                builder.Append(" active=");
                builder.Append(button.gameObject.activeInHierarchy);
                builder.Append(" interactable=");
                builder.Append(button.interactable);
                if (text != null && !string.IsNullOrEmpty(text.text))
                {
                    builder.Append(" text='");
                    builder.Append(text.text);
                    builder.Append("'");
                }
            }

            return builder.ToString();
        }
    }
}