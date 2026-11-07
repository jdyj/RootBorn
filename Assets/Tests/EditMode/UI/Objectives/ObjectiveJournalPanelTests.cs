using Rootborn.Game.Localization;
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rootborn.UI.Modern;
using Rootborn.UI.Objectives;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.Tests.EditMode.UI.Objectives
{
    public sealed class ObjectiveJournalPanelTests
    {
        [Test]
        public void ObjectiveJournalPanel_ShowBuildsSingleShellWithPlainRailListAndDetailContent()
        {
            var panelType = Type.GetType("Rootborn.UI.Objectives.ObjectiveJournalPanel, Rootborn.UI");
            Assert.IsNotNull(panelType, "Missing Rootborn.UI.Objectives.ObjectiveJournalPanel.");

            var host = new GameObject("ObjectiveJournalPanelFixture", typeof(RectTransform));
            try
            {
                var panel = (Component)host.AddComponent(panelType);
                panelType.GetMethod("Show", BindingFlags.Instance | BindingFlags.Public).Invoke(panel, null);

                Assert.IsTrue((bool)panelType.GetProperty("IsVisible").GetValue(panel));
                AssertChildHasTiles(host.transform, "ObjectiveJournalRoot");
                Assert.AreEqual(1, CountCommonPanel48(host), "Objective Journal should use exactly one CommonPanel48: the outer root shell.");
                var rootShell = host.transform.Find("ObjectiveJournalRoot");
                Assert.IsNotNull(rootShell, "Objective content must live inside ObjectiveJournalRoot.");
                AssertPlainChild(rootShell, "ObjectiveCategoryRail");
                AssertPlainChild(rootShell, "ObjectiveList");
                AssertPlainChild(rootShell, "ObjectiveDetail");

                string visibleText = (string)panelType.GetProperty("VisibleText").GetValue(panel);
                StringAssert.Contains("Goals", visibleText);
                StringAssert.Contains("Quests", visibleText);
                StringAssert.Contains("Campaign", visibleText);
                StringAssert.Contains("Hints", visibleText);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ObjectiveJournalPanel_TranslatesKeysFromAnySourceBeforeShowingThem()
        {
            var host = new GameObject("ObjectiveJournalKeyFixture", typeof(RectTransform));
            var previous = Rootborn.Game.Localization.GameText.Language;
            Rootborn.Game.Common.GameDataRegistryProvider.SetOverrideForTests(UnityEditor.AssetDatabase.LoadAssetAtPath<Rootborn.Game.Common.GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset"));
            try
            {
                Rootborn.Game.Localization.GameText.Language = "ko";
                var panel = host.AddComponent<ObjectiveJournalPanel>();
                panel.SetItems("Goals", new[] { new ObjectiveJournalItem("k", "Goals", "objective.daily.choose.title", "tutorial.day1", "InProgress", "plain progress", "opening.day.1.next", false, 1) });
                panel.Show();
                panel.SelectCategory("Goals");
                var shown = string.Join("\n", System.Linq.Enumerable.Select(host.GetComponentsInChildren<UnityEngine.UI.Text>(true), t => t.text));
                foreach (string raw in new[] { "objective.daily.choose.title", "tutorial.day1", "InProgress", "opening.day.1.next" })
                    StringAssert.DoesNotContain(raw, shown);
                StringAssert.Contains("plain progress", shown);
                StringAssert.Contains("진행 중", shown);
            }
            finally
            {
                Rootborn.Game.Localization.GameText.Language = previous;
                Rootborn.Game.Common.GameDataRegistryProvider.ClearOverrideForTests();
                Rootborn.Game.Common.GameDataRegistryProvider.ResetCacheForTests();
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ObjectiveJournalPanel_RowButtonClickUpdatesSelectedDetail()
        {
            var host = new GameObject("ObjectiveJournalRowClickFixture", typeof(RectTransform));
            try
            {
                var panel = host.AddComponent<ObjectiveJournalPanel>();
                panel.SetItems("Quests", new[]
                {
                    new ObjectiveJournalItem("quest.first", "Quests", "First Quest", "First detail", "Active", "0 / 1", "Do first", false, 0),
                    new ObjectiveJournalItem("quest.second", "Quests", "Second Quest", "Second detail", "Active", "1 / 2", "Do second", false, 1),
                });
                panel.Show();
                panel.SelectCategory("Quests");

                var secondRow = host.transform.Find("ObjectiveJournalRoot/ObjectiveList/ObjectiveListViewport/ObjectiveListContent/ObjectiveRow_1")
                    ?? host.transform.Find("ObjectiveJournalRoot/ObjectiveList/ObjectiveRow_1");
                Assert.IsNotNull(secondRow, "Second objective row must be built.");
                var button = secondRow.GetComponent<Button>();
                Assert.IsNotNull(button, "Objective row must be a real Button so player UI click can select it.");

                button.onClick.Invoke();

                StringAssert.Contains("Second Quest", panel.VisibleText);
                StringAssert.Contains("Second detail", panel.VisibleText);
                StringAssert.Contains("1 / 2", panel.VisibleText);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ObjectiveJournalPanel_SelectedStableKeySurvivesRefreshReorder()
        {
            var host = new GameObject("ObjectiveJournalStableSelectionFixture", typeof(RectTransform));
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
                    new ObjectiveJournalItem("quest.target", "Quests", "Target Quest", "Target detail after refresh", "Active", "2 / 3", "Target next after refresh", false, 0),
                    new ObjectiveJournalItem("quest.alpha", "Quests", "Alpha Quest", "Alpha detail after refresh", "Active", "0 / 1", "Alpha next", false, 1),
                });

                StringAssert.Contains("Target Quest", panel.VisibleText);
                StringAssert.Contains("Target detail after refresh", panel.VisibleText);
                StringAssert.Contains("2 / 3", panel.VisibleText);
                Assert.IsFalse(panel.VisibleText.Contains("Alpha detail after refresh"), "Refresh reorder must not move selection to the same index when the stable key still exists.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ObjectiveJournalPanel_TrackedSelectionPersistsSameStableKeyAfterRefreshReorder()
        {
            var root = new GameObject("ObjectiveJournalTrackedReorderFixture", typeof(RectTransform));
            try
            {
                var panel = root.AddComponent<ObjectiveJournalPanel>();
                var hudGo = new GameObject("TrackedObjectiveHud", typeof(RectTransform));
                hudGo.transform.SetParent(root.transform, false);
                var hud = hudGo.AddComponent<TrackedObjectiveHud>();
                panel.BindTrackedHud(hud);
                panel.SetItems("Quests", new[]
                {
                    new ObjectiveJournalItem("quest.alpha", "Quests", "Alpha Quest", string.Empty, "Active", "0 / 1", "Alpha next", false, 0),
                    new ObjectiveJournalItem("quest.target", "Quests", "Target Quest", string.Empty, "Active", "1 / 2", "Target next", false, 1),
                });
                panel.Show();
                panel.SelectCategory("Quests");
                panel.SelectItem(1);

                panel.SetItems("Quests", new[]
                {
                    new ObjectiveJournalItem("quest.target", "Quests", "Target Quest", string.Empty, "Active", "2 / 3", "Target next after refresh", false, 0),
                    new ObjectiveJournalItem("quest.alpha", "Quests", "Alpha Quest", string.Empty, "Active", "0 / 1", "Alpha next", false, 1),
                });
                panel.TrackSelectedItem();

                Assert.IsTrue(hud.IsVisible);
                StringAssert.Contains("Target Quest", hud.VisibleText);
                StringAssert.Contains("2 / 3", hud.VisibleText);
                Assert.IsFalse(hud.VisibleText.Contains("Alpha Quest"), "Track after refresh must keep the selected stable key, not the old row index.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [TestCase("en", "No objectives in this category")]
        [TestCase("ko", "이 분류에는 아직 기록이 없어요.")]
        public void ObjectiveJournalPanel_EmptyCategoryShowsExplicitEmptyText(string language, string expectedText)
        {
            string previousLanguage = GameText.Language;
            GameText.Language = language;
            var host = new GameObject("ObjectiveJournalEmptyCategoryFixture", typeof(RectTransform));
            try
            {
                var panel = host.AddComponent<ObjectiveJournalPanel>();
                panel.SetItems("Hints", Array.Empty<ObjectiveJournalItem>());
                panel.Show();
                panel.SelectCategory("Hints");

                StringAssert.Contains(expectedText, panel.VisibleText);
            }
            finally
            {
                GameText.Language = previousLanguage;
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ObjectiveJournalPanel_RepeatedRefreshDoesNotDuplicateRows()
        {
            var host = new GameObject("ObjectiveJournalRefreshRowsFixture", typeof(RectTransform));
            try
            {
                var panel = host.AddComponent<ObjectiveJournalPanel>();
                panel.SetItems("Quests", new[]
                {
                    new ObjectiveJournalItem("quest.one", "Quests", "One", string.Empty, "Active", "0 / 1", string.Empty, false, 0),
                    new ObjectiveJournalItem("quest.two", "Quests", "Two", string.Empty, "Active", "0 / 1", string.Empty, false, 1),
                });
                panel.Show();
                panel.SelectCategory("Quests");
                panel.SetItems("Quests", new[]
                {
                    new ObjectiveJournalItem("quest.one", "Quests", "One", string.Empty, "Active", "0 / 1", string.Empty, false, 0),
                    new ObjectiveJournalItem("quest.two", "Quests", "Two", string.Empty, "Active", "0 / 1", string.Empty, false, 1),
                });

                Assert.AreEqual(2, CountObjectiveRows(host.transform), "Refreshing Objective Journal rows must replace stale row GameObjects instead of stacking duplicates.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ObjectiveJournalPanel_ManyRowsAreHostedInScrollableList()
        {
            var host = new GameObject("ObjectiveJournalManyRowsFixture", typeof(RectTransform));
            try
            {
                var panel = host.AddComponent<ObjectiveJournalPanel>();
                var many = new ObjectiveJournalItem[18];
                for (int i = 0; i < many.Length; i++)
                {
                    many[i] = new ObjectiveJournalItem("quest." + i, "Quests", "Quest " + i, string.Empty, "Active", i + " / 18", string.Empty, false, i);
                }

                panel.SetItems("Quests", many);
                panel.Show();
                panel.SelectCategory("Quests");

                var list = host.transform.Find("ObjectiveJournalRoot/ObjectiveList");
                Assert.IsNotNull(list, "Objective Journal must build a list area.");
                Assert.IsNotNull(list.GetComponent<ScrollRect>(), "Large objective categories must be scrollable instead of placing rows outside the visible list area.");
                Assert.AreEqual(18, CountObjectiveRows(host.transform));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ObjectiveJournalPanel_TrackSelectedItemUpdatesSingleTrackedHud()
        {
            var panelType = Type.GetType("Rootborn.UI.Objectives.ObjectiveJournalPanel, Rootborn.UI");
            var hudType = Type.GetType("Rootborn.UI.Objectives.TrackedObjectiveHud, Rootborn.UI");
            Assert.IsNotNull(panelType, "Missing Rootborn.UI.Objectives.ObjectiveJournalPanel.");
            Assert.IsNotNull(hudType, "Missing Rootborn.UI.Objectives.TrackedObjectiveHud.");

            var root = new GameObject("ObjectiveTrackingFixture", typeof(RectTransform));
            try
            {
                var hudGo = new GameObject("TrackedObjectiveHud", typeof(RectTransform));
                hudGo.transform.SetParent(root.transform, false);
                var hud = (Component)hudGo.AddComponent(hudType);

                var panelGo = new GameObject("ObjectiveJournalPanel", typeof(RectTransform));
                panelGo.transform.SetParent(root.transform, false);
                var panel = (Component)panelGo.AddComponent(panelType);

                panelType.GetMethod("BindTrackedHud", BindingFlags.Instance | BindingFlags.Public).Invoke(panel, new object[] { hud });
                panelType.GetMethod("Show", BindingFlags.Instance | BindingFlags.Public).Invoke(panel, null);
                panelType.GetMethod("SelectCategory", BindingFlags.Instance | BindingFlags.Public).Invoke(panel, new object[] { "Quests" });
                panelType.GetMethod("SelectItem", BindingFlags.Instance | BindingFlags.Public).Invoke(panel, new object[] { 0 });
                panelType.GetMethod("TrackSelectedItem", BindingFlags.Instance | BindingFlags.Public).Invoke(panel, null);

                Assert.IsTrue((bool)hudType.GetProperty("IsVisible").GetValue(hud));
                string hudText = (string)hudType.GetProperty("VisibleText").GetValue(hud);
                StringAssert.Contains("Quest", hudText);
                Assert.LessOrEqual(hudGo.GetComponentsInChildren<Text>(true).Length, 4, "Tracked HUD must remain a compact single-objective surface.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ModernUiPanelInputRouter_TogglesObjectiveJournalWithoutStackingFullPanels()
        {
            var routerType = Type.GetType("Rootborn.UI.Modern.ModernUiPanelInputRouter, Rootborn.UI");
            var panelType = Type.GetType("Rootborn.UI.Objectives.ObjectiveJournalPanel, Rootborn.UI");
            Assert.IsNotNull(routerType, "Missing ModernUiPanelInputRouter.");
            Assert.IsNotNull(panelType, "Missing ObjectiveJournalPanel.");
            Assert.IsNotNull(routerType.GetMethod("ToggleObjectiveJournalPanel", BindingFlags.Instance | BindingFlags.Public));

            var host = new GameObject("ObjectiveRouterFixture", typeof(RectTransform));
            try
            {
                var inventory = host.AddComponent<ModernUiInventoryPanel>();
                var status = host.AddComponent<ModernUiStatusPanel>();
                var settings = host.AddComponent<SettingsPanel>();
                var journal = (Component)host.AddComponent(panelType);
                var router = (Component)host.AddComponent(routerType);

                var bind = routerType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .FirstOrDefault(method => method.Name == "Bind" && method.GetParameters().Length == 4);
                Assert.IsNotNull(bind, "Router must expose Bind(inventory, status, settings, objectiveJournal).");
                bind.Invoke(router, new object[] { inventory, status, settings, journal });

                inventory.Show();
                Assert.IsTrue(inventory.IsVisible);

                routerType.GetMethod("ToggleObjectiveJournalPanel").Invoke(router, null);

                Assert.IsFalse(inventory.IsVisible, "Opening Objective Journal must close inventory.");
                Assert.IsFalse(status.IsVisible, "Opening Objective Journal must close status.");
                Assert.IsFalse(settings.IsVisible, "Opening Objective Journal must close settings.");
                Assert.IsTrue((bool)panelType.GetProperty("IsVisible").GetValue(journal));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ModernUiPanelAutoInstaller_InstallsObjectiveJournalHiddenUntilTabToggle()
        {
            var canvasGo = new GameObject("ObjectiveJournalAutoInstallFixture", typeof(RectTransform), typeof(Canvas));
            try
            {
                var canvas = canvasGo.GetComponent<Canvas>();
                ModernUiPanelAutoInstaller.InstallOnCanvas(canvas, null);

                var journal = canvasGo.GetComponentInChildren<Rootborn.UI.Objectives.ObjectiveJournalPanel>(true);
                var router = canvasGo.GetComponent<ModernUiPanelInputRouter>();
                Assert.IsNotNull(journal, "Auto installer must create the Objective Journal panel.");
                Assert.IsNotNull(router, "Auto installer must create the input router.");
                Assert.IsFalse(journal.IsVisible, "Objective Journal must not start visible in the middle of gameplay.");
                Assert.IsFalse(journal.gameObject.activeSelf, "Objective Journal GameObject must stay inactive until the player presses Tab.");

                router.ToggleObjectiveJournalPanel();

                Assert.IsTrue(journal.gameObject.activeSelf, "Tab toggle must activate the hidden Objective Journal GameObject.");
                Assert.IsTrue(journal.IsVisible, "Tab toggle must show the Objective Journal.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvasGo);
            }
        }

        // Town's first canvas can be a display-only HUD canvas without a raycaster; the journal and inventory on it
        // must still receive mouse clicks (QUEST_DIALOGUE_E2E_001 caught this after the old House upgrade canvas was deleted).
        [Test]
        public void ModernUiPanelAutoInstaller_HostCanvasWithoutRaycasterGetsOneSoPanelsTakeClicks()
        {
            var canvasGo = new GameObject("DisplayOnlyHudCanvasFixture", typeof(RectTransform), typeof(Canvas));
            try
            {
                Assert.IsNull(canvasGo.GetComponent<GraphicRaycaster>());
                ModernUiPanelAutoInstaller.InstallOnCanvas(canvasGo.GetComponent<Canvas>(), null);
                Assert.IsNotNull(canvasGo.GetComponent<GraphicRaycaster>(), "Interactive panels need a GraphicRaycaster on their host canvas.");
                ModernUiPanelAutoInstaller.InstallOnCanvas(canvasGo.GetComponent<Canvas>(), null);
                Assert.AreEqual(1, canvasGo.GetComponents<GraphicRaycaster>().Length, "Reinstalling must not stack raycasters.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvasGo);
            }
        }

        // A detail action (the quest "Claim reward" button) shows only for items that supply one, runs once per press,
        // keeps its message on the same item after the owner rebuilds items, and asks the owner to rebuild.
        [Test]
        public void ObjectiveJournalPanel_DetailActionButtonShowsOnlyForItemsWithAnActionAndRunsIt()
        {
            var host = new GameObject("ObjectiveJournalActionFixture", typeof(RectTransform));
            try
            {
                int runs = 0;
                int completed = 0;
                var action = new ObjectiveJournalAction("Claim reward", () => { runs++; return new ObjectiveJournalActionResult(true, "Reward received."); });
                var withAction = new ObjectiveJournalItem("quest.done", "Quests", "Done quest", "", "Completed", "1 / 1", "", false, 10, action);
                var plain = new ObjectiveJournalItem("quest.active", "Quests", "Active quest", "", "Active", "0 / 1", "", false, 20);
                var panel = host.AddComponent<ObjectiveJournalPanel>();
                panel.ItemActionCompleted += () => completed++;
                panel.SetItems("Quests", new[] { withAction, plain });
                panel.Show();
                panel.SelectCategory("Quests");

                Assert.IsNotNull(panel.DetailActionButton, "A completed quest shows its claim button.");
                StringAssert.Contains("Claim reward", panel.VisibleText);
                panel.DetailActionButton.GetComponent<Button>().onClick.Invoke();
                Assert.AreEqual(1, runs);
                Assert.AreEqual(1, completed, "The owner is asked to rebuild items after the action.");
                StringAssert.Contains("Reward received.", panel.VisibleText);

                panel.SetItems("Quests", new[] { new ObjectiveJournalItem("quest.done", "Quests", "Done quest", "", "RewardClaimed", "1 / 1", "", false, 60), plain });
                Assert.IsNull(panel.DetailActionButton, "A claimed quest no longer shows the button.");
                StringAssert.Contains("Reward received.", panel.VisibleText, "The message stays on the item it belongs to.");

                panel.SelectItem(1);
                Assert.IsNull(panel.DetailActionButton, "An item without an action shows no button.");
                StringAssert.DoesNotContain("Reward received.", panel.VisibleText, "Another item does not show the message.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static int CountObjectiveRows(Transform root)
        {
            return root.GetComponentsInChildren<Button>(true).Count(button => button.name.StartsWith("ObjectiveRow_", StringComparison.Ordinal));
        }

        private static int CountCommonPanel48(GameObject root)
        {
            return root.GetComponentsInChildren<ModernUiTileImage>(true)
                .Count(tile => tile.Recipe == ModernUiRecipes.CommonPanel48);
        }

        private static void AssertChildHasTiles(Transform root, string name)
        {
            var child = root.Find(name);
            Assert.IsNotNull(child, "Missing " + name + ".");
            var tileImage = child.GetComponent<ModernUiTileImage>();
            Assert.IsNotNull(tileImage, name + " must use ModernUiTileImage.");
            Assert.AreSame(ModernUiRecipes.CommonPanel48, tileImage.Recipe, name + " must use CommonPanel48.");
            Assert.Greater(tileImage.TileCount, 0, name + " must build Style2 tiled sprites.");
        }

        private static void AssertPlainChild(Transform root, string name)
        {
            var child = root.Find(name);
            Assert.IsNotNull(child, "Missing " + name + ".");
            Assert.IsNull(child.GetComponent<ModernUiTileImage>(), name + " must be plain content inside the outer CommonPanel, not another CommonPanel.");
        }
    }
}
