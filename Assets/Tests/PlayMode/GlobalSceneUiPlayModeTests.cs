using System.Collections;
using NUnit.Framework;
using Rootborn.UI.Modern;
using Rootborn.UI.Quests;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode
{
    [SetUpFixture]
    internal sealed class InputSystemUiModuleGuardFixture
    {
        [OneTimeSetUp]
        public void OneTimeSetUp() => InputSystemUiModulePlayModeTestGuard.InstallForCurrentTest();

        [OneTimeTearDown]
        public void OneTimeTearDown() => InputSystemUiModulePlayModeTestGuard.UninstallForCurrentTest();
    }

    public sealed class GlobalSceneUiPlayModeTests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator GLOBAL_UI_PM_001_TownEscKeyboardEventOpensSettingsPanelOnOverlayCanvas()
        {
            yield return SceneManager.LoadSceneAsync("Town", LoadSceneMode.Single);
            yield return WaitForGlobalUi();

            var canvas = FindGlobalUiCanvas();
            var router = canvas.GetComponent<ModernUiPanelInputRouter>();
            var settings = FindModernSettingsPanel(canvas);

            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
            Assert.GreaterOrEqual(canvas.sortingOrder, 1000);
            Assert.IsNotNull(router);
            Assert.IsNotNull(settings);
            Assert.IsFalse(settings.IsVisible);

            yield return HoldKeyboardKey(Key.Escape, 4);

            Assert.IsTrue(settings.IsVisible, "Actual Esc keyboard input should open the same Modern UI panel in Town.");
            Assert.GreaterOrEqual(settings.PanelTileCount, 9);
        }

        [UnityTest]
        public IEnumerator GLOBAL_UI_PM_002_FarmEscKeyboardEventOpensSameSettingsPanelOnOverlayCanvas()
        {
            yield return SceneManager.LoadSceneAsync("Farm", LoadSceneMode.Single);
            yield return WaitForGlobalUi();

            var canvas = FindGlobalUiCanvas();
            var router = canvas.GetComponent<ModernUiPanelInputRouter>();
            var settings = FindModernSettingsPanel(canvas);

            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
            Assert.GreaterOrEqual(canvas.sortingOrder, 1000);
            Assert.IsNotNull(router);
            Assert.IsNotNull(settings);
            Assert.IsFalse(settings.IsVisible);

            yield return HoldKeyboardKey(Key.Escape, 4);

            Assert.IsTrue(settings.IsVisible, "Actual Esc keyboard input should open through the global Modern UI router in Farm.");
            Assert.GreaterOrEqual(settings.PanelTileCount, 9);
        }

        [UnityTest]
        public IEnumerator GLOBAL_UI_PM_003_TownQuestLogPanelRendersOnTopOverlayCanvas()
        {
            yield return SceneManager.LoadSceneAsync("Town", LoadSceneMode.Single);
            yield return WaitForQuestPanel();

            var canvas = FindGlobalUiCanvas();
            var questPanel = Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include);

            Assert.IsNotNull(canvas);
            Assert.IsNotNull(questPanel);
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
            Assert.GreaterOrEqual(canvas.sortingOrder, 1000);
            Assert.AreSame(canvas.transform, questPanel.transform.parent);
            Assert.IsTrue(questPanel.gameObject.activeInHierarchy, "QuestLogPanel should not be hidden behind world sprites or disabled in Town.");
        }

        private static IEnumerator WaitForGlobalUi()
        {
            for (int i = 0; i < 600; i++)
            {
                if (FindGlobalUiCanvas() != null)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("Global Modern UI did not install router and SettingsPanel within timeout.");
        }

        private static IEnumerator WaitForQuestPanel()
        {
            for (int i = 0; i < 600; i++)
            {
                if (Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include) != null && FindGlobalUiCanvas() != null)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("QuestLogPanel did not install within timeout.");
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
            yield return null;
            InputSystem.RemoveDevice(keyboard);
        }

        private static Canvas FindGlobalUiCanvas()
        {
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                var canvas = canvases[i];
                if (canvas != null &&
                    canvas.GetComponent<ModernUiPanelInputRouter>() != null &&
                    FindModernSettingsPanel(canvas) != null)
                {
                    return canvas;
                }
            }

            return null;
        }

        private static SettingsPanel FindModernSettingsPanel(Canvas canvas)
        {
            if (canvas == null)
            {
                return null;
            }

            var child = canvas.transform.Find("ModernSettingsPanel");
            return child != null ? child.GetComponent<SettingsPanel>() : null;
        }
    }
}
