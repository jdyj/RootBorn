using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode
{
    public sealed class ModernUiPanelAutoInstallerSourceTests
    {
        private const string SourcePath = "Assets/Scripts/UI/Modern/ModernUiPanelAutoInstaller.cs";

        [Test]
        public void AutoInstaller_AttachesPanelsRouterAndBindsPlayerInventoryWithoutGlobalFindCalls()
        {
            Assert.IsTrue(File.Exists(SourcePath), "Missing ModernUiPanelAutoInstaller source.");
            string source = File.ReadAllText(SourcePath);

            StringAssert.Contains("RuntimeInitializeOnLoadMethod", source);
            StringAssert.Contains("SceneManager.sceneLoaded", source);
            StringAssert.Contains("InstallOnCanvas", source);
            StringAssert.Contains("ModernUiInventoryPanel", source);
            StringAssert.Contains("ModernUiStatusPanel", source);
            StringAssert.Contains("SettingsPanel", source);
            StringAssert.Contains("ModernUiPanelInputRouter", source);
            StringAssert.Contains("inventoryPanel.Bind(playerInventory)", source);
            StringAssert.Contains("router.Bind(inventoryPanel, statusPanel, settingsPanel)", source);
            StringAssert.DoesNotContain("GameObject.Find", source);
            StringAssert.DoesNotContain("FindObjectOfType", source);
            StringAssert.DoesNotContain("FindFirstObjectByType", source);
            StringAssert.DoesNotContain("Resources.Load", source);
        }
    }
}