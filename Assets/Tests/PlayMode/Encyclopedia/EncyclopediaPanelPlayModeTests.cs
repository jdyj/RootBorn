using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Encyclopedia;
using Rootborn.UI.Encyclopedia;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.TestTools;
using System.Collections;

namespace Rootborn.Tests.PlayMode.Encyclopedia
{
    public sealed class EncyclopediaPanelPlayModeTests
    {
        [UnityTest]
        public IEnumerator ENCYCLOPEDIA_E2E_003_004_005_006_009_PanelOpensSwitchesCategoriesShowsLockedUnlockedDetailsAndUsesStyle2()
        {
            var canvasGo = new GameObject("EncyclopediaCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var panelGo = new GameObject("EncyclopediaPanel", typeof(RectTransform));
            panelGo.transform.SetParent(canvasGo.transform, false);
            var rect = (RectTransform)panelGo.transform;
            rect.sizeDelta = new Vector2(820f, 560f);
            var panel = panelGo.AddComponent<EncyclopediaPanel>();

            var place = Category("ency.category.locations", "Locations", 0);
            var npc = Category("ency.category.npcs", "NPC", 1);
            var unlocked = Entry("ency.location.library", place, "Library", "Library details");
            var locked = Entry("ency.location.archive", place, "Archive", "Archive details");
            var npcEntry = Entry("ency.npc.guide", npc, "Guide", "Guide details");
            var progress = new EncyclopediaProgress("slot-a", "player-a");
            progress.TryUnlock(unlocked.Id, 1, 1L);
            progress.TryUnlock(npcEntry.Id, 1, 2L);

            panel.Bind(new EncyclopediaIndex(new[] { place, npc }, new[] { unlocked, locked, npcEntry }), progress);
            panel.Show();
            yield return null;

            Assert.IsTrue(panel.IsVisible, "ENCYCLOPEDIA-E2E-003 failed: the panel must open as an actual UI object.");
            Assert.GreaterOrEqual(panel.GetComponentsInChildren<ModernUiTileImage>(true).Sum(t => t.TileCount), 30, "ENCYCLOPEDIA-E2E-009 failed: Style2 tiled panel background is required.");
            AssertPanelContains(panel.transform, "Library", "ENCYCLOPEDIA-E2E-005 failed: unlocked entries must show their title.");
            AssertPanelContains(panel.transform, "???", "ENCYCLOPEDIA-E2E-005 failed: locked entries must show hidden state.");

            Click(FindButton(panel.transform, "Entry_" + unlocked.Id));
            yield return null;
            AssertPanelContains(panel.transform, "Library details", "ENCYCLOPEDIA-E2E-006 failed: selecting an unlocked row must show detail text.");
            Assert.IsFalse(progress.IsNew(unlocked.Id), "ENCYCLOPEDIA-E2E-008 failed: selecting a new entry should clear its new marker once.");

            Click(FindButton(panel.transform, "Category_" + npc.Id));
            yield return null;
            AssertPanelContains(panel.transform, "Guide", "ENCYCLOPEDIA-E2E-004 failed: category tabs must switch the list.");
            AssertPanelContains(panel.transform, "Guide details", "ENCYCLOPEDIA-E2E-006 failed: switched category should display unlocked entry details.");
        }

        private static EncyclopediaCategoryDefinition Category(string id, string display, int order)
        {
            var category = ScriptableObject.CreateInstance<EncyclopediaCategoryDefinition>();
            category.ConfigureForTests(id, display, order);
            return category;
        }

        private static EncyclopediaEntryDefinition Entry(string id, EncyclopediaCategoryDefinition category, string title, string description)
        {
            var entry = ScriptableObject.CreateInstance<EncyclopediaEntryDefinition>();
            entry.ConfigureForTests(id, category, title, "???", description, "Find clues", new[] { "Detail" }, Array.Empty<EncyclopediaUnlockConditionBase>());
            return entry;
        }

        private static Button FindButton(Transform root, string name)
        {
            var buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++) if (buttons[i].name == name) return buttons[i];
            Assert.Fail("Missing button " + name);
            return null;
        }

        private static void Click(Button button)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }
            button.onClick.Invoke();
        }

        private static void AssertPanelContains(Transform root, string expected, string message)
        {
            var texts = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (!string.IsNullOrEmpty(texts[i].text) && texts[i].text.Contains(expected)) return;
            }
            Assert.Fail(message + " Missing: " + expected);
        }
    }
}
