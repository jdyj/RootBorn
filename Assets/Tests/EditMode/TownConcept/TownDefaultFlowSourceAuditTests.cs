using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode.TownConcept
{
    public sealed class TownDefaultFlowSourceAuditTests
    {
        [Test]
        public void MainMenuAndBootstrap_DefaultUserFlowReachesTown()
        {
            AssertDefaultTownScene("Assets/Scripts/UI/MainMenu/ModeSelectPanel.cs");
            AssertDefaultTownScene("Assets/Scripts/UI/MainMenu/SaveSlotSelectPanel.cs");
            AssertBootstrapLoadsMainMenu("Assets/Scripts/Game/Bootstrap/GameBootstrap.cs");
        }

        [Test]
        public void MainMenuSceneEventSystem_HasUiInputModuleForPointerClicks()
        {
            string scene = File.ReadAllText("Assets/Scenes/MainMenu.unity");

            StringAssert.Contains("m_Name: EventSystem", scene, "MainMenu must contain an EventSystem for UI pointer routing.");
            StringAssert.Contains("Unity.InputSystem::UnityEngine.InputSystem.UI.InputSystemUIInputModule", scene, "MainMenu EventSystem must serialize a real UI input module so Boot -> MainMenu mouse clicks work.");
        }

        [Test]
        public void SceneSetup_RepairsExistingEventSystemWithoutInputModule()
        {
            string source = File.ReadAllText("Assets/Scripts/Editor/Tools/SceneSetup.cs");

            StringAssert.Contains("BaseInputModule", source, "Scene setup must detect an EventSystem that exists without a UI input module.");
            StringAssert.Contains("UiInputModuleInstaller.AddPreferredInputModule", source, "Scene setup should use the shared input module installer instead of only fixing newly-created EventSystems.");
        }

        [Test]
        public void ModernUiPanelAutoInstaller_RunsOnPlayableScenesExceptBootAndMainMenu()
        {
            string source = File.ReadAllText("Assets/Scripts/UI/Modern/ModernUiPanelAutoInstaller.cs");

            StringAssert.Contains("BootSceneName", source);
            StringAssert.Contains("MainMenuSceneName", source);
            StringAssert.Contains("ShouldInstallForScene", source);
            StringAssert.Contains("scene.isLoaded", source);
            StringAssert.Contains("scene.name != BootSceneName", source);
            StringAssert.Contains("scene.name != MainMenuSceneName", source);
            StringAssert.Contains("SettingsPanelName", source);
            StringAssert.Contains("ScreenSpaceOverlay", source);
            StringAssert.DoesNotContain("TownSceneName", source);
            StringAssert.DoesNotContain("scene.name == \"Town\"", source);
        }

        [Test]
        public void TownPlayableBaselineRuntimeInstaller_IsTownScopedAndDoesNotCallFarmAutoFiller()
        {
            string source = File.ReadAllText("Assets/Scripts/Game/Bootstrap/TownPlayableBaselineRuntimeInstaller.cs");

            StringAssert.Contains("TownSceneName", source);
            StringAssert.Contains("\"Town\"", source);
            StringAssert.DoesNotContain("FarmSceneName", source);
            StringAssert.DoesNotContain("FarmAutoFiller", source);
            StringAssert.DoesNotContain("GameObject.Find", source);
            StringAssert.DoesNotContain("FindObjectOfType", source);
            StringAssert.DoesNotContain("Resources.Load", source);
        }

        [Test]
        public void TownPlayableBaselineRuntimeInstaller_EnsuresEventSystemInputModuleForClickableUi()
        {
            string source = File.ReadAllText("Assets/Scripts/Game/Bootstrap/TownPlayableBaselineRuntimeInstaller.cs");
            string installer = File.ReadAllText("Assets/Scripts/Game/Common/PassiveInputModule.cs");

            StringAssert.Contains("UiInputModuleInstaller.AddPreferredInputModule", source, "Town UI buttons need a UI input module installer for real pointer clicks.");
            StringAssert.Contains("BaseInputModule", source, "Town should repair an EventSystem that exists without an input module.");
            StringAssert.Contains("InputSystemUIInputModule", installer, "The runtime installer helper must default to the real UI input module for editor play and builds.");
        }

        [Test]
        public void RuntimeEventSystemInstallers_DoNotUseTestCompileSymbolForClickableUi()
        {
            AssertRuntimeEventSystemInstaller("Assets/Scripts/UI/MainMenu/SaveSlotSelectPanel.cs");
            AssertRuntimeEventSystemInstaller("Assets/Scripts/UI/Quests/QuestHudAutoFiller.cs");
            AssertRuntimeEventSystemInstaller("Assets/Scripts/Game/Bootstrap/TownPlayableBaselineRuntimeInstaller.cs");
            AssertRuntimeEventSystemInstaller("Assets/Scripts/Game/Bootstrap/FarmAutoFiller.cs");
        }

        [Test]
        public void PlayModeInputGuard_DoesNotAutoRunDuringNormalEditorPlay()
        {
            string source = File.ReadAllText("Assets/Tests/PlayMode/InputSystemUiModulePlayModeTestGuard.cs");

            StringAssert.DoesNotContain("RuntimeInitializeOnLoadMethod", source, "A PlayMode test guard must not auto-run in normal editor Play and remove clickable UI modules.");
            StringAssert.Contains("InstallForCurrentTest", source, "Input guard should be opt-in from tests that need InputTestFixture isolation.");
        }

        [Test]
        public void MainMenuButtonLabels_DoNotInterceptPointerRaycasts()
        {
            string modeSource = File.ReadAllText("Assets/Scripts/UI/MainMenu/ModeSelectPanel.cs");
            string slotSource = File.ReadAllText("Assets/Scripts/UI/MainMenu/SaveSlotSelectPanel.cs");

            StringAssert.Contains("DisableButtonLabelRaycasts", modeSource, "Main menu scene labels must not be the top raycast target over buttons.");
            StringAssert.Contains("raycastTarget = false", modeSource, "Mode button labels should pass pointer events to their Button.");
            StringAssert.Contains("raycastTarget = false", slotSource, "Procedural save slot labels should pass pointer events to their Button.");
        }

        private static void AssertDefaultTownScene(string path)
        {
            string source = File.ReadAllText(path);

            StringAssert.Contains("\"Town\"", source, path + " should name Town as the default playable scene.");
            StringAssert.DoesNotContain("_farmScene = \"Farm\"", source, path + " should not default to Farm.");
            StringAssert.DoesNotContain("LoadScene(_farmScene)", source, path + " should not load Farm through the old default field.");
        }

        private static void AssertBootstrapLoadsMainMenu(string path)
        {
            string source = File.ReadAllText(path);

            StringAssert.Contains("\"MainMenu\"", source, path + " should enter the player-facing main menu before save slot/SPUM/Town flow.");
            StringAssert.Contains("LoadScene(_mainMenuScene)", source, path + " should load the configured main menu scene.");
            StringAssert.DoesNotContain("_farmScene = \"Farm\"", source, path + " should not default to Farm.");
        }

        private static void AssertRuntimeEventSystemInstaller(string path)
        {
            string source = File.ReadAllText(path);
            string installer = File.ReadAllText("Assets/Scripts/Game/Common/PassiveInputModule.cs");

            StringAssert.Contains("UiInputModuleInstaller.AddPreferredInputModule", source, path + " must install the shared UI input module helper.");
            StringAssert.Contains("InputSystemUIInputModule", installer, "UiInputModuleInstaller must install the real UI input module by default.");
            StringAssert.DoesNotContain("UNITY_INCLUDE_TESTS", source, path + " must not swap clickable UI to a test-only passive module in runtime code.");
            StringAssert.DoesNotContain("PassiveInputModule.AddComponent", source, path + " must not directly install a no-op input module in runtime code.");
        }
    }
}
