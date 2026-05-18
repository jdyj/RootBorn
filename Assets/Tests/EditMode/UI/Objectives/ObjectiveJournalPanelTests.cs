using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rootborn.UI.Modern;
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
