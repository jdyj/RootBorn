using NUnit.Framework;
using Rootborn.UI.Objectives;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.Tests.EditMode.UI.Objectives
{
    public sealed class ObjectiveJournalPanelStrictScalingTests
    {
        [Test]
        public void ObjectiveJournalPanel_SelectedStableKeyMissingFallsBackToFirstSortedItem()
        {
            var host = new GameObject("ObjectiveJournalMissingStableKeyFallbackFixture", typeof(RectTransform));
            try
            {
                var panel = host.AddComponent<ObjectiveJournalPanel>();
                panel.SetItems("Quests", new[]
                {
                    new ObjectiveJournalItem("quest.alpha", "Quests", "Alpha Quest", "Alpha detail", "Active", "0 / 1", "Alpha next", false, 0),
                    new ObjectiveJournalItem("quest.target", "Quests", "Target Quest", "Target detail", "Active", "1 / 2", "Target next", false, 1),
                });
                panel.Show();
                panel.SelectCategory("Quests");
                panel.SelectItem(1);

                panel.SetItems("Quests", new[]
                {
                    new ObjectiveJournalItem("quest.beta", "Quests", "Beta Quest", "Beta detail", "Active", "0 / 2", "Beta next", false, 0),
                    new ObjectiveJournalItem("quest.gamma", "Quests", "Gamma Quest", "Gamma detail", "Active", "1 / 3", "Gamma next", false, 1),
                });

                Assert.AreEqual("Quests", panel.SelectedCategoryIdForTests);
                Assert.AreEqual("quest.beta", panel.SelectedStableKeyForTests, "When a selected stable key disappears, fallback must be deterministic and select the first sorted item instead of staying on the old row index.");
                Assert.AreEqual(0, panel.SelectedItemIndexForTests);
                StringAssert.Contains("Beta Quest", panel.VisibleText);
                Assert.IsFalse(panel.VisibleText.Contains("Gamma detail"), "Missing stable-key fallback must not silently jump to the old row index.");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ObjectiveJournalPanel_ExposesRuntimeInspectionStateForStrictPlayModeEvidence()
        {
            var host = new GameObject("ObjectiveJournalInspectionStateFixture", typeof(RectTransform));
            try
            {
                var panel = host.AddComponent<ObjectiveJournalPanel>();
                panel.SetItems("Quests", new[]
                {
                    new ObjectiveJournalItem("quest.alpha", "Quests", "Alpha Quest", "Alpha detail", "Active", "0 / 1", "Alpha next", false, 0),
                    new ObjectiveJournalItem("quest.target", "Quests", "Target Quest", "Target detail", "Active", "1 / 2", "Target next", false, 1),
                    new ObjectiveJournalItem("quest.extra", "Quests", "Extra Quest", "Extra detail", "Active", "2 / 3", "Extra next", false, 2),
                });
                panel.Show();
                panel.SelectCategory("Quests");
                panel.SelectItem(1);

                Assert.AreEqual("Quests", panel.SelectedCategoryIdForTests);
                Assert.AreEqual("quest.target", panel.SelectedStableKeyForTests);
                Assert.AreEqual(1, panel.SelectedItemIndexForTests);
                Assert.AreEqual(3, panel.VisibleRowCountForTests);
                Assert.AreEqual(3, host.GetComponentsInChildren<Button>(true).Length - 5, "Runtime inspection row count should match the actual objective row GameObjects, excluding category and track buttons.");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
