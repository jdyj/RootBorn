using NUnit.Framework;
using Rootborn.UI.Objectives;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.EndToEnd.Support
{
    public static class ObjectiveJournalE2EDriver
    {
        public static System.Collections.IEnumerator SelectCategoryAndRow(ObjectiveJournalPanel panel, string categoryId, int rowIndex)
        {
            var categoryButton = PlayerFlowE2EDriver.FindButton(panel.transform, "ObjectiveCategory_" + categoryId);
            yield return PlayerFlowE2EDriver.ClickButton(categoryButton);
            var rowButton = PlayerFlowE2EDriver.FindButton(panel.transform, "ObjectiveRow_" + rowIndex);
            yield return PlayerFlowE2EDriver.ClickButton(rowButton);
        }

        public static Button FindRowButtonContaining(ObjectiveJournalPanel panel, string contains, string failurePrefix)
        {
            Assert.IsNotNull(panel, failurePrefix + " Objective Journal panel was missing.");
            var buttons = panel.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                var text = buttons[i].GetComponentInChildren<Text>(true);
                if (text != null && text.text.Contains(contains)) return buttons[i];
            }

            Assert.Fail(failurePrefix + " Missing Objective Journal row containing: " + contains + "\nVisible Journal Text:\n" + panel.VisibleText + "\n" + PlayerFlowE2EDriver.DescribeButtons(panel.transform));
            return null;
        }

        public static Button FindTrackButton(ObjectiveJournalPanel panel)
        {
            return PlayerFlowE2EDriver.FindButton(panel.transform, "ObjectiveTrackButton");
        }
    }
}