using NUnit.Framework;
using Rootborn.Game.Dialogue;
using Rootborn.UI.Modern;
using Rootborn.UI.Quests;
using Rootborn.UI.StudentLife;
using Rootborn.Game.StudentLife;
using UnityEngine;
using UnityEngine.UI;
namespace Rootborn.Tests.EditMode.UI.Modern
{
    public sealed class UrbanUiSurfaceTests
    {
        [Test] public void InventoryManualSurfaces_UseSharedRendererAndPreserveTabSelection()
        {
            var go = new GameObject("Inventory", typeof(RectTransform));
            try
            {
                var panel = go.AddComponent<ModernUiInventoryPanel>(); panel.Show();
                foreach (string name in new[] { "Tab_0", "Tab_1", "Tab_2", "Tab_3", "SearchField", "DeleteButton", "FilterButton" })
                    Assert.That(go.transform.Find(name).GetComponent<ModernUiTileImage>(), Is.Not.Null, name);
                var first = go.transform.Find("Tab_0").GetComponent<Button>();
                var second = go.transform.Find("Tab_1").GetComponent<Button>();
                second.onClick.Invoke();
                Assert.That(second.colors.normalColor, Is.EqualTo(Color.white));
                Assert.That(first.colors.normalColor, Is.Not.EqualTo(Color.white));
                Assert.That(second.GetComponent<Image>().raycastTarget, Is.True);
                Assert.That(second.transform.Find("Icon").GetComponent<Image>().raycastTarget, Is.False);
            }
            finally { Object.DestroyImmediate(go); }
        }
        [Test] public void DialogueClose_UsesSharedSkinAndKeepsCloseAction()
        {
            var go = new GameObject("Dialogue", typeof(RectTransform));
            try
            {
                var panel = go.AddComponent<DialoguePanel>(); panel.Open(null, default);
                var close = go.transform.Find("CloseButton").GetComponent<Button>();
                Assert.That(close.GetComponent<ModernUiTileImage>(), Is.Not.Null);
                Assert.That(close.GetComponentInChildren<Text>().raycastTarget, Is.False);
                close.onClick.Invoke(); Assert.That(panel.IsOpen, Is.False);
            }
            finally { Object.DestroyImmediate(go); }
        }
        [Test] public void DialogueOpen_LeavesMoldingInsetsAndFitsThreeChoices()
        {
            var go = new GameObject("Dialogue", typeof(RectTransform));
            var dialogue = ScriptableObject.CreateInstance<DialogueDefinition>();
            var choice = ScriptableObject.CreateInstance<DialogueChoiceDefinition>();
            try
            {
                var root = (RectTransform)go.transform; root.sizeDelta = new Vector2(760f, 210f);
                dialogue.ConfigureForTests(new[] { "A conversation line" }, new[] { choice, choice, choice }, null);
                var panel = go.AddComponent<DialoguePanel>(); panel.Open(dialogue, default);
                var close = (RectTransform)go.transform.Find("CloseButton");
                Assert.That(close.rect.height, Is.GreaterThanOrEqualTo(48f));
                Assert.That(close.anchoredPosition.x, Is.LessThanOrEqualTo(-36f));
                Assert.That(close.anchoredPosition.y, Is.LessThanOrEqualTo(-36f));
                var host = (RectTransform)go.transform.Find("ChoiceButtons");
                Assert.That(host.anchoredPosition.y, Is.GreaterThanOrEqualTo(42f));
                Assert.That(host.sizeDelta.x, Is.LessThanOrEqualTo(-84f));
                Assert.That(host.rect.height, Is.GreaterThanOrEqualTo(168f));
                for (int i = 0; i < 3; i++)
                {
                    var button = (RectTransform)host.Find("ChoiceButton_" + i);
                    Assert.That(button.rect.height, Is.GreaterThanOrEqualTo(48f));
                    Assert.That(button.anchoredPosition.y, Is.EqualTo(-i * 60f));
                }
                var line = (RectTransform)go.transform.Find("Lines");
                Assert.That(line.offsetMin.x, Is.GreaterThanOrEqualTo(42f));
                Assert.That(line.offsetMax.y, Is.LessThanOrEqualTo(-42f));
                Assert.That(line.offsetMin.y, Is.GreaterThanOrEqualTo(42f + host.rect.height + 24f));
                Assert.That(root.rect.height, Is.GreaterThanOrEqualTo(360f));
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(choice); Object.DestroyImmediate(dialogue); }
        }
        [Test] public void DayResult_KeepsRendererEnabledForLateThemeAndSkinsFooter()
        {
            var canvas = new GameObject("Canvas", typeof(Canvas));
            try
            {
                var panel = StudentDayResultPanel.EnsureInScene(canvas.GetComponent<Canvas>());
                panel.Show(null, new StudentDaySummary(1, new string[0], new string[0], string.Empty));
                var root = panel.transform.Find("StudentDayResultRoot");
                Assert.That(root.GetComponent<ModernUiTileImage>().enabled, Is.True);
                Assert.That(root.Find("NextDayButton").GetComponent<ModernUiTileImage>(), Is.Not.Null);
            }
            finally { Object.DestroyImmediate(canvas); }
        }
    }
}
