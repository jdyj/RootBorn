using System.Collections;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.StudentLife;
using Rootborn.UI.Modern;
using Rootborn.UI.Objectives;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.Objectives
{
    public sealed class ObjectiveJournalInputPlayModeTests : InputTestFixture
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

                Directory.CreateDirectory(Path.GetDirectoryName(ScreenshotPath));
                if (File.Exists(ScreenshotPath)) File.Delete(ScreenshotPath);
                ScreenCapture.CaptureScreenshot(ScreenshotPath);
                for (int i = 0; i < 5; i++) yield return null;
                Assert.IsTrue(File.Exists(ScreenshotPath), "PlayMode visual evidence screenshot must be captured for Objective Journal.");
            }
            finally
            {
                Object.Destroy(canvasGo);
                Object.Destroy(playerGo);
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
    }
}
