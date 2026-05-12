using System.Collections;
using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Bootstrap;
using Rootborn.Game.Managers;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode
{
    public sealed class ModernUiPanelAutoInstallerPlayModeTests
    {
        [UnityTest]
        public IEnumerator TownScene_AutoInstallerAttachesModernPanelsRouterAndSupportsToggles()
        {
            yield return LoadTownAndBootstrapRuntime();

            ModernUiPanelInputRouter router = null;
            ModernUiInventoryPanel inventoryPanel = null;
            ModernUiStatusPanel statusPanel = null;
            yield return WaitUntilInstalled(
                resolvedRouter => router = resolvedRouter,
                resolvedInventory => inventoryPanel = resolvedInventory,
                resolvedStatus => statusPanel = resolvedStatus);

            Assert.IsNotNull(router, "Town canvas should receive ModernUiPanelInputRouter at runtime.");
            Assert.IsNotNull(inventoryPanel, "Town canvas should receive ModernUiInventoryPanel at runtime.");
            Assert.IsNotNull(statusPanel, "Town canvas should receive ModernUiStatusPanel at runtime.");
            Assert.IsNotNull(router.GetComponent<Canvas>(), "Router should be attached to the Town UI canvas host.");
            Assert.IsFalse(inventoryPanel.IsVisible, "Inventory starts hidden after install.");
            Assert.IsFalse(statusPanel.IsVisible, "Status starts hidden after install.");

            router.ToggleInventoryPanel();
            yield return null;
            Assert.IsTrue(inventoryPanel.IsVisible, "I-route toggle should show inventory.");
            Assert.IsFalse(statusPanel.IsVisible, "Inventory toggle should not show status.");
            Assert.GreaterOrEqual(inventoryPanel.GetComponentsInChildren<ModernUiTileImage>(true).Sum(tile => tile.TileCount), 80);

            router.ToggleStatusPanel();
            yield return null;
            Assert.IsFalse(inventoryPanel.IsVisible, "Status toggle should close inventory.");
            Assert.IsTrue(statusPanel.IsVisible, "TAB-route toggle should show status.");
            Assert.GreaterOrEqual(statusPanel.GetComponentsInChildren<ModernUiTileImage>(true).Sum(tile => tile.TileCount), 60);
        }

        private static IEnumerator LoadTownAndBootstrapRuntime()
        {
            var asyncLoad = SceneManager.LoadSceneAsync("Town", LoadSceneMode.Single);
            while (!asyncLoad.isDone)
            {
                yield return null;
            }

            var bootstrapTask = Managers.BootstrapAsync();
            float elapsed = 0f;
            while (!bootstrapTask.IsCompleted && elapsed < 15f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.IsTrue(bootstrapTask.IsCompleted, "Managers.BootstrapAsync did not complete within timeout.");
            if (bootstrapTask.Exception != null)
            {
                throw bootstrapTask.Exception;
            }

            var farmFiller = new GameObject("[FarmAutoFiller]");
            farmFiller.AddComponent<FarmAutoFiller>();
        }

        private static IEnumerator WaitUntilInstalled(
            System.Action<ModernUiPanelInputRouter> setRouter,
            System.Action<ModernUiInventoryPanel> setInventory,
            System.Action<ModernUiStatusPanel> setStatus)
        {
            float elapsed = 0f;
            while (elapsed < 20f)
            {
                foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                {
                    var router = canvas.GetComponent<ModernUiPanelInputRouter>();
                    var inventory = canvas.GetComponentInChildren<ModernUiInventoryPanel>(true);
                    var status = canvas.GetComponentInChildren<ModernUiStatusPanel>(true);
                    if (router != null && inventory != null && status != null)
                    {
                        setRouter(router);
                        setInventory(inventory);
                        setStatus(status);
                        yield break;
                    }
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }
}
