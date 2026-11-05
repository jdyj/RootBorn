using System.Collections;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.UI.Modern;
using Rootborn.UI.Objectives;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.Objectives
{
    public sealed class ObjectiveJournalInputPlayModeTests : Rootborn.Tests.PlayMode.PlayModeInputFixture
    {
        private const string ScreenshotPath = "Builds/Logs/objective-journal-playmode.png";

        [UnityTest]
        public IEnumerator OBJECTIVE_JOURNAL_PM_001_TabKeyboardInputOpensObjectiveJournalAndClosesInventory()
        {
            var canvasGo = new GameObject("ObjectiveJournalInputCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(ModernUiPanelInputRouter));
            var playerGo = new GameObject("Player");
            try
            {
                canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var inventoryGo = MakeChild(canvasGo.transform, "ModernInventoryPanel");
                var statusGo = MakeChild(canvasGo.transform, "ModernStatusPanel");
                var settingsGo = MakeChild(canvasGo.transform, "ModernSettingsPanel");
                var journalGo = MakeChild(canvasGo.transform, "ObjectiveJournalPanel");
                var hudGo = MakeChild(canvasGo.transform, "TrackedObjectiveHud");

                var studentLife = playerGo.AddComponent<StudentLifeProgressComponent>();
                studentLife.ConfigureForTests("objective-journal-flow", "player", 9, 7, 0, 8 * 60);

                var inventory = inventoryGo.AddComponent<ModernUiInventoryPanel>();
                var status = statusGo.AddComponent<ModernUiStatusPanel>();
                var settings = settingsGo.AddComponent<SettingsPanel>();
                var journal = journalGo.AddComponent<ObjectiveJournalPanel>();
                var hud = hudGo.AddComponent<TrackedObjectiveHud>();
                journal.BindTrackedHud(hud);

                var provider = canvasGo.AddComponent<ObjectiveJournalRuntimeProvider>();
                provider.Bind(journal, hud, playerGo);

                var router = canvasGo.GetComponent<ModernUiPanelInputRouter>();
                router.Bind(inventory, status, settings, journal);
                inventory.Show();
                status.Hide();
                settings.Hide();
                journal.Hide();

                Assert.IsTrue(inventory.IsVisible, "Inventory starts visible so Tab must prove full-panel stacking is closed.");
                Assert.IsFalse(journal.IsVisible);

                yield return HoldKeyboardKey(Key.Tab, 3);

                Assert.IsFalse(inventory.IsVisible, "Actual Tab keyboard input must close inventory before opening Objective Journal.");
                Assert.IsTrue(journal.IsVisible, "Actual Tab keyboard input must open Objective Journal through ModernUiPanelInputRouter.");
                StringAssert.Contains("Goals", journal.VisibleText);
                StringAssert.Contains("Quests", journal.VisibleText);
                StringAssert.Contains("Campaign", journal.VisibleText);
                StringAssert.Contains("Student Day Guide", journal.VisibleText);
                StringAssert.Contains("Energy 9", journal.VisibleText);

                CaptureScreenshot();
                yield return WaitForScreenshot();
                Assert.IsTrue(File.Exists(ScreenshotPath), "PlayMode visual evidence screenshot must be captured for Objective Journal.");
            }
            finally
            {
                Object.Destroy(canvasGo);
                Object.Destroy(playerGo);
            }
        }

        [UnityTest]
        public IEnumerator OBJECTIVE_JOURNAL_PM_002_RowClickSelectsDetailAndTrackClickUpdatesHud()
        {
            string rootDirectory = Path.Combine(Application.temporaryCachePath, "rootborn-objective-journal-row-click", System.Guid.NewGuid().ToString("N"));
            SaveService.SetRootDirectoryForTests(rootDirectory);
            ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = "slot-row-click", DisplayName = "Row Click" });
            EnsurePassiveEventSystem();

            var canvasGo = new GameObject("ObjectiveJournalRowClickCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            try
            {
                canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var journalGo = MakeChild(canvasGo.transform, "ObjectiveJournalPanel");
                var hudGo = MakeChild(canvasGo.transform, "TrackedObjectiveHud");
                var journal = journalGo.AddComponent<ObjectiveJournalPanel>();
                var hud = hudGo.AddComponent<TrackedObjectiveHud>();
                journal.BindTrackedHud(hud);
                journal.SetItems("Quests", new[]
                {
                    new ObjectiveJournalItem("quest.first", "Quests", "First Quest", "First detail", "Active", "0 / 1", "Continue first", false, 0),
                    new ObjectiveJournalItem("quest.second", "Quests", "Second Quest", "Second detail", "Active", "1 / 2", "Continue second", false, 1),
                });

                journal.Show();
                journal.SelectCategory("Quests");
                yield return null;

                yield return ClickButton(FindButton(journal.transform, "ObjectiveRow_1"));

                StringAssert.Contains("Second Quest", journal.VisibleText);
                StringAssert.Contains("Second detail", journal.VisibleText);
                StringAssert.Contains("1 / 2", journal.VisibleText);

                yield return ClickButton(FindButton(journal.transform, "ObjectiveTrackButton"));

                Assert.IsTrue(hud.IsVisible, "Track click must update the single TrackedObjectiveHud.");
                StringAssert.Contains("Second Quest", hud.VisibleText);
                Assert.IsTrue(ObjectiveJournalTrackingStore.TryLoad(ActiveSaveContext.Metadata, out var categoryId, out var stableKey));
                Assert.AreEqual("Quests", categoryId);
                Assert.AreEqual("quest.second", stableKey);

                CaptureScreenshot();
                yield return WaitForScreenshot();
                Assert.IsTrue(File.Exists(ScreenshotPath), "PlayMode screenshot must capture row-click/track visual state.");
            }
            finally
            {
                Object.Destroy(canvasGo);
                ActiveSaveContext.Clear();
                SaveService.SetRootDirectoryForTests(null);
                if (Directory.Exists(rootDirectory)) Directory.Delete(rootDirectory, true);
            }
        }

        private static GameObject MakeChild(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static IEnumerator HoldKeyboardKey(Key key, int frameCount)
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            InputSystem.Update();
            for (int i = 0; i < frameCount; i++)
            {
                yield return null;
            }

            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            InputSystem.RemoveDevice(keyboard);
        }

        private static IEnumerator ClickButton(Button button)
        {
            Assert.IsNotNull(button);
            Assert.IsTrue(button.gameObject.activeInHierarchy, button.name + " must be active for user click.");
            Assert.IsTrue(button.interactable, button.name + " must be interactable for user click.");
            ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
            yield return null;
        }

        private static Button FindButton(Transform root, string name)
        {
            if (root.name == name && root.TryGetComponent<Button>(out var button)) return button;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindButton(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        private static void CaptureScreenshot()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScreenshotPath));
            if (File.Exists(ScreenshotPath)) File.Delete(ScreenshotPath);
            ScreenCapture.CaptureScreenshot(ScreenshotPath);
        }

        private static IEnumerator WaitForScreenshot()
        {
            for (int i = 0; i < 10 && !File.Exists(ScreenshotPath); i++)
            {
                yield return null;
            }
        }

        private static void EnsurePassiveEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            go.AddComponent<PassiveInputModule>();
        }
    }
}
