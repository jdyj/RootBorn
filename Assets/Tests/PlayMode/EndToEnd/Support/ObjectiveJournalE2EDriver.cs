using Rootborn.UI.Objectives;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.EndToEnd.Support
{
    public static class ObjectiveJournalE2EDriver
    {
        public static void SelectCategoryAndRow(ObjectiveJournalPanel panel, string categoryId, int rowIndex)
        {
            var categoryButton = PlayerFlowE2EDriver.FindButton(panel.transform, "ObjectiveCategory_" + categoryId);
            categoryButton.onClick.Invoke();
            var rowButton = PlayerFlowE2EDriver.FindButton(panel.transform, "ObjectiveRow_" + rowIndex);
            rowButton.onClick.Invoke();
        }

        public static Button FindTrackButton(ObjectiveJournalPanel panel)
        {
            return PlayerFlowE2EDriver.FindButton(panel.transform, "ObjectiveTrackButton");
        }
    }
}